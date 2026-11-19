namespace Kage
{
    /// <summary>
    /// Group fights (Resources/Tuning/engagement.json, SAN-83): how many foes may attack at once, where the others
    /// wait and how they circle, and the "nearest in front" target rule. Distances are metres, times frames (60 a second).
    /// The JS numbers (STRETCH.maxEngaged, waitRing, AIController.waitTurn) are converted at 115 px a metre (ADR-0010).
    /// </summary>
    [System.Serializable, TuningFile("engagement")]
    public class EngagementTuning
    {
        /// <summary>Foes engaged (allowed to attack) at once. JS STRETCH.maxEngaged.</summary>
        public int maxAttackers = 2;
        /// <summary>
        /// An engaged foe that is not committed to an attack gives its place to a waiting foe this much nearer to their target.
        /// Large, so places don't change hands as foes shuffle; a foe knocked far away is replaced by one standing close.
        /// </summary>
        public float swapMargin = 1.5f;

        /// <summary>How far from the target a waiting foe stays. JS waitRing 360 px.</summary>
        public float waitDistance = 3.1f;
        /// <summary>A waiting foe nearer than waitDistance minus this backs away. JS ring - 70 px.</summary>
        public float retreatInside = 0.6f;
        /// <summary>A waiting foe farther than waitDistance plus this walks in. JS ring + 90 px.</summary>
        public float approachBeyond = 0.8f;
        /// <summary>Frames a waiting foe walks in or backs away before it looks again.</summary>
        public int stepFrames = 8;
        /// <summary>Chance that a waiting foe at its distance circles (a walking step to one side) rather than holds. JS 0.4.</summary>
        public float circleChance = 0.4f;
        /// <summary>Length of one circling step, in frames (JS 0.2 to 0.4 s). Its walking speed is the foe's own.</summary>
        public int circleStepMin = 12, circleStepMax = 24;
        /// <summary>Length of a hold between circling steps, in frames (JS 0.22 to 0.5 s).</summary>
        public int holdMin = 13, holdMax = 30;

        /// <summary>
        /// The target rule: a fighter targets the opposing fighter with the lowest distance, where a fighter behind it counts
        /// this much farther. So it keeps to the nearest foe in front and turns only for a foe behind that is this much nearer
        /// (or when nobody is in front).
        /// </summary>
        public float behindPenalty = 1.5f;
        /// <summary>The current target counts this much nearer, so two foes at about the same distance don't swap every frame.</summary>
        public float stickiness = 0.25f;
    }
}
