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
    }
}
