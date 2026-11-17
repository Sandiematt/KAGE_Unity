namespace Kage.Rules
{
    /// <summary>
    /// Dev mode defaults (ADR-0009). The JS game keeps these in DEV (10-data.js): the grants of the developer unlock.
    /// </summary>
    public static class DevDefaults
    {
        /// <summary>
        /// Whether a brand-new install starts in dev mode. On while the game is in development, so the owner can test any
        /// content at once; set it to false for a release. A save already on disk keeps its own flag.
        /// </summary>
        public static readonly bool DevModeOnFreshSave = true;

        /// <summary>Dev mode raises coins to at least this much (JS DEV.coins).</summary>
        public const int Coins = 99999;

        /// <summary>Dev mode raises gems to at least this much (JS DEV.gems).</summary>
        public const int Gems = 999;
    }
}
