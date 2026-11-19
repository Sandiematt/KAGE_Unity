using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Crimson Moon's numbers (Resources/Tuning/crimsonMoon.json, SAN-82). They are the JS game's rage art
    /// (<c>SPECIAL_IMPL.crimsonMoon</c> in 67-rage-arts.js) at 60 frames a second, in the art's own frames: Kage's ticks, which
    /// hit-stop and whole-screen slow motion don't advance, so the timeline reads the same however long the stops last.
    /// <para>JS (seconds -> frames): freeze 0.55 s (33), the three cuts at 0.55, 0.74 and 0.93 s (33, 44, 56) for 7 each, the
    /// foe bound for 1.3 s (78) from the first, the step back and the sheathing at 1.12 s (67) with 0.2 s of slow motion at
    /// 0.4, the giant crescent at 1.38 s (83) for 15 with a knockdown, hit-stop 0.12 s (7), slow motion 0.3 for 0.45 s, the
    /// whole art 1.85 s (111). JS pixels per second are 115 to the metre (ADR-0010).</para>
    /// </summary>
    [System.Serializable, TuningFile("crimsonMoon")]
    public class CrimsonMoonTuning
    {
        // ---- the timeline: the art's frame (Kage's ticks since it started) each thing happens on ----
        /// <summary>The whole art; Kage is back in neutral after it.</summary>
        public int totalFrames = 111;
        /// <summary>The anticipation: the cut-in band slides across and the camera closes in on Kage, the world held still (JS freeze 0.55 s).</summary>
        public int anticipationFrames = 33;
        /// <summary>The frames the three crescent cuts land on (JS 0.55, 0.74 and 0.93 s).</summary>
        public int[] cutFrames = { 33, 44, 56 };
        /// <summary>Kage steps back and sheathes (JS 1.12 s).</summary>
        public int stepBackFrame = 67;
        /// <summary>The giant crescent and the finisher; the world runs again from here (JS 1.38 s).</summary>
        public int finisherFrame = 83;

        // ---- the three cuts ----
        public int cutDamage = 7;
        /// <summary>Away from Kage, up (negative: down). The JS cut's 60 px/s and 90 px/s.</summary>
        public Vector2 cutKnockback = new(0.52f, -0.78f);
        /// <summary>Hit-stun a cut gives, which is also how long the first cut binds the foe (JS 1.3 s).</summary>
        public int bindFrames = 78;
        public int cutHitstop = 3;
        public float cutShake = 0.28f;
        /// <summary>The cuts' crescent angles (radians, as the JS art's) and size.</summary>
        public float[] cutAngles = { -0.6f, 0.75f, 0.08f };
        public float cutScale = 1.05f;
        /// <summary>Kage stands this far from the foe for each cut, alternating sides (JS 92 px).</summary>
        public float standDistance = 0.8f;

        // ---- the step back ----
        /// <summary>His speed away from the foe (JS 520 px/s) and how fast it dies away (JS damp rate 10 a second).</summary>
        public float stepBackSpeed = 4.5f;
        public float stepBackDamp = 10f;
        public float stepBackSlowScale = 0.4f;
        public float stepBackSlowSeconds = 0.2f;

        // ---- the finisher ----
        public int finisherDamage = 15;
        /// <summary>The JS finisher's 640 px/s forward and 540 px/s down, a spike: the foe bounces off the floor.</summary>
        public Vector2 finisherKnockback = new(4.2f, -4.7f);
        public int finisherStun = 30;
        /// <summary>Hit-stop frames (JS 0.12 s), slow motion (JS 0.3 for 0.45 s), shake and zoom punch.</summary>
        public int finisherHitstop = 7;
        public float finisherSlowScale = 0.3f;
        public float finisherSlowSeconds = 0.45f;
        public float finisherShake = 0.9f;
        public float finisherPunch = 0.12f;
        public float finisherScale = 2.4f;
        public float finisherAngle = -0.2f;

        // ---- the screen ----
        /// <summary>The cut-in band: seconds it is on screen (anticipation plus 0.15 s, as JS).</summary>
        public float cutInSeconds = 0.7f;
        /// <summary>
        /// The camera (the rig's override): orthographic size, close in on Kage through the anticipation (JS zoom 1.42),
        /// then framing Kage and the foe (JS 1.18); how far ahead of Kage the close-in looks (JS 40 px).
        /// </summary>
        public float closeInSize = 1.7f;
        public float exchangeSize = 2.2f;
        public float closeInAhead = 0.35f;
    }
}
