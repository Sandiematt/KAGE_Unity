using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Combat flow numbers (Resources/Tuning/flow.json, docs/combat-flow.md): how fighters face, how a launched body turns,
    /// which recoveries a contact cancels, the whiff punish and guard pressure. Times are frames (60 a second), distances metres.
    /// </summary>
    [System.Serializable, TuningFile("flow")]
    public class CombatFlowTuning
    {
        // ---- facing ----
        /// <summary>In neutral a fighter turns to its target only when the target stands more than this far past its centre (no flip-flop over a foe directly above).</summary>
        public float turnDeadZone = 0.12f;
        /// <summary>After a turn, a fighter doesn't turn back in neutral for this many frames unless the target is <see cref="turnForceDistance"/> or more behind it.</summary>
        public int turnHoldFrames = 8;
        public float turnForceDistance = 0.6f;
        /// <summary>A ground attack started from neutral or a chain faces its target first, when the target stands behind (a flip between animations).</summary>
        public bool turnOnMoveStart = true;
        /// <summary>A fighter that reels (hitstun) or blocks from a blow from behind turns to its attacker as the reel or block starts. A shield bearer never turns (its back is its weak side).</summary>
        public bool turnToBlowFromBehind = true;

        // ---- launches ----
        /// <summary>A launched body turns towards lying (90° onto its back, or onto its front) and stops there: no somersaults in the air.</summary>
        public float tumbleRestAngle = 90f;
        /// <summary>The fastest the launched body turns, degrees a second.</summary>
        public float tumbleMaxSpeed = 300f;

        // ---- cancels ----
        /// <summary>Once a move's last blow is over and one of its blows landed or was guarded, a dash press cancels the rest of the recovery.</summary>
        public bool dashCancelOnContact = true;
        /// <summary>Once a move's last blow is over and one of its blows landed, Guard cancels the rest of the recovery into the guard. A guarded or whiffed move recovers in full.</summary>
        public bool guardCancelOnHit = true;

        // ---- the whiff punish ----
        /// <summary>A clean melee blow on a fighter recovering from a move that touched nothing is a punish: damage × this, more hit-stun and hit-stop.</summary>
        public float whiffPunishDamageMul = 1.25f;
        public int whiffPunishStunBonus = 6;
        public int whiffPunishHitstopFrames = 2;
        /// <summary>False: only Kage scores punishes (a foe hitting his whiffed recovery deals plain damage). True: every fighter does.</summary>
        public bool foesPunishWhiffs;
        public Color punishTint = new(0.55f, 0.85f, 1f, 1f);

        // ---- guard pressure ----
        /// <summary>Guarded blows from the same attacker less than this many frames apart are one string of pressure.</summary>
        public int pressureWindowFrames = 40;
        /// <summary>Each guarded blow of a string after the first adds this share more posture, up to <see cref="pressureMaxMul"/>.</summary>
        public float pressureStep = 0.15f;
        public float pressureMaxMul = 1.6f;
    }
}
