using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // The generated JS tables (ADR-0010) hold the JS values as they are once every JS module has loaded.
    // Expected values below are the numbers the JS game reports (tests/data.test.js, gear.test.js, economy.test.js,
    // and a load-sim read of ENEMIES), not numbers derived from the tables.
    public class JsTablesTests
    {
        [Test]
        public void Garan_has_the_run_time_health_not_the_one_in_the_data_file()
        {
            Assert.AreEqual(395, JsTables.Enemy("garan").Hp);
        }

        [Test]
        public void Prologue_and_Act_I_foes_carry_raw_JS_health()
        {
            Assert.AreEqual(170, JsTables.Enemy("order_footman").Hp);
            Assert.AreEqual(360, JsTables.Enemy("kuroe").Hp);
            Assert.AreEqual(375, JsTables.Enemy("hisame").Hp);
            Assert.AreEqual(400, JsTables.Enemy("shiden").Hp);
            Assert.AreEqual(640, JsTables.Enemy("raikon").Hp);
        }

        [Test]
        public void Gear_tables_have_every_JS_item()
        {
            Assert.AreEqual(19, JsTables.Helmets.Length);
            Assert.AreEqual(28, JsTables.Armors.Length);
            Assert.AreEqual("Crimson Scarf", JsTables.Helmet("shiro_maki").Name);
            Assert.AreEqual("Shinobi Garb", JsTables.Armor("tachibana_yoroi").Name);
        }

        [Test]
        public void Gear_caps_and_dev_grants_match_JS()
        {
            Assert.AreEqual(8, JsTables.GearMaxWeapons);
            Assert.AreEqual(5, JsTables.GearMaxHelmets);
            Assert.AreEqual(5, JsTables.GearMaxArmors);
            Assert.AreEqual(99999, JsTables.DevCoins);
            Assert.AreEqual(999, JsTables.DevGems);
        }

        [Test]
        public void Exports_the_thirteen_Act_I_foes_and_the_Hollow_Husk()
        {
            var ids = JsTables.Enemies.Select(e => e.Id).OrderBy(i => i).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "hollow_tutorial", "hollow_shade", "hollow_flicker", "hollow_husk",
                "order_footman", "order_runner", "order_shieldbearer", "order_chainthrower", "order_ironclad",
                "kuroe", "garan", "hisame", "shiden", "raikon",
            }, ids);
        }

        [Test]
        public void Raikon_has_three_phases_at_66_and_33_percent_health()
        {
            var phases = JsTables.Enemy("raikon").Phases;
            Assert.AreEqual(3, phases.Length);
            Assert.AreEqual(1.0, phases[0].At, 1e-9);
            Assert.AreEqual(0.66, phases[1].At, 1e-9);
            Assert.AreEqual(0.33, phases[2].At, 1e-9);
        }

        // ---- integrity, as in the JS data tests (tests/data.test.js, gear.test.js) ----

        [Test]
        public void Every_stage_fights_an_enemy_that_exists_of_the_right_weight_class()
        {
            var type = new System.Collections.Generic.Dictionary<string, string> { { "stretch", "regular" }, { "subboss", "sub" }, { "boss", "boss" } };
            foreach (var st in JsTables.Stages)
            {
                var e = JsTables.Enemy(st.Enemy);
                Assert.IsNotNull(e, st.Id + " -> " + st.Enemy);
                Assert.AreEqual(type[st.Kind], e.Type, st.Id);
            }
        }

        [Test]
        public void Every_wave_names_foes_that_exist()
        {
            foreach (var st in JsTables.Stages.Where(s => s.Stretch != null))
                foreach (var enc in st.Stretch.Encounters)
                    foreach (var wave in enc.Waves)
                        foreach (var id in wave)
                            Assert.IsNotNull(JsTables.Enemy(id), st.Id + " wave foe " + id);
        }

        [Test]
        public void Gear_rewards_only_name_items_that_exist()
        {
            foreach (var st in JsTables.Stages.Where(s => s.Reward != null))
            {
                foreach (var id in st.Reward.Weapons) Assert.IsNotNull(JsTables.Weapon(id), st.Id + " " + id);
                foreach (var id in st.Reward.Helmets) Assert.IsNotNull(JsTables.Helmet(id), st.Id + " " + id);
                foreach (var id in st.Reward.Armors) Assert.IsNotNull(JsTables.Armor(id), st.Id + " " + id);
            }
        }

        [Test]
        public void Every_foe_uses_a_weapon_and_an_AI_profile_that_exist()
        {
            foreach (var e in JsTables.Enemies)
            {
                Assert.IsNotNull(JsTables.Weapon(e.Weapon), e.Id + " weapon " + e.Weapon);
                Assert.IsTrue(JsTables.AiProfiles.Any(a => a.Id == e.Ai), e.Id + " ai " + e.Ai);
            }
        }

        [Test]
        public void The_shop_sells_ten_helmets_and_seven_armours_after_the_Shinobi_Garb_across_Common_Rare_and_Epic()
        {
            var helmets = JsTables.Helmets.Where(g => !g.Enemy && !g.Exclusive && g.Tier != "starter").ToArray();
            var armours = JsTables.Armors.Where(g => !g.Enemy && !g.Exclusive && g.Tier != "starter").ToArray();
            Assert.AreEqual(10, helmets.Length);
            Assert.AreEqual(7, armours.Length);
            CollectionAssert.AreEquivalent(new[] { "common", "rare", "epic" }, helmets.Select(g => g.Tier).Distinct().ToArray());
            CollectionAssert.AreEquivalent(new[] { "common", "rare", "epic" }, armours.Select(g => g.Tier).Distinct().ToArray());
        }

        [Test]
        public void The_Mugen_Armor_costs_4200_coins_and_25_gems_with_its_three_perks()
        {
            var m = JsTables.Armor("mugen_yoroi");
            Assert.AreEqual(4200, m.Price);
            Assert.AreEqual(25, m.Gems);
            Assert.AreEqual(3, m.Perks.Count);
            Assert.AreEqual(0.07, m.Perks["dmg"], 1e-9);
            Assert.AreEqual(0.2, m.Perks["rage"], 1e-9);
            Assert.AreEqual(0.2, m.Perks["stunRes"], 1e-9);
        }

        [Test]
        public void Boss_weapons_and_trophies_are_exclusive_and_shop_gear_is_not()
        {
            foreach (var id in new[] { "shinsen", "raiko_no_tachi", "kagehira_yurei", "mugen_no_ha" })
                Assert.IsTrue(JsTables.Weapon(id).Exclusive, id);
            Assert.IsTrue(JsTables.Helmet("raijin_kabuto").Exclusive);
            Assert.IsFalse(JsTables.Weapon("tsubame").Exclusive);
            Assert.IsTrue(JsTables.Weapon("cutter_blade").Enemy, "enemy-only weapons are flagged");
        }
    }
}
