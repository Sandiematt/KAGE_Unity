using System;
using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>The damage, speed and reach the Armory shows for a weapon at a level.</summary>
    public struct WeaponStatLine
    {
        public int Damage, Speed, Reach;
    }

    /// <summary>The combat multipliers of a weapon at a level, relative to the weapon whose forms it fights with.</summary>
    public struct WeaponMultipliers
    {
        public double Dmg, Spd, Reach;
    }

    /// <summary>Everything the equipped gear does for Kage in a fight (JS playerCombatStats), in JS health units.</summary>
    public sealed class PlayerGearStats
    {
        /// <summary>Maximum health in JS units (before the health scale, ADR-0010).</summary>
        public int Hp;
        /// <summary>Damage reduction from helmet and armour, capped at 0.5.</summary>
        public double Defense;
        public double WeaponDmg;
        /// <summary>
        /// The damage multiplier Kage himself carries into a fight: +1.2 % per player level, times the gear's damage perk
        /// (JS <c>makePlayer</c>: <c>(1 + 0.012 × (level − 1)) × (1 + perks.dmg)</c>). It does not include the weapon (<see cref="WeaponDmg"/>).
        /// </summary>
        public double DamageMul;
        /// <summary>The summed helmet and armour perks (dash, speed, ammo, stunRes, rage, kunai, dmg, blockRage).</summary>
        public Dictionary<string, double> Perks = new Dictionary<string, double>();
    }

    // Weapon stats and multipliers, item tiers, gear defence and health, player health and defence (JS economy formulas).
    // Weapons: stats = [damage, speed, reach] at level 1; each level adds 8 % damage, 2.5 % speed and 1.5 % reach. A weapon's
    // combat damage multiplier is its damage over the damage of the weapon whose forms it uses, so a shop weapon starts at
    // x1 and a boss weapon on the same forms starts far above any upgraded shop weapon. Speed and reach are capped
    // (x1.25 and x1.2). Helmets and armour: defence grows 0.012 per level, health 10 % per level.
    public static partial class GameRules
    {
        const double GrowthDamage = 0.08, GrowthSpeed = 0.025, GrowthReach = 0.015;
        const double SpeedMulCap = 1.25, ReachMulCap = 1.2;
        const double GearDefPerLevel = 0.012, GearHpGrowthPerLevel = 0.1;
        const int PlayerBaseHealth = 220, PlayerHealthPerLevel = 4;
        const double PlayerDefenseCap = 0.5;
        const double PlayerDamagePerLevel = 0.012;

        static readonly int[] FallbackStats = { 40, 50, 40 };

        static int[] StatsOf(JsWeapon w)
        {
            return w != null && w.Stats != null ? w.Stats : FallbackStats;
        }

        static double[] WeaponRaw(JsWeapon w, int level)
        {
            var s = StatsOf(w);
            int l = Math.Max(0, (level <= 0 ? 1 : level) - 1);
            return new[]
            {
                s[0] * (1 + GrowthDamage * l),
                s[1] * (1 + GrowthSpeed * l),
                s[2] * (1 + GrowthReach * l),
            };
        }

        /// <summary>The stats the Armory shows for a weapon at a level (uncapped: boss weapons go past 100).</summary>
        public static WeaponStatLine WeaponStats(string id, int level)
        {
            var r = WeaponRaw(JsTables.Weapon(id), level);
            return new WeaponStatLine { Damage = JsRound(r[0]), Speed = JsRound(r[1]), Reach = JsRound(r[2]) };
        }

        public static int WeaponTotal(string id, int level)
        {
            var s = WeaponStats(id, level);
            return s.Damage + s.Speed + s.Reach;
        }

        /// <summary>
        /// Combat multipliers of a weapon at a level, relative to the weapon whose forms (JS base) it fights with.
        /// An exact JS port (JS weaponMul); an unknown weapon reads as bare hands.
        /// </summary>
        public static WeaponMultipliers WeaponMul(string id, int level)
        {
            var w = JsTables.Weapon(id) ?? JsTables.Weapon("fists");
            return WeaponMul(id, level, w.Base ?? w.Id);
        }

        /// <summary>
        /// Combat multipliers of a weapon at a level measured against <paramref name="referenceId"/> at level 1 (capped
        /// like the JS rule). Unity's weapon families use it to measure a family's reference weapon against itself
        /// (ADR-0006: Raikō no Tachi). An unknown weapon reads as bare hands, an unknown reference as the weapon itself.
        /// </summary>
        public static WeaponMultipliers WeaponMul(string id, int level, string referenceId)
        {
            var w = JsTables.Weapon(id) ?? JsTables.Weapon("fists");
            var reference = (referenceId != null ? JsTables.Weapon(referenceId) : null) ?? w;
            var refStats = StatsOf(reference);
            var r = WeaponRaw(w, level);
            return new WeaponMultipliers
            {
                Dmg = r[0] / refStats[0],
                Spd = Math.Min(SpeedMulCap, r[1] / refStats[1]),
                Reach = Math.Min(ReachMulCap, r[2] / refStats[2]),
            };
        }

        /// <summary>
        /// An item's tier at a level. Shop weapons climb as they are upgraded (Rare from level 4, Epic from 7), never
        /// dropping below the tier they are sold at; everything else keeps its tier.
        /// </summary>
        public static string ItemTier(GearKind kind, string id, int level)
        {
            var it = Item(kind, id);
            string baseTier = it != null && !string.IsNullOrEmpty(it.Tier) ? it.Tier : "common";
            if (kind != GearKind.Weapons || (baseTier != "common" && baseTier != "rare" && baseTier != "epic")) return baseTier;
            int lvl = level <= 0 ? 1 : level;
            string byLevel = lvl >= 7 ? "epic" : lvl >= 4 ? "rare" : "common";
            return JsTables.Tier(byLevel).Rank > JsTables.Tier(baseTier).Rank ? byLevel : baseTier;
        }

        public static string TierLabel(GearKind kind, string id, int level)
        {
            return JsTables.Tier(ItemTier(kind, id, level)).Label;
        }

        /// <summary>Defence a helmet or armour gives at a level.</summary>
        public static double GearDef(GearKind kind, string id, int level)
        {
            var g = Item(kind, id);
            return g != null && g.Gear != null && g.Gear.Def > 0 ? g.Gear.Def + GearDefPerLevel * ((level <= 0 ? 1 : level) - 1) : 0;
        }

        /// <summary>Health (JS units) a helmet or armour gives at a level.</summary>
        public static int GearHp(GearKind kind, string id, int level)
        {
            var g = Item(kind, id);
            return g != null && g.Gear != null && g.Gear.Hp > 0
                ? JsRound(g.Gear.Hp * (1 + GearHpGrowthPerLevel * ((level <= 0 ? 1 : level) - 1)))
                : 0;
        }

        // The level an owned item is at, 1 when it isn't in the save.
        static int LevelOf(SaveData save, GearKind kind, string id)
        {
            int level;
            return OwnedOf(save, kind).TryGetValue(id, out level) && level > 0 ? level : 1;
        }

        /// <summary>Kage's maximum health in JS units: 220, +4 per player level, plus the equipped armour and helmet.</summary>
        public static int PlayerMaxHp(SaveData save)
        {
            string a = save.equipped.armor, h = save.equipped.helmet;
            return JsRound(PlayerBaseHealth + (save.level - 1) * PlayerHealthPerLevel
                + GearHp(GearKind.Armors, a, LevelOf(save, GearKind.Armors, a))
                + GearHp(GearKind.Helmets, h, LevelOf(save, GearKind.Helmets, h)));
        }

        /// <summary>
        /// Everything the equipped gear does for Kage in a fight: health, damage reduction (capped at 50 %), the weapon's
        /// damage multiplier and the summed helmet and armour perks.
        /// </summary>
        public static PlayerGearStats PlayerCombatStats(SaveData save)
        {
            string a = save.equipped.armor, h = save.equipped.helmet, w = save.equipped.weapon;
            var stats = new PlayerGearStats
            {
                Hp = PlayerMaxHp(save),
                Defense = Math.Min(PlayerDefenseCap,
                    GearDef(GearKind.Helmets, h, LevelOf(save, GearKind.Helmets, h)) + GearDef(GearKind.Armors, a, LevelOf(save, GearKind.Armors, a))),
                WeaponDmg = WeaponMul(w, LevelOf(save, GearKind.Weapons, w)).Dmg,
            };
            AddPerks(stats.Perks, Item(GearKind.Helmets, h));
            AddPerks(stats.Perks, Item(GearKind.Armors, a));
            double dmgPerk;
            stats.Perks.TryGetValue("dmg", out dmgPerk);
            stats.DamageMul = (1 + PlayerDamagePerLevel * (save.level - 1)) * (1 + dmgPerk);
            return stats;
        }

        static void AddPerks(Dictionary<string, double> total, GearItem item)
        {
            if (item == null || item.Gear == null || item.Gear.Perks == null) return;
            foreach (var kv in item.Gear.Perks)
            {
                double have;
                total.TryGetValue(kv.Key, out have);
                total[kv.Key] = have + kv.Value;
            }
        }
    }
}
