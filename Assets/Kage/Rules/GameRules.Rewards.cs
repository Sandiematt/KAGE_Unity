using System;
using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>The performance rating of a fight, best first.</summary>
    public enum Rating
    {
        S,
        A,
        B,
        C,
    }

    /// <summary>
    /// What a finished stage hands the rules (ADR-0009): built by the fight stats recorder from the event stream.
    /// </summary>
    public sealed class FightResult
    {
        public bool win;
        /// <summary>Player health left at the end, as a fraction of maximum (0 to 1).</summary>
        public double hpLeft;
        /// <summary>Fight time in seconds.</summary>
        public double time;
        public int maxCombo;
        public int parries;
        public bool flawless;
    }

    /// <summary>The reward tier of a stage: stretches pay like regular fights; sub-bosses and Raikon pay more.</summary>
    public enum RewardType
    {
        Regular,
        Sub,
        Boss,
    }

    /// <summary>What a finished stage earned (JS applyFightResult), after the save was updated.</summary>
    public sealed class StageOutcome
    {
        public string Stage;
        public bool Win;
        public Rating Rating;
        public int Coins, Gems, Xp;
        public bool FirstClear;
        /// <summary>A win on a stage that is still locked: nothing was earned and the save is unchanged.</summary>
        public bool Rejected;
        /// <summary>The story frontier after the clear, or null.</summary>
        public string Next;
        /// <summary>Chapters and items the clear opened.</summary>
        public List<StageUnlock> Unlocks = new List<StageUnlock>();
        public int LevelUps, LevelBefore, XpBefore;
    }

    /// <summary>What the stage card promises before the fight: a C-rated win without the flawless gem.</summary>
    public struct RewardPreview
    {
        public int Coins, Gems;
    }

    public static partial class GameRules
    {
        /// <summary>JS Math.round for the positive numbers used here (halves round up).</summary>
        static int JsRound(double x) { return (int)Math.Floor(x + 0.5); }

        static int RoundToFive(double x) { return JsRound(x / 5) * 5; }

        public static double RewardTypeMul(RewardType type)
        {
            return type == RewardType.Boss ? 3.4 : type == RewardType.Sub ? 2.2 : 1;
        }

        public static RewardType StageRewardType(string stageId)
        {
            var st = Stage(stageId);
            return st != null && st.Kind == "boss" ? RewardType.Boss : st != null && st.Kind == "subboss" ? RewardType.Sub : RewardType.Regular;
        }

        /// <summary>Base coins of a stage: (60 + 45 x chapter tier) x the reward type multiplier.</summary>
        public static double StageBaseCoins(string stageId)
        {
            return (60 + 45 * ChapterOf(stageId).Diff) * RewardTypeMul(StageRewardType(stageId));
        }

        public static int FirstClearGems(RewardType type)
        {
            return type == RewardType.Boss ? 5 : type == RewardType.Sub ? 2 : 0;
        }

        static double RatingCoinMul(Rating r)
        {
            return r == Rating.S ? 1.5 : r == Rating.A ? 1.25 : r == Rating.B ? 1.1 : 1;
        }

        /// <summary>XP needed to leave a player level (JS xpForLevel).</summary>
        public static int XpForLevel(int level) { return 200 + 120 * (level - 1); }

        public static RewardPreview StageRewardPreview(SaveData save, string stageId)
        {
            if (Stage(stageId) == null) return new RewardPreview();
            bool cleared = IsStageComplete(save, stageId);
            return new RewardPreview
            {
                Coins = RoundToFive(StageBaseCoins(stageId) * (cleared ? 0.5 : 1)),
                Gems = cleared ? 0 : FirstClearGems(StageRewardType(stageId)),
            };
        }

        /// <summary>
        /// Applies a finished stage to the save and returns what it earned (JS applyFightResult for campaign fights).
        /// A win completes the stage (see <see cref="MarkStageCleared"/>); a win on a locked stage earns nothing and comes
        /// back with Rejected set. The save is updated in place; writing it is the caller's job.
        /// </summary>
        public static StageOutcome CompleteStage(SaveData save, string stageId, FightResult r)
        {
            var st = Stage(stageId);
            if (st == null) throw new ArgumentException("unknown stage: " + stageId, "stageId");
            var type = StageRewardType(stageId);
            int diff = ChapterOf(stageId).Diff;
            double baseCoins = StageBaseCoins(stageId);
            var o = new StageOutcome { Stage = stageId, Win = r.win, Rating = RateFight(r) };

            if (r.win)
            {
                var clear = MarkStageCleared(save, stageId);
                if (clear == null)
                {
                    o.Rejected = true;
                    o.LevelBefore = save.level;
                    o.XpBefore = save.xp;
                    return o;
                }
                o.FirstClear = clear.FirstClear;
                o.Coins = RoundToFive(baseCoins * RatingCoinMul(o.Rating) * (o.FirstClear ? 1 : 0.5));
                o.Gems = (o.FirstClear ? FirstClearGems(type) : (type == RewardType.Boss ? 1 : 0)) + (r.flawless ? 1 : 0);
                o.Xp = JsRound((type == RewardType.Boss ? 300 : type == RewardType.Sub ? 150 : 80) * diff);
                save.stats.wins++;
                if (r.flawless) save.stats.perfects++;
                if (type == RewardType.Boss) save.stats.bosses++;
                o.Unlocks.AddRange(clear.Unlocks);
                o.Next = clear.Next;
            }
            else
            {
                o.Coins = RoundToFive(baseCoins * 0.25);
                o.Xp = 20 * diff;
                save.stats.losses++;
            }

            save.coins += o.Coins;
            save.gems += o.Gems;
            save.xp += o.Xp;
            o.LevelBefore = save.level;
            o.XpBefore = save.xp - o.Xp;
            while (save.xp >= XpForLevel(save.level))
            {
                save.xp -= XpForLevel(save.level);
                save.level++;
                o.LevelUps++;
            }
            return o;
        }

        /// <summary>The performance rating of a fight (JS rateFight). A loss is always C.</summary>
        public static Rating RateFight(FightResult r)
        {
            if (!r.win) return Rating.C;
            double score = r.hpLeft * 40
                + (r.time < 45 ? 20 : r.time < 80 ? 10 : 0)
                + Math.Min(20, r.maxCombo * 2)
                + r.parries * 5
                + (r.flawless ? 20 : 0);
            return score >= 85 ? Rating.S : score >= 62 ? Rating.A : score >= 42 ? Rating.B : Rating.C;
        }
    }
}
