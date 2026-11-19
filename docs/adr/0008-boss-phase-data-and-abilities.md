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

## Notes from the implementation (SAN-103: Kuroe, the first sub-boss)

Kuroe is the template for Garan (SAN-104), Hisame (SAN-105) and Shiden (SAN-106). What she added, and what to copy.

**Files, one per concern.** `Character/Roster/Kuroe.cs` (the entry), `Character/PartSets/KuroePartSet.cs` and `KuroePalette.cs` (the look), `Boss/Abilities/KuroeAbilities.cs` (her abilities; the recipe's `Bosses/Abilities/<Boss>.cs` lives under `Scripts/Boss/Abilities/`), two lines in `CharacterCatalog`, no line in `BossCatalog` (her phases are the JS table as it is). Tests: `KuroeDataTests` (EditMode: stats, slots, phases, ability numbers, part set, arena env), `KuroeFightTests` (the counter, waiting it out, the throw, the phases, the AI pressing the slot), `KuroeBotFightTests` (the bot fight and its time-to-kill), `KuroeArenaTests` (F5, boss format, boss bar, arena), `KuroeScreenshots` (the review sheet).

**Ability slots of a JS boss.** A JS boss lists its abilities only in its phases, so `CharacterDef.FromJs` now makes the slots the phases' abilities in first-use order when the entry has no list of its own (Raikon: `thunderSwing`, `stormBreak`, `skyfall`, `redlineCounter`; Garan: `stormFist`, `garanTsuppari`, `charge`). Before this a boss had no slots at all.

**Ability numbers are generated.** `Tools/js_export/export.js` now writes `JsTables.BossAbilities` (ADR-0010): `cd`, `range` and `chance` from `BOSS_ABILITIES` for every ability an exported foe uses, plus the `RAIKON_VARIANTS` numbers of the sub-bosses' reduced Raikon moves (`stance`, `punish`, `dmg`, `unb`, `windup`, `shock`, `shockLife`, `reach`, `dash`, `wave`). An ability file reads `JsTables.BossAbility(id)`: `cooldownFrames = round(Cd × 60)`, `range = Range / JsUnits.PxPerMetre`, `chance = Chance`, and turns the variant's seconds into frames. Nothing is typed by hand. The damage of a reduced move is the variant's `dmg` multiplier on the Unity move it reduces (Censure's punish: 0.6 × the Kage Counter's 12 = 7, before her `damageMul` 1.15).

**Counter stances** (`MoveDef.counterAnswer`, `counterFrontOnly`; `Fighter.Specials.TriggerCounterAnswer`). The JS counter stance (Censure, Hold the Line, Redline Counter) is not the Mirror Guard: it catches only a melee blow from the front, turns it aside like a parry and answers where the fighter stands. Step 4 of the defence order now plays `counterAnswer` when a move has one (the attacker `GetParried` by its role: Kage reels 28 frames and loses 30% posture; hit-stop 8; the answer starts at once), and the Mirror Guard (no answer) is unchanged, so the baseline replay is too. A projectile, a hazard, a blow from behind and an unblockable blow are not caught, and a throw takes a fighter in a stance (it is an attack move, not invulnerable). Censure: the staff comes up in 4 frames, the window is the JS `stance` (0.7 s = 42 frames), then 14 frames of dropping the guard with no window (the opening that makes waiting it out worth it); the answer waits the JS `punish` (0.14 s = 8 frames), then dashes in with a diagonal strike live for 12 frames (the JS 0.2 s area), `Mid` (JS `unb` 0: blockable), heavy, knockdown. Shiden's Hold the Line (`vanguardGuard`: stance 0.8, dmg 0.75, unb 0) and Raikon's Redline Counter (stance 1.0, punish 0.1, dmg 1, unblockable) are the same builder with other table rows: copy `KuroeAbilities.Censure`/`Punish` and change the poses.

**The arena placeholder** (`Scripts/Stage/Arenas/`: `BossArena`, `EnvBackdrop`, `EnvLook`). A sub-boss fights on the dev arena's floor and walls (`ArenaBuilder.BuildPhysics`) under a backdrop painted in code from the JS environment of its campaign stage. `BossArena.EnvFor(id)` finds the `subboss` stage whose enemy is the character (Kuroe: act1.sub1, `rooftops`; Garan, Hisame, Shiden: `districts`). `EnvLook` holds one env's colours, taken from the JS `ENVS` in `50-world.js`; `EnvBackdrop` paints the JS rooftops layout from any look (sky with stars, moon and clouds, a far city of blocks and pagoda roofs with lit windows, haze, a string of red paper lanterns, near tiled roof ridges, the tiled roof the fight is on) in camera parallax layers, seeded by the env id. The districts look is the rooftops under a cold slate wash with a dimmer moon (the JS rain is not painted). So the other sub-bosses get their arena by being in `CharacterCatalog`: nothing to add, unless their env needs its own `EnvLook`. `KageGame` builds it in place of the shrine with one line (`BossArena.TryBuild(stage, cam, foeId)`, after the stretch check); `StageWorld` (the campaign) still builds the shrine for every stage, and SAN-109 can call `BossArena.TryBuild` with the stage's enemy (or paint `EnvLook.For(stage.Env)` directly).

**Look.** A sub-boss gets a palette and a silhouette only. Kuroe: ink black, gold and paper (`KuroePalette`), a warm bone rim; a tall black eboshi leaning back with a pale ink stroke and a swinging redaction slip, huge hanging sleeves, a robe to the ankles in swinging front and back panels, a gold-edged kesa. Weapon looks belong to the family visual (`SpearVisual.LookOf`: the temple staff is now the JS dark wood with gold bands).

**Profile.** She keeps the JS archetype (`defensive`). The JS fight style (`41-styles/kuroe.js`: pref 205 px, parry 0.18, the stop-thrust, the riposte off a block) is not ported; that is SAN-110's tuning if the owner wants it.

**The bot fight** (`KuroeBotFightTests`, seed 103, real health: Kage 100, Kuroe 155). The SAN-95 `KageBot` alone (walk in, cycle its buttons) loses to her (knocked out at 30 s with Kuroe at 103 of 155): she parries and outranges a masher. The sub-boss bot adds the two rules her fight teaches: hold the blade while a counter window is up, and hold Guard while the foe's blow is coming (from the start of her attack to the end of its last window) when she is within 2.8 m. It wins in **4167 frames = 69.5 s** (the spec's 60 to 90 s for a sub-boss), Kage on 8 of 100, through both phases, 7 Censures raised and none walked into, 26 of her parries; no 10 s without a hit, block, parry or counter. Later sub-boss tests can reuse that bot (it reads only the foe's move, counter window and hit windows).

**Review images** (`KuroeScreenshots`, `Screenshots/review/SAN-103/`): `kuroe_beside_kage`, `kuroe_vs_footman_vs_shade` (her silhouette and palette against the Order and the Hollows), `censure_raised`, `censure_counter`, `censure_punish`, `kuroe_arena_phase1`, `kuroe_arena_phase2` (the dev arena: rooftops backdrop, boss bar, the phase two banner).

## Notes from the implementation (SAN-104: Garan, the second sub-boss)

Garan follows Kuroe's recipe. What he added, and what Hisame, Shiden and Raikon can reuse.

**Files.** `Character/Roster/Garan.cs` (the entry and `Garan.Boss()`), `Character/PartSets/GaranPartSet.cs` and `GaranPalette.cs` (the look, with the iron tekko gauntlets: SAN-80 left a fists foe's gauntlets to its part set), `Boss/Abilities/GaranAbilities.cs` (his four abilities), `Combat/Fighter.MoveHooks.cs` (three new move hooks, below), two lines in `CharacterCatalog`, one in `BossCatalog.Ensure`. Tests: `GaranDataTests` (EditMode), `GaranFightTests`, `GaranBotFightTests`, `GaranArenaTests`, `GaranScreenshots`.

**Data.** `CharacterDef.FromJs("garan")`: JS health 395 (170 Unity), defence 0.1, damage ×1.1, speed ×0.92, build scale 1.18, `iron_fists`, the `heavy` archetype, `heavyArmor` (the Armour flag), slots `stormFist`, `garanTsuppari`, `charge`. The entry adds the fists family, mass 1.8, posture 80 and a fourth slot, `garan.clinch`. `Garan.Boss()` is `BossDef.FromJs` with `garan.clinch` added to both phases (a phase would otherwise disable it): "Thunderstruck" (Storm Fist, Tsuppari, clinch), then at 0.5 "Stormbroken" (adds the charge). He stays on the heavy profile.

**Numbers.** Everything the JS keeps as data is generated (ADR-0010 now also exports `Moves`, `Projectiles`, `AiRoles`, `Difficulties` and `FightStyles`): cooldown, range and chance; Storm Fist's variant; the `shock` projectile; the grab's timing; the clinch's range (the style string `['grab']`, 0 to 115 px); and the tells' scale, the JS `ai.p.windup` for a sub-boss on Normal (Normal 1 × the `sub` role's 0.98). What the JS writes only in its ability code (Storm Break's 19 damage and gb 110; the Tsuppari's 0.5 tell, four palms 0.17 s apart walking 170 px/s, palm damage 6 and 10 with gb 24; the charge's 0.6 s tell, 1050 px/s run, 3.2 s, damage 22, gb 120, 0.6 s stop and 1.7 s wall stun; the throw's 16 damage and 0.5 s carry; the clinch's 0.55 chance; the JS guard lean of damage × 1.3) is a named constant in `GaranAbilities` with its JS file. Two conversions, both in that file: **damage** is the JS damage × `UnityPerJsDamage` (Unity's jab 4 over the JS jab 5 = 0.8), so a JS blow keeps its weight against Unity's own moves; **posture** is `postureMul = gb / (JS damage × 1.3)`, how much heavier on a guard than a plain blow of its damage, which is what `postureMul` means.

| Ability | Slot | Cooldown | Range | Chance | Move |
|---|---|---|---|---|---|
| Storm Fist `stormFist` | 1 | 336 f (5.6 s) | 0 to 2.09 m | 0.6 | a 46 f armoured wind-up (0.78 × 0.98 s), both fists overhead; the slam on frames 45 to 49: 10 damage, Overhead, knockdown, postureMul 6.85 (one guarded slam breaks Kage's guard, as the JS gb 110 does); a 45 f open recovery |
| Storm Fist's shock | | | | | hazard `garan.stormFist.shock`, laid on frame 1 and telegraphed 44 f, strikes for 18 f (the JS shockwave's 0.75 × 0.4 s) from the slam's first frame; a floor box 2.57 × 0.59 m (the shockwave's run of 228 px plus its 68 px width; a jump clears it) centred 2.22 m in front; 5 damage, Mid, knockdown; strike cue `VFX_Anime_Ground_Crack` |
| Tsuppari `garanTsuppari` | 2 | 360 f (6 s) | 0 to 2.0 m | 0.55 | the shikiri crouch (29 f), then palms on frames 29, 40, 50 and 60 (alternate hands) walking 1.48 m/s: 5, 5, 5 and 8 damage, Mid, postureMul 3.08 and 1.85; armoured to frame 70; 100 f |
| Charge `charge` (phase 2) | 3 | 390 f (6.5 s) | 1.74 to 10.4 m | 0.6 | a 36 f tell stepping back, then a 9.13 m/s run to frame 192 with a body window (both fists and the knee): 18 damage, knockdown, postureMul 4.23; armoured; on contact `garan.chargeStop` (36 f); into the wall he faces, staggered 102 f |
| Clinch `garan.clinch` | 4 | none (JS) | 0 to 1.0 m | 0.55, only on a guard | a step in; a grab window on frames 4 to 8 (the JS grab's 0.06 + 0.08 s) with 1.0 m reach; the carry up and over to frame 38 (0.5 s), released behind into a physics launch (13 damage); armoured 0 to 38; the whiff `garan.clinchWhiff` (26 f) |

**The clinch** (the ticket's armoured grapple). An ordinary throw move with one new field, `MoveDef.grabTo`, a **grab window**: on every frame from `grabFrame` to `grabTo` the grab takes the foe if it is within `grabRange` and can be thrown (`Fighter.CanBeThrown`: grounded and neutral, guarding, in blockstun, or in an attack that isn't invulnerable), and the move jumps to `grabTo` so the carry is the authored one. If nobody is taken by `grabTo`, or the foe is no-grab, it becomes its whiff. Every other throw keeps `grabTo = -1` (the old single-frame grab), so the baseline replay is unchanged. Its armour frames cover the reach, the carry and the release: a light hit lands its damage and posture, Garan doesn't flinch and the victim stays held (`ReceiveHit` lets a victim go only on a flinch). A heavy hit still flinches him and frees Kage. It is avoided by anything that can't be thrown during the window: a backflip or a roll (invulnerable), a dash, a jump, or being out of reach. The AI side is `AbilityDef.when` (new: `(self, target) => bool`, asked by `AIInputSource.TryAbility` on top of range, cooldown and phase). The clinch's is "Kage is guarding", the JS style string's condition. A scripted press ignores it.

**Move hooks** (`Fighter.MoveHooks.cs`; each is off unless a move sets it). `MoveDef.Hazard(frame, def, ahead)` lays a hazard (ADR-0007) down on a frame of the move, `ahead` metres in front of the fighter's feet (Storm Fist; Shiden's Vanguard Break and Raikon's Storm Break are the same call). `MoveDef.onContact` plays a follow-up in place once a window of the move lands or is blocked (the charge's skid). `MoveDef.wallStagger` staggers the fighter (`Fighter.Stagger`) when its root motion drives it into the wall it faces (the dodged charge: the JS "STAGGERED" opening). The baseline replay uses none of them.

**Fixed on the way.** `HazardMark` (SAN-75) made its `MaterialPropertyBlock` in a field initializer, which Unity forbids in a `MonoBehaviour`. With a `Vfx` present (the dev arena), every hazard telegraph logged an exception. It is made in `Init` now. No test had shown a hazard with a `Vfx` before.

**Look.** Iron and storm red (`GaranPalette`, from the JS `SUB_IRON`, `SUB_RED_LACQ` and his accent `#ff6a52`) with a hot ember rim: a wild black mane, a horned iron brow band, a red oni menpō with bared teeth, a broad gi under half a dō with a glowing storm scar, one huge laced sode on the near shoulder, a thick rope belt, suneate, and the tekko (a flared cuff with a storm-red band, the fist, a knuckle plate with three studs, a crackle on the near one), drawn a little brighter than his other iron so they read. He is drawn wider than Kage's frame (JS build w 14), and the 1.18 scale makes him the biggest sub-boss: a broad horned block next to Kuroe's tall column.

**Profile.** The JS archetype (`heavy`: parry 0, guard 0.12). The JS style (`41-styles/garan.js`: pref 120, the strings, a clinch after a flurry or a light cut, the posture lesson where a parried Garan loses far more posture) is not ported beyond the clinch on a guard; that is SAN-110's tuning.

**The bot fight** (`GaranBotFightTests`, seed 104, real health: Kage 100, Garan 170). An early version of the bot that walked back from his abilities was knocked out at 28 s with Garan on 145 of 170: the slam breaks a guard, the shock covers the walk back, and the palms keep coming. The Garan bot follows the JS tips instead. Through Storm Fist's wind-up and the Tsuppari crouch it closes in and rolls through him to his back (both go the other way) and hits him there, and dashes away when there is no time. It rolls through the charge as it comes (he meets the wall). It guards his other blows, low against a low one, and holds Guard at no other time, so the clinch (guards only) seldom comes. It wins in **4993 frames = 83.2 s** (the spec's 60 to 90 s for a sub-boss), Kage on 36 of 100, through both phases: 12 Storm Fists (1 slam and 3 shocks landed), 10 Tsuppari (no palm landed), 1 charge (dodged), no clinch, 20 rolls; never 10 s without a hit, block, parry or counter. Run inside the full PlayMode suite the same seed gives 5059 frames = 84.3 s (Kage on 52): something earlier in the suite leaks into it, as the group-fight flake (SAN-115) suggests; both are in the range.

**Review images** (`GaranScreenshots`, `Screenshots/review/SAN-104/`): `garan_beside_kage`, `garan_vs_kuroe_vs_footman`, `storm_fist_windup` (with the ground mark: that capture runs a `Vfx` on real frames), `storm_fist_slam` (the shock striking Kage), `tsuppari_crouch`, `tsuppari_palm`, `clinch_carry`, `charge_run`, `garan_arena_phase1`, `garan_arena_phase2` (the districts backdrop, the boss bar, the phase two banner).
