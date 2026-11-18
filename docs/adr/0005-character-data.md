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

## Consequences

- Every opponent is one character file, one part-set file and two catalog lines, so opponent tickets don't collide.
- Kage stays one entry. His health and defence come from the rules and his save, so gear changes him with no special case.
- "Speed" now means two separate things: `speedMul` (moving around) and `moveRate` (how fast attacks play). The glossary keeps them apart.
