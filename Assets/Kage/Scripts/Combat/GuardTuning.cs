namespace Kage
{
    /// <summary>
    /// Guard, posture and guard-break numbers (Resources/Tuning/guard.json, ADR-0004, SAN-86). Times are in frames
    /// (60 a second). The posture maximum itself is character data (<see cref="CharacterDef.postureMax"/>).
    /// </summary>
    [System.Serializable, TuningFile("guard")]
    public class GuardTuning
    {
        /// <summary>Posture a guarded hit adds: the hit's damage × this × the window's <see cref="HitWindow.postureMul"/>.</summary>
        public float blockPostureFactor = 1.5f;
        /// <summary>Posture a clean hit adds: damage × this × postureMul. Never more than the bar, and it never breaks the guard.</summary>
        public float hitPostureFactor = 0.3f;
        /// <summary>Frames without a posture gain (a guarded or clean hit) before posture starts to drain.</summary>
        public int drainDelayFrames = 60;
        /// <summary>Posture drained each frame once draining.</summary>
        public float drainPerFrame = 0.2f;
        /// <summary>When false, posture doesn't drain while Guard is held (as in the JS game): holding the guard has a cost.</summary>
        public bool drainWhileGuarding;
        /// <summary>Frames a guard-broken fighter is helpless. The next hit in them is a guaranteed critical.</summary>
        public int breakFrames = 45;
        /// <summary>Share of the guarded hit's push-back a guard-broken fighter keeps: it staggers in place, open to the next hit.</summary>
        public float breakPushKept = 0.2f;
        /// <summary>Posture left after a break, as a share of the maximum (JS: the guard refills to 60 of 100).</summary>
        public float postureAfterBreak = 0.4f;
        /// <summary>Health a guarded blade hit still takes: damage × this, rounded up. Fists and feet do no chip damage.</summary>
        public float chipFactor = 0.15f;
        /// <summary>Blockstun: the window's stun × this, at least <see cref="minBlockstun"/> frames.</summary>
        public float blockstunFactor = 0.6f;
        public int minBlockstun = 8;
    }
}
