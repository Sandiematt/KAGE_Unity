# KAGE Unity glossary

The words the tickets, code, tests and docs use. Use these terms. Don't drift to the synonyms listed under "avoid". Decisions behind the terms are in `docs/adr/`.

## Time and simulation

- **Frame**: one 60 Hz logic step of the fight loop. All combat timing (move frames, windows, stun, hitstop, cooldowns) is counted in frames. Say *rendered frame* for a drawn frame. *Avoid:* tick, step (in docs), "seconds" for move timing.
- **Fight loop**: the component that runs one frame for every fighter in a fixed order: input, fighter tick, clashes, hits, projectiles and hazards, late tick (ADR-0002).
- **Fight context**: the per-fight services: seed, random source, fight clock, event stream, fighters, props (ADR-0002).
- **Fight clock**: counts frames; owns hitstop, whole-screen slow motion and the world time scale.
- **World time scale**: the speed of every fighter, projectile and hazard except the **exempt** fighter (Kage) and what he owns. 0.4 during a perfect dodge, 0 during the Crimson Moon freeze. Counted by the fight clock in loop frames: a slowed fighter takes a logic frame only on the slowed world's frames (0.4 of them), its body moves at 0.4 of its speed in between, and a frozen one is held still. Not `Time.timeScale` (that is hitstop's and whole-screen slow motion's). *Avoid:* witch time, bullet time, local time scale.
- **Hitstop**: a short freeze of the whole fight on impact. *Avoid:* hit-pause, freeze frame.
- **Seed / random stream**: every gameplay roll comes from a named stream of the fight's seeded random source. Cosmetic randomness doesn't.
- **Fight harness**: the test seam that builds a fight from data, drives it with scripted input under a fixed seed and checks events and public state. *Avoid:* simulator.
- **Baseline replay**: the recording of every current Kage move (against the training bag and the AI) that later combat changes are compared with.
- **Legacy switch**: the setting that restores the old hit and push resolution until the owner signs off the physics pass.

## Fighters and characters

- **Fighter**: the one combat object (`Fighter`) every combatant is, player or foe, driven through an input source. There is no second combat path.
- **Character**: an entry in character data (`CharacterDef`): stats, look, family, AI profile, flags. Kage is a character.
- **Character data**: the shape in ADR-0005.
- **Opponent**: the fighter on the other side. `Fighter.opponent` is the **current target**.
- **Target list**: every active fighter of another team that a fighter can fight (`Fighter.Targets`, fighter order). With one foe it has one entry. **Current target**: the one picked from it every frame by the **nearest-in-front rule**: the nearest by horizontal distance, a fighter behind counting `behindPenalty` (1.5 m) farther and the current target `stickiness` (0.25 m) nearer, knocked-out fighters only when nobody else stands (`Fighter.PickTarget`, SAN-83). Move selection, the AI and facing read it, so Kage turns only for a foe behind that is clearly nearer, or when nobody is in front. *Avoid:* lock-on, focus (except the camera's focus fighter).
- **Foe**: an enemy-side fighter. **Regular foe**: a rank-and-file unit (Footman, Runner, Shieldbearer, Chainthrower, Ironclad, Shade, Flicker, tutorial Hollow). *Avoid:* mob, minion, grunt, enemy unit (in prose).
- **Sub-boss**: one of the four Act I stretch bosses: Kuroe, Garan, Hisame, Shiden. *Avoid:* mini-boss, mid-boss.
- **Boss**: Raikon, the Act I boss. "Boss fights" in rules text means both sub-boss and boss stages (the boss format).
- **The Order**: the Order of Silence, human soldiers in slate grey. **Hollows**: violet memory echoes (Shade, Flicker, the tutorial Hollow).
- **Team**: 0 is Kage's side, 1 is the foes. Hits never land on your own team.
- **Archetype**: the JS AI role of a foe: regular, agile, defensive, ranged, heavy, boss.
- **AI profile**: the data that drives the AI for an archetype: preferred range, aggression, guard and parry rate, move weights, reaction frames, combo length.
- **Attack rate**: a character's share (0..1) of the attacks its AI has chosen that it actually throws (`CharacterDef.attackRate`). The tutorial Hollow's is 0.3. *Avoid:* aggression (that is the JS profile's `aggr`, a different number).
- **Engagement / engaged foe**: in a group fight, at most two foes are engaged (allowed to attack; `maxAttackers`). The rest are **waiting foes**: they keep the **wait distance** (3.1 m, the JS ring) from Kage and **circle** (step in and out) until a place frees. A knocked-out engaged foe's place goes to the nearest waiting foe. The `EngagementManager` decides (`ctx.Engagement`); numbers in `engagement.json` (SAN-83). *Avoid:* aggro, attack token.
- **Attacking** (for the engagement limit): in an attack move (`Fighter.InAttackMove`): a move with a hit window, a throw, a projectile or a counter window; not a roll, backflip, dash or getting up. **Committed**: attacking, or with an attack button still buffered; an engaged foe keeps its place while committed.
- **Group fight**: the dev arena's Kage against four Footmen (F6), and any fight with more than one foe. *Avoid:* mob fight, horde.
- **Health scale**: the one shared multiplier from JS health to Unity health (about 0.431). **JS health**: a character's raw health in the JS data, before the scale. *Avoid:* HP multiplier, difficulty scaling.
- **Damage multiplier**: a character's or weapon's multiplier on Unity window damage.
- **Speed multiplier**: how fast a character moves around (walk, run, dash). **Move rate**: how fast its attacks play. They are different things.
- **Mass**: a character's physics mass. Knockback impulses are divided by it.
- **Body scale**: uniform size of a character (Raikon about 1.24).
- **Gameplay skeleton**: the 20 bones every character shares (`B` enum). Hurt circles, hit points and push colliders belong to it. **Part set**: the drawn parts placed on it for one character. **Soft parts**: parts moved by springs or cloth (Kage's scarf, hair and coat; Raikon's cape, ponytail, tassels and fur mantle, which hang on **spring chains**, `SoftChains`).
- **Colour trace**: an SVG with one compound shape per colour, like the owner's Raikon SVG. **Region**: the area of the trace that one part is cut from (`RaikonRegions`, written by `Tools/raikon_art/regions.py`). **Fill**: a shape in the trace's colours drawn behind a moving part where the painting shows nothing (the torso behind the arms). **Bake**: the cut stored in Resources so a spawn doesn't cut the SVG (`Kage > Bake Raikon Art`). *Avoid:* sprite sheet, slicing.
- **Flags**: Armour, NoGrab, FrontalArmour, CanRageArt, CanThrowKunai.
- **Opponent kit**: the small shared helpers every new foe's part set is drawn with (`OpponentKit`: Kage's skeleton alone, shapes, adders, depth slots; `OrderPalette` for the Order soldiers), so a foe is character data plus a part set (ADR-0005). *Avoid:* art pipeline (as a type name), sprite sheet (nothing is a sprite).

## Moves and hits

- **Move**: one `MoveDef`: pose keys, hit windows, root motion, effect events, chains and cancels. *Avoid:* attack (as a noun for the data), animation.
- **Move set**: every move a fighter can do with one weapon family.
- **Weapon family** (or **family**): katana, twin blades (`twin`), spear/staff (`spear`), chain sickle (`chain`), cleaver, fists, and the heavy tachi (`tachi`). The equipped weapon picks the family.
- **Weapon**: an item in the JS weapon table (Kagetsu, Tsubame, ...). **Reference weapon**: the weapon a family's multipliers are measured against.
- **Polearm grip**: how the spear and staff family holds its weapon: both hands on the shaft, the rear hand a forearm behind the lead hand (`GripPose.Polearm`, `Pose.PolearmSpacing`), against the katana's fist-width grip. The **lead hand** is the one that leads on the shaft (the far hand bone); the **rear hand** is the near hand.
- **Heavy grip**: how the cleaver family holds its weapon: both hands on a long handle, the rear hand a hand's width behind the lead hand (`GripPose.HeavyTwoHand`, `Pose.HeavySpacing`). Wider than the katana's fist-width grip, shorter than the polearm grip.
- **Weapon level**: 1 to 8. **Player level**: Kage's XP level. Never just "level".
- **Hit window**: the frames of a move in which its hit points can hit (`HitWindow`). *Avoid:* hitbox, active frames (in data).
- **Hit point**: a point on the skeleton that hits during a window: Fist, Hilt, Foot, Shin, Knee, Blade. **Blade** means the weapon's line for every family. **Second blade** (`BladeN`): the twin blades' near-hand blade; its windows are blade windows too.
- **Blade extension**: metres by which a move runs the **Blade** hit line past the blade (`MoveDef.Extend`, `Fighter.BladeExtension`). The chain sickle's **lash** and **pull** use it: the hit line is the chain, and the drawn chain and sickle follow it. The **pull** (Reaper's Pull) is a hit whose knockback points at the attacker, so the foe is dragged in by the ordinary knockback and push. *Avoid:* hook (for the move; the Chainthrower's weapon is the Chain Hook).
- **Dual grip**: how the twin blades are held, one blade in each hand with the hands apart (`GripPose.Dual`). *Avoid:* two-handed (that is the katana's grip).
- **Hit sweep**: one hit point's path from its last-frame to this-frame position, cast against hurtboxes.
- **Hurtbox**: a hurt circle on a bone, as a trigger collider on the Hurtbox layer.
- **Push collider**: the body that keeps fighters apart. **Pass-through frames**: frames of a move where push colliders let fighters pass.
- **Hit level**: High, Mid, Low, Overhead, Unblockable.
- **Light hit / heavy hit**: a window without / with the `heavy` flag. Armour ignores light hits.
- **Critical**: a ×1.5 hit, rolled by the attacker, or guaranteed after a guard-break.
- **Chip damage**: the health a blade takes off through a guard (damage × 0.15, rounded up).
- **Knockback, launch, juggle, knockdown, OTG (a hit on a downed fighter), kip-up, ragdoll**: as in today's code.
- **Hitstun / blockstun / stagger**: the helpless frames after a hit, after a guarded hit, and after being parried or countered. *Avoid:* stun (on its own), daze.
- **Throw**: a grab, carry and release into a physics launch. The light one is the shoulder throw (Punch + forward); the **heavy grapple** is the fists family's Iron Grip (Weapon + forward): longer reach, overhead carry, armoured until the release. **No-grab**: throws fail on this character.
- **Armour**: the fighter takes damage and posture but doesn't flinch. From a flag (light hits) or a move's armour frames. **Frontal armour**: the same, only for hits from the front. *Avoid:* super armour, hyper armour, poise.
- **Blade length** (`WeaponFamilyDef.bladeLength`): metres from the guard to the tip of the cutting blade; the tachi's is the longest. *Not* **total reach**, the distance from the body to the farthest hit point of a move (the spear's is the longest, 2.25 m).
- **Posture multiplier** (`HitWindow.postureMul`): how much more posture damage a window does than a plain hit; the tachi's windows are the highest (up to ×3.0).
- **Armour frames**: the frames of a move (`MoveDef.armorFrom..armorTo`) in which the fighter has armour: light hits land their damage and don't flinch it, and a held victim stays held. Heavy hits still flinch. The check is `Fighter.Armour.cs`.
- **Counter window**: frames of a move that catch an incoming hit and answer it (Mirror Guard, Kuroe's Censure, Shiden's Hold the Line, Raikon's Redline Counter). Not a parry.

## Defence

- **Guard**: the Guard button (keyboard O or B, gamepad RB or RT; `KeyBindings`), and the state of holding it on the ground (the **guard stance**): blocks hits by the level rules. **Low guard**: Guard with Down. A guarded hit is **blocked**. Walking backward doesn't guard. *Avoid:* block (as the button or state), shield, defend, hold-back guard (gone).
- **Parry**: a Guard press within a short window before a hit lands. The hit does nothing; the attacker staggers and takes posture. A mistimed press starts a **parry lockout**. Unblockable hits can't be parried. *Avoid:* deflect, perfect block, just guard.
- **Posture**: a bar on every fighter (`postureMax` in character data: 60, Raikon 100) that fills when it guards (hit damage × 1.5 × the window's postureMul) and a little on clean hits (× 0.3), and drains after 1 s (60 frames) without a posture gain, not while Guard is held. Numbers in `guard.json`. *Avoid:* stamina, guard meter, stance, poise, balance.
- **Guard-break**: posture full: 45 frames (0.75 s) helpless, and the next hit is a guaranteed critical; the bar is left at 40 %. A **guard-broken** fighter is in those frames (`Fighter.GuardBroken`). *Avoid:* guard crush, stagger (for this).
- **Perfect dodge**: a dash or roll started just before a hit would land: the hit reaches Kage's hurtboxes within the **perfect-dodge window** (8 of his frames from the start of the dash, roll or backflip, plus the dash perk's bonus). The hit does nothing (`PerfectDodged`, no damage), the world slows to 0.4 for 24 frames (Kage doesn't), Kage gains 12 Rage, blue afterimages show. Once per dash or roll; player only. Numbers in `dodge.json`. A dash or roll outside the window is a **normal dodge**: only its own invulnerability. *Avoid:* witch time, just dodge.
- **Clash**: two blade windows whose sweeps cross on the same frame: no damage, both recoil, a little posture each, sparks. Blades only, never fists or feet.

## Meter and specials

- **Rage**: the existing 100-point meter, shown as the Rage bar (怒). *Avoid:* shadow meter, super meter, meter (in UI text).
- **Rage art**: the full-Rage move: **Crimson Moon** (紅月) for the katana family, **Thousand Shadows** for every other family. Triggered by Special + Down with full Rage. *Avoid:* ultimate (old code name `FState.Ultimate`), super, special (for this).
- **Special**: the Special button. A plain Special is the family's Special move (Crescent for the katana). *Avoid:* using "special" for rage arts or for abilities.
- **Crescent**: the katana's 35-Rage projectile move.
- **Kunai**: the thrown knife on Special + Down when Rage isn't full, with a per-fight count.
- **Projectile**: a thrown or launched object with a hit window (Crescent, kunai, the Chainthrower's lash if built as one, Thunder Swing's ground wave).
- **Hazard**: an area that shows a **telegraph** (ground mark) and then strikes once (Skyfall, the shock areas of Storm Break, Storm Fist and Vanguard Break).
- **Ability**: a named special move a foe or boss chooses through an **ability slot** in its input (Thunder Swing, Censure, Tsuppari). Abilities are moves. *Avoid:* skill, power.
- **Effect cue**: a named effect request whose id is the JS VFX manifest id (or `VFX_Kage_<Name>`). It plays a placeholder until the VFX stage.

## Stages and campaign

- **Chapter**: `prologue` or `act1` (JS chapter ids). *Avoid:* level, world.
- **Stage**: one entry on the campaign map, with a kind: **stretch**, **sub-boss** or **boss**. *Avoid:* level, mission.
- **Stretch**: a side-scrolling walk along a **road** with encounters. The JS term. *Avoid:* corridor, run.
- **Encounter**: a point on the road where a **gate** closes and waves arrive. It is cleared when its last wave is cleared.
- **Wave**: one group of 2–4 foes within an encounter. The next wave comes when the previous one is cleared. *Avoid:* squad (except when quoting the spec's "JS squads", meaning all the foes of a stretch), spawn group.
- **Phase**: a part of a boss fight that starts when health falls to its threshold, and swaps abilities, profile, multipliers and flags. Sub-bosses have two, Raikon three.
- **Boss bar**: the HUD bar for a boss: art card, name, title, health, phase marks.
- **Fight format**: duel (best of three, clock), boss (one round, no clock), stretch (no rounds, no clock).
- **Title card**: the plain card that opens a stage in place of story scenes.
- **Tutorial prompt**: a Prologue prompt at a road position, showing the bound key (`KeyBindings.Label`), that waits until the action is done.
- **Key bindings**: the one table of fight controls, action to keyboard keys and gamepad buttons (`KeyBindings`, ADR-0004). *Avoid:* key map, controls config.
- **Dev arena**: `KageGame`'s duel, group fight (F6) and training dojo, for trying fighters and weapons in the Editor. **Campaign flow**: title → campaign map → pre-fight loadout → stage → results → map.

## Economy and save

- **Rules** (`Kage.Rules`): the pure C# port of the JS game's `Logic` module for this scope. *Avoid:* "Logic" for the Unity code, game logic (vague).
- **JS tables**: the generated C# copy of the JS content tables (ADR-0010).
- **Coins, gems, XP**: JS currencies and experience.
- **Rating**: S, A, B or C at the end of a fight, from the JS formula (health left, time, max combo, parries, flawless).
- **Rewards**: coins, gems and reward items for a stage: base coins by chapter and stage kind, first-clear gems, half coins on a repeat clear.
- **Gear**: helmets and armour. **Loadout**: the equipped weapon, helmet and armour with their levels, read from the save by `KageCharacter.Def(save)` whenever a fight starts (campaign and dev arena). **Starter gear**: Kagetsu, Shiro Maki (shown as Crimson Scarf), Tachibana Yoroi (shown as Shinobi Garb). **Helmet visual**: the drawing of an equipped helmet, layered on Kage's head by his part set (`HelmetVisuals`); a helmet with no drawing leaves his head as it is. Use the JS ids in code and data, the JS display names in UI.
- **Tier**: an item's rarity (starter, common, rare, epic, trophy, boss). Shop weapons climb tiers with weapon level.
- **Exclusive / locked item**: never sold; unlocked by a stage reward. **Lock hint**: the text that says how.
- **Armory**: the screen to buy, upgrade and equip. *Avoid:* shop (for the screen), store, inventory.
- **Armory tabs and rows**: Weapons, Helmets and Armor; the Armor tab shows one **row** at a time, the **armour line** (Shinobi Garb to Mugen) or the **trophies**. **Tiles** (weapons, helmets) and **cards** (armour) are the items in the row at the bottom of the screen. **Preview Kage**: the live Kage the Armory builds from the equipped loadout with the selection swapped in (`ArmoryView.PreviewSave`). **Stat preview**: the gold part of a stat bar and its +N, the change the next level brings.
- **Perks**: the small bonuses gear gives, eight in the JS data: dash, speed, ammo, stun resistance, rage gain, kunai damage, damage, block rage. Summed on Kage's character data as `GearPerks`; applied by the fighter (`Fighter.Gear.cs`). **Hooked perk**: one whose mechanic isn't built yet (block rage); it has a named hook and waits for the owning ticket.
- **Save**: the JS save schema as JSON (ADR-0009). **Fresh save**: a new save with starter gear at level 1.
- **Dev mode**: a save flag, on by default for now: every stage unlocked, every item owned, plenty of coins. Turning it off replaces the save with a fresh one. *Avoid:* debug mode, cheat mode, unlock-all.

## Review

- **Owner sign-off**: the two points (after the physics pass, after the defence layer) where the owner plays a build and approves it before work continues.
- **Screenshot sheet**: the review images under `Screenshots/review/<ticket>/`.
