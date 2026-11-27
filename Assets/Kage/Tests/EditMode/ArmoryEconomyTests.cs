using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Buy, upgrade and equip (JS buyItem, upgradeItem, equipItem, upgradeCost, maxLevel; tests/economy.test.js and
    // gear.test.js). Upgrade cost = max(160, base x (0.35 + 0.3 x level)) rounded to 10, where base is the coin price, else
    // 60 per gem, else 500. Level caps: weapons 8, helmets and armour 5; trophies and boss weapons stay at 1.
    public class ArmoryEconomyTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        // ---- upgrade costs ----

        [Test]
        public void Upgrade_costs_scale_with_price_and_level_and_round_to_10_coins()
        {
            // Kagetsu upgrades at its JS price, none (it was the JS starter; Unity sells it now): base 500 -> 500 x 0.65 -> 320, then 500 x 0.95 -> 480
            Assert.AreEqual(320, GameRules.UpgradeCost(GearKind.Weapons, "kagetsu", 1));
            Assert.AreEqual(480, GameRules.UpgradeCost(GearKind.Weapons, "kagetsu", 2));
        }

        [TestCase(GearKind.Weapons, "tsubame", 1, 290)]
        [TestCase(GearKind.Weapons, "onikiri", 1, 910)]
        [TestCase(GearKind.Weapons, "iwakudaki", 4, 4960)]
        [TestCase(GearKind.Helmets, "kuro_zukin", 1, 190)]
        [TestCase(GearKind.Helmets, "kuro_zukin", 4, 460)]
        [TestCase(GearKind.Helmets, "oni_menpo", 1, 470)]
        [TestCase(GearKind.Armors, "tachibana_yoroi", 1, 320)]
        [TestCase(GearKind.Armors, "mugen_yoroi", 1, 2730)]
        [TestCase(GearKind.Armors, "mugen_yoroi", 4, 6510)]
        public void Upgrade_costs_match_the_JS_rule(GearKind kind, string id, int level, int cost)
        {
            Assert.AreEqual(cost, GameRules.UpgradeCost(kind, id, level));
        }

        // ---- level caps ----

        [Test]
        public void Level_caps_are_8_for_weapons_5_for_helmets_and_armour_and_1_for_trophies()
        {
            Assert.AreEqual(8, GameRules.MaxLevel(GearKind.Weapons, "tsubame"));
            Assert.AreEqual(8, GameRules.MaxLevel(GearKind.Weapons, "kagetsu"));
            Assert.AreEqual(5, GameRules.MaxLevel(GearKind.Helmets, "tetsu_kabuto"));
            Assert.AreEqual(5, GameRules.MaxLevel(GearKind.Armors, "tachibana_yoroi"));
            Assert.AreEqual(1, GameRules.MaxLevel(GearKind.Helmets, "kasa"));
            Assert.AreEqual(1, GameRules.MaxLevel(GearKind.Helmets, "raijin_kabuto"));
            Assert.AreEqual(1, GameRules.MaxLevel(GearKind.Armors, "shiden_yoroi"));
            Assert.AreEqual(1, GameRules.MaxLevel(GearKind.Weapons, "raiko_no_tachi"), "boss weapons are fixed at level 1");
            Assert.AreEqual(1, GameRules.MaxLevel(GearKind.Weapons, "shinsen"));
        }

        // ---- buying ----

        [TestCase("tsubame", 450)]
        [TestCase("shizuka", 1100)]
        [TestCase("onikiri", 1400)]
        [TestCase("mizuchi", 1700)]
        [TestCase("yamigama", 2600)]
        [TestCase("iwakudaki", 3200)]
        public void Shop_weapons_are_bought_at_their_JS_prices(string id, int price)
        {
            var s = Fresh();
            s.coins = price - 1;
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Weapons, id), "one coin short");
            Assert.IsFalse(s.owned.weapons.ContainsKey(id));
            Assert.AreEqual(price - 1, s.coins);

            s.coins = price;
            Assert.IsTrue(GameRules.BuyItem(s, GearKind.Weapons, id));
            Assert.AreEqual(0, s.coins);
            Assert.AreEqual(1, s.owned.weapons[id]);
        }

        [Test]
        public void An_item_you_already_own_cannot_be_bought_again()
        {
            var s = Fresh();
            s.coins = 450;
            Assert.IsTrue(GameRules.BuyItem(s, GearKind.Weapons, "tsubame"));
            s.coins = 450;
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Weapons, "tsubame"));
            Assert.AreEqual(450, s.coins);
        }

        [Test]
        public void Trophies_boss_weapons_and_enemy_gear_are_never_sold()
        {
            var s = Fresh();
            s.coins = 1000000000;
            s.gems = 1000000000;
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Weapons, "raiko_no_tachi"));
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Helmets, "raijin_kabuto"));
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Armors, "ikazuchi_do"));
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Weapons, "cutter_blade"), "an enemy-only weapon");
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Weapons, "no_such_weapon"));
            Assert.AreEqual(1000000000, s.coins);
            Assert.IsFalse(s.owned.weapons.ContainsKey("raiko_no_tachi"));
        }

        [Test]
        public void The_Mugen_Armor_takes_coins_and_gems_and_needs_both()
        {
            var s = Fresh();
            s.coins = 4200;
            s.gems = 24;
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Armors, "mugen_yoroi"), "one gem short");
            s.gems = 25;
            s.coins = 4199;
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Armors, "mugen_yoroi"), "one coin short");
            s.coins = 5000;
            Assert.IsTrue(GameRules.BuyItem(s, GearKind.Armors, "mugen_yoroi"));
            Assert.AreEqual(800, s.coins);
            Assert.AreEqual(0, s.gems);
            Assert.AreEqual(1, s.owned.armors["mugen_yoroi"]);
        }

        [Test]
        public void A_gem_priced_helmet_costs_gems_only()
        {
            var s = Fresh();
            s.coins = 0;
            s.gems = 12;
            Assert.IsTrue(GameRules.BuyItem(s, GearKind.Helmets, "oni_menpo"));
            Assert.AreEqual(0, s.gems);
            Assert.AreEqual(0, s.coins);
        }

        [Test]
        public void Retired_armour_is_never_sold()
        {
            var s = Fresh();
            s.coins = 1000000000;
            s.gems = 1000000000;
            Assert.IsFalse(GameRules.BuyItem(s, GearKind.Armors, "kozane_do"));
        }

        // ---- upgrading ----

        [Test]
        public void Upgrading_spends_the_upgrade_cost_and_stops_at_the_cap()
        {
            // the starter weapon (Mumei) upgrades at the cost the old starter did: base 500
            var s = Fresh();
            s.coins = 1000000000;
            Assert.IsTrue(GameRules.UpgradeItem(s, GearKind.Weapons, "mumei"));
            Assert.AreEqual(2, s.owned.weapons["mumei"]);
            Assert.AreEqual(1000000000 - 320, s.coins);
            while (GameRules.UpgradeItem(s, GearKind.Weapons, "mumei")) { }
            Assert.AreEqual(8, s.owned.weapons["mumei"]);
            Assert.AreEqual(1000000000 - (320 + 480 + 630 + 770 + 930 + 1080 + 1230), s.coins, "each level costs more than the last");
        }

        [Test]
        public void Upgrading_needs_enough_coins()
        {
            var s = Fresh();
            s.coins = 319;
            Assert.IsFalse(GameRules.UpgradeItem(s, GearKind.Weapons, "mumei"));
            Assert.AreEqual(319, s.coins);
            Assert.AreEqual(1, s.owned.weapons["mumei"]);
        }

        [Test]
        public void Gear_you_do_not_own_cannot_be_upgraded()
        {
            var s = Fresh();
            s.coins = 1000000000;
            Assert.IsFalse(GameRules.UpgradeItem(s, GearKind.Weapons, "iwakudaki"));
            Assert.AreEqual(1000000000, s.coins);
        }

        [Test]
        public void Armour_stops_at_level_5_and_a_boss_weapon_cannot_be_upgraded()
        {
            var s = Fresh();
            s.coins = 1000000000;
            s.owned.armors["raiko_yoroi"] = 1;
            s.owned.weapons["raiko_no_tachi"] = 1;
            while (GameRules.UpgradeItem(s, GearKind.Armors, "raiko_yoroi")) { }
            Assert.AreEqual(5, s.owned.armors["raiko_yoroi"]);
            Assert.IsFalse(GameRules.UpgradeItem(s, GearKind.Weapons, "raiko_no_tachi"));
            Assert.AreEqual(1, s.owned.weapons["raiko_no_tachi"]);
        }

        // ---- equipping ----

        [Test]
        public void Only_owned_gear_can_be_equipped_into_its_own_slot()
        {
            var s = Fresh();
            Assert.IsFalse(GameRules.EquipItem(s, GearKind.Weapons, "iwakudaki"));
            Assert.AreEqual("mumei", s.equipped.weapon);
            s.owned.helmets["kuro_zukin"] = 1;
            Assert.IsTrue(GameRules.EquipItem(s, GearKind.Helmets, "kuro_zukin"));
            Assert.AreEqual("kuro_zukin", s.equipped.helmet);
            Assert.AreEqual("tachibana_yoroi", s.equipped.armor, "other slots are untouched");
        }

        [Test]
        public void A_trophy_granted_by_a_stage_can_be_equipped()
        {
            var s = Fresh();
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == "act1.boss") break;
                GameRules.MarkStageCleared(s, st.Id);
            }
            GameRules.MarkStageCleared(s, "act1.boss");
            Assert.IsTrue(GameRules.EquipItem(s, GearKind.Helmets, "raijin_kabuto"), "Raikon's helmet trophy is granted and wearable");
            Assert.AreEqual("raijin_kabuto", s.equipped.helmet);
            // his blade is withheld for now (GameRules.WithheldWeapons, the owner's call): the stage keeps the reward row but never grants it
            Assert.IsTrue(GameRules.IsWithheld(GearKind.Weapons, "raiko_no_tachi"));
            Assert.IsFalse(GameRules.EquipItem(s, GearKind.Weapons, "raiko_no_tachi"), "a withheld blade is not owned, so it cannot be equipped");
            Assert.AreNotEqual("raiko_no_tachi", s.equipped.weapon);
        }
    }
}
