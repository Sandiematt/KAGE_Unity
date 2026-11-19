using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Perfect-dodge numbers (Resources/Tuning/dodge.json, ADR-0004, SAN-92). Times are in frames (60 a second).
    /// The JS game's perfect dodge was 0.3 world speed for 0.75 s, +12 Rage and three blue afterimages; the spec asks
    /// for about 8 frames of window and about 0.4 s of slow motion, at the glossary's 0.4 world speed.
    /// </summary>
    [System.Serializable, TuningFile("dodge")]
    public class DodgeTuning
    {
        /// <summary>A hit that would land on Kage at most this many of his frames after his dash or roll started is perfect-dodged.</summary>
        public int windowFrames = 8;
        /// <summary>Extra window frames a dash perk of 1 adds (SAN-84's <see cref="Fighter.DashBonus"/>): <c>round(DashBonus × this)</c>, as the perk's invulnerability.</summary>
        public float dashBonusWindowFrames = 30f;
        /// <summary>The world time scale while the perfect dodge lasts: every fighter, projectile and hazard but Kage's.</summary>
        public float worldScale = 0.4f;
        /// <summary>How long the world stays slow, in Kage's frames (0.4 s).</summary>
        public int slowFrames = 24;
        /// <summary>Rage (<see cref="Fighter.meter"/>) gained, × the Rage perk (JS +12).</summary>
        public float meterGain = 12f;
        /// <summary>Afterimages left where Kage dodged (JS 3), how long each lasts (seconds), and their tint.</summary>
        public int afterimages = 3;
        public float afterimageLife = 0.45f;
        public Color afterimageTint = new(0.55f, 0.82f, 1f, 0.45f);
        /// <summary>Spacing between the afterimages, metres, back along the dodge.</summary>
        public float afterimageSpacing = 0.18f;
        /// <summary>The blue screen tint (JS rgba(120,200,255) at 0.6) and its fade, seconds.</summary>
        public Color screenTint = new(0.47f, 0.78f, 1f, 0.3f);
        public float screenTintSeconds = 0.4f;
    }
}
