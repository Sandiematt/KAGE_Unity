using System;

namespace Kage.Rules
{
    // Buy, upgrade and equip (JS buyItem, upgradeItem, equipItem). Each checks the rule, updates the save in place and
    // returns whether it happened.
    public static partial class GameRules
    {
        static int GearMax(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapons: return JsTables.GearMaxWeapons;
                case GearKind.Hairstyles: return 1;
                default: return JsTables.GearMaxArmors;
            }
        }

        /// <summary>Highest level an item can reach. Trophies and boss weapons are fixed at level 1.</summary>
        public static int MaxLevel(GearKind kind, string id)
        {
            var it = Item(kind, id);
            string tier = it != null && it.Gear != null ? it.Gear.Tier : it?.Tier;     // Trophy gear stays at level 1.
            return it != null && (tier == "trophy" || tier == "boss") ? 1 : GearMax(kind);
        }

        /// <summary>Coins to take an owned item from <paramref name="level"/> to the next level.</summary>
        public static int UpgradeCost(GearKind kind, string id, int level)
        {
            if (kind == GearKind.Hairstyles) return 0;
            var item = Item(kind, id);
            // a weapon upgrades at its JS price, not the shop price Unity's tiers gave it: Kagetsu, the old starter (JS price 0), now
            // sold, still upgrades exactly as it did, and Mumei, the new starter, as the old starter did
            int price = item.Weapon != null && WeaponPrices.ContainsKey(id) ? item.Weapon.Price : item.Price;
            double basis = price > 0 ? price : item.Gems > 0 ? item.Gems * 60 : 500;
            return JsRound(Math.Max(160, basis * (0.35 + 0.3 * level)) / 10) * 10;
        }

        /// <summary>Buys an item at level 1. An item may cost coins, gems or both. Locked, enemy-only and owned items can't be bought.</summary>
        public static bool BuyItem(SaveData save, GearKind kind, string id)
        {
            var it = Item(kind, id);
            if (it == null || Owns(save, kind, id) || it.Exclusive || it.Enemy) return false;
            if (save.coins < it.Price || save.gems < it.Gems) return false;
            save.coins -= it.Price;
            save.gems -= it.Gems;
            OwnedOf(save, kind)[id] = 1;
            return true;
        }

        /// <summary>Raises an owned item one level for coins, up to its cap.</summary>
        public static bool UpgradeItem(SaveData save, GearKind kind, string id)
        {
            if (kind == GearKind.Hairstyles) return false;
            int level;
            if (!OwnedOf(save, kind).TryGetValue(id, out level) || level <= 0 || level >= MaxLevel(kind, id)) return false;
            int cost = UpgradeCost(kind, id, level);
            if (save.coins < cost) return false;
            save.coins -= cost;
            OwnedOf(save, kind)[id] = level + 1;
            return true;
        }

        public static bool CanEquip(SaveData save, GearKind kind, string id)
        {
            return !string.IsNullOrEmpty(id) && Owns(save, kind, id);
        }

        public static bool EquipItem(SaveData save, GearKind kind, string id)
        {
            if (!CanEquip(save, kind, id)) return false;
            switch (kind)
            {
                case GearKind.Weapons: save.equipped.weapon = id; break;
                case GearKind.Hairstyles: save.equipped.hairstyle = id; break;
                default:
                    save.equipped.armor = id;
                    break;
            }
            return true;
        }
    }
}
