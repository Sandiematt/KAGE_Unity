using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The rage art's cue over a boss, as the view draws it (Hud.RageCue.cs): a small gold 怒 diamond, the RAGE READY pill's colours, that
    /// lights over a sub-boss or the boss while Kage's Rage is full and the boss is open to the art (it would land now, not be turned aside:
    /// Fighter.RageArtCounter.cs). It pops in at each opening, pulses while it lasts, and fades out where it was when the opening closes, so a
    /// short opening still reads and a flickering one doesn't strobe. Unscaled time: it shows through hit-stop, when the opening often comes.
    /// </summary>
    public sealed class HudRageCueView
    {
        /// <summary>Seconds to fade in at an opening (quick: the opening may be short) and out after it.</summary>
        public const float InSeconds = 0.07f, OutSeconds = 0.18f;
        /// <summary>Seconds the onset's pop takes to settle.</summary>
        public const float PopSeconds = 0.32f;

        /// <summary>The opening is live this tick.</summary>
        public bool On { get; private set; }
        /// <summary>0..1: the cue's fade.</summary>
        public float Alpha { get; private set; }
        /// <summary>1 at an opening's first tick, settling to 0: the onset's overshoot.</summary>
        public float Pop { get; private set; }
        /// <summary>Seconds since the last opening began (the pulse).</summary>
        public float Age { get; private set; }
        /// <summary>Openings shown since the reset (each one pops).</summary>
        public int Openings { get; private set; }
        /// <summary>Where the cue sits: over the boss's head, held where it was while it fades out.</summary>
        public Vector2 World { get; private set; }

        public bool Visible => Alpha > 0.001f;

        public void Step(float dt, in HudRageCue c)
        {
            if (c.On && !On) { Openings++; Pop = 1f; Age = 0f; }
            On = c.On;
            if (c.On) World = c.World;
            Alpha = On ? Mathf.Min(1f, Alpha + dt / InSeconds) : Mathf.Max(0f, Alpha - dt / OutSeconds);
            Pop = Mathf.Max(0f, Pop - dt / PopSeconds);
            Age += dt;
        }

        public void Reset()
        {
            On = false;
            Alpha = Pop = Age = 0f;
            Openings = 0;
        }

        /// <summary>The cue's scale: big at the onset and snapping down fast (cubic on the falling pop), then a slow breath.</summary>
        public float Scale => 1f + 0.55f * Pop * Pop * Pop + 0.06f * Mathf.Sin(Age * Mathf.PI * 2f * 2.2f);

        /// <summary>0..1: the glow's beat (about two a second), bright at the onset.</summary>
        public float Beat => Mathf.Max(Pop, 0.5f + 0.5f * Mathf.Sin(Age * Mathf.PI * 2f * 2.2f));
    }
}
