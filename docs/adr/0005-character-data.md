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
- Helmet parts are on the head bone, in draw orders 85 to 95 (over the hair at 84, under the eye at 96 and the scarf wrap at 100). A layer is one mesh of coloured polygons with a rim polygon each (like the coat); cloth tails are separate parts on a spring pivot (`PartDef.jigglePivot`), so they lag and swing with the head, not a cloth simulation. Pivots, parents and hurt circles are never touched.
- Hair under a helmet (`HairUnderHelmet`): hoods and caps (Kuro Zukin, Kasumi Veil, Hoshi Kabuto, Shikkō Zukin, Raijin Kabuto, Sumi Eboshi, Hisame Zukin) replace the hair part with a plain black head part named `Head`; kabuto and bands (Hachigane, Tetsu Kabuto, Oni Menpō, Shika Kabuto, Utsuro Kabuto) keep `Head_Hair` as it is, still moving; the Sugegasa keeps only the hair below its brim. This follows the JS drawings (`drawKageHair` under the kabuto, none under a hood).
- Art is authored in the JS head frame (head radii, +u to the face, +v down), as shape strings copied from the JS helmet drawings (`Scripts/Character/Gear/HelmetArt.cs`, looks in `HelmetLooks.cs`). The JS cel shading is flattened to one tone per shape; the rim light and a few lit details carry the form.

## Notes from the implementation (SAN-85, armour visuals)

- Same layering as the helmets: `CharacterDef.armor` is the equipped armour's JS id (`KageCharacter.Def(save)` fills it from `save.equipped.armor`). `KagePartSet.Build` builds Kage, recolours his trim, then calls `ArmorVisuals.Apply(rig, def.armor, ...)` and then `HelmetVisuals.Apply`; the armour goes first so the helmet's rim polygons take the armour's rim light. An armour with no drawing (null, the starter `tachibana_yoroi`, later-act or retired armours, an unknown id) changes nothing: the rig equals `KageModel.Build(Look.Kage)` part for part.
- Nine armours have a drawing: the seven shop armours (Ronin, Yami, Shinsen, Raikō, Kagehira, Yūrei, Mugen) and the two Act I trophies (Ikazuchi-dō, Shiden Yoroi). The ticket counts 11 ("starter, 8 shop, 2 rewards") but the data holds 8 non-retired armours that are not trophies **including** the starter, so there are 10 armours Kage can own by the end of Act I; the starter is Kage's own plain design and adds nothing.
- The seven shop armours are cuts of his own coat (JS `armory/_themes.js`): `ArmorLooks.Retone` recolours his trim, belt, coat cloth, rim light, hair's crimson strokes and scarf (`RigDef.scarfColor/scarfBack`, new `scarfEdge`), the hem gets teeth in the armour's kind (rags, shards, flames, tatters) and art along it (rope, tomoe, wards, lightning, flames, violet fire, spirit scrolls), plus a chest piece, a shoulder guard on the near upper arm, extra scarf tails (`RigDef.scarfStrands`, drawn by `ScarfRibbon` like the two main tails) and, for Mugen, a crescent halo behind the head. The two trophies add plates only (chest, sode, kusazuri); they keep his crimson.
- Pieces are authored in reference-image pixels (`ArmorArt`, the same numbers as `KageModel`). Chest pieces are parts on the chest bone at draw order 52 (over torso 44 and lapel 48, under belt 72 and scarf wrap 100), the sode is on `ArmNU` at 107 (over the upper arm and cuff, under the forearm 108), the halo is on the head at 82 (behind the hair at 84). Hem teeth, hem art and kusazuri are polygons appended to the coat's own cloth meshes, so they swing with the cloth. Nothing touches the skeleton, hurt circles, scarf anchors or coat cloth patches; no armour part is ever drawn over the eye.
- Not drawn: the JS hairstyle per armour (only the hair colour follows), glow/bloom on accents, the baked Godot flame, spirit and lightning sheets, and the Mugen card art (the code-drawn stand-in is used, as the JS does before the art has decoded). The far arm has no sode (it is behind the torso in this view).

## Notes from the implementation (SAN-71, Raikon art)

- **Source art:** the owner's files, untouched, are in `Tools/raikon_art/original/` (`Raikon.svg`, 976 × 1230, a 7-colour cutout trace with one even-odd compound path per colour; `Raikon.png`, the painting). The game reads a byte copy, `Assets/Kage/Resources/Art/Raikon/Raikon.svg.bytes` (renamed so Unity loads it as text; an EditMode test checks it matches the original).
- **Importer:** `SvgArt` (`Scripts/Character/SvgArt.cs`) reads a colour trace with Unity's built-in vector graphics module (`com.unity.modules.vectorgraphics`, `SVGParser`), one layer per colour in paint order. `Cut(region, cell)` clips every colour to a region (polygons in SVG px, even-odd) and tessellates it; with a cell size it cuts cell by cell so no triangle is bigger than a cell (soft parts bend smoothly). The trace's first colour (#010000) is both the canvas and the figure's ink, so it is set as the `backgroundLayer`: a cut paints the whole region in it and the other colours over it (the same picture, without tessellating the canvas's many-holed shape).
- **Regions:** because the black outline and the black face merge with the black canvas, the figure's outline is not in the file. `Tools/raikon_art/regions.py` (Python 3, numpy, opencv) finds it (every non-black colour, closed by 5 px, its enclosed black filled except the cape's tears, plus hand-marked black areas: face, neck; grown by a 2 px outline) and writes `Scripts/Character/PartSets/RaikonRegions.g.cs`: each part's region is its rough polygon inside that silhouette minus the parts that cover it in the painting (so nothing is drawn twice and moves as a ghost), with holes joined in by zero-width cuts. It also writes **fills**: flat underlays in the trace's colours for what the painting hides behind moving parts (torso behind the arms, hips and thighs under the skirt, the hilt under the hand), and a **patch** that paints the skirt back under the painted tachi by continuing each skirt column down. Previews: `Screenshots/review/SAN-71/regions.png` (each part tinted, magenta = figure no part draws) and `regions_patches.png`.
- **Bake:** cutting the whole figure takes about 4 s, so the Editor bakes it: `Kage > Bake Raikon Art` (or `-executeMethod KageEditor.RaikonArtBake.Bake`) writes every piece to `Resources/Art/Raikon/RaikonCut.bytes` (about 0.8 MB) with a hash of the SVG, the regions and a cut version. A spawn reads the bake (first build about 0.15 s, then cached); a stale bake is ignored with a warning (the SVG is cut instead) and the EditMode test `TheBakedCutIsUpToDateWithTheSvgAndTheRegions` fails until it is re-baked. Re-run `regions.py` and re-bake after changing the regions; bump `RaikonModel.CutVersion` when the cutting code changes.
- **On the skeleton:** `RaikonPartSet` → `RaikonModel.Build()` starts from `KageModel.Build(Look.Kage)` and keeps its pivots, parents, hurt circles, hit points and blade values (art never moves a gameplay joint); only the parts are Raikon's, at Kage's bind size (the 1.24 body scale is the spawn's transform scale, from `CharacterDef.FromJs`). The trunk (head, hat, ponytail, fur, torso, rope, cape, skirt) is one fit of the painting onto Kage's reference: 0.688 Kage reference px per SVG px, Raikon's soles (SVG y 1195) on the ground, his hips (SVG x 501) over Kage's; with that fit the painted shoulder, elbow, hand, knee and ankle land within a few pixels of the bones. Each limb segment is mapped so its drawn joints land exactly on the bones' pivots (rotation, and a stretch along the segment); hands and boots follow their forearm or shin. The drawn joints are constants at the top of `RaikonModel`.
- **Which arm holds the tachi:** the painting holds it in the near hand, the shared skeleton in the far hand (`Sword` hangs from `HandF`). So the far arm is the sword arm drawn from the painted near arm (sleeve with the elbow wrap, gauntlet, gripping hand), one step darker along the trace's palette (the reds a little deeper); the near arm keeps the painted shoulder plate (sode with the tomoe), sleeve and gauntlet and gets the painted far fist. The far leg is the painted near leg (with the buckle) one step darker. The painted far forearm and far boot are not used.
- **The tachi** is two parts on the `Sword` bone (`Tachi_Hilt`, `Tachi_Blade` with glow 1.7) plus a red grip underlay, all `swordLayer`, placed by the grip point in the painted hand on the sword bone's pivot and the blade along Kage's `bladeBindAngle`. The gripping hand (`HandF`) is placed with the tachi, so the hand closes around the hilt. The drawn tachi reaches about 0.84 bind units from the grip (Kagetsu's blade: 0.80); the Blade hit points still come from Kage's blade values and the family's `bladeLength` (SAN-81 sets the tachi's).
- **Parts and depth** (order slot ×4, Kage's slots so `swordLayer` works the same): Cape 0 and Ponytail 0 (behind everything), far arm 1–2, tachi and sword hand 4, far leg 5–7, hips fill 8, torso fill 10, Torso 11, near leg 13–15 (thigh is a fill), skirt patch and Skirt 16, Rope 18, Head and glowing Eye 21, Fur 22, Hat and three Tassels 24, near arm 26–28 (sleeve, sode over it, gauntlet, fist). Every cut part is one indexed mesh in bind space (`PartDef.mesh`, now honoured by `FighterRig`), no rim polygons: the trace carries its own outline.
- **Soft parts:** cape, ponytail, the three hat tassels and the fur mantle are `SoftKind.Chain` parts: `SoftChains` (in `SoftBody.cs`) runs verlet spring chains pinned at their roots to the bone (inertia, gravity, air drag trailing the body's motion and a breeze behind the fighter, shape matching that weakens to the tip, ground clamp, teleport reset); every vertex is bound to its two nearest chains and follows their position and turn. Each part has its own `ChainFeel` (stiffness at root and tip, drag, gravity, breeze); the chains are drawn polylines in SVG px in `RaikonModel`. `FighterRig` ticks them with the hair and coat and resets them in `ResetSoft`.
- **Shared files touched:** `FighterRig` (honours `PartDef.mesh`; glow on mesh/poly parts; builds, ticks and resets `SoftChains`), `RigDef` (`SoftKind.Chain`, `ChainFeel`, `PartDef.chains`, `PartDef.chainFeel`), `SoftBody.cs` (`SoftChains`), `MeshKit.Shifted`, `CharacterCatalog` (two lines). Kage and the Ronin draw exactly as before.
- **Roster:** `Roster/Raikon.cs` is `CharacterDef.FromJs("raikon")` with his part set, family `tachi` and effect colours. Mass, posture, moves, phases and abilities are SAN-81 / SAN-107; until the tachi family is registered he fights with the katana set (`EquipWeapon` logs a warning).
- **Review images:** `Screenshots/review/SAN-71/raikon_side_by_side.png` (painting, bind pose, fight idle), `raikon_poses.png`, `raikon_soft_motion.png`, tiles in `tiles/` (`RaikonScreenshots`, needs a GPU).

## Consequences

- Every opponent is one character file, one part-set file and two catalog lines, so opponent tickets don't collide.
- Kage stays one entry. His health and defence come from the rules and his save, so gear changes him with no special case.
- "Speed" now means two separate things: `speedMul` (moving around) and `moveRate` (how fast attacks play). The glossary keeps them apart.
