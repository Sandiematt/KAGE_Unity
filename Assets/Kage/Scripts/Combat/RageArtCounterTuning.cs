using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The bosses' read on the rage art (Resources/Tuning/rageArtCounter.json; the rule is in Fighter.RageArtCounter.cs). A sub-boss or the
    /// boss turns Kage's rage art aside unless it is open (<see cref="Fighter.OpenToRageArt"/>): the Rage is spent, Kage reels and the boss
    /// answers. Times are in frames (60 a second).
    /// </summary>
    [System.Serializable, TuningFile("rageArtCounter")]
    public class RageArtCounterTuning
    {
        /// <summary>False: every foe takes the rage art wherever it stands (the rule before the bosses read it).</summary>
        public bool enabled = true;
        /// <summary>
        /// Frames Kage reels after his art is turned aside (a parried blow's reel is 28). Nothing ends it early (a parried blow's reel can be left
        /// from frame 12 by a guard or a dodge): the boss's answer is meant to land.
        /// </summary>
        public int staggerFrames = 48;
        /// <summary>The share of Kage's posture bar the turned-aside art costs (a parried blow costs 0.3).</summary>
        public float postureShare = 0.3f;
        /// <summary>Metres a second Kage is pushed back from the boss (a parried blow: 2.4).</summary>
        public float pushback = 3.2f;
        /// <summary>Kage's lunge stops this far from the boss (× its body scale, so a big boss doesn't swallow him): at blade's length for the clash.</summary>
        public float lungeStop = 1.1f;
        public int hitstopFrames = 12;
        public float shake = 0.45f;
        /// <summary>The screen flash as the art is turned aside: the dark crimson of a foe's parry, a little stronger.</summary>
        public Color flashTint = new(0.75f, 0.08f, 0.1f, 0.3f);
        public float flashSeconds = 0.2f;
    }
}
