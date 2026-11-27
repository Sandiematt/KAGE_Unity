using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Parry numbers (Resources/Tuning/parry.json, ADR-0004, SAN-91). Times are in frames (60 a second). The JS game's
    /// parry: a guard raised less than 0.15 s (9 frames, 0 to 8) before the blow; the parried fighter reels 0.95 s (a
    /// regular foe), 0.62 s (sub-boss) or 0.5 s (boss) and loses 55, 34 or 26 of its 100 posture; Kage's own cut turned
    /// aside by a foe is a 0.46 s recoil and 30 posture; the parrier gains 14 Rage. The spec asks for about 8 frames of
    /// window, about 0.5 s of stagger and about 0.33 s of lockout. The window and the Rage are the JS's; the stagger
    /// and posture are the JS's per role; the lockout (the JS has none) is the spec's.
    /// </summary>
    [System.Serializable, TuningFile("parry")]
    public class ParryTuning
    {
        /// <summary>A Guard press at most this many of the defender's frames before a hit lands parries it (0 = the frame of the hit; the edge is inclusive).</summary>
        public int windowFrames = 8;
        /// <summary>After a Guard press that opens a parry window, parrying stays locked for this many frames from the press (0.33 s). A press inside the lockout only guards. A parry doesn't clear it, so mashing can't chain parries.</summary>
        public int lockoutFrames = 20;
        /// <summary>
        /// False (the JS game): a projectile is only ever blocked. True: a parried projectile is destroyed (ADR-0004), and its
        /// owner is neither staggered nor loses posture.
        /// </summary>
        public bool parryProjectiles;

        /// <summary>Frames a parried fighter reels, by its role: a regular foe, a sub-boss, a boss, and Kage (his cut turned aside by a foe).</summary>
        public int regularStaggerFrames = 57;
        public int subStaggerFrames = 37;
        public int bossStaggerFrames = 30;
        public int playerStaggerFrames = 28;
        /// <summary>Posture a parried fighter takes, as a share of its own bar (the JS's 55, 34, 26 and 30 of 100).</summary>
        public float regularPostureShare = 0.55f;
        public float subPostureShare = 0.34f;
        public float bossPostureShare = 0.26f;
        public float playerPostureShare = 0.30f;
        /// <summary>The factor the stun-resist perk cuts the stagger by (<see cref="Fighter.ScaleStun"/>): JS <c>1 − stunRes × 0.5</c>.</summary>
        public float stunResistFactor = 0.5f;
        /// <summary>Metres per second the parried fighter is pushed back (JS 260 px/s).</summary>
        public float pushback = 2.4f;

        /// <summary>Rage the parrying fighter gains, × the Rage perk (JS +14).</summary>
        public float meterGain = 14f;
        /// <summary>Hit-stop on a parry, in frames: when Kage parries, and when a foe does (JS 0.2 s and 0.13 s).</summary>
        public int playerHitstopFrames = 12;
        public int foeHitstopFrames = 8;
        public float shake = 0.35f;

        /// <summary>The placeholder cue: a gold flash on the screen, a star, a ring and sparks at the contact.</summary>
        public Color flashTint = new(1f, 0.89f, 0.66f, 0.35f);
        public float flashSeconds = 0.18f;
        public Color sparkTint = new(1f, 0.89f, 0.66f, 1f);
        public int sparks = 18;

        // ---- group fights: turning to the blow, and the counter hit (docs/combat-turn-and-counter.md) ----
        /// <summary>A parry of a blow from behind turns the parrier round to face its attacker at once; in a group fight a blocked blow from behind does too.</summary>
        public bool turnToBlow = true;
        /// <summary>Frames a parrier keeps the fighter it parried as its target (the nearest-in-front rule waits), so the answer goes to the right foe.</summary>
        public int parryFocusFrames = 75;
        /// <summary>Frames a fighter keeps the foe it turned to (Guard + a fresh Back press, or a blocked blow from behind) as its target.</summary>
        public int turnFocusFrames = 45;
        /// <summary>The parried fighter is open to a counter hit for its stagger plus this many frames.</summary>
        public int counterGraceFrames = 12;
        /// <summary>A counter hit's damage × this.</summary>
        public float counterDamageMul = 1.5f;
        /// <summary>Extra hit-stun frames a counter hit gives, so the next blow links.</summary>
        public int counterStunBonus = 10;
        /// <summary>A counter hit's clean-hit posture × this.</summary>
        public float counterPostureMul = 2f;
        /// <summary>Extra hit-stop frames on a counter hit.</summary>
        public int counterHitstopFrames = 4;
        /// <summary>False: only Kage scores counter hits (a foe that parries him doesn't). True: every fighter does.</summary>
        public bool foesCounterHit;
        public Color counterTint = new(1f, 0.45f, 0.25f, 1f);

        // ---- the two-way parry (docs/combat-flow.md) ----
        /// <summary>
        /// The elite tier (<see cref="CombatTier.Elite"/>: a regular foe in armour or behind a shield): stagger and posture share
        /// between a regular foe's and a sub-boss's.
        /// </summary>
        public int eliteStaggerFrames = 46;
        public float elitePostureShare = 0.42f;
        /// <summary>
        /// Kage's stagger from a blow a foe parried: from this frame of it, Guard held, a dash or a dodge ends it early, so the foe's answer can be
        /// guarded, parried or dodged (0 = the stagger always runs out). Attacks still wait for the whole stagger.
        /// </summary>
        public int playerRecoverFrames = 12;
        /// <summary>After its parry, a foe holds its answer this many frames, by tier, then winds it up with a tell (a glint and a "!").</summary>
        public int regularAnswerDelay = 16, eliteAnswerDelay = 14, subAnswerDelay = 13, bossAnswerDelay = 12;
        /// <summary>A foe's first attack within this many frames of its parry is its answer and shows the tell.</summary>
        public int answerTellWindow = 45;
        /// <summary>A foe's parry: its crimson steel, and the dark red flash of the screen.</summary>
        public Color foeParryTint = new(1f, 0.32f, 0.28f, 1f);
        public Color foeParryFlashTint = new(0.75f, 0.08f, 0.1f, 0.22f);
        /// <summary>The parry's clang, and Kage's impact punch (the frame the camera jolts and the colours split).</summary>
        public float clangVolume = 0.85f;
        public float impactPunch = 0.07f;

        // ---- the contact-matched parry (ParryContactSolver, DynamicParryController) ----
        /// <summary>Frames (60 a second, on unscaled time so it runs through the hit-stop) the defender takes to bring its arms and blade onto the clash point.</summary>
        public float contactSnapFrames = 2.5f;
        /// <summary>How much of the way to the clash pose the defender is on the impact frame itself (0 to 1).</summary>
        public float contactSnapInitial = 0.45f;
        /// <summary>Live frames after the hit-stop the defender keeps its blade on the clash point before it starts to settle back.</summary>
        public int contactHoldFrames = 3;
        /// <summary>Live frames the defender takes to settle from the clash pose back into its guard.</summary>
        public int contactRecoverFrames = 8;
        /// <summary>Live frames after the hit-stop the parried attacker's blade recoils along the clash normal before its reel takes over.</summary>
        public int attackerDeflectFrames = 6;
        /// <summary>While a parry window is open and a foe is swinging close by, the arms and blade lean this much (0 to 1) towards the coming blow (no hip or chest change).</summary>
        public float anticipationWeight = 0.32f;
        /// <summary>The defender meets the blow on this vertical plane in front of it (root metres, facing right, before body scale).</summary>
        public float guardPlaneX = 0.38f;

        /// <summary>The frames a parried fighter of <paramref name="tier"/> reels.</summary>
        public int StaggerFor(CombatTier tier) => tier switch
        {
            CombatTier.Player => playerStaggerFrames, CombatTier.Elite => eliteStaggerFrames, CombatTier.Sub => subStaggerFrames,
            CombatTier.Boss => bossStaggerFrames, _ => regularStaggerFrames,
        };

        /// <summary>The share of its posture bar a parried fighter of <paramref name="tier"/> takes.</summary>
        public float PostureShareFor(CombatTier tier) => tier switch
        {
            CombatTier.Player => playerPostureShare, CombatTier.Elite => elitePostureShare, CombatTier.Sub => subPostureShare,
            CombatTier.Boss => bossPostureShare, _ => regularPostureShare,
        };

        /// <summary>How long a foe of <paramref name="tier"/> holds its answer after its own parry.</summary>
        public int AnswerDelayFor(CombatTier tier) => tier switch
        {
            CombatTier.Elite => eliteAnswerDelay, CombatTier.Sub => subAnswerDelay, CombatTier.Boss => bossAnswerDelay, _ => regularAnswerDelay,
        };
    }
}
