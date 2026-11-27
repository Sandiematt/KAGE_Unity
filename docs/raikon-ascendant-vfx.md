# Raikon, the Lightning Ascendant — boss fight and VFX brief

The three-phase boss fight (code: `Assets/Kage/Scripts/Boss/Abilities/RaikonAscendant*.cs`) and its effects. The effects
follow the effekseer-vfx skill's craft rules (time structure, value layering, shape change, eased particles, screen
feedback; lightning's element signature) and are built in the game's procedural `Vfx` (`Vfx.Lightning.cs`,
`Vfx.Thunder.cs`, `Vfx.Ascendant.cs`), the way `Vfx.cs` already applies that skill. They are not Effekseer assets: this
project has no Effekseer runtime for Unity, and the skill's Godot review renderer is not installed on this machine. So
nothing here has an independent filmstrip review yet (see "Not verified" at the end).

The expansion pass (2026-11-27) adds nine moves (three a phase), upgrades four of them across phases, and adds nine boss
strings. Every new row below is marked **new**; every existing move, frame, tell, wind state and string probability is
unchanged.

## Constraints kept

- Skeleton, hurt circles, body scale and the tachi hit line (`RaikonPartSet.PaintedBladeLength`, ADR-0005) are unchanged.
  Every new blade window uses `HitPoint.Blade` on the painted blade line.
- One combat path (ADR-0008): every new attack is an ordinary `MoveDef` started as an ability slot, a counter answer, a
  `then` follow-up, an on-contact follow-up, or a phase's entry move. The expansion's teleports, ring lock and decoy run
  as `MoveDef.Step` frame actions inside those moves.
- Secondary motion: each phase sets `FighterRig.wind` on both fighters (P1 Calm, P2 Moderate Gust, P3 Storm Gale, ramped
  in over the transition), and every phase shift, dash, blink, teleport and slam kicks `FighterRig.Impulse`, which calls
  `AddImpulse` on the rig's `SoftChains`, `CoatCloth` and `HairField`.
- Afterimages are `FighterRig.Snapshot` ghosts (the frozen soft meshes are owned by a `MeshOwner`), tinted crimson-white.

### Phase identity (the expansion's rule)

| Phase | Allowed | Never |
|---|---|---|
| 1 — Pure Samurai | steel, momentum, dust, air shear, embers | sky lightning, clouds, teleports or blinks, unblockable or unguardable lightning |
| 2 — Storm Blade | crackling tachi, ground lightning, static tethers, short-range blinks | sky strikes out of a cloud on his own moves (Storm Kindling's ignition bolt is the transition) |
| 3 — Lightning Ascendant (and 3b) | full storm: cloud-called strikes, delayed detonations, afterimage and decoy mix-ups | — |

## The telegraph system (three colours, never mixed)

| Tell | Cue | Means | Answer |
|---|---|---|---|
| Steel / white glint on the blade (or the striking limb, or the scabbard mouth) | `VFX_Raikon_Tell_Steel` (`_Hand`, `_Foot`, `_Saya`) | a sword or martial strike that can be blocked and parried | block, parry |
| Bright red eye flash with a long thin lens streak | `VFX_Raikon_Tell_Eye` (`_Cloud`: the same flash in the storm over a locked ring) | a high-speed iaijutsu or blink: it cannot be guarded, it can be parried (`HitWindow.unguardable`) | dodge, or a tight parry |
| Crimson-white crackling aura round the body | `VFX_Raikon_Tell_Aura` | an unblockable lightning attack (`HitLevel.Unblockable`) | jump, roll, or hit him first |

Rules: each tell fires 10–20 frames before the active frames it announces; a move shows only the tells of the attacks it
has; and the ambient storm (blade static, the P3 eye trail) never uses the tell shapes. The static is short, low sparks.
The eye trail is a thin, dim ribbon that never flares like the eye tell.

Two expansion moves bend the second rule on purpose, and say so in their tell column: **Whirlwind Feint** shows a steel
glint on a wind-up that never lands (the bait) and the eye flash on the thrust that does; **Mirage Severance**'s decoy
dash does no damage, so it carries no tell, and the read is the eye trail (only the real Raikon keeps it). No new effect
uses a white four-point star before an active frame (the steel tell's shape), a horizontal red lens streak (the eye
tell's), or crimson-white body crackle (the aura's) outside its own tell.

## Palette roles

| Role | Colour | Job |
|---|---|---|
| core | white `#FFFFFF`, hot pink-white `#FFE6E6` | contact flashes, the bolt's core line, 2–5 frames |
| body | storm crimson `#FF2A36` (the old `#FF3B2F` storm red stays on the legacy cues) | the bolt body, arcs, rings: names the element |
| dark | blood black `#2A0306` (alpha blend) | the scorch under a bolt, the residue under slashes, the dim veil |
| matter | ash red-grey `(110, 78, 78)` | dust, floor scorch smoke |
| accent | steel `#D8E4F0` | only on the steel tell and blocks |
| accent | eye red `#FF1010` | only on the eye tell and the P3 eye trail |
| air (new) | pale shear `#EEF1F6` at ≤ 70 % alpha | phase 1's blade arcs, air shear and wind blades: steel-adjacent but never a star or a glint |

## Lightning signature (craft §3)

A jagged polyline that does not travel. A main strike, a return stroke and an after-flicker on separate frames. The
path is re-struck (a new jag) every 3 frames. Ground cracks and crawling arcs are the secondary motion. `Vfx.Bolt` has
the first three built in: main f0–3, return stroke at 35–45 % of life, after-flicker at 70 %, a white core inside a
crimson body inside a dark rim. A scorch decal and rising ash do the dissipation.

## Phases

| Phase | Opens at | Wind on both rigs | Abilities |
|---|---|---|---|
| 1 — Pure Samurai | 100 % | Calm | kesaGiri, pommelKick, crimsonFlash, lowSweep, ironGuard, **sheatheCross, tsubaPress, whirlwindFeint** |
| 2 — Storm Blade | 65 % (entry move: Storm Kindling) | Moderate Gust (2.2 m/s, gusts 2.6) | phase 1 (with the upgrades) + raijinArc, thunderStep, skySplitter, staticPalm, tempestWheel, **fulminationDrag, arcAnchor, galeSever** |
| 3 — Lightning Ascendant | 30 % (entry move: Ascension) | Storm Gale (4.6 m/s, gusts 5.4, 1.2 gusts/s) | everything (with the upgrades) + blinkFlurry, heavensVerdict, ascendantRush, afterimageFeint, **tempestCage, zenithGuillotine, mirageSeverance** |
| 3b — Final Overload | 12 % (entry move: Final Overload, once) | Storm Gale | as phase 3 (see "Phase 3b inheritance") |

The four SAN-107 abilities (thunderSwing, stormBreak, skyfall, redlineCounter) stay registered in their slots, but no
phase enables them any more. The new kit replaces them. A phase's new abilities keep their weight in the random pick
(`AbilityDef.weight`): phase 2's ×1.5, phase 3's ×2, phase 1's ×1; the expansion's nine moves are weighted a further ×1.5
(`RaikonAscendant.ExpansionWeight`) so they come up in a playtest.

**Reviewing the kit in the dev arena** (`KageGame.BossReview.cs`): **F11** drops the boss just under its next phase's
threshold, so the phase opens the way it does in a fight (entry move, wind, storm); **F12** / **Shift+F12** make the foe
play the next / previous ability its current phase enables, newest first, in that phase's version, with a banner naming it.

## Moves (frames at 60 FPS)

### Phase 1 — Pure Samurai (100 % → 65 %)

| Move | Tell | Startup | Active | Recovery / punish | Notes |
|---|---|---|---|---|---|
| Kesa-Giri | steel ×3 | 21 (0.35 s) | f21 diag down · f36 reverse up · f63 overhead (0.45 s after hit 2) | 36 (0.6 s) | P2: hit 3 lays a floor spark line that strikes 0.35 s later |
| Pommel Check → Front Kick | steel | 13 (0.22 s) hilt | f13 hilt (stagger) · f26 push kick (heavy knockback to tachi range) | 22 | |
| Crimson Flash (iaijutsu) | eye ×2 | 33 (0.55 s) low crouch | f34–45 dash-slash, 8.5 m (~60 % of a 14 m arena), unguardable | 48 (0.8 s) | P2: a trailing lightning arc on the path strikes 0.25 s after he stops |
| Low Sweep → Rising Cut | steel (low) | 23 (0.38 s) | f23 low sweep, beats a standing guard | 30 on whiff | on hit or block: the rising cut (launch) |
| Iron Guard Counter | steel | 4 | counter window f4–34 (0.5 s): catches one light blow from the front | 8 spent | AI: reactive, after 3 light attacks in 1.25 s; a heavy blow or a guard-break lands as a hit; the answer slash can be parried |
| **Sheathe Cross** | steel ×2 (f12 scabbard mouth, f22 hilt) | 24 (0.4 s): backstep 0.8 m f2–12, blade sheathed f8–23 | f24 fast horizontal draw · f35 reverse draw (both mid, blockable; the draw lunges 0.7 m) | 34 (0.57 s) | punishes chasing the backstep. P2: Static Cross |
| **Tsuba Press → Overhead Cleave** | steel (f3 hilt; the cleave's own glint 18 frames before it) | 15 (0.25 s), advancing 0.75 m | f15 hilt shove: 1 point of damage, the posture of a heavy guarded blow · on hit or block the cleave starts at once: overhead at f46 (30 frames after a f15 contact), heavy, knockdown, parryable (a parry reels him for the boss reel) | press whiff 32; cleave 36 (0.6 s) | whiffed shove → the string goes to Low Sweep. P2: Slam Thunder + a V of spark lines |
| **Whirlwind Feint** | steel → eye (f2 steel on the wind-up is the bait; f15 eye is the truth) | 29 | f0–14 wide horizontal wind-up · f15 feint, skid 0.9 m round his guard (f15–24) · f29 quick thrust (mid, unguardable) | 32 | the wide cut never lands |

### Phase 2 — Storm Blade (65 % → 30 %)

| Move | Tell | Startup | Active | Recovery / punish | Notes |
|---|---|---|---|---|---|
| Raijin Arc | aura | 27 (0.45 s) raise | f27 slam + ground wave (unblockable, jumpable) | 33 (0.55 s) | |
| Thunder Step | eye + boot static | 18 (0.3 s) | blink f18–29 (0.18 s, invulnerable, ghosts, flat along the floor) → f31 chambered push kick → f44 horizontal slash | 30 | parrying the kick cancels the slash (the parry reels him) |
| Sky-Splitter | aura | 18 (0.3 s) crouch | f18 rising uppercut (hop) + a thin vertical bolt | ~39 (0.65 s) landing | AI: reactive anti-air |
| Static Palm | aura + palm glow | 25 (0.42 s) | f25 unblockable thrust, heavy knockback, a large posture cost | 32 | |
| Tempest Wheel | steel | 24 (0.4 s) tell | f24 kick · f34 kick · f44–60 grounded blade sweep, forward then backhand (no body spin) | 30 | |
| **Fulmination Drag** | aura (blade tip low; pulses f4–34) | 40 | blade tip to the floor f10 · rush up to 6 m dragging it f18–34 (stops at Kage's body) · f40 upward flick: unblockable launch | 36 (0.6 s) | P3: the gouge detonates in three rising pillars |
| **Arc Anchor** | steel (f6) + aura (f34, f40, f46) | 22 | f22 hurls the spinning tachi: 9 m, mid (block or parry), a hit pins Kage 0.6 s · f50 blinks to the hilt · f52 radial extraction slash (unblockable, 2.8 m wide) | 34 (to f90) | deflecting (parrying) the throw knocks the blade down and cancels the blink: he recalls it, 40 frames open |
| **Gale Sever** | eye (f4; f24 for the second crescent) | 20 | f20 wide crescent swing (mid, unguardable) sending a vertical vacuum blade (16 m/s, unguardable, parryable) · f38 a low horizontal crescent (13 m/s along the floor, low, unguardable: jump it) | 34 (to f72) | P3: a third, delayed X-crescent |

### Phase 3 — Lightning Ascendant (30 % → 12 %)

| Move | Tell | Startup | Active | Recovery / punish | Notes |
|---|---|---|---|---|---|
| Blink Flurry | eye ×3 | 33 (0.55 s) | 3 chained dashes: low · reverse mid · aerial dive slam; each leaves a slash that detonates 0.3 s later | the blade sticks in the floor 66 (1.1 s) | three `MoveDef`s linked by `then` |
| Heavens' Verdict | aura | — | bolts at f35, f69, f103 (0.55 s tells, following Kage); a flying kick f76 between bolts 2 and 3 | 22 | |
| Ascendant Rush | steel (on the rising kick) | 21 (0.35 s) | elbow f21 · sweep f30 · rising kick f39 · air cut f48 · slam f62 | 40 | parrying any hit (the rising kick included) breaks the chain |
| Afterimage Feint | — | escape | after 4+ hits of Kage's combo: a decoy that pops (stun) f14, a blink behind f2–12, a slash f22 | 28 | cooldown 11 s; phase 3 on |
| **Tempest Cage** | aura ×2 (f14, f42) | 16 hilt slam | the ring locks on Kage at f15 · f30 four lightning spears drop on a 5 m ring (unblockable; its two sides zap once and bounce Kage back in, f30–62) · f40 blinks to his side of the ring · f44 chord dash across (mid, blockable) · f54 turns at the far spears · f58 chord dash back (unblockable final cross) | 36 (to f100) | jumping out of the ring before f30 escapes it: both chords run inside the ring |
| **Zenith Guillotine** | eye (f4; f24 in the cloud over the ring) + aura (f40, f46) | 14 | f14 vanishes upward (invulnerable f14–34) as a cloud and a ring follow Kage; the ring locks at f30 · f34 reappears over the lock · f38 blade-first crash (overhead, heavy, unguardable: parryable) · f52 expanding discharge: one ground wave each way (unblockable, jump it) | 36 (to f90) | |
| **Mirage Severance** | eye ×2, flickering (f10, f20) | 16 | f16 splits into two silhouettes · f26 the fake dashes in (no damage) and dissolves into red ash · the real blinks behind Kage f26–34 (invulnerable f16–34) · f36 slash (mid, unguardable) leaving a mark that detonates 0.25 s later (unblockable) | 34 (to f73) | read: only the real Raikon keeps the dim red eye trail |

### Phase shifts and the Final Overload (unchanged)

| Move | Tell | Startup | Active | Recovery / punish | Notes |
|---|---|---|---|---|---|
| Storm Kindling (→P2) | — | invulnerable | f14 zero-damage knockback shockwave · f30 sky bolt ignites the blade | to f84 | wind ramps to Moderate Gust |
| Ascension (→P3) | — | invulnerable | f20 crimson spiral burst + zero-damage knockback | to f80 | wind ramps to Storm Gale, the red eye trail turns on |
| Final Overload | aura, eye | f0–48 pull + dim (invulnerable) | f48 twin waves · bolts f80, f96 · f110 full-arena unblockable thrust | kneels 90 (1.5 s) | once, at 12 % |

### Cross-phase upgrades

A move's upgraded version is its `MoveDef.variantFor` answer from the phase on (`PhaseOf(f) >= n`), so every later phase,
3b included, plays it.

| Move | From | Upgrade | Gameplay |
|---|---|---|---|
| Kesa-Giri | P2 | hit 3 lays a floor spark line, struck 0.35 s later | hazard, unblockable |
| Crimson Flash | P2 | a trailing lightning arc on the path, struck 0.25 s after he stops | hazard, unblockable |
| **Sheathe Cross** | P2 | **Static Cross**: the X where the two draws met lingers 0.4 s, crackling, then snaps once in a short arc burst | hazard at the X (1.0 m ahead, chest height box 1.2 × 1.8 m): telegraph f35–59, strike f59–63, unblockable |
| **Tsuba Press → Overhead Cleave** | P2 | the cleave triggers Slam Thunder (cracks + 2 bolts) and sends two diagonal floor spark lines forward in a V that erupt 0.3 s later | hazard 0.3–3.1 m ahead: telegraph 18, strike 5, unblockable |
| **Fulmination Drag** | P3 | the gouge detonates in three rising lightning pillars, each 0.35 s after he passed it | hazards laid at f22, f28, f34, 0.4 m behind him: strike f43, f49, f55, unblockable launch |
| **Gale Sever** | P3 | a third, delayed X-crescent at f54 (eye at f40) that leaves burning static residue on the floor for 1.2 s | projectile (unguardable, parryable) + a floor hazard 0.55–5.05 m ahead, live 1.2 s, one light zap |

## Boss strings

| After | Goes into (first that is ready and in range) | Why |
|---|---|---|
| Pommel Check → Front Kick | Crimson Flash, Raijin Arc, Low Sweep | the push kick throws Kage out to the iaijutsu's range |
| Kesa-Giri | Pommel Check, Thunder Step | guard pressure after the cleave |
| Low Sweep | Kesa-Giri, Tempest Wheel | a whiffed sweep becomes a string |
| Crimson Flash | Blink Flurry, Thunder Step | he turns round on the far side |
| Raijin Arc | Crimson Flash, Thunder Step | the wave makes Kage jump; the flash takes him on landing |
| Thunder Step | Kesa-Giri, Static Palm | the blink puts him at Kage's back |
| Tempest Wheel | Static Palm, Pommel Check | |
| Static Palm | Raijin Arc, Crimson Flash | the palm knocks Kage far: a wave or the flash follows |
| Blink Flurry | Heavens' Verdict, Thunder Step | |
| Heavens' Verdict | Crimson Flash, Ascendant Rush | |
| Ascendant Rush | Thunder Step, Blink Flurry | |
| Afterimage Feint | Ascendant Rush, Kesa-Giri | |
| **Sheathe Cross** | Pommel Check | the reverse draw leaves him at hilt range |
| **Tsuba Press** (only when the shove whiffed) | Low Sweep | a whiffed shove drops low under the counter-attack; a connected one already became the cleave (`AbilityDef.chainIf`) |
| **Whirlwind Feint** | Kesa-Giri | the thrust staggers; the cleave pattern follows |
| **Fulmination Drag** | Sky-Splitter, Tempest Wheel | the flick launches: the anti-air takes the air, the wheel the landing |
| **Arc Anchor** | Static Palm, Thunder Step | he arrives at Kage's side with an empty-handed palm, or blinks again |
| **Gale Sever** | Raijin Arc, Crimson Flash | the low crescent makes Kage jump: the wave or the flash takes the landing |
| **Tempest Cage** | Heavens' Verdict, Blink Flurry | the cage's storm is already overhead |
| **Zenith Guillotine** | Ascendant Rush, Static Palm | Kage lands from the discharge wave in front of him |
| **Mirage Severance** | Crimson Flash, Blink Flurry | he is behind Kage after the blink |

A string starts only after the move's full recovery, so its punish window stays open. It is dropped when Raikon is hit,
when Kage is down, and when no follow-up is ready or in range. The chance and the beat are the phase's (unchanged):
followed 45 % on a 14-frame beat in phase 1, 70 % / 9 in phase 2, 85 % / 5 in phase 3, 92 % / 3 in 3b.

## Phase 3b inheritance (Final Overload, 12 % → 0 %)

3b is a phase of its own (`PhaseDef` "Final Overload"), not a copy of phase 3, so it inherits by rule rather than by list:

- **Abilities**: its list is phase 3's list object (`p3`), so it holds every phase 3 move, the three new ones included,
  with no separate entry to keep in step.
- **Upgrades**: every variant switches on `PhaseOf(f) >= n` (Static Cross, the V lines, the Kesa-Giri spark line and the
  Crimson Flash arc from 1; the gouge pillars and the X-crescent from 2), so phase index 3 plays every upgraded phase 2
  and phase 3 pattern.
- **Strings**: `ChainChance` and `ChainBeat` read the phase index: 3b follows every string, the nine new ones included,
  at 92 % on a 3-frame beat.
- **Storm power**: effects scale by `RaikonStorm.PowerOf` (3 in 3b): Zenith Guillotine's Slam Thunder throws 6 bolts
  instead of 4, Tempest Cage's clouds and the sky strikes fork more. Shapes and colours never change, so a tell reads the
  same in 3b as in phase 1.

## Effect briefs

Frames are 60 FPS from the cue. Each has the skill's three key frames: earliest readable, body takes over, last residue.

| Effect | Purpose | Earliest readable | Body takes over | Last residue | Avoid |
|---|---|---|---|---|---|
| `VFX_Raikon_Sky_Bolt` | hazard strike (Heavens' Verdict, Final Overload, transition 1) | f0: a white line from the sky top to the mark, 0.4 m flash on the floor | f4: the crimson body re-strikes on a new jag; the shock ring opens on the floor; sparks spray up and out | f40: the scorch ellipse cools, ash rises | a bolt that stays white; a straight line |
| `VFX_Raikon_Floor_Spark_Line` | telegraph + strike of a delayed floor line (Kesa-Giri P2, Blink Flurry, Crimson Flash trail, **the cleave's V**) | telegraph: a dim crawling crimson crack along the floor | strike f0: a row of short bolts up from the crack; sparks; a ring at each end | f30: crack glow dies, ash | a bright telegraph that looks like the strike |
| `VFX_Raikon_Tell_*` | the three tells | f0 | f4 | f16 | tells that read alike |
| `VFX_Raikon_Blink_Ghost` | blink / dash afterimage | f0 a crimson-white snapshot | it fades and drifts up | f17 gone | ghosts on every frame (every 2nd only) |
| `VFX_Raikon_Decoy` | Afterimage Feint's decoy | f0 a held white-crimson snapshot | f14 it pops: a star, a ring, sparks out | f30 ash | a decoy that fades before it pops |
| `VFX_Raikon_Plant_Shock` | transition 1 shockwave | f0 a flash at the blade's foot | a wide ground ellipse and a dust roll outward | f40 dust | a full-screen white |
| `VFX_Raikon_Blade_Ignite` | transition 1, the bolt into the tachi | the sky bolt onto the blade tip | arcs crawl down the blade for 0.8 s | the blade static stays on (phase 2 level) | — |
| `VFX_Raikon_Spiral_Burst` | transition 2 | f0 a flash at the chest | three crimson spiral arms sweep out and erode; a ring; sparks along the arms | f45 ash ring | a symmetric starburst |
| `VFX_Raikon_Overload_Pull` | Final Overload draw-in | f0 the screen dims (dark veil 35 %) | streaks and motes move inward to him (charges invert the time structure) | the one release flash at the twin arcs | brightening during the pull |
| `VFX_Raikon_Iai_Slice` | Crimson Flash / Final Overload thrust (**and Tempest Cage's chords**) | a thin slice line across the path, aspect > 40:1, for 8–15 frames | the dark residue line under it | f30 | a thick bar |
| `VFX_Raikon_Palm_Charge` | Static Palm | crimson-white glow gathering in the palm | release flash + forward shards | sparks | — |
| `VFX_Raikon_Wheel` | Tempest Wheel blade sweep | a crackling blade circle at chest height | it re-strikes as he turns | sparks | a clean ring (draw broken arcs) |
| **`VFX_Raikon_Draw_Arc`** (`_Reverse`) | Sheathe Cross's two draws | f0: a pale crescent opens along the draw in 3 frames, white leading edge | f4: the arc's tail erodes; two thin crimson air-shear ribbons peel off and run ahead; the second draw crosses the first into an X | f18: a few red embers drift down from the ends | a lightning arc (phase 1); a closed ring |
| **`VFX_Raikon_Backstep_Dust`** | the backstep's kick | f0 a dust kick off the front foot | dust rolls forward (the way he left) | f30 | — |
| **`VFX_Raikon_Static_Cross`** (`_Mark`, `_Snap`) | Sheathe Cross P2 | mark f0: the X stays drawn, dim crimson, re-struck every 4 frames | its flicker and crawling arcs rise to the snap at 0.4 s | snap: a white pop, short arcs out along the four arms, a small ring; f20 ash | a mark as bright as the snap |
| **`VFX_Raikon_Air_Cone`** | the shove's compressed air | f0 pale lines converge on the hilt | they burst forward in a cone and a squashed ring opens at the mouth | f12 gone | a white star (the steel tell's shape) |
| **`VFX_Raikon_Ring_Spark`** | the shove's contact (only when it connected) | f0 a steel-white ring and warm metal sparks fanned back | sparks droop under gravity | f20 | — |
| **`VFX_Raikon_Wind_Split`** | the cleave | f0 a tall white-cored vertical line along the drop | the air parts: two pale lines bow outward from it; dust blows sideways; deeper floor cracks | f30 cracks cool | lightning in phase 1 (the cracks glow ember, no bolts) |
| **`VFX_Raikon_Feint_Smear`** | Whirlwind Feint's cancelled swing | f0 a wide broken half-arc in pale shear | it erodes from both ends; red embers peel off it and drift back | f24 embers only | a full arc that reads as a hit |
| **`VFX_Raikon_Skid`** | the sidestep | f0 dirt thrown low, sideways | a dark skid streak on the floor | f40 the streak fades | — |
| **`VFX_Raikon_Needle_Flash`** | Whirlwind Feint's thrust | f0 a very thin needle line (> 40:1) shoots forward in 2 frames, white core | it holds 4 frames, thins, crimson body | f14 gone | a thick bar; a horizontal lens streak at the eye height (the eye tell's) |
| **`VFX_Raikon_Rooster_Tail`** | Fulmination Drag's drag (every 2 frames) | sparks thrown back and up off the dragged tip | a glowing crimson gouge on the floor behind the tip, its dark residue under it; a micro-bolt hops off it every third step | the gouge cools to blood black over 1.1 s | sparks at constant speed |
| **`VFX_Raikon_Needle_Fan`** | the flick | f0 a star at the tip | seven jagged lightning needles fan upward over 3 frames | f20 sparks fall | a symmetric fan (biased forward) |
| **`VFX_Raikon_Gouge_Pillar`** (`_Mark`) | Fulmination Drag P3 | mark: the gouge segment glows and crawls | strike f0: a lightning pillar rises from the floor (a light column and a bolt upward), sparks up | f40 scorch, ash | a bolt from the sky (these rise) |
| **`VFX_Raikon_Anchor_Blade`** | Arc Anchor's thrown tachi and tether | f0 the blade leaves his hand spinning inside a crimson-white buzzsaw halo | a flickering static tether joins his hand to it; on landing: impact sparks, the blade planted and humming | the tether snaps at the blink (ghosts streak along it) or at the recall | a tether bright enough to read as a strike |
| **`VFX_Raikon_Radial_Slash`** | Arc Anchor's extraction | f0 a ring of broken crimson slash arcs round him | it re-strikes once as it widens; sparks out | f24 | a clean ring |
| **`VFX_Raikon_Vacuum_Blade`** / **`_Low_Crescent`** / **`_X_Crescent`** | Gale Sever's projectiles | f0 a translucent pale crescent (vertical, low, or crossed) | crimson static crawls on its rim, re-struck every 3 frames; a dust wake skims the floor under it; faint wind streaks at the screen's edges | it dissolves where it ends | an opaque white blade |
| **`VFX_Raikon_Static_Residue`** | the X-crescent's floor residue | f0 crimson embers ignite along the floor | small arcs crawl on it for 1.2 s | it cools to blood black | a strip so bright it reads as a strike |
| **`VFX_Raikon_Cage`** | Tempest Cage's spears and perimeter | f16: four small storm clouds gather over the four spear points | f30: four spears fall out of them and stand, re-struck; buzzing arcs run round the ring between them | f64 the spears shatter into spark shrapnel as the second chord passes | spears that only scale and fade |
| **`VFX_Raikon_Zenith_*`** (column, ring, meteor, discharge) | Zenith Guillotine | f14: a column of ghosts up into the storm | a crackling ring widens under Kage, a cloud over it; f34–38 a white-hot meteor streak down onto the lock | Slam Thunder at f38; the discharge ring expands with the waves at f52 | a ring that keeps following after the lock |
| **`VFX_Raikon_Mirage_*`** (split, decoy, burst) | Mirage Severance | f16: a double-exposure split: two offset silhouettes with a static seam between them | the decoy holds, dashes in at f26 and dissolves into red ash; the slash line holds razor-thin | f51: it pops into a star burst and a micro-bolt falls on it | a decoy with an eye trail |

Screen feedback (craft §1.5) rides the hits' own `hitstop` and `shake`. Only the finisher (Final Overload's thrust)
gets a flash frame. The dim veil is not an impact frame. None of the expansion's effects adds a flash frame.

## VFX implementation notes (the expansion)

The shader layers are the procedural `Vfx`'s: **AddHDR(k)** (an additive HDR ribbon; k is the HDR multiplier the bloom
reads), **Alpha** (`MeshKit.Alpha`, an alpha-blended ribbon: the dark rims and residues, so effects read on bright stages
too), the particle emitters **sparks / embers / dust**, **Snapshot** (`FighterRig.Snapshot` frozen copies tinted through
the `_Flash` property block) and **ThunderCloud** (the gathered storm cloud). Frames are the move's own. Sounds are
`KageAudio`'s synthesised `SfxKind`s; no new clip is made.

| Effect | Layers | Active frames | Audio-visual tell cue |
|---|---|---|---|
| **Static Cross** (Sheathe Cross P2) | mark: AddHDR 1.6 X ribbon (crimson, alpha 0.25 → 0.6) + crawling `Arc`s every 4 frames (AddHDR 2.4 body, 3.2 core, Alpha blood-black rim) · snap: `Star` 0.6, six short arcs along the arms, a small ring, sparks | mark f35–59 (0.4 s) · snap f59–63 | it rides the two steel glints of the draws; the snap plays `Slash` |
| **Spark Rooster-Tail** (Fulmination Drag) | sparks (white / crimson, eased: thrown back at 3–7 m/s, gravity) + AddHDR 1.8 gouge segment (crimson, life 0.7 s) over an Alpha blood-black residue (life 1.1 s) + a micro-`Arc` every third step | every 2nd frame f18–34 | the aura pulses f4–34 announce the flick; the drag plays `SwordWhoosh` once at f18 |
| **Static Tether** (Arc Anchor) | a long re-struck `Bolt`-style ribbon from his far hand to the blade: AddHDR 2.0 crimson body at 35–60 % flicker, no white core (never a strike's value), Alpha rim · the buzzsaw halo: AddHDR 2.4 broken arcs spinning round the blade + a steel blade line | f22 (throw) to f50 (blink) or the recall | the steel glint at f6 tells the throw; the aura f34–46 tells the extraction; the throw plays `Crescent`, the blink `Whoosh` |
| **Lightning Spears** (Tempest Cage) | four `ThunderCloud.Gather` pinpoints (f16–30) · each spear: a sky `Bolt` out of its cloud at f30, then a held AddHDR 2.6 jagged spear re-struck every 3 frames over an Alpha rim · perimeter: `Crackle` on the ring ellipse (rx 1.9, ry 0.2) every 4 frames · finish: sparks shrapnel and `Star` per spear | clouds f16–30 · spears f30–64 · shatter f64 | aura ×2 (f14, f42); the spears' fall plays `HitHeavy` once, the shatter `Clang` |
| **Meteor Streak** (Zenith Guillotine) | a tapered AddHDR 3.0 white-hot streak out of the cloud over the lock down to the floor + AddHDR 2.4 crimson body + Alpha rim (×2.2); a pointed white head shedding sparks; the tail catches up | f34–38 (to f44 fade) | the eye in the cloud at f24 tells the crash; the landing plays `Thud` with `VFX_Raikon_Slam_Thunder` |
| **Mirage Split** (Mirage Severance) | two Snapshot ghosts offset ±0.25 m (one hot white, one crimson) + a vertical static seam (`Arc`, no white core) + a thin horizontal shear streak · the decoy: a held Snapshot that slides in f26–36 then breaks into embers and Alpha ash puffs | split f16–22 · decoy f16–36 · ash to f60 | eye ×2 (f10, f20, flickering); the real keeps the dim eye trail, the decoy has none; the dash plays `Whoosh` |
| Draw arcs + air shear (Sheathe Cross) | AddHDR 1.6 pale crescent + AddHDR 2.6 white edge + Alpha blood-black underlay; two AddHDR 1.8 crimson shear ribbons; embers | f24–40 and f35–51 | steel f12 (scabbard mouth) and f22 (hilt); each draw plays `SwordWhoosh` |
| Air cone, ring spark, wind-split (Tsuba Press) | AddHDR 1.6 pale cone lines + a squashed ring · steel ring + warm metal sparks · AddHDR 2.6 white line, pale parting lines, dust, extra `GroundCracks` | f14–26 · cleave f0–20 · f29–60 | steel f3 (hilt), the cleave's steel at its f12; the ring spark plays `Clang` |
| Feint smear, skid, needle flash (Whirlwind Feint) | AddHDR 1.4 broken half-arc + embers · dust + an Alpha skid streak · AddHDR 2.8 needle line + a 3-point tip spark | f15–39 · f15–55 · f29–43 | steel f2 (bait), eye f15; the thrust plays `Slash` |
| Needle fan, gouge pillars (Fulmination Drag) | seven thin `Bolt`s fanned upward, `Star` · per pillar: `LightPillar` + an upward `Bolt` + `Scorch` | f40–60 · f43 / f49 / f55 | aura pulses; the pillars ride their own glowing gouge marks |
| Radial slash (Arc Anchor) | `WheelArcs` 1.3 m + two crossing `SlashLine`s + ring + sparks | f52–70 | aura f34–46 |
| Vacuum blade, low crescent, X-crescent, residue (Gale Sever) | Alpha pale translucent crescent + AddHDR 2.2 crimson rim arcs re-struck every 3 frames + dust wake; screen-edge streaks (AddHDR 1.4, 0.25 s) · residue: AddHDR 1.6 ember strip + crawling arcs, Alpha cool-down | f20–60 · f38–84 · f54–100 · residue f54–126 | eye f4 / f24 (/ f40 in P3); each crescent plays `Crescent` |
| Zenith column, ring, discharge | ghosts rising into the storm + a thin AddHDR streak to the cloud base · a following crackle ring (rings + `Crackle`, widening) that locks at f30 · a floor ring expanding to 4 m with the waves | f14–20 · f14–38 · f52–80 | eye f4 and in the cloud f24; aura f40 / f46 |

## Thunder pass (2026-11-24)

The owner's notes after playing the fight: the effects did not feel like a boss, the moves and patterns looked weak, and
Raikon should carry a red aura round his whole body all the time. Thunder only, more of it each phase, a cloud before
every bolt from above, and a thunderstorm in the background. Same route as above: the effekseer-vfx craft rules in the
procedural `Vfx`, no Effekseer runtime.

### What is new

| Piece | Code | Job |
|---|---|---|
| Standing red aura | `RaikonAura` (Boss/) | always on, round the whole body; says "boss" at a glance |
| Storm cloud | `ThunderCloud` (VFX/) | the anticipation of every bolt from above: it gathers, then the bolt falls out of its base |
| Thunder strike | `Vfx.ThunderStrike` (Vfx.Thunder.cs) | a sky bolt with a light column, a branch, a re-strike, running ground cracks and the backdrop lit |
| Backdrop storm | `RaikonSkyStorm` (Boss/) | a storm bank across the sky, sheet lightning, far bolts behind the city, near strikes on the roofs |
| Thunder on his moves | `RaikonStorm.Surge` | lightning along the blade (and the swept path behind its tip) through every active window, down the striking limb, a streak behind every dash |
| Thunder on his blows | `Vfx.ThunderImpact`, `RaikonStorm.OnEvent` | arcs out of each landed blow; from phase 3 a heavy blow calls a bolt down from a cloud onto the one he hit |
| A slam's thunder | `VFX_Raikon_Slam_Thunder` → `Vfx.SlamThunder` | the floor cracks open where the blade lands; bolts erupt out of the cracks from phase 2 |
| Boss strings | `AbilityDef.chain`, `AIInputSource.ChainStep`, `RaikonAscendant.Strings` | he goes from one ability straight into the next on a beat, instead of picking at random every time |

### Phase ramp

One number drives all of it: `RaikonStorm.Phase` (0 Pure Samurai, 1 Storm Blade, 2 Lightning Ascendant, 3 Final
Overload). The effects grow per phase in size, count and rate. They never change shape or colour, so a phase-1 tell still
reads the same in phase 3.

| | Phase 1 | Phase 2 | Phase 3 | Final Overload |
|---|---|---|---|---|
| Aura | smoulders: a rim, a glow, small tongues | burns: adds a halo, dark arcs over the body | blazes: tall tongues with a white-hot root | hotter still |
| Blade in a window | a thin arc every other frame | an arc every frame, sparks, the swept bolt | two arcs every frame, a thicker swept bolt | as phase 3 |
| Landed blow | 2 arcs, a star | adds a ring | adds a crackle; heavy blows call a bolt | as phase 3 |
| Slam | cracks only | cracks and 2 bolts | cracks, 4 bolts, a ring | 6 bolts |
| Sky strike | (none in phase 1) | adds a branch | adds a re-strike | more forks |
| Backdrop | a distant storm, a flash every 3.5–7 s | rolls in, 1.8–3.6 s, crawlers | breaks over the roofs, 0.8–1.9 s, near strikes | 0.45–1.1 s |
| Strings | followed 45 %, 14-frame beat | 70 %, 9 frames | 85 %, 5 frames | 92 %, 3 frames |

### Effect briefs

Frames are 60 FPS from the cue. Each has the skill's three key frames.

| Effect | Purpose | Earliest readable | Body takes over | Last residue | Avoid |
|---|---|---|---|---|---|
| Red aura (`RaikonAura`) | boss presence, all fight | — (always on) | a red silhouette rim that hugs every pose, a heat-glow that sways up, tongues licking off the outline | embers rising off him, a red glow on the floor | a solid red blob over his body (it draws behind him); white arcs (the tell's) |
| Aura flare (on `VFX_Raikon_Tell_Aura`) | makes the unblockable tell louder | f0: the rim turns white-hot | f4: tongues burst taller and faster | f13: back to red | a flare on blockable moves |
| Storm cloud (`ThunderCloud`) | the anticipation of a bolt from above | f0: puffs drift in from wide, faint | f(gather)·0.5: a lumpy dark band near the top of the view, lit red from below, flickering inside, arcs crawling between puffs | after the strike: the puffs swell, drift apart and thin over 0.9 s | a cloud so bright it reads as the strike; a clean oval |
| Thunder strike (`Vfx.ThunderStrike`) | the strike out of the cloud | f0: the white core line from the cloud base, a light column, a star on the floor | f3: the branch lands beside it; cracks run out; rings open; sparks spray up | f40: the scorch cools, ash rises, cracks fade to blood-black | a straight line; a bolt that starts off-screen with no cloud |
| Slam thunder | a slam that hits like thunder | f0: a star at the blade's foot; cracks white-hot | f4: cracks crimson; bolts erupt ahead of him | f30: the cracks' residue | a symmetric burst (the bolts go mostly forward) |
| Backdrop storm | a thunderstorm behind the fight | — | the bank churning; a far bolt forking behind the city, which stands black against it | the bank's glow dying | bolts in front of the fighters; flashing the fighters themselves |

Value layering throughout: a white core (bolt cores, crack heads, tongue roots in phase 3), the crimson or aura-red body,
and a dark side (blood-black bolt rims, tongue rims and crack residue; the cloud's dark backing). The persistent aura's arcs
have no white core, so the white-crimson crackle stays the unblockable tell's alone.

### Draw order

The aura copies and tongues sit just under his own parts (`baseOrder` −26 to −14). The floor glow is on the backdrop's
floor (−1100). The storm clouds are behind the fighters (600). The backdrop storm sits inside `EnvBackdrop`'s layers:
the bank over the moon and painted clouds (−2978), far bolts behind the far city (−2950), near strikes behind the roof
ridges (−2606), and the backdrop flash over the wash (−2540). The fight's own bolts and sparks stay on top (`Vfx.Order`+).
The expansion's floor marks (gouges, residue, the skid) sit with the scorches (`Vfx.Order` −30 to −10); its spears,
tethers and crescents with the bolts.

## Not verified

- No independent `vfx review` filmstrip: the Godot renderer is not installed (`vfx doctor`: `render: false`). The
  effects are judged by the craft rules only, until the owner plays the fight. The thunder pass's own render check is
  `RaikonThunderScreenshots` (PlayMode, graphics on) into `Screenshots/review/raikon-thunder/`. The expansion's effects
  were rendered in edit mode (2026-11-27): each move played in a headless fight with its real cues, frames rendered to
  contact sheets in `Screenshots/review/raikon-expansion/` (dark stage, and phase 1 on a bright stage too). That pass made
  the phase 1 arcs, air cone and feint smear, the Static Cross, the wind-blade rims, the residue and the meteor bolder,
  spread the cage's clouds, and hid the aura while Zenith Guillotine has him vanished. Not an independent review.
- No Effekseer `.efkefc` assets: the Unity project has no Effekseer runtime.
- PlayMode feel (tells, punish windows, combo links, the cage's escape timing, the mirage read) needs the owner's
  playtest. EditMode tests cover the frame data, the tells, the strings and the hooks in headless fights.
