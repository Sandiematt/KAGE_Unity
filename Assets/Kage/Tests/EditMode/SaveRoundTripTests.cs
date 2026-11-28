using System.Collections.Generic;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // The save as JSON (ADR-0009, SAN-64): a SaveData goes to a string and comes back with nothing lost.
    public class SaveRoundTripTests
    {
        static SaveData RichSave()
        {
            var s = GameRules.DefaultSave(false);
            s.coins = 1234; s.gems = 17; s.xp = 310; s.level = 3;
            s.cleared["prologue.1"] = true;
            s.defeated["prologue.1"] = 2;
            s.owned.weapons["tsubame"] = 4;
            s.owned.armors["ronin_yoroi"] = 5;
            s.equipped.weapon = "tsubame";
            s.equipped.hairstyle = "messy_layers";
            s.equipped.armor = "ronin_yoroi";
            s.settings.shake = "off"; s.settings.reduceFlash = true; s.settings.sound = false; s.settings.music = false;
            s.settings.touch = "on"; s.settings.stick = "fixed";
            s.stats.wins = 7; s.stats.losses = 3; s.stats.perfects = 1; s.stats.bosses = 2; s.stats.playTime = 4521.5;
            s.seenRewards["act1.stretch1"] = true;
            s.storyFlags["met_raikon"] = true;
            s.bossFates["act1.boss"] = "killed";
            s.clues["act1.stretch1"] = new Dictionary<string, bool> { { "scorched_banner", true } };
            s.trialsBest = 9;
            s.bossRushBest.bosses = 3; s.bossRushBest.time = 188.25;
            return s;
        }

        [Test]
        public void A_save_comes_back_from_JSON_with_nothing_lost()
        {
            var original = RichSave();
            var json = SaveSerializer.Serialize(original);

            SaveData back;
            string error;
            Assert.IsTrue(SaveSerializer.TryDeserialize(json, out back, out error), error);

            Assert.AreEqual(1234, back.coins);
            Assert.AreEqual(17, back.gems);
            Assert.AreEqual(310, back.xp);
            Assert.AreEqual(3, back.level);
            Assert.IsTrue(back.cleared["prologue.1"]);
            Assert.AreEqual(2, back.defeated["prologue.1"]);
            Assert.AreEqual(1, back.owned.weapons["mumei"]);
            Assert.AreEqual(4, back.owned.weapons["tsubame"]);
            Assert.AreEqual(5, back.owned.armors["ronin_yoroi"]);
            Assert.AreEqual("tsubame", back.equipped.weapon);
            Assert.AreEqual("messy_layers", back.equipped.hairstyle);
            Assert.AreEqual("ronin_yoroi", back.equipped.armor);
            Assert.AreEqual("off", back.settings.shake);
            Assert.IsTrue(back.settings.reduceFlash);
            Assert.IsFalse(back.settings.sound);
            Assert.IsFalse(back.settings.music);
            Assert.AreEqual("on", back.settings.touch);
            Assert.AreEqual("fixed", back.settings.stick);
            Assert.AreEqual(7, back.stats.wins);
            Assert.AreEqual(4521.5, back.stats.playTime);
            Assert.IsTrue(back.seenRewards["act1.stretch1"]);
            Assert.IsTrue(back.storyFlags["met_raikon"]);
            Assert.AreEqual("killed", back.bossFates["act1.boss"]);
            Assert.IsTrue(back.clues["act1.stretch1"]["scorched_banner"]);
            Assert.AreEqual(9, back.trialsBest);
            Assert.AreEqual(3, back.bossRushBest.bosses);
            Assert.AreEqual(188.25, back.bossRushBest.time);
        }

        [Test]
        public void Writing_a_loaded_save_again_gives_the_same_JSON()
        {
            var json = SaveSerializer.Serialize(RichSave());
            SaveData back;
            string error;
            Assert.IsTrue(SaveSerializer.TryDeserialize(json, out back, out error), error);
            Assert.AreEqual(json, SaveSerializer.Serialize(back));
        }

        [Test]
        public void The_JSON_uses_the_JS_save_field_names()
        {
            var json = SaveSerializer.Serialize(GameRules.DefaultSave(false));
            StringAssert.Contains("\"coins\": 250", json);
            StringAssert.Contains("\"gems\": 5", json);
            StringAssert.Contains("\"equipped\"", json);
            StringAssert.Contains("\"weapon\": \"mumei\"", json);
            StringAssert.Contains("\"hairstyle\": \"spike\"", json);
            StringAssert.Contains("\"armor\": \"tachibana_yoroi\"", json);
            StringAssert.Contains("\"seenRewards\"", json);
            StringAssert.Contains("\"bossRushBest\"", json);
        }

        [Test]
        public void Dev_mode_survives_the_round_trip()
        {
            var s = GameRules.DefaultSave(true);
            SaveData back;
            string error;
            Assert.IsTrue(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(s), out back, out error), error);
            Assert.IsTrue(back.devMode);
        }

        [Test]
        public void Fields_this_version_does_not_know_are_kept()
        {
            // a save written by a later version, or by the JS game with a field the Unity port doesn't use
            var json = "{\"v\":2,\"coins\":10,\"futureThing\":{\"a\":[1,2,3]}}";
            SaveData back;
            string error;
            Assert.IsTrue(SaveSerializer.TryDeserialize(json, out back, out error), error);
            var again = SaveSerializer.Serialize(back);
            StringAssert.Contains("futureThing", again);
            StringAssert.Contains("3", again);
        }
    }
}
