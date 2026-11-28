using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // What the campaign map shows (SAN-67): chapters and stages with their locked / open / cleared state taken from the
    // save, and the stage card's words and reward. CampaignView is the pure view model under the map screen.
    public class CampaignViewTests
    {
        static CampaignView View(SaveData save, string chapter = "prologue", string selected = null)
        {
            return CampaignView.Build(save, chapter, selected);
        }

        [Test]
        public void The_map_covers_the_prologue_and_act_one_in_story_order()
        {
            var v = View(GameRules.DefaultSave(false));
            CollectionAssert.AreEqual(new[] { "prologue", "act1" }, v.Chapters.Select(c => c.Id).ToArray());
            Assert.AreEqual("Hoshimura", v.Chapters[0].Title);
            Assert.AreEqual("The Outer Districts", v.Chapters[1].Title);
        }

        [Test]
        public void A_fresh_save_has_only_the_first_stage_open()
        {
            var save = GameRules.DefaultSave(false);
            Assert.AreEqual(StageState.Open, View(save, "prologue").Stages[0].State);
            var act1 = View(save, "act1");
            foreach (var st in act1.Stages) Assert.AreEqual(StageState.Locked, st.State, st.Id);
            Assert.AreEqual(ChapterState.Open, View(save).Chapters[0].State);
            Assert.AreEqual(ChapterState.Locked, View(save).Chapters[1].State);
        }

        [Test]
        public void Clearing_a_stage_opens_the_next_one_in_order()
        {
            var save = GameRules.DefaultSave(false);
            GameRules.MarkStageCleared(save, "prologue.1");
            var prologue = View(save, "prologue");
            Assert.AreEqual(StageState.Cleared, prologue.Stages[0].State);
            Assert.AreEqual(ChapterState.Cleared, prologue.Chapters[0].State);
            var act1 = View(save, "act1");
            Assert.AreEqual(StageState.Open, act1.Stages[0].State);
            Assert.AreEqual(StageState.Locked, act1.Stages[1].State, "the second stage waits for the first");
            Assert.AreEqual(ChapterState.Open, act1.Chapters[1].State);
        }

        [Test]
        public void The_next_stage_is_marked_as_the_story_frontier()
        {
            var save = GameRules.DefaultSave(false);
            GameRules.MarkStageCleared(save, "prologue.1");
            GameRules.MarkStageCleared(save, "act1.stretch1");
            var act1 = View(save, "act1");
            Assert.IsTrue(act1.Stages.Single(s => s.Id == "act1.sub1").IsNext);
            Assert.AreEqual(1, act1.Stages.Count(s => s.IsNext));
        }

        [Test]
        public void Dev_mode_shows_every_stage_unlocked()
        {
            var save = GameRules.SetDevMode(GameRules.DefaultSave(false), true);
            foreach (var chId in new[] { "prologue", "act1" })
            {
                var v = View(save, chId);
                foreach (var st in v.Stages) Assert.AreEqual(StageState.Open, st.State, st.Id);
            }
            Assert.IsTrue(View(save).Chapters.All(c => c.State != ChapterState.Locked));
        }

        [Test]
        public void Chapter_progress_counts_cleared_stages()
        {
            var save = GameRules.DefaultSave(false);
            GameRules.MarkStageCleared(save, "prologue.1");
            GameRules.MarkStageCleared(save, "act1.stretch1");
            GameRules.MarkStageCleared(save, "act1.sub1");
            var v = View(save, "act1");
            Assert.AreEqual(2, v.Chapters[1].Done);
            Assert.AreEqual(9, v.Chapters[1].Total);
            Assert.AreEqual(1, v.Chapters[0].Done);
        }

        [Test]
        public void Act_one_lists_four_stretches_four_sub_bosses_and_the_boss()
        {
            var v = View(GameRules.DefaultSave(false), "act1");
            Assert.AreEqual(9, v.Stages.Length);
            Assert.AreEqual(4, v.Stages.Count(s => s.Kind == "stretch"));
            Assert.AreEqual(4, v.Stages.Count(s => s.Kind == "subboss"));
            Assert.AreEqual("boss", v.Stages.Last().Kind);
            Assert.AreEqual("act1.boss", v.Stages.Last().Id);
        }

        [Test]
        public void A_stage_card_says_what_the_stage_is_and_what_it_pays()
        {
            var save = GameRules.DefaultSave(false);
            var card = View(save, "prologue", "prologue.1").Card;
            Assert.AreEqual("prologue.1", card.StageId);
            Assert.AreEqual("The Empty Village", card.Title);
            Assert.AreEqual("Prologue · Stretch", card.KindLine);
            Assert.AreEqual("Fight", card.Action);
            Assert.IsTrue(card.CanStart);
            Assert.AreEqual(GameRules.StageRewardPreview(save, "prologue.1").Coins, card.Coins);
            StringAssert.Contains("2 encounters", card.Description, "the Prologue road has the tutorial Hollow and the Hollow Shade (SAN-96)");
        }

        [Test]
        public void A_locked_stage_card_cannot_be_started()
        {
            var card = View(GameRules.DefaultSave(false), "act1", "act1.stretch2").Card;
            Assert.AreEqual("Locked", card.Action);
            Assert.IsFalse(card.CanStart);
        }

        [Test]
        public void A_cleared_stage_card_offers_a_replay()
        {
            var save = GameRules.DefaultSave(false);
            GameRules.MarkStageCleared(save, "prologue.1");
            var card = View(save, "prologue", "prologue.1").Card;
            Assert.AreEqual("Replay", card.Action);
            Assert.IsTrue(card.CanStart);
            Assert.IsTrue(card.Cleared);
        }

        [Test]
        public void A_boss_stage_card_is_named_after_the_boss_and_shows_the_boss_art()
        {
            var save = GameRules.SetDevMode(GameRules.DefaultSave(false), true);
            var card = View(save, "act1", "act1.boss").Card;
            Assert.AreEqual("Raikon", card.Title);
            Assert.AreEqual("Act I · Main boss", card.KindLine);
            Assert.AreEqual("raikon", card.BossArt);
            Assert.IsNull(View(save, "act1", "act1.sub1").Card.BossArt);
        }

        [Test]
        public void A_sub_boss_card_names_the_foe_and_its_title()
        {
            var save = GameRules.SetDevMode(GameRules.DefaultSave(false), true);
            var card = View(save, "act1", "act1.sub1").Card;
            Assert.AreEqual("Sub-boss I", card.Title);
            StringAssert.Contains("Kuroe, the Ink Censor", card.Description);
        }

        [Test]
        public void A_sub_boss_card_carries_its_portrait_foe_and_the_gear_he_gives_up()
        {
            var save = GameRules.DefaultSave(false);
            var card = View(save, "act1", "act1.sub1").Card;
            Assert.AreEqual("kuroe", card.PortraitFoe);
            Assert.AreEqual("Kuroe", card.FoeName);
            Assert.AreEqual("the Ink Censor", card.FoeTitle);
            Assert.IsEmpty(card.Spoils);

            // only a sub-boss card is drawn with the foe himself
            Assert.IsNull(View(save, "act1", "act1.stretch1").Card.PortraitFoe);
            Assert.IsNull(View(save, "act1", "act1.boss").Card.PortraitFoe);
        }

        [Test]
        public void With_no_stage_selected_the_card_shows_the_first_open_stage_of_the_chapter()
        {
            var save = GameRules.DefaultSave(false);
            GameRules.MarkStageCleared(save, "prologue.1");
            var v = View(save, "act1");
            Assert.AreEqual("act1.stretch1", v.Card.StageId);
        }
    }
}
