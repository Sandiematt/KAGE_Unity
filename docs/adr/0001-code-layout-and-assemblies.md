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
