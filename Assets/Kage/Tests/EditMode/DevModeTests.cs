using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Dev mode (ADR-0009): a save-level flag. On: every stage open, every non-enemy weapon, helmet and armour owned, plenty
    // of coins and gems. Off: the real JS economy from a fresh save.
    public class DevModeTests
    {
        static SaveData DevSave()
        {
            return GameRules.SetDevMode(GameRules.DefaultSave(false), true);
        }

        // ---- on ----

        [Test]
        public void Dev_mode_on_owns_every_weapon_helmet_and_armour_that_is_not_enemy_only()
        {
            var s = DevSave();
            Assert.IsTrue(s.devMode);
            foreach (var w in JsTables.Weapons.Where(w => !w.Enemy && w.Id != "fists"))
                Assert.GreaterOrEqual(s.owned.weapons[w.Id], 1, "weapon " + w.Id);
            foreach (var h in JsTables.Helmets.Where(h => !h.Enemy && !h.Retired))
                Assert.GreaterOrEqual(s.owned.helmets[h.Id], 1, "helmet " + h.Id);
            foreach (var a in JsTables.Armors.Where(a => !a.Enemy && !a.Retired))
                Assert.GreaterOrEqual(s.owned.armors[a.Id], 1, "armour " + a.Id);
        }

        [Test]
        public void Dev_mode_does_not_hand_out_enemy_only_gear()
        {
            var s = DevSave();
            foreach (var w in JsTables.Weapons.Where(w => w.Enemy))
                Assert.IsFalse(s.owned.weapons.ContainsKey(w.Id), "weapon " + w.Id);
            foreach (var a in JsTables.Armors.Where(a => a.Enemy))
                Assert.IsFalse(s.owned.armors.ContainsKey(a.Id), "armour " + a.Id);
        }

        [Test]
        public void Dev_mode_on_gives_enough_coins_and_gems_to_test_upgrades()
        {
            var s = DevSave();
            Assert.AreEqual(99999, s.coins);
            Assert.AreEqual(999, s.gems);
        }

        [Test]
        public void Dev_mode_never_lowers_what_the_save_already_has()
        {
            var s = GameRules.DefaultSave(false);
            s.coins = 500000;
            s.owned.weapons["tsubame"] = 6;
            GameRules.SetDevMode(s, true);
            Assert.AreEqual(500000, s.coins);
            Assert.AreEqual(6, s.owned.weapons["tsubame"]);
        }

        [Test]
        public void Dev_mode_opens_every_stage_in_any_order()
        {
            var s = DevSave();
            foreach (var st in GameRules.Stages)
                Assert.IsTrue(GameRules.IsStageUnlocked(s, st.Id), st.Id);
        }

        [Test]
        public void Without_dev_mode_only_the_first_stage_is_open_on_a_fresh_save()
        {
            var s = GameRules.DefaultSave(false);
            Assert.IsTrue(GameRules.IsStageUnlocked(s, GameRules.Stages[0].Id));
            foreach (var st in GameRules.Stages.Skip(1))
                Assert.IsFalse(GameRules.IsStageUnlocked(s, st.Id), st.Id);
        }

        [Test]
        public void Dev_mode_keeps_the_starter_loadout_equipped()
        {
            var s = DevSave();
            Assert.AreEqual("mumei", s.equipped.weapon);
            Assert.AreEqual("shiro_maki", s.equipped.helmet);
            Assert.AreEqual("tachibana_yoroi", s.equipped.armor);
        }

        [Test]
        public void Upgrades_can_be_tested_at_once_in_dev_mode()
        {
            var s = DevSave();
            Assert.IsTrue(GameRules.UpgradeItem(s, GearKind.Weapons, "tsubame"));
            Assert.AreEqual(2, s.owned.weapons["tsubame"]);
        }

        // ---- off ----

        [Test]
        public void Turning_dev_mode_off_starts_the_real_economy_from_a_fresh_save()
        {
            var dev = DevSave();
            GameRules.EquipItem(dev, GearKind.Weapons, "onikiri");
            dev.cleared[GameRules.Stages[0].Id] = true;
            dev.stats.wins = 12;

            var real = GameRules.SetDevMode(dev, false);

            Assert.IsFalse(real.devMode);
            Assert.AreEqual(250, real.coins);
            Assert.AreEqual(5, real.gems);
            Assert.AreEqual(1, real.level);
            Assert.AreEqual(0, real.stats.wins);
            Assert.AreEqual(0, real.cleared.Count);
            Assert.AreEqual(1, real.owned.weapons.Count);
            Assert.AreEqual(1, real.owned.helmets.Count);
            Assert.AreEqual(1, real.owned.armors.Count);
            Assert.AreEqual("mumei", real.equipped.weapon);
        }

        [Test]
        public void Turning_dev_mode_off_closes_the_stages_again()
        {
            var real = GameRules.SetDevMode(DevSave(), false);
            Assert.IsTrue(GameRules.IsStageUnlocked(real, GameRules.Stages[0].Id));
            Assert.IsFalse(GameRules.IsStageUnlocked(real, GameRules.Stages[1].Id));
        }

        [Test]
        public void Turning_dev_mode_off_when_it_is_already_off_changes_nothing()
        {
            var s = GameRules.DefaultSave(false);
            s.coins = 777;
            var again = GameRules.SetDevMode(s, false);
            Assert.AreEqual(777, again.coins);
        }

        // ---- the default ----

        [Test]
        public void A_brand_new_install_starts_in_dev_mode_while_the_default_is_on()
        {
            Assert.IsTrue(DevDefaults.DevModeOnFreshSave);
            Assert.IsTrue(GameRules.OpenSave(null).Save.devMode);
        }
    }
}
