# Raikon, the Lightning Ascendant — boss fight and VFX brief

The three-phase boss fight (code: `Assets/Kage/Scripts/Boss/Abilities/RaikonAscendant*.cs`) and its effects. The effects
follow the effekseer-vfx skill's craft rules (time structure, value layering, shape change, eased particles, screen
feedback; lightning's element signature) and are built in the game's procedural `Vfx` (`Vfx.Lightning.cs`), the way
`Vfx.cs` already applies that skill. They are not Effekseer assets: this project has no Effekseer runtime for Unity, and
the skill's Godot review renderer is not installed on this machine. So nothing here has an independent filmstrip review
yet (see "Not verified" at the end).

## Constraints kept

- Skeleton, hurt circles, body scale and the tachi hit line (`RaikonPartSet.PaintedBladeLength`, ADR-0005) are unchanged.
  Every new blade window uses `HitPoint.Blade` on the painted blade line.
- One combat path (ADR-0008): every new attack is an ordinary `MoveDef` started as an ability slot, a counter answer, a
  `then` follow-up, or a phase's entry move.
- Secondary motion: each phase sets `FighterRig.wind` on both fighters (P1 Calm, P2 Moderate Gust, P3 Storm Gale, ramped
  in over the transition), and every phase shift, dash, blink, teleport and slam kicks `FighterRig.Impulse`, which calls
  `AddImpulse` on the rig's `SoftChains`, `CoatCloth` and `HairField`.
- Afterimages are `FighterRig.Snapshot` ghosts (the frozen soft meshes are owned by a `MeshOwner`), tinted crimson-white.

## The telegraph system (three colours, never mixed)

| Tell | Cue | Means | Answer |
|---|---|---|---|
| Steel / white glint on the blade (or the striking limb) | `VFX_Raikon_Tell_Steel` | a sword or martial strike that can be blocked and parried | block, parry |
| Bright red eye flash with a long thin lens streak | `VFX_Raikon_Tell_Eye` | a high-speed iaijutsu or blink: it cannot be guarded, it can be parried (`HitWindow.unguardable`) | dodge, or a tight parry |
| Crimson-white crackling aura round the body | `VFX_Raikon_Tell_Aura` | an unblockable lightning attack (`HitLevel.Unblockable`) | jump, roll, or hit him first |

Rules: each tell fires 10–20 frames before the active frames it announces; a move shows only the tells of the attacks it
has; and the ambient storm (blade static, the P3 eye trail) never uses the tell shapes. The static is short, low sparks.
The eye trail is a thin, dim ribbon that never flares like the eye tell.

## Palette roles

| Role | Colour | Job |
|---|---|---|
| core | white `#FFFFFF`, hot pink-white `#FFE6E6` | contact flashes, the bolt's core line, 2–5 frames |
| body | storm crimson `#FF2A36` (the old `#FF3B2F` storm red stays on the legacy cues) | the bolt body, arcs, rings: names the element |
| dark | blood black `#2A0306` (alpha blend) | the scorch under a bolt, the residue under slashes, the dim veil |
| matter | ash red-grey `(110, 78, 78)` | dust, floor scorch smoke |
| accent | steel `#D8E4F0` | only on the steel tell and blocks |
| accent | eye red `#FF1010` | only on the eye tell and the P3 eye trail |

## Lightning signature (craft §3)

A jagged polyline that does not travel. A main strike, a return stroke and an after-flicker on separate frames. The
path is re-struck (a new jag) every 3 frames. Ground cracks and crawling arcs are the secondary motion. `Vfx.Bolt` has
the first three built in: main f0–3, return stroke at 35–45 % of life, after-flicker at 70 %, a white core inside a
crimson body inside a dark rim. A scorch decal and rising ash do the dissipation.

## Effect briefs

Frames are 60 FPS from the cue. Each has the skill's three key frames: earliest readable, body takes over, last residue.

| Effect | Purpose | Earliest readable | Body takes over | Last residue | Avoid |
|---|---|---|---|---|---|
| `VFX_Raikon_Sky_Bolt` | hazard strike (Heavens' Verdict, Final Overload, transition 1) | f0: a white line from the sky top to the mark, 0.4 m flash on the floor | f4: the crimson body re-strikes on a new jag; the shock ring opens on the floor; sparks spray up and out | f40: the scorch ellipse cools, ash rises | a bolt that stays white; a straight line |
| `VFX_Raikon_Floor_Spark_Line` | telegraph + strike of a delayed floor line (Kesa-Giri P2, Blink Flurry, Crimson Flash trail) | telegraph: a dim crawling crimson crack along the floor | strike f0: a row of short bolts up from the crack; sparks; a ring at each end | f30: crack glow dies, ash | a bright telegraph that looks like the strike |
| `VFX_Raikon_Tell_*` | the three tells | f0 | f4 | f16 | tells that read alike |
| `VFX_Raikon_Blink_Ghost` | blink / dash afterimage | f0 a crimson-white snapshot | it fades and drifts up | f17 gone | ghosts on every frame (every 2nd only) |
| `VFX_Raikon_Decoy` | Afterimage Feint's decoy | f0 a held white-crimson snapshot | f14 it pops: a star, a ring, sparks out | f30 ash | a decoy that fades before it pops |
| `VFX_Raikon_Plant_Shock` | transition 1 shockwave | f0 a flash at the blade's foot | a wide ground ellipse and a dust roll outward | f40 dust | a full-screen white |
| `VFX_Raikon_Blade_Ignite` | transition 1, the bolt into the tachi | the sky bolt onto the blade tip | arcs crawl down the blade for 0.8 s | the blade static stays on (phase 2 level) | — |
| `VFX_Raikon_Spiral_Burst` | transition 2 | f0 a flash at the chest | three crimson spiral arms sweep out and erode; a ring; sparks along the arms | f45 ash ring | a symmetric starburst |
| `VFX_Raikon_Overload_Pull` | Final Overload draw-in | f0 the screen dims (dark veil 35 %) | streaks and motes move inward to him (charges invert the time structure) | the one release flash at the twin arcs | brightening during the pull |
| `VFX_Raikon_Iai_Slice` | Crimson Flash / Final Overload thrust | a thin slice line across the path, aspect > 40:1, for 8–15 frames | the dark residue line under it | f30 | a thick bar |
| `VFX_Raikon_Palm_Charge` | Static Palm | crimson-white glow gathering in the palm | release flash + forward shards | sparks | — |
| `VFX_Raikon_Wheel` | Tempest Wheel spin | a crackling blade circle at chest height | it re-strikes as he turns | sparks | a clean ring (draw broken arcs) |

Screen feedback (craft §1.5) rides the hits' own `hitstop` and `shake`. Only the finisher (Final Overload's thrust)
gets a flash frame. The dim veil is not an impact frame.

## Phases

| Phase | Opens at | Wind on both rigs | Abilities |
|---|---|---|---|
| 1 — Pure Samurai | 100 % | Calm | kesaGiri, pommelKick, crimsonFlash, lowSweep, ironGuard |
| 2 — Storm Blade | 65 % (entry move: Storm Kindling) | Moderate Gust (2.2 m/s, gusts 2.6) | phase 1 (with the upgrades) + raijinArc, thunderStep, skySplitter, staticPalm, tempestWheel |
| 3 — Lightning Ascendant | 30 % (entry move: Ascension) | Storm Gale (4.6 m/s, gusts 5.4, 1.2 gusts/s) | everything + blinkFlurry, heavensVerdict, ascendantRush, afterimageFeint |
| 3b — Final Overload | 12 % (entry move: Final Overload, once) | Storm Gale | as phase 3 |

The four SAN-107 abilities (thunderSwing, stormBreak, skyfall, redlineCounter) stay registered in their slots, but no
phase enables them any more. The new kit replaces them.

## Moves (frames at 60 FPS)

| Move | Tell | Startup | Active | Recovery / punish | Notes |
|---|---|---|---|---|---|
| Kesa-Giri | steel ×3 | 21 (0.35 s) | f21 diag down · f36 reverse up · f63 overhead (0.45 s after hit 2) | 36 (0.6 s) | P2: hit 3 lays a floor spark line that strikes 0.35 s later |
| Pommel Check → Front Kick | steel | 13 (0.22 s) hilt | f13 hilt (stagger) · f26 push kick (heavy knockback to tachi range) | 22 | |
| Crimson Flash (iaijutsu) | eye ×2 | 33 (0.55 s) low crouch | f34–45 dash-slash, 8.5 m (~60 % of a 14 m arena), unguardable | 48 (0.8 s) | P2: a trailing lightning arc on the path strikes 0.25 s after he stops |
| Low Sweep → Rising Cut | steel (low) | 23 (0.38 s) | f23 low sweep, beats a standing guard | 30 on whiff | on hit or block: the rising cut (launch) |
| Iron Guard Counter | steel | 4 | counter window f4–34 (0.5 s): catches one light blow from the front | 8 spent | AI: reactive, after 3 light attacks in 1.25 s; a heavy blow or a guard-break lands as a hit; the answer slash can be parried |
| Raijin Arc | aura | 27 (0.45 s) raise | f27 slam + ground wave (unblockable, jumpable) | 33 (0.55 s) | |
| Thunder Step | eye + boot static | 18 (0.3 s) | blink f18–29 (0.18 s, invulnerable, ghosts) → f31 spinning back-kick → f44 horizontal slash | 30 | parrying the kick cancels the slash (the parry reels him) |
| Sky-Splitter | aura | 18 (0.3 s) crouch | f18 rising uppercut (hop) + a thin vertical bolt | ~39 (0.65 s) landing | AI: reactive anti-air |
| Static Palm | aura + palm glow | 25 (0.42 s) | f25 unblockable thrust, heavy knockback, a large posture cost | 32 | |
| Tempest Wheel | steel | 24 (0.4 s) spin tell | f24 kick · f34 kick · f44–60 360° blade spin | 30 | |
| Blink Flurry | eye ×3 | 33 (0.55 s) | 3 chained dashes: low · reverse mid · aerial dive slam; each leaves a slash that detonates 0.3 s later | the blade sticks in the floor 66 (1.1 s) | three `MoveDef`s linked by `then` |
| Heavens' Verdict | aura | — | bolts at f35, f69, f103 (0.55 s tells, following Kage); a flying kick f76 between bolts 2 and 3 | 22 | |
| Ascendant Rush | steel (on the rising kick) | 21 (0.35 s) | elbow f21 · sweep f30 · rising kick f39 · air spin f48 · slam f62 | 40 | parrying any hit (the rising kick included) breaks the chain |
| Afterimage Feint | — | escape | after 4+ hits of Kage's combo: a decoy that pops (stun) f14, a blink behind f2–12, a slash f22 | 28 | cooldown 11 s; phase 3 on |
| Storm Kindling (→P2) | — | invulnerable | f14 zero-damage knockback shockwave · f30 sky bolt ignites the blade | to f84 | wind ramps to Moderate Gust |
| Ascension (→P3) | — | invulnerable | f20 crimson spiral burst + zero-damage knockback | to f80 | wind ramps to Storm Gale, the red eye trail turns on |
| Final Overload | aura, eye | f0–48 pull + dim (invulnerable) | f48 twin waves · bolts f80, f96 · f110 full-arena unblockable thrust | kneels 90 (1.5 s) | once, at 12 % |

## Not verified

- No independent `vfx review` filmstrip: the Godot renderer is not installed (`vfx doctor`: `render: false`). The
  effects are judged by the craft rules only, until the owner plays the fight.
- No Effekseer `.efkefc` assets: the Unity project has no Effekseer runtime.
- PlayMode feel (tells, punish windows, combo links) needs the owner's playtest.
