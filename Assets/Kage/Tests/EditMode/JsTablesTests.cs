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
    }
}
