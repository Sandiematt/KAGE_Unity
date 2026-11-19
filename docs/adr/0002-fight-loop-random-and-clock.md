# ADR-0002: Fight context, fight loop, seeded random source and fight clock

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-61 (builds it), SAN-65, SAN-83, SAN-92, SAN-93, SAN-75, every fight-harness test

## Context

The spec needs headless fight tests: build a fight from data, drive it with scripted input under a fixed seed, step a fixed number of frames, and get identical event logs twice. Today that is not possible:

- Crits use `UnityEngine.Random.value` (`Fighter.TryTarget`). `AIInputSource` has its own `System.Random(1234)` per instance.
- Each `Fighter` runs its own `FixedUpdate`. The order between two fighters is whatever Unity picks, and a hit is resolved inside the attacker's own update.
- Hitstop and slow motion (`GameFeel`) are measured in **unscaled wall-clock seconds** and work by setting `Time.timeScale`. In a test the number of frames frozen depends on the machine.
- `Crescent` moves and hits in `Update` with `Time.deltaTime`, not on the 60 Hz step.
- Some code assumes `Vfx.I` and `KageGame.I` exist (`Crescent` calls `Vfx.I.Echo` without a null check).

Weapon clashes (SAN-93) need both fighters' blade paths from the same frame before any hit is resolved. Multiple targets (SAN-83) and stretches need more than two fighters.

## Decision

### Fight context

One `FightContext` per fight holds the per-fight services. Nothing in a fight reaches for a static to get them.

```csharp
public sealed class FightContext
{
    public readonly int Seed;
    public readonly FightRandom Random;
    public readonly FightClock Clock;
    public readonly CombatEvents Events;              // ADR-0003
    public readonly List<Fighter> Fighters;           // spawn order = tick order
    public readonly List<IHittable> Props;            // training bag, later breakables
}
```

- `Fighter` gets `public FightContext ctx`, `public int team` (0 = Kage's side, 1 = foes) and `public string FighterId` (character id, plus `#n` for the n-th copy: `order_footman#2`; Kage is `kage`; the bag is `bag`).
- Hits never land on a fighter of the same team. Projectiles and hazards use their owner's team.
- `Fighter.opponent` stays, and means **the current target**. With one foe it is that foe. From SAN-83 it is chosen every frame by the "nearest in front" rule from the fighter's target list. Move selection, AI and facing keep reading `opponent`, so weapon families and AI don't change when multiple targets land.
- `FightBuilder` (SAN-61; data-driven from SAN-66) creates the context, spawns fighters into it and wires input sources. `KageGame` and `GameFlow` both use it.

### Fight loop

`FightLoop` (a `MonoBehaviour`, `[DefaultExecutionOrder(-40)]`) owns the 60 Hz step. Fighters no longer have their own `FixedUpdate`; the loop calls them in `ctx.Fighters` order. One call to `FightLoop.Step()` is one **frame**:

1. If the clock says this frame is frozen by hitstop, advance the clock and stop.
2. **Input:** poll every fighter's `IInputSource` in fighter order.
3. **Fighter tick:** each fighter runs its state machine, root motion and move playback, poses its rig at the exact logic frame, and records a `HitSweep` for each active hit point (owner, window, previous and current position, radius).
4. **Clashes:** `partial void ResolveClashes(List<HitSweep> sweeps)`. Declared by SAN-65, implemented by SAN-93 in `FightLoop.Clash.cs`.
5. **Hits:** resolve every sweep in fighter order against the hurtbox layer (SAN-65). A sweep whose owner was interrupted earlier in this phase (no longer in that move) is dropped, so the first fighter to hit still wins a trade, as today.
6. **Projectiles and hazards** tick (SAN-75).
7. **Late tick:** sessions (stretch waves, boss phases, round logic) react to what happened this frame. Gameplay reactions to events are queued to here, never applied inside step 5.
8. `Clock.Frame++`.

Unity's 2D physics simulation runs after the loop: automatically in play, or by the harness with `Physics2D.Simulate(1f / 60f)` when `Physics2D.simulationMode` is `Script`.

SAN-61 introduces the loop with steps 2, 3 and 8, and hits still resolve inline inside step 3 exactly as today, so behaviour doesn't change. SAN-65 moves hit resolution to step 5. Its acceptance already allows listed and justified one-frame differences.

### Seeded random source

```csharp
public sealed class FightRandom
{
    public FightRandom(int seed);
    public FightRandom Stream(string name);   // independent generator from (seed, name)
    public float Value();                      // [0, 1)
    public int Range(int minInclusive, int maxExclusive);
    public float Range(float min, float max);
    public bool Chance(float p);
}
```

- The generator is our own (for example SplitMix64 or xorshift128+), not `System.Random` or `UnityEngine.Random`, so a seed gives the same numbers on every platform and Unity version.
- Every **gameplay** roll goes through a named stream from `ctx.Random`: `"crit"`, `"ai/<FighterId>"`, `"spawn"`, `"boss/<FighterId>"`. Separate streams mean that adding a roll to one AI doesn't shift every crit in the baseline replay.
- **Cosmetic** randomness stays on `UnityEngine.Random`: VFX, audio pitch, floating text jitter, ragdoll impulse jitter after a KO. Nothing cosmetic may change a fight's outcome.
- In play, the seed comes from `Environment.TickCount` unless the dev arena fixes it. The seed is published in the `FightStarted` event (ADR-0003), so any fight can be replayed.

### Fight clock

```csharp
public sealed class FightClock
{
    public int Frame { get; }
    public bool Manual { get; }                   // true in the headless harness
    public void Hitstop(int frames);
    public void SlowMo(float scale, int frames);  // whole-screen slow motion (KO, finishers)
    public float WorldScale { get; set; }         // 1 normal, 0.4 perfect dodge, 0 rage-art freeze (SAN-92)
    public bool IsExempt(Fighter f);              // the fighter that keeps full speed
}
```

- All combat timing is counted in frames at 60 Hz. Gameplay code uses `Time.fixedDeltaTime` (always 1/60) and frame counters. It never uses `Time.time`, `Time.deltaTime` or unscaled time. Pose blending and other visuals may use rendered time.
- `GameFeel.Hitstop` and `GameFeel.SlowMo` call sites don't change. In play (`Manual == false`), `GameFeel` works exactly as today. In the harness (`Manual == true`), `GameFeel` forwards the request to the active `FightClock`, which converts it to frames: hitstop of N frames skips N loop steps; slow motion at scale s runs a step on s of the frames. At 60 fps this is the same as today.
- **World time scale with an exemption** (built by SAN-92, reused by SAN-82): fighters, projectiles and hazards that are not exempt advance at `WorldScale`. A slowed fighter accumulates `WorldScale` each frame and runs its logic tick only when the total reaches 1. Its body velocity and gravity are scaled while slowed and restored afterwards. At 0 the body is held still (kinematic) and restored afterwards. The exempt fighter (Kage) runs normally. `Time.timeScale` stays reserved for hitstop and whole-screen slow motion.

### Determinism contract

With the same seed, the same scripted inputs and a manual clock, a fight produces an identical event log (ADR-0003), and positions match within 1e-4 m. Play mode is not required to be deterministic: input arrives between steps and hitstop uses wall-clock time there.

Headless fights must run with no camera, HUD, audio or `Vfx`. Code that calls `Vfx.I`, `KageGame.I`, `KageGame.I.Hud` or `KageGame.I.Audio` must null-check it (SAN-61 fixes the existing calls).

## Notes from the implementation (SAN-92: the world time scale)

**The API is a timed call, not a setter.** The sketch above had `WorldScale { get; set; }`; a perfect dodge also needs a length and the exempt fighter, so it is built as:

```csharp
// FightClock
public float WorldScale { get; }          // 1 normal, below 1 slow, 0 frozen
public Fighter WorldExempt { get; }       // keeps full speed, and so does what it owns
public bool WorldRuns { get; }            // the slowed world takes a frame on this loop frame
public int WorldFramesLeft { get; }       // -1 until ResumeWorld, 0 when normal
public float WorldPhase { get; }          // 0..1 into the slowed world's next frame (pose interpolation in play)
public void SlowWorld(float scale, int frames, Fighter exempt);   // frames < 0: until ResumeWorld; replaces a running one
public void ResumeWorld();
public bool IsExempt(Fighter f);          // true at scale 1, or for the exempt fighter
public float ScaleFor(Fighter owner);     // 1 when exempt, else WorldScale
public bool Advances(Fighter owner);      // WorldRuns || IsExempt(owner)
```

**How it is counted.** One accumulator for the whole world, in the clock: on each frame the loop runs, `BeginFrame` adds `WorldScale` and the world takes a frame when it reaches 1 (`WorldRuns`). Every slowed thing shares that one beat, so a slowed fighter and the projectile it threw stay in step. At 0.4 the pattern is 0.4, 0.8, **1.2**, 0.6, **1.0**, ...: 9 world frames in 24 (`WorldTimeScaleTests`). It counts only frames the loop runs, in play and on a manual clock alike: hitstop frames don't spend it, and play and the harness behave the same. A slow started during frame F covers F's physics step and the next `frames` loop frames; the frame after that is normal. Floats are summed the same way every run, so it is deterministic under the fight loop (`PerfectDodgeTests.A_fight_with_a_perfect_dodge_is_identical_every_run`).

**What slows, and how** (scale s, everything except `WorldExempt` and what it owns):

| | How | Where |
|---|---|---|
| Fighter logic (input, state machine, move playback, timers, stun, posture drain, AI) | polled and ticked only when `clock.Advances(f)`; skipped frames record no sweeps | `FightLoop.Step` |
| Fighter body | last thing in the frame: velocity × s and gravity scale × s² for the physics step (in world time x(t) = X(s·t): speed × s, acceleration × s²); first thing in the next frame: velocity ÷ s and gravity back, so all gameplay code (its tick, a knockback from Kage's hit, the push) reads and writes logic-unit velocities. At 0 the body is made kinematic and held, and gets its velocity back on resume | `Fighter.ScaleWorldTime` / `RestoreWorldTime` (`Fighter.WorldTime.cs`), called by the loop |
| Fighter pose | `LogicVelocity` (not the rigidbody's scaled one) drives the air pose and secondary motion; in play the pose interpolates by `WorldPhase` and secondary motion runs at s | `Fighter.PoseOutput` |
| Projectiles | move by s of a frame every frame (smooth; gravity × s² as above); `Age` counts only world frames, so life and range are unchanged; frozen at 0 | `Projectile.Tick` |
| Hazards | count telegraph and strike frames only on world frames | `Hazard.Tick` |

A slowed jump reaches the same height and lasts 1/s as long (within 6 cm: the physics step's integration differs slightly at a scaled velocity); a slowed walk covers s of the distance (`PerfectDodgeTests`). `Time.timeScale` is untouched: hitstop and whole-screen slow motion still compose with it.

**Not slowed (known, cosmetic).** Particles and `Vfx` effects (they run on rendered time), the ragdoll bodies of a physical fall or a KO (separate rigidbodies; the tethered fall follows the slowed gameplay body, its limbs flail at full speed), and a projectile's flight cue lifetime (`seconds` is set at spawn).

**For SAN-82 (Crimson Moon's freeze).** Call `ctx.Clock.SlowWorld(0f, -1, kage)` when the art starts and `ctx.Clock.ResumeWorld()` when it ends (or `SlowWorld(0f, frames, kage)` for a fixed length). Foes hold still mid-motion (kinematic) and resume with their velocity; their hazards and projectiles stop; Kage and his own projectiles and hazards keep full speed. A foe hit during the freeze gets its knockback velocity stored and released on resume. A later `SlowWorld` replaces a running one (a perfect dodge during the art would shorten the freeze: the art should not let one start, or should restore its own call afterwards).

**Files.** `FightClock` (the counter), `FightLoop.Step` (skip, restore, scale), `Fighter.WorldTime.cs` (new partial: the body), two reads in `Fighter.Pose.cs`, `Projectile.Tick`, `Hazard.Tick`. Nothing changes at scale 1: the baseline replay is unchanged.

## Notes from the implementation (SAN-83: multiple targets, two attackers at most, camera framing)

**Two new steps at the start of the frame.** After the push settle and before input, the loop runs

```csharp
foreach (var f in fs) if (Live(f)) f.PickTarget();   // the target list and the current target
ctx.Engagement.Update(fs);                            // which foes may attack
```

so input, move selection, the AI and facing all read this frame's `opponent` and engagement. Both read positions only and roll no random numbers: a fight with one foe gives exactly the same `opponent` the builder wired, and the baseline replay (both copies) is unchanged.

**The target list and the "nearest in front" rule** (`Fighter.Targets.cs`):

- `Fighter.Targets`: every active fighter of another team, in fighter order. With one foe it has one entry, the opponent; against the training bag it is empty (`opponent` null, the bag stays `extraTarget`).
- `opponent` is the target with the lowest score: horizontal distance, plus `behindPenalty` (1.5 m) for a fighter behind (against `facing`), minus `stickiness` (0.25 m) for the current target. Ties go to fighter order. A knocked-out fighter is a target only when every fighter in the list is out (so with one foe nothing changes).
- In words: Kage keeps to the nearest foe in front; he turns (in neutral, through the existing `FaceOpponent`) only when nobody is in front or a foe behind is 1.5 m nearer than the nearest in front. After turning, the old front foe is behind and pays the penalty, so he doesn't turn back and forth.
- The target is held while a throw or the rage art holds it (`Ultimate`, `Thrown`, a held victim): Thousand Shadows and the shoulder throw keep working on the fighter they started on.

**Hits on any foe.** The hit phase already cast every sweep against every hurtbox and skipped the attacker's own team (SAN-65), so a window hits every foe in reach, once per window each. Under the legacy switch `CheckHits` now tries every entry of `Targets` when there is more than one (with one, the opponent as before). Foes never hit each other (`EngagementTests.No_more_than_two_foes_attack_on_any_frame` checks every `Hit` and `Blocked` event of a long fight).

**Engagement** (`EngagementManager`, `ctx.Engagement`, numbers in `Resources/Tuning/engagement.json`, `EngagementTuning`):

- The manager limits **foes**: fighters not on team 0. Kage's side is never limited.
- **Attacking** means in an attack move: `Fighter.InAttackMove`, an `Attack` state whose move has a hit window, is a throw, throws a projectile or has a counter window. Rolls, backflips, dashes and getting up are not attacks. **Committed** (`Fighter.CommittedToAttack`) is attacking, the rage art, or an attack button still in the input buffer.
- Each frame: (1) an engaged foe that is knocked out, inactive or without a target leaves; (2) free places go to the waiting foes nearest their targets (the JS rule, `StretchSession.updateEngaged`); (3) the engaged foe farthest from its target gives its place to the nearest waiting foe when that one is `swapMargin` (1.5 m) nearer, only while it isn't committed.
- A foe that isn't engaged can't attack, whatever drives it: `Fighter.PollInput` drops its attack buttons (P, K, S, X; Guard and movement go through). With rule (3) never taking a place from a committed foe, every foe in an attack move is engaged, so at most `maxAttackers` (2) are ever attacking on one frame. The test counts them every frame of a minute-long fight.
- When an engaged foe is knocked out, the nearest waiting foe takes its place on the next frame that runs (the KO's hitstop and slow motion pass first).

**Waiting and circling** (`AIInputSource.WaitTurn`, the JS `AIController.waitTurn`): a waiting foe nearer than `waitDistance - retreatInside` (2.5 m) backs away, farther than `waitDistance + approachBeyond` (3.9 m) walks in, each for `stepFrames` (8); between those it circles (a walking step in or out, `circleChance` 0.4, 12 to 24 frames) or holds (13 to 30 frames). It still guards a technique aimed at it, but never punishes, chases, mashes a kip-up or presses an attack. On being engaged it drops its waiting plan and decides afresh. The JS ring (360 px) is 3.1 m at 115 px a metre (ADR-0010). The rolls come from the foe's own `ai/<FighterId>` stream; an engaged foe's AI makes exactly the rolls it made before, so a duel's rolls are unchanged.

**Camera framing** (`CameraRig`): `Frame(ctx, focus)` switches the rig from its `targets` list to the focus fighter (Kage) and the foes engaged on him, read every frame from the engagement manager, zooming out up to `groupMaxSize` (4.2) instead of `maxSize`. Waiting foes are not framed: they stand back at 3.1 m, and the framing of two engaged foes on either side of Kage usually shows them anyway. `Follow(dt)` is one step of the follow (what `LateUpdate` calls with the unscaled time), so a test can run it on a fixed step; the override fields still win (SAN-82). The duel is unchanged: it never calls `Frame`. `EngagementCameraTests` checks through `Camera.WorldToViewportPoint` that Kage's and every engaged foe's body (feet to head, a shoulder's width each side) is inside the viewport on every frame of a minute-long fight (an engaged foe from 30 frames after it was engaged, the follow's easing; shake and zoom punch left out).

**The dev arena.** F6 reloads the arena as the group fight, Kage against four Footmen (`KageGame.GroupFightSize`, `groupFoeId`), and F6 again goes back to the duel; F5 still cycles the duel's foe. Kage starts at 0 with Footmen at 1.3, -2.4, 2.8 and -3.8 m: the nearest two, one on each side, are engaged. Rounds work as in the duel: Kage takes the round when every foe is down, the foes when Kage is; at time-up health shares are compared. Each foe has its own AI (F3 and F4 set all of them) and shadow, and its own depth (sort base 1000, 600, 200, -200), Kage draws in front. The right-hand health bar shows Kage's current target.

**Determinism.** A group fight with the same seed gives the same event log (`EngagementTests.A_group_fight_is_identical_every_run`). Positions agree within the contract's 1e-4 m, not to the last digit: with five bodies touching, the physics step's contact order depends on the broadphase's proxy ids, which a second fight in the same session reuses in a different order, and the last printed digit can differ. Duels (two bodies) still match exactly.

**Files.** `Fighter.Targets.cs` (new: `Targets`, `PickTarget`, `InAttackMove`, `CommittedToAttack`, the input filter), `EngagementManager.cs` and `EngagementTuning.cs` (new), one line each in `Fighter.cs` (`PollInput`) and `FightContext.cs`, two in `FightLoop.Step`, the legacy branch of `Fighter.HitDetection.CheckHits`, `AIInputSource` (the waiting turn), `CameraRig` (`Frame`, `Follow`, `groupMaxSize`), `KageGame` (F6). Tests: `MultiTargetTests`, `EngagementTests`, `EngagementCameraTests`, `DevArenaTests.TheGroupFightIsKageAgainstFourFootmen`, the `GroupFight` harness, the review sheet `MultiTargetScreenshots` (Screenshots/review/SAN-83).

**For SAN-89 (stretch sessions).** Spawn a wave's foes into the same context on team 1; nothing needs wiring, since the loop picks every fighter's targets and the engaged foes each frame. Removing a cleared foe (`SetActive(false)`) takes it out of every target list and frees its place. A stretch with no foe in sight leaves Kage's `opponent` null; the JS game then turns him to where he walks, which the session can do the same way.

## Notes from the implementation (SAN-82: Crimson Moon's freeze)

- **One mechanism, reused.** The art calls `ctx.Clock.SlowWorld(0f, -1, kage)` in its first tick and `ResumeWorld()` when the finisher lands (frame 83 of 111), or when it ends early: `Fighter.SetState` away from `Ultimate`, `ResetForRound` and `OnDestroy` abort it (`AbortRageArt`), so a round reset, a `Win()` or a destroyed fighter can never leave the world frozen. `ResumeWorld` is only called if the clock is still the art's (`WorldExempt == kage`).
- **Longer than the JS freeze.** The JS art holds the world only through its 0.55 s anticipation. Here the world stays frozen until the finisher, because the ticket asks that other fighters hold still while the foe is bound and cut. Frozen foes take their hits (the knockback velocity of each is stored); the finisher resumes the world first, so the foe is thrown into a running world, in slow motion.
- **Who is bound: only the target.** As in the JS game (`tgt = nearestEnemy`), only the foe Kage targeted when the art started is cut and bound (`PickTarget` holds `opponent` through the art). In a group fight (F6) every other foe is frozen with the world and free from the finisher on; none is touched. (Kage is untouchable until frame 111.)
- **Timeline** (art frames = Kage's ticks; hit-stop and slow motion don't advance them): 0 freeze, cut-in band, camera close-in; 33, 44, 56 the three cuts (7 each, 0.8 m from the foe, alternating sides, the first binds); 67 step back and sheathe (slow motion 0.4 for 0.2 s); 83 giant crescent (15, knockdown, hit-stop 7, slow motion 0.3 for 0.45 s, shake, punch); 111 back in neutral. All in `crimsonMoon.json`.
- **Camera.** The art uses `CameraRig`'s override (`useOverride`, `overrideCenter`, `overrideSize`), set each tick through `FightContext.Camera` (new; `KageGame` and `StageWorld` set it, with `FightContext.Hud` for the cut-in band). The override wins over SAN-83's `Frame` (group framing) and the previous override (a tableau's) is restored when the art ends or aborts.
- Not slowed (as noted above for particles): the cut-in band and the effects run on rendered time.


## Consequences

- Tests can build any fight and replay it exactly.
- Tick order is explicit, so clashes and multi-foe fights have one well-defined frame order.
- The fighter's per-frame logic moves from `FixedUpdate` into `Tick`. That is a mechanical change inside `Fighter`, done once by SAN-61.
- Moving hit resolution after all fighters tick (SAN-65) can change the frame on which a trade or a simultaneous hit resolves. The baseline replay shows any such change.
- `Crescent` and every new projectile must move on the fight loop, not in `Update` (SAN-75).
