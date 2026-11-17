using System;

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

    public static partial class GameRules
    {
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
