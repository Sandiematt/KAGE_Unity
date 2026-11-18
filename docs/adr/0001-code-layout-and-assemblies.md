# ADR-0001: Code layout, assemblies and the Fighter file map

- Status: Accepted (2026-11-17, SAN-111)
- Affects: every ticket; first written by SAN-61, SAN-62, SAN-63, SAN-64

## Context

All game code compiles into `Assembly-CSharp` with no assembly definitions. Unity test assemblies can't reference `Assembly-CSharp`, so neither the fight tests (SAN-61) nor the rules tests (SAN-62) can be written. The spec also wants the game rules as pure C# with no scene dependency.

`Fighter.cs` (1,329 lines) and `KageGame.cs` are edited by almost every combat ticket. Parallel work needs a fixed place for each concern, so that two tickets in the same wave don't edit the same file.

## Decision

### Assemblies

| Assembly | Folder | Namespace | Platforms | References |
|---|---|---|---|---|
| `Kage.Rules` | `Assets/Kage/Rules/` | `Kage.Rules` | any | `noEngineReferences: true`; `Newtonsoft.Json.dll` (precompiled, from `com.unity.nuget.newtonsoft-json`) |
| `Kage.Runtime` | `Assets/Kage/Scripts/` | `Kage` | any | `Unity.InputSystem`, `Unity.RenderPipelines.Core.Runtime`, `Unity.RenderPipelines.Universal.Runtime`, `UnityEngine.UI`, `Kage.Rules` |
| `Kage.Editor` | `Assets/Kage/Editor/` | `KageEditor` (as today) | Editor only | `Kage.Runtime`, `Kage.Rules`, `Unity.RenderPipelines.Universal.Runtime` |
| `Kage.Tests.EditMode` | `Assets/Kage/Tests/EditMode/` | `Kage.Tests` | Editor only | `Kage.Rules`, `Kage.Runtime`, test runner assemblies, `nunit.framework.dll`; `defineConstraints: ["UNITY_INCLUDE_TESTS"]` |
| `Kage.Tests.PlayMode` | `Assets/Kage/Tests/PlayMode/` | `Kage.Tests` | any | `Kage.Runtime`, `Kage.Rules`, test runner assemblies, `nunit.framework.dll`; `defineConstraints: ["UNITY_INCLUDE_TESTS"]` |

- `Kage.Rules` never references `Kage.Runtime`. Runtime code calls the rules; the rules never call Unity.
- Runtime code keeps the `Kage` namespace so existing files don't change.
- Test helpers (the scripted bots, the replay recorder, baseline comparison) live in `Tests/PlayMode/Harness/`. The fight builder itself is runtime code (`Scripts/Fight/FightBuilder.cs`), because the game also builds fights from data.
- Baseline replay files live in `Assets/Kage/Tests/PlayMode/Baselines/`, so they never ship in a build.
- `Assets/Welcome` keeps its own asmdef and is not touched.

Who creates which file (so no two tickets in the same wave create the same file):

- SAN-61 creates `Kage.Runtime`, `Kage.Editor` and `Kage.Tests.PlayMode`. It moves nothing; adding the asmdef files puts the existing folders into those assemblies. Script GUIDs stay the same, so scenes and prefabs keep their references.
- SAN-62 creates `Kage.Rules`, `Kage.Tests.EditMode`, and adds `com.unity.nuget.newtonsoft-json` to `Packages/manifest.json`. At that point `Kage.Tests.EditMode` references only `Kage.Rules`.
- SAN-64 adds `Kage.Rules` to the references of `Kage.Runtime` (the first runtime use is the save store). It also adds `Kage.Runtime` to `Kage.Tests.EditMode` if it needs it.

### Folders inside `Assets/Kage/Scripts/`

| Folder | Holds |
|---|---|
| `Fight/` | `FightContext`, `FightLoop`, `FightClock`, `FightRandom`, `CombatEvents`, `FightBuilder` (ADR-0002, ADR-0003) |
| `Combat/` | `Fighter.*.cs`, `MoveDef`, `FighterInput`, input sources, AI |
| `Combat/Families/` | one file per weapon family (ADR-0006) |
| `Character/` | rig, skeleton, pose, Kage's model (as today), `CharacterDef`, `CharacterCatalog` (ADR-0005) |
| `Character/Roster/` | one file per character entry |
| `Character/PartSets/` | one file per opponent part set |
| `Bosses/` | `BossDef`, `PhaseController`; `Bosses/Abilities/<boss>.cs` (ADR-0008) |
| `Stage/` | arena, road, stretch session, fight session |
| `VFX/` | `Vfx`, `EffectCues`, `Projectile`, `Hazard` (ADR-0007) |
| `Save/` | `SaveStore` (file IO for ADR-0009) |
| `UI/` | today's HUD; the JS-style screens go in `UI/Screens/` and `UI/Flow/` |
| `Core/` | as today, plus `Tuning` and `PhysicsLayers` |

### The Fighter file map

SAN-63 splits `Fighter` into these partial files. Later tickets put their code in the file named here, and add a new partial file for a new concern instead of growing an existing one.

| File | Contents |
|---|---|
| `Fighter.cs` | fields, `Setup`, `ResetForRound`, `SetState`, `Tick` dispatch, public properties |
| `Fighter.Locomotion.cs` | neutral, walk, run, dash, skid, jump, air, land, gravity, gait, stage clamp, facing |
| `Fighter.Attack.cs` | input buffer, `TryBuffered` (move selection), `StartMove`, `AttackUpdate` (chains, root motion, FX events) |
| `Fighter.HitDetection.cs` | hit points, hit sweeps, `TryTarget`, `OnHitResolved`, `ResolveExternal` |
| `Fighter.Reaction.cs` | `HittableBy`, `Overlap`, `ReceiveHit`, launch, knockdown, KO, `Stagger` |
| `Fighter.Guard.cs` | guard state and the guard branch of `ReceiveHit` (`TryGuard`) |
| `Fighter.Throw.cs` | grab, carry, release, `BeginThrown` |
| `Fighter.Specials.cs` | Mirror Guard counter, Crescent spawn, Thousand Shadows, and `TrySpecialDown()` (Special + Down, see ADR-0004) |
| `Fighter.Pose.cs` | `LateUpdate` pose output, blends, gait and air poses, trails |
| `Fighter.Fall.cs` | physical falls (SAN-113): the tethered ragdoll for launched and down, its drive, and the hand-back to the get-up |
| `Fighter.SwordBreak.cs` | sword break (SAN-113): `SwordBroken`, `BreakSword`, `ResetForFight`, the unarmed move filter and fist guard |

New partial files expected later: `Fighter.Posture.cs` (SAN-86), `Fighter.Parry.cs` (SAN-91), `Fighter.PerfectDodge.cs` (SAN-92), `Fighter.Armour.cs` (the first of SAN-80 / SAN-102), `Fighter.Targets.cs` (SAN-83), `Fighter.Kunai.cs` (SAN-88), `Fighter.RageArt.cs` (SAN-82).

Hook points that let parallel tickets avoid each other's files are fixed in ADR-0004.

### Entry points

- `KageGame` stays as the **dev arena**: duel and training dojo, dev hotkeys, and the place where each new opponent or weapon is tried in the Editor. Its fight setup moves into `FightBuilder` (SAN-61, SAN-66).
- `GameFlow` (SAN-67) is the **campaign entry**: title → campaign map → pre-fight loadout → stage → results → map. It lives in new files and a new scene, builds fights through `FightBuilder`, and never edits `KageGame.cs`.
- SAN-109 decides whether `KageGame` shrinks further once the campaign runs end to end.

### Where data lives

- Move data stays in C# (the fluent `MoveDef` API).
- Character, weapon family, boss and ability entries are C# objects built in code (ADR-0005, ADR-0006, ADR-0008).
- JS content tables are generated C# (ADR-0010). Never edit them by hand.
- Tuning numbers that the owner wants to change without code (defence windows, posture rates, health scale, legacy switch) live in JSON files under `Assets/Kage/Resources/Tuning/<feature>.json`. Each file maps to one plain C# class with `[Serializable]` fields and in-code defaults, loaded with `Tuning.Get<T>()` (JsonUtility; a missing file means defaults). One file per feature (`combat.json`, `guard.json`, `parry.json`, `dodge.json`, `clash.json`, `health.json`, ...), so tickets don't edit each other's tuning. SAN-65 creates the `Tuning` loader (its legacy switch is the first entry).

### Shared files with one-line registrations

Some files are edited by many tickets, one line each: `CharacterCatalog.cs`, `WeaponFamilies.cs`, `BossCatalog.cs`, dev hotkeys in `KageGame.cs`, and reset calls in `Fighter.ResetForRound`. These are allowed in parallel. The second ticket to finish merges `main` and keeps both lines.

## Consequences

- Test assemblies can now reference game code, and the rules can be tested in EditMode with no scene.
- `Kage.Rules` can't use `UnityEngine` (no `Mathf`, no `Debug`, no `JsonUtility`). That is deliberate: it forces the JS port to stay a pure port.
- Everyone can tell where a change goes, and wave planning can check file overlap by file name.
- Adding a package or a referenced Unity assembly means editing an asmdef. Only the ticket named above edits each asmdef in its wave.

## Notes from the implementation (SAN-90, the Armory)

- **Three layers, as for the map and the results.** `ArmoryView` (plain data from a save, a tab, a selection and a row: the tiles, the selected item's tier, level, stats with the next-level preview, perks or rage art, lock hint, and what the Buy / Upgrade and Equip buttons say and whether they work), `ArmoryController` (the state, and `Buy`, `Upgrade`, `Equip` straight through `GameRules.BuyItem / UpgradeItem / EquipItem`; each success is saved at once and announced, a refusal changes nothing) and `ArmoryScreen` (the uGUI that only draws the view). `FlowController` owns an `ArmoryController` while `FlowScreen.Armory` is up (`OpenArmory()`, `Armory`, `ArmoryFrom`).
- **Where it is reached, as in the JS game.** The title's Armory row and tile, the loadout's *Change gear* button (the gold-edged tile after the Mode cell) and the *Upgrade* button on a defeat's results. Back returns to the screen it came from (`ArmoryFrom`: Title, Loadout or Results); the stage and the results are untouched meanwhile. The map, a fight and Settings can't open it. The JS Upgrade button sits between Retry and Map, so a defeat now has three buttons.
- **The preview is a real Kage.** `ArmoryWorld` builds fighters through `FightBuilder` from `KageCharacter.Def(ArmoryView.PreviewSave(save, tab, id))`: the preview save is the equipped loadout with the selection swapped in (the Armor cards wear the Crimson Scarf, like the JS cards), so the helmet layers (SAN-72), the armour layers (SAN-85), the weapon family's blade and grip (SAN-70, every family) are exactly what a fight would show. The world has its own camera framing per tab (whole body on the weapons tab, head and shoulders on the helmets tab, a row of whole figures on the armour cards) and puts the stage world away while it is open (the stage world is built again on the way back).
- **Backdrop behind the fighters, panels in front.** The fighters are world objects, the flow's panels are an overlay canvas, so the Armory's backdrop (sky, sun, hills, torii, the cards' dark grounds) is drawn into a second, camera-space canvas (`ArmoryWorld.BackRoot`, sorting order -5000) that the world camera draws behind every fighter. `ArmoryScreen` builds into both on each rebuild; the back canvas's objects are put on layer 0 (the world camera does not draw layer 5).
- **Debounce.** Changing the selection builds the new Kage after 0.12 s of no change (arrow keys repeat faster than a fighter builds); picking another armour card keeps the row's fighters (the JS cards do too).
- **Navigation.** Same `NavGroup` as every screen: arrows, WASD, d-pad and stick move the focus, Enter / Space / J / A press, Esc / K / B go back. Focusing a tile or card selects it, so the stats update while browsing; Q / E and the shoulder buttons turn the tabs (menu-only: `MenuInput` is not the fight's `KeyBindings`, so RB's use as Guard in fights doesn't touch it). The tile row scrolls to keep the focused tile in view.
- **Not like the JS screen.** The weapons tab shows Kage holding the weapon (the ticket asks for the weapon on Kage) instead of the JS ghost warrior behind a large illustration of the blade; helmet tiles show a kanji, not a drawn head (the JS draws a canvas icon per helmet); the armour cards are live Kages without the JS rim-light outline, and the trophy row (10 cards) is narrower so it fits instead of scrolling; the lore is not italic (no italic Mincho). The rage-art box shows the JS special's name and text for the weapon (`ArmoryText`), which the JS tables don't export; the Unity rage arts per family are a later ticket.
- **Tests.** `ArmoryViewTests` (listing and order, selection rules, tiers, stats and previews against the JS Armory's own numbers, prices in coins and gems, lock hints, perks wording, preview loadouts), `ArmoryControllerTests` (buy, upgrade, equip through the rules with coins, gems and saves; refusals change and save nothing; tabs, rows, selection), `ArmoryFlowTests` (the flow: from the title, loadout and results, Back, saves), and in PlayMode `GearArmoryFlowTests` (keyboard commands and the gamepad through the real `GameFlow`, the live preview wearing the selected weapon, helmet and armour, purchases in the save file, Change gear and Upgrade round trips). Review images: `Screenshots/review/SAN-90/` (`js`, `unity`, `side_by_side`; `GearArmoryScreenshots`, needs a GPU; the JS images were taken from the JS game on the same mid-game save with the built `index.html` in a browser at 1600 x 900).

