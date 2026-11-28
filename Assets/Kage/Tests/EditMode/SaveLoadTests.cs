using System.Collections.Generic;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Opening a save (ADR-0009, JS openSave / loadSave / normalizeSave): any text becomes a complete, valid save, and a
    // corrupt or missing one becomes a fresh save without throwing.
    public class SaveLoadTests
    {
        static SaveData Open(string json)
        {
            return GameRules.OpenSave(json).Save;
        }

        // ---- fresh save ----

        [Test]
        public void A_fresh_save_owns_Mumei_and_the_starter_helmet_and_armour_at_level_1_equipped()
        {
            // the starter weapon is Unity's plain Mumei now (docs/weapon-styles.md, the tiers); Kagetsu is sold
            var s = GameRules.DefaultSave(false);
            Assert.AreEqual(1, s.owned.weapons["mumei"]);
            Assert.AreEqual(1, s.owned.armors["tachibana_yoroi"]);
            Assert.AreEqual(1, s.owned.weapons.Count);
            Assert.AreEqual(1, s.owned.armors.Count);
            Assert.AreEqual("mumei", s.equipped.weapon);
            Assert.AreEqual("spike", s.equipped.hairstyle);
            Assert.AreEqual("tachibana_yoroi", s.equipped.armor);
            Assert.AreEqual(250, s.coins);
            Assert.AreEqual(5, s.gems);
            Assert.AreEqual(1, s.level);
            Assert.AreEqual(0, s.xp);
        }

        // ---- missing and corrupt ----

        [Test]
        public void Missing_save_text_opens_a_fresh_save()
        {
            var opened = GameRules.OpenSave(null);
            Assert.IsTrue(opened.Fresh);
            Assert.IsNull(opened.Error);
            Assert.IsNull(opened.CorruptText);
            Assert.GreaterOrEqual(opened.Save.coins, 250);
            Assert.AreEqual(DevDefaults.DevModeOnFreshSave, opened.Save.devMode);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Blank_save_text_counts_as_missing(string text)
        {
            var opened = GameRules.OpenSave(text);
            Assert.IsTrue(opened.Fresh);
            Assert.IsNull(opened.CorruptText);
        }

        [TestCase("{ not json")]
        [TestCase("null")]
        [TestCase("[1,2,3]")]
        [TestCase("\"hello\"")]
        [TestCase("{\"coins\":\"lots\"}")]
        [TestCase("{\"owned\":{\"weapons\":[1,2]}}")]
        public void Corrupt_save_text_opens_a_fresh_save_and_keeps_the_text_for_a_backup(string text)
        {
            var opened = GameRules.OpenSave(text);
            Assert.IsFalse(opened.Fresh);
            Assert.IsNotNull(opened.Error);
            Assert.AreEqual(text, opened.CorruptText);
            Assert.GreaterOrEqual(opened.Save.coins, 250);
            Assert.AreEqual(1, opened.Save.owned.weapons["mumei"]);
        }

        // ---- normalising ----

        [Test]
        public void Missing_fields_are_filled_from_the_defaults()
        {
            var s = Open("{\"coins\":900}");
            Assert.AreEqual(900, s.coins);
            Assert.AreEqual(5, s.gems);
            Assert.AreEqual(1, s.level);
            Assert.AreEqual("normal", s.difficulty);
            Assert.AreEqual("full", s.settings.shake);
            Assert.IsTrue(s.settings.sound);
            Assert.AreEqual(1, s.owned.weapons["mumei"]);
            Assert.AreEqual("mumei", s.equipped.weapon);
            Assert.IsNotNull(s.cleared);
            Assert.IsNotNull(s.stats);
        }

        [Test]
        public void Explicit_nulls_do_not_leave_holes()
        {
            var s = Open("{\"owned\":null,\"equipped\":null,\"cleared\":null,\"settings\":null,\"stats\":null,\"clues\":null}");
            Assert.AreEqual(1, s.owned.weapons["mumei"]);
            Assert.AreEqual("spike", s.equipped.hairstyle);
            Assert.IsNotNull(s.cleared);
            Assert.IsNotNull(s.settings);
            Assert.IsNotNull(s.stats);
            Assert.IsNotNull(s.clues);
        }

        [Test]
        public void The_starter_items_stay_owned_even_if_the_file_leaves_them_out()
        {
            var s = Open("{\"owned\":{\"weapons\":{\"tsubame\":3}}}");
            Assert.AreEqual(1, s.owned.weapons["mumei"]);
            Assert.AreEqual(3, s.owned.weapons["tsubame"]);
            Assert.AreEqual(1, s.owned.armors["tachibana_yoroi"]);
        }

        [Test]
        public void Unknown_enemy_only_and_unowned_level_gear_is_dropped()
        {
            var s = Open("{\"owned\":{\"weapons\":{\"no_such_blade\":2,\"fists\":1,\"tsubame\":0,\"onikiri\":2},\"helmets\":{\"nope\":1},\"armors\":{\"nada\":1}}}");
            Assert.IsFalse(s.owned.weapons.ContainsKey("no_such_blade"));
            Assert.IsFalse(s.owned.weapons.ContainsKey("fists"));
            Assert.IsFalse(s.owned.weapons.ContainsKey("tsubame"));
            Assert.AreEqual(2, s.owned.weapons["onikiri"]);
            Assert.IsFalse(s.owned.armors.ContainsKey("nada"));
        }

        [Test]
        public void A_slot_wearing_something_unowned_goes_back_to_the_starter_item()
        {
            var s = Open("{\"equipped\":{\"weapon\":\"onikiri\",\"helmet\":\"made_up\",\"armor\":\"mugen_yoroi\"},\"owned\":{\"weapons\":{\"tsubame\":1}}}");
            Assert.AreEqual("mumei", s.equipped.weapon);
            Assert.AreEqual("spike", s.equipped.hairstyle);
            Assert.AreEqual("tachibana_yoroi", s.equipped.armor);
        }

        [Test]
        public void An_owned_item_stays_equipped()
        {
            var s = Open("{\"equipped\":{\"weapon\":\"tsubame\"},\"owned\":{\"weapons\":{\"tsubame\":2}}}");
            Assert.AreEqual("tsubame", s.equipped.weapon);
        }

        [Test]
        public void Unknown_stage_ids_are_dropped_from_cleared_and_defeated()
        {
            var s = Open("{\"cleared\":{\"prologue.1\":true,\"act9.nowhere\":true,\"act1.stretch1\":false},\"defeated\":{\"prologue.1\":3,\"act9.nowhere\":2,\"act1.stretch1\":0}}");
            Assert.IsTrue(s.cleared["prologue.1"]);
            Assert.AreEqual(1, s.cleared.Count);
            Assert.AreEqual(3, s.defeated["prologue.1"]);
            Assert.AreEqual(1, s.defeated.Count);
        }

        [Test]
        public void Negative_amounts_are_raised_to_zero_and_the_level_to_one()
        {
            var s = Open("{\"coins\":-5,\"gems\":-1,\"xp\":-10,\"level\":0}");
            Assert.AreEqual(0, s.coins);
            Assert.AreEqual(0, s.gems);
            Assert.AreEqual(0, s.xp);
            Assert.AreEqual(1, s.level);
        }

        [Test]
        public void A_JS_save_without_the_dev_flag_opens_with_dev_mode_off()
        {
            var s = Open("{\"v\":2,\"coins\":400,\"gems\":2,\"owned\":{\"weapons\":{\"kagetsu\":1},\"helmets\":{\"shiro_maki\":1},\"armors\":{\"tachibana_yoroi\":1}}}");
            Assert.IsFalse(s.devMode);
            Assert.AreEqual(400, s.coins);
            // a JS save owns its old starter Kagetsu; Unity's starter Mumei is always owned too
            Assert.AreEqual(2, s.owned.weapons.Count);
            Assert.AreEqual(1, s.owned.weapons["kagetsu"]);
            Assert.AreEqual(1, s.owned.weapons["mumei"]);
        }

        [Test]
        public void Retired_gear_is_refunded_and_dropped()
        {
            // the JS retired shop armours pay back their price and upgrades once
            var retired = System.Array.Find(JsTables.Armors, a => a.Retired);
            Assert.IsNotNull(retired);
            var s = Open("{\"coins\":100,\"gems\":0,\"owned\":{\"armors\":{\"" + retired.Id + "\":1}}}");
            Assert.IsFalse(s.owned.armors.ContainsKey(retired.Id));
            Assert.AreEqual(100 + retired.Price, s.coins);
        }

        [Test]
        public void Opening_a_dev_mode_save_grants_everything_again()
        {
            var s = Open("{\"devMode\":true,\"coins\":10}");
            Assert.IsTrue(s.devMode);
            Assert.GreaterOrEqual(s.coins, 99999);
            Assert.IsTrue(GameRules.Owns(s, GearKind.Weapons, "raiko_no_tachi"));
        }
    }
}
