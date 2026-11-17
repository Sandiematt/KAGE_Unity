# Spec: KAGE in Unity — Prologue and Act I, playable on Unity combat

> Published as Linear issue [SAN-59](https://linear.app/sandiematt/issue/SAN-59/spec-prologue-and-act-i-playable-on-unity-combat) (project KAGE Unity, label `ready-for-agent`). The Linear issue is the source of truth; this file is a local copy.
> Source of decisions: the grilling session of 2026-11-17 that produced Migration Plan v3.

## Problem Statement

KAGE exists as a complete HTML5/JS game, and its Unity remake currently has one fighter (Kage), one recoloured opponent, one arena and one duel mode. The owner wants the Unity version to become the real game, with mobile as the eventual target. But the JS game's combat isn't what they want in Unity. They want the combat already built in Unity, which feels right, extended rather than replaced.

Today, from the owner's side:

- Only Kage can be played against. None of the JS game's opponents, sub-bosses or bosses exist in Unity.
- Hit detection is hand-rolled distance checks, which can miss fast blade frames. Knockback ignores how heavy a character is.
- Defence is only hold-back guarding. There's no parry, posture, guard-break, perfect dodge or weapon clash, and hold-back guarding is poor on a touch screen.
- Kage has one katana and fists. There are no weapon families, shop, upgrades, gear or save.
- There's no stage structure: no Prologue, no Act I, no walking stages with enemy waves, no boss fights with phases.
- The UI is a placeholder HUD that doesn't look like the JS game.
- The project has no version control, so the approved combat feel can't be protected.

## Solution

Make the Prologue and all of Act I playable in Unity, as fights only (no story yet), built on Unity's existing combat:

- **Combat keeps the current Unity feel** but is resolved by Unity physics: collider hurtboxes, swept hit casts, and knockback that depends on mass. A replay recording made before the change proves that nothing that should stay the same has changed.
- **A new defence layer, designed for Unity:**
  - a dedicated Guard button
  - a timed parry
  - a posture bar on every fighter, which breaks the guard when full
  - a perfect dodge with slow motion
  - weapon clashes.
- **Kunai** throws and **Crimson Moon**, Kagetsu's rage art, on Unity's meter.
- **Six weapon families** at player quality: katana, twin blades, spear/staff, chain sickle, cleaver, fists. Plus **Raikō no Tachi**, a heavy tachi. Enemies use the same sets.
- **Every Act I opponent**, designed fresh on Kage's skeleton: 7 regular foe types, 4 sub-bosses, and **Raikon**, built from the owner's SVG, with his 3-phase JS boss design.
- **The JS stage structure:** a Prologue tutorial walk, four Act I stretches with gated enemy waves, four sub-boss fights and the Raikon fight.
- **The JS economy:**
  - Kagetsu at level 1 to start
  - shop, upgrades, helmets and armour with JS stats
  - helmets and armour drawn on Kage
  - JSON save
  - a dev mode that unlocks everything.
- **UI rebuilt to look like the JS game,** using its fonts and images: title, campaign map, Armory, pre-fight loadout, HUD, results, settings and pause.
- **Keyboard and gamepad controls** in the Editor now. A touch layout comes before Android.

## User Stories

### Project safety
1. As the owner, I want the Unity project under git and pushed to my GitHub repo, so that the approved combat feel and every later change can be recovered.
2. As the owner, I want a recorded replay of every current Kage move (against the training bag and against the AI), so that any change to combat can be compared with how it behaves today.
3. As the owner, I want each finished piece of work committed and pushed, so that I can follow progress and roll back one piece at a time.
4. As the owner, I want the migration plan rewritten as v3 with every decision from the grilling session, so that the plan and the work match.

### Combat on Unity physics
5. As a player, I want combat to feel exactly like the current Unity build after the physics change, so that the feel I approved isn't lost.
6. As a player, I want fast blade moves (cleave, shadow step, air kick) to always connect when the blade passes through an opponent, so that hits never go missing.
7. As a player, I want each attack window to hit a target at most once, so that damage is predictable.
8. As a player, I want heavy enemies (Ironclad, Raikon) to be pushed back less than light ones, so that weight is readable.
9. As a player, I want fighters to push each other apart unless a move passes through, so that bodies feel solid.
10. As a player, I want throws to keep their current animated arc and release into a physics launch, so that throws look the same and land naturally.
11. As the owner, I want a switch back to the old combat resolution until I sign off on the physics pass, so that I can compare and recover.
12. As the owner, I want to play a build and approve it before work continues after the physics pass, so that I decide whether the feel is preserved.

### Defence
13. As a player, I want a dedicated Guard button, so that guarding is precise on any input device.
14. As a player, I want to guard low by holding Guard with Down, so that I can block sweeps and low attacks.
15. As a player, I want walking backward not to guard, so that guarding is always deliberate.
16. As a player, I want to parry by pressing Guard just before a hit lands, so that good timing is rewarded.
17. As a player, I want a parried attacker to stagger and lose posture, so that a parry opens a counter-attack.
18. As a player, I want parry attempts to be locked out briefly after a mistimed press, so that mashing Guard isn't a strategy.
19. As a player, I want unblockable boss attacks to be impossible to parry, so that they have to be dodged.
20. As a player, I want every fighter, me included, to have a posture bar that fills when blocking, so that turtling has a cost.
21. As a player, I want a full posture bar to break the guard, leaving the fighter briefly helpless and taking a guaranteed critical next, so that pressure pays off.
22. As a player, I want posture to drain after a short time without blocking or being hit, so that it reflects recent pressure.
23. As a player, I want bosses to have larger posture bars, so that breaking a boss's guard is a meaningful goal.
24. As a player, I want a dash or roll started just before a hit lands to trigger a perfect dodge (world in slow motion, meter gain, afterimage), so that precise evasion is rewarded.
25. As a player, I want two blades whose active hit windows cross in the same frame to clash (no damage, both recoil, sparks, a little posture loss), so that blade fights feel physical.
26. As a player, I want fist and kick attacks never to clash, so that clashes stay a blade-on-blade event.
27. As the owner, I want every parry, posture, dodge and clash number in data, so that I can tune them without code changes.
28. As the owner, I want to play and approve the defence layer before work continues, so that I control how the new mechanics feel.

### Kage's abilities
29. As a player, I want Kage's current look, skeleton and moves unchanged, so that the character I approved stays the same.
30. As a player, I want a kunai throw on Special with Down held, with a limited count shown on the HUD, so that I have a ranged option.
31. As a player, I want the Rage bar to be the existing meter, so that there's one resource to learn.
32. As a player using Kagetsu, I want a full Rage bar to trigger Crimson Moon:
    - the world freezes
    - a cut-in band slides across with the name and kanji
    - the camera closes in
    - unblockable hits land on the JS timing
    - the finisher plays with hit-stop and slow motion

    so that the katana has its signature finisher.
33. As a player using Kagetsu, I want Crescent to stay a normal meter move, so that the meter has a smaller use too.
34. As a player using any other weapon, I want a full Rage bar to trigger Thousand Shadows, so that a full meter always does something.

### Weapons
35. As a player, I want to start with Kagetsu at level 1, so that progression starts where the JS game starts.
36. As a player, I want to buy Tsubame, Shizuka, Onikiri, Mizuchi and Yamigama at their JS prices, so that the economy matches the JS game.
37. As a player, I want each weapon family to have its own complete moveset (normal attacks, chains, air attacks, specials), so that switching weapons changes how I fight.
38. As a player, I want weapon upgrades up to level 8 that raise damage, speed and reach as in the JS game, so that coins turn into power.
39. As a player, I want Shizuka to play as a katana with its own stats, so that it's a stronger katana option.
40. As a player, I want to win Raikō no Tachi by beating Raikon and wield its heavy two-handed tachi moveset (long reach, slow, high damage and posture damage), so that the boss reward feels powerful.
41. As a player, I want my fists to remain a usable family, so that unarmed play stays possible.
42. As a player, I want enemies to use the same weapon families as I do, so that I can read their attacks from my own experience.

### Gear
43. As a player, I want to start with the JS starter helmet and armour, so that my loadout begins as in the JS game.
44. As a player, I want helmets and armour to have their JS health, defence and perks, upgradeable to their JS limits, so that gear matters.
45. As a player, I want equipped helmets and armour drawn on Kage, so that my loadout is visible.
46. As a player, I want every helmet and armour I can get by the end of Act I to have a visual (14 helmets, 11 armours), so that nothing I can equip looks missing.
47. As a player, I want the Act I sub-boss and Raikon rewards to grant their JS helmets and armour, so that bosses reward me with gear.

### Opponents
48. As a player, I want every Act I opponent to have a distinct silhouette, palette and weapon, so that I can tell foes apart at a glance.
49. As a player, I want Order Footmen to fight with the katana set at a slightly slower pace, so that they're a fair baseline enemy.
50. As a player, I want Order Runners to be fast twin-blade fighters who dash through, so that they pressure me.
51. As a player, I want Order Shieldbearers to fight with spear and shield and resist frontal hits, so that I must break their guard or get around them.
52. As a player, I want Order Chainthrowers to lash from mid range, so that I have to close distance.
53. As a player, I want Order Ironclads to be heavy cleaver users who don't flinch from light hits, so that I must use heavy attacks or posture breaks.
54. As a player, I want Hollow Shades and Hollow Flickers to appear in Act I squads as in the JS game, so that the Hollows are foreshadowed.
55. As a player, I want a slow, gentle tutorial Hollow in the Prologue, so that I can practise safely.
56. As a player, I want each enemy's AI to follow its JS archetype (regular, agile, defensive, ranged, heavy), so that enemies behave differently.
57. As a player, I want enemies to be driven through the same input interface as I am, so that they obey the same combat rules.

### Sub-bosses
58. As a player, I want to fight Kuroe (staff, defensive) with a raised-staff guard counter, so that I learn not to attack into a counter.
59. As a player, I want to fight Garan (heavy fists) with an armoured grapple, so that I learn to avoid grabs.
60. As a player, I want to fight Hisame (twin blades, agile) with a mist step teleport, so that I learn to track a fast foe.
61. As a player, I want to fight Shiden (spear) with a long charging thrust, so that I learn to dodge a lunge.
62. As a player, I want each sub-boss to have two phases with a visible change, so that the fight escalates.

### Raikon
63. As a player, I want Raikon to look like the owner's Raikon painting (straw hat with tassels, white ponytail, fur mantle, red-black armour, torn cape, glowing tachi), so that the main boss matches the design.
64. As a player, I want Raikon's cape, ponytail, tassels and fur to move softly, so that he feels alive.
65. As a player, I want Raikon to fight with Raikō no Tachi's heavy tachi moveset, so that his reward weapon is the one he used.
66. As a player, I want Thunder Swing to send lightning along the ground that I must jump or dash through, so that it reads as in the JS tip.
67. As a player, I want Storm Break to be a ground slam with a shock area, so that I'm punished for staying close.
68. As a player, I want Skyfall to put a red mark on the ground before a bolt lands, so that I can move off it in time.
69. As a player, I want Redline Counter to appear from phase 2 on, so that attacking carelessly gets punished later in the fight.
70. As a player, I want Raikon's phase 3 (below 33 % health) to add a late second Thunder Swing wave and more aggression, so that the finale is the hardest part.
71. As a player, I want Raikon to resist flinching (heavy armour) and to be immune to grabs, so that he feels like a boss.
72. As a player, I want a boss health bar with phase marks, name, title and art card, so that I can track the fight.
73. As the owner, I want to approve Raikon from a side-by-side image against my painting, so that I control his look.

### Stages
74. As a player, I want the Prologue to be a walk with tutorial prompts (move, jump, attack, guard, parry, dodge) at points along the road, each showing the current key, so that I learn every mechanic before fighting.
75. As a player, I want the Prologue to end with the tutorial Hollow and the Hollow Shade encounter, so that I practise in a real fight.
76. As a player, I want each Act I stretch to be a side-scrolling walk where a gate closes and waves of 2–4 foes arrive, so that it plays like the JS stretches.
77. As a player, I want at most two foes to attack me at once while the rest circle, so that group fights stay fair.
78. As a player, I want the camera to frame me and the engaged foes, so that I can see every threat.
79. As a player, I want each stretch to lead into its sub-boss arena, and the fourth sub-boss to lead to Raikon, so that the act follows the JS order.
80. As a player, I want a road progress strip and wave label on the HUD during stretches, so that I know how far I am.
81. As a player, I want a plain title card at the start of each stage instead of story scenes, so that I go straight to playing.

### Progression, economy and save
82. As a player, I want stages to unlock in order, so that progression follows the JS campaign.
83. As a player, I want coins and gems from each stage to match the JS reward rules (stage type, first clear, rating), so that the economy is balanced as in the JS game.
84. As a player, I want an S/A/B/C rating at the end of each fight using the JS formula, with parries counting, so that I have a goal beyond winning.
85. As a player, I want my progress, coins, owned items, upgrade levels and equipped loadout saved and restored, so that I never lose progress.
86. As the owner, I want a dev mode, on by default for now, that unlocks every stage, owns every weapon, helmet and armour and gives enough coins, so that I can test any content right away.
87. As the owner, I want turning dev mode off to give me the real JS economy from a fresh start, so that I can test normal progression.

### UI
88. As a player, I want the title screen, campaign map, Armory, pre-fight loadout, results, settings and pause to look like the JS game, using its fonts and images, so that the Unity game keeps the JS identity.
89. As a player, I want the HUD laid out like the JS HUD:
    - Kage's panel: portrait, level, health with a damage trail, Rage bar, kunai count
    - the enemy panel
    - the boss bar
    - the clock and round pips where they apply
    - the combo counter and pause button.
90. As a player, I want a posture bar on the HUD for me and for my current opponent, so that I can read the new defence mechanics.
91. As a player, I want to buy, upgrade and equip weapons, helmets and armour in the Armory, with JS prices and stat previews, so that I can manage my loadout.

### Controls
92. As a player on desktop, I want keyboard controls for move, jump, crouch, punch, kick, weapon, special, guard, dash and pause, so that I can play and test in the Editor.
93. As a player with a gamepad, I want the same actions mapped to the gamepad, so that I can play either way.
94. As the owner, I want input kept behind the shared input interface, so that a touch layout can be added for Android without touching combat.

### Tuning
95. As a player on Normal, I want regular foes to take about 15–25 s, a stretch wave about 45 s, a sub-boss about 60–90 s and Raikon about 2–3 min with the gear I can have by then, so that pacing matches the design.
96. As the owner, I want one shared health scale applied to all JS health values (enemies, Kage, gear bonuses) while Unity's damage values stay unchanged, so that I can tune pacing with a single number.

### Verification
97. As the owner, I want automated fight tests that drive fighters with scripted input and check events and health, so that combat changes are verified without manual play.
98. As the owner, I want automated rules tests for economy, rewards, unlocks, save and dev mode, so that progression logic is verified exactly as the JS tests verified it.

## Implementation Decisions

### Ground rules
- Unity owns the mechanics; the JS game is a source of content and design intent (names, roles, health, weapon families, phases, ability lists, economy and stats). The JS combat logic (fighter, combat, budo, AI) is not translated.
- One combat system: the existing state-machine `Fighter` with frame-based `MoveDef`s, `HitWindow`s, poses and IK. Every opponent is a `Fighter` fed through `IInputSource`. No second combat path exists for bosses.
- Move data stays in C# using the existing fluent move API. Characters, weapons, gear, stages and bosses become data.
- Scenes and stages stay built in code with Unity's built-in assets for now. The visual direction of stages comes later from the owner. Exception: the UI uses the JS game's fonts and images.

### Combat core
- **Fighter split:** the monolithic fighter is split into partial classes (locomotion, attack, reaction, guard, throw) behind the same public interface, before other combat changes.
- **Seeded randomness:** every combat roll (crits, AI choices) comes from one seeded random source that tests can control.
- **Combat event stream:** one stream publishes hit, block, parry, clash, guard-break, perfect dodge, KO, move start, boss phase change, wave cleared and stage cleared. It generalises the existing hit and KO callbacks. The HUD, rating, tutorial and tests all read from it.
- **Physics pass:**
  - hurt circles become trigger colliders on a hurtbox layer, parented to bones
  - each active hit point is swept from its previous to its current position with a circle cast against that layer, excluding the owner, and the existing per-window hit log still limits each window to one hit per target
  - body pushing moves from the game loop to fighter push colliders on a dedicated layer; pass-through frames switch layers
  - knockback becomes an impulse scaled by a per-character mass
  - throws keep their authored arc and release into a physics launch
  - physics materials and the layer matrix move into project settings
  - the ragdoll is unchanged
  - a legacy switch keeps the old resolution until sign-off.
- **Acceptance for the physics pass:**
  - the replay produces the same hits on the same frames with the same damage
  - knockback distances stay within a tolerance
  - a mass-3 character is pushed visibly less than Kage
  - no hit is lost or counted twice on fast moves
  - the owner signs off after playing.

### Defence layer
- **Guard input:** a new Guard input on the frame input and on every input source. Holding Guard guards, Guard with Down guards low, and walking backward no longer guards. The existing high/low and overhead rules apply unchanged when guarding.
- **Parry:** a Guard press within a data-driven window before an incoming hit (about 8 frames) parries it. The attacker staggers (about 0.5 s) and takes posture damage. A missed press locks out parrying (about 0.33 s) but still guards. Unblockable windows can't be parried.
- **Posture:**
  - each fighter has posture and a posture maximum taken from the character data
  - blocking adds the hit's damage × a data factor (about 1.5)
  - clean hits add a little, and parries add nothing
  - posture drains after about 1 s without blocking or being hit
  - a full bar triggers guard-break: a helpless state of about 0.75 s, and the next hit is a guaranteed critical
  - bosses have larger maximums.
- **Perfect dodge:** starting a dash or roll within a data-driven window (about 8 frames) before a hit would land triggers world slow motion (about 0.4 s, with Kage at full speed), meter gain and an afterimage. It applies to the player only.
- **Clash:** when two blade hit windows are active and their swept paths cross in the same frame, there is no damage; both fighters recoil, take a short hit-stop and lose a little posture, and sparks show. Fist and foot hit points never clash.
- Every defence number is in data.

### Abilities
- **Kunai:** a projectile move on Special with Down held, with a per-fight count from the player loadout. It uses the generic projectile.
- **Generic projectile and hazard:** reusable physics objects (a body plus a trigger) replace the one-off Crescent. A hazard has a telegraph phase (a ground mark) followed by a strike. Chainthrower, Raikon's abilities and the kunai use them.
- **Rage meter:** the existing 100-point meter is the Rage bar.
  - Kagetsu's full-meter move is Crimson Moon, which replaces Thousand Shadows for Kagetsu: world freeze, cut-in band, camera override, binding unblockable hits on the JS timing, a finisher with hit-stop and slow motion, and placeholder effects.
  - Crescent stays a normal meter move.
  - Every other weapon uses Thousand Shadows.
- **Effects:** every effect call goes through a named cue whose id equals the JS VFX manifest id. Until the VFX stage, cues play cheap placeholders from the existing effects class.

### Characters
- **Character data:** body scale, mass, health, defence, speed multiplier, damage multiplier, posture maximum, knockback taken, palette, part set, weapon, move set, AI profile, flags (armour, no-grab) and hurt-collider layout. Kage's current build becomes one of these entries without any visual change.
- **Opponent art:** designed fresh in code as vector parts on Kage's 20-bone skeleton. Regular foes get distinct silhouettes and weapons. Sub-bosses get a distinct palette and silhouette only. Art never moves a gameplay joint, and hurt colliders belong to the gameplay skeleton.
- **Raikon art:**
  - the owner's SVG (a 7-colour trace, one compound shape per colour) is read by a new importer
  - it is cut into rig parts by region: hat and tassels, head, ponytail, fur mantle, shoulder plates, torso, arms, hands, waist rope, skirt panels, legs, boots, cape, tachi
  - the hidden far arm and far leg are drawn to match
  - cape, ponytail, tassels and mantle are soft physics parts
  - the existing Kage export tool's format is not used, because the SVG format differs.
- **Gear visuals:** helmets and armour are part sets layered on Kage's bones without changing his base design. 25 items get visuals now: the starter pieces, every shop item, and the Act I rewards. The rest have data and icons only.

### Weapons
- **Weapon data:** id, name, family, tier, price (coins or gems), base stats (damage, speed, reach), upgrade cap, exclusivity and lock hint, all from the JS game. Combat multipliers follow the JS rule: relative to the family's reference weapon, +8 % damage, +2.5 % speed and +1.5 % reach per level, with speed and reach capped.
- **Weapon families:** each family is a full player-quality move set selected by the equipped weapon. A family changes blade length, grip pose and move set. The katana family is Kage's existing katana moves. Shizuka uses the katana set. Raikō no Tachi has its own heavy two-handed tachi set: long reach, slow start-up, high damage and posture damage, wide horizontal and overhead cuts, one lunging thrust.
- **Variation within a family:** a per-character move-rate and damage multiplier let one family set play faster or slower without copying it.

### AI
- **Profile-driven AI** wraps the existing utility AI with data: preferred range, aggression, guard and parry rate, move weights, reaction frames and combo length, one profile per JS archetype. Boss abilities are chosen like button presses that map to ability move slots.
- **Multiple targets:** a fighter's opponent becomes a target list with a "nearest in front" rule. An engagement manager allows at most 2 attackers at once while others circle. The camera frames all engaged fighters.
- **Phase controller:** health thresholds swap the ability list, AI profile, speed, damage and flags, from boss data. It is used by all four sub-bosses (two phases) and Raikon (three phases at 66 % and 33 %, with the late second wave and aggression from 33 %).

### Stages and flow
- **Stage data:** the chapter and stage list for the Prologue and Act I from the JS campaign: id, kind (stretch, sub-boss, boss), enemy, squads, rewards, environment, and story scene ids kept but not played.
- **Stretch session:** road length, start, encounters at positions with a gate and waves, tutorial prompt positions (move, jump, attack, guard, parry, dodge), progress reporting for the HUD and a wave-cleared event. Clues are not built.
- **Fight session:** one opponent; boss fights have one round and no clock, and sub-boss fights follow the JS fight format.
- **Flow controller:** title → campaign map → pre-fight loadout → stage → results → map, plus pause. The current game bootstrap shrinks to setup.

### Logic, economy and save
- **Logic module:** a pure C# port of the JS rules for this scope, with no Unity scene dependency:
  - stage unlocks, completion, rewards (coins, gems, first-clear and rating multipliers) and the rating formula
  - buy, upgrade and equip
  - item tiers and locked items
  - weapon multipliers, player health and defence from gear, XP and level
  - difficulty fixed at Normal.
- **Save:** the JS save schema as JSON in the persistent data path. A new save starts with Kagetsu, the starter helmet and the starter armour at level 1.
- **Dev mode:** a save-level flag, on by default during development. It unlocks every stage, owns every weapon, helmet and armour, and grants enough coins to test upgrades. Turning it off starts the real JS economy from a fresh save.
- **Health scale:** one shared multiplier applied to JS health for enemies, Kage's base health and gear health bonuses. Unity's per-window damage values stay unchanged. The scale is tuned so the pacing targets are met on Normal, and Kage's starting health stays near today's value.

### UI and input
- **UI:** rebuilt in Unity to match the JS screens and HUD, using the JS fonts (converted for Unity text) and images (portraits, boss art cards, panels, weapon icons). The HUD binds to the event stream and fighter state, adds posture bars, and shows a road strip and wave label in stretches and the boss bar with phase marks in boss fights.
- **Input:** keyboard and gamepad mappings chosen by the implementer, with Guard and kunai added. Touch input is a later input source behind the same interface. Android build support isn't installed yet, and all testing happens in the Editor.

### Order of work
1. Version control and replay recording
2. Fighter split, seeded randomness, event stream, physics pass (owner sign-off)
3. Guard, parry, posture, perfect dodge, clash (owner sign-off)
4. Character data and the opponent art pipeline (Footman, Shade)
5. Weapon families, Raikō no Tachi, kunai, Crimson Moon
6. Multiple targets, stretch session, tutorial → the Prologue is playable
7. Act I regular foes and AI profiles
8. The 4 sub-bosses
9. Raikon
10. Logic port, save, gear stats and visuals, dev mode
11. JS-style screens and HUD

## Testing Decisions

### What a good test is
A good test drives the game the way a player or the game loop would and checks only observable outcomes: events that happened, health, posture and meter values, fighter states, save contents, coin totals. It never reaches into private fields or depends on how a mechanic is implemented. A fight test survives the physics pass, the fighter split and AI rewrites unchanged.

### Seam 1: the fight simulation, driven through the input interface (confirmed with the owner)
- **How a test runs:** build a fight from data (characters, weapons, stage or stretch) without the game bootstrap, drive every side with scripted input, step a fixed number of physics frames under a fixed seed, then check the combat event stream and the fighters' public state.
- **What it covers:**
  - the replay recording of the current moveset, which is the regression baseline for the physics pass
  - the physics pass: same hit frames and damage as the recording, no tunnelling on fast moves, one hit per window, heavy characters pushed less, props and fighters sharing one hurtbox path
  - determinism: the same script and seed twice produce identical event logs, and positions are compared within a tolerance
  - defence: parry inside and outside the window, lockout, unblockable not parryable, posture filling and guard-break, drain, perfect-dodge window, clash only blade-on-blade
  - kunai count and projectile hits, Crimson Moon damage and timing, Thousand Shadows fallback
  - weapon families: each family's moves land and upgrade multipliers change damage
  - stretches: waves spawn at gates, at most 2 foes engaged, the stage clears
  - AI: a scripted bot beats each Act I foe, each sub-boss and Raikon on Normal without softlocks, boss phases trigger at the right health, and time-to-kill is logged against the pacing targets.
- **Prior art:** the existing scripted input source and the fighter's hit and KO callbacks. The JS game's headless fight simulation tests are the model for headless fight checks.
- **New pieces this seam needs:** a headless fight builder, the seeded random source and the combat event stream. All three are part of the design anyway.

### Seam 2: the Logic rules as plain C# (confirmed with the owner)
- **How a test runs:** EditMode tests on plain save objects with no scene and no fighters.
- **What it covers:** stage unlock order, rewards and rating, buy, upgrade and equip rules, item tiers and locks, weapon multipliers, player health and defence from gear, save round-trip and fresh-save contents, dev mode on and off.
- **Prior art:** the JS game's rules tests (core, campaign, economy, rewards, save, gear, data). Their expected values are reused as fixtures so the C# port matches the JS rules.

### Checked by the owner, not automated
- Combat feel after the physics pass and after the defence layer: the owner plays a build.
- Opponent art, Raikon against the painting, and gear visuals: side-by-side screenshots.
- UI against the JS screens: side-by-side screenshots.

## Out of Scope
- Story: scenes, dialogue, the story player, clues and Prologue story text (ids are kept in the data).
- Acts II and III, the Interlude spar, The Core, the Epilogue; Akari, Takashi, The Witness, Core echoes and Young Rem.
- Echo Trials, Boss Rush, New Game+ and difficulties other than Normal.
- The hammer family (Iwakudaki) movesets and rage arts other than Crimson Moon.
- The VFX stage: real effects for cues, Effekseer, flipbook sheets, screen-space effects. Placeholders only.
- Visuals for the 22 helmets and armour pieces from later acts or worn only by enemies (data and icons only).
- Touch controls, Android and iOS builds, mobile performance work.
- Final stage art and environment look, which the owner will direct later.
- Audio beyond the existing synthesised sounds.
- Porting any JS combat logic (JS posture, parry, damage chain, juggle rules, AI code).

## Further Notes
- The JS game folder is read-only reference. JS data is read with its own headless loader.
- The Unity project currently has no version control. Step 1 initialises git and pushes to the owner's GitHub repo on a single main branch with no LFS.
- The owner's two sign-off points (after the physics pass and after the defence layer) block the next step. Other steps proceed when their checks pass.
- Raikon's source art is the owner's SVG and PNG, kept outside the project. Copy them into the project with the Raikon work.
- Act I enemy squads, sub-bosses, rewards and Raikon's phase table were verified from the JS data and match the stories above.
