using System;
using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>The result of opening a save from its stored text (JS openSave).</summary>
    public sealed class OpenedSave
    {
        /// <summary>The save to play with. Never null.</summary>
        public SaveData Save;
        /// <summary>True when there was no stored text, so <see cref="Save"/> is a brand-new save that should be written.</summary>
        public bool Fresh;
        /// <summary>Why the stored text could not be read, or null.</summary>
        public string Error;
        /// <summary>The unreadable stored text, to copy aside before a fresh save replaces it, or null.</summary>
        public string CorruptText;
    }

    // Opening, repairing and dev-mode rules of the save (JS openSave, normalizeSave, applyDevUnlock; ADR-0009).
    // Pure: no file access. SaveStore (Kage.Runtime) reads and writes the file.
    public static partial class GameRules
    {
        static readonly string StarterWeapon = StarterWeaponId, StarterHelmet = "shiro_maki", StarterArmor = "tachibana_yoroi";

        // ---------------- opening ----------------

        /// <summary>
        /// A save for a brand-new install: the fresh JS save, in dev mode while <see cref="DevDefaults.DevModeOnFreshSave"/> is on.
        /// </summary>
        public static SaveData NewInstallSave()
        {
            var s = DefaultSave(DevDefaults.DevModeOnFreshSave);
            if (s.devMode) ApplyDevUnlock(s);
            return s;
        }

        /// <summary>
        /// Turns stored text (null or blank when nothing is stored) into the save to play with. Missing text gives a fresh
        /// save; text that is not a save gives a fresh save and reports the text so it can be backed up; a dev-mode save gets
        /// the dev grants again, so items added since appear.
        /// </summary>
        public static OpenedSave OpenSave(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return new OpenedSave { Save = NewInstallSave(), Fresh = true };

            SaveData save;
            string error;
            if (!SaveSerializer.TryDeserialize(stored, out save, out error))
                return new OpenedSave { Save = NewInstallSave(), Error = error, CorruptText = stored };

            if (save.devMode) ApplyDevUnlock(save);
            return new OpenedSave { Save = save };
        }

        // ---------------- repairing ----------------

        /// <summary>
        /// Fills a loaded save up to the current schema (JS normalizeSave, for this scope): missing fields come from the
        /// defaults, unknown, enemy-only and zero-level gear is dropped, a slot wearing something unowned goes back to the
        /// starter item, unknown stage ids are dropped from cleared and defeated, and negative amounts are raised to zero.
        /// Retired gear is refunded once and dropped. Works on the object it is given and returns it.
        /// </summary>
        public static SaveData NormalizeSave(SaveData s)
        {
            if (s == null) return DefaultSave(false);

            if (s.cleared == null) s.cleared = new Dictionary<string, bool>();
            if (s.defeated == null) s.defeated = new Dictionary<string, int>();
            if (s.seenRewards == null) s.seenRewards = new Dictionary<string, bool>();
            if (s.clues == null) s.clues = new Dictionary<string, Dictionary<string, bool>>();
            if (s.seenScenes == null) s.seenScenes = new Dictionary<string, bool>();
            if (s.storyFlags == null) s.storyFlags = new Dictionary<string, bool>();
            if (s.bossFates == null) s.bossFates = new Dictionary<string, string>();
            if (s.bossRushBest == null) s.bossRushBest = new BossRushBest();
            if (s.stats == null) s.stats = new SaveStats();
            if (s.settings == null) s.settings = new SaveSettings();
            if (s.owned == null) s.owned = new OwnedItems();
            if (s.owned.weapons == null) s.owned.weapons = new Dictionary<string, int>();
            if (s.owned.helmets == null) s.owned.helmets = new Dictionary<string, int>();
            if (s.owned.armors == null) s.owned.armors = new Dictionary<string, int>();
            if (s.equipped == null) s.equipped = new Equipped();
            var defaults = new SaveSettings();
            if (s.settings.shake == null) s.settings.shake = defaults.shake;
            if (s.settings.touch == null) s.settings.touch = defaults.touch;
            if (s.settings.stick == null) s.settings.stick = defaults.stick;
            if (string.IsNullOrEmpty(s.difficulty)) s.difficulty = "normal";
            foreach (var key in new List<string>(s.clues.Keys))
                if (s.clues[key] == null) s.clues[key] = new Dictionary<string, bool>();

            s.coins = Math.Max(0, s.coins);
            s.gems = Math.Max(0, s.gems);
            s.xp = Math.Max(0, s.xp);
            s.level = Math.Max(1, s.level);
            s.trialsBest = Math.Max(0, s.trialsBest);

            // the starter items are always owned
            EnsureOwned(s.owned.weapons, StarterWeapon);
            EnsureOwned(s.owned.helmets, StarterHelmet);
            EnsureOwned(s.owned.armors, StarterArmor);

            foreach (GearKind kind in new[] { GearKind.Weapons, GearKind.Helmets, GearKind.Armors })
            {
                var owned = OwnedOf(s, kind);
                foreach (var id in new List<string>(owned.Keys))
                {
                    var item = Item(kind, id);
                    int level = owned[id];
                    // retired shop gear pays back what it cost, once; it is dropped below, so the next load finds nothing
                    if (item != null && item.Retired && level > 0)
                    {
                        s.coins += item.Price;
                        s.gems += item.Gems;
                        for (int l = 1; l < Math.Min(level, MaxLevel(kind, id)); l++) s.coins += UpgradeCost(kind, id, l);
                    }
                    if (item == null || item.Enemy || id == "fists" || level <= 0) owned.Remove(id);
                }
            }

            // a slot wearing something unowned or unknown goes back to the starter item
            if (!Owns(s, GearKind.Weapons, s.equipped.weapon ?? "")) s.equipped.weapon = StarterWeapon;
            if (!Owns(s, GearKind.Helmets, s.equipped.helmet ?? "")) s.equipped.helmet = StarterHelmet;
            if (!Owns(s, GearKind.Armors, s.equipped.armor ?? "")) s.equipped.armor = StarterArmor;

            // clears and defeats are keyed by stage id; keys the map doesn't know (old saves, removed stages) are dropped
            var cleared = new Dictionary<string, bool>();
            foreach (var kv in s.cleared) if (kv.Value && Stage(kv.Key) != null) cleared[kv.Key] = true;
            s.cleared = cleared;
            var defeated = new Dictionary<string, int>();
            foreach (var kv in s.defeated) if (kv.Value > 0 && Stage(kv.Key) != null) defeated[kv.Key] = kv.Value;
            s.defeated = defeated;

            return s;
        }

        static void EnsureOwned(Dictionary<string, int> owned, string id)
        {
            int level;
            if (!owned.TryGetValue(id, out level) || level <= 0) owned[id] = 1;
        }

        // ---------------- dev mode ----------------

        /// <summary>
        /// The dev unlock's grants (JS applyDevUnlock): every weapon, helmet and armour that isn't enemy-only or retired is
        /// owned (at level 1 unless already higher), coins are at least <see cref="DevDefaults.Coins"/> and gems at least
        /// <see cref="DevDefaults.Gems"/>. Nothing the save already has is lowered. Returns the save it was given.
        /// </summary>
        public static SaveData ApplyDevUnlock(SaveData save)
        {
            foreach (var w in JsTables.AllWeapons)
                if (!w.Enemy && w.Id != "fists") EnsureOwned(save.owned.weapons, w.Id);
            foreach (var h in JsTables.Helmets)
                if (!h.Enemy && !h.Retired) EnsureOwned(save.owned.helmets, h.Id);
            foreach (var a in JsTables.Armors)
                if (!a.Enemy && !a.Retired) EnsureOwned(save.owned.armors, a.Id);
            save.coins = Math.Max(save.coins, DevDefaults.Coins);
            save.gems = Math.Max(save.gems, DevDefaults.Gems);
            return save;
        }

        /// <summary>
        /// Switches dev mode. On: the save gets the dev flag and the dev grants, and is returned. Off: the real JS economy
        /// starts from a fresh save, so a new save replaces this one (the dev save's progress is lost; the caller confirms
        /// first). Turning it off when it is already off returns the save unchanged.
        /// </summary>
        public static SaveData SetDevMode(SaveData save, bool on)
        {
            if (on)
            {
                save.devMode = true;
                return ApplyDevUnlock(save);
            }
            return save.devMode ? DefaultSave(false) : save;
        }
    }
}
