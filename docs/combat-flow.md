# Combat flow: crisp facing, the two-way parry, cancels and playstyles

Code: `Combat/Fighter.Facing.cs`, `Combat/Fighter.Flow.cs`, `Combat/Fighter.Parry.cs`, `Combat/AIInputSource.Profile.cs`,
`Character/FighterRig.cs` (the flip). Numbers: `Resources/Tuning/flow.json` (`CombatFlowTuning`), `parry.json`
(`ParryTuning`), `ai.json` (`AiTuning`). Tests: `Tests/EditMode/CombatFlowTests.cs`.

## Why

The owner's brief (2026-11-26): make the 2D combat tighter, cleaner and more responsive without losing its mechanics. Remove the
rotation that made fighters turn backwards awkwardly or flip 360°, and face left or right crisply; lock facing while a blow is
being thrown. Let Kage parry every tier of foe, and let skilled foes parry him back, with cues he can read and a fair chance to
answer. Polish timing and cancels so defensive, aggressive and hit-and-run play all work and none wins everything.

## 1. Facing

| Before | Now |
| --- | --- |
| A turn squeezed the rig's scale through zero over about 7 frames (a "paper flip"), which read as the body spinning round. | `SetFacing` flips scale x to +1 or -1 on the same frame. |
| Neutral turned to the target whenever it was 0.06 m past the centre, so a foe crossing close could flip a fighter back and forth. | A 0.12 m dead zone, and no turning straight back within 8 frames unless the target is 0.6 m or more behind (`turnDeadZone`, `turnHoldFrames`, `turnForceDistance`). |
| Nothing turned a fighter for a chain or a button pressed with the target behind. | A new ground attack (a button from neutral, a landing, a dash or a skid, a chain link, an ability) faces its target first: the flip falls between two animations (`turnOnMoveStart`). A held direction then reads on the new side. |
| A fighter hit from behind reeled backwards into the blade. A 1v1 block from behind faced away. | A reel or a block from a melee blow from behind turns to the attacker as it starts (`turnToBlowFromBehind`). A shield bearer (frontal armour) doesn't turn on a reel: its back stays its weak side. |
| A launched body kept spinning at up to 460°/s: a full backward somersault on a big launch. | It turns towards lying (90°, onto its back or its front) at up to 300°/s and stops there (`tumbleRestAngle`, `tumbleMaxSpeed`). A juggle keeps the side it already turned to. The ragdoll's first spin is capped so it doesn't overshoot. |
| The backflip was a 360° somersault (every family shares it, so every foe that dodged flipped). | The **back step** (field still `backflip`): a flat slide straight back on the floor, never airborne, X-only root motion (a burst, a glide, a brake), the hips at one low height, the guard up. Same 34 frames, about the same travel, invulnerable 2 to 16. |
| The roll was a 360° tuck roll, and some attacks spun the whole body (the katana's Crescent Spin, the twin Whirl, the spear's Wave Crest, Hisame's fan dance, Raikon's Thunder Step, Tempest Wheel and Ascendant Rush). | No move spins: `MoveDef.Spin` is gone. The roll is the **Shadow Slide**, a low forward slide through the foe (same frames, invulnerability and pass-through). The spinning attacks are grounded or upright cuts: two clean air cuts (Crescent Cut, Whirl, Wave Crest), Hisame's stepping twin-fan cross-sweep, Raikon's chambered push kick, a forward-and-backhand blade sweep (Tempest Wheel) and an air cut (Ascendant Rush). Hit frames and numbers are unchanged. |
| Raikon's blinks (Thunder Step, Afterimage Feint, Blink Flurry 1 and 2) hopped 5 to 12 cm. | Flat along the floor (vault height 0). His leaping slam and the other jumping attacks keep their arcs: they are attacks, not dodges. |
| A thrown body turned 200° over the carry, upside down. | It tips 90° onto its back over the carry, where the tumble rests. |

**The lock** (`Fighter.FacingLocked`): a move's wind-up and blows (a move with no blows, like a roll, throughout), a dash, the
air, a reel, a block, a fall, being thrown and the rage art never turn on their own. Only a move's own turn-around frames (a
vault's landing, a counter's teleport, an art's beats) and the defence's turn to a blow set facing then.

## 2. The two-way parry

The parry itself is unchanged (SAN-91): Guard pressed at most 8 frames before a melee blow, a 20-frame lockout, no damage, the
attacker reels and loses posture, Kage's next clean blow is a counter hit.

**Tiers** (`Fighter.Tier`, `CombatTier`): Player, Regular, **Elite**, Sub, Boss. Sub-boss and boss come from the JS enemy type.
Elite is new: a regular foe the character data puts in armour or behind a shield (the Ironclad, the Husk, the Shieldbearer).
What a parry does to each:

| Tier | Reels (frames) | Posture lost | Its answer waits (frames) | AI skill (read and stance) |
| --- | --- | --- | --- | --- |
| Regular | 57 | 55 % | 16 | 0.25 |
| Elite | 46 | 42 % | 14 | 0.6 |
| Sub-boss | 37 | 34 % | 13 | 0.9 |
| Boss | 30 | 26 % | 12 | 1.1 |
| Kage (parried by a foe) | 28, leavable from 12 | 30 % | | |

Kage parries every tier's blows. Only the hits the boss kit marks unblockable (the crimson-white aura) stay dodge-only, as
designed; the red-eye unguardable hits can still be parried.

**Kage's parry, the metallic clash** (`VFX_Kage_Parry`): crossed steel streaks where the blades met, a white star and a ring, gold
sparks flying back at the attacker with a white spray off his own blade, a gold flash, an impact punch (the camera jolts and
the colours split), 12 frames of hit-stop, "PARRY", a ringing clang (`SfxKind.Clang`) over the chime.

**A foe's parry** (`VFX_Foe_Parry`): crimson steel streaks, a white-and-crimson star and ring, sparks thrown at Kage, a dark red
flash, a lower, harsher clang (`SfxKind.FoeParry`), a red "PARRIED" over Kage and both fighters flash red. Then:

- **Kage's stagger can be left early.** From frame 12 of his 28-frame stagger (`playerRecoverFrames`), Guard held ends it into the
  guard, a dash press dashes, and a double tap hops back or rolls. A Guard pressed during the stagger has already opened its
  parry window, so the foe's answer can be parried. Attacks still wait for the whole stagger: mashing doesn't get out.
- **The foe's answer shows a tell.** A profiled foe that parried holds still for its tier's delay (never shorter than Kage's
  12), then attacks (`answerChance` 0.85). Its first attack within 45 frames of the parry plays a glint at its weapon hand, a
  "!" over it and a steel ring as it winds up.

**When foes parry** (profiled AI only; the Ronin's legacy AI is unchanged):

- The base chance is the profile's JS parry rate on the stage's curve, as before.
- **The read**: a foe remembers the opponent's last 6 attack starts for 4 s. Each earlier start of the attack now coming adds
  0.12 (at most 0.36), times its tier's skill. Mashing one button, or repeating one string, gets read.
- **The stance**: a blow into a guard a foe is holding for its own sake (its defensive stance) adds 0.3, times its tier's skill.
  The guard stays up and drops for one frame just before the blow: that re-press opens the parry window.
- The read and the stance never lift the chance above 0.55 (`parryCap`). A grunt barely reads; a boss reads well.

## 2b. The contact-matched parry: blade meets blade

Code: `Combat/ParryContactSolver.cs`, `Combat/DynamicParryController.cs` (wired in `Fighter.Parry.cs`, `Fighter.HitDetection.cs`,
`Fighter.Pose.cs`, `FightLoop.StepHeadless`). Numbers: the `contact*`, `attackerDeflectFrames`, `anticipationWeight` and `guardPlaneX`
entries of `parry.json`. Tests: `Tests/EditMode/DynamicParryTests.cs`.

A parry no longer shows the fixed guard pose. The same code runs both ways, for Kage parrying any foe and any foe parrying Kage:

- **The blow's striking part.** The attacker's hit detection keeps the segment that struck (the blade from guard to tip; a fist's
  forearm; a shin) and its travel on the frame of the hit (`HitInfo.strikeBase`, `strikeTip`, `swingDir`). A synthetic hit with
  none gets one made up from its contact and level.
- **The solve.** In the defender's root space (its facing and body scale out of the way, so Raikon at 1.24 solves the same mirrored
  problem), the guard is braced for the blow's height: high, upright; mid, a small lean in; low, the stance sinks with the feet
  planted. The hips never turn. A search over the defender's blade angle, the point on the incoming weapon and the point on its
  own blade keeps only poses with the far wrist inside the arm's reach (never locked straight, never folded), both hands on the
  grip, the point off the floor and clear of the body, and the blades crossed at 35° or more. The best meets the blow on a plane
  0.38 m in front at the blow's height, square, mid-blade on its forte, in a shape that reads: a roof guard high, the upright
  uke-nagashi mid, gedan-barai low. Forward kinematics (`PoseFK`) then refine it until the blades touch. Twin blades cross the near
  blade under the far one; bare hands take the blow on the forearm; a blow out of reach is met at the nearest point the arm reaches.
- **The defender** snaps onto the clash point (0.45 of the way on the impact frame, the rest over 2.5 frames of real time, so it plays
  through the hit-stop), holds there through the hit-stop with its blade drawn in front, holds 3 live frames more, then settles back
  into its guard over 8. Any state change (the Iai Counter, an attack, a dash, letting go of Guard) ends it at once and blends
  from the pose on screen, so an answer has no lag.
- **The attacker** holds its exact impact pose through the hit-stop, so the two blades stay locked (if the parry's one frame of
  pushback moves it, the defender re-solves against it). Then its weapon recoils 35° to 48° along the clash normal (off the
  defender's blade, against the swing, away from the defender, off the floor) and its reel takes over within 6 frames. Stagger,
  pushback and posture are unchanged.
- **The cue, the Parried event's contact** and every call-out anchored on it sit on the blades' crossing, not on the hurt circle;
  the sparks and crossed streaks turn to the clash normal.
- **Anticipation.** While a parry window is open and a foe within 2.4 m is swinging, the arms and blade lean 0.32 of the way towards
  the height of its next window. The hips and chest don't move, so hurt circles and hit frames are as they were.

What it moves: nothing in the frames. While the defender holds and settles (about 11 live frames) the brace moves its hips and chest
a little (a low parry from a standing guard sinks up to 16 cm), and its hurt circles go with them.

## 2c. The reference kata

Two flowing patterns from the reference clips, for the katana and (heavier) the tachi. Both keep the two-handed grip on every half
frame, the hips within 12° and the feet on the ground.

| Kata | Katana | Tachi | Blows | Inputs |
| --- | --- | --- | --- | --- |
| Crown Wheel (Tempest Coil) | `crownWheel`, 44 frames | `tachi.crownWheel`, 56 | a low reverse sweep (Low, 10-14), then the crown whip into a stepping level cut (Mid, heavy, 26-30) | S P; Down+S S; P P S (tachi: S P; Down+S S) |
| Poised Fang (Thunder Poise) | `tameCut`, 44 frames | `tachi.tameCut`, 58 | a held tame behind the crown with a glint, then the kiri-oroshi stopped at chest height (Overhead, heavy, 27-31, posture × 1.45) | Back+P; S S P; K K S; Crown Wheel S (tachi: Back+P; S S P; Crown Wheel S) |

Low then mid in one flowing sword, and an overhead held long enough to bait an early parry: the two rhythms the contact-matched parry
is built to meet. Bare-handed (a broken sword) Back+P stays the jab; the fists keep their own Back+P and kick string. They are
`MoveDef.profiledOnly`: a legacy AI (the Ronin) never picks them, by Back+P or a chain, so its fights replay as before; a profiled
katana or tachi foe opens with Back+P now and then at mid range and chains into both on hits.

## 3. Cancels, punishes and pressure

- **Recovery cancels.** Once a ground move's last action is over (its blows, a throw, a hazard, a pull, its sheathing) and one of
  its blows landed or was guarded, a dash press cancels the rest of the recovery; if one landed, Guard does too. A whiffed move
  recovers in full. Throws and moves with a next link (`then`) never cancel. The twin blades' own earlier dash-cancel stays theirs.
- **The whiff punish.** A clean melee blow on a fighter recovering from a move that touched nothing: damage × 1.25, 6 more frames
  of reel, 2 more of hit-stop, "PUNISH". Kage's only (`foesPunishWhiffs` off).
- **Guard pressure.** Guarded blows from one attacker less than 40 frames apart are a string: each after the first adds 15 % more
  posture, up to × 1.6. It works both ways: Kage's strings break guards sooner, and so do a boss's on Kage's guard.

## 4. Falls, landings, get-ups and footwork (2026-11-27)

The owner's brief: falls must not fold the body into a stiff, right-angled "table", a fall should settle onto the floor with weight,
the get-ups should be a person pushing off the floor with their hands instead of a kip-up, and attacks should step rather than skate.

| Before | Now |
| --- | --- |
| The ragdoll's hinges allowed ± 35–120° around whatever pose the fall started from, so a knee could fold to 27° or bend backwards. | `Ragdoll2D.Range`: each joint keeps a human range measured from the bone lines: knees only bend back, elbows only forward, the spine curls 40° and arches 22°, the hip flexes 125° and extends 28°. The drawn bind pose and the starting pose are always inside. KO uses the same limits. |
| One tumble shape (`PoseLib.Tumble`, legs pulled 60° forward, head thrown back) for both sides: a face-down fall jack-knifed with the hips up. | `Tumble` (onto the back: chin tucked, back gently rounded, knees soft, arms forward to slap) and `TumbleFront` (onto the front: back slightly arched, head up, hips open, shins trailing, arms reaching down to brace). Muscles 45/6 (were 30/4). |
| A low knockdown (a sweep) landed sitting up on straight legs, then folded forward. | Coming down near the floor, the trunk turns ahead of the tumble towards the side it will lie on (`DriveFall`), and the first frames down pull the trunk flat (turn weight 2.2 → 1) before the limbs ease into the lie. |
| `Lie` / `LieFront` were planks with hyperextended elbows; the face-down far leg pointed up. | Knees a little bent, arms loose along the body (on the back) or drawn in by the shoulders (on the front), the head off the floor. |
| `getUp` was a kip-up (hips at 128°, legs flung up); `getUpFront` a push-up whose hands never reached the floor. | **Hand-push Rise** (`getUp`): sit up chin tucked, both hands planted behind (wrist goals at floor height, held f6–f12), the palms press the hips off the floor, the body rocks forward over the planted feet into a squat, rises and steps back into the stance. **Push-up Rise** (`getUpFront`): hands planted by the shoulders f5–f17, press up to hands and knees, hop the feet in under the hips with the hands still down, lift the hands once the weight is over the feet, step into the stance. Same ids, length (30 / 26), invulnerability (0–30 / 0–26) and dust frames. |
| A foot placed elsewhere by the next key glided along the floor. | `MoveDef.StepArc`: a grounded foot moving more than 5 cm between two keys lifts in a low arc (up to 7.5 cm, 30 % of the step) with the toe dipping. A foot that is one of the move's hit points (kicks, sweeps) is untouched, as are airborne moves. Hit windows, frames, root motion and cancels are unchanged; leg hurt circles rise by at most a few centimetres for the frames of a step. |

The attack keys were audited on the skeleton (every grounded move of the seven families, every frame from the stance): no wrong-way
knee, no hyperextended elbow, no grounded frame with both feet off the floor, and every blow loads the hips or trunk (or rides root
motion) in the 8 frames before it. Feet still travel with the root on root-motion moves (the dodges are flat slides by design; a
lunge is a sliding step).

## Playstyles

| Style | What it gets | What keeps it from winning everything |
| --- | --- | --- |
| Defensive / counter | Parries on every tier, counter hits, posture breaks on bosses, the Iai Counter riposte (Kagetsu, Shizuka) | The lockout; unblockable boss hits; a string of guarded blows breaks a turtled guard sooner; foes grab a guarding Kage |
| Aggressive / rushdown | Chains, guard pressure, a dash out of a connected blow's recovery | A repeated button or string gets read and parried by skilled foes, and the stagger costs him the turn |
| Hit-and-run / mobility | Dash out of a connected blow, whiff punishes, the back step and forward slide | A whiff can't be cancelled; elites shrug off light pokes (armour) |

## What this moves

- `MoveFrameData.txt` (EditMode): the backflip's spin field in the seven families that share it (every frame the same). Later the spin
  field left the snapshot altogether, and the roll and back step lines changed (X-only root motion in three phases, not airborne).
- `UprightGroundedTests` (EditMode): every family move and every boss and sub-boss ability keeps its hips within 45° (the kip-ups
  aside), and every dodge slides flat.
- `RaikonAscendantTests`: the Final Overload check now waits for the move to end instead of a fixed frame count. The instant flip
  lets one more blow land in that 220-frame move, and its 8 frames of hit-stop pushed it past the old fixed count.
- PlayMode baselines (`Tests/PlayMode/Baselines`, legacy too): positions and events can move wherever a cross-up, a reel from
  behind, a launch or a backflip happens. Not run here (agents run EditMode only); re-record with `KAGE_RECORD_BASELINES=1`
  after the owner's playtest if the diffs read right.
- Not render-reviewed: the parry cues and the back hop's poses have not been looked at in a playing build.
- The contact-matched parry and the reference kata: `MoveFrameData.txt` gains the four kata, their chain links and two cancel frames
  (`lowReap` 14, `tachi.sweep` 25); `PacingTuningTests`' damage hash gains their four lines (without them it is the old hash). PlayMode
  baselines may move where a profiled foe now throws a kata or a parry's clash pose shifts a hurt circle; the legacy AI is gated off
  the kata. The clash poses and kata are judged on the skeleton and in headless fights, not yet looked at in a playing build.
