using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Weapon-clash numbers (Resources/Tuning/clash.json, ADR-0004 step 1, SAN-93). Times are in frames (60 a second).
    /// The JS game's clash threw both fighters back at 420 px/s (3.65 m at 115 px a metre) into 0.22 s of hitstun, with a
    /// 0.13 s freeze, a flare, gold sparks and a ring; it had no posture loss, so that number is new (the spec asks for "a little").
    /// </summary>
    [System.Serializable, TuningFile("clash")]
    public class ClashTuning
    {
        /// <summary>Posture each clashing fighter gains (the bar is 60, Raikon's 100); a full bar breaks the guard as it does anywhere else.</summary>
        public float posture = 8f;
        /// <summary>Frames each fighter recoils: helpless in hitstun, the move over (JS 0.22 s).</summary>
        public int recoilFrames = 13;
        /// <summary>Speed (m/s) each fighter is thrown back from the other, before mass and the character's knockback taken (JS 420 px/s).</summary>
        public float recoilSpeed = 3.6f;
        /// <summary>Hit-stop the whole fight freezes for (JS 0.13 s).</summary>
        public int hitstopFrames = 8;
        /// <summary>Screen shake added (GameFeel trauma, 0 to 1) and the camera punch.</summary>
        public float shake = 0.35f;
        public float punch = 0.05f;

        // ---- the placeholder cue VFX_Kage_Clash (ClashCues.cs) ----
        /// <summary>Sparks thrown out each way along the blades, and their speed range (m/s) and life (s).</summary>
        public int sparksPerSide = 12;
        public float sparkSpeedMin = 3f;
        public float sparkSpeedMax = 9f;
        public float sparkLife = 0.35f;
        /// <summary>The four-point flare's size (m) and the ring's end radius (m).</summary>
        public float flareSize = 0.6f;
        public float ringRadius = 0.55f;
        /// <summary>Spark, flare and ring colour (JS gold, #ffe2a8).</summary>
        public Color sparkTint = new(1f, 0.886f, 0.659f, 1f);
        /// <summary>The steel-on-steel sound's volume.</summary>
        public float soundVolume = 0.9f;
    }
}
