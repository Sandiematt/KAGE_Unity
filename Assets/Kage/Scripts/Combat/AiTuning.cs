namespace Kage
{
    /// <summary>
    /// The numbers the AI profiles (<see cref="AiProfile"/>, SAN-95) need that are not in the JS archetype table:
    /// Resources/Tuning/ai.json. The profile numbers themselves (rates, ranges, reaction, combo length) come from the
    /// generated JS table and are never here. Distances are metres, times frames (60 a second). The JS game's pixels are
    /// converted at 115 a metre (ADR-0010); where a number is the JS one the comment says which.
    /// </summary>
    [System.Serializable, TuningFile("ai")]
    public class AiTuning
    {
        // ---- stage level ----
        /// <summary>
        /// <see cref="AIInputSource.level"/> (0 easy to 1 hard, the stage's difficulty) scales the profile's aggression, guard, parry and
        /// punish by <c>levelBase + levelSlope x level</c> and its reaction time by the inverse (JS Logic.aiTuning's stage curve). At
        /// the default level 0.6 it is exactly 1, so the profile's own numbers are what the foe does.
        /// </summary>
        public float levelBase = 0.7f, levelSlope = 0.5f;
        /// <summary>No profile rate goes above this after the level scale (JS <c>cap</c>).</summary>
        public float rateCap = 0.95f;

        // ---- reaction ----
        /// <summary>
        /// The profile's reaction frames (JS seconds x 60) are multiplied by this: Unity's moves start up in 6 to 20 frames where the JS
        /// game's take a quarter of a second, so at 1 a foe would never guard a jab. At 0.5 the delay still decides jabs and the
        /// quick kicks and lets a slow iai be guarded.
        /// </summary>
        public float reactionScale = 0.5f;

        // ---- ranges ----
        /// <summary>The reach of the sword family's strikes: where the AI's close band ends (the legacy bands' 0.82 and 1.25).</summary>
        public float closeReach = 0.82f, meleeReach = 1.25f;
        /// <summary>
        /// It attacks from this much beyond <see cref="meleeReach"/>. The JS game adds 30 px (0.26 m); Unity's strikes close the gap quicker, and a
        /// smaller margin keeps a foe whose preferred range lies beyond it (the agile profile's 1.39 m) outside its own range, where it dashes in.
        /// </summary>
        public float attackRangeMargin = 0.08f;
        /// <summary>A ranged foe backs off when nearer than its preferred range minus this (JS 120 px).</summary>
        public float rangedBackOff = 1.04f;
        /// <summary>A ranged foe shoots from beyond this (JS 220 px); any foe with a Special shot throws it from beyond <see cref="specialFar"/> (JS 300 px).</summary>
        public float shotRange = 1.9f, specialFar = 2.6f;
        /// <summary>A dash-in (JS 420 px) and a jump-in (JS 380 px) start from no further than this.</summary>
        public float dashInRange = 3.65f, jumpInRange = 3.3f;
        /// <summary>A throw is tried from this close (JS 90 px).</summary>
        public float grabRange = 0.78f;
        /// <summary>The Special (the ranged shot) needs this much Shadow, as in the legacy AI.</summary>
        public float specialMeter = 35f;

        // ---- decisions ----
        /// <summary>Chance per decision, once in range and not attacking, that it side-steps (strafes); and that it holds its guard up for a moment (JS 0.35, 0.2).</summary>
        public float strafeChance = 0.35f, holdGuardChance = 0.2f;
        /// <summary>A voluntary guard lasts this many frames (JS 0.3 to 0.7 s).</summary>
        public int holdGuardMin = 18, holdGuardMax = 42;
        /// <summary>After a hit, the chance it chains the next attack of the string while its combo has attacks left.</summary>
        public float comboFollow = 0.85f;
        /// <summary>Below this share of its health the AI changes aggression: by <see cref="lowHealthRashness"/> (JS +0.15), or minus <see cref="lowHealthBoldness"/> (JS 0.1) for a cautious profile.</summary>
        public float lowHealthFraction = 0.35f, lowHealthRashness = 0.15f, lowHealthBoldness = 0.1f;
        /// <summary>A boss phase's <c>aggressive</c> flag adds this to aggression (JS 0.2) and multiplies ability chances by <see cref="aggressiveAbilityMul"/> (JS 1.3). SAN-102 sets <see cref="AIInputSource.aggressive"/>.</summary>
        public float aggressiveBonus = 0.2f, aggressiveAbilityMul = 1.3f;
        /// <summary>A heavy profile's decision gaps are this much longer (JS 1.3).</summary>
        public float heavyThinkMul = 1.3f;
        /// <summary>The chance that an unanswered attack (no parry, guard or dodge rolled) is traded with a quick punch, times aggression (JS 0.4).</summary>
        public float tradeChance = 0.4f;
        /// <summary>An attack that cannot be guarded (Unblockable, a grab) is never guarded, and the dodge chance is higher by this (JS 0.3).</summary>
        public float unblockableDodgeBonus = 0.3f;
    }
}
