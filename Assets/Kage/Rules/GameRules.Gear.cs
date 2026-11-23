using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>The three kinds of gear. The JS names (weapons, helmets, armors) are the save and table keys.</summary>
    public enum GearKind
    {
        Weapons,
        Helmets,
        Armors,
    }

    /// <summary>The fields every gear table row shares, whether it is a weapon, a helmet or an armour.</summary>
    public sealed class GearItem
    {
        public GearKind Kind;
        public string Id, Name, Tier, Hint;
        public int Price, Gems;
        public bool Exclusive, Enemy, NgOnly, Retired;
        /// <summary>Set for weapons.</summary>
        public JsWeapon Weapon;
        /// <summary>Set for helmets and armours.</summary>
        public JsGear Gear;
    }

    // Gear access shared by the economy and reward rules.
    public static partial class GameRules
    {
        /// <summary>The JS key of a gear kind: "weapons", "helmets" or "armors".</summary>
        public static string KindName(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapons: return "weapons";
                case GearKind.Helmets: return "helmets";
                default: return "armors";
            }
        }

        /// <summary>The gear kind for a JS key ("weapons", "helmets", "armors").</summary>
        public static GearKind KindFromName(string name)
        {
            switch (name)
            {
                case "weapons": return GearKind.Weapons;
                case "helmets": return GearKind.Helmets;
                default: return GearKind.Armors;
            }
        }

        /// <summary>
        /// The legendary helmets (owner's call, after the SAN-72 helmet tiers): Utsuro Kabuto, Shikko Zukin and Raijin Kabuto. Their
        /// tier is Unity's <see cref="JsTables.Legendary"/> whatever the JS row says, so the Armory lists them last; they give the
        /// best defence and health of every helmet in the tables (<see cref="GearDef"/>, <see cref="GearHp"/>).
        /// </summary>
        public static readonly HashSet<string> LegendaryHelmets = new HashSet<string> { "utsuro_kabuto", "shikko_zukin", "raijin_kabuto" };

        public static bool IsLegendary(GearKind kind, string id) { return kind == GearKind.Helmets && id != null && LegendaryHelmets.Contains(id); }

        /// <summary>The table row for an item, or null for an unknown id.</summary>
        public static GearItem Item(GearKind kind, string id)
        {
            if (kind == GearKind.Weapons)
            {
                var w = JsTables.Weapon(id);
                if (w == null) return null;
                return new GearItem
                {
                    Kind = kind, Id = w.Id, Name = w.Name, Tier = w.Tier, Hint = w.Hint, Price = w.Price, Gems = w.Gems,
                    Exclusive = w.Exclusive, Enemy = w.Enemy, Weapon = w,
                };
            }
            var g = kind == GearKind.Helmets ? JsTables.Helmet(id) : JsTables.Armor(id);
            if (g == null) return null;
            return new GearItem
            {
                Kind = kind, Id = g.Id, Name = g.Name, Tier = IsLegendary(kind, g.Id) ? JsTables.Legendary.Id : g.Tier, Hint = g.Hint, Price = g.Price, Gems = g.Gems,
                Exclusive = g.Exclusive, Enemy = g.Enemy, NgOnly = g.NgOnly, Retired = g.Retired, Gear = g,
            };
        }

        /// <summary>The owned items of one kind: id to level.</summary>
        public static Dictionary<string, int> OwnedOf(SaveData save, GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Weapons: return save.owned.weapons;
                case GearKind.Helmets: return save.owned.helmets;
                default: return save.owned.armors;
            }
        }

        public static bool Owns(SaveData save, GearKind kind, string id)
        {
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
