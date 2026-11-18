# ADR-0009: Save schema and dev mode

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-62, SAN-64 (builds it), SAN-67, SAN-73, SAN-84, SAN-90, SAN-109

## Context

The spec says: the JS save schema as JSON in the persistent data path; a fresh save starts with Kagetsu, the starter helmet and the starter armour at level 1; dev mode is a **save-level flag**, on by default for now, that unlocks every stage, owns every weapon, helmet and armour and gives enough coins; turning it off starts the real JS economy **from a fresh save**.

The JS save (`Logic.defaultSave()`, schema version 2) is a nested object with dictionaries (`owned.weapons: { kagetsu: 1 }`, `cleared: { "act1.stretch1": true }`). Unity's `JsonUtility` can't read or write dictionaries. The JS game keeps dev mode on a second save key (`DEV_SAVE_KEY`), so the real save is never touched. The spec's "fresh save when dev mode is turned off" differs from that.

## Decision

### Schema

`SaveData` in `Kage.Rules` mirrors the JS default save field for field, with the same JSON names (camelCase), so a JS save and a Unity save read the same way:

```csharp
public sealed class SaveData
{
    public int v = 2;                       // JS SAVE_VERSION
    public int coins = 250, gems = 5, xp = 0, level = 1;
    public int ng = 0; public bool ngUnlocked; public int ngCompletions;
    public Dictionary<string, bool> cleared = new();     // stage id -> cleared this cycle
    public Dictionary<string, int> defeated = new();     // stage id -> wins
    public OwnedItems owned = new();                     // weapons / helmets / armors: id -> level
    public Equipped equipped = new();                    // weapon "kagetsu", helmet "shiro_maki", armor "tachibana_yoroi"
    public Dictionary<string, bool> seenRewards = new();
    public Dictionary<string, Dictionary<string, bool>> clues = new();
    public Dictionary<string, bool> seenScenes = new(), storyFlags = new();
    public Dictionary<string, string> bossFates = new();
    public string difficulty = "normal"; public bool difficultyPicked;
    public int trialsBest; public BossRushBest bossRushBest = new();
    public SaveStats stats = new();                      // wins, losses, perfects, bosses, playTime
    public SaveSettings settings = new();                // shake, reduceFlash, sound, music, touch, stick

    // Unity additions
    public bool devMode;                                 // dev mode (this ADR)

    [JsonExtensionData] public IDictionary<string, JToken> extra;  // fields we don't use are kept as they are
}
```

- Fields that are out of scope (clues, scenes, story flags, boss fates, New Game+, trials, boss rush) are kept and round-tripped, not used. A later act can use them without a schema change.
- `difficulty` stays `"normal"`. Nothing in this scope changes it.
- `v` stays 2 while the Unity schema only adds optional fields. A breaking change bumps it and adds a migration in `GameRules.NormalizeSave`.

### Serialisation and storage

- `SaveSerializer` (in `Kage.Rules`) turns `SaveData` into a JSON string and back, with Newtonsoft.Json. Loading runs `GameRules.NormalizeSave`, the port of JS `normalizeSave` for this scope: missing fields filled from defaults, unknown gear ids dropped, slots wearing something unowned put back on the starter item, unknown stage ids dropped from `cleared` and `defeated`.
- `SaveStore` (in `Kage.Runtime`, `Scripts/Save/SaveStore.cs`) owns the file: `Application.persistentDataPath/kage_save.json`. It writes to `kage_save.json.tmp`, then replaces the file, so a crash mid-write never leaves half a save.
- A missing file → a fresh save, written at once. A file that doesn't parse → its text is copied to `kage_save.json.corrupt`, then a fresh save replaces it, and a warning is logged. The game never crashes on a bad save (SAN-64 acceptance).
- The save is written after every change that matters: stage result, purchase, upgrade, equip, settings, dev-mode toggle.
- EditMode tests use `SaveSerializer` on strings and `SaveStore` on a temporary folder. They never touch the real persistent data path.

### Fresh save

`GameRules.DefaultSave(bool devMode)` returns the JS default save: 250 coins, 5 gems, level 1, 0 XP, owning and wearing Kagetsu, Shiro Maki (shown as "Crimson Scarf") and Tachibana Yoroi (shown as "Shinobi Garb"), all at level 1, plus `devMode`. During development `DevDefaults.DevModeOnFreshSave = true`, so a brand-new install starts in dev mode. JS parity tests call `DefaultSave(devMode: false)`.

### Dev mode

- **On:** `GameRules.ApplyDevUnlock(save)` (port of JS `applyDevUnlock`): every weapon, helmet and armour that isn't enemy-only is owned at level ≥ 1; coins ≥ 99,999 and gems ≥ 999 (JS `DEV`). `GameRules.IsStageUnlocked` returns true for every stage while `save.devMode` is set. Dev unlock is applied when dev mode is turned on and every time a dev-mode save is loaded, so new items appear too.
- **Off:** the current save is replaced by `DefaultSave(devMode: false)` and written. That is the real JS economy from a fresh start. Turning dev mode off asks for confirmation first, because the dev save's progress is lost.
- The switch is in Settings (SAN-67), plus a dev hotkey in the dev arena.

### Fight results

A finished stage hands `GameRules.CompleteStage(save, stageId, FightResult result)` a plain result: `win`, `hpLeft` (fraction), `time` (seconds), `maxCombo`, `parries`, `flawless`. It returns the rating (S/A/B/C, JS `rateFight`), coins, gems, XP and reward items, and updates the save. `FightStatsRecorder` builds the `FightResult` from the event stream (ADR-0003, SAN-73).

## Consequences

- A Unity save is a JS save plus `devMode`. JS rules-test fixtures can be used as-is.
- `Kage.Rules` depends on the Newtonsoft package (`com.unity.nuget.newtonsoft-json`, added by SAN-62).
- Dev mode and real progress never mix: turning dev mode off always starts clean. Unlike the JS game, there is one save file, not a separate dev save. That is the spec's choice.

## Notes from the implementation (SAN-90, the Armory and the save)

- Buying, upgrading and equipping change the save only through `GameRules.BuyItem`, `UpgradeItem` and `EquipItem` (coins, gems, `owned` levels, `equipped`), and `FlowController` writes the save (`SaveStore`) after each one that happened, so the file always matches the screen. A refused action (not enough coins or gems, a locked or owned item, the level cap, an item not owned) changes nothing and writes nothing.
- The next stage's fight and the pre-fight tableau read the loadout from that same save (`KageCharacter.Def(save)`, `LoadoutKey`), so the new gear is on Kage the moment the player is back on the loadout screen. In dev mode every item is owned, so the Armory shows Equip and Upgrade everywhere and nothing is Buy or Locked; the review images use a fresh save with a few items instead.
- The Armory's own selection (tab, row, tile) is screen state only; it is never saved.

