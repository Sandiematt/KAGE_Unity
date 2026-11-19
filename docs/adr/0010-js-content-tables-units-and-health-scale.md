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

## Consequences

- JS numbers reach Unity by one script, so they can be checked and regenerated, and the rules tests match the JS tests.
- `Kage.Rules` carries a large generated file. Diffs to it are reviewed as data.
- Dropping the JS role multipliers changes the health ratio between regulars and bosses by up to ×1.5 compared with the JS game on Normal. If pacing needs it, SAN-110 puts that back per character with `healthMul`. This is listed as a question for the owner.
