# ADR-0010: JS content tables, units and the health scale

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-62 (builds the export), SAN-64, SAN-66, SAN-70, SAN-84, SAN-89, SAN-95, SAN-96, SAN-102 to SAN-110

## Context

Many tickets need JS numbers: weapon, helmet and armour stats and prices, enemy health and multipliers, boss phases, the Prologue and Act I stage list with stretch encounters and rewards, AI archetypes. Typing them by hand invites mistakes, and the JS `10-data.js` file isn't the final word: later modules change entries at load time (Garan's health is 420 in `10-data.js` and 395 once the game has loaded; sub-boss ability lists grow in `41-styles/*.js`). The JS game uses pixels and seconds; Unity uses metres and 60 Hz frames. Unity health today is 100 for Kage, and the JS player starts at 232.

## Decision

### Export, don't transcribe

- `Tools/js_export/export.js` (Node, created by SAN-62) loads the JS game with its own headless loader (`E:\PROJECTS\KAGE\KAGE\tests\helpers\load-sim.js`, read-only) and writes **`Assets/Kage/Rules/Generated/JsTables.g.cs`**: C# object literals in `Kage.Rules`. Values are taken after every JS module has loaded.
- The generated file is never edited by hand. To change a JS value, change nothing in Unity: re-run `node Tools/js_export/export.js` against the JS game. To change a Unity value, use a Unity field (`healthMul`, tuning files), not the generated table.
- The file starts with a header naming the JS commit or file date it came from.
- Contents:
  - `Weapons`: every weapon, with id, name, class, tier, JS `base`, hands, length, stats `[damage, speed, reach]`, price, gems, exclusive flag and lock hint, and enemy-only flag.
  - `Helmets` and `Armors`: all of them (19 and 28 in the JS game), with tier, defence, health, price or gems, perks, look id, exclusive flag and hint. Visuals exist only for the 25 Act I items; the rest are data and icons.
  - `GearTiers`, `GearMax` (weapons 8, helmets 5, armour 5), upgrade costs, player constants (base health 220, +4 per level, kunai ammo 5, XP per level), `Dev` (coins 99,999, gems 999).
  - `Enemies`: the 13 Prologue and Act I foes, sub-bosses and Raikon: name, title, type, AI archetype, weapon, health, defence, damage and speed multipliers, build scale, `heavyArmor`, `noGrab`, and `phases` for bosses. Also `hollow_husk`, which the JS Stretch IV uses (see the SAN-109 comment).
  - `BossAbilities` (SAN-103): every ability the exported foes use (their phases' lists), from the JS `BOSS_ABILITIES` (cooldown s, range px, chance) and, for the Act I sub-bosses' reduced Raikon moves, `RAIKON_VARIANTS` (base move, label, stance, punish delay, damage multiplier, unblockable, wind-up, shockwave, reach, dash, wave). `JsTables.BossAbility(id)`.
  - `Moves`, `Projectiles`, `AiRoles`, `Difficulties`, `FightStyles` (SAN-104): the basic moves a sub-boss ability is measured against or built from (`MOVES.p1`, the jab, `MOVES.grab`, and since SAN-105 `MOVES.tw1`, the twin blades' first cut that ends Hisame's Kocho: timing, damage, box), every `PROJ` row (the Storm Break shockwave `shock`, kunai, orbs...), the `BUDO.roles` multipliers (a sub-boss's tell is the mode's wind-up × the `sub` role's 0.98), the `DIFFICULTIES` multipliers, and the exported foes' `FIGHT_STYLES` (the school and the strings: each string's moves, range in px, weight, tell, and whether it is played only in a situation; since SAN-106 also the style's `poise`, 1 when it has none). `JsTables.Move(id)`, `Projectile(id)`, `AiRole(id)`, `Difficulty(id)`, `FightStyle(id)`.
  - `ShidenBrace` (SAN-106): Shiden's Braced Spear, the const `SHIDEN_BRACE` of `41-styles/shiden.js` (hold s, reach px, trigger px, damage, open s), which is not a `BOSS_ABILITIES` field.
  - `Ai`: the JS `AI_PROFILES` (regular, agile, defensive, ranged, heavy, boss) as a design brief for SAN-95.
  - `Campaign`: the `prologue` and `act1` chapters: stage ids in play order, kind (`stretch`, `subboss`, `boss`), enemy, fight format, rewards, story scene ids (kept, not played), and for stretches the road length, start, encounters (position, gate, waves), tutorial steps and clue ids.
- An EditMode test checks a handful of exported values against the JS rules tests' expected values (SAN-62).

### Units

| JS | Unity | Rule |
|---|---|---|
| pixels (distances: stretch length, encounter and gate positions, ability ranges, AI preferred range) | metres | `JsUnits.PxPerMetre = 115` (JS body about 200 px tall, Kage about 1.75 m), in `Kage.Rules` |
| seconds (cooldowns, telegraphs, durations) | frames | × 60, rounded |
| JS move timings, damage and knockback | — | design brief only, never converted (the spec keeps Unity's move values) |

`PxPerMetre` is the one place to change if stretches feel too long or too short.

### Health and the health scale

- `HealthScale` lives in `Tuning/health.json` (ADR-0001). Its default is **100 / 232 ≈ 0.431**, so Kage's fresh-save health (JS 232: base 220, Crimson Scarf +4, Shinobi Garb +8) stays at today's 100.
- Every fighter: `maxHealth = jsHealth × healthMul × HealthScale`.
  - Foes: `jsHealth` is the raw JS `hp` (Footman 170, Kuroe 360, Garan 395, Hisame 375, Shiden 400, Raikon 640), the numbers the tickets quote.
  - Kage: `jsHealth = GameRules.PlayerMaxHp(save)` (level and gear), so helmets and armour raise his health through the same scale (SAN-84).
- The JS game's Normal-difficulty and role multipliers on foe health (regular ×0.82, sub-boss ×1.05, boss ×1.22) are **not** applied. Pacing is tuned only by `HealthScale` and per-character `healthMul` (SAN-110).
- Unity's per-window damage values never change. JS `dmg`, `def` and `speed` become `damageMul`, `defense` and `speedMul` in character data (ADR-0005).

## Notes from the implementation (SAN-89)

- Stretch roads are read from the generated tables by `StretchData.FromStage(stageId)` (`Kage.Rules`, pure C#): `Length`, `Start`, and each encounter's `X` (trigger), `Gate` and `Waves`, all converted to **metres with `JsUnits.PxToMetres`** (`act1.stretch1` is 4400 px = 38.26 m long, Kage starts at 1.39 m, its first gate is at 15.2 m). The JS rules `encounterAt`, `stretchLimit` and `isStretchComplete` (reach 60 px) are `EncounterAt`, `Limit` and `IsComplete`; the locked arena is the JS `STRETCH.arena` of 1500 px ending at the gate (`ArenaLeft`). Nothing about a road is typed in Unity except the test stretch `dev.footmen` (`StretchData.DevFootmen()`, not JS data: 3000 px, two encounters of Footmen), which exists so a road can be played with the one foe that is built.
- Clues, props and tutorial prompts of a stretch stay in the table (`JsStretch.Clues`, `Tutorial`); SAN-89 does not read them (SAN-96 reads the prompts; clues are not built).
- `PxPerMetre` is still the one place to change if roads feel too long or too short.

## Notes from the implementation (SAN-96: Unity additions to the JS Prologue)

The JS `prologue.1` ("The Empty Village") has one encounter (x 2600, gate 3200, wave `[hollow_tutorial]`) and five tutorial steps (move 0, jump 520, slash 1000, block 1420, dodge 1780 px); the ticket and spec stories 74 and 75 want a parry step and a Hollow Shade encounter as well. Those are **Unity-authored additions**, kept in one file and never written into the generated tables:

- `Kage.Rules/PrologueAdditions.cs`. `StretchData.FromStage("prologue.1")` stays the pure JS road (one encounter, five prompts, `Added` empty); `StretchData.FromId("prologue.1")` returns it with `PrologueAdditions.Apply`, and `Added` names what was put on: `prompt:parry`, `prompt:practice-partner`, `prompt:hints`, `encounter:hollow_shade`. The dev arena and the future campaign wiring read `FromId`.
- **Parry prompt** at 1600 px (13.9 m), between the guard prompt (1420) and the dodge prompt (1780).
- **Hollow Shade encounter**: trigger 3450 px (30.0 m), gate 4050 px (35.2 m), one wave `[hollow_shade]`, after the tutorial Hollow's gate (3200 px) on the 4200 px road, so the road keeps its 36.5 m length and the stage still ends 60 px short of the end. Encounter ids are `prologue.1.e1` (the tutorial Hollow) and `.e2`.
- **Practice partner** (`Partner = "hollow_tutorial"`) on the guard, parry and dodge prompts, and **hints** reworded for those three (the JS block hint described the parry too): guard "Hold Guard as the blow lands", parry "Press Guard just before the blow lands", dodge "Dash just before the blow lands: a perfect dodge".
- **JS data read for the first time** (`StretchData.FromStage`): the prompts (`Prompts`; JS `slash` is the **attack** and `block` is the **guard**, `PromptStep.FromJs`; hints from `JsTables.TutorialSteps`), the clues (`Clues`: id, label, scene id, x in metres) and the stage's story scene ids (`SceneBefore`, `SceneAfter`, `SceneDefeat`), plus the chapter's id and name. Clues and scenes are **in the data and not played** (spec: no story).
- Not changed: the road length, start, the tutorial Hollow's encounter and the five JS prompt positions (`PrologueDataTests` compares them with the JS numbers divided by 115).

## Consequences

- JS numbers reach Unity by one script, so they can be checked and regenerated, and the rules tests match the JS tests.
- `Kage.Rules` carries a large generated file. Diffs to it are reviewed as data.
- Dropping the JS role multipliers changes the health ratio between regulars and bosses by up to ×1.5 compared with the JS game on Normal. If pacing needs it, SAN-110 puts that back per character with `healthMul`. This is listed as a question for the owner.

## Implementation notes (SAN-110: the pacing pass)

**What changed.** The shared health scale stays **100 / 232**. Moving it would move Kage (fresh save 100) and the Ronin, and re-record about thirty baseline replays whose `hp=` text is the Kage-versus-Ronin fight; and every foe needs a different factor anyway. So the pass is per-character **health multipliers** (`CharacterDef.healthMul`), kept as rows in `Tuning/health.json` (`HealthTuning.multipliers`, read by `CharacterDef.FromJs`, so no roster file changed). Kage has none. No move damage, defence, speed or AI knob changed: `PacingTuningTests` pins every foe's damage, defence and speed multiplier to the JS table and a checksum of every hit window's damage (7 weapon families and every ability), and the diff touches no `Combat/` or `Boss/` file.

| Character | JS hp | multiplier before, after | Unity health before, after |
|---|---|---|---|
| hollow_tutorial | 95 | 1, 1 | 41 |
| hollow_shade | 160 | 1, 1.4 | 69, 97 |
| order_footman | 170 | 1, 1.65 | 73, 121 |
| order_runner | 150 | 1, 1.35 | 65, 87 |
| order_chainthrower | 150 | 1, 1.7 | 65, 110 |
| order_shieldbearer | 220 | 1, 1.1 | 95, 104 |
| hollow_flicker | 140 | 1, 2.5 | 60, 151 |
| order_ironclad | 280 | 1, 1.35 | 121, 163 |
| hollow_husk | 270 | 1, 1.4 | 116, 163 |
| kuroe | 360 | 1, 1.15 | 155, 178 |
| garan | 395 | 1, 1.25 | 170, 213 |
| hisame | 375 | 1, 1.2 | 162, 194 |
| shiden | 400 | 1, 1.05 | 172, 181 |
| raikon | 640 | 1, 1.4 | 276, 386 |

**Expected gear** (`PacingKit.Loadouts`, through the real rules). A new save clears the ten stages in order, each a B-rated first clear (coins 250 at the start, then 365, 480, 735, 850, 1105, 1220, 1475, 1590 and 1845 before each stage; 2240 in all), spends every coin on Kagetsu upgrades before each stage, and wears the best helmet and armour it owns (most defence). Gems are not spent. Resulting loadouts: Prologue Kagetsu L1, starter helmet and armour, level 1; Stretch I and Kuroe Kagetsu L2; Stretch II Kagetsu L2, Sumi Eboshi, level 2; Garan Kagetsu L3; Stretch III Kagetsu L3, Ikazuchi Do, level 3; Hisame Kagetsu L3; Stretch IV and Shiden Kagetsu L4; Raikon Kagetsu L4 and Shiden Yoroi, level 4. Kagetsu L4 is +24 % damage, so TTK barely depends on the gear plan; his defence does (0.03 at the start, about 0.4 from Stretch III).

**Measuring convention.** TTK is the time the foe (or a wave's foes) is alive, first frame to last knock-out. Kage is **helped**: the gear above with 20 times the health and his normal damage, so the clock reads his damage against the foe's real health. The same fights are played with the real Kage and the win count is reported beside. Regular foes are one on one against `AIInputSource` as Kage (level 0.9, the stretch bot) with the foe at the road level 0.5; waves are the real roads (`StretchFight`, `RoadWalker`); sub-bosses and Raikon use the four JS-tip bots and `StormBot` at levels 0.7 and 0.85. Foes use 5 seeds, the rest 3 (110 to 114). Targets: regular foe 15 to 25 s; a stretch wave about 45 s, asserted as **35 to 55 s on the mean wave of each stretch** (a wave's time grows with its foe count: two foes 26 to 44 s, three 48 to 60 s, four 60 to 68 s, so no single band fits every wave); sub-boss 60 to 90 s; Raikon 120 to 180 s. `PacingReportTests` logs the table and asserts the bands, with `Exceptions` for rows allowed outside.

**The report** (helped TTK in seconds; "bot wins" is the same bot with real health):

| Row | Target | TTK | Bot wins |
|---|---|---|---|
| hollow_tutorial | 15-25 | 7.8 (exception) | 5/5 |
| hollow_shade | 15-25 | 19.4 | 5/5 |
| order_footman | 15-25 | 21.0 | 5/5 |
| order_runner | 15-25 | 19.0 | 5/5 |
| order_chainthrower | 15-25 | 17.8 | 5/5 |
| order_shieldbearer | 15-25 | 19.5 | 5/5 |
| hollow_flicker | 15-25 | 15.7 | 5/5 |
| order_ironclad | 15-25 | 20.2 | 5/5 |
| hollow_husk | 15-25 | 20.5 | 5/5 |
| Prologue mean wave | about 45 | 11.1 (exception) | 3/3 |
| Stretch I mean wave (waves 29.7, 39.6, 35.6, 59.8) | 35-55 | 41.2 | 0/3 |
| Stretch II mean wave (30.2, 43.5, 36.4, 59.7, 48.4) | 35-55 | 43.6 | 0/3 |
| Stretch III mean wave (30.1, 40.0, 57.2, 39.6, 26.7, 67.5) | 35-55 | 43.5 | 0/3 |
| Stretch IV mean wave (32.9, 29.3, 26.4, 44.1, 48.6, 60.2) | 35-55 | 40.3 | 0/3 |
| Kuroe | 60-90 | 67.4 | 2/3 |
| Garan | 60-90 | 74.1 | 3/3 |
| Hisame | 60-90 | 73.9 | 3/3 |
| Shiden | 60-90 | 74.6 | 3/3 |
| Raikon | 120-180 | 158.2 | 3/3 |

Before the pass the same report read: regular foes 6 to 18 s (Flicker 6.3, Footman 12.2), mean waves about 25 to 38 s, Kuroe 65, Garan 60, Hisame 63, Shiden 73 and Raikon 109 s.

**Exceptions.** (1) The tutorial Hollow (7.8 s) is the Prologue's practice dummy (JS hp 95, damage x0.3), short on purpose; so the Prologue's mean wave (11 s) is not a stretch wave. (2) Single waves outside 35 to 55 s are not asserted (see above); the stretch means are. (3) **The bot loses every stretch with real health** (0 of 3 on all four, as in SAN-109): the stretch bot is the AI playing Kage and a group of regular foes beats it. The shared scale cannot fix that, because Kage and the foes scale together and the health race stays the same. It is bot weakness, not a measured player result; the owner's Act I play is the real test. (4) Flicker needed x2.5 to reach 15.7 s: at x1 he fell in 6 s.

**Guard-break conversion (ADR-0008).** One rule for every boss would be Hisame's (a JS `gb` takes the same share of Kage's posture as of the JS guard). Garan's Tsuppari palms (gb 24) are about 1.6 times heavier than that rule gives (3.08 against 1.92). Both are `postureMul` values written into each ability's hit windows, and there is no per-character multiplier for posture, so it cannot be done with multipliers: **listed as an exception, not changed.** The fix is a three-line change in `GaranAbilities.PostureMul`, the owner's call (it changes Garan's guard feel, not his damage).

**Tests whose expectations changed:** the 13 EditMode data tests and 8 PlayMode spawn tests that assert a foe's health (now `JS hp x multiplier x 100/232`); `CharacterDataTests` (Footman 120.9, Raikon 386.2) and `RaikonFightDataTests`; `EngagementTests.No_more_than_two_foes_attack_on_any_frame` (foe attacks > 10, was > 20; the seed gave exactly 20); `KuroeBotFightTests` seed 104 (was 103: the bot loses seed 103 with Kuroe at x1.15; it wins 20 of the 40 seeds 90 to 129); `StretchSessionTests.Nobody_leaves_the_floor_bounds...` seed 90 (the default 89 let a Footman end 5 cm past the wall gap; several seeds overshoot by up to 0.43 m, a latent leak in the SAN-116 area); `SagaBotCampaignTests`: the in-order run fights the bosses with the expected gear (Raikon at x1.4 was lost 8 of 8 times with the starter Kage) and the boss quiet bar is 900 (was 600; one Garan loss was quiet for 12.5 s).

**Open for the owner.** Parry stagger for regular foes is the JS 0.95 s against the ticket's about 0.5 s (`parry.json`); the perfect dodge is 0.4 for 24 frames against the JS 0.3 for 0.75 s (`dodge.json`); neither was touched. Regular foes got x1.1 to x2.5 health, which makes Act I harder than before; if that is too hard, raise Kage's side with the shared scale instead (it re-records the baseline replays). The raikon.js style's extra techniques (Ichi no Tachi, Tai-atari, Raiko-gaeshi, Raijin Daiko, Kiriage) are not ported. The owner plays the Act I run to confirm the pacing (acceptance 4, left open).
