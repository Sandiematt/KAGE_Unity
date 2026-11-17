# KAGE — The Last Betrayal: JS → Unity Migration Plan (v3)

| | |
|---|---|
| **Date** | 2026-11-17 |
| **Revision** | v3. Replaces v2 with every decision of the 2026-11-17 grilling session: **Prologue and Act I only, built on Unity combat resolved by physics, a new defence layer, weapon families, gear visuals, dev mode, JS-style UI, desktop controls now.** |
| **Source game** | `E:\PROJECTS\KAGE\KAGE` — HTML5 Canvas / vanilla JS. Read-only reference. |
| **Target** | `E:\PROJECTS\KAGE\kage_unity` — Unity 6000.6.9f1, URP 2D, Input System 1.20.1. Repo: `https://github.com/Sandiematt/KAGE_Unity` (single `main` branch, no LFS). |
| **Status** | Plan. The work is tracked in Linear: spec SAN-59, batch plan SAN-111, ticket SAN-60 onward. |
| **Companion documents** | [`KAGE_Act1_Spec.md`](KAGE_Act1_Spec.md) (local copy of SAN-59: user stories, implementation and testing decisions), [`adr/`](adr/) (ADR-0001 to ADR-0010: the shared interfaces), [`../GLOSSARY.md`](../GLOSSARY.md) (terms). If this plan and an ADR disagree, the ADR wins; if the spec and this plan disagree, the spec wins. |

---

## 1. What changed from v2

| v2 | v3 |
|---|---|
| Whole game: 36 stages, Acts I–III, Interlude, Core, Epilogue, four story bosses, modes | **Prologue and all of Act I**, fights only (no story). Raikon is the only story boss. Everything else is out of scope (Section 11). |
| JS blocking, parry, posture and dodge slow-mo were **dropped** | **A new defence layer designed for Unity** (Section 5): Guard button, timed parry, posture on every fighter, perfect dodge with slow motion, weapon clash. Not a port of the JS rules. |
| Seven weapon families, hammer included | **Six families** (katana, twin blades, spear/staff, chain sickle, cleaver, fists) plus **Raikō no Tachi**, a heavy tachi. Hammer is out of scope. |
| Rage arts deferred to the VFX stage | **Crimson Moon** (Kagetsu) and **Thousand Shadows** (every other weapon) are built now, with placeholder effects. Other rage arts are out of scope. |
| Kage had Crescent and Thousand Shadows only | Adds **kunai** (Special + Down), a **Rage bar** that is the existing meter, and perfect-dodge meter gain. |
| Gear was data only | **Gear visuals now** for 25 items (14 helmets, 11 armours): the starter pieces, every shop item and the Act I rewards. |
| UI was a later phase | **UI rebuilt to look like the JS game** with its fonts and images. |
| Platforms undecided | **Keyboard and gamepad now.** Touch comes before Android, behind the same input interface. |
| Art route: vector export of JS drawing code (option B), pilot first | **Opponents are designed fresh in code** as vector parts on Kage's 20-bone skeleton. Raikon is built from the owner's 7-colour SVG with a new importer. No JS art export. |
| Roadmap in phases, VFX last | Ordered work list and a **batch plan in waves** (SAN-111). **Two owner sign-offs** block the next step: after the physics pass and after the defence layer. |
| JS data via JSON exports and ScriptableObjects | JS tables are **generated C#** (`JsTables.g.cs`), never typed by hand (ADR-0010). |
| Health scale ≈ 0.41, a guess | **`HealthScale` = 100 / 232**, so Kage's fresh-save health stays at 100. Foe health is the raw JS value; the JS Normal role multipliers are not applied. |

---

## 2. Ground rules

**Unity owns the mechanics. JS owns the content.**

1. **One combat system: the one already in Unity.** The state-machine `Fighter`, frame-based `MoveDef` / `Pose` / IK moves, hit windows and levels, throws, juggles, knockdown, ragdoll KO, Mirror Guard counter and the Crescent / Ultimate meter moves. **Every opponent is a `Fighter` fed through `IInputSource`.** No second combat path exists for bosses.
2. **Combat keeps the current Unity feel** but is resolved by Unity physics (Section 4). A replay recording made before the change proves that nothing that should stay the same has changed.
3. **JS combat logic is not ported:** the fighter, combat, budo and AI code, the JS damage chain, JS posture, JS parry timing, juggle rules and impact tiers. JS is used for *who* the characters are (name, role, health, weapon family, phases, ability lists) and for the economy and rules, not for how a fight resolves.
4. **Move data stays in C#** in the existing fluent API. Characters, weapons, gear, stages and bosses are data.
5. **Scenes and stages stay built in code** with Unity's built-in assets for now. Final stage art comes later from the owner. Exception: the UI uses the JS game's fonts and images.
6. **Content is exported, not transcribed.** JS values come from the JS game's own headless loader (`tests/helpers/load-sim.js`), because later modules override entries in `10-data.js`.
7. **The JS game folder is read-only.**
8. **Every effect call goes through a named cue** whose id equals the JS VFX manifest id. Until the VFX stage, cues play cheap placeholders from the existing `Vfx` class.

---

## 3. What exists today (Unity, verified by reading the code)

~7,500 lines of C# in 29 scripts, namespace `Kage`, compiled into `Assembly-CSharp` with no asmdef. The scene is built at runtime by `KageGame`.

| Area | Files | What is there |
|---|---|---|
| Combat core | `Combat/Fighter.cs` (1,329 lines), `MoveDef.cs`, `KageMoves.cs`, `PoseLib.cs`, `FighterInput.cs`, `AIInputSource.cs`, `ScriptedInput.cs` | `FState` machine, `MoveDef` keyframes at 60 fps with `HitWindow`s, hit levels and points, chains, cancels, invulnerability, pass-through, throws, counters, Rigidbody2D locomotion, hold-back guard, juggle with 0.8× decay, Mirror Guard, Crescent, 100-point meter, input buffers |
| Character | `Character/*` | 20-bone skeleton, Kage built from a traced SVG (`Tools/svg_export`), IK, Verlet ragdoll, soft scarf, hair and coat |
| Feel / VFX / audio | `GameFeel`, `Vfx`, `Crescent`, `KageAudio` | Hit-stop, slow motion, shake, bursts, 14 synthesised sounds |
| Stage / camera | `Stage/*` | One arena, multi-target camera, training bag |
| UI | `UI/Hud.cs`, `InkBar.cs` | Placeholder uGUI HUD |
| Loop | `Core/KageGame.cs` | Duel (best of 3) and Training against one AI opponent |

Today's hit detection is hand-rolled distance checks, fighter-vs-fighter push is hand-rolled, knockback writes velocity directly, crits use `Random.value`, hitstop and slow motion use wall-clock time, and each fighter has its own `FixedUpdate`. ADR-0002 fixes the determinism problems.

**Not present:** any character besides Kage and a recolour, weapon variety, bosses, phases, projectiles besides the Crescent, campaign, save, shop, menus, tests, version control (SAN-60 fixes the last one).

### JS content in scope

| Content | Count in scope |
|---|---|
| Stages | Prologue (1), Act I (9: four stretches, four sub-boss fights, Raikon) |
| Regular foes | 7 types: Order Footman, Runner, Shieldbearer, Chainthrower, Ironclad; Hollow Shade, Hollow Flicker; plus the tutorial Hollow |
| Sub-bosses | Kuroe, Garan, Hisame, Shiden (2 phases each) |
| Boss | Raikon (640 HP, 3 phases) |
| Weapons | Kagetsu (start), Tsubame, Shizuka, Onikiri, Mizuchi, Yamigama, Raikō no Tachi (boss reward), fists |
| Gear visuals | 14 helmets, 11 armours; the rest of the 19 helmets and 28 armours are data and icons only |

---

## 4. Combat on Unity physics (the physics pass)

The goal is that combat is resolved by Unity's physics instead of distance checks and velocity writes, **without changing move data or the feel of Kage's existing moves.** Details: ADR-0002, ADR-0007.

| Part | Today | After |
|---|---|---|
| **Fight loop** | Each fighter runs its own `FixedUpdate` | One `FightLoop` steps all fighters in a fixed order at 60 Hz, under a `FightContext` with a seeded random source, a fight clock and the event stream (ADR-0002) |
| **Hurtboxes** | `RigDef.hurt` circles tested by hand | Trigger colliders on a `Hurtbox` layer, parented to bones, carrying owner and hit zone. Props use the same component. |
| **Hitboxes** | Per-frame distance tests, which can miss fast blade frames | Each active hit point is swept from its previous to its current position with a circle cast against the `Hurtbox` layer, skipping the owner and its team. The per-window hit log still limits each window to one hit per target. |
| **Push** | `KageGame.Pushbox` | Push colliders on a dedicated layer. Pass-through frames switch layers. |
| **Knockback** | Velocity written directly | Impulse scaled by per-character mass (a mass-3 character is pushed visibly less than Kage) |
| **Throws** | Animated arc | Same arc, released into a physics launch |
| **Settings** | Layer matrix and materials set in code | `ProjectSettings` layers and collision matrix, `PhysicsMaterial2D` assets |
| **Ragdoll** | `Ragdoll2D` | Unchanged |
| **Legacy switch** | | The old resolution stays selectable until the owner signs off |

**Acceptance:** the replay produces the same hits on the same frames with the same damage; knockback distances stay within a tolerance; no hit is lost or counted twice on fast moves (`cleave`, `shadowStep`, `airKick`); the owner plays a build and signs off. Moving hit resolution after all fighters tick can shift a simultaneous hit by a frame; such differences must be listed and justified.

The `Fighter` is first split into partial classes behind the same public interface (nine partials, ADR-0001), so parallel tickets edit different files.

---

## 5. The defence layer (new, designed for Unity)

JS defence rules are not ported. The numbers below are starting values, all kept in data. Details: ADR-0004.

- **Guard:** a dedicated Guard input on the frame input and on every input source. Holding Guard guards; Guard with Down guards low; walking backward no longer guards. High / low / overhead rules apply unchanged.
- **Parry:** pressing Guard within about 8 frames before a hit lands parries it. The attacker staggers about 0.5 s and takes posture damage. A mistimed press locks out parrying for about 0.33 s but still guards. Unblockable windows can't be parried.
- **Posture:** every fighter has a posture bar (maximum from character data, larger for bosses). Blocking adds hit damage × about 1.5. Clean hits add a little; parries add nothing. Posture drains after about 1 s without blocking or being hit. A full bar breaks the guard: about 0.75 s helpless and a guaranteed critical on the next hit.
- **Perfect dodge:** a dash or roll started within about 8 frames before a hit would land triggers about 0.4 s of world slow motion (Kage at full speed), meter gain and an afterimage. Player only.
- **Clash:** two blade hit windows whose swept paths cross in the same frame deal no damage; both fighters recoil, take a short hit-stop and lose a little posture, with sparks. Fist and foot hit points never clash.
- **Resolution order and hook points** (`PreHitCheck`, `ParryCheck`, `ArmourCheck`, `ResolveClashes`) are fixed in ADR-0004. The owner plays and approves this layer before work continues.

---

## 6. Abilities, weapons and gear

### 6.1 Kage's abilities
- **Kunai:** Special with Down held, a limited count per fight from the loadout, shown on the HUD. Uses the generic projectile.
- **Rage bar:** the existing 100-point meter.
  - Kagetsu: a full bar triggers **Crimson Moon**: world freeze, cut-in band with name and kanji, camera close-in, unblockable hits on the JS timing, a finisher with hit-stop and slow motion. Placeholder effects.
  - Every other weapon: **Thousand Shadows**.
  - **Crescent** stays a normal meter move.
  - Special + Down = rage art if the bar is full, else kunai, else nothing (ADR-0004).
- **Generic projectile and hazard** replace the one-off Crescent. A hazard has a telegraph (ground mark) then a strike (ADR-0007).

### 6.2 Weapon families (ADR-0006)
Each family is a full player-quality move set covering every button, selected by the equipped weapon. Enemies use the same sets.

| Family | Weapons / users |
|---|---|
| Katana | Kagetsu, Shizuka (own stats); Footman, Shade, Takashi-style foes. Kage's existing moves, unchanged. |
| Twin blades | Tsubame; Runner, Flicker, Hisame |
| Spear / staff | Mizuchi; Shieldbearer, Kuroe, Shiden |
| Chain sickle | Yamigama; Chainthrower |
| Cleaver | Onikiri; Ironclad |
| Fists | Garan, usable by Kage unarmed |
| Heavy tachi | Raikō no Tachi (Raikon's weapon and the reward for beating him): long reach, slow, high damage and posture damage |

- Weapon data and prices come from JS. Combat multipliers follow the JS rule relative to the family's reference weapon: +8 % damage, +2.5 % speed, +1.5 % reach per level, speed and reach capped; upgrades to level 8.
- A per-character move rate and damage multiplier let one family play faster or slower without copying it (Footman at 0.9).

### 6.3 Gear
Kage starts with the JS starter helmet and armour. Gear has JS health, defence and perks, upgradeable to JS limits. Equipped gear is drawn on Kage as part sets layered on his bones, without changing his base design. Act I sub-boss and Raikon rewards grant their JS gear.

---

## 7. Opponents

All designed fresh in code as vector parts on Kage's 20-bone skeleton. Art never moves a gameplay joint, and hurt colliders belong to the gameplay skeleton. Data shape: ADR-0005.

### 7.1 Regular foes
| Foe | Family and behaviour |
|---|---|
| Tutorial Hollow | Katana, slow and gentle, Prologue |
| Order Footman | Katana at move rate 0.9. Calibration foe. |
| Order Runner | Twin blades, agile, dashes through |
| Order Shieldbearer | Spear and shield, frontal armour, defensive |
| Order Chainthrower | Chain sickle, lashes from mid range |
| Order Ironclad | Cleaver, heavy, armour (no flinch from light hits), high mass |
| Hollow Shade, Hollow Flicker | Katana / twin; glitch tint; appear in Act I squads |

The JS Stretch IV's last wave has a `hollow_husk` with no ticket; Ironclad stands in until the owner decides. The JS Prologue has one encounter and five tutorial steps; the Unity Prologue adds a Shade encounter and a parry step as Unity-authored stage data.

### 7.2 Sub-bosses (two phases each, visible change at the switch)
| Boss | Family | Signature (intent from JS ability lists, ADR-0008) |
|---|---|---|
| Kuroe | Staff, defensive | Raised-staff guard counter |
| Garan | Heavy fists | Armoured grapple |
| Hisame | Twin blades, agile | Mist step teleport |
| Shiden | Spear | Long charging thrust |

### 7.3 Raikon
- **Look:** from the owner's SVG (a 7-colour trace, one compound shape per colour), cut into rig parts by region by a new importer: hat and tassels, head, ponytail, fur mantle, shoulder plates, torso, arms, hands, waist rope, skirt panels, legs, boots, cape, tachi. The hidden far arm and leg are drawn to match. Cape, ponytail, tassels and mantle are soft physics parts. The owner approves him from a side-by-side image against the painting.
- **Fight:** heavy tachi set. Thunder Swing (ground lightning to jump or dash through), Storm Break (ground slam with a shock area), Skyfall (red mark on the ground, then a bolt), Redline Counter from phase 2.
- **Phases:** 3, at 66 % and 33 % health. From 33 %, a late second Thunder Swing wave and more aggression.
- **Flags:** armour (resists flinching) and no-grab.
- **HUD:** boss bar with phase marks, name, title and art card.

### 7.4 AI
- **Profile-driven AI** wraps the existing utility AI with data (preferred range, aggression, guard and parry rate, move weights, reaction frames, combo length), one profile per JS archetype: regular, agile, defensive, ranged, heavy, boss. Boss abilities are chosen like button presses that map to ability slots (ADR-0008).
- **Multiple targets:** `opponent` means "current target", chosen by a "nearest in front" rule. At most 2 attackers engage at once while others circle. The camera frames all engaged fighters.
- **Phase controller:** health thresholds swap the ability list, AI profile, speed, damage and flags from boss data.

---

## 8. Stages, flow, economy, save and UI

### 8.1 Stages and flow
- **Stage data** for the Prologue and Act I from the JS campaign: id, kind (stretch, sub-boss, boss), enemy, squads, rewards, environment; story scene ids kept but not played.
- **Prologue:** a walk with tutorial prompts (move, jump, attack, guard, parry, dodge) at points along the road, each showing the current key; it ends with the tutorial Hollow and the Shade encounter.
- **Stretch:** a side-scrolling walk where a gate closes and waves of 2–4 foes arrive. HUD road progress strip and wave label. Each stretch leads to its sub-boss arena; the fourth leads to Raikon.
- **Fight session:** one opponent; boss fights have one round and no clock; sub-boss fights follow the JS format.
- **Flow:** title → campaign map → pre-fight loadout → stage → results → map, plus pause. A plain title card starts each stage instead of story scenes. `KageGame` stays as the dev arena; the campaign flow is new files and a new scene.

### 8.2 Rules, economy and save
- **Rules module** (`Kage.Rules`, pure C#, no Unity): stage unlocks and completion, rewards (coins, gems, first-clear and rating multipliers), the S/A/B/C rating (parries count), buy / upgrade / equip, item tiers and locks, weapon multipliers, player health and defence from gear, XP and level. Difficulty fixed at Normal. JS rules tests' expected values are reused as fixtures.
- **Save:** the JS save schema as JSON in the persistent data path, written atomically, with a corrupt-file fallback (ADR-0009). A new save starts with Kagetsu, the starter helmet and the starter armour at level 1.
- **Dev mode:** a save-level flag, **on by default for now**. Unlocks every stage, owns every weapon, helmet and armour, grants enough coins. Turning it off starts the real JS economy from a fresh save.
- **Health scale:** one shared multiplier on JS health for foes, Kage and gear bonuses (`HealthScale` = 100 / 232). Unity's per-window damage values never change (ADR-0010).
- **Pacing targets on Normal:** regular foes 15–25 s, a stretch wave about 45 s, a sub-boss 60–90 s, Raikon about 2–3 min with the gear available by then.

### 8.3 UI
Rebuilt to match the JS screens with the JS fonts (converted for Unity text, kanji subset) and images: title, campaign map, Armory, pre-fight loadout, results, settings, pause. HUD: Kage's panel (portrait, level, health with damage trail, Rage bar, kunai count), enemy panel, boss bar, clock and round pips where they apply, combo counter, pause button, plus **posture bars** for Kage and his current opponent.

### 8.4 Controls
Keyboard and gamepad mappings for move, jump, crouch, punch, kick, weapon, special, guard, dash and pause, in one `KeyBindings` table. Input stays behind the shared input interface so a touch layout can be added for Android without touching combat. Android build support isn't installed; all testing is in the Editor.

---

## 9. Order of work and gates

1. **Version control** (SAN-60: git baseline, this plan) and the replay recording of every current Kage move
2. Fighter split, seeded randomness, event stream, physics pass → **owner sign-off**
3. Guard, parry, posture, perfect dodge, clash → **owner sign-off**
4. Character data and the opponent art pipeline (Footman, Shade)
5. Weapon families, Raikō no Tachi, kunai, Crimson Moon
6. Multiple targets, stretch session, tutorial → **the Prologue is playable**
7. Act I regular foes and AI profiles
8. The four sub-bosses
9. Raikon
10. Rules port, save, gear stats and visuals, dev mode
11. JS-style screens and HUD

The 14 waves of tickets that implement this, with their bottlenecks and risks, are in the SAN-111 batch plan comment on SAN-59. The two sign-offs block the next step; other steps proceed when their checks pass. Each finished piece is committed and pushed to `main`.

---

## 10. Testing

A good test drives the game as a player or the game loop would and checks only observable outcomes: events, health, posture and meter values, fighter states, save contents, coin totals. It never reaches into private fields.

| Seam | How | Covers |
|---|---|---|
| **Fight simulation through the input interface** | Build a fight from data without the game bootstrap, drive every side with scripted input, step a fixed number of physics frames under a fixed seed, check the event stream and public state | The replay baseline of the current moveset; physics pass (same hit frames and damage, no tunnelling, one hit per window, mass); determinism (same script and seed give identical event logs); defence (parry window, lockout, unblockable, posture, guard-break, dodge window, blade-only clash); kunai and Crimson Moon; weapon families and upgrade multipliers; stretches (waves, at most 2 engaged); a scripted bot beats every Act I foe, sub-boss and Raikon on Normal with no softlock, phases at the right health, time-to-kill logged |
| **Rules as plain C#** | EditMode tests with no scene | Unlock order, rewards, rating, buy / upgrade / equip, tiers and locks, multipliers, health and defence from gear, save round-trip and fresh save, dev mode on and off |
| **Owner checks** | Not automated | Combat feel after the physics pass and after the defence layer (play a build); opponent art, Raikon against the painting and gear visuals (side-by-side screenshots); UI against the JS screens |

Assemblies, folders and test layout: ADR-0001.

---

## 11. Out of scope

- Story: scenes, dialogue, story player, clues and Prologue story text (ids are kept in the data).
- Acts II and III, the Interlude spar, The Core, the Epilogue; Akari, Takashi, The Witness, Core echoes, Young Rem.
- Echo Trials, Boss Rush, New Game+ and difficulties other than Normal.
- The hammer family (Iwakudaki) and rage arts other than Crimson Moon and Thousand Shadows.
- **The VFX stage:** real effects for cues, Effekseer, flipbook sheets, screen-space effects. Placeholders only. (When it comes: 63 authored JS effects, baked sheets in `src/img`; options are flipbooks first, native rebuilds for hero effects, or the Effekseer plugin.)
- Visuals for the 22 helmets and armour pieces from later acts or worn only by enemies.
- Touch controls, Android and iOS builds, mobile performance.
- Final stage art and environment look (the owner will direct it).
- Audio beyond the existing synthesised sounds.
- Porting any JS combat logic.

---

## 12. Risks and open questions

| Risk | Mitigation |
|---|---|
| The physics pass changes the approved feel | Replay baseline, legacy switch until sign-off, move data untouched, owner plays before work continues |
| Determinism: wall-clock hitstop, per-fighter `FixedUpdate`, `Random.value`, null-unsafe `Vfx.I` | ADR-0002 (fight loop, clock, seeded random) |
| `Fighter.cs` is monolithic and every ticket edits it | Split into nine partials first (ADR-0001) |
| Fractional move playback (move rate ≠ 1) gates every family and foe | SAN-70 builds it before the families |
| World time scale (the world slows, Kage doesn't) is a new mechanism; Crimson Moon's freeze reuses it | SAN-92, before the rage art |
| Raikon's SVG importer and soft parts | Owner approval loop on a side-by-side image |
| JS fonts with kanji in Unity text | Convert and subset early (SAN-67) |
| Integration of every stage and a full-act bot run | SAN-109 |

**Open questions for the owner** (from SAN-111):
1. Health ratio: foe health is the raw JS value; the JS Normal multipliers (regular ×0.82, sub-boss ×1.05, boss ×1.22) are dropped. Keep?
2. Hollow Husk in Stretch IV: no ticket builds one; Ironclad stands in.
3. Prologue: the Shade encounter and parry step are Unity additions to the JS Prologue.
4. Sub-boss signatures follow the JS ability lists; the ticket signatures are mapped onto them.

---

## 13. Appendix: where JS data goes

| JS | Unity |
|---|---|
| `10-data.js` and later overrides (read with `load-sim`) | `Tools/js_export` → generated `Assets/Kage/Rules/Generated/JsTables.g.cs` |
| `15-logic.js` | `Kage.Rules` (`GameRules`, `SaveData`) plus EditMode tests using the JS expected values |
| `11-sword-arcs.js`, `20-fighter.js`, `30-combat.js`, `13-budo*.js`, `40-ai.js` | Not ported. Design brief only. |
| `21-characters/**` | Not exported. Opponents are designed fresh in code; Raikon from the owner's SVG. |
| `60-engine.js` | `FightSession` / stretch session / flow controller |
| `55-title.js`, `65-splash.js`, `70-ui.js`, `71-theme.js`, `styles.css`, `src/img`, `src/fonts` | `UI/` (Flow, Screens), fonts and images |
| `vfx/vfx_manifest.json` | Cue ids only, until the VFX stage |
| `tests/*.test.js` | Expected values reused as fixtures in `Tests/EditMode` and `Tests/PlayMode` |

Existing Unity move vocabulary reused by opponents: `jab, cross, uppercut, crouchPunch, lunge, kick1, kick2, axeKick, sweep, pushKick, throwGrab, airKick, airPunch, airSlash, roll, backflip, groundPound, getUp, iai, risingMoon, cleave, shadowStep, lowReap, counterStance, counterSlash, crescent`.
