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

## Consequences

- Tests can build any fight and replay it exactly.
- Tick order is explicit, so clashes and multi-foe fights have one well-defined frame order.
- The fighter's per-frame logic moves from `FixedUpdate` into `Tick`. That is a mechanical change inside `Fighter`, done once by SAN-61.
- Moving hit resolution after all fighters tick (SAN-65) can change the frame on which a trade or a simultaneous hit resolves. The baseline replay shows any such change.
- `Crescent` and every new projectile must move on the fight loop, not in `Update` (SAN-75).
