using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Stage list and unlock order for the Prologue and Act I. Expected values are the JS campaign's
    // (tests/campaign.test.js): the Prologue is one stretch; Act I is stretch -> sub-boss four times, then Raikon.
    public class StageProgressionTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        // Clears every stage before `id` in play order (not `id` itself).
        static void ClearUpTo(SaveData save, string id)
        {
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == id) break;
                GameRules.MarkStageCleared(save, st.Id);
            }
        }

        [Test]
        public void Chapters_run_Prologue_then_Act_I()
        {
            CollectionAssert.AreEqual(new[] { "prologue", "act1" }, GameRules.Chapters.Select(c => c.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "Prologue", "Act I" }, GameRules.Chapters.Select(c => c.Name).ToArray());
        }

        [Test]
        public void The_Prologue_is_a_single_stretch()
        {
            var stages = GameRules.Chapter("prologue").Stages;
            CollectionAssert.AreEqual(new[] { "prologue.1" }, stages.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "stretch" }, stages.Select(s => s.Kind).ToArray());
        }

        [Test]
        public void Act_I_is_four_stretches_each_followed_by_its_sub_boss_then_Raikon()
        {
            var stages = GameRules.Chapter("act1").Stages;
            CollectionAssert.AreEqual(new[]
            {
                "act1.stretch1", "act1.sub1", "act1.stretch2", "act1.sub2", "act1.stretch3", "act1.sub3",
                "act1.stretch4", "act1.sub4", "act1.boss",
            }, stages.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[]
            {
                "stretch", "subboss", "stretch", "subboss", "stretch", "subboss", "stretch", "subboss", "boss",
            }, stages.Select(s => s.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { "Kuroe", "Garan", "Hisame", "Shiden" },
                stages.Where(s => s.Kind == "subboss").Select(s => s.Foe).ToArray());
            Assert.AreEqual("Raikon", stages[8].Foe);
        }

        [Test]
        public void Stage_list_has_the_ten_stages_in_play_order_with_unique_ids()
        {
            var ids = GameRules.Stages.Select(s => s.Id).ToArray();
            Assert.AreEqual(10, ids.Length);
            Assert.AreEqual(10, ids.Distinct().Count());
            Assert.AreEqual("prologue.1", ids[0]);
            Assert.AreEqual("act1.stretch1", ids[1]);
            Assert.AreEqual("act1.boss", ids[9]);
        }

        [Test]
        public void A_new_player_can_only_play_the_Prologue()
        {
            var s = Fresh();
            Assert.IsTrue(GameRules.IsStageUnlocked(s, "prologue.1"));
            Assert.IsFalse(GameRules.IsStageUnlocked(s, "act1.stretch1"));
            Assert.IsFalse(GameRules.IsChapterUnlocked(s, "act1"));
            Assert.AreEqual("prologue.1", GameRules.NextStage(s));
        }

        [Test]
        public void Completing_the_Prologue_unlocks_Act_I_stretch_1_and_opens_Act_I()
        {
            var s = Fresh();
            var clear = GameRules.MarkStageCleared(s, "prologue.1");
            Assert.IsTrue(clear.FirstClear);
            Assert.AreEqual("act1.stretch1", clear.Next);
            Assert.AreEqual(1, clear.Unlocks.Count);
            Assert.AreEqual("chapter", clear.Unlocks[0].Kind);
            Assert.AreEqual("act1", clear.Unlocks[0].Id);
            Assert.IsTrue(GameRules.IsStageComplete(s, "prologue.1"));
            Assert.IsTrue(GameRules.IsChapterComplete(s, "prologue"));
            Assert.IsTrue(GameRules.IsStageUnlocked(s, "act1.stretch1"));
            Assert.IsFalse(GameRules.IsStageUnlocked(s, "act1.sub1"));
            Assert.AreEqual("act1.stretch1", GameRules.NextStage(s));
        }

        [Test]
        public void A_stretch_unlocks_its_own_sub_boss_next_and_the_fourth_sub_boss_unlocks_Raikon()
        {
            var s = Fresh();
            ClearUpTo(s, "act1.stretch2");
            Assert.IsTrue(GameRules.IsStageUnlocked(s, "act1.stretch2"));
            Assert.IsFalse(GameRules.IsStageUnlocked(s, "act1.sub2"));
            GameRules.MarkStageCleared(s, "act1.stretch2");
            Assert.AreEqual("act1.sub2", GameRules.NextStage(s));
            Assert.IsTrue(GameRules.IsStageUnlocked(s, "act1.sub2"));

            ClearUpTo(s, "act1.boss");
            Assert.IsTrue(GameRules.IsStageUnlocked(s, "act1.boss"));
        }

        [Test]
        public void A_stage_cannot_be_completed_while_it_is_locked()
        {
            var s = Fresh();
            Assert.IsNull(GameRules.MarkStageCleared(s, "act1.boss"));
            Assert.IsFalse(GameRules.IsStageComplete(s, "act1.boss"));
            Assert.IsFalse(s.defeated.ContainsKey("act1.boss"));
        }

        [Test]
        public void A_cleared_stage_stays_open_and_replaying_it_is_not_a_first_clear()
        {
            var s = Fresh();
            GameRules.MarkStageCleared(s, "prologue.1");
            GameRules.MarkStageCleared(s, "act1.stretch1");
            Assert.IsTrue(GameRules.IsStageUnlocked(s, "prologue.1"));

            var again = GameRules.MarkStageCleared(s, "prologue.1");
            Assert.IsFalse(again.FirstClear);
            Assert.AreEqual(0, again.Unlocks.Count);
            Assert.AreEqual("act1.sub1", GameRules.NextStage(s), "replays do not move the story frontier");
            Assert.AreEqual(2, s.defeated["prologue.1"], "every win counts");
        }

        [Test]
        public void Chapter_progress_counts_cleared_stages()
        {
            var s = Fresh();
            ClearUpTo(s, "act1.stretch3");
            var act1 = GameRules.ChapterProgress(s, "act1");
            Assert.AreEqual(4, act1.Done);
            Assert.AreEqual(9, act1.Total);
        }

        [Test]
        public void Unknown_stage_ids_are_never_unlocked()
        {
            Assert.IsFalse(GameRules.IsStageUnlocked(Fresh(), "act9.boss"));
            Assert.IsNull(GameRules.MarkStageCleared(Fresh(), "act9.boss"));
        }

        [Test]
        public void Beating_Raikon_leaves_nothing_left_to_play_in_this_scope()
        {
            var s = Fresh();
            ClearUpTo(s, "act1.boss");
            var clear = GameRules.MarkStageCleared(s, "act1.boss");
            Assert.IsNull(clear.Next);
            Assert.IsNull(GameRules.NextStage(s));
            Assert.IsTrue(GameRules.IsChapterComplete(s, "act1"));
        }
    }
}
