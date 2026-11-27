# Act I Sub-Bosses — Martial Arts Movesets, Raikon Lineage & VFX Brief

This document covers the martial-arts combat expansion, weapon redesign, and procedural VFX crafted for the four Act I sub-bosses (**Kuroe**, **Garan**, **Hisame**, **Shiden**) using the installed `effekseer-vfx` skill (`craft.md`, `layering.md`, `timing.md`, `stylized.md`).

Like `docs/raikon-ascendant-vfx.md`, effects are authored in KAGE's procedural `Vfx` engine (`Assets/Kage/Scripts/VFX/SubBoss/` and `Assets/Kage/Scripts/Boss/SubBossAura.cs`), because Unity has no Effekseer runtime package installed and Godot (`vfx doctor`: `render: false`) is not installed on this machine.

---

## 1. Storyline Thread: Trained by Raikon, the Lightning Ascendant

Each Act I sub-boss studied under or served **Raikon** and carries a distinct **apprentice echo** of his Lightning Ascendant kit, scaled down to sub-boss level (~40–50 % of Raikon's spatial footprint, no full-screen flash frames, blockable mid-level hazards/waves):

| Sub-Boss | Martial Art Discipline | Signature Element | Raikon Lineage Pattern |
|---|---|---|---|
| **Kuroe, the Ink Censor** | Bōjutsu · Shodō Calligraphy | Sumi-e black ink slashes, splats, floating calligraphy seals (封 · 斬), ink puddles that erupt into blades | **Sumi Nidan (P2)** lays a delayed ink-and-crimson-spark floor line (*Kesa-Giri* echo); **Thunder Redaction** calls a tracking crimson-ink seal bolt (*Heavens' Verdict* / *Skyfall* echo) |
| **Garan, the Storm Bearer** (Phase 2: *Stormbroken*) | Kyokushin Karate · Sumō | Blue-white lightning: heavy strikes from the sky, thunder domes, ground-cracking impacts, an electric aura | **Shiko Raijin Stomp** sends a short delayed floor spark line; **Broken Static Palm (P2)** channels chest-scar crimson lightning into his iron *tekko* (*Static Palm* echo) |
| **Hisame, the Rain Courier** | Tessenjutsu (Iron War Fans) | Water: fast water crescents, flowing ribbons, rain-streak dashes and watery afterimages, sinking mist | **Lightning Cut** (*Thunder Swing* first stroke); **Kasumi Fan Dance** ends in a 360° fan vortex (*Tempest Wheel* echo); **Inazuma Mist Step (P2)** dashes through Kage leaving a delayed electrified slash (*Blink Flurry* echo) |
| **Shiden, the Last Lieutenant** | Hōzōin-ryū Sōjutsu | Purple-black lightning: blade energy waves, thunder-infused spear trails, explosive lightning impacts, a dark electric aura | **Suriage · Ō-Nagi** low sweep into electrified rising fang (*Low Sweep → Rising Cut* / *Sky-Splitter* echo); **Vanguard Raijin Arc (P2)** launches a ground-skimming violet-crimson spark wave (*Raijin Arc* echo) |

---

## 2. Kuroe's Weapon Redesign (`SpearVisual.cs`)

`temple_staff` now renders as `Head.InkBrush` — an *Ichimonji*-style giant calligraphy ink brush-pen (`fude` + fountain nib collar) aligned to `SpearMoves.BladeLength`:
- **Barrel**: Dark ebony lacquer (`#1E1714`) with censor-gold rings (`#C9A45C`), weighted iron/gold end-cap with hanging cord loop, and a wrapped censor paper slip bearing two black redaction bars.
- **Ferrule & Nib Collar**: Stepped dark iron ferrule (`Spear_PenFerrule`), gold collars, and a split golden fountain-pen nib (`Spear_PenNib` + `Spear_PenSlit`).
- **Ink-Soaked Brush (`hosaki`)**: Swelling calligraphy belly tapering to a sharp writing point (`Spear_Blade`), saturated front four-fifths in void-black sumi ink (`Spear_BrushInk`, `#050507`), glossy wet sheen (`Spear_BrushSheen`), and a paper-ivory tip glint.

---

## 3. Sub-Boss VFX — four combat looks

Each sub-boss has a **combat look** (`CombatLook`, `Assets/Kage/Scripts/VFX/SubBoss/`). It owns three things:

1. **Weapon trails along the real weapon path.** `SampledTrail` records the weapon's inner and outer edge every frame a move's trail window is open, Catmull-Rom smooths the samples, and the boss's own trail draws them. This replaces the generic `MotionTrail` for **every** move the boss plays, so the effect always follows the animation. Trails run on scaled time, so a stroke freezes with the fighters in hit-stop.
2. **Contact effects.** When a blow lands or is guarded, `Fighter.OnHitResolved` asks the attacker's look first (`Hit` / `Block`), and only falls back to the generic `HitBurst` / `BlockBurst`.
3. **Set pieces.** The cue handlers in each `*Abilities.cs` call that boss's own effects (`Vfx.Kuroe.cs`, `Vfx.Garan.cs`, `Vfx.Hisame.cs`, `Vfx.Shiden.cs`). `SubBossAura` draws the between-move presence and the Phase 2 entry burst from the same files.

### 3.1 Refinement pass (2026-11-26): bolder 2D anime combat VFX

The first pass read as thin lines and small stars next to the fighters. This pass follows the owner's reference sheet (four panels: *Kuroe — The Ink Censor*, *Garan — The Storm Bearer*, *Hisame — The Rain Courier*, *Shiden — The Last Lieutenant of Raikon*). Every look gets a **signature body shape with mass** (a splat, a seal, a blade crown, a sky strike, a water crescent, a black energy wave), and the four identities are pulled apart on every axis. Garan's effects move from ember-orange iron to **blue-white lightning**, Hisame's from wind to **water**, and Shiden's from a silver needle to **purple-black lightning**.

Shared pieces (`Vfx.SubBossKit.cs`):

- `Blob`: a filled, noise-rimmed fan for ink and water.
- `Ell`: a point and normal on a turned ellipse, the path of every crescent and spiral.
- `Hash`: stable per-piece random values.
- `TintedScorch`: a ground mark in a boss's own dark and glow colours.
- A `Bolt` overload with its own rim colour. Raikon's bolts keep their blood-black rim; Garan's get storm navy and Shiden's a heavy purple-black.

| | Kuroe — living calligraphy | Garan — the storm brought down | Hisame — flowing water | Shiden — purple-black lightning |
|---|---|---|---|---|
| **Read first** | A black splat and a broad written stroke; a seal glyph | A thick vertical strike from the sky | A translucent crescent of water | A black-cored violet crescent; a black spike burst |
| **Shape** | Brush strokes, splats with tendrils and satellite drops, round seals, curved blades | Jagged vertical bolts, radial bolts, a dome, cracks | Smooth crescents, waving ribbons, slanted streaks | Crescents, spikes, straight lances |
| **Motion** | Written (the head races out), held wet, dried away; blades erupt and melt | Falls, slams, bursts outward; re-struck every 3 frames | Sweeps and flows; strands peel off and wave; drops fall | Cuts forward and travels; explodes outward |
| **Rhythm** | Medium: written in 6–8 f, held, a long drying tail (0.5–0.9 s) | Heavy: a big contact, a held crackle, rock and dust settle | The shortest contact, a flowing exit (0.2–0.4 s) | A sharp contact, a violent body, a dark lingering exit |
| **Material and value** | Alpha sumi body, wet sheen lane, paper-gold edge, lit paper behind fresh ink | White-hot core, electric-blue body, storm-navy rim | Ice edge, aqua highlight, translucent teal body over a slate backing | White core, violet glow, purple-black core and rim, a few gold sparks |
| **Secondary** | Ink drops, ink mist, paper ash, floor stains | Rock chips, dust, cracks with a blue glow, a scorch | Droplets, rain streaks, sinking mist, ripples | Sparks, dark smoke, a violet scorch |
| **Trail** | A wet stroke a little broader than the brush: five bristle lanes drying into kasure, flung ink specks past the edge, a gold sheen while fresh (0.5 s) | A storm trail on fists and shins: navy backing, an electric body whose edges are re-struck every 3 frames, a white core on the leading part, arcs crawling back along the path (0.14 s) | A water trail on both fans: slate, teal, aqua and an ice edge; two strands peel off and wave as it ages; drops shed (0.2 s) | The spear point: a purple-black sheet, a violet sheet, a white needle, and a line of lightning beside the track re-struck every 3 frames (0.18 s) |
| **Hit** | Nib star → splat thrown along the blow → a broad stroke written across the target → spray, mist, floor stain. Heavy: a crossing stroke and a stamped 斬 seal | Blue-white star → compression discs → 4–6 radial bolts → thunder ring, sparks, dust. Heavy: a strike called down onto the target's feet, rock, a camera punch | Ice glint → a water crescent slashed across the target → splash crown, ripple, sinking mist | Needle through the target → Mach ring → an explosive blast (spike burst, radial bolts, ring, smoke). Heavy: an impact frame and a bolt carried on through |
| **Guard** | A curved smear on the guard, a small splat back toward her | A dent ring, static crawling the guard, sparks, skid dust | A short curl of water splashing back off the guard | A spark fan along the guard, static, a wisp of dark smoke |
| **Presence (aura)** | Ink beads off the tip; now and then a small seal floats up and burns (often in Phase 2, with crimson) | An electric aura: arcs surge over his outline, sparks lift off his fists. Phase 2: a constant surge, bolts climbing from the floor, static round his feet | Drips off the cape and a little rain. Phase 2: a downpour round her and a ribbon curling round her | Dark smoke smouldering off his shoulders. Phase 2: dark flames from his feet, violet and crimson arcs on his outline and up the shaft |
| **Phase 2** | Crimson static in the ink, splats and seals | Crimson forks in the strikes and the aura | Crimson arcs along the water, static between the fans | Crimson-violet in every bolt, wave and blast |

Signature set pieces:

- **Kuroe.** `InkSplat`. `KuroeSeal`: 封 or 斬 written stroke by stroke in paper-gold on a disc of ink, floating, then burning away from the rim. `KuroeInkBlades`: a puddle erupting into curved blades that melt back. The Ichimonji **execution**: a great stroke, a splat and a large 斬 seal with a paper flash. A blade crown round the slam. In Phase 2 the ink line erupts into a row of blades. Thunder Redaction: a 封 seal hangs over Kage through the tell, then a blade crown rises round a tall centre blade.
- **Garan.** `GaranThunderStrike`: a main bolt with forks, two side strands, a star, flat rings, the `GaranThunderDome`, cracks, rock, a scorch and a pale-blue flash. The ground slam adds two flanking strikes and static skittering over the floor. The Storm Fist fissure has static at its racing head and a strike where it ends. The Shiko line throws rock spikes with bolts leaping out of the crack. The Harite palm is a storm cannon: discs plus a bolt down the blast.
- **Hisame.** `HisameWaterArc`: slate, teal, aqua and an ice edge, two waving strands, droplets off the eroding tail. `HisameWaterRibbon`, `HisameRainVeil`, and `HisameWaterGhost` (a teal afterimage, rain streaks and a ribbon laid along the dash). The fan gust is a crescent that travels and widens; the spin is a water vortex; the Inazuma cleave is one great water crescent.
- **Shiden.** `ShidenEnergyCrescent`: a black core, a violet glow, a hot edge, lightning along the outer edge and sparks off both tips. `ShidenThunderBlast`. `ShidenLance`, the Ō-Tsuki **finisher**: a black-violet beam wrapped in two bolts, cone rings and an impact frame. The rising fang and the low sweep are energy waves; Vanguard Break is three purple-black strikes that each explode; the Raijin ground wave is a standing black crescent.

Key frames (60 FPS, from the contact):

- **f0–4:** the contact glint or star only.
- **f4–12:** the body shape takes over (the splat opens, the stroke is written, the strike is on its main and return stroke, the crescent is swept, the blast is open).
- **f12–30:** the body changes (it dries, melts, erodes, re-strikes or travels) while the secondary matter moves.
- **f30–55:** residue only (stains, scorch, mist, smoke, ash).

Avoid:

- A recoloured shared effect between two bosses.
- A white blob that lasts past f5.
- Black ink with nothing lit behind it on the dark stage.
- Garan's blue reading as Hisame's water. His is jagged, additive and white-cored; hers is smooth, translucent and teal.
- Full-screen flashes. At sub-boss level only the execution, the strikes and the finishers get a brief flash or impact frame.

**Verification:** `SubBossLadderTests.Every_sub_boss_effect_builds_over_its_whole_life` runs every effect's build callbacks (including the new signature pieces) from u = 0 to 1, and every trail through a sweep and its drying. `SubBossVfxScreenshots` renders one review sheet per boss (trail mid-swing, contact, +8 frames, residue) to `Screenshots/review/subboss-vfx/`. As with the Raikon VFX, there is no Effekseer runtime and no Godot filmstrip review here; the looks are judged from those Unity renders.

---

## 4. Martial Arts Move Tables (60 FPS Frames)

### Kuroe, the Ink Censor (`KuroeAbilities.cs`)
- **Phase 1 — The Black Line (100 %)**: `censure`, `kuroe.sumiNidan`, `kuroe.sumiBarai`, `kuroe.bokuseki` (Calm wind, dripping ink tip)
- **Phase 2 — Redacted (50 %, aggressive)**: + `kuroe.thunderRedaction`, `sumiNidan` upgraded with ink-spark floor line (`InkBreeze` wind, crimson-gold brush static)

| Move | Martial Origin | Startup | Active | Total | Notes |
|---|---|---|---|---|---|
| `kuroe.censure` | Bōjutsu Cross Guard | f4 | Counter f4–45 → Punish f8–20 | 60 / 41 | Diagonal *Ichimonji* ink stroke on punish |
| `kuroe.sumiNidan` | Bōjutsu *Kesa-Uchi → Gyaku-Kesa* | f19 | f19–23 down · f34–38 rising | 62 | P2 lays `InkSparkLine` (22f telegraph) |
| `kuroe.sumiBarai` | Bōjutsu *Ashibarai → Shōmen-Uchi* | f20 | f20–24 Low sweep → on contact `shomenDrop` f14–18 Overhead | 48 / 40 | Teaches low guard; splatters ink pool |
| `kuroe.bokuseki` | Bleach Calligraphy Slash Wave | f24 | f24–28 cut + `kuroe.inkWave` projectile f25 | 54 | Mid-range sumi crescent wave |
| `kuroe.thunderRedaction` | Raikon's *Skyfall* Lineage | — | Tracking `RedactionSeal` f2 (12f follow, 34f strike) | 62 | Phase 2 ranged call |

### Garan, the Stormbroken (`GaranAbilities.cs`)
- **Phase 1 — Thunderstruck (100 %)**: `stormFist`, `garanTsuppari`, `garan.clinch`, `garan.gedanCombo`, `garan.shikoStomp`
- **Phase 2 — Stormbroken (50 %)**: + `charge`, `garan.brokenPalm` (`EmberGust` wind, chest-scar → tekko crimson arcs)

| Move | Martial Origin | Startup | Active | Total | Notes |
|---|---|---|---|---|---|
| `garan.gedanCombo` | Kyokushin *Seiken → Harite → Gedan Mawashi* | f16 | f16–20 Mid · f28–32 Mid · f42–46 Low shin kick | 72 | Classic knockdown-karate high-low pocket string; cues `GaranSeiken` f17 · `GaranPalmWave` f29 · `GaranGedanKick` f43 |
| `garan.shikoStomp` | Sumō *Shiko* Leg Lift + Raikon Spark Line | f24 | Stomp f24–28 + `ShikoSparkLine` (f11–24 mark, f24–28 strike) | 68 | Ceremonial leg lift crackling with vanguard sparks |
| `garan.brokenPalm` | Raikon's *Static Palm* Lineage | f26 | Armoured f18–26; electrified palm f26–30 (knockdown) | 66 | Phase 2 heavy electrified palm shock |

### Hisame, the Rain Courier (`HisameAbilities.cs`)
- **Phase 1 — Rainfall (100 %)**: `lightningCut`, `hisameKocho`, `hisame.kasumiMai`, `hisame.rainGust` (`RainWind`)
- **Phase 2 — Downpour (50 %, berserk ×0.7 cd)**: + `smokeBomb`, `hisame.inazumaStep` (`DownpourSquall` wind, crimson-blue fan static)

| Move | Martial Origin | Startup | Active | Total | Notes |
|---|---|---|---|---|---|
| `hisame.kasumiMai` | Tessenjutsu *Atemi → Kasumi → Cross-sweep* | f14 | f14–17 · f26–30 · f40–48 stepping twin-fan cross-sweep (upright, no spin) | 68 | Light courier cousin of Raikon's *Tempest Wheel*; cues `HisameAtemi` f15 · `HisameFanSlash` f27 · `HisameFanWheel` f41/46 |
| `hisame.rainGust` | Tessenjutsu Spacing Snap | f15 | Back-step f2–10 + `rainGust.blade` projectile f15 | 42 | Fast mid-range rain-wind crescent |
| `hisame.inazumaStep` | Raikon's *Blink Flurry* Lineage | f25 | Pass-through dash f25–32 + delayed `InazumaDelayedSlash` (22f) | 58 | Phase 2 rain-ghost dash + delayed lightning cut |

### Shiden, the Last Lieutenant (`ShidenAbilities.cs`)
- **Phase 1 — Vanguard (100 %)**: `vanguardBreak`, `shidenBrace`, `shiden.chargingThrust`, `shiden.makiOtoshi`, `shiden.sojutsuSweep`
- **Phase 2 — Hold the Line (50 %, aggressive)**: + `vanguardGuard`, `shiden.raijinLance` (`LieutenantGust` wind, violet-crimson yari static)

| Move | Martial Origin | Startup | Active | Total | Notes |
|---|---|---|---|---|---|
| `shiden.makiOtoshi` | Hōzōin-ryū *Maki-Otoshi → Ren-Tsuki* | f16 | f16–20 beat (`postureMul 1.6`) · f29–33 probe · f43–49 piston *Ō-Tsuki* | 72 | Authentic cross-spear bind into centerline thrust; cues `ShidenMakiBeat` f17 · `ShidenSpearPierce` f30 · `ShidenStormPierce` f44 |
| `shiden.sojutsuSweep` | Sōjutsu *Suriage · Ō-Nagi* | f20 | f20–24 Low sweep · f35–40 rising electrified fang | 60 | Echoes Raikon's *Low Sweep → Rising Cut*; cues `ShidenLowSweep` f21 · `ShidenRisingFang` f36 |
| `shiden.raijinLance` | Raikon's *Raijin Arc* Lineage | f28 | Armoured f0–26; slash f28–32 + `shiden.raijinWave` ground wave f29 | 60 | Phase 2 disciplined lieutenant ground wave |

---

## 5. Difficulty ladder (`SubBossTier`)

Sub-boss I is the gentlest and sub-boss IV the hardest, and every rung is harder than the one before on every knob. The JS stats are unchanged; a tier is applied on top of them:

- In each roster's `Def()`: footwork and AI skill.
- In `Boss()`: per-phase attack speed, damage and cooldowns.
- In `Tuning/health.json`: health.

| Knob | Kuroe (I) | Garan (II) | Hisame (III) | Shiden (IV) |
|---|---|---|---|---|
| Walk/run speed (× JS speed) | 1.25 (→ 1.25) | 1.36 (→ 1.25) | 1.3 (→ 1.51) | 1.55 (→ 1.55) |
| Runs in beyond | 3.3 m | 3.0 m | 2.6 m | 2.3 m |
| AI skill (added to the stage level) | −0.08 | 0 | +0.1 | +0.2 |
| Attack speed P1 / P2 | 1.0 / 1.03 | 1.03 / 1.07 | 1.07 / 1.11 | 1.1 / 1.15 |
| Damage P1 / P2 (× JS dmg) | 0.9 / 0.95 (→ 1.04) | 1.0 / 1.06 (→ 1.1) | 1.2 / 1.26 (→ 1.14) | 1.3 / 1.38 (→ 1.21) |
| Ability cooldowns P1 / P2 | ×1.15 / ×1.0 | ×1.0 / ×0.9 | ×0.9 / ×0.8 | ×0.8 / ×0.7 |
| Phase 2 speed-up | ×1.05 | ×1.08 | ×1.1 | ×1.12 |
| Health (JS hp × pacing mul) | 360 × 1.15 = 414 | 395 × 1.25 = 494 | 375 × 1.35 = 506 | 400 × 1.31 = 524 |
| String chance P1 / P2 | 0.3 / 0.5 | 0.45 / 0.62 | 0.6 / 0.76 | 0.72 / 0.88 |
| Beat between links P1 / P2 | 16 / 13 f | 13 / 11 f | 10 / 8 f | 8 / 6 f |

- **Walking faster:** `CharacterDef.groundSpeedMul` scales walk and run only, not dash or jump distance.
- **Running in:** the AI used to approach in 8–16 frame steps, which never reached the 0.42 s run threshold. Beyond `runInBeyond` it now holds forward until it breaks into a run, and stops the moment it is back in its fighting range (`AIInputSource`, `pursue` steps).
- **AI skill:** `AIInputSource.Level` = stage level + skill. It drives reaction time, guard, parry and punish rates, aggression and think rate.

## 6. Attack patterns (boss strings)

Each kit's abilities string into follow-ups through `AbilityDef.chain`. The AI takes the first follow-up that is enabled in the current phase, off cooldown and in range, at its rung's chance and beat (§5). Phase 2 moves sit first in each list, so the strings change character when the phase turns.

| Boss | Strings |
|---|---|
| Kuroe | Sumi Barai → Bokuseki / Sumi Nidan · Bokuseki → Thunder Redaction (P2) / Sumi Nidan · Sumi Nidan → Thunder Redaction (P2) / Sumi Barai · Thunder Redaction → Bokuseki |
| Garan | Gedan combo → Shiko Stomp / Tsuppari · Tsuppari → Clinch (into a guard) / Storm Fist · Shiko Stomp → Broken Palm (P2) / Storm Fist · Storm Fist → Charge (P2) / Gedan combo · Broken Palm → Charge / Tsuppari |
| Hisame | Rain Gust → Inazuma Step (P2) / Lightning Cut · Lightning Cut → Kasumi Mai / Smoke Bomb (P2) · Kasumi Mai → Rain Gust / Inazuma Step · Smoke Bomb → Lightning Cut / Inazuma Step · Inazuma Step → Kasumi Mai / Rain Gust · Kocho → Kasumi Mai / Lightning Cut |
| Shiden | Maki-Otoshi → Suriage sweep / Vanguard Break · Suriage → Raijin Lance (P2) / Charging Thrust / Maki-Otoshi · Vanguard Break → Maki-Otoshi / Raijin Lance · Raijin Lance → Charging Thrust / Maki-Otoshi · Charging Thrust → Suriage / Maki-Otoshi |
