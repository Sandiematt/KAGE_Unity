using System.IO;
using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // What the results screen shows (JS showResult, SAN-73). ResultsView is plain data built from the save, the rules' outcome and
    // the fight result; the screen only draws it. Expected texts are the JS strings; numbers come from the rules port.
    public class ResultsViewTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        static FightResult Win(double time = 50, bool flawless = false, int combo = 5, int parries = 0, double hp = 0.8)
        {
            return new FightResult { win = true, hpLeft = hp, time = time, maxCombo = combo, parries = parries, flawless = flawless };
        }

        static FightResult Loss() { return new FightResult { win = false, time = 30, enemyDamage = 0.37 }; }

        static ResultsView Play(SaveData save, string stage, FightResult r)
        {
            var o = GameRules.CompleteStage(save, stage, r);
            return ResultsView.Build(save, o, r);
        }

        [Test]
        public void A_win_says_victory_and_names_the_chapter_and_stage_in_the_eyebrow()
        {
            var v = Play(Fresh(), "prologue.1", Win());
            Assert.IsTrue(v.Win);
            Assert.AreEqual("Victory", v.Title);
            StringAssert.StartsWith(GameRules.ChapterOf("prologue.1").Name + " · ", v.Eyebrow);
            StringAssert.EndsWith(GameRules.Stage("prologue.1").Name, v.Eyebrow);
        }

        [Test]
        public void The_rating_coins_and_gems_are_the_rules_outcome_for_the_same_fight()
        {
            var save = Fresh();
            var r = Win(time: 40, flawless: true, combo: 10, parries: 2, hp: 1.0);
            var expect = GameRules.CompleteStage(Fresh(), "prologue.1", r);
            var v = ResultsView.Build(save, GameRules.CompleteStage(save, "prologue.1", r), r);
            Assert.AreEqual(expect.Rating.ToString(), v.Rating);
            Assert.AreEqual(expect.Coins, v.Coins);
            Assert.AreEqual(expect.Gems, v.Gems);
            Assert.AreEqual(expect.Xp, v.XpGain);
            Assert.IsTrue(v.Gems >= 1, "a flawless win pays a gem");
        }

        [Test]
        public void The_three_seals_are_victory_under_sixty_seconds_and_flawless()
        {
            CollectionAssert.AreEqual(new[] { "Victory", "Under 60s", "Flawless" }, Play(Fresh(), "prologue.1", Win()).SealNames);
            CollectionAssert.AreEqual(new[] { true, true, false }, Play(Fresh(), "prologue.1", Win(time: 59.9)).Seals);
            CollectionAssert.AreEqual(new[] { true, false, false }, Play(Fresh(), "prologue.1", Win(time: 60)).Seals);
            CollectionAssert.AreEqual(new[] { true, true, true }, Play(Fresh(), "prologue.1", Win(time: 30, flawless: true)).Seals);
        }

        // plays a stage on a non-dev save whose earlier stages are all cleared
        static ResultsView PlayDev(string stage, FightResult r)
        {
            var s = Fresh();
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == stage) break;
                GameRules.MarkStageCleared(s, st.Id);
            }
            var o = GameRules.CompleteStage(s, stage, r);
            return ResultsView.Build(s, o, r);
        }

        [Test]
        public void A_stretch_win_says_the_road_is_walked()
        {
            Assert.AreEqual("The road is walked.", PlayDev("act1.stretch1", Win()).Line);
            Assert.AreEqual("The road still bars the way. Rise again.", PlayDev("act1.stretch1", Loss()).Line);
        }

        [Test]
        public void A_sub_boss_has_fallen_and_a_boss_win_says_the_way_opens()
        {
            StringAssert.EndsWith(" has fallen.", PlayDev("act1.sub1", Win()).Line);
            StringAssert.EndsWith("has fallen. The way opens.", PlayDev("act1.boss", Win()).Line);
        }

        [Test]
        public void A_win_shows_the_rewards_and_continue_first_then_replay()
        {
            var v = Play(Fresh(), "prologue.1", Win());
            Assert.IsTrue(v.ShowRewards);
            CollectionAssert.AreEqual(new[] { "continue", "retry" }, v.Buttons.Select(b => b.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Continue", "Replay" }, v.Buttons.Select(b => b.Label).ToArray());
            Assert.AreEqual("continue", v.PrimaryKey);
        }

        [Test]
        public void A_loss_says_defeated_shows_the_recap_not_the_rewards_and_retry_comes_first()
        {
            var save = Fresh();
            int gemsBefore = save.gems;
            var v = Play(save, "prologue.1", Loss());
            Assert.IsFalse(v.Win);
            StringAssert.EndsWith("Rise again.", v.Line);
            Assert.AreEqual("Defeated", v.Title);
            Assert.IsFalse(v.ShowRewards);
            Assert.AreEqual(37, v.EnemyDamagePct);
            Assert.AreEqual(0, v.Gems);
            Assert.AreEqual(gemsBefore, save.gems, "a loss gives no gems");
            CollectionAssert.AreEqual(new[] { false, false, false }, v.Seals);
            CollectionAssert.AreEqual(new[] { "retry", "armory", "continue" }, v.Buttons.Select(b => b.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Retry", "Upgrade", "Map" }, v.Buttons.Select(b => b.Label).ToArray());
            StringAssert.EndsWith("still stands. Rise again.", PlayDev("act1.sub1", Loss()).Line);
            Assert.IsFalse(string.IsNullOrEmpty(v.Tip));
        }

        [Test]
        public void A_loss_does_not_clear_the_stage()
        {
            var save = Fresh();
            Play(save, "prologue.1", Loss());
            Assert.IsFalse(GameRules.IsStageComplete(save, "prologue.1"));
        }

        [Test]
        public void Boss_gear_dropped_by_the_stage_is_listed_by_name_and_chapters_are_not()
        {
            var v = PlayDev("act1.sub2", Win());
            CollectionAssert.Contains(v.Items, GameRules.Item(GearKind.Armors, "ikazuchi_do").Name);
            Assert.IsFalse(v.Items.Any(n => n.Contains("Act")), "chapter unlocks are a toast, not an item");
        }

        [Test]
        public void The_xp_bar_splits_what_was_there_from_what_was_just_earned()
        {
            var save = Fresh();
            save.xp = 100;
            var v = Play(save, "prologue.1", Win());
            double need = GameRules.XpForLevel(save.level);
            Assert.AreEqual(need, v.XpNeed);
            Assert.AreEqual(100.0 / need * 100, v.XpBasePct, 1e-9);
            Assert.AreEqual(v.XpGain / need * 100, v.XpGainPct, 1e-9);
            Assert.IsFalse(v.LevelUp);
        }

        [Test]
        public void A_level_up_fills_from_zero_and_says_so()
        {
            var save = Fresh();
            save.xp = GameRules.XpForLevel(save.level) - 1;
            var v = Play(save, "prologue.1", Win());
            Assert.IsTrue(v.LevelUp);
            Assert.AreEqual(0.0, v.XpBasePct);
            Assert.AreEqual((double)save.xp / GameRules.XpForLevel(save.level) * 100, v.XpGainPct, 1e-9);
            Assert.AreEqual(save.level, v.Level);
            CollectionAssert.Contains(v.Toasts.Select(t => t.Title).ToArray(), "Level up");
        }

        [Test]
        public void Rewards_are_in_the_written_save_and_survive_a_restart()
        {
            string dir = Path.Combine(Path.GetTempPath(), "kage_res_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SaveStore(dir);
                var save = Fresh();
                var flow = new FlowController(save, s => store.Save(s));
                flow.OpenCampaign();
                flow.SelectStage("prologue.1");
                flow.BeginLoadout();
                flow.StartStage();
                int coins0 = save.coins;
                flow.FinishStage(Win());
                var o = flow.LastOutcome;

                var reopened = new SaveStore(dir).Load();
                Assert.AreEqual(coins0 + o.Coins, reopened.coins);
                Assert.IsTrue(GameRules.IsStageComplete(reopened, "prologue.1"));
                Assert.AreEqual(save.xp, reopened.xp);
                Assert.AreEqual(save.level, reopened.level);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
