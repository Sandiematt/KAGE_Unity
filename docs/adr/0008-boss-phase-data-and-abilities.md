# ADR-0008: Boss phase data and abilities

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-95 (ability slots), SAN-102 (builds phases), SAN-103 to SAN-107, SAN-108, SAN-109

## Context

In the JS game a boss is an `ENEMIES` entry with `phases: [{ name, at, abilities, ...flags }]`. `at` is the health fraction at which the phase opens. Act I sub-bosses have two phases (`at` 1 and 0.5). Raikon has three (1, 0.66, 0.33). Abilities are JS functions in `40-ai.js` and `41-styles/*.js`, with a cooldown, a range in JS pixels and a chance. The spec: health thresholds swap the ability list, AI profile, speed, damage and flags; boss abilities are chosen like button presses that map to ability move slots; there is no second combat path for bosses.

## Decision

### Shape

```csharp
public sealed class BossDef
{
    public string id;               // = CharacterDef.id ("kuroe", "raikon")
    public string name, title;      // boss bar text ("Raikon", "the Lightning Ascendant")
    public string artCard;          // UI image id for the boss bar (SAN-67 imports the JS art cards)
    public List<PhaseDef> phases;   // phases[0].at == 1; `at` strictly decreasing
}

public sealed class PhaseDef
{
    public string name;             // JS phase name ("The Storm Gathers")
    public float at;                // health fraction that opens the phase (JS `at`)
    public string[] abilities;      // ability ids usable in this phase (JS `abilities`)
    public string aiProfile;        // null = keep the current profile
    public float speedMul = 1f;     // multiplies CharacterDef.speedMul
    public float damageMul = 1f;    // multiplies CharacterDef.damageMul
    public CharacterFlags addFlags, removeFlags;
    public bool aggressive;         // JS flag: AI profile aggression boost (SAN-95 defines how much)
    public bool berserk;            // JS flag (Hisame phase 2)
    public float secondWave;        // JS `secondWave`: delay in seconds of Thunder Swing's second wave; 0 = none (Raikon phase 3)
}

public sealed class AbilityDef
{
    public string id;               // JS ability id ("thunderSwing", "censure", "garanTsuppari")
    public string moveId;           // the MoveDef it starts (from the family set's Ability() or the character's file)
    public int cooldownFrames;      // JS `cd` seconds × 60
    public Vector2 range;           // JS `range` px → metres (ADR-0010)
    public float chance;            // JS `chance`
    public ProjectileDef projectile;// optional (ADR-0007)
    public HazardDef hazard;        // optional (ADR-0007)
}
```

- Phase names, `at` values, ability lists and flags come from the generated JS tables (ADR-0010). The Unity side adds only the ability → move mapping, ranges in metres and Unity tuning.
- `CharacterDef.abilities` lists every ability the character may use, in a fixed order. That order is the **ability slot**: `FrameInput.ability = n` asks for the n-th entry (ADR-0004). A phase enables a subset by id. Slots don't move between phases.
- An ability is an ordinary `MoveDef` started through the input interface. It obeys the same hit windows, guard rules, defence order and events as any move. Counter-window abilities (Kuroe's Censure, Shiden's Hold the Line, Raikon's Redline Counter) use `MoveDef.counterFrom..counterTo`, like the Mirror Guard.
- The AI profile (SAN-95) chooses abilities like buttons: in range, off cooldown, enabled in the current phase, then a roll on `chance` from the fighter's random stream. A disabled or cooling ability request is ignored by the fighter.
- Each boss's abilities live in `Bosses/Abilities/<Boss>.cs`. `BossCatalog.cs` has one line per boss.

### Phase changes

- `PhaseController` (SAN-102) is attached to any fighter whose `CharacterDef.bossId` is set. After hits resolve each frame, it compares `health / maxHealth` with the next phase's `at`. When the fraction is at or below it, the next phase starts. A hit that crosses two thresholds enters both in order.
- Entering a phase applies the ability list, profile, multipliers and flags, and publishes `BossPhaseChanged` (`Target` = the boss, `Index` = the new phase index, `Name` = the phase name). The boss bar (SAN-102, later SAN-108) and tests read that event.
- Boss and sub-boss stages use the JS **boss format**: one round, no clock (JS `fightFormat`). `FightSession` (SAN-102) owns rounds and the clock for every arena fight. Duels keep today's best of three. `FightSession` publishes `StageCleared` or `StageFailed` when a stage fight ends.

### Act I phase table (JS data at run time)

| Boss | Phase 1 (`at` 1) | Phase 2 | Phase 3 |
|---|---|---|---|
| Kuroe | The Black Line: `censure` | Redacted (0.5): `censure`, aggressive | — |
| Garan | Thunderstruck: `stormFist`, `garanTsuppari` | Stormbroken (0.5): `stormFist`, `charge`, `garanTsuppari` | — |
| Hisame | Rainfall: `lightningCut`, `hisameKocho` | Downpour (0.5): `lightningCut`, `smokeBomb`, `hisameKocho`, berserk | — |
| Shiden | Vanguard: `vanguardBreak`, `shidenBrace` | Hold the Line (0.5): `vanguardBreak`, `vanguardGuard`, `shidenBrace`, aggressive | — |
| Raikon | The Storm Gathers: `thunderSwing`, `stormBreak`, `skyfall` | Redline (0.66): adds `redlineCounter` | Lightning Ascendant (0.33): same list, `secondWave` 0.7 s, aggressive |

What each JS ability is, so the Unity move matches its design intent:

| Ability | JS design | Unity build |
|---|---|---|
| `censure` | raised-staff stance; a strike into it is punished | counter-window move on the spear (staff) set |
| `stormFist` | gauntlets crackle overhead, then a slam with a shock area | heavy slam + hazard |
| `garanTsuppari` | crouch tell, then four walking palm thrusts that crush a guard (posture) | four-hit armoured string |
| `charge` | telegraphed long running charge across the arena | armoured charging move |
| `lightningCut` | low crouch, fans spark, dash straight through | dash-through move (reduced Thunder Swing, no wave) |
| `hisameKocho` | when chased, an arc over Kage to land behind him, cut a beat later | repositioning move with invulnerable frames (the ticket's "mist step") |
| `smokeBomb` | throws a smoke bomb (a JS thrown weapon, `smoke`) | thrown projectile with a smoke cue |
| `vanguardBreak` | spear driven into the ground, shockwave | slam + hazard |
| `shidenBrace` | spear set low; anything rushing in is met by a counter thrust | counter-window thrust |
| `vanguardGuard` | raised guard; don't strike into it | counter-window move |
| `thunderSwing` | wide heavy swing, red lightning along the ground | tachi swing + ground-wave projectile |
| `stormBreak` | ground slam with a shock area | tachi slam + hazard |
| `skyfall` | when Kage keeps his distance: red ground mark, then a bolt | hazard on Kage's position |
| `redlineCounter` | counter stance | counter-window move |

## Consequences

- One phase mechanism serves four sub-bosses and Raikon, and a test boss (a Footman variant) can prove it before any real boss exists.
- Boss abilities are moves fed through input, so the player's own rules (guard, parry, dodge, armour) apply to them with no special case.
- Some ticket texts describe a sub-boss signature differently from the JS ability list (SAN-104, SAN-105, SAN-106). The JS list above is the reference. Those tickets have comments explaining the mapping.

## Notes from the implementation (SAN-95: what SAN-102 can call)

- **Ability slots** are built (ADR-0004's SAN-95 notes): `FrameInput.ability`, `Fighter.AbilityIds`, `Fighter.SetEnabledAbilities(ids)` (a phase's `abilities`), `Fighter.AbilityReady(slot)`, `AbilityDef` and `AbilityCatalog` (`Combat/AbilityDef.cs`; `id`, `move` or `moveId`, `cooldownFrames`, `range` in metres, `chance`; the projectile and hazard fields are for the boss tickets). Boss files `AbilityCatalog.Register` their abilities. `CharacterDef.FromJs` already fills `CharacterDef.abilities` with the JS ids in the JS order, so slot n is the n-th JS ability.
- **Phase hooks on the AI:** `AIInputSource.SetProfile(AiProfiles.Get(phase.aiProfile))` swaps the profile (and with it the parry rate); `AIInputSource.aggressive = phase.aggressive` adds `AiTuning.aggressiveBonus` (0.2, the JS `aggr += 0.2`) to aggression and multiplies ability chances by 1.3 (the JS number). `berserk` (JS: ability cooldown × 0.7) and `secondWave` are not wired: the cooldown lives on the fighter, so `PhaseController` can change `AbilityDef.cooldownFrames` per fighter if it wants to.
- Which profile each boss starts with is the JS archetype in the generated table, taken by `CharacterDef.FromJs`: Kuroe `defensive`, Garan `heavy`, Hisame `agile`, Shiden and Raikon `boss`. A phase's `aiProfile` swaps it (the Act I phases in the table above change none).

## Notes from the implementation (SAN-102: phases, flags, boss bar, FightSession)

What is built, and the recipe a boss ticket (SAN-103 to SAN-107, the Shieldbearer, Ironclad and the Husk) follows.

**Data** (`Scripts/Boss/BossDef.cs`). `BossDef` and `PhaseDef` are the shapes above, plus one field: `PhaseDef.moveRateMul` (multiplies `CharacterDef.moveRate`, so a phase can make attacks play quicker without a faster walk; the ticket's "speed and the move rate"). `BossDef.FromJs(id)` builds a boss from the generated table: name, title, and per phase the name, `at`, `abilities`, `aggressive`, `berserk` and `secondWave`. `BossDef.Validate()` throws unless `phases[0].at == 1` and `at` strictly falls (`BossCatalog.Register` validates). `BossCatalog.Get(id)` returns the registered boss, else the plain JS one, so Raikon and the sub-bosses already run their JS phases (abilities per phase, no profile or multiplier changes) before their ticket tunes them. `CharacterDef.FromJs` sets `bossId` for every JS enemy that has phases.

**Phases are absolute.** A phase's multipliers and flags apply to the boss's `CharacterDef`, not on top of the previous phase: `SpeedMul = def.speedMul * speedMul`, `MoveRate = def.moveRate * moveRateMul`, `DamageMul = def.damageMul * damageMul`, `Flags = (def.flags | addFlags) & ~removeFlags`. Read one phase and you know the boss in it. The AI profile is the exception (`aiProfile` null keeps the current one), as the ADR says.

**`PhaseController`** (`Scripts/Boss/PhaseController.cs`) is a plain class on `Fighter.Phases`, created by `FightBuilder.Spawn` for a character with `bossId`, and ticked by `FightLoop.Step` after the frame's hits, projectiles and hazards (before the push step). It opens every phase whose `at` the health fraction has reached (a blow across two thresholds opens both, in order), never goes back, opens nothing for a boss at 0 health, and starts again from phase 0 on `Fighter.ResetForRound`. Entering a phase calls only what SAN-95 exposed: `SetEnabledAbilities(phase.abilities)`, `AIInputSource.SetProfile` and `.aggressive` (found behind the input, directly or as a `ScriptedInput` fallback), the multipliers, `Fighter.Flags`, then publishes `BossPhaseChanged` (Target = the boss, Index, Name). Phase 0 is announced by the first tick of a fight (and again after a reset), so a listener sees "phase 0" as the first boss event. `berserk` sets `Fighter.AbilityCooldownMul` to `PhaseController.BerserkCooldownMul` (0.7, the JS number); `secondWave` is exposed as `Fighter.SecondWaveDelay` (seconds, 0 = none), the hook for SAN-107's Thunder Swing and SAN-89's waves. Nothing here spawns a wave.

**`FightSession`** (`Scripts/Fight/FightSession.cs`, `FightFormat`). The rounds and the clock that lived in `KageGame` are a plain class driven by `Update(unscaledDt, scaledDt)`; the owner (the dev arena now, `StageWorld` later) places the fighters and shows the announcements through events: `RoundStarting` (place them), `IntroBeat` (0.35 s), `FightBegan`, `RoundEnded(RoundOutcome)` (knockout, time up, draw), `MatchEnded(winner)`. It owns the intro (1.45 s), the clock, the knockout rules (a group's round goes on while a foe stands), the winner's bow, the next round, and the match end. `FightFormat.Duel` is best of three on 60 s (the dev arena's old numbers; the JS campaign's 75 s is a later choice), `Boss` is one round and no clock, `Stretch` the same for SAN-89. `FightFormat.For(def)` picks Boss when the character has a `bossId`. With a stage id the session publishes `StageCleared` or `StageFailed` when the match ends; the dev arena passes none. The dev arena's behaviour in duels is unchanged: same timings, same HUD, same results.

**Boss bar** (`Scripts/UI/Hud.Boss.cs`, `Hud.BindBoss(boss, def, events)`): bottom centre as in the JS HUD: phase numerals and title on a top row, the name, a health track with a mark where each later phase opens, and the current phase's name under it. The fill reads the boss's health each tick; the phase comes from `BossPhaseChanged` (so it updates when the fight says so) and a banner names the new phase. It replaces the right-hand foe panel (hidden, as JS hides it for a boss) and the dev key hint. `Hud.SetFormat(format)` hides the clock, the round count and the pips for formats that have none. The art card, medal and hurt shake are SAN-108's.

**The dev arena** picks the format from the foe: a duel for everything but a boss. The test boss is the Footman Captain (`footman_boss`, `Scripts/Boss/FootmanBoss.cs`), in the F5 cycle like every catalog foe. Raikon is in the cycle too and now runs his JS phases.

### Recipe: a boss entry

1. **Character** (`Character/Roster/<Boss>.cs`): `CharacterDef.FromJs(id)` already fills the stats, JS flags (`heavyArmor` is Armour, `noGrab` is NoGrab), `abilities` in slot order and `bossId`. Add the part set, family, mass, posture, move rate, colours. Register it in `CharacterCatalog`.
2. **Abilities** (`Bosses/Abilities/<Boss>.cs`): one `AbilityDef` per JS ability id in `AbilityCatalog` (`move` or `moveId`, `cooldownFrames`, `range` in metres, `chance`), each an ordinary `MoveDef` (use `Armour(from, to)` for armoured frames, `counterFrom..counterTo` for a counter). `FootmanBoss.RegisterAbilities` is the smallest example.
3. **Phases**: `var b = BossDef.FromJs(id); b.phases[1].aiProfile = ...; b.phases[1].speedMul = ...; ...; BossCatalog.Register(b);`. Set only what differs from the JS table (profile swaps, `speedMul`, `moveRateMul`, `damageMul`, `addFlags`/`removeFlags`). Put the call in `BossCatalog.Ensure` (one line per boss). A boss with no Unity tuning needs no line at all.
4. **Flags**: `CharacterFlags.Armour` (light hits don't flinch, always) and `NoGrab` (every throw fails) on the character, or on a phase through `addFlags`.
5. **Test**: a `HeadlessFight` with `foeDef: CharacterCatalog.Get(id)`, set `Ronin.health` to a fraction and `Run` a few frames; read `Phases.Index`, `AbilityEnabled`, `SpeedMul` and the `BossPhaseChanged` events (see `BossPhaseTests`).
