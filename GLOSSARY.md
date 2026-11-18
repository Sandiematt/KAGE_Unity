# KAGE Unity glossary

The words the tickets, code, tests and docs use. Use these terms. Don't drift to the synonyms listed under "avoid". Decisions behind the terms are in `docs/adr/`.

## Time and simulation

- **Frame**: one 60 Hz logic step of the fight loop. All combat timing (move frames, windows, stun, hitstop, cooldowns) is counted in frames. Say *rendered frame* for a drawn frame. *Avoid:* tick, step (in docs), "seconds" for move timing.
- **Fight loop**: the component that runs one frame for every fighter in a fixed order: input, fighter tick, clashes, hits, projectiles and hazards, late tick (ADR-0002).
- **Fight context**: the per-fight services: seed, random source, fight clock, event stream, fighters, props (ADR-0002).
- **Fight clock**: counts frames; owns hitstop, whole-screen slow motion and the world time scale.
- **World time scale**: the speed of every fighter, projectile and hazard except the exempt one. 0.4 during a perfect dodge, 0 during the Crimson Moon freeze.
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
- **Foe**: an enemy-side fighter. **Regular foe**: a rank-and-file unit (Footman, Runner, Shieldbearer, Chainthrower, Ironclad, Shade, Flicker, tutorial Hollow). *Avoid:* mob, minion, grunt, enemy unit (in prose).
- **Sub-boss**: one of the four Act I stretch bosses: Kuroe, Garan, Hisame, Shiden. *Avoid:* mini-boss, mid-boss.
- **Boss**: Raikon, the Act I boss. "Boss fights" in rules text means both sub-boss and boss stages (the boss format).
- **The Order**: the Order of Silence, human soldiers in slate grey. **Hollows**: violet memory echoes (Shade, Flicker, the tutorial Hollow).
- **Team**: 0 is Kage's side, 1 is the foes. Hits never land on your own team.
- **Archetype**: the JS AI role of a foe: regular, agile, defensive, ranged, heavy, boss.
- **AI profile**: the data that drives the AI for an archetype: preferred range, aggression, guard and parry rate, move weights, reaction frames, combo length.
- **Engagement / engaged foe**: in a group fight, at most two foes are engaged (allowed to attack). The rest **circle** and wait. *Avoid:* aggro.
- **Health scale**: the one shared multiplier from JS health to Unity health (about 0.431). **JS health**: a character's raw health in the JS data, before the scale. *Avoid:* HP multiplier, difficulty scaling.
- **Damage multiplier**: a character's or weapon's multiplier on Unity window damage.
- **Speed multiplier**: how fast a character moves around (walk, run, dash). **Move rate**: how fast its attacks play. They are different things.
- **Mass**: a character's physics mass. Knockback impulses are divided by it.
- **Body scale**: uniform size of a character (Raikon about 1.24).
- **Gameplay skeleton**: the 20 bones every character shares (`B` enum). Hurt circles, hit points and push colliders belong to it. **Part set**: the drawn parts placed on it for one character. **Soft parts**: parts moved by springs or cloth (Kage's scarf, hair and coat; Raikon's cape, ponytail, tassels and fur mantle).
- **Flags**: Armour, NoGrab, FrontalArmour, CanRageArt, CanThrowKunai.
- **Opponent kit**: the small shared helpers every new foe's part set is drawn with (`OpponentKit`: Kage's skeleton alone, shapes, adders, depth slots; `OrderPalette` for the Order soldiers), so a foe is character data plus a part set (ADR-0005). *Avoid:* art pipeline (as a type name), sprite sheet (nothing is a sprite).

## Moves and hits

- **Move**: one `MoveDef`: pose keys, hit windows, root motion, effect events, chains and cancels. *Avoid:* attack (as a noun for the data), animation.
- **Move set**: every move a fighter can do with one weapon family.
- **Weapon family** (or **family**): katana, twin blades (`twin`), spear/staff (`spear`), chain sickle (`chain`), cleaver, fists, and the heavy tachi (`tachi`). The equipped weapon picks the family.
- **Weapon**: an item in the JS weapon table (Kagetsu, Tsubame, ...). **Reference weapon**: the weapon a family's multipliers are measured against.
- **Polearm grip**: how the spear and staff family holds its weapon: both hands on the shaft, the rear hand a forearm behind the lead hand (`GripPose.Polearm`, `Pose.PolearmSpacing`), against the katana's fist-width grip. The **lead hand** is the one that leads on the shaft (the far hand bone); the **rear hand** is the near hand.
- **Weapon level**: 1 to 8. **Player level**: Kage's XP level. Never just "level".
- **Hit window**: the frames of a move in which its hit points can hit (`HitWindow`). *Avoid:* hitbox, active frames (in data).
- **Hit point**: a point on the skeleton that hits during a window: Fist, Hilt, Foot, Shin, Knee, Blade. **Blade** means the weapon's line for every family. **Second blade** (`BladeN`): the twin blades' near-hand blade; its windows are blade windows too.
- **Dual grip**: how the twin blades are held, one blade in each hand with the hands apart (`GripPose.Dual`). *Avoid:* two-handed (that is the katana's grip).
- **Hit sweep**: one hit point's path from its last-frame to this-frame position, cast against hurtboxes.
- **Hurtbox**: a hurt circle on a bone, as a trigger collider on the Hurtbox layer.
- **Push collider**: the body that keeps fighters apart. **Pass-through frames**: frames of a move where push colliders let fighters pass.
- **Hit level**: High, Mid, Low, Overhead, Unblockable.
- **Light hit / heavy hit**: a window without / with the `heavy` flag. Armour ignores light hits.
- **Critical**: a ×1.5 hit, rolled by the attacker, or guaranteed after a guard-break.
- **Chip damage**: the health a blade takes off through a guard.
- **Knockback, launch, juggle, knockdown, OTG (a hit on a downed fighter), kip-up, ragdoll**: as in today's code.
- **Hitstun / blockstun / stagger**: the helpless frames after a hit, after a guarded hit, and after being parried or countered. *Avoid:* stun (on its own), daze.
- **Throw**: a grab, carry and release into a physics launch. **No-grab**: throws fail on this character.
- **Armour**: the fighter takes damage and posture but doesn't flinch. From a flag (light hits) or a move's armour frames. **Frontal armour**: the same, only for hits from the front. *Avoid:* super armour, hyper armour, poise.
- **Counter window**: frames of a move that catch an incoming hit and answer it (Mirror Guard, Kuroe's Censure, Shiden's Hold the Line, Raikon's Redline Counter). Not a parry.

## Defence

- **Guard**: the Guard button, and the state of holding it: blocks hits by the level rules. **Low guard**: Guard with Down. A guarded hit is **blocked**. *Avoid:* block (as the button or state), shield, defend, hold-back guard (gone).
- **Parry**: a Guard press within a short window before a hit lands. The hit does nothing; the attacker staggers and takes posture. A mistimed press starts a **parry lockout**. Unblockable hits can't be parried. *Avoid:* deflect, perfect block, just guard.
- **Posture**: a bar on every fighter that fills when it guards (hit damage × a factor) and a little on clean hits, and drains after about 1 s without guarding or being hit. *Avoid:* stamina, guard meter, stance, poise, balance.
- **Guard-break**: posture full: about 0.75 s helpless, and the next hit is a guaranteed critical. *Avoid:* guard crush, stagger (for this).
- **Perfect dodge**: a dash or roll started just before a hit would land: the world slows (Kage doesn't), Kage gains Rage, an afterimage shows. Player only. *Avoid:* witch time, just dodge.
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
- **Tutorial prompt**: a Prologue prompt at a road position, showing the bound key, that waits until the action is done.
- **Dev arena**: `KageGame`'s duel and training dojo, for trying fighters and weapons in the Editor. **Campaign flow**: title → campaign map → pre-fight loadout → stage → results → map.

## Economy and save

- **Rules** (`Kage.Rules`): the pure C# port of the JS game's `Logic` module for this scope. *Avoid:* "Logic" for the Unity code, game logic (vague).
- **JS tables**: the generated C# copy of the JS content tables (ADR-0010).
- **Coins, gems, XP**: JS currencies and experience.
- **Rating**: S, A, B or C at the end of a fight, from the JS formula (health left, time, max combo, parries, flawless).
- **Rewards**: coins, gems and reward items for a stage: base coins by chapter and stage kind, first-clear gems, half coins on a repeat clear.
- **Gear**: helmets and armour. **Loadout**: the equipped weapon, helmet and armour with their levels. **Starter gear**: Kagetsu, Shiro Maki (shown as Crimson Scarf), Tachibana Yoroi (shown as Shinobi Garb). **Helmet visual**: the drawing of an equipped helmet, layered on Kage's head by his part set (`HelmetVisuals`); a helmet with no drawing leaves his head as it is. Use the JS ids in code and data, the JS display names in UI.
- **Tier**: an item's rarity (starter, common, rare, epic, trophy, boss). Shop weapons climb tiers with weapon level.
- **Exclusive / locked item**: never sold; unlocked by a stage reward. **Lock hint**: the text that says how.
- **Armory**: the screen to buy, upgrade and equip. *Avoid:* shop (for the screen), store, inventory.
- **Perks**: the small bonuses gear gives (dash, speed, ammo, stun resistance, rage gain, kunai).
- **Save**: the JS save schema as JSON (ADR-0009). **Fresh save**: a new save with starter gear at level 1.
- **Dev mode**: a save flag, on by default for now: every stage unlocked, every item owned, plenty of coins. Turning it off replaces the save with a fresh one. *Avoid:* debug mode, cheat mode, unlock-all.

## Review

- **Owner sign-off**: the two points (after the physics pass, after the defence layer) where the owner plays a build and approves it before work continues.
- **Screenshot sheet**: the review images under `Screenshots/review/<ticket>/`.
