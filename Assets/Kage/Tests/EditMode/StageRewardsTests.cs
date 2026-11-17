using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Coins, gems and XP for a finished stage (JS applyFightResult, stageRewardPreview, tests/rewards.test.js).
    // Expected numbers come from running the JS rules on the same results, plus the JS test's own worked examples:
    // base coins (60 + 45 x chapter tier) x 1 / 2.2 / 3.4 for stretch / sub-boss / boss, x 1.5 / 1.25 / 1.1 / 1 for S / A / B / C,
    // halved on a repeat clear, rounded to 5; first-clear gems 0 / 2 / 5 (+1 for a flawless win, 1 per boss replay);
    // XP 80 / 150 / 300 per chapter tier; a loss pays a quarter of the base coins and 20 XP.
    public class StageRewardsTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        // Results that rate C, B, A and S with no flawless flag.
        static FightResult Scrappy() { return new FightResult { win = true, hpLeft = 0.2, time = 90, maxCombo = 1 }; }
        static FightResult Good() { return new FightResult { win = true, hpLeft = 0.8, time = 60, maxCombo = 6, parries = 1 }; }
        static FightResult Great() { return new FightResult { win = true, hpLeft = 1, time = 60, maxCombo = 6 }; }
        static FightResult Dominant() { return new FightResult { win = true, hpLeft = 1, time = 30, maxCombo = 10, parries = 1 }; }

        static SaveData AtStage(string id, SaveData s = null)
        {
            if (s == null) s = Fresh();
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == id) break;
                GameRules.MarkStageCleared(s, st.Id);
            }
            return s;
        }

        // ---- the first win on a stage ----

        [Test]
        public void First_win_on_the_Prologue_pays_105_coins_and_80_XP_and_clears_the_stage()
        {
            var s = Fresh();
            var o = GameRules.CompleteStage(s, "prologue.1", Scrappy());
            Assert.AreEqual(Rating.C, o.Rating);
            Assert.IsTrue(o.FirstClear);
            Assert.AreEqual(105, o.Coins);
            Assert.AreEqual(0, o.Gems);
            Assert.AreEqual(80, o.Xp);
            Assert.AreEqual(250 + 105, s.coins);
            Assert.AreEqual(80, s.xp);
            Assert.IsTrue(GameRules.IsStageComplete(s, "prologue.1"));
            Assert.AreEqual(1, GameRules.DefeatedCount(s, "prologue.1"));
            Assert.AreEqual("act1.stretch1", o.Next);
            Assert.AreEqual(1, o.Unlocks.Count);
            Assert.AreEqual("chapter", o.Unlocks[0].Kind);
            Assert.AreEqual("act1", o.Unlocks[0].Id);
            Assert.AreEqual(1, s.stats.wins);
        }

        [TestCase("prologue.1", 105, 0, 80, 55, 0)]
        [TestCase("act1.stretch1", 105, 0, 80, 55, 0)]
        [TestCase("act1.sub1", 230, 2, 150, 115, 0)]
        [TestCase("act1.sub4", 230, 2, 150, 115, 0)]
        [TestCase("act1.boss", 355, 5, 300, 180, 1)]
        public void A_C_rated_win_pays_the_base_reward_the_first_time_and_half_coins_after(
            string stage, int coins, int gems, int xp, int replayCoins, int replayGems)
        {
            var s = AtStage(stage);
            var first = GameRules.CompleteStage(s, stage, Scrappy());
            Assert.AreEqual(coins, first.Coins, "first coins");
            Assert.AreEqual(gems, first.Gems, "first gems");
            Assert.AreEqual(xp, first.Xp, "xp");

            var replay = GameRules.CompleteStage(s, stage, Scrappy());
            Assert.IsFalse(replay.FirstClear);
            Assert.AreEqual(replayCoins, replay.Coins, "replay coins");
            Assert.AreEqual(replayGems, replay.Gems, "replay gems");
            Assert.AreEqual(xp, replay.Xp, "a replay still pays the full XP");
            Assert.AreEqual(0, replay.Unlocks.Count);
            Assert.AreEqual(2, GameRules.DefeatedCount(s, stage));
        }

        // ---- the rating scales the coins ----

        [TestCase("prologue.1", 115, 130, 160)]
        [TestCase("act1.sub2", 255, 290, 345)]
        [TestCase("act1.boss", 395, 445, 535)]
        public void Coins_rise_with_the_rating_B_A_S(string stage, int b, int a, int sRating)
        {
            Assert.AreEqual(b, GameRules.CompleteStage(AtStage(stage), stage, Good()).Coins);
            Assert.AreEqual(a, GameRules.CompleteStage(AtStage(stage), stage, Great()).Coins);
            var o = GameRules.CompleteStage(AtStage(stage), stage, Dominant());
            Assert.AreEqual(Rating.S, o.Rating);
            Assert.AreEqual(sRating, o.Coins);
        }

        [Test]
        public void Repeat_clears_pay_half_of_the_rated_coins()
        {
            var s = AtStage("act1.sub1");
            GameRules.CompleteStage(s, "act1.sub1", Dominant());
            Assert.AreEqual(175, GameRules.CompleteStage(s, "act1.sub1", Dominant()).Coins);
        }

        // ---- flawless ----

        [Test]
        public void A_flawless_win_earns_a_bonus_gem_and_counts_as_a_perfect()
        {
            var s = Fresh();
            var r = Scrappy();
            r.flawless = true;
            var o = GameRules.CompleteStage(s, "prologue.1", r);
            Assert.AreEqual(1, o.Gems);
            Assert.AreEqual(1, s.stats.perfects);
            Assert.AreEqual(5 + 1, s.gems);
        }

        [Test]
        public void A_flawless_win_over_a_sub_boss_adds_one_gem_to_the_first_clear_two()
        {
            var s = AtStage("act1.sub1");
            var r = Scrappy();
            r.flawless = true;
            Assert.AreEqual(3, GameRules.CompleteStage(s, "act1.sub1", r).Gems);
        }

        // ---- a loss ----

        [Test]
        public void A_loss_pays_a_quarter_of_the_base_coins_and_20_XP_and_clears_nothing()
        {
            var s = Fresh();
            var r = Scrappy();
            r.win = false;
            var o = GameRules.CompleteStage(s, "prologue.1", r);
            Assert.AreEqual(Rating.C, o.Rating);
            Assert.AreEqual(25, o.Coins);
            Assert.AreEqual(20, o.Xp);
            Assert.AreEqual(0, o.Gems);
            Assert.IsFalse(GameRules.IsStageComplete(s, "prologue.1"));
            Assert.AreEqual("prologue.1", GameRules.NextStage(s));
            Assert.AreEqual(1, s.stats.losses);
            Assert.AreEqual(250 + 25, s.coins);
        }

        [Test]
        public void A_loss_to_a_sub_boss_or_Raikon_still_pays_a_quarter_of_that_stages_base()
        {
            var lose = Scrappy();
            lose.win = false;
            Assert.AreEqual(60, GameRules.CompleteStage(AtStage("act1.sub1"), "act1.sub1", lose).Coins);
            Assert.AreEqual(90, GameRules.CompleteStage(AtStage("act1.boss"), "act1.boss", lose).Coins);
        }

        // ---- Raikon ----

        [Test]
        public void Beating_Raikon_pays_boss_rewards_and_a_replay_pays_one_gem()
        {
            var s = AtStage("act1.boss");
            var o = GameRules.CompleteStage(s, "act1.boss", Dominant());
            Assert.AreEqual(Rating.S, o.Rating);
            Assert.AreEqual(535, o.Coins);
            Assert.AreEqual(5, o.Gems);
            Assert.AreEqual(300, o.Xp);
            Assert.AreEqual(1, s.stats.bosses);
            CollectionAssert.AreEqual(new[] { "raiko_no_tachi", "raijin_kabuto" }, o.Unlocks.Select(u => u.Id).ToArray());

            var again = GameRules.CompleteStage(s, "act1.boss", Dominant());
            Assert.AreEqual(0, again.Unlocks.Count);
            Assert.AreEqual(1, again.Gems, "a boss replay still pays one gem");
        }

        // ---- a win on a locked stage ----

        [Test]
        public void Winning_a_stage_that_is_still_locked_earns_nothing_and_is_flagged_rejected()
        {
            var s = Fresh();
            string before = JsonConvert.SerializeObject(s);
            var r = Dominant();
            r.flawless = true;
            var o = GameRules.CompleteStage(s, "act1.boss", r);
            Assert.IsTrue(o.Rejected);
            Assert.AreEqual(0, o.Coins);
            Assert.AreEqual(0, o.Gems);
            Assert.AreEqual(0, o.Xp);
            Assert.AreEqual(0, o.Unlocks.Count);
            Assert.IsFalse(o.FirstClear);
            Assert.AreEqual(before, JsonConvert.SerializeObject(s), "no coins, XP, gems, stats or progress reach the save");
            Assert.IsFalse(GameRules.CompleteStage(s, "prologue.1", Scrappy()).Rejected, "an open stage pays as usual");
        }

        [Test]
        public void Dev_mode_stages_are_never_rejected()
        {
            var s = GameRules.DefaultSave(true);
            Assert.IsFalse(GameRules.CompleteStage(s, "act1.boss", Dominant()).Rejected);
        }

        // ---- XP and levels ----

        [Test]
        public void Each_level_needs_120_more_XP_than_the_last_starting_at_200()
        {
            Assert.AreEqual(200, GameRules.XpForLevel(1));
            Assert.AreEqual(320, GameRules.XpForLevel(2));
            Assert.AreEqual(1280, GameRules.XpForLevel(10));
        }

        [Test]
        public void Enough_XP_levels_the_player_up_and_carries_the_remainder_over()
        {
            var s = Fresh();
            s.xp = 150;
            var o = GameRules.CompleteStage(s, "prologue.1", Scrappy());
            Assert.AreEqual(1, o.LevelUps);
            Assert.AreEqual(1, o.LevelBefore);
            Assert.AreEqual(150, o.XpBefore);
            Assert.AreEqual(2, s.level);
            Assert.AreEqual(30, s.xp);
        }

        [Test]
        public void A_big_XP_gain_can_level_up_more_than_once()
        {
            var s = AtStage("act1.boss");
            s.xp = 400;
            // 400 + 300 = 700: level 1 needs 200 (500 left), level 2 needs 320 (180 left), level 3 needs 440
            var o = GameRules.CompleteStage(s, "act1.boss", Scrappy());
            Assert.AreEqual(2, o.LevelUps);
            Assert.AreEqual(3, s.level);
            Assert.AreEqual(180, s.xp);
        }

        // ---- the stage card preview ----

        [Test]
        public void The_stage_card_previews_the_first_clear_and_the_replay_reward()
        {
            var s = Fresh();
            AssertPreview(s, "prologue.1", 105, 0);
            AssertPreview(s, "act1.boss", 355, 5);
            AssertPreview(s, "act1.sub1", 230, 2);
            AtStage("act1.boss", s);
            GameRules.MarkStageCleared(s, "act1.boss");
            AssertPreview(s, "act1.boss", 180, 0);
        }

        static void AssertPreview(SaveData s, string stage, int coins, int gems)
        {
            var p = GameRules.StageRewardPreview(s, stage);
            Assert.AreEqual(coins, p.Coins, stage + " coins");
            Assert.AreEqual(gems, p.Gems, stage + " gems");
        }
    }
}
