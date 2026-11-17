using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // The S/A/B/C rating (JS Logic.rateFight, tests/rewards.test.js). Score = health left x 40, plus 20 / 10 / 0 for a win
    // under 45 s / under 80 s / slower, plus 2 per combo hit capped at 20, plus 5 per parry, plus 20 for a flawless win.
    // S needs 85, A 62, B 42. A loss is always C.
    public class FightRatingTests
    {
        static FightResult Win(double hpLeft, double time, int maxCombo, int parries = 0, bool flawless = false)
        {
            return new FightResult { win = true, hpLeft = hpLeft, time = time, maxCombo = maxCombo, parries = parries, flawless = flawless };
        }

        [Test]
        public void A_loss_is_always_C()
        {
            var dominantLoss = Win(1, 30, 10, 1);
            dominantLoss.win = false;
            Assert.AreEqual(Rating.C, GameRules.RateFight(dominantLoss));
        }

        [Test]
        public void A_scrappy_win_is_C()
        {
            // 8 + 0 + 2 = 10
            Assert.AreEqual(Rating.C, GameRules.RateFight(Win(0.2, 90, 1)));
        }

        [Test]
        public void A_dominant_win_is_S()
        {
            // 40 + 20 + 20 + 5 = 85
            Assert.AreEqual(Rating.S, GameRules.RateFight(Win(1, 30, 10, 1)));
        }

        [Test]
        public void Eighty_three_points_is_A_and_not_S()
        {
            // the dominant win with one combo hit less: 40 + 20 + 18 + 5 = 83
            Assert.AreEqual(Rating.A, GameRules.RateFight(Win(1, 30, 9, 1)));
        }

        [Test]
        public void Sixty_two_points_is_A_and_forty_two_is_B()
        {
            // 40 + 10 + 12 = 62
            Assert.AreEqual(Rating.A, GameRules.RateFight(Win(1, 60, 6)));
            // 20 + 10 + 12 = 42
            Assert.AreEqual(Rating.B, GameRules.RateFight(Win(0.5, 60, 6)));
        }

        [Test]
        public void Just_under_the_thresholds_rates_one_letter_lower()
        {
            // 0.8 health, 60 s, 6 combo, 1 parry: 32 + 10 + 12 + 5 = 59 is B, not A
            Assert.AreEqual(Rating.B, GameRules.RateFight(Win(0.8, 60, 6, 1)));
            // 20 + 10 + 10 = 40 is C, not B
            Assert.AreEqual(Rating.C, GameRules.RateFight(Win(0.5, 60, 5)));
        }

        [Test]
        public void A_win_under_45_seconds_scores_20_and_at_45_scores_10()
        {
            // 22 + 20 = 42 is B
            Assert.AreEqual(Rating.B, GameRules.RateFight(Win(0.55, 44.99, 0)));
            // 22 + 10 = 32 is C
            Assert.AreEqual(Rating.C, GameRules.RateFight(Win(0.55, 45, 0)));
        }

        [Test]
        public void A_win_under_80_seconds_scores_10_and_at_80_scores_nothing()
        {
            // 22 + 10 + 10 = 42 is B
            Assert.AreEqual(Rating.B, GameRules.RateFight(Win(0.55, 79.99, 5)));
            // 22 + 0 + 10 = 32 is C
            Assert.AreEqual(Rating.C, GameRules.RateFight(Win(0.55, 80, 5)));
        }

        [Test]
        public void Combo_points_stop_at_20()
        {
            // 40 + 20 + min(20, 20) = 80 (A), the same for a 10 hit and a 50 hit combo
            Assert.AreEqual(Rating.A, GameRules.RateFight(Win(1, 30, 10)));
            Assert.AreEqual(Rating.A, GameRules.RateFight(Win(1, 30, 50)));
        }

        [Test]
        public void Each_parry_is_worth_5_points()
        {
            // 20 + 10 + 0 = 30 C; three parries add 15 = 45 B
            Assert.AreEqual(Rating.C, GameRules.RateFight(Win(0.5, 60, 0, 0)));
            Assert.AreEqual(Rating.B, GameRules.RateFight(Win(0.5, 60, 0, 3)));
        }

        [Test]
        public void A_flawless_win_is_worth_20_points()
        {
            // 24 + 0 + 0 = 24 C; flawless adds 20 = 44 B
            Assert.AreEqual(Rating.C, GameRules.RateFight(Win(0.6, 90, 0)));
            Assert.AreEqual(Rating.B, GameRules.RateFight(Win(0.6, 90, 0, 0, true)));
        }
    }
}
