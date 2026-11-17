using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Reward items: helmets, armour and Raikō no Tachi granted by clearing Act I stages. Expected rewards are the JS
    // campaign's (tests/gear.test.js: "Acts I to III ... grant gear", and the stage hints in the gear tables).
    public class StageRewardItemsTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        static void ClearUpTo(SaveData save, string id)
        {
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == id) break;
                GameRules.MarkStageCleared(save, st.Id);
            }
        }

        static string[] Items(JsStage st)
        {
            if (st.Reward == null) return new string[0];
            return st.Reward.Weapons.Concat(st.Reward.Helmets).Concat(st.Reward.Armors).ToArray();
        }

        [Test]
        public void Each_Act_I_sub_boss_drops_its_trophy_and_Raikon_drops_Raiko_no_Tachi_and_the_Raijin_Kabuto()
        {
            CollectionAssert.AreEqual(new[] { "sumi_eboshi" }, GameRules.Stage("act1.sub1").Reward.Helmets);
            CollectionAssert.AreEqual(new[] { "ikazuchi_do" }, GameRules.Stage("act1.sub2").Reward.Armors);
            CollectionAssert.AreEqual(new[] { "hisame_zukin" }, GameRules.Stage("act1.sub3").Reward.Helmets);
            CollectionAssert.AreEqual(new[] { "shiden_yoroi" }, GameRules.Stage("act1.sub4").Reward.Armors);
            CollectionAssert.AreEqual(new[] { "raiko_no_tachi" }, GameRules.Stage("act1.boss").Reward.Weapons);
            CollectionAssert.AreEqual(new[] { "raijin_kabuto" }, GameRules.Stage("act1.boss").Reward.Helmets);
        }

        [Test]
        public void Stretches_and_the_Prologue_grant_no_gear()
        {
            foreach (var st in GameRules.Stages.Where(s => s.Kind == "stretch"))
                Assert.AreEqual(0, Items(st).Length, st.Id);
        }

        [Test]
        public void Trophies_are_locked_until_their_stage_is_cleared_and_then_owned_at_level_1()
        {
            var s = Fresh();
            ClearUpTo(s, "act1.sub1");
            Assert.IsTrue(GameRules.IsLocked(s, GearKind.Helmets, "sumi_eboshi"));

            var clear = GameRules.MarkStageCleared(s, "act1.sub1");

            Assert.IsFalse(GameRules.IsLocked(s, GearKind.Helmets, "sumi_eboshi"));
            Assert.AreEqual(1, s.owned.helmets["sumi_eboshi"]);
            Assert.AreEqual(1, clear.Gear.Count);
            Assert.AreEqual("helmets", clear.Gear[0].Kind);
            Assert.AreEqual("sumi_eboshi", clear.Gear[0].Id);
            Assert.AreEqual(1, clear.Unlocks.Count(u => u.Kind == "helmets" && u.Id == "sumi_eboshi"));
        }

        [Test]
        public void Raikon_grants_both_items_weapon_first_and_replaying_grants_nothing_new()
        {
            var s = Fresh();
            ClearUpTo(s, "act1.boss");
            Assert.IsTrue(GameRules.IsLocked(s, GearKind.Weapons, "raiko_no_tachi"));

            var clear = GameRules.MarkStageCleared(s, "act1.boss");
            CollectionAssert.AreEqual(new[] { "raiko_no_tachi", "raijin_kabuto" }, clear.Gear.Select(g => g.Id).ToArray());
            Assert.AreEqual(1, s.owned.weapons["raiko_no_tachi"]);
            Assert.AreEqual(1, s.owned.helmets["raijin_kabuto"]);

            var again = GameRules.MarkStageCleared(s, "act1.boss");
            Assert.AreEqual(0, again.Gear.Count);
            Assert.AreEqual(0, again.Unlocks.Count);
            Assert.AreEqual(1, s.owned.weapons["raiko_no_tachi"]);
        }

        [Test]
        public void An_item_the_player_already_owns_is_not_granted_again_or_reset()
        {
            var s = Fresh();
            s.owned.helmets["sumi_eboshi"] = 3;
            ClearUpTo(s, "act1.sub1");
            var clear = GameRules.MarkStageCleared(s, "act1.sub1");
            Assert.AreEqual(0, clear.Gear.Count);
            Assert.AreEqual(3, s.owned.helmets["sumi_eboshi"]);
        }

        [Test]
        public void The_lock_hint_names_the_foe_to_defeat()
        {
            Assert.AreEqual("Defeat Raikon", GameRules.LockHint(GearKind.Weapons, "raiko_no_tachi"));
            Assert.AreEqual("Defeat Kuroe, the Ink Censor", GameRules.LockHint(GearKind.Helmets, "sumi_eboshi"));
            Assert.AreEqual("Defeat Garan, the Stormbroken", GameRules.LockHint(GearKind.Armors, "ikazuchi_do"));
        }

        [Test]
        public void Shop_items_are_never_locked()
        {
            var s = Fresh();
            Assert.IsFalse(GameRules.IsLocked(s, GearKind.Weapons, "tsubame"));
            Assert.IsFalse(GameRules.IsLocked(s, GearKind.Helmets, "kuro_zukin"));
        }

        [Test]
        public void Dev_mode_opens_every_stage_in_any_order()
        {
            var s = GameRules.DefaultSave(true);
            foreach (var st in GameRules.Stages) Assert.IsTrue(GameRules.IsStageUnlocked(s, st.Id), st.Id);
            foreach (var ch in GameRules.Chapters) Assert.IsTrue(GameRules.IsChapterUnlocked(s, ch.Id), ch.Id);
            Assert.IsNotNull(GameRules.MarkStageCleared(s, "act1.boss"), "Raikon can be cleared straight away");
            Assert.IsTrue(s.cleared["act1.boss"]);
        }

        [Test]
        public void Without_dev_mode_the_same_stage_stays_locked()
        {
            Assert.IsFalse(GameRules.IsStageUnlocked(GameRules.DefaultSave(false), "act1.boss"));
        }
    }
}
