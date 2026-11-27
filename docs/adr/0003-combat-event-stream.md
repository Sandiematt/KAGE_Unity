# ADR-0003: The combat event stream

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-61 (builds it), and every ticket that publishes or reads combat events: SAN-73, SAN-86, SAN-89, SAN-91, SAN-92, SAN-93, SAN-96, SAN-102, SAN-108, all fight-harness tests

## Context

Today the fighter reports two things: `onHit(attacker, defender, HitInfo, HitResult)` and `onKO(fighter)`. Props are reported through `KageGame.I.OnPropHit`. The HUD, the fight rating, the tutorial, stretch waves, boss phases and every fight test need one place to read what happened, in order, with enough detail to compare two runs.

## Decision

### Shape

One stream per fight: `ctx.Events` (ADR-0002). Events are one flat, read-only struct, so a log is easy to filter, store and compare, and publishing allocates nothing.

```csharp
public enum CombatEventType
{
    FightStarted,     // Index = seed
    MoveStarted,      // Source started MoveId
    Hit,              // Source's MoveId hit Target
    Blocked,          // Target guarded Source's MoveId
    Countered,        // Target's counter window caught Source's MoveId (Mirror Guard, Censure, Redline Counter)
    Parried,          // Target parried Source's MoveId (SAN-91)
    Clashed,          // Source's and Target's blades met (SAN-93)
    GuardBroken,      // Target's posture filled (SAN-86)
    PerfectDodged,    // Target perfect-dodged Source's MoveId (SAN-92)
    KnockedOut,       // Target was knocked out; Source landed the last hit
    RageArt,          // Source started a rage art: MoveId = "crimsonMoon" | "silentMoon" | "swallowStorm" | "hellSplitter" | "waterDragon" | "reaperRequiem" | "thousandShadows"
    BossPhaseChanged, // Target entered phase Index (Name = JS phase name) (SAN-102)
    WaveStarted,      // Index = wave number in the stretch, Name = encounter id (SAN-89)
    WaveCleared,      // same fields (SAN-89)
    StageCleared,     // Name = stage id
    StageFailed       // Name = stage id
}

public readonly struct CombatEvent
{
    public readonly int Frame;               // ctx.Clock.Frame when it happened
    public readonly CombatEventType Type;
    public readonly Fighter Source;          // the actor (attacker); null for stage events
    public readonly Fighter Target;          // the one acted on; null for props and stage events
    public readonly string SourceId, TargetId; // FighterId or prop id ("bag"); always set when the fighter or prop is
    public readonly string MoveId;           // move, projectile, hazard, ability or rage-art id
    public readonly int Window;              // index of the hit window in the move; -1 when not a window
    public readonly float Damage;            // health removed from Target after every multiplier, crit and defence
    public readonly float Posture;           // posture added: to Target (Hit, Blocked), to Source (Parried), to each (Clashed)
    public readonly bool Crit;
    public readonly HitLevel Level;
    public readonly HitZone Zone;
    public readonly Vector2 Contact;
    public readonly Vector2 Knockback;       // world velocity or impulse given to Target
    public readonly int Index;               // seed, phase index, wave number; 0 when unused
    public readonly string Name;             // phase name, encounter id, stage id; null when unused
}
```

Static factory methods (`CombatEvent.Hit(...)`, `CombatEvent.Parried(...)`, ...) build each type, so callers can't mix up fields.

```csharp
public sealed class CombatEvents
{
    public event Action<CombatEvent> Published;
    public void Publish(in CombatEvent e);
}

public sealed class CombatEventLog   // test and debug helper, in Kage.Runtime
{
    public CombatEventLog(CombatEvents source);
    public IReadOnlyList<CombatEvent> All { get; }
    public int Count(CombatEventType t);
    public IEnumerable<string> Lines();   // canonical text, one event per line
}
```

The canonical line is the format of the baseline replay and of every "identical event log" check:

`{Frame} {Type} {SourceId}>{TargetId} {MoveId}#{Window} dmg={Damage:0.###} pos={Posture:0.###} crit={0|1} lvl={Level} zone={Zone} i={Index} n={Name}`

Positions are not part of the line. The replay stores per-frame positions separately and compares them within a tolerance (ADR-0002).

### Rules

- Dispatch is synchronous, in publish order, on the fight loop.
- **Listeners never change fight state inside the callback.** The HUD, the rating, the tutorial and tests only read. A session that must react (spawn the next wave, change a boss phase) records the fact and acts in the loop's late tick.
- Every hit result publishes exactly one event: `Hit`, `Blocked`, `Countered`, `Parried` or `PerfectDodged`. A whiff publishes nothing. Throws, Thousand Shadows cuts, projectiles and hazards publish `Hit` / `Blocked` like any window. `MoveId` tells them apart: the throw move id, `thousandShadows`, the projectile id (`crescent`, `kunai`, `thunderSwing.wave`) or the hazard id (`skyfall`).
- The existing `onHit` and `onKO` callbacks stay, invoked from the same place as the matching event, so existing listeners keep working (SAN-61 acceptance). `KageGame.OnPropHit` becomes a listener for events whose `TargetId` is a prop.
- Fight rating input (health left, time, max combo, parries, flawless) is computed by a `FightStatsRecorder` that only reads the stream and the fighters' public state (SAN-73). Nothing else keeps its own parry or combo count for the rating.

### Who publishes what

| Event | Published by | Ticket |
|---|---|---|
| `FightStarted`, `MoveStarted`, `Hit`, `Blocked`, `Countered`, `KnockedOut` | `FightBuilder` / `Fighter` | SAN-61 |
| `RageArt` | `Fighter.Specials` / `Fighter.RageArt` | SAN-61 (Thousand Shadows), SAN-82 (Crimson Moon) |
| `GuardBroken` | `Fighter.Posture` | SAN-86 |
| `Parried` | `Fighter.Parry` | SAN-91 |
| `PerfectDodged` | `Fighter.PerfectDodge` | SAN-92 |
| `Clashed` | `FightLoop.Clash` | SAN-93 |
| `Hit` / `Blocked` from projectiles and hazards | `Projectile`, `Hazard` | SAN-75 |
| `WaveStarted`, `WaveCleared`, `StageCleared` (stretch) | `StretchSession` | SAN-89 |
| `BossPhaseChanged` | `PhaseController` | SAN-102 |
| `StageCleared`, `StageFailed` (fight stages) | `FightSession` | SAN-102 |

A ticket that needs a new event type adds it to the end of the enum, so the order of earlier types doesn't change.


## Notes from the implementation (SAN-86)

- `Hit` and `Blocked` now carry `Posture`: what the target's bar rose by (damage × 1.5 × the window's postureMul on a block, × 0.3 on a clean hit, after the cap). Throws and Thousand Shadows cuts don't add posture, so their `Hit` lines keep `pos=0`.
- `GuardBroken` (factory `CombatEvent.GuardBroken`): Source is the fighter whose hit filled the bar, Target the broken fighter, `MoveId` and `Window` the hit's, `Posture` the bar that filled (the target's posture maximum), `Contact` the hit's. It follows the `Blocked` event of that hit on the same frame (`Fighter.FlushGuardBreak`).
- The `Hit` on a guard-broken fighter has `Crit = 1` and the critical damage, though the attacker didn't roll it.
- `HitResult.Parried` and `HitResult.Dodged` map to `Parried` and `PerfectDodged` in `PublishHit`, ready for SAN-91 and SAN-92.
- The baseline replay was re-recorded for the posture field and the AI's Guard input (ADR-0004, SAN-86 notes).

## Notes from the implementation (SAN-92)

- `PerfectDodged` is published by the **attacker** like every hit result (`PublishHit`, from `OnHitResolved` or `ResolveExternal` for projectiles and hazards): Source is the attacker, Target Kage, `MoveId` and `Window` the dodged hit's, `Damage` 0 (the dodge clears Kage's `LastDamageTaken`, so a stale value from an earlier hit never leaks in), `Posture` 0, `Crit` 0. One per perfect dodge, on the frame the hit would have landed; a dash or roll perfect-dodges once, so there is never a second one for the same dodge.
- The `onHit` callback is invoked with `HitResult.Dodged`; `KageGame.OnFighterHit` ignores it. The "EVADE" call-out comes from the cue (`VFX_Kage_PerfectDodge`), not from a listener.
- No baseline scenario dodges, so the baseline replay is unchanged.

## Notes from the implementation (SAN-93)

- `Clashed` (factory `CombatEvent.Clashed`) is published by the fight loop (`FightLoop.Clash.cs`) once per clashing pair: Source is the fighter earlier in fight order, Target the other; `MoveId` and `Window` are Source's, **`Name` is Target's move id** (the one place `Name` carries a move; `ToLine` prints it as `n=`), `Damage` 0, `Posture` what each fighter gained (the clash's posture loss, as added to Source's bar), `Contact` where the blades crossed. A guard-break the posture causes is published right after it (`GuardBroken`, Source the other fighter, `MoveId` that fighter's move).
- No `Hit` or `Blocked` follows for either blade: both moves are over.

## Consequences

- The HUD, rating, tutorial and tests share one definition of what happened.
- One flat struct carries some fields that a given type doesn't use. The table above and the factory methods say which are set.
- The baseline replay is a text file of canonical lines, easy to diff in review.

## Notes from the implementation (SAN-91)

- `Parried` is published by the attacker's `PublishHit` like every hit result, with Source = the attacker, Target = the fighter who parried, Damage 0, Crit false (a crit rolled for the parried blow is dropped) and Posture = the posture the attacker lost to the parry (`Fighter.LastParryPosture` on the parrier; 0 for a parried projectile). When that posture fills the attacker's bar, `GuardBroken` (Source = the parrier) follows on the same frame.
- `FightStatsRecorder` counts `Parried` events whose Target is the player; a foe's parry of Kage isn't counted. No other class keeps a parry count for the rating (`Fighter.ParriesMade` is the fighter's own tally, for tests and the HUD).

## Notes from the implementation (SAN-102)

- `BossPhaseChanged` is published by `PhaseController` (`CombatEvent.BossPhaseChanged(frame, boss, index, name)`: Target = the boss, Index = the new phase, Name = the JS phase name). Phase 0 is published by the first tick of a fight, and again after a round reset. The boss bar reads it.
- `StageCleared` and `StageFailed` have factories (`CombatEvent.StageCleared(frame, stageId)`, `StageFailed`; Name = the stage id) and are published by `FightSession` when a match ends and it was given a stage id.

## Notes from the implementation (SAN-89)

- `WaveStarted` and `WaveCleared` have factories (`CombatEvent.WaveStarted(frame, wave, encounterId)`, ...); `StageCleared` and `StageFailed` (SAN-102's factories) are published by `StretchSession` too, as `FightSession` does for a duel. `StretchSession` publishes them from the fight loop's late tick, on the frame it sees the fact. **`Index` is the wave's number in the whole stretch (1-based, counting across encounters), `Name` the encounter id `{stageId}.e{n}`** (n is 1-based, for example `dev.footmen.e1`). `WaveCleared` follows the `KnockedOut` event of the wave's last foe by at least a frame; `StageCleared` and `StageFailed` carry `Name` = the stage id, with no actor and no target. A stretch publishes exactly one of the two, once.
- There is no event for a gate: it follows the encounter (`StretchSession.GateChanged` is for the road's drawing, not a combat event). The first `WaveStarted` of an encounter comes 21 frames after its gate shut.
- `FightStatsRecorder(events, player, stretch: true)` reads a stretch: a foe's `KnockedOut` does not end the fight (by default the first `KnockedOut` does), `StageCleared` ends it as a win and `StageFailed` as a loss. A duel's recorder is unchanged.

## Notes from the implementation (SAN-108)

- The HUD is a reader of this stream and of the fighters' public state, nothing else (`FightHudModel.Handle`, fed by `Hud.BindEvents`). It never holds a fighter: it matches events by `TargetId`, so a recorded or hand-made stream drives it in a test.
- **Events the HUD reads**: `Hit` with `Damage > 0` shakes the panel of its `TargetId` (Kage's, the foe's or the boss bar, the JS `hudHurt`; a blocked, parried or zero-damage hit does not); `BossPhaseChanged` moves the boss bar to `Index` and glitches its card when it is a later phase (the opening event, `Index` 0, does not); `WaveStarted` sets the road's wave number and flashes its label, `WaveCleared` a half flash; `StageCleared` and `StageFailed` mark the stretch finished. Health, Rage, kunai, posture, combo, rounds and road progress come from state (`HudReader`), not from events, so they cannot drift from the fight.
- `CombatEvent.BossPhaseChanged(frame, string bossFighterId, index, name)` is a second factory for a boss known only by its id (`TargetId`, no `Target`), for a recorded stream or a test; `PhaseController` still uses the `Fighter` one.
- The boss bar's `HudBossInfo.Id` is the boss's `FighterId`, which is what its events carry in `TargetId`.
