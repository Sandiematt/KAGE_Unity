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
