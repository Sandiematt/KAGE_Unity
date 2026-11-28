using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>The three kinds of gear. The JS names (weapons, hairstyles, armors) are the save and table keys.</summary>
    public enum GearKind
    {
        Weapons,
        Hairstyles,
        Armors,
    }

    /// <summary>The fields every gear table row shares, whether it is a weapon, a hairstyle or an armor.</summary>
    public sealed class GearItem
    {
        public GearKind Kind;
        public string Id, Name, Tier, Hint;
        public int Price, Gems;
        public bool Exclusive, Enemy, NgOnly, Retired;
        /// <summary>Set for weapons.</summary>
        public JsWeapon Weapon;
        /// <summary>Set for armours.</summary>
        public JsGear Gear;
    }

    // Gear access shared by the economy and reward rules.
    public static partial class GameRules
    {
        /// <summary>The JS key of a gear kind: "weapons", "hairstyles" or "armors".</summary>
        public static string KindName(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapons: return "weapons";
                case GearKind.Hairstyles: return "hairstyles";
                default: return "armors";
            }
        }

        /// <summary>The gear kind for a JS key ("weapons", "hairstyles", "armors").</summary>
        public static GearKind KindFromName(string name)
        {
            switch (name)
            {
                case "weapons": return GearKind.Weapons;
                case "hairstyles": return GearKind.Hairstyles;
                default: return GearKind.Armors;
            }
        }

        /// <summary>The legendary armors (5-tier rarity system): Yūrei Armor and Mugen Armor.</summary>
        public static readonly HashSet<string> LegendaryArmors = new HashSet<string> { "yurei_yoroi", "mugen_yoroi" };

        public static bool IsLegendaryArmor(string id) { return id != null && LegendaryArmors.Contains(id); }

        /// <summary>
        /// The 5-tier Act I armor rarity mapping (Starter/Common, Rare, Epic, Trophy, Legendary), elevating the top two armors
        /// (Yūrei Armor &amp; Mugen Armor) to Legendary tier while preserving existing IDs, prices, stats and unlock rules.
        /// </summary>
        public static readonly Dictionary<string, string> ArmorTiers = new Dictionary<string, string>
        {
            { "tachibana_yoroi", "starter" }, { "ronin_yoroi", "common" },
            { "yami_yoroi", "rare" }, { "shinsen_yoroi", "rare" },
            { "raiko_yoroi", "epic" }, { "kagehira_yoroi", "epic" },
            { "ikazuchi_do", "trophy" }, { "shiden_yoroi", "trophy" },
            { "yurei_yoroi", JsTables.Legendary.Id }, { "mugen_yoroi", JsTables.Legendary.Id },
        };

        /// <summary>Display order of the 10 Act I armors across the five rarity tiers.</summary>
        public static readonly string[] ArmorOrder =
        {
            "tachibana_yoroi", "ronin_yoroi",
            "yami_yoroi", "shinsen_yoroi",
            "raiko_yoroi", "kagehira_yoroi",
            "ikazuchi_do", "shiden_yoroi",
            "yurei_yoroi", "mugen_yoroi",
        };

        static string ArmorTierOf(JsGear g)
        {
            string t;
            if (ArmorTiers.TryGetValue(g.Id, out t)) return t;
            return g.Tier;
        }

        /// <summary>
        /// The owner's weapon tiers (2026-11-23): one common weapon (Mumei, the plain starter), rare from Kagetsu, then epic, and the last
        /// two legendary (Unity's <see cref="JsTables.Legendary"/>, the rainbow tier line). A shop weapon not named here keeps its JS tier.
        /// </summary>
        public static readonly Dictionary<string, string> WeaponTiers = new Dictionary<string, string>
        {
            { "mumei", "common" },
            { "kagetsu", "rare" }, { "tsubame", "rare" },
            { "shizuka", "epic" }, { "onikiri", "epic" }, { "mizuchi", "epic" },
            { "yamigama", JsTables.Legendary.Id }, { "iwakudaki", JsTables.Legendary.Id },
        };

        /// <summary>Prices that moved with the tiers: Kagetsu is no longer the free starter (Mumei is), so it is sold.</summary>
        public static readonly Dictionary<string, int> WeaponPrices = new Dictionary<string, int> { { "kagetsu", 300 } };

        /// <summary>The weapon every new save starts with and every save always owns.</summary>
        public const string StarterWeaponId = "mumei";

        static string WeaponTier(JsWeapon w) { string t; return WeaponTiers.TryGetValue(w.Id, out t) ? t : w.Tier; }
        static int WeaponPrice(JsWeapon w) { int p; return WeaponPrices.TryGetValue(w.Id, out p) ? p : w.Price; }

        /// <summary>The table row for an item, or null for an unknown id.</summary>
        public static GearItem Item(GearKind kind, string id)
        {
            if (kind == GearKind.Weapons)
            {
                var w = JsTables.Weapon(id);
                if (w == null) return null;
                return new GearItem
                {
                    Kind = kind, Id = w.Id, Name = w.Name, Tier = WeaponTier(w), Hint = w.Hint, Price = WeaponPrice(w), Gems = w.Gems,
                    Exclusive = w.Exclusive, Enemy = w.Enemy, Weapon = w,
                };
            }
            if (kind == GearKind.Hairstyles)
            {
                string name = Hairstyles.Name(id);
                return name == null ? null : new GearItem { Kind = kind, Id = id, Name = name };
            }
            var g = JsTables.Armor(id);
            if (g == null) return null;
            return new GearItem
            {
                Kind = kind, Id = g.Id, Name = g.Name, Tier = ArmorTierOf(g), Hint = g.Hint, Price = g.Price, Gems = g.Gems,
                Exclusive = g.Exclusive, Enemy = g.Enemy, NgOnly = g.NgOnly, Retired = g.Retired, Gear = g,
            };
        }

        /// <summary>The owned items of one kind: id to level.</summary>
        public static Dictionary<string, int> OwnedOf(SaveData save, GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapons: return save.owned.weapons;
                case GearKind.Hairstyles: throw new System.ArgumentException("Hairstyles have no inventory levels.", nameof(kind));
                default: return save.owned.armors;
            }
        }

        public static bool Owns(SaveData save, GearKind kind, string id)
        {
            if (kind == GearKind.Hairstyles) return Hairstyles.Name(id) != null;
            int lvl;
            return OwnedOf(save, kind).TryGetValue(id, out lvl) && lvl > 0;
        }

        /// <summary>
        /// Locked gear: never sold (exclusive) and not owned yet. Boss weapons and trophies stay locked until a stage
        /// reward grants them.
        /// </summary>
        public static bool IsLocked(SaveData save, GearKind kind, string id)
        {
            var it = Item(kind, id);
            return it != null && it.Exclusive && !Owns(save, kind, id);
        }

        /// <summary>What the Armory says unlocks a locked item.</summary>
        public static string LockHint(GearKind kind, string id)
        {
            var it = Item(kind, id);
            return it == null || string.IsNullOrEmpty(it.Hint) ? "Not obtainable yet" : it.Hint;
        }
    }
}
