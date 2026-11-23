# ADR-0005: Character data

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-66 (builds it), SAN-68, SAN-69, SAN-71, SAN-76, SAN-84, SAN-86, SAN-95, SAN-97 to SAN-107, SAN-110

## Context

Today `KageGame.SpawnFighter` hard-codes each fighter: `KageModel.Build(Look.Kage | Look.Ronin)`, one shared `MoveSet`, colours, a sort order, and a 1.04 scale for the Ronin. Health is 100 for both. The spec wants every opponent added as data, Kage's own build to become one entry with no visual or behaviour change, and JS health converted by one shared scale.

## Decision

### Shape

```csharp
[Flags] public enum CharacterFlags
{
    None = 0,
    Armour = 1,          // light hits don't flinch (Ironclad, Garan, Raikon) — SAN-102
    NoGrab = 2,          // throws fail (Raikon, Shiden) — SAN-102
    FrontalArmour = 4,   // hits from the front don't flinch (Shieldbearer) — SAN-98
    CanRageArt = 8,      // Special + Down with full Rage (Kage)
    CanThrowKunai = 16,  // Special + Down kunai (Kage) — SAN-88
}

public sealed class CharacterDef
{
    public string id;                 // "kage", "ronin", or the JS ENEMIES id: "order_footman", "kuroe", "raikon", ...
    public string displayName;        // HUD name ("Order Footman")
    public string title;              // HUD title ("Order of Silence", "the Lightning Ascendant")

    // stats
    public float jsHealth;            // JS health before the health scale (ADR-0010); Kage: from the rules and his gear
    public float healthMul = 1f;      // per-character pacing knob (SAN-110)
    public float defense;             // damage reduction 0..0.5 (JS `def`)
    public float damageMul = 1f;      // JS `dmg`
    public float speedMul = 1f;       // locomotion speed (walk, run, dash, jump distance) — JS `speed`
    public float moveRate = 1f;       // attack playback rate (Footman 0.9) — ADR-0006
    public float postureMax;          // posture bar size; larger for bosses — SAN-86
    public float mass = 1f;           // Rigidbody2D mass; knockback impulses are divided by it — SAN-68
    public float knockbackTaken = 1f; // extra multiplier on knockback received, on top of mass
    public float bodyScale = 1f;      // uniform scale of the whole fighter (Ronin 1.04, Raikon about 1.24)

    // look
    public string partSet;            // id of the part-set builder that draws this character
    public FighterColors colors;      // fx main, core, dark (trails, hit bursts, shadow glow)

    // fighting
    public string family;             // weapon family id (ADR-0006): "katana", "twin", "spear", "chain", "cleaver", "fists", "tachi"
    public string weaponId;           // JS weapon id for stats and drawing; null = the family's reference weapon
    public string aiProfile;          // "regular", "agile", "defensive", "ranged", "heavy", "boss"; null for the player
    public List<string> abilities;    // ability ids this character may use, in slot order (ADR-0008)
    public string bossId;             // BossDef id when the character has phases (ADR-0008); else null
    public CharacterFlags flags;

    // gameplay skeleton
    public List<HurtDef> hurt;        // hurt circles on the 20-bone skeleton; null = Kage's layout scaled by bodyScale
}

public struct FighterColors { public Color main, core, dark; }
```

- Health at fight start: `maxHealth = jsHealth × healthMul × HealthScale` (ADR-0010).
- Damage dealt and received: ADR-0006 (damage formula).
- **The gameplay skeleton is the same 20-bone skeleton for every character** (`B` enum, Kage's bind pivots scaled by `bodyScale`). Art never moves a gameplay joint. Hurt circles, hit points and push colliders belong to the gameplay skeleton, never to an art part.
- A **part set** builds the drawn parts on that skeleton. It returns a `RigDef` whose pivots and parents are Kage's (scaled), and whose parts, soft parts and colours are its own:

```csharp
public interface IPartSet
{
    string Id { get; }
    RigDef Build(CharacterDef c);
}
```

  Kage's part set wraps `KageModel.Build(Look.Kage)` and the Ronin's wraps `KageModel.Build(Look.Ronin)`, so neither changes visually. Opponents get new part sets in code (SAN-69 and later). Raikon's comes from his SVG (SAN-71). Gear visuals layer on top of Kage's part set (SAN-72, SAN-85).

### Registration

- One file per character: `Scripts/Character/Roster/<PascalId>.cs`, a static method returning its `CharacterDef`.
- One file per part set: `Scripts/Character/PartSets/<PascalId>PartSet.cs`.
- `CharacterCatalog.cs` holds the list, one line per character (`Add(OrderFootman.Def());`) and one per part set. `CharacterCatalog.Get(id)` looks entries up. A new character adds its own two files and two lines.
- Dev-only entries: `ronin` (today's recolour), `test_heavy` (the mass-3 dummy for SAN-68), `test_boss` (the two-phase Footman variant for SAN-102).

### Building a fighter

```csharp
Fighter FightBuilder.Spawn(CharacterDef def, int team, float x, float facing, IInputSource input, Loadout loadout = null);
```

- `Loadout` is the player's equipped weapon, helmet and armour with their levels, from the save (SAN-84). Foes use their `family` and `weaponId` at level 1.
- `FightBuilder` sets `rb.mass = mass`, the transform scale, health, multipliers and flags, builds the rig from the part set, picks the move set from the family, and builds the AI from `aiProfile` (SAN-95; until then today's `AIInputSource`).
- Flags are read only through `Fighter.Has(CharacterFlags f)`, so a boss phase can add or remove flags at run time (ADR-0008).

### Act I entries (stats from the JS data, raw `hp`)

| id | jsHealth | defense | damageMul | speedMul | family | aiProfile | flags | ticket |
|---|---|---|---|---|---|---|---|---|
| `kage` | from the rules (232 on a fresh save) | from gear | 1 | 1 | from loadout | — | CanRageArt, CanThrowKunai | SAN-66, SAN-84 |
| `order_footman` | 170 | 0.054 | 0.9 | 1 | katana (moveRate 0.9) | regular | — | SAN-69 |
| `hollow_shade` | 160 | 0.03 | 0.85 | 1 | katana | regular | — | SAN-76 |
| `hollow_tutorial` | 95 | 0 | 0.3 | 1 | katana | regular (slow, rare attacks) | — | SAN-76 |
| `order_runner` | 150 | 0.03 | 0.85 | 1.12 | twin | agile | — | SAN-97 |
| `order_shieldbearer` | 220 | 0.12 | 1 | 1 | spear (with shield) | defensive | FrontalArmour | SAN-98 |
| `order_chainthrower` | 150 | 0.04 | 0.85 | 1 | chain | ranged | — | SAN-99 |
| `order_ironclad` | 280 | 0.14 | 1.15 | 0.9 | cleaver | heavy | Armour (a Unity addition from the spec; the JS foe has no `heavyArmor`), mass about 2 | SAN-100 |
| `hollow_flicker` | 140 | 0.02 | 0.8 | 1.18 | twin | agile | — | SAN-101 |
| `kuroe` | 360 | 0.08 | 1.15 | 1 | spear (staff) | defensive | — | SAN-103 |
| `garan` | 395 | 0.1 | 1.1 | 0.92 | fists (heavy) | heavy | Armour (JS `heavyArmor`), heavy mass | SAN-104 |
| `hisame` | 375 | 0.04 | 0.95 | 1.16 | twin (fans) | agile | — | SAN-105 |
| `shiden` | 400 | 0.12 | 0.93 | 1 | spear | boss | NoGrab (JS `noGrab`) | SAN-106 |
| `raikon` | 640 | 0.16 | 1.1 | 0.9 | tachi | boss | Armour, NoGrab | SAN-107 |

These numbers are the values after every JS module has loaded (later JS modules override some of `10-data.js`: Garan's health is 420 in `10-data.js` and 395 at run time). They come from the generated JS tables (ADR-0010), never typed by hand. The table here is for orientation. `mass`, `postureMax`, `moveRate`, `knockbackTaken` and `hurt` are Unity values, authored in the character file.

## Notes from the implementation (SAN-66)

- Kage's roster class is `KageCharacter` (file `Roster/KageCharacter.cs`), because a class named `Kage` would hide the `Kage` namespace. Every other entry follows the `<PascalId>.Def()` pattern (`Ronin.Def()`, `TestHeavy.Def()`).
- `KageCharacter.Def(save)` takes his JS health from `GameRules.PlayerMaxHp(save)` and his defence from `GameRules.PlayerCombatStats(save)`; null means a fresh save (232, so 100 health).
- `CharacterDef.FromJs(enemyId)` fills a foe's JS stats from the generated tables (raw `hp`, `def`, `dmg`, `speed`, name, title, AI archetype, weapon, build scale, `heavyArmor` → Armour, `noGrab` → NoGrab). Character files add the Unity values. `CharacterCatalog.Get` returns a copy.
- `FightBuilder.Spawn(def, team, input)` and `Spawn(def, team, x, facing, input)` exist; the `Loadout` parameter is left to SAN-84. Draw order comes from the team (player side 1500, foes 1000) rather than character data. Every family gets Kage's katana move set until SAN-70.
- Applied now: health, mass, body scale, colours, part set, flags (`Fighter.Has`, `Fighter.Flags` is writable at run time), `speedMul` (walk, run, dash, jump and air speeds) and `knockbackTaken`. Stored on the fighter but not applied yet: `damageMul` and `defense` (ADR-0006 damage formula, SAN-70), `moveRate` (SAN-70), `postureMax` (SAN-86), `aiProfile` (SAN-95; the caller still passes the input source).
- A part set recolours its trim to the character's `colors.main` / `colors.dark` (`KageModel.Recolour`). Kage's SVG head strokes and his belt are fixed colours and don't follow.

## Notes from the implementation (SAN-72, helmet visuals)

- Gear layers over Kage's part set, not inside it. `CharacterDef.helmet` is the equipped helmet's JS id (`KageCharacter.Def(save)` fills it from `save.equipped.helmet`; a fresh save is `shiro_maki`). `KagePartSet.Build` builds Kage, recolours his trim, then calls `HelmetVisuals.Apply(rig, def.helmet)`, so a recoloured Kage keeps each helmet's own colours. SAN-85 (armour) adds the same kind of layer, see its notes below.
- A helmet with no drawing (null, the starter `shiro_maki`, every later-act helmet, an unknown id) changes nothing: the rig equals `KageModel.Build(Look.Kage)` part for part, so Kage stays pixel-identical. Shiro Maki's look is Kage's bare hair and his scarf, so it has no parts of its own.
- Helmet parts are on the head bone, in draw orders 83 to 95 (83 behind the head for hanging pieces, over the hair at 84, under the eye at 96); since the tier redesign (below) the neck wrap is on the chest at 102 and 103. Pivots, parents and hurt circles are never touched. The first pass's layers and spring-pivot tails (`HelmetArt`, `HelmetLooks`) were replaced by the tier redesign.
- Hair under a helmet (`HairUnderHelmet`), first pass (superseded by the tier redesign, below): hoods, caps and closed kabuto (Kuro Zukin, Kasumi Veil, Tetsu Kabuto, Hoshi Kabuto, Shika Kabuto, Utsuro Kabuto, Shikkō Zukin, Raijin Kabuto, Sumi Eboshi, Hisame Zukin) replace the hair part with a plain black head part named `Head`; the band and the open mask (Hachigane, Oni Menpō) keep `Head_Hair` as it is, still moving; the Sugegasa keeps only the hair below its brim. Rework after review (the owner: hair of Kage poked through some helmets): the first pass kept the hair under Tetsu, Shika and Utsuro Kabuto because the JS calls `drawKageHair` under them, but the JS draws that hair at 0.9 of the head and a kabuto covers all but a few tips at its rim, while the port's hair (full size, a fringe in front) stood well above the bowl, behind the neck guard and over the face below the visor. Those three now hide it like a hood. The JS armour hairstyles (`armory/hair.js`) are drawn by the JS on a bare head only (`kageHairStyle` needs `helmet === 'topknot'`), and the port draws none of them (see the SAN-85 notes), so no armour hair can show through a helmet. Tests: `HelmetHairTests`.
- First pass: art authored in the JS head frame as shape strings copied from the JS helmet drawings (`HelmetArt.cs`, `HelmetLooks.cs`, both removed by the tier redesign).

### Tier redesign (SAN-72 rework)

The owner's review: redesign every helmet on a five-tier rarity pattern (`docs/reference/helmet_tiers.png`: COMMON, EPIC, TROPHY, LEGENDARY, GOD MODE), with fresh designs in each tier's language, not copies of the five pictures, and hide the hair where it does not fit. Research and the per-helmet table: `docs/research/helmet-design.md`.

- **Tiers.** Common: Kuro Zukin, Sugegasa, Hachigane, Kasumi Veil (and the starter, which has no design). Epic: Tetsu Kabuto, Oni Menpō, Hoshi Kabuto. Trophy: Hisame Zukin, Sumi Eboshi. Legendary: Shika Kabuto, Shikkō Zukin. God Mode: Utsuro Kabuto, Raijin Kabuto. The mapping follows price and standing: the four cheapest shop helmets, the three JS "rare", two of the three JS "epic", then the 25-gem Utsuro and Raikon's Raijin (the highest health in Act I) at the top. The JS `Tier` field is unchanged; the design tier is the drawing's.
- **Data.** `HelmetDesign` (`Scripts/Character/Gear/HelmetDesign.cs`): id, JS look, `HelmetTier`, `HairUnderHelmet`, pieces. A `HelmetPiece` is an outline in reference px on a bone at a draw order, painted in a named tone of its tier's `HelmetPalette` (shade, mid, lit), as `Cel` (`OpponentKit.Cel` with the tier's back-light rim), `Flat` (one polygon, optionally see-through: glow lines, halos, studs) or `Cloth` (cel-shaded on `SoftChains` with a `SoftPresets` feel). `glow` above 1 makes the part HDR (it blooms in the game). The designs and the palettes are generated: `Tools/helmet_art/helmets.py` (the designs, by tier) and `kit.py` (shape kit, palettes, a preview renderer that paints like FighterRig, the C# exporter) write `HelmetDesigns.g.cs` (`python Tools/helmet_art/build.py export`; `check` verifies every outline is simple and triangulates). `HelmetVisuals.Apply` turns the pieces into parts named `Helmet_<look>_<piece>`.
- **Tier language.** Common: grey cloth, steel, straw, no glow. Epic: a black angular shell, violet blades, one glowing violet ring or seam. Trophy: white enamel and gold, laurel, a star medallion, white ribbons. Legendary: black lacquer kabuto, gold crest, red and gold mon, red lacing and tassels. God Mode: a black demonic shell, burning red horns and flames, a burning emblem, and a red glow behind his eye. Each tier's rim light is its colour. Height, piece count, motion and glow grow by tier (`HelmetDesignTests.TheTierLanguageHolds`).
- **Soft pieces** (knot tails, cord tails, ribbons, tassels, fire tails, Kasumi's drape and veil, the wrap's loose end) are `SoftKind.Chain` parts: they trail, sag, swing in attacks and take `FighterRig.wind`, like Raikon's tassels and the Hollows' robes. Hanging pieces behind the head are at 83.
- **The neck wrap.** As in the owner's sheet, every design but Kasumi Veil winds a tier-coloured wrap round the neck: a cel part on the chest at 103 (over the scarf wrap at 100, under the near arm at 104) and its loose end on a chain at 102. It stops short of the scarf's knot, so the knot and the two crimson scarf tails still stream behind. Kasumi's veil hangs from the head at 103 instead, below the eye. Nothing is ever drawn over the eye (`HelmetVisualTests.TheWhiteEyeStaysOnTopUnderEveryHelmet`).
- **Hair.** Hidden (replaced by the plain black `Head`) under every design but Hachigane, whose brow band keeps his spiky hair out. `HairUnderHelmet.BelowBrim` and its clipping are gone (the Sugegasa now has a head cloth under the hat).
- **Starter.** Shiro Maki ("Crimson Scarf") keeps no design: his bare hair and crimson scarf are its look and the baseline of the tiers. Later-act helmets have no design and leave him unchanged.
- **Armory.** The helmet tile pictures (`Resources/UI/Img/icon_h_<id>.png`) for the 14 Act I helmets are now rendered from the game's rig by `HelmetTierScreenshots` (on black and on white, the difference is the coverage), instead of the JS canvas exports. The live preview Kage shows the designs as they are.
- **Checks.** EditMode `HelmetDesignTests` (every helmet has a design in its tier, palettes per tier, tones in palette, simple outlines that triangulate, chains, orders, the hair rule, fallback), `HelmetVisualTests` and `HelmetHairTests` (rewritten). PlayMode `HelmetTierScreenshots` renders the review tiles and the Armory pictures, and its pixel check (`KAGE_PIXEL_DIR`) showed Kage bare-headed, in the starter, in every armour, and every other character pixel-identical before and after. Review images: `Screenshots/review/SAN-72/tiers/` (`tier_sheet.png`, `poses.png`, `pose_<pose>.png`, `bodies.png`, made from `tiles/` by `python Tools/helmet_art/build.py sheets`).

## Notes from the implementation (SAN-85, armour visuals)

- Same layering as the helmets: `CharacterDef.armor` is the equipped armour's JS id (`KageCharacter.Def(save)` fills it from `save.equipped.armor`). `KagePartSet.Build` builds Kage, recolours his trim, then calls `ArmorVisuals.Apply(rig, def.armor, ...)` and then `HelmetVisuals.Apply`; the armour goes first so the helmet's rim polygons take the armour's rim light. An armour with no drawing (null, the starter `tachibana_yoroi`, later-act or retired armours, an unknown id) changes nothing: the rig equals `KageModel.Build(Look.Kage)` part for part.
- Nine armours have a drawing: the seven shop armours (Ronin, Yami, Shinsen, Raikō, Kagehira, Yūrei, Mugen) and the two Act I trophies (Ikazuchi-dō, Shiden Yoroi). The ticket counts 11 ("starter, 8 shop, 2 rewards") but the data holds 8 non-retired armours that are not trophies **including** the starter, so there are 10 armours Kage can own by the end of Act I; the starter is Kage's own plain design and adds nothing.
- The seven shop armours are cuts of his own coat (JS `armory/_themes.js`): `ArmorLooks.Retone` recolours his trim, belt, coat cloth, rim light, hair's crimson strokes and scarf (`RigDef.scarfColor/scarfBack`, new `scarfEdge`), the hem gets teeth in the armour's kind (rags, shards, flames, tatters) and art along it (rope, tomoe, wards, lightning, flames, violet fire, spirit scrolls), plus a chest piece, a shoulder guard on the near upper arm, extra scarf tails (`RigDef.scarfStrands`, drawn by `ScarfRibbon` like the two main tails) and, for Mugen, a crescent halo behind the head. The two trophies add plates only (chest, sode, kusazuri); they keep his crimson.
- Pieces are authored in reference-image pixels (`ArmorArt`, the same numbers as `KageModel`). Chest pieces are parts on the chest bone at draw order 52 (over torso 44 and lapel 48, under belt 72 and scarf wrap 100), the sode is on `ArmNU` at 107 (over the upper arm and cuff, under the forearm 108), the halo is on the head at 82 (behind the hair at 84). Hem teeth, hem art and kusazuri are polygons appended to the coat's own cloth meshes, so they swing with the cloth. Nothing touches the skeleton, hurt circles, scarf anchors or coat cloth patches; no armour part is ever drawn over the eye.
- Not drawn: the JS hairstyle per armour (only the hair colour follows), glow/bloom on accents, the baked Godot flame, spirit and lightning sheets, and the Mugen card art (the code-drawn stand-in is used, as the JS does before the art has decoded). The far arm has no sode (it is behind the torso in this view).
- Rework after review (SAN-85 / SAN-90): the Mugen ring is the JS art's thin arch centred on the back of the head (a head radius behind the skull's centre, half a radius above it, 2.98 radii out, opening to the face; `ArmorLooks.HaloCentre`), carried by the head bone; `MugenRingTests` fit a circle to the posed ring on a real `FighterRig`. All ten trophies now have a drawing, each named for its JS look (`operative_vest`, `ledger_coat`, `gate_plate`, `conduit_harness`, `mei_en`, `kysmei`, `tyrant`, `shinsei` beside `half_do` and `vanguard_plate`; `ArmorLooksTrophies.cs`). Back-of-body pieces (Kudamaki's canister, Kurokumo's cape, Kysmei's rear shards) are chest parts at 46 beyond the torso's edge, Kysmei's standing collar is on the head at 80 and its skirt shards on the hips. Closed outlines go through `ArmorArt.Outline`: a closed `Line` is a ring polygon and the mesh builder fills it in. Not drawn: the JS glow pulses, the sash's and cape's cloth physics, and the drift of the Kysmei shards (static here).

## Notes from the implementation (SAN-69, opponent art pipeline and the Order Footman)

**How a new opponent is drawn.** One file for the character, one for the part set, two catalog lines (the Footman is the worked example):

1. `Roster/<PascalId>.cs`: `CharacterDef.FromJs("<js id>")` gives the JS stats, name, title, weapon, AI archetype and flags from the generated tables (never typed by hand). The file adds the Unity values: `partSet`, `colors`, `family`, `moveRate`, `mass`, `bodyScale`.
2. `PartSets/<PascalId>PartSet.cs`: `var d = OpponentKit.Skeleton(name);`, set `d.rimColor`/`d.bodyColor`, then add parts. `OpponentKit.Skeleton` is Kage's gameplay skeleton alone (`KageModel.Skeleton`, which `KageModel.Build` now starts from too): the 20 pivots and parents, the hurt circles, and the fist, hilt, foot and blade hit points. A part set never writes to any of them (a test compares them with Kage's), so hurt circles, hit points and push colliders stay on the gameplay skeleton. A character that needs different hurt circles sets `CharacterDef.hurt`, as before.
3. `CharacterCatalog`: `Add(<PascalId>.Def());` and `Add(new <PascalId>PartSet());`.
4. Tests to copy from `OrderFootmanTests` (EditMode: entry, stats, family, same skeleton, palette) and `OrderFootmanFightTests` (PlayMode: spawned from data, move rate, damage multiplier, a scripted K.O. with its ragdoll) and the review sheet `OrderFootmanScreenshots`.

**`OpponentKit`** (`Scripts/Character/OpponentKit.cs`) is small on purpose. Art is authored in Kage's reference pixels (x towards the front-left, y down, 400 px to a unit; its header lists every bone's pivot in them), so shapes can be read off a drawing of Kage's frame and `P`/`V` turn them into bind space. Shapes: `Tube` (a tapered limb piece with round ends), `Ellipse`, `Band` (a stroke along a polyline), `AlongSword` (a polygon in the blade's own axis). Adders: `Add` (one flat polygon with a rim outline), `AddMulti` (several polygons in one mesh, for a plate with its lacing and a lit edge, with an optional shifted outline as its rim), `AddSwinging` (a hanging piece with its own pivot, moved by the fighter's spring motion: skirt plates, hood tails, ears). `OpponentKit.Slot` names the depth slots Kage's own parts use (far arm 1, far leg 5 to 7, pelvis 8, torso 11, near leg 13 to 15, belt 18, head 21 to 24, near arm 26 to 28); a part set uses the same numbers, with `sub` 0 to 3 inside a slot, so Kage and every foe stack alike.

**Attach-to-bone rules.** Each piece rides the one bone it moves with (sleeves on the upper arm, guards on the forearm, hakama on the thigh and shin, kusazuri on the hips) and overlaps its neighbour at the joint, so a bent limb shows no gap. The far (katana) arm's parts and every part on the sword bone are `swordLayer` parts. The blade is drawn along the hit line (`bladeBaseLocal` to `bladeTipLocal`), so what you see is what hits. Hanging pieces are `AddSwinging`; a foe has no scarf (`d.scarf = false`) and no cloth coat, since the cloth, hair field and scarf ribbon belong to Kage's rig and the Raikon ticket. A part set sets its fighter's effect colours in the character's `colors`, and its own palette in a shared static class when several foes share it (`OrderPalette`, for the five Order soldiers).

**The Footman.** `order_footman`: 170 JS health (73.28 through the health scale), defence 0.054, damage ×0.9, weapon `cutter_blade` (JS base Kagetsu, so the katana family), AI archetype regular, no flags, `moveRate = 0.9`. He plays the katana move set at a playback rate of 0.9 through `Fighter.PlaybackRate` (ADR-0006), the same `MoveSet` object Kage uses (a test checks they are the same instance), with no path of his own. His iai takes about 1/0.9 as many frames as Kage's, and a Footman iai does `round(9 × 0.9)` = 8 where a Ronin's does 9 (damage is rounded once, so very small hits show the multiplier badly). The look is the JS Order soldier: a cloth hood with two tall pointed fox ears and a hood tail, a long-muzzled pale fox mask with a cold eye, a slate-lacquer dō laced in muted crimson, wide box-like sode on both shoulders (the near one carries the Order mark, a barred circle), kote on the forearms, a kusazuri skirt of swinging plates, wide hakama over silver suneate, an empty saya out behind the hip and a straight cutter blade. Palette: cool slate, silver and a pale mask, a cold blue-grey rim light, crimson only in the lacing; Kage is warm black with crimson. Silhouette: blocky and bulky at the shoulders and hips, no streaming scarf or coat tails, fox ears and a saya where Kage has spiky hair and coat tails.

**Dev arena.** `KageGame.foeId` (inspector) names the opponent, default `ronin`; F5 cycles `KageGame.DevFoeIds()` (every foe in the catalog that has an AI profile, not the `test_` dummies) and reloads the arena with the choice (`KageGame.NextFoeId`, used once). The HUD name, the "X WINS" banner and the foe's shadow glow follow the character. Kage vs Footman is `foeId = order_footman` or F5 once.

**Tests.** `OrderFootmanTests` (EditMode), `OrderFootmanFightTests` and `DevArenaTests` (PlayMode), the review sheet in `Screenshots/review/SAN-69/` (`kage_vs_footman_sheet.png`: idle, walk, attack and hit with Kage beside him, plus `footman_closeup`, `footman_head`, `footman_ko_air` and `footman_ko`).

## Notes from the implementation (SAN-76, the Hollow Shade and the tutorial Hollow)

- **Two entries, one palette, one body.** `hollow_shade` and `hollow_tutorial` follow the SAN-69 recipe (`Roster/HollowShade.cs`, `Roster/HollowTutorial.cs`, `PartSets/HollowShadePartSet.cs`, `PartSets/HollowTutorialPartSet.cs`, two catalog lines each kind). `PartSets/HollowPalette.cs` is the shared colour set (violet-black cloth, ash-violet cracked mask lacquer, bandage wraps, a violet glow along breaks, a cold white-violet eye, a violet back-light), the Hollow counterpart of `OrderPalette`. SAN-101 Flicker and SAN-112 Husk reuse it. `PartSets/HollowBody.cs` holds the pieces every Hollow shares (frayed sleeves over wrapped forearms, ragged hakama over wrapped shins, dark hands, the saya, the echo of a katana with a glowing edge, `Torn` for ragged hems, `Straw`); a Hollow's part set adds its own head, torso dressing and skirt. Both use the katana family (`cutter_blade`) and the shared `MoveSet` instance.
- **Looks.** The Shade (JS `hollow_shade`): a ragged cowl with a torn tail, a crown of four floating mask shards that drift, a gaunt ash-violet face with a long nose and a parted mouth under a cracked half-mask, a torn haori with a violet-lit rip across the chest, a ragged sash and a skirt of torn swinging panels to the calf. The tutorial Hollow (JS `hollow_tutorial`): the same body dressed as a training husk, a stitched sack hood with straw bursting from its crown, a plank practice mask nailed over the eyes, rope bindings crossed over the chest and a rope sash, straw at the cuffs and a short rag skirt. Neither has a scarf or a coat, and neither is Order slate or Kage crimson. The JS glitch, offset ghost and memory-ash dissolve are fight-draw effects (not part-set art) and are not ported here.
- **AI knob: `CharacterDef.attackRate`** (0..1, default 1). It is the share of the attacks the AI has chosen that it actually throws. `FightBuilder.Spawn` hands it to an `AIInputSource` input through `AIInputSource.Configure(def)` (the harness does the same for a scripted foe's fallback AI). In `AIInputSource` it does three things: after each `Decide` the plan is checked, and if it contains an attack button it is kept with probability `attackRate`, else replaced by a short approach (when far) or a pause; combo continuation is scaled by it; a punish after a guarded hit is scaled by it. At 1 nothing changes (the baseline replay is unaffected). It is a single data-driven scalar on top of the profile; SAN-95's per-profile tuning (preferred range, guard and parry rate, reaction frames, combo length) should build the profile table and may fold this in as the profile's `aggr`.
- **The tutorial Hollow's numbers.** JS `hollow_tutorial`: 95 JS health (40.95 Unity health), defence 0, damage 0.3. Unity values from its file: `moveRate = 0.75` (slow attacks: its moves take 1/0.75 of Kage's frames) and `attackRate = 0.3` (rare attacks). The Shade is the Footman recipe with no move rate change (1.0) and the regular profile as is. Colours (`FighterColors`): Hollow glow `#B9A8FF`, eye, deep violet.
- **Dev arena.** F5 now also cycles to `hollow_shade` and `hollow_tutorial` (`DevFoeIds()` lists every foe in the catalog).
- **Tests.** `HollowTests` (EditMode: entries, JS stats, katana family, part sets, skeleton untouched, shared palette), `HollowFightTests` (PlayMode: spawned from data on the shared move set, slow attack playback, rare attacks measured over a 1800-frame AI fight, a scripted K.O. of each), `HollowScreenshots` and the review images in `Screenshots/review/SAN-76/` (`kage_vs_hollow_shade_sheet.png`, `kage_vs_hollow_tutorial_sheet.png`, `footman_shade_tutorial_sheet.png`).
## Notes from the implementation (SAN-71, Raikon art)

*Superseded by "Implementation notes (SAN-71 SVG redesign)" at the end: Raikon is now an SVG rig. The importer, regions, fills, bake and soft-chain feels below describe the retired first cut; the source art, the fit, the arm swap, the tachi's placement and the soft chains' mechanics still hold.*

- **Source art:** the owner's files, untouched, are in `Tools/raikon_art/original/` (`Raikon.svg`, 976 × 1230, a 7-colour cutout trace with one even-odd compound path per colour; `Raikon.png`, the painting). The game reads a byte copy, `Assets/Kage/Resources/Art/Raikon/Raikon.svg.bytes` (renamed so Unity loads it as text; an EditMode test checks it matches the original).
- **Importer:** `SvgArt` (`Scripts/Character/SvgArt.cs`) reads a colour trace with Unity's built-in vector graphics module (`com.unity.modules.vectorgraphics`, `SVGParser`), one layer per colour in paint order. `Cut(region, cell)` clips every colour to a region (polygons in SVG px, even-odd) and tessellates it; with a cell size it cuts cell by cell so no triangle is bigger than a cell (soft parts bend smoothly). The trace's first colour (#010000) is both the canvas and the figure's ink, so it is set as the `backgroundLayer`: a cut paints the whole region in it and the other colours over it (the same picture, without tessellating the canvas's many-holed shape).
- **Regions:** because the black outline and the black face merge with the black canvas, the figure's outline is not in the file. `Tools/raikon_art/regions.py` (Python 3, numpy, opencv) finds it (every non-black colour, closed by 5 px, its enclosed black filled except the cape's tears, plus hand-marked black areas: face, neck; grown by a 2 px outline) and writes `Scripts/Character/PartSets/RaikonRegions.g.cs`: each part's region is its rough polygon inside that silhouette minus the parts that cover it in the painting (so nothing is drawn twice and moves as a ghost), with holes joined in by zero-width cuts. It also writes **fills**: flat underlays in the trace's colours for what the painting hides behind moving parts (torso behind the arms, hips and thighs under the skirt, the hilt under the hand), and a **patch** that paints the skirt back under the painted tachi by continuing each skirt column down. Previews: `Screenshots/review/SAN-71/regions.png` (each part tinted, magenta = figure no part draws) and `regions_patches.png`.
- **Bake:** cutting the whole figure takes about 4 s, so the Editor bakes it: `Kage > Bake Raikon Art` (or `-executeMethod KageEditor.RaikonArtBake.Bake`) writes every piece to `Resources/Art/Raikon/RaikonCut.bytes` (about 0.8 MB) with a hash of the SVG, the regions and a cut version. A spawn reads the bake (first build about 0.15 s, then cached); a stale bake is ignored with a warning (the SVG is cut instead) and the EditMode test `TheBakedCutIsUpToDateWithTheSvgAndTheRegions` fails until it is re-baked. Re-run `regions.py` and re-bake after changing the regions; bump `RaikonModel.CutVersion` when the cutting code changes.
- **On the skeleton:** `RaikonPartSet` → `RaikonModel.Build()` starts from `KageModel.Skeleton("RAIKON")` (SAN-69: the shared gameplay skeleton alone, its pivots, parents, hurt circles, hit points and blade values) (art never moves a gameplay joint); only the parts are Raikon's, at Kage's bind size (the 1.24 body scale is the spawn's transform scale, from `CharacterDef.FromJs`). The trunk (head, hat, ponytail, fur, torso, rope, cape, skirt) is one fit of the painting onto Kage's reference: 0.688 Kage reference px per SVG px, Raikon's soles (SVG y 1195) on the ground, his hips (SVG x 501) over Kage's; with that fit the painted shoulder, elbow, hand, knee and ankle land within a few pixels of the bones. Each limb segment is mapped so its drawn joints land exactly on the bones' pivots (rotation, and a stretch along the segment); hands and boots follow their forearm or shin. The drawn joints are constants at the top of `RaikonModel`.
- **Which arm holds the tachi:** the painting holds it in the near hand, the shared skeleton in the far hand (`Sword` hangs from `HandF`). So the far arm is the sword arm drawn from the painted near arm (sleeve with the elbow wrap, gauntlet, gripping hand), one step darker along the trace's palette (the reds a little deeper); the near arm keeps the painted shoulder plate (sode with the tomoe), sleeve and gauntlet and gets the painted far fist. The far leg is the painted near leg (with the buckle) one step darker. The painted far forearm and far boot are not used.
- **The tachi** is two parts on the `Sword` bone (`Tachi_Hilt`, `Tachi_Blade` with glow 1.7) plus a red grip underlay, all `swordLayer`, placed by the grip point in the painted hand on the sword bone's pivot and the blade along Kage's `bladeBindAngle`. The gripping hand (`HandF`) is placed with the tachi, so the hand closes around the hilt. The drawn tachi reaches about 0.84 bind units from the grip (Kagetsu's blade: 0.80); the Blade hit points still come from Kage's blade values and the family's `bladeLength` (SAN-81 sets the tachi's).
- **Parts and depth** (order slot ×4, Kage's slots so `swordLayer` works the same): Cape 0 and Ponytail 0 (behind everything), far arm 1–2, tachi and sword hand 4, far leg 5–7, hips fill 8, torso fill 10, Torso 11, near leg 13–15 (thigh is a fill), skirt patch and Skirt 16, Rope 18, Head and glowing Eye 21, Fur 22, Hat and three Tassels 24, near arm 26–28 (sleeve, sode over it, gauntlet, fist). Every cut part is one indexed mesh in bind space (`PartDef.mesh`, now honoured by `FighterRig`), no rim polygons: the trace carries its own outline.
- **Soft parts:** cape, ponytail, the three hat tassels and the fur mantle are `SoftKind.Chain` parts: `SoftChains` (in `SoftBody.cs`) runs verlet spring chains pinned at their roots to the bone (inertia, gravity, air drag trailing the body's motion and a breeze behind the fighter, shape matching that weakens to the tip, ground clamp, teleport reset); every vertex is bound to its two nearest chains and follows their position and turn. Each part has its own `ChainFeel` (stiffness at root and tip, drag, gravity, breeze); the chains are drawn polylines in SVG px in `RaikonModel`. `FighterRig` ticks them with the hair and coat and resets them in `ResetSoft`.
- **Shared files touched:** `FighterRig` (honours `PartDef.mesh`; glow on mesh/poly parts; builds, ticks and resets `SoftChains`), `RigDef` (`SoftKind.Chain`, `ChainFeel`, `PartDef.chains`, `PartDef.chainFeel`), `SoftBody.cs` (`SoftChains`), `MeshKit.Shifted`, `CharacterCatalog` (two lines). Kage and the Ronin draw exactly as before.
- **Roster:** `Roster/Raikon.cs` is `CharacterDef.FromJs("raikon")` with his part set, family `tachi` and effect colours. Mass, posture, moves, phases and abilities are SAN-81 / SAN-107; until the tachi family is registered he fights with the katana set (`EquipWeapon` logs a warning).
- **Review images:** `Screenshots/review/SAN-71/raikon_side_by_side.png` (painting, bind pose, fight idle), `raikon_poses.png`, `raikon_soft_motion.png`, tiles in `tiles/` (`RaikonScreenshots`, needs a GPU).

## Notes from the implementation (SAN-84, gear and weapon levels in combat)

- **The loadout is the save.** There is no `Loadout` class: `KageCharacter.Def(save)` already read the save's equipped weapon, helmet and armour and their levels, and SAN-84 completes it, so a `Loadout` parameter on `FightBuilder.Spawn` is not needed (the line in "Building a fighter" above is superseded). A save gives a `CharacterDef` with: `jsHealth = GameRules.PlayerMaxHp(save)` (220 + 4 per player level + the gear's health at its levels), `defense = PlayerCombatStats(save).Defense` (helmet plus armour, 0.012 more per level, capped at 0.5), `weaponId` and `weaponLevel` (the family follows the weapon), `helmet` and `armor` (the drawn gear layers of SAN-72 and SAN-85), `damageMul`, `speedMul` and `perks`. Health goes through the shared health scale (`MaxHealth()`, ADR-0010). A fresh save, or null, is the starter loadout: 232 JS health (100 Unity health), defence 0.03, no perks, every multiplier 1. It builds the same Kage the baseline replay was recorded with.
- **Where fights read the save.** The campaign: `StageWorld.Create(parent, stageId, save)` builds Kage from `FlowController.Save` (`GameFlow` passes it). The world behind the loadout screen is rebuilt when the save's loadout changes (`KageCharacter.LoadoutKey(save)` is stored on the world as `StageWorld.LoadoutKey` and compared on every screen change), so picking another weapon on the loadout screen is the weapon he fights with. The dev arena: `KageGame` builds Kage from `KageGame.SaveSource()`, or from the save file on disk (`SaveStore.AtPersistentDataPath().Load()`) when it is null. The PlayMode suite sets `SaveSource` to a fresh save for every test (`PlayModeSaveIsolation`, a `[SetUpFixture]`), so no test reads the machine's real save. The headless harness and the baseline replay still use the catalog entry (a fresh save).
- **Kage's own damage and speed.** JS `makePlayer` gives the player `dmgMul = (1 + 0.012 x (level - 1)) x (1 + perks.dmg)` and `speedMul = 1 + perks.speed`. `PlayerGearStats.DamageMul` is the first (a Unity addition to the rules port), `GearPerks.SpeedMul` the second, and `KageCharacter.Def` puts them in `CharacterDef.damageMul` and `speedMul`. The weapon's own multiplier is separate (`WeaponStats.Combat`, ADR-0006). Both are 1 at level 1 with no perks.
- **Perks.** The JS data has eight perk ids, on the helmets and armours (`GearPerks.Keys`; a test checks the data has no other). `CharacterDef.perks` holds their sum (`GearPerks`, raw JS values), `Fighter.Gear.cs` applies them. Perks apply as the JS rules say, in Unity's own units:

  | Perk | JS effect | In Unity | State |
  |---|---|---|---|
  | `dmg` | +x % damage | folded into `damageMul` (above) | applies now |
  | `speed` | +x % locomotion speed (negative on the heavy enemy armours) | folded into `speedMul` (walk, run, dash, jump) | applies now |
  | `rage` | +x % Rage gain | `Fighter.RageMul`: both Rage gains (dealing a hit, taking a hit) x (1 + rage) | applies now |
  | `stunRes` | hit-stun x (1 - 0.4x), hard stun x (1 - x), guard-break, parry and deflect stun x (1 - 0.5x) | `Fighter.ScaleStun(frames, k)`; hit-stun uses it now (`HitStunResist` 0.4) | hit-stun applies now; the other stuns are hooked (`BreakStunResist`, `HardStunResist`) for SAN-86 (posture break) and SAN-91 (parry) to call |
  | `dash` | dash invulnerability 0.17 s + 0.5 s x x, dash cool-down x (1 - x) | `Fighter.DashBonus`: every dash gets `round(x * 30)` more untouchable frames (the dash state, and after the roll's and the backflip's own frames), the dash cool-down is x (1 - x) | applies now |
  | `ammo` | +n kunai | `kunaiStart = 5 + n` (`GearPerks.KunaiStart`) | applies now |
  | `kunai` | +x % kunai damage | `Fighter.KunaiMul`, through `ProjectileDamageMul` into `DamageCalc.For(..., extraMul)` | applies now |
  | `blockRage` | blocking builds (1 + x) x the Rage | `Fighter.BlockRageMul` and `Fighter.BlockRageGain(damage)` (JS `damage x 0.4 x rageMul x (1 + blockRage)`) | hooked: Unity's block gives no Rage yet, so the guard ticket adds `meter += BlockRageGain(damage)` in `TryGuard` and the Kagehira Armor and Shikko Garb perks start to work |

  Which items carry which perk is in the data (`JsTables.Helmets`, `JsTables.Armors`), never retyped here.
- **Not built here, by design.** Guard break, posture, parry, perfect dodge and deflect don't exist yet (SAN-86, SAN-91, SAN-92). The perks that touch them (`stunRes`'s other stuns, `dash`'s perfect-dodge window, `blockRage`) have the named hooks above and nothing else. A stagger from a counter or the rage art isn't scaled by stun resist (the JS game's `m.stun` is a different effect).
- **Defaults change nothing.** Every perk value is 0 or 1 on a fighter with no perks: `ScaleStun` returns its input, `DamageCalc`'s `extraMul` is 1f (an exact float multiply), the dash invulnerability and cool-down code take their old branch, so Kage in the starter loadout and every foe fight exactly as before (the baseline replay holds).
- **Tests.** `GearLoadoutTests` (EditMode: starter loadout equals the catalog entry; health, defence, the 0.5 cap, level damage, perks summed, every JS perk id known, against values worked by hand from the JS data and `Kage.Rules`), `GearInCombatTests` (PlayMode harness: health and defence on the fighter, upgraded armour lowers the damage taken, a level 8 weapon and the damage perk raise damage dealt, rage, stun resist, ammo and kunai, speed and dash perks, the hooks), `GearLoadoutFlowTests` (the campaign tableau and stage, picking a weapon at the loadout screen, the dev arena), review image `Screenshots/review/SAN-84/kage_from_save_loadouts.png` (`GearLoadoutScreenshots`: three saves, Kage drawn and labelled with what the rules give). `ScriptedInput.Add` gained `dash:` for the tests.

## Notes from the implementation (SAN-86, posture)

- `postureMax` is applied: `Fighter.PostureMax` is the posture bar (`Fighter.Posture`, Fighter.Posture.cs). The default is now 60 (was a placeholder 100): with the guard factor of 1.5 about two blocked strings break a regular guard, the JS game's pace. Kage, the Ronin, the Footman and the Hollows use the default; `Raikon.Def()` sets 100 (a boss's bar is larger; SAN-107 tunes it, and each sub-boss ticket sets its own). Tests can change `Fighter.PostureMax` at run time.
- The other posture numbers are not character data: they are in `Resources/Tuning/guard.json` (ADR-0004, SAN-86 notes).
- SAN-84's two guard hooks are wired: `TryGuard` adds `meter += BlockRageGain(damage)` (so a block now builds Rage, damage × 0.4 × the Rage perk × (1 + blockRage), as in the JS game; this is not 0 without a perk), and the guard-break's helpless time is `ScaleStun(breakFrames, BreakStunResist)` (unchanged with no stunRes perk). Tests: `GuardPostureTests.A_block_builds_Rage_and_the_blockRage_perk_multiplies_it`, `The_stunRes_perk_shortens_the_guard_break`.

## Consequences

- Every opponent is one character file, one part-set file and two catalog lines, so opponent tickets don't collide.
- Kage stays one entry. His health and defence come from the rules and his save, so gear changes him with no special case.
- "Speed" now means two separate things: `speedMul` (moving around) and `moveRate` (how fast attacks play). The glossary keeps them apart.

## Notes from the implementation (SAN-91)

- **AI knob: `CharacterDef.parryRate`** (0..1, default 0): the share of the attacks the AI guards that it tries to parry. `AIInputSource.Configure(def)` takes it; at 0 nothing changes. SAN-95's profile table should set it from the JS `parry` column. See ADR-0004's SAN-91 notes.
- **stunRes and the parry.** A parried fighter's stagger goes through `ScaleStun(frames, ParryTuning.stunResistFactor)` (0.5, the `BreakStunResist` curve of the JS `parried`), so `HardStunResist` stays unused by the parry. The factor is data (`parry.json`).

## Notes from the implementation (SAN-95: AI profiles)

**The data shape.** `AiProfile` (`Scripts/Combat/AiProfile.cs`) has one field per JS `AI_PROFILES` column, in Unity units: `preferredRange` (JS `pref` px / 115 = metres), `aggression` (`aggr`), `guardRate` (`block`), `parryRate` (`parry`), `dodgeRate`, `jumpRate`, `punishRate`, `grabRate`, `kunaiRate`, `dashInRate`, `reactMinFrames` / `reactMaxFrames` (`react` seconds × 60), `comboMin` / `comboMax`, and the `ranged` and `heavy` flags (`cautious` marks the defensive and ranged archetypes, whose aggression falls instead of rising when hurt). They are built from `JsTables.AiProfiles`, the generated table (ADR-0010), by `AiProfile.FromJs`; nothing is typed by hand. `AiProfiles.Get(id)` returns a copy (null for null or an unknown id). The numbers the table doesn't have (spacing band, reaction scale, ranges, the stage-level curve, the JS-derived constants such as the 0.35 low-health line) are `AiTuning`, `Resources/Tuning/ai.json` (ADR-0001).

| Profile | preferred range | aggression | guard (block) | parry | dodge | jump | punish | grab | kunai | dashIn | react (frames, JS timing) | combo |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| regular | 1.09 m | 0.55 | 0.38 | 0.06 | 0.12 | 0.05 | 0.55 | 0.05 | 0.05 | 0 | 14 to 24 | 1 to 3 |
| agile | 1.39 m | 0.62 | 0.18 | 0.05 | 0.38 | 0.20 | 0.70 | 0.03 | 0.06 | 0.35 | 9 to 16 | 2 to 4 |
| defensive | 1.00 m | 0.34 | 0.62 | 0.16 | 0.08 | 0.02 | 0.85 | 0.20 | 0.02 | 0 | 10 to 18 | 1 to 2 |
| ranged | 3.74 m | 0.42 | 0.32 | 0.04 | 0.28 | 0.10 | 0.45 | 0.02 | 0.55 | 0 | 12 to 20 | 1 to 2 |
| heavy | 1.22 m | 0.50 | 0.12 | 0 | 0.03 | 0 | 0.45 | 0 | 0 | 0 | 19 to 31 | 1 to 2 |
| boss | 1.30 m | 0.62 | 0.42 | 0.12 | 0.18 | 0.06 | 0.78 | 0.06 | 0.05 | 0 | 10 to 17 | 2 to 4 |

**Selection.** `CharacterDef.aiProfile` names the profile. `CharacterDef.FromJs` already copies the foe's JS archetype into it, so a foe ticket gets its profile by calling `FromJs` and writing nothing about the AI: the Footman, Shade and tutorial Hollow are `regular`, Runner and Flicker `agile`, Kuroe, the Shieldbearer `defensive`, the Chainthrower `ranged`, Garan, the Ironclad and the Husk `heavy`, Hisame `agile`, Shiden and Raikon `boss`. `FightBuilder.Spawn` hands the character to `AIInputSource.Configure(def)`, which loads the profile (`AIInputSource.profile`), `attackRate`, and the parry rate: `CharacterDef.parryRate` replaces the profile's when it is above 0, else the profile's is used. A foe whose weaker or stronger behaviour is a matter of taste sets the per-character knobs (`attackRate`, `parryRate`, `moveRate`) on top, as the tutorial Hollow does; changing the archetype is changing `aiProfile`. `AIInputSource.SetProfile` swaps the profile at run time (a boss phase, ADR-0008).

**The Ronin keeps the old AI.** The dev arena's Ronin and the campaign's placeholder foe have `aiProfile = "legacy"` (`AiProfiles.LegacyId`), a profile with `legacy = true` and no numbers: `AIInputSource` then runs the pre-SAN-95 code path untouched, draw for draw, so the baseline replay (which fights the Ronin) is unchanged and needed no re-recording. `TestHeavy` is a Ronin and inherits it. The rest of the roster is profiled.

**What a profile does** (`AIInputSource.Profile.cs`; the utility AI, the plan queue and `WaitTurn` are the same as before; everything is a button press in `FrameInput` and every roll is on the "ai/&lt;FighterId&gt;" stream):

- *Answering an attack.* Once per opponent attack that is about to reach it, the AI rolls parry (`parryRate`), then guard (`guardRate`), then a backflip dodge (`dodgeRate`), as the JS `respond` does; an unblockable blow or a throw is never guarded and raises the dodge chance by 0.3. Otherwise it may trade with a quick punch (aggression × 0.4 × `attackRate`). It does so after its reaction delay, counted from the start of the attack: `react` frames × `AiTuning.reactionScale` (0.5, because Unity's moves start up in 6 to 20 frames where the JS ones take a quarter of a second) ÷ the stage-level scale. A quick attack can therefore land before a slow-reacting foe's guard is up. A parry attempt holds the Guard press until the blow is half a parry window away (SAN-91). The guard is the Guard input, with Down for a low blow.
- *Where it stands.* It walks in to `preferredRange` and holds; nearer than the range minus `spacingBand` (0.2 m) it backs off a step; a `ranged` foe backs off from anything nearer than its range minus 1.04 m (never into a wall) and shoots its Special from beyond 1.9 m at `kunaiRate`.
- *Pace.* In range it attacks with chance `aggression` per decision (the stage level and low health move it; an aggressive boss phase adds 0.2), else grabs (`grabRate`, close, more often against a guard), strafes, or holds its guard for a moment when `guardRate` is over 0.3. Out of range it dashes in (`dashInRate`), jumps in (`jumpRate`), throws its Special (`kunaiRate`), lunges with the shadow step (`lungeShare` of its aggression, under 2 m), else walks in. A `heavy` profile decides 1.3 times slower. `punishRate` is the chance it counters an opening (the opponent's attack finished its last hit and is recovering) or follows up after a guarded blow. A string is `comboMin` to `comboMax` attacks, drawn per string: the first, then up to n - 1 chained on hit (`AiTuning.comboFollow`).
- *Abilities.* ADR-0004's SAN-95 notes: `FrameInput.ability`, `AbilityDef`.
- *Stage level.* `AIInputSource.level` (0.6 default) scales aggression, guard, parry and punish by `0.7 + 0.5 × level` (1 at the default) and reaction by the inverse, the JS stage curve in miniature; rates cap at 0.95.

**Not ported.** The JS fighting styles (`FIGHT_STYLES`), role multipliers (rank and file are slower and tamer), difficulty modes, the family forms and the spam read, the boss abilities' scripts (ADR-0008) and the mirror, Takashi's pattern read: they are boss and foe tickets. The profile carries what a foe ticket needs to say "this one is agile".

**Measured** (`ProfileAiBehaviourTests`, the Footman, fixed seeds; the test logs these as "SAN-95 ..." lines): raised-guard rate over 80 to 130 attacks thrown at a foe that could answer (profile parry + guard in brackets): regular 0.45 (0.44), agile 0.17 to 0.30 (0.23), defensive 0.75 (0.78), heavy 0.12 to 0.14 (0.12); parry attempts: defensive 0.17 to 0.21 (0.16), heavy 0; a guard comes about 7 frames after the attack starts for agile and about 13 for heavy; attacks a second against a Kage who stands still, mean of five seeds: regular 0.78, defensive 0.59, agile 0.91; standing distance with the profile's attacking switched off (so only its range shows): regular 1.07 m, agile 1.39 m, defensive 1.02 m (preferred ranges 1.09, 1.39, 1.00). With its attacks on, the three stand about as far apart as each other (1.14 to 1.17 m), because the lunges and chains close the gap: the distance difference only shows between attacks.

**Group fights.** The profiled Footman attacks from range with a lunge (the shadow step, `AiTuning.lungeRange` 2.0 m, `lungeShare` 0.35 of its aggression), as the legacy AI did from 1.25 to 2.2 m; without it a foe pressed behind another in a group fight could not attack, and the camera and engagement tests (`EngagementCameraTests`, `EngagementTests`) saw fewer engaged foes. `No_more_than_two_foes_attack_on_any_frame` moved from seed 83 to 86 (on seeds 83 and 89 the foes fall, under Kage's 4x damage, before two of them happen to attack together; 8 of 10 seeds from 83 do) and `A_group_fight_is_identical_every_run` from seed 8383 to 8384: 8383 alone of 16 seeds (8380 to 8395) replays differently within a session with the profiled AI, the multi-body physics-order case that test's own comment names; the other 15 replay identically three times over.

**Tests.** `ProfileAiProfileTests` (EditMode: every archetype's numbers against the JS table as literals, selection through character data, the Ronin on the legacy profile, `ai.json` matches its defaults), `ProfileAiBehaviourTests`, `ProfileAiAbilityTests`, `ProfileAiBotFightTests` (PlayMode: a scripted bot Kage against the Footman with each profile and the legacy one, real health, seed 95: each ends in a knock-out inside 7200 frames and never goes 600 frames without a hit, block or parry; the same seed gives the same fight).

## Notes from the implementation (SAN-102: boss data and flags)

- `CharacterDef.bossId` is set by `CharacterDef.FromJs` for every JS enemy with phases (Kuroe, Garan, Hisame, Shiden, Raikon), and by hand for a variant (the test boss `footman_boss`). `FightBuilder.Spawn` attaches a `PhaseController` (ADR-0008 notes) when it is set, and throws if `BossCatalog` has no data for it. A character without a `bossId` has `Fighter.Phases == null`.
- `CharacterDef.flags` stay the character's own; a phase's `addFlags`/`removeFlags` are applied to a copy in `Fighter.Flags` (`(def.flags | add) & ~remove`), so a phase never leaks into the next fight.
- The test boss, `FootmanBoss.Def()`: the Order Footman with id `footman_boss`, name "Footman Captain", two abilities in slot order, and `bossId` set. It is not a test dummy (no `test_` prefix), so the dev arena's F5 cycle includes it.

## Notes from the implementation (SAN-97/99/101)

- **Three foes, the SAN-69 recipe.** `order_runner` (150 JS health, defence 0.03, damage 0.85, speed 1.12, scale 0.97, agile, twin family, `shadow_daggers`), `order_chainthrower` (150, 0.04, 0.85, speed 1, scale 0.98, ranged, chain family, `chain_hook`) and `hollow_flicker` (140, 0.02, 0.8, speed 1.18, scale 0.94, agile, twin, `shadow_daggers`). Roster files `Roster/OrderRunner.cs`, `OrderChainthrower.cs`, `HollowFlicker.cs` call `CharacterDef.FromJs` and add only `partSet`, `colors` and `family` (no `moveRate`: the family sets are authored at rate 1). Part sets `OrderRunnerPartSet`, `OrderChainthrowerPartSet`, `HollowFlickerPartSet`; six catalog lines. `StretchSession.ResolveFoe` now returns each real character, with no Footman fallback.
- **No weapon in the part set.** A twin or chain character's part set draws no blade: `TwinVisual` / `ChainVisual` attach the daggers or the sickle and chain when the fighter takes its weapon, and hide the part set's katana if there is one. `OrderBody` (new, `PartSets/OrderBody.cs`) holds the pieces the light-armoured Order soldiers share (slate sleeves over kote with no sode, hakama over suneate, pelvis, the gi torso), with a width factor so the Runner is lean; the Footman keeps his own heavier pieces. `OrderPalette` gained `Skin` and `SkinShade` for a bare face. The Hollow Flicker uses `HollowBody`'s arms and legs and its own pelvis, torso, tatters and head.
- **Looks.** Runner: a peaked shinobi hood with two long streaming tails, a bare narrow face under a veil, a light hara-ate with a silver chest strap, a two-plate kusazuri, two dagger sheaths crossed on the back. Chainthrower: a wide flat jingasa with crimson cords over a hood, a pale fox menpo, a hara-ate with a chain bandolier and a coil of chain at the belt. Flicker: slim and hunched, a hood pulled forward into a beak with two violet-edged tails, a thin cracked visor, a torso bound in wraps, one long split tatter behind and no saya, crown or haori.
- **Tests.** EditMode `OrderRunnerTests`, `OrderChainthrowerTests`, `HollowFlickerTests` (entry and JS stats, family and profile, part set, Kage's skeleton untouched, distinct from the Footman or the Shade, palette). PlayMode `OrderRunnerFightTests`, `OrderChainthrowerFightTests`, `HollowFlickerFightTests` (spawned from data, stretch resolution, dev arena cycle, scripted K.O., a `KageBot` fight to a knock-out with no quiet stretch over 600 frames; the Runner is faster on the ground than the Footman; the Chainthrower backs off while Kage walks at it and throws `chain.` moves). Review images in `Screenshots/review/SAN-97`, `SAN-99` and `SAN-101` (each foe beside Kage, a close-up and head, and `lineup_sheet.png` of every Order and Hollow foe so far).

## Notes from the implementation (SAN-98/100/112)

- **Three foes, the SAN-69 recipe** (roster file, part set, two catalog lines each; stats from `CharacterDef.FromJs`):

| id | JS health / def / dmg / speed / scale | family, weapon | profile | flags | mass | look |
|---|---|---|---|---|---|---|
| `order_shieldbearer` | 220 / 0.12 / 1 / 1 / 1.06 | spear, `sentinel_spear` | defensive | FrontalArmour | 1.25 | crescent-crested kabuto, bare hook-nosed face, full dō, big sode, a tall silver-rimmed plank tate on the near forearm |
| `order_ironclad` | 280 / 0.14 / 1.15 / 0.9 / 1.14 | cleaver, `raider_cleaver` | heavy | Armour | 2 | horned kabuto, heavy-jawed bare face with a throat guard, a dō 190 px across (the Footman's is 160), o-sode, haidate, five-row kusazuri, limbs 1.3 times as thick |
| `hollow_husk` | 270 / 0.1 / 1.15 / 0.88 / 1.16 | fists, `iron_fists` | heavy | Armour | 2.5 | heavy cowl, horned brow mask with broken horns, a mantle humped over the shoulders, torn robe open on glowing chest cracks, a shimenawa belt with shide, huge bandaged fists; no blade or saya |

- **Unity values** (not JS): the flags (the Ironclad's armour is a Unity addition, the JS foe has no `heavyArmor`; the Husk's is the spec's "hard to stagger"), the masses (Ironclad 2 and Husk 2.5 as the tickets ask; the Shieldbearer's 1.25 is a choice: armoured, a plank shield on one arm), and `FrontalArmour` for the shield (JS auto-blocks frontal light blows; here the ticket's rule is "no flinch", ADR-0004 SAN-98 notes). No `moveRate`, `postureMax` or `attackRate` is set: the weight of the heavy foes is in the families' authored frames and the heavy profile's slow decisions.
- **Part sets.** `OrderShieldbearerPartSet` and `OrderIroncladPartSet` use `OrderBody` for the limbs they share with the other soldiers (the Ironclad overrides them with thicker pieces), `HollowHuskPartSet` writes its own thick limbs and robe in `HollowPalette` (`HollowBody.Torn` for its hems). The tate is one `AddMulti` part on `ArmNL` (the near forearm), drawn above the arm and below the hand; the spear and the cleaver are the families' (`SpearVisual`, `CleaverVisual`), the Husk draws nothing but its fists. `StretchSession.ResolveFoe` needed no change: the three ids resolve to the real characters, and a PlayMode test runs the JS Stretch IV road, cut to its last encounter, until the Husk spawns with `FellBack` false.
- **Knockback** (`Travel` in `OrderIroncladFightTests` and `HollowHuskFightTests`: a 3 m/s push applied out of push range, distance after 40 frames): a heavy hit moves a Footman 0.384 m, the Ironclad 0.090 m (0.23 of it) and the Husk 0.056 m (0.15); a light hit moves the Footman 0.384 m and either armoured foe 0 m (armour takes the knockback with the flinch). Distance falls faster than 1/mass because the same friction acts on a slower body.
- **Tests.** EditMode `OrderShieldbearerTests`, `OrderIroncladTests`, `HollowHuskTests` (entry and JS stats, family and profile, flags and mass, part set on Kage's skeleton, visible shield, bulk against the Footman or the Shade, distinct look). PlayMode `FrontalArmourTests`, `OrderShieldbearerFightTests`, `OrderIroncladFightTests`, `HollowHuskFightTests` (spawn from data, armour from the front and behind, heavy hit, guard-break, knockback against the Footman, stretch resolution, dev arena cycle, a `KageBot` fight to a knock-out with no quiet stretch over 600 frames). Review images in `Screenshots/review/SAN-98`, `SAN-100` and `SAN-112` (each foe beside Kage, close-up and head, and `lineup_sheet.png` of every regular foe so far).

## Implementation notes (SAN-112 rework)

The owner's verdict on the first Husk (and on the Ironclad, SAN-100) was that the foes read "block by block": hard quads and few-sided polygons where Kage has curved, tapering, draping shapes. The Husk is redrawn as a port of the JS drawing, shape for shape, with a shared tool the next reworks (Hollow Shade and tutorial Hollow SAN-76, Order Shieldbearer SAN-98, Order Ironclad SAN-100) use the same way.

- **The tool: `OpponentKit.Smooth.cs`** (`OpponentKit` is now a partial class; no existing helper changed, so every other character renders pixel-identical: the SAN-112 line-up of the other eight foes and Kage's frames were compared before and after).
  - `Frame`: a JS drawing's own coordinates mapped into reference px. `Frame.Head(cx, cy, r)` is the JS head frame (+x the face side, head units); `Frame.Spine(hx, hy, nx, ny, unit)` is the JS spine frame (`spX`/`spY`: u along the spine, w across it to the front); `Frame.Along` a limb segment; `Frame.Ref` plain px. JS numbers are copied as they are.
  - `Outline`: a canvas-style path (`MoveTo`, `LineTo`, `QuadTo`, `CubicTo`, `Arc`) flattened at about one point per 5 px and cleaned (no repeated or straight-through points), so it triangulates.
  - Shapes: `Rounded` (JS `orderShape`: (x, y, sharp) triples, rounded corners unless flagged sharp), `RoundedAll` (`animeTorso`, every corner rounded), `Capsule` (`animeCapsule`: tapered limb, swelling, round ends), `Oval`, `TornPanel` (`tornPanelPath`, with the JS `Teeth`), `QuadXy` (a curve as a polyline for `Band`), `Crack` (`hollowCrack`), `Dashes`, `Fold` (`animeFold`).
  - `ClipConvex` (Sutherland-Hodgman: any subject, convex clip) and `Inside` cut detail to an outline (wraps round a limb, a mask cut along its break, the robe's chest opening).
  - `Cel` paints one outline as the JS `celFill`: a dark seam, the shadow tone, a lit edge and the base, by insetting the outline along its normals towards the light (`Inset`, capped at 0.45 of the local thickness so torn points stay whole) instead of clipping a shifted copy. `ShadeDir` puts the shadow back and down, the side of the rig's back-light rim.
  - `AddCel` adds a cel part with the world-fixed back-light rim (new `PartDef.rimPts`, read by `FighterRig` only when set). `AddCloth` adds a cel part bound to `SoftChains` (chains in px, root first), its rim painted into the mesh so it bends with the cloth.
- **The recipe for the next rework.**
  1. Render the JS foe in the Unity sheet's poses (a copy of `.scratch/render/harness.js` with a one-foe mode; facing left matches the Unity foe) and read its parts in `21-characters/_shared/` (the anime body: `animeArm`, `animeLeg`, `animeTorsoDraw`, `animeFoot`, `face.js`) and its skin folder.
  2. Rebuild the part set as one function per JS draw call, in JS draw order, on Kage's bones: limbs `Capsule` pivot to pivot with the JS radii times `OpponentKit.JsUnit` (4.45 px per JS unit at Kage's bind size; JS px per unit times 2.08 once the body scale is taken out); trunk pieces in a `Frame.Spine` from the waist (JS u = 0, y about 412) to the neck (222), its across unit wider (the Husk uses 6.45) because Kage's trunk is longer than the JS body's; head pieces in a `Frame.Head`.
  3. Every body part `AddCel` with the JS `sd`/`hd`, far-side parts in the far tone with no rim and no lit edge; details (folds, wraps, cracks) passed in and cut with `Inside`.
  4. Hanging cloth (robe panels, capes, tatters, tassels) `AddCloth` with two or three chains down it.
  5. Keep the part names the tests read (`Torso`, `Mask`, `Eye`, ...), and copy `HollowHuskShapeTests` (closed simple outlines that triangulate, each part on its bone, curves not blocks, cloth on chains).
  6. Render the screenshot fixture alone, compare beside the JS render, iterate.
- **The Husk** (`HollowHuskPartSet`): robe back and front flap (cloth, torn hems, folds, a broken violet band, tears), sleeves torn off at the elbow over wrapped bare forearms, fists 1.25 times the JS's oval with wrap lines and a crack and a swinging loose end, hakama thighs and shins with pleats over wrapped shins and the JS foot, a rounded hakama seat, the gi trunk with the open black chest and its cracks, the shimenawa as the JS band with three swinging shide, the mantle hump (cloth on six chains), the JS gaunt face (the Husk's numbers) with its violet profile light, the cowl, the brow mask (the face grown 1.1 cut along `HOLLOW_BROW_CUT`, eye holes, a lit front, a crack), the two broken horn stubs and the JS triangle eye. Skeleton, hurt circles, hit points, scale 1.16, mass 2.5 and the fists family are unchanged.
- **Where it still differs from the JS, on purpose.** Kage's skeleton is not the JS one: the lower leg is laid knee to sole (Kage's ankle is high), a hakama seat bridges Kage's longer waist-to-hip, and the trunk is wider than the JS proportion so the Husk stays a quarter wider than the Shade. The robe is narrower (0.8 of the JS width) and hangs to the knee, the shide are smaller and the fists bigger than a straight conversion gave, matched by eye to the JS render. The horn bases are rounded where the JS has four hard corners. The cracks' glow is a translucent band, not the canvas additive blend; the rim is Unity's back-light outline per part, not the JS outline pass round the whole figure; the JS glitch and flicker are fight effects, not part art.
- **Tests.** EditMode `SmoothShapeTests` (the tool) and `HollowHuskShapeTests` (the Husk); `HollowHuskTests.Width` reads a cel part's outline. Review images in `Screenshots/review/SAN-112/` and `rework/` (`side_by_side.png`: the JS Husk left, Unity right, idle, walk, attack and hit; `before_after_head.png`, `before_after_torso.png`, `before_after_fists.png`).

## Implementation notes (SAN-98 rework)

The owner sent the Shieldbearer back: "armor not aligned properly". The dō, sode and plates floated off the torso and shoulders and did not follow their bones, the shield did not read, and every part was a blocky polygon. The Shieldbearer is redrawn from the JS `order_shieldbearer` with the SAN-112 kit and recipe, one function per JS draw call in JS draw order (`animeArm` with `orderSode`, `animeLeg` with `orderSuneate`, the gi trunk, `orderKusazuri`, `drawOrderArmor`, `orderBelt`, `animeNeck`, the face, `_kabuto` with the crescent, `orderShield`).

- **Each armour piece on the bone it moves with, from the JS numbers.** Sode hang from the shoulder on the upper arm (the JS direction, between down the spine and along the arm, set at bind); kote and the tate ride the forearm; suneate the shin; the dō and the belt sit in the spine frame on the chest; the kusazuri hang from the waist (u 0.06) on the hips, between down the spine and along their thigh; the kabuto, shikoro, visor, wing and maedate in the head frame on the head. Alignment comes from construction, not from hand-placed points. Units: 3.3 reference px per JS unit on the limbs (the JS U of 1.42 JS px on a skeleton 2.3 times smaller), 5 across the trunk.
- **Kit additions.**
  - Cel parts can swing. `FighterRig` now sets up a part's `jigglePivot` before it branches on polys, so a cel part (and its rim) hangs from its own pivot with the same spring as a flat `AddSwinging` part; `OpponentKit.Swing(part, x, y, swing)` sets it. No existing polys part had a pivot, so nothing else moves (the line-up of the other six foes and Kage's frames are pixel-identical before and after).
  - In the part set (not yet in the kit; lift them when the Ironclad, SAN-100, is redrawn): `Plate` (the JS `_quad`: a straight top edge, sides and hem bowed outward), `Lames` (the JS `_odoshi`), `Mark` (`orderMark`), and `Stroke`, which clips a detail line to a part of any shape by keeping the runs whose samples lie inside it (a fixed number of samples per segment). `Inside`/`ClipConvex` need a convex clip, and the dō, shikoro and kusazuri are not.
- **The recipe, one addition.** A weapon family that keeps the weapon arm in front of the body (`FighterRig.SetSwordAlwaysFront`: the spear, any polearm) has no far arm in the JS sense: drawn in the JS far tones with no detail, its sode read as a blank black slab over the chest. Draw that arm lit, rimmed and laced like the near one, its parts still `swordLayer`.
- **Where it still differs from the JS, on purpose.** The JS holds the tate upright in the far hand, clear of the body; on Kage's skeleton it is strapped to the near forearm (it rotates with the arm, and in the idle stance it stands over the trunk), so its planks are a weathered lacquer a step lighter than the dō, and it is about a sixth taller than the JS proportion (about 345 px against 295) so it reads at fighting distance. The kusazuri are about 205 px long, not the JS 34 JS px scaled, because Kage's waist sits 130 px above his hips; they cover the hakama seat and the top of the thigh, swing from the belt and do not turn halfway with the thigh as the JS plates do (a part rides one bone). The lower leg is laid knee to sole as on the Husk, with the hakama seat bridging waist and hips. The cloth stays the Order's slate (`OrderPalette.Cloth`), lighter than the JS near-black, so the line-up reads as one family; the lacquer, silver and crimson are the JS tones. The face is the JS black profile with its cold crimson strip (the old Unity part set had a bare grey face). The rim is Unity's back-light outline per part, not the JS outline pass.
- **Tests.** EditMode `OrderShieldbearerShapeTests` (closed simple outlines, each part on its bone, each armour piece along its bone and over the body it covers, curves not boxes, the kusazuri swinging from the belt on a real rig, the tate on the near forearm under the hand); `OrderShieldbearerTests` unchanged. Review images in `Screenshots/review/SAN-98/` and `rework/` (`side_by_side.png`: the JS Shieldbearer left, Unity right, idle, walk, attack and hit; `before_after_torso.png`, `before_after_shoulder.png`, `before_after_shield.png`).

## Implementation notes (SAN-76 rework)

The owner sent the Hollow Shade and the tutorial Hollow back for refinement: like the first Husk, both were blocky polygons beside a smooth Kage. Both are redrawn from the JS (`hollow_shade`, `hollow_tutorial`) with the SAN-112 kit and recipe, one function per JS draw call in JS draw order (the back robe, `animeArm` far, `animeLeg` far, the hakama seat, `animeTorsoDraw` with `drawHollowArmor`, `animeLeg` near, the front flap, `hollowSash`, `animeSaya` on the Shade, `animeNeck`, `animeFace`, `drawHollowHead`, `animeArm` near, the weapon).

- **A shared gi body: `PartSets/HollowAnimeBody.cs`.** The Shade and the tutorial Hollow wear the same JS anime body (gi, wrapped kote and shins, straw sandals), so its pieces are one class built with each Hollow's sizes: `Arm` (the full upper sleeve with creases, the bandaged kote 0.3 to 0.9 down the forearm, the forearm sleeve flaring to its opening at mid-forearm, the opening torn into points with a violet lining or stuffed with straw, the glove), `Leg` (as the Husk's), `Pelvis` (the hakama seat, in the dark back tone), `Neck`, `Torso` (`ANIME_TORSO.gi`, the crossed collar and its violet edge), `Robe` (`hollowRobe`: the torn panel on three chains with folds, the broken violet band and two tears), `FacePath` with every `FACE_PROFILES` field, `Face`, `MaskPart` (a mask piece with its lit front edge, eye holes and crack), `Eye`. It is new code beside `HollowBody` (the Flicker's) and the Husk's own copy, which were not touched: the Husk and Flicker cells of the line-up are pixel-identical before and after.
- **Sizes.** The JS Shade is the lean Hollow (build 9.5, U 1.14 JS px per design unit against the Husk's 2.14, on a skeleton 1.18 times smaller), so the limbs are 2.84 reference px per JS unit (the Husk's 4.45 scaled the same way), the trunk 4.1 across (the Husk's 6.45), the head radius 56 (the Husk's 58 at its 1.16 body scale). The Husk's trunk is 1.6 times the Shade's in the part set and about 1.85 times on the screen (the JS proportion is 1.88).
- **Kit addition: `OpponentKit.ClipStar(a, b, centre)`.** The overlap of two shapes that may both be concave, as long as each is seen whole from the centre: rays from it (a fixed 360 plus one through every corner of either shape) keep the nearer edge. The Shade's half-mask is its own face grown 1.06 (concave at the bridge of the nose) cut along `HOLLOW_HALF_CUT` (a zigzag break), which `ClipConvex` cannot do without filling the bridge or straightening the break. No existing helper changed.
- **Where it still differs from the JS, on purpose.** As on the Husk: the lower leg is laid knee to sole, a hakama seat bridges Kage's long waist-to-hip, the rim is the rig's back-light outline per part and the crack glow a translucent band. The robe panels hang from the waist with their JS links, widths, teeth and rag, their lengths taken as a share of waist-to-ground (the Shade's back panel to the calf, its flap over the knee, the tutorial Hollow's both above the knee); the Shade's flap is 0.65 of the JS width and its back panel the full width, so the near leg shows beside the flap as it does in the JS, where the leg steps out ahead of it. The tutorial Hollow's plank is wood (the JS paints the board in the mask lacquer and only its grain in wood), so the training husk reads as a board at fighting distance. The Shade's saya cord is the Hollow deep violet (the JS keeps the crimson cord of every saya), so no Kage crimson shows on a Hollow; the saya is 175 px long. The tutorial Hollow has no saya (the JS skin gives it none; the first Unity art gave it the Shade's). The katana is still `HollowBody.Sword` (the echo of a blade on the hit line); the shards drift on the part swing, not the JS sine.
- **Tests.** EditMode `HollowShapeTests` (both Hollows: closed simple outlines, polygons with an area, each part on its bone, curves not boxes and no flat blocks, the robe panels, the sash ends and the sack's rope ends on chains, the Shade leaner than the Husk, the Shade's shard crown, rip and haori to the calf, the tutorial Hollow's straw, sack, plank, crossed ropes and rope sash) and `SmoothShapeTests` (`ClipStar`); `HollowTests`, `HollowHuskTests` and `HollowFlickerTests` unchanged. Review images in `Screenshots/review/SAN-76/` and `rework/` (`side_by_side_shade.png`, `side_by_side_tutorial.png`: JS left, Unity right, idle, walk, attack and hit; `before_after_<shade|tutorial>_<head|torso|skirt|body>.png`).

## Implementation notes (SAN-76 SVG redesign)

The owner reviewed the smooth Shade and sent their own painting and a VTracer trace of it: "use unmovable parts from svg and movable parts design it so that it responds to physics and wind", built as reusable parts for the other Hollow mini-bosses. The Shade is now cut from that art. Nothing of it is drawn in code. The pipeline is generic: the next Hollow (the Husk, SAN-112, then later ones) adds its own SVG and a config, and no new C#.

**The pipeline.**

1. **Source art**: the owner's files, unchanged, in `Tools/hollow_art/<name>/original/` (the Shade: `shade_ref.png`, the 1536 x 1024 painting, and `shade_cloaked_miniboss.svg`, a stacked VTracer trace of 346 single-path shapes in 340 colours). The painting is an input too: the trace merged the cape's black inside with the canvas, so the silhouette is taken from the painting.
2. **Config** `Tools/hollow_art/<name>/rig.json` (data only): the crop, the trace's background colours, the **tones** (each a name from the shared Hollow palette and the trace colours that snap to it), the silhouette settings (painting threshold, hole size, hand-marked `add`/`cut` polygons), the speck size, the **fit** of the painting onto Kage's reference frame, the **regions** (a rough polygon per piece, and the pieces that cover it, `minus`; `tones` for a piece cut by colour, like the eye and the mask's crack) and the **parts** (region, bone, depth slot, map, flags, soft preset, chains, swing pivot).
3. **Cut** `python Tools/svg_art/svg_rig.py Tools/hollow_art/<name>/rig.json` (Python 3, numpy, opencv; no Unity). It rasterises the trace at 4x in paint order, snaps every colour to its nearest tone (CIELAB), adds the painting's silhouette (dark cloth takes the fill tone), turns islands under `speck` px² into the tone around them, cuts the regions, and writes each region as simple polygons in paint order. First a base layer (the region's outline in `Ink`), then every island of every other tone with its holes filled, outermost first. An island inside another's hole is painted after it, so no polygon has a hole and a seam shows the base, not the background. Holes that open onto the outside are slit open. Outlines are traced half a sample outside each island, simplified by `eps` (0.45 px) and checked simple. **Soft** regions are cut cell by cell (`softCell`, 24 px) and resampled (`resample`, 8 px), so no triangle is bigger than a cell. The first cut was one triangulation per polygon: when the cape bent, the long slivers of its rim stroke swept across the cloth as a violet sail. It writes `Assets/Kage/Resources/Art/SvgRig/<Name>.txt` (regions, the parts passed through, the sources and an FNV-1a hash of the SVG, the config, the tool and the painting) and the previews `regions.png` (each region tinted, magenta = figure no region draws), `tones.png` and `rebuilt.png` (the polygons painted back) in the config's `preview` folder.
4. **Build** (`Scripts/Character/SvgRig/`): `SvgRigData` reads the text (cached per run). `SvgRig.Build(rig, data, style)` puts each part on its bone in the style's colours. Map kinds:
   - `trunk`: the fit alone;
   - `pin`: one SVG point on a bone's pivot or a reference point, with a scale and a turn; for rigid pieces (head, boots, plate column, saya);
   - `seg`: two drawn joints on two bones' pivots, stretched along and kept across; SAN-71's limb method;
   - `line`: the same onto two reference points; for a cape or coat tails hung from the body;
   - `attach`: a hand turned with its forearm;
   - `blade`: the grip on the sword bone and the tip along Kage's blade at a given length, so the painted point is where the hit line ends.

   Flags:
   - `rim`: the rig's world-fixed back-light rim round the region's outline;
   - `far`: the style's far colours;
   - `sword`: the sword layer;
   - `glow`, `role`;
   - `swing`: its own spring pivot;
   - `soft`: a `SoftPresets` feel, with the chains mapped like the part.

   The part set is three lines: `Skeleton`, `HollowArt.Dress`, `SvgRig.Build`.
5. **Shared Hollow parts**:
   - `PartSets/HollowArt.cs`: the 12 tones in their game colours (`Ink`, `Shadow`, `Cloth`, `ClothMid`, `ClothLit`; the violet light `VioletDeep`, `Rim`, `RimBright`, `RimHot`; `SteelDark`, `Steel`, `White`), the far-side colour (half way to the deepest ink: the painting's far limbs are near-black silhouettes), the violet rim (`HollowPalette.Rim`), the eye and crack glows. It is new beside `HollowPalette` and changes nothing drawn in code.
   - `SvgRig/SoftPresets.cs`: soft-part presets `Cape`, `Tails`, `Strip`, `Plates`, `Tassel`. Cloth is loose at the tip with high drag. Plates are heavy, stiff and low in drag. A tassel is thin and falls straight.
6. **Tests to copy** (`HollowShadeSvgTests`):
   - the cut is fresh (it recomputes the hash);
   - every cut polygon is closed, simple, triangulates whole and is bigger than half a speck;
   - every part is on its bone (a table) and near it;
   - the cloth and plates are on chains with their presets and carry no fixed rim;
   - every colour is in the Hollow palette, the eye and crack glow, the spikes swing;
   - the blade ends at the hit line;
   - the foe's width beside the others;
   - the soft parts bend in the wind on a real `FighterRig`, and calm repeats.

**Steps for the next Hollow** (the Husk: `Tools/hollow_art/husk/`; the owner's `husk_hooded_warrior.svg` is in the main checkout's `.claude/owner_art/`):

1. Copy the SVG and painting into `Tools/hollow_art/husk/original/`.
2. Copy the Shade's `rig.json`. Set `svg`, `painting`, `out` (`Art/SvgRig/HollowHusk`), `preview`, `crop` and `background`, and the tones' trace colours (list the trace's colours by lightness, Lab `a`/`b` for violet against grey).
3. Run the tool with one catch-all region and look at `tones.png` against the painting. Adjust the tones, `speck` and the silhouette until it reads clean.
4. Draw the regions on a gridded view of `tones.png`. Re-run until `regions.png` has no magenta worth a part and `rebuilt.png` matches `tones.png`.
5. Write the parts: rigid pieces `pin`/`seg`/`attach` on their bones (Kage's slots, ADR-0005 SAN-69), the far limbs as the near regions with `far` and `rim`, hanging pieces with a preset and two or three chains root first, the weapon with `blade` (the Husk has none: fists).
6. Replace the part set's body with `SvgRig.Build(d, SvgRigData.Load(<resource>), HollowArt.Style)`.
7. Copy `HollowShadeSvgTests` with the Husk's table, then copy `ShadeSvgScreenshots` and look at the images.
8. After any change to the SVG, the config, the painting or the tool, re-run the tool: the freshness test fails until you do.

**Wind** (`Scripts/Character/Wind.cs`):
- `Wind` is a steady world wind (m/s) with gusts on top: smooth value noise from the seed, two octaves at `gustRate` a second, travelling along the wind with x, pushing 0 to `gust` m/s along it with a third of that sideways. It is a pure function of the seed, the time and x, with no random draws.
- `SoftChains.wind` adds it as a push on every particle, by the part's drag times `SoftChains.WindPush` (6), more towards the free end. The trailing breeze alone could not bend a shape-matched part: the first try moved the cape 7 mm in a 6 m/s wind.
- `FighterRig.wind` hands it to every chain each frame. Calm (the default) skips the code entirely, so no character moved: a golden of Raikon's cape measured on main before the change still matches, and the line-up of every Order and Hollow foe, Raikon's chains and Kage's coat in motion are pixel-identical before and after.
- Kage's own coat (`CoatCloth`), hair and scarf do not read the wind yet. Nothing sets a wind in the game yet. A stage or arena wanting weather sets `rig.wind` on its fighters.

**The Shade.**
- Rigid parts from the SVG:
  - on the head: the head (dome, mask, band, ear disc), the crack (glow 1.6), the eye (glow 2.6) and three crown spikes on their own swing pivots;
  - the neck/hood, the torso, the pelvis and the saya;
  - the near arm (upper, fore, the fist on its forearm) and the hilt and blade on the sword bone;
  - the near leg (thigh, shin with the boot shaft, boot).
- Far side: the far arm, the sword hand and the far leg are the near regions in the far colours, with the rig's violet rim. They are not darker copies of drawn detail, so they read as the painting's dark far limbs.
- Soft parts on chains:
  - the cape from the shoulders (`Cape`, 3 chains);
  - the torn shoulder strips (`Strip`, 2);
  - the coat tails and the rear hem from the waist (`Tails`, 3);
  - the sash of hanging plates (`Plates`, 2).
- Unchanged: the skeleton, the hurt circles, scale, mass, stats, the katana family and the attack rate.
- The rig's world-fixed rim is only on the far parts: on the near parts it doubled the painted rim strokes into a fringe.

**Where it differs from the painting, and why.**
- The painting is a deep low stance. The game skeleton stands Kage's way, so every limb is mapped joint to joint and the fight idle is Kage's chūdan, not the painted crouch. The thigh is squashed to about 0.8 of its painted length and the trunk turned upright by about 15 degrees.
- The coat tails and cape are stretched along about 1.16 to reach the calf of a standing figure.
- The far arm, the sword hand and the far leg are not in the painting (hidden or merged in black), so they are the near ones in the far colours.
- The rear boot area of the painting (black, merged with the hem) is part of the coat tails, so it swings.
- The painting's soft gradients are flattened to 12 tones, so the cape's inside is one ink.
- The trace's hands and arms are a tangle of small planes. They are cut as painted, so they read as a dark fist rather than separate fingers.

**Polish pass (definition).** After review, two things looked like the low definition the owner rejected in SAN-71.

*Joints.* The thigh, shin and boot floated apart, and the far leg read as fragments. The causes were:
- the painting leaves the boot, the knee, the fist and the forearm hollow inside their rim strokes (canvas colour);
- the cut lines did not meet at the bones' pivots.

The fixes:
- **Tone fills**: the silhouette's `add` polygons can carry their own tone (`{"poly": [...], "tone": "Shadow"}`), so those areas are filled solid in the painting's dark tones.
- **Underlays**: a part's `under` (capsules `[ax, ay, bx, by, r, tone]`) are drawn first in its mesh, from drawn joint to drawn joint. The joints are now the painted limbs' centrelines, which `seg` puts exactly on the bones' pivots. Neighbouring pieces therefore overlap round every pivot in any pose, as Kage's capsule limbs do.
- **Far hand**: the far hand is placed on its forearm (`attach`) like the near one, a clean fist rather than a smaller copy at the blade's scale.
- **No rig rim**: the rig's world-fixed rim is off on every piece. On the far limbs it drew a line across each joint; the painted rim strokes carry the light.

`NeighbouringLimbPiecesOverlapAtEveryJointInEveryPose` (EditMode) poses a real `FighterRig` in bind, stance, crouch, guard, two hurts, a deep landing, a dash, a tumble and lying. For every joint (knees, ankles, elbows, wrists, near and far), it checks that a 10 reference px disc round the pivot is covered by the two pieces' rendered triangles. It failed on the first cut in every pose.

*Edges.* The cutter now:
- softens the 1x painting's silhouette (a Gaussian of 1 px, then a cubic upsample) before it joins the trace;
- simplifies every outline by Douglas-Peucker (`eps` 0.6 px);
- rounds it by two rounds of Chaikin, except at real corners (a turn over `corner`, 40 degrees).

So straight painted edges stay straight, curves come out smooth, and spike tips and plate corners stay sharp. Cloth cells are smoothed the same way.

Not fixed, and not for this ticket: the game renders without MSAA (`UniversalRP.asset`, MSAA 1). Every character's polygon edges therefore step at screen pixels, Kage's included.

**Tests changed.** `HollowShapeTests` lost its Shade cases (they tested the JS port's part names, `Cowl`, `Shard_`, `Robe_Back`, ...). The tutorial Hollow's cases are unchanged; the Shade's are in `HollowShadeSvgTests`. `HollowHuskTests.Width` also reads a torso cut from an SVG (its polygons) where it read `pts ?? rimPts`; the expectation is the same.

**Tests.**
- EditMode: `SoftChainWindTests` (a rope sags under gravity, trails the body and settles, bends with the wind either way, the same seed repeats and another differs, calm changes nothing, the wind's bounds, every preset hangs and bends by its weight, the Raikon cape golden) and `HollowShadeSvgTests`.
- PlayMode: `ShadeSvgScreenshots`.
- Review images in `Screenshots/review/SAN-76/svg/`:
  - `side_by_side.png`: the painting, the bind pose, the fight idle;
  - `kage_vs_hollow_shade_sheet.png`: idle, walk, attack, hit;
  - `hollow_shade_closeup.png` and `hollow_shade_head.png`;
  - `soft_motion.png`: a walk, stop and swing, a gusty head wind, a gusty tail wind;
  - `lineup_sheet.png`, with the tiles alone in `lineup/`;
  - the cut previews in `cut/`.

## Implementation notes (SAN-112 SVG redesign)

The owner reviewed the smooth Husk (the SAN-112 rework above) and sent an SVG, a VTracer trace titled "Dark Purple Hooded Warrior Profile": "use the svg to design the character take still elements from svg and use it and other movable elements to respons according to physcis and wind". The Husk is now cut from that trace with the SAN-76 pipeline, the second Hollow drawn this way. Its part set is the Shade's three lines. Nothing of it is drawn in code.

**Source.** `Tools/hollow_art/husk/original/`: `husk_hooded_warrior.svg` (823 paths, unchanged) and `husk_ref.png`, its 1536 x 1024 render by headless Edge. There was no painting this time: the render stands in as the painting, so the tool and the tests read the same inputs as the Shade's.

**Config** (`Tools/hollow_art/husk/rig.json`).
- The Shade's 12 tones, plus three rope tones for the shimenawa (`RopeDark`, `Rope`, `RopeLit`, added to `HollowArt`, so the Shade's colours are unchanged).
- The background is `#0E0814`, and the trace paints the cloak's inside and the far leg in that same colour. One silhouette `add` polygon fills the cloak in `Ink`. The painted far boot and the ground shadow are figure that no region draws.
- Fit k 0.8. The trunk, belt, tassets, shide and pelvis share one `line` map, the painted neck (720, 235) and hips (750, 560) onto Kage's neck and hips. The cloak is `pin`ned on the hips at k 0.8, so its hem stays above the ground.
- Limbs are `seg` joint to joint, with painted joints at the shoulder (785, 285), elbow (695, 425), wrist (648, 345), hip (668, 588), knee (582, 790) and ankle (614, 903). The ankle is set so the boot's sole lands on Kage's ground line. The fists are `attach`ed on their forearms. The head is one `pin` of the painted neck top (700, 228) on the head bone.
- Underlays (capsules) run on every limb segment. The thigh's stops inside the painted knee (596, 760), and the ankle disc is small: at full size their round ends bulged past the knee and heel. The torso has a big ink underlay and the face an ink disc, so the bites left by the fist and forearm painted over the chest and jaw never show the background when the arm moves.

**The Husk.**
- Rigid parts:
  - on the head: the hood, two horns, the horned brow mask, the black face and the eye (glow 2.6);
  - on the chest: the torso;
  - on the hips: the pelvis and the shimenawa;
  - on the arm bones: the upper arm with its pauldron (one piece on `ArmNU`), the forearm with its bracer, and the huge banded fist;
  - on the leg bones: the hakama thigh, the boot and the foot.
- Far side: the far arm (with its pauldron), the far fist and the far leg are the near regions in the far tones, as on the Shade.
- Soft parts:
  - the long torn cloak from the hips (`Cape`, 4 chains);
  - the mantle humped over the shoulders (`Cape`, 3);
  - the skirt of pointed tassets (`Plates`, 3);
  - three white paper shide (`Tassel`, 2 each);
  - the torn bandage ends under the near forearm (`Strip`, 3).
- Unchanged: the skeleton, hurt circles, hit points, scale 1.16, mass 2.5, the Armour flag, the stats and the heavy fists set.

**Pipeline addition: `across` on `seg` and `line`.**
- `seg ax ay bx by FROM TO [across]` and `line ... [across]` take an optional factor across the segment. The default is 1, so every existing map gives the same numbers: the Shade's line-up tile is pixel-identical.
- The Husk's thighs use 1.12. Mapped joint to joint at the fit's scale, the painting's baggy hakama came out only 1.18 times the Shade's thigh. The heavy Hollow should read clearly heavier, and the Shade's own "thin legs" test sat on its threshold.
- The cutter passes maps through unchanged, so `svg_rig.py` did not change and the Shade's cut stays fresh.

**Where it differs from the art, and why.**
- The trace is a wide, knee-bent stance with one fist up at the chin. In the game the skeleton stands Kage's way: the legs are mapped joint to joint, the thighs squashed to about 0.77 of their painted length, the shins to about 0.8, and the trunk stretched about 1.14 along. The fists family's idle happens to hold the near fist at the chin, as painted.
- The far arm, far fist and far leg are not in the trace (hidden, or merged into the canvas-black cloak), so they are the near ones in the far colours. The painted far boot is not used.
- The cloak's inside is canvas colour in the trace, so it is one flat ink with the trace's violet edge strokes. The torn tongues between the legs are part of the cloak and swing with it.
- The spiky strokes under the near forearm are read as torn bandage ends and hang from the forearm. In the trace they might be fur or a torn sleeve.
- The trace has no glowing chest cracks, which the JS Husk and the rework had.

**Tests.**
- EditMode `HollowHuskSvgTests`, the Shade's set with the Husk's table:
  - the cut is fresh;
  - every polygon is closed, simple, triangulates and is bigger than half a speck;
  - every part is on its bone;
  - the belt sits at the waist;
  - the soft parts are on chains with their presets, and they are exactly the soft parts;
  - the palette, the eye glow, the rope and the white shide;
  - the Husk is bulkier than the Shade (torso, upper arm and fist by 1.25 to 1.5, thigh by 1.2);
  - `NeighbouringLimbPiecesOverlapAtEveryJointInEveryPose`: the Shade's eight joints plus shoulder and hip, in ten poses;
  - a walk and a stop swing the cloak, mantle, bandage ends and tassets (the tassets least);
  - a head wind streams the cloak, mantle and shide back and only stirs the tassets, and calm repeats;
  - the `across` factor of a `seg` map.
- PlayMode `HuskSvgScreenshots`, a copy of `ShadeSvgScreenshots` with the Husk's framing.
- Retired: `HollowHuskShapeTests`, whose five cases read the JS port's part names and cel outlines.
- Changed:
  - `HollowHuskTests.ItReadsAsAHollowNotAnOrderSoldier` asks for the horned mask and the glowing white eye where it asked for `Chest_Cracks` in `HollowPalette.Violet`;
  - `HollowShadeSvgTests.TheShadeIsLeanBesideTheHusk` compares against the Husk's `ArmN_Upper` and `LegN_Thigh` (it read `ArmN_Sleeve` and `LegN_Hakama`).
- The `HollowHuskTests` width checks are unchanged and pass.
- Review images are in `Screenshots/review/SAN-112/svg/`:
  - `side_by_side.png`;
  - `kage_vs_hollow_husk_sheet.png`;
  - `hollow_husk_closeup.png` and `hollow_husk_head.png`;
  - `soft_motion.png`;
  - `lineup_sheet.png`, with the tiles in `lineup/`;
  - the cut previews in `cut/`.

## Implementation notes (SAN-112 definition pass)

The owner, on the SVG Husk: "use opus high to define this character bit more use svg as reference". The pass compared the rig with the SVG at the same scale and fixed what read differently. The skeleton, hurt circles, scale, mass, stats and moves are unchanged, and `svg_rig.py` did not change, so the Shade's and the Ironclad's cuts stay fresh.

**How it was compared.** `HuskSvgScreenshots` now poses the rig *as painted*: each limb segment turned so its drawn joints (its `seg` map) lie along the painted ones, the hands and feet as painted, the far leg reaching back to the painted far boot (IK), the far arm (hidden in the art) behind the trunk. It renders that beside the art at the art's own scale, on the art's canvas colour, from the fit (the painted sole on Kage's ground), and four close-ups at twice the scale, each centred through the bone that carries the piece. The cut itself (`tones.png`, `rebuilt.png`) already matched the trace closely. The differences were in the game:

- **Colour.** The shared Hollow palette is a cooler blue-violet than the trace's plum and lavender. Every rim stroke and cloth plane read bluer and brighter than the art. `HollowArt.Husk` is the Husk's own style: the same 15 tone names, each painted in the area-weighted mean of the trace colours the cut snaps to it (measured over the figure: `Ink` #0F0914, `Shadow` #150D1E, `Cloth` #1F132A, `ClothMid` #2A193D, `ClothLit` #3A284C, `VioletDeep` #5B3D7A, `Rim` #6D4999, `RimBright` #8B65B4, `RimHot` #A87BDC, `SteelDark` #7E6A8A, `Steel` #CBB3DA, `White` #EBE8EC, rope #3B2B31 / #513E3D / #806761). The far colours, rim and glows are the shared ones. The Shade keeps `HollowArt.Style`, so it is pixel-identical.
- **Ink blobs.** The torso's and the fists' underlays were `Ink`, darker than the cloth round them. They showed as black discs under the fist and a black hump in the chest wherever the arm moved off its painted place. The torso's is now `Shadow`, the torso's own colour. The fists' are smaller (r 22) and `Shadow`, and sit inside the fist.
- **Doubled and cut edges.** The hood's region took a strip of the mantle and the top of the pauldron. When the head turned from the chest, the pauldron's arc showed twice and a straight edge of mantle rode on the head. The hood's region now stops at its back rim stroke (x 786) and leaves the pauldron (`minus ArmN_Upper`, the face too). The pauldron's region follows its arc instead of a chord. The mantle has two `ClothMid` underlays: under the hood's back edge, and under the pauldron's top. When the arm or head moves, the bites they leave in the mantle show mantle, not canvas.
- **The far boot is the painted one.** The trace draws a far boot, smaller and darker (rim strokes round a canvas-dark inside). The far shin and foot are now cut from it (`ShinF`, `FootF`: an `add` fill in `Ink` for its inside, `seg 872 800 872 930`, `pin 872 930`) and painted as drawn, not darkened. The far thigh is hidden behind the cloak in the art, so it stays the near thigh in the far colours.
- **The hakama keeps its length.** The painted thigh is long (a crouch). Mapped hip to knee onto Kage's thigh it came out at 0.77 of its painted length and lost its bulk. Its drawn knee is now 0.8 of the way down (`seg 668 588 599.2 749.6`), so the thigh is drawn at about 0.96 of its painted length and runs on past the knee pivot under the boot's cuff (the shin is drawn after it), as the painted hakama tucks into the boot.
- **Finer cut.** `eps` 0.6 to 0.45 and `speck` 8 to 6: the outlines follow the trace more closely and small shading planes survive (Wraps 105 polygons, Plates 359).
- **Soles.** The feet were cut to y 970 and 976, which took a strip of the ground shadow as a dark bar under each boot. They now stop at the painted sole (969).

**Open questions, with the choices made.**
- The spiky strokes under the forearm are kept as torn bandage ends on chains. The trace draws them as loose, hanging spikes in the bracer's colour.
- No chest glow: the SVG has none.
- Fist size is as painted. It does not break a pose: the joint-overlap test passes in all ten poses.
- Stance: the fists family's idle (Kage's skeleton) is unchanged. The painted crouch is only in the review pose.

**Tests.**
- EditMode `HollowHuskSvgTests` gains three:
  - `TheHuskIsPaintedInItsSvgsOwnPlumNotTheSharedBlueViolet`: the same tone names and rim as the shared style; the cloth and violet tones redder; two anchors from the trace; the Shade still painted only in the shared palette.
  - `TheFarBootIsThePaintedFarBootNotADarkCopyOfTheNearOne`.
  - `TheHakamaThighRunsOnPastTheKneeUnderTheBootCuff`.
- Changed: `ItIsDrawnInTheHollowPaletteFromTheTrace` and `HollowHuskTests.ItReadsAsAHollowNotAnOrderSoldier` read `HollowArt.Husk` where they read `HollowArt.Style`. Their expectations are the same.
- PlayMode `HuskSvgScreenshots`: writes to `Screenshots/review/SAN-112/define/`. It adds the painted pose and the same-scale side-by-side, and the close-ups (`closeups/`, `closeup_*.png`). `Render` takes an optional background colour; the default is unchanged, so `IroncladSvgScreenshots` and `HelmetTierScreenshots` render as before.
- Review images in `Screenshots/review/SAN-112/define/`:
  - `side_by_side.png`: the SVG, the painted pose, the bind pose, the fight idle, all at the SVG's scale;
  - `before_after_painted_pose.png` and `before_after_<head|torso|arms_fists|legs>.png`: the SVG, the SVG round and this pass at twice the scale (the "before" is the previous cut rendered by this fixture);
  - `kage_vs_hollow_husk_sheet.png` (idle, walk, attack, hit), `hollow_husk_closeup.png`, `hollow_husk_head.png`;
  - `soft_motion.png`;
  - `lineup_sheet.png` and `lineup/`;
  - `cut/`.
- Pixel check: every other line-up tile (the five Order foes, the Shade, the tutorial Hollow, Flicker) and Raikon and Kage in motion are identical to a render of the same fixture made before the change.

## Implementation notes (SAN-100 SVG redesign)

The owner reviewed the smooth Ironclad (the SAN-100 rework) and sent a painting and its VTracer trace: "take unmovable components from svg and movable components design so that it responds to physics and wind". The Ironclad is now cut from that art with the SAN-76 pipeline, the first Order foe drawn this way. Its part set is three lines: `Skeleton`, `OrderArt.Dress`, `SvgRig.Build`. Nothing of it is drawn in code.

**Source.** `Tools/order_art/ironclad/original/`: `ironclad_ref.png` (the 1536 x 1024 painting) and `ironclad_horned_samurai.svg` (961 paths), both unchanged.

**The pipeline, generalised for the Order.**
- `svg_rig.py` did not change (the Shade's and the Husk's cuts stay fresh). A config already names its own tones; the faction is the style the part set passes to `SvgRig.Build`.
- `OrderArt` (new, `PartSets/OrderArt.cs`) is the Order's style beside `HollowArt`: 12 tones (`Ink`, `Shadow`, `Lacquer`, `LacquerMid`, `LacquerLit`, the cold plate edges `Edge` and `EdgeLit`, the lacing `CrimsonDeep`, `Crimson`, `CrimsonLit`, `Steel`, `White`), each with a little blue so the lacquer stays the Order's slate; far parts half way to the deepest ink; the painting's crimson rim (`OrderArt.Rim`); the white eye's glow (`EyeGlow` 2.6). Any later Order foe painted in black lacquer and crimson snaps its trace to these names.
- `SvgRigStyle.Dress(d)` (rim, body colour, no scarf or coat) is shared: `HollowArt.Dress` and `OrderArt.Dress` call it, so the Hollows set exactly what they set before.
- `blade gx gy tx ty len [across]`: an optional factor across the weapon's axis (default 1, so the Shade's katana gives the same numbers). The painted cleaver is about as broad as it is long; stretched so its point reaches the family's hit line it was 0.45 m broad. At `across` 0.55 it is 0.27 m, close to `CleaverVisual`'s 0.21.
- `CleaverVisual` leaves a painted cleaver alone: a rig with its own `Cleaver_Blade` part gets no family parts and keeps its own weapon shown (the `TachiVisual` rule for Raikon). Every other cleaver wielder gets the family drawing as before.

**Config** (`Tools/order_art/ironclad/rig.json`).
- Crop (460, 0)-(1110, 960), background `#0F0B12`, the painting's silhouette with `holeArea` 3000 (the hakama's dark folds sit within 10 of the canvas), two tone fills (the thigh's underside, the hakama between the legs).
- Fit k 0.85, the painted sole on Kage's ground. The trunk, belt, kusazuri, hakama, tassel, pelvis and wakizashi share one `line` map: painted neck (775, 330) and hips (860, 600) onto (845, 222) and (875, 520).
- The head pieces (kabuto, horns, shikoro, face, eye, throat guard) are one `pin` of (760, 300) on the head bone at k 0.85.
- Limbs `seg` joint to joint: shoulder (915, 360) for the sode, (968, 395) for the upper arm behind it, elbow (935, 495), wrist (845, 556), hip (790, 650), knee (655, 738), ankle (712, 872); thighs 1.1 across. The near hand is `attach`ed on its forearm with its grip a fist from the wrist, where the two-handed grip puts the fist; the far hand is the painting's upper hand, attached so its grip lands on the sword pivot.
- The cleaver is two `blade` maps with the same scale: the blade and guard on the blade's own axis, (715, 507) to (474, 338), and the handle on the painted handle's axis (the painting bends the handle about 15 degrees from the blade). An ink underlay carries the handle back past the near hand's grip (-0.24 m) to a pommel.
- Underlays on every limb segment, a smaller ink one inside the dō (a big one showed as a hump behind the shoulders once Kage's arm sat forward of the painted one), and ink under the face and both fists.

**The Ironclad.**
- Rigid: on the head, the two ox horns, the kabuto (crest disc, brim band, visor), the laced shikoro, the black face, the white eye, the throat guard with its crimson rings. On the chest, the dō with the back of the trunk. On the hips, the pelvis, the crimson rope belt and the wakizashi. The upper arm with its big laced sode, the forearm and elbow cop, the fists. The hakama thigh, the boot with its crimson top, the foot.
- Far side: the far arm, sode and leg are the near regions in the far tones. The far hand is the painted upper hand.
- Soft: the front and back kusazuri (`Plates`, 3 chains each), the hanging rope and tassel (`Tassel`, 3), the hakama hem (`Tails`, 3).
- The cleaver: the painting's blade, guard and handle on the Sword bone, its front corner on the hit line. Gameplay is unchanged: skeleton, hurt circles, hit line and reach (the family's 0.9 m past the guard times the raider cleaver's reach of 1), stats, cleaver set, Armour flag, mass 2 and scale 1.14.

**Where it differs from the painting, and why.**
- The painting is a deep crouch with the front knee far forward. The skeleton stands Kage's way, so the legs are mapped joint to joint: the boot shaft is squashed to about 0.7 of its painted length, and the hakama thigh hangs instead of reaching forward. The trunk is turned upright by about 9 degrees.
- Kage's shoulder sits further forward on the trunk than the painted one, so the sode hangs over the middle of the dō and the back of the trunk shows behind the arm.
- The far arm, sode and leg are hidden in the painting (and the painted back boot is not used), so they are the near ones in the far colours.
- The cleaver is 0.55 times as broad as a straight scale gives (see `blade` above). Its hit line runs just under the spine, along the painted crimson line, to the front corner.
- The handle is laid along the hands' line: the painting bends it from the blade.
- The painting's soft gradients are 12 tones, and its lacquer highlights are the two edge greys.

**Steps for the next Order foe.**
1. Copy the SVG and painting into `Tools/order_art/<name>/original/`.
2. Copy the Ironclad's `rig.json`. Set `svg`, `painting`, `out` (`Art/SvgRig/Order<Name>`), `preview`, `crop`, `background`, and the trace colours of each `OrderArt` tone (list the trace's colours by lightness; Lab `a` sorts crimson from lacquer).
3. Run the tool with one catch-all region and compare `tones.png` with the painting. Fill dark cloth with `add` polygons, and raise `holeArea` where the cloth folds are near the canvas colour.
4. Draw the regions on a gridded view of `tones.png`, until `regions.png` has no magenta worth a part. A rope or lacing cut by colour is a `tones` region (no base layer).
5. Write the parts: rigid pieces `pin`/`seg`/`attach`; far limbs as the near regions with `far`; kusazuri `Plates`, rope `Tassel`, cloth `Tails` or `Cape`, each with 2 or 3 chains root first. A painted weapon uses `blade`, with `across` if it must be stretched to the family's reach. Name a painted cleaver `Cleaver_Blade` so `CleaverVisual` keeps it. Other families' visuals draw over a painted weapon until they learn the same rule.
6. The part set is `Skeleton`, `OrderArt.Dress`, `SvgRig.Build(d, SvgRigData.Load(<resource>), OrderArt.Style)`.
7. Copy `OrderIroncladSvgTests` with the foe's table, and `IroncladSvgScreenshots` with its framing. Render the line-up before your changes and compare tile for tile after.
8. Re-run the tool after any change to the SVG, the config, the painting or the tool.

**Tests.**
- EditMode `OrderIroncladSvgTests`:
  - the cut is fresh; every polygon is closed, simple, triangulates and is bigger than half a speck;
  - every part on its bone, the belt at the waist, the sode from the shoulder;
  - the soft parts on chains with their presets, and they are exactly the soft parts;
  - the Order palette, the crimson rim, lacing, rope and crest, the black face, the glowing white eye;
  - the `across` factor of a `blade` map;
  - the painted cleaver's front on the hit line (0.979 m), the hit line's end and middle on the blade, a 0.2 to 0.4 m broad blade, a handle past the near grip;
  - `CleaverVisual` adds nothing to the Ironclad and still draws for Kage;
  - bulkier than the Footman (dō a third wider on screen, thighs and boots a fifth);
  - `NeighbouringLimbPiecesOverlapAtEveryJointInEveryPose` (ten joints, ten poses);
  - a walk and stop swing the hakama, tassel and lames (lames least); a head wind streams the hakama and tassel and only stirs the lames, and calm repeats.
- Retired: `IroncladShapeTests`. Its seven cases (`EveryPartOutlineIsClosedSimpleAndTriangulatesWhole`, `EveryPaintedPolygonHasAnArea`, `EveryPartRidesTheBoneItsPieceMovesWith`, `TheBodyArmourAndHeadAreDrawnInCurvesNotBlocks`, `TheKusazuriArePlatesOnSpringChains`, `TheShouldersCarryAPlateEachAndTheHeadHasTwoHorns`, `TheDoAndTrunkAreStillWiderThanTheFootmans`) read the smooth port's part names and cel outlines; the new tests cover the same ground on the cut.
- Changed:
  - `OrderIroncladTests.ItsPartSetIsRegisteredAndKeepsKagesSkeleton` expects exactly `Cleaver_Handle` and `Cleaver_Blade` on the Sword bone, where it expected nothing;
  - PlayMode `OrderIroncladFightTests.ItIsSpawnedFromCharacterDataWithTheCleaverSetAndTheJsStats` expects the painted cleaver shown and no family parts, where it expected family parts over a hidden katana.
- The `OrderIroncladTests` width checks are unchanged and pass.
- PlayMode `IroncladSvgScreenshots`. Review images are in `Screenshots/review/SAN-100/svg/`:
  - `side_by_side.png`;
  - `kage_vs_order_ironclad_sheet.png`;
  - `order_ironclad_closeup.png`, `order_ironclad_head.png` and `order_ironclad_cleaver.png`;
  - `soft_motion.png`;
  - `lineup_sheet.png`, with the tiles in `lineup/`;
  - the cut previews in `cut/`.
- Pixel check: every other line-up tile (the four other Order foes, the four Hollows), Raikon and Kage in motion, and Kage holding Onikiri and the Raider Cleaver are identical to a render made before the change.

## Implementation notes (SAN-71 SVG redesign)

The owner, on the region-cut Raikon: "this needs full redesign as the unmovable parts of the character needs to be taken from svg and movable parts like hair and coat which responds to wind need to follow the physics and character definition needs to be increased." Raikon is now cut from his trace with the SAN-76 pipeline, the fourth SVG rig. His part set is three lines (`KageModel.Skeleton("RAIKON")`, `RaikonArt.Dress`, `SvgRig.Build`). Nothing of him is drawn in code.

**Source.** Unchanged: `Tools/raikon_art/original/Raikon.svg` (976 x 1230, seven colours, one even-odd compound path each) and `Raikon.png` (the 1117 x 1408 painting, the same picture at 1.1445 times; the tool resizes it to the SVG).

**Pipeline additions** (`svg_rig.py`; each is off unless a config sets it, so the Shade's, the Husk's and the Ironclad's cuts come out the same and were re-cut for the new tool hash only):
- `silhouette.grow` (px): the silhouette grown in the fill tone. Raikon's trace paints the canvas and the figure's outline in one black (#010000, set as `background`), so the painting's silhouette stops inside the outline; 1.5 px gives it back.
- A region's `painting: [tones]`: its samples take the nearest of those tones to the painting's colour (CIELAB; the painting resampled to the cut's grid and blurred by `paintingBlur`, 0.6 px), then the cut is despecked again. The trace breaks soft gradients into mottled islands: the ponytail was hundreds of black and grey specks, the fur blotches, the face plain canvas black. The ponytail, fur and face now read the painting's strands and the lit profile in the same seven tones; the ponytail keeps 4 ink islands under 30 px² (the trace's had dozens). The shapes are still the trace's: the region outline is the trace's silhouette.
- A region's `inpaint: {under, axis, reach}`: the samples of its rough polygon that the `under` regions draw are added to it, each in the tone of its own nearest sample along `axis` within `reach` px. The apron runs on under the tachi, the cord tassel and the far fist (straight down: its panels and red edges are vertical); the skirt under the tachi, hilt and near hand; the torso across under the arm (its lames are horizontal); the cape down under the arm. SAN-71's first cut did this for the skirt alone (`regions_patches.png`).
- `previewSvg`: the trace itself at 1x (`cut/svg.png`), which the review sheets set the rig beside.

**Config** (`Tools/raikon_art/rig.json`).
- The seven trace colours are seven tones: `Ink`, `Shadow`, `Cloth`, `Mauve`, `Pale`, `White`, `Crimson`. `RaikonArt.Style` paints them in the trace's own colours; far parts are one step darker along the trace's palette (white to pale, pale to mauve, mauve to cloth, cloth to shadow, shadow deeper, crimson deeper), SAN-71's rule. The rim (#C7181F) and body colour are SAN-71's.
- Fit k 0.688, SVG (501, 1195) on reference (878, 815): SAN-71's fit, so his soles and hips land where they did and the review compares before and after at one scale. Every trunk and head piece uses that fit (`trunk`): with it the painted shoulder, elbow, wrist, hip, knee and ankle already lie within a few px of the bones.
- Limbs `seg` joint to joint on SAN-71's painted joints: shoulder (545, 410), elbow (548, 528), wrist (542, 628), hip (505, 800), knee (518, 1000), ankle (520, 1118); hands and boots `attach` on their forearm or shin.
- Underlays (capsules) on every limb segment, a big `Shadow` one in the torso and the skirt, an ink one under the face, one under the hat's crown (the ponytail covers it in the painting) and one along the hilt. The forearm's starts below the elbow's bandage and the hand's sits inside the hand: a full-length one drew over the white wrap and the gauntlet's cuff.
- 27 regions, 2972 polygons.

**Raikon.**
- Rigid, cut from the SVG: the hat with its red veins, knot and bow; the face (black, with the painting's lit profile) and the glowing eye (2.6); the torso; the white rope and red braided belt; the skirt (kusazuri lames and the cloud-pattern haidate); the sode with the tomoe, the sleeve with its white bandage, the gauntlet, both hands; the hakama at the knee, the shin and boot; the tachi (hilt, and the blade with glow 1.7).
- On spring chains (`SoftPresets`): the cape (`Cape`, 3 chains), the ponytail (`Hair`, 2), the red ribbons flying from its tie (`Strip`, 3), the three hat tassels, the white chest tassel and the red cord tassel (`Tassel`, 1 each), the fur mantle (`Fur`, 6), the front apron of red-edged panels and the back coat panels (`Tails`, 3 each). `Hair` and `Fur` are new presets: SAN-71's ponytail feel a little looser, and its fur feel heavier so the tufts hang (a preset must sag and bend in the wind, `SoftChainWindTests`).
- Far side, the near pieces one step darker: the far sleeve, gauntlet and a far sode (`SodeF`, the old cut had none), the far thigh, shin and boot. The painting hides most of the far boot behind the near one and the far thigh under the skirt, so the painted far leg is not used.
- **Which hand holds the tachi.** The painting holds it in the near hand; the shared skeleton hangs the sword from the far hand. As before, the far arm is the sword arm, drawn from the painted near arm. Better than before: the sword hand is the painted far fist (the painting's free hand, a closed fist, now closed on the hilt), and the near hand is the painted hand that grips the tachi, which is where it lands in the two-handed stances (`Pose.Grip`, the near hand on the handle behind the far one). The first cut put the gripping hand on the far arm and the fist on the near.
- **The tachi** is the painting's, on the Sword bone by `blade 545 665 105 1005 0.95645`: the grip on the bone's pivot and the blade along Kage's `bladeBindAngle` at the fit's own scale, which is how SAN-71 placed it. `RaikonPartSet.PaintedBladeLength` (0.87721705, Raikon's `bladeLength`) is SAN-107's number, measured on the first cut in float32; it is kept as a constant so his reach does not move. A test checks the redrawn blade's tip still lies on the hit line's end (within 0.015).
- Unchanged: the skeleton, hurt circles, hit points, body scale 1.24, mass, posture, move rate, family, abilities, phases and stats.

**Where it differs from the painting, and why.**
- The skeleton stands Kage's way: in the bind pose and the fight idle the near leg is Kage's front leg (it steps forward about 70 painted px) and the far leg stands behind. The painted far boot is not drawn.
- Posed as painted, the tachi hangs from the far hand, whose shoulder sits about 60 painted px ahead of the near one, so it hangs lower and further forward than the painted one.
- The hair, fur and face take their tones from the painting (above); every other piece is the trace's.
- The painting's gradients are seven tones (the trace's own colours), and the cape's inside is the trace's ink.

**Editor tooling.** `Kage > Bake Raikon Art` is gone with the bake: an SVG rig needs none (the cut is text the game reads at spawn). `Kage > Check SVG Rig Cuts` (`KageEditor.SvgRigCuts.Check`) recomputes every cut's input hash and logs each stale one with the command that re-cuts it.

**Retired.** `RaikonModel`, `SvgArt` (the colour-trace importer; nothing else used it), `RaikonRegions.g.cs`, `Tools/raikon_art/regions.py`, `Resources/Art/Raikon/` (the SVG's byte copy and the `RaikonCut` bake), `Editor/RaikonArtBake.cs`. Tests: EditMode `RaikonArtTests` (bake freshness, the SVG byte copy, the old part names; its catalog, skeleton and ground cases moved to `RaikonSvgTests`) and `SvgArtTests` (the importer); PlayMode `RaikonScreenshots` (the old review sheets; `RaikonSvgScreenshots` replaces it). `com.unity.modules.vectorgraphics` stays in the manifest, now unused.

**Tests.**
- EditMode `RaikonSvgTests`: the cut is fresh; every polygon is closed, simple, triangulates and is bigger than half a speck (over 1500 of them); the catalog entry, scale and blade length; art never moves a gameplay joint; he stands on the ground and reaches 1.95 to 2.6 units; every part on its bone (a table, exactly these parts), the far arm and tachi on the sword layer, the belt at the waist; exactly the soft parts on chains with their presets and chain counts; the trace's seven colours (far parts darker); the face's lit profile, no black specks in the hair and fur, the hat's red veins; the far arm is the near one darker with its own sode, the sword hand the painted fist, the near hand the painted grip; the tachi's tip on the hit line's end; `NeighbouringLimbPiecesOverlapAtEveryJointInEveryPose` (ten joints, ten poses); a walk and a stop swing every soft part (the fur least) and leave the hat, torso, skirt, sode and tachi still; a head wind streams the cape, hair, ribbons and apron back, calm repeats and is the same as no wind set.
- `SoftChainWindTests`: the golden of SAN-71's cape (measured before wind existed) is replaced by `WithTheWindCalmACapeMovesExactlyAsWithNoWindSet` on Raikon's real rig; the presets case now also covers `Hair` and `Fur`.
- `RaikonFightDataTests` reads `RaikonPartSet.PaintedBladeLength` (it read `RaikonModel`'s); the expectation (0.875 ± 0.01) is the same.
- PlayMode `RaikonRigTests` is unchanged and keeps its part names (`Cape`, `Ponytail`, `Tassel1` to `3`, `Fur` soft; `Torso`, `Skirt`, `Hat` rigid; `Tachi_Blade` on the Sword bone under `HandF`); it was not run here (EditMode only).
- PlayMode `RaikonSvgScreenshots` (new): reads only the fit and the painted joints, so the same fixture rendered the first cut as the "before".
- Review images in `Screenshots/review/SAN-71/svg/`: `side_by_side.png` (the SVG, posed as painted, bind, fight idle at the SVG's scale); `before_after_head_hat.png`, `before_after_torso_mantle.png`, `before_after_arms_sword.png`, `before_after_legs_cape.png` (the SVG, the first cut and this one posed as painted, at 1.6 times) and `before_after_bind_and_idle.png`; `closeups/`; `kage_vs_raikon_sheet.png` (idle, walk, attack, hit), `raikon_poses.png` (twelve fight poses), `raikon_closeup.png`, `raikon_head.png`; `soft_motion.png` (a walk, stop and swing; a gusty head wind; a gusty tail wind); `lineup/` with `pixel_check.txt`; the cut previews in `cut/`.
- Pixel check: every other character's tile, standing and after a walk (the five Order foes, the four Hollows, Kuroe, Garan, Hisame, Shiden, the Ronin, the test dummies, Kage), is identical to a render made before the change.
