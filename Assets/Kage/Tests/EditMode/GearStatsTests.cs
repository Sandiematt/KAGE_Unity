using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Weapon stats and combat multipliers, item tiers, gear defence and health, player health and defence
    // (JS weaponStats, weaponMul, itemTier, gearDef, gearHp, playerMaxHp, playerCombatStats; tests/economy.test.js and
    // gear.test.js). Several expected values are the JS tests' own; the rest come from running the JS rules.
    public class GearStatsTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        static void AssertStats(string id, int level, int damage, int speed, int reach)
        {
            var s = GameRules.WeaponStats(id, level);
            Assert.AreEqual(damage, s.Damage, id + " damage at level " + level);
            Assert.AreEqual(speed, s.Speed, id + " speed at level " + level);
            Assert.AreEqual(reach, s.Reach, id + " reach at level " + level);
        }

        // ---- weapon stats shown in the Armory ----

        [Test]
        public void Upgrading_Kagetsu_raises_its_shown_stats_level_by_level()
        {
            AssertStats("kagetsu", 1, 60, 64, 58);
            AssertStats("kagetsu", 2, 65, 66, 59);
            AssertStats("kagetsu", 3, 70, 67, 60);
            AssertStats("kagetsu", 8, 94, 75, 64);
        }

        [Test]
        public void A_fully_upgraded_hammer_and_spear_show_their_stats()
        {
            AssertStats("iwakudaki", 8, 153, 19, 73);
            AssertStats("mizuchi", 8, 81, 71, 106);
        }

        // ---- combat multipliers ----

        [Test]
        public void Each_weapon_level_adds_8_percent_damage_2_and_a_half_speed_and_1_and_a_half_reach()
        {
            var m1 = GameRules.WeaponMul("kagetsu", 1);
            Assert.AreEqual(1.0, m1.Dmg, 1e-9);
            Assert.AreEqual(1.0, m1.Spd, 1e-9);
            Assert.AreEqual(1.0, m1.Reach, 1e-9);

            var m3 = GameRules.WeaponMul("kagetsu", 3);
            Assert.AreEqual(1.16, m3.Dmg, 1e-9);
            Assert.AreEqual(1.05, m3.Spd, 1e-9);
            Assert.AreEqual(1.03, m3.Reach, 1e-9);

            var m8 = GameRules.WeaponMul("kagetsu", 8);
            Assert.AreEqual(1.56, m8.Dmg, 1e-9);
            Assert.AreEqual(1.175, m8.Spd, 1e-9);
            Assert.AreEqual(1.105, m8.Reach, 1e-9);
        }

        [Test]
        public void Multipliers_are_relative_to_the_weapon_the_forms_come_from()
        {
            // Shizuka fights on Kagetsu's forms: 64 / 60 damage, 68 / 64 speed, 62 / 58 reach
            var m = GameRules.WeaponMul("shizuka", 1);
            Assert.AreEqual(64.0 / 60, m.Dmg, 1e-9);
            Assert.AreEqual(68.0 / 64, m.Spd, 1e-9);
            Assert.AreEqual(62.0 / 58, m.Reach, 1e-9);
        }

        [Test]
        public void A_boss_weapon_on_the_katana_forms_hits_far_harder_than_Kagetsu_and_speed_and_reach_are_capped()
        {
            // Shinsen: 150 damage against Kagetsu's 60
            var m = GameRules.WeaponMul("shinsen", 1);
            Assert.AreEqual(2.5, m.Dmg, 1e-9);
            Assert.AreEqual(1.25, m.Spd, 1e-9, "speed is capped at x1.25 (120 / 64 would be 1.875)");
            Assert.AreEqual(1.2, m.Reach, 1e-9, "reach is capped at x1.2 (132 / 58 would be 2.28)");
        }

        [Test]
        public void An_unknown_weapon_falls_back_to_bare_hands()
        {
            var m = GameRules.WeaponMul("no_such_weapon", 1);
            Assert.AreEqual(1.0, m.Dmg, 1e-9);
        }

        // ---- boss weapons against shop weapons ----

        [Test]
        public void Every_boss_weapon_has_at_least_1_5_times_the_total_stats_of_the_best_fully_upgraded_shop_weapon()
        {
            var shop = JsTables.Weapons.Where(w => w.Tier == "common" || w.Tier == "rare" || w.Tier == "epic").Select(w => w.Id).ToArray();
            Assert.AreEqual(7, shop.Length, "Kagetsu plus six buyable weapons");
            int best = shop.Max(id => GameRules.WeaponTotal(id, GameRules.MaxLevel(GearKind.Weapons, id)));
            Assert.AreEqual(258, best, "Mizuchi at level 8: 81 + 71 + 106");
            foreach (var id in new[] { "shinsen", "raiko_no_tachi", "kagehira_yurei", "mugen_no_ha" })
            {
                Assert.AreEqual("boss", JsTables.Weapon(id).Tier, id);
                Assert.GreaterOrEqual(GameRules.WeaponTotal(id, 1), 1.5 * best, id);
            }
        }

        // ---- item tiers ----

        [Test]
        public void Kagetsu_climbs_from_Common_to_Rare_to_Epic_as_it_is_upgraded()
        {
            Assert.AreEqual("common", GameRules.ItemTier(GearKind.Weapons, "kagetsu", 1));
            Assert.AreEqual("common", GameRules.ItemTier(GearKind.Weapons, "kagetsu", 3));
            Assert.AreEqual("rare", GameRules.ItemTier(GearKind.Weapons, "kagetsu", 4));
            Assert.AreEqual("rare", GameRules.ItemTier(GearKind.Weapons, "kagetsu", 6));
            Assert.AreEqual("epic", GameRules.ItemTier(GearKind.Weapons, "kagetsu", 7));
            Assert.AreEqual("epic", GameRules.ItemTier(GearKind.Weapons, "kagetsu", 8));
        }

        [Test]
        public void A_shop_weapon_never_drops_below_the_tier_it_is_sold_at()
        {
            Assert.AreEqual("rare", GameRules.ItemTier(GearKind.Weapons, "onikiri", 1));
            Assert.AreEqual("rare", GameRules.ItemTier(GearKind.Weapons, "onikiri", 4));
            Assert.AreEqual("epic", GameRules.ItemTier(GearKind.Weapons, "onikiri", 7));
            Assert.AreEqual("epic", GameRules.ItemTier(GearKind.Weapons, "yamigama", 1));
        }

        [Test]
        public void Only_weapons_climb_tiers_helmets_armour_and_boss_weapons_keep_theirs()
        {
            Assert.AreEqual("common", GameRules.ItemTier(GearKind.Helmets, "kuro_zukin", 5));
            Assert.AreEqual("starter", GameRules.ItemTier(GearKind.Armors, "tachibana_yoroi", 5));
            Assert.AreEqual("trophy", GameRules.ItemTier(GearKind.Helmets, "raijin_kabuto", 1));
            Assert.AreEqual("boss", GameRules.ItemTier(GearKind.Weapons, "raiko_no_tachi", 1));
        }

        [Test]
        public void Tier_labels_are_the_JS_labels()
        {
            Assert.AreEqual("Common", GameRules.TierLabel(GearKind.Weapons, "kagetsu", 1));
            Assert.AreEqual("Epic", GameRules.TierLabel(GearKind.Weapons, "kagetsu", 8));
            Assert.AreEqual("Starting gear", GameRules.TierLabel(GearKind.Helmets, "shiro_maki", 1));
            Assert.AreEqual("Boss weapon", GameRules.TierLabel(GearKind.Weapons, "shinsen", 1));
        }

        // ---- helmet and armour defence and health ----

        [Test]
        public void Gear_defence_grows_by_1_point_2_per_level_and_health_by_10_percent()
        {
            Assert.AreEqual(0.11, GameRules.GearDef(GearKind.Armors, "raiko_yoroi", 1), 1e-9);
            Assert.AreEqual(0.158, GameRules.GearDef(GearKind.Armors, "raiko_yoroi", 5), 1e-9);
            Assert.AreEqual(32, GameRules.GearHp(GearKind.Armors, "raiko_yoroi", 1));
            Assert.AreEqual(45, GameRules.GearHp(GearKind.Armors, "raiko_yoroi", 5));
        }

        [Test]
        public void Even_the_starting_gear_gets_tougher_when_upgraded()
        {
            Assert.AreEqual(0.058, GameRules.GearDef(GearKind.Helmets, "shiro_maki", 5), 1e-9);
            Assert.AreEqual(11, GameRules.GearHp(GearKind.Armors, "tachibana_yoroi", 5));
        }

        // ---- player health, defence and combat stats ----

        [Test]
        public void A_new_player_has_232_health_220_plus_the_starting_gear_and_each_level_adds_4()
        {
            var s = Fresh();
            Assert.AreEqual(232, GameRules.PlayerMaxHp(s));
            s.level = 6;
            Assert.AreEqual(252, GameRules.PlayerMaxHp(s));
        }

        [Test]
        public void Better_gear_noticeably_raises_Kages_health_defence_and_damage()
        {
            var s = Fresh();
            var start = GameRules.PlayerCombatStats(s);
            Assert.AreEqual(232, start.Hp, "220 base + 8 from the Shinobi Garb + 4 from the Crimson Scarf");
            Assert.AreEqual(0.03, start.Defense, 1e-9);
            Assert.AreEqual(1.0, start.WeaponDmg, 1e-9);

            s.owned.armors["mugen_yoroi"] = 5;
            s.owned.helmets["shika_kabuto"] = 5;
            s.owned.weapons["iwakudaki"] = 8;
            s.equipped.weapon = "iwakudaki";
            s.equipped.helmet = "shika_kabuto";
            s.equipped.armor = "mugen_yoroi";

            var geared = GameRules.PlayerCombatStats(s);
            Assert.AreEqual(220 + 77 + 31, geared.Hp);
            Assert.AreEqual(0.376, geared.Defense, 1e-9);
            Assert.AreEqual(1.56, geared.WeaponDmg, 1e-9);
        }

        [Test]
        public void Damage_reduction_from_gear_is_capped_at_50_percent()
        {
            var s = Fresh();
            s.owned.armors["mugen_yoroi"] = 50;
            s.equipped.armor = "mugen_yoroi";
            Assert.AreEqual(0.5, GameRules.PlayerCombatStats(s).Defense, 1e-9);
        }

        [Test]
        public void Helmet_and_armour_perks_are_summed()
        {
            var s = Fresh();
            s.owned.armors["mugen_yoroi"] = 1;
            s.owned.helmets["kasa"] = 1;
            s.equipped.armor = "mugen_yoroi";
            s.equipped.helmet = "kasa";
            var perks = GameRules.PlayerCombatStats(s).Perks;
            Assert.AreEqual(0.07, perks["dmg"], 1e-9);
            Assert.AreEqual(0.35, perks["rage"], 1e-9, "0.2 from the armour + 0.15 from the helmet");
            Assert.AreEqual(0.2, perks["stunRes"], 1e-9);
            Assert.AreEqual(3, perks["ammo"], 1e-9);
        }

        [Test]
        public void The_starting_gear_has_no_perks()
        {
            Assert.AreEqual(0, GameRules.PlayerCombatStats(Fresh()).Perks.Count);
        }
    }
}
