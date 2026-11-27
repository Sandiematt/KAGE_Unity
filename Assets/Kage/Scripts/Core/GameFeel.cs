using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kage
{
    /// <summary>
    /// Global game-feel channels: hit-stop (freeze frames via timeScale), slow motion,
    /// trauma-based screen shake, zoom punch and full-screen flash. Driven from KageGame.Update.
    /// </summary>
    public static class GameFeel
    {
        static float _hitstopUntil, _slowUntil, _slowScale = 1f;
        public static float trauma, punch;
        public static Color flashColor;
        public static float flashAmount, flashDecay = 6f;
        public static float aberration;
        public static Vector2 kick { get; private set; }
        public static float bloomPulse, vignettePulse;
        public static Color impactTint { get; private set; }
        static float _impactUntil;
        static int _impactFrame = -10;
        public static bool ImpactVisible => Time.frameCount - _impactFrame < 3 &&
            (Time.frameCount == _impactFrame || Time.unscaledTime < _impactUntil);
        public static bool paused;
        public static float debugTimeScale = 1f;   // verification aid (slow-motion capture)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ManualClock = null;
            kick = Vector2.zero;
            bloomPulse = vignettePulse = _impactUntil = 0f;
            _impactFrame = -10;
            _hitstopUntil = _slowUntil = 0f; _slowScale = 1f; trauma = punch = flashAmount = aberration = 0f; paused = false; debugTimeScale = 1f;
        }

        /// <summary>
        /// The clock of the running headless fight (FightClock.Manual), set by its FightLoop. While set,
        /// hitstop and slow motion are counted in that clock's frames instead of wall-clock time and
        /// Time.timeScale (ADR-0002). Null in play, where everything works as before.
        /// </summary>
        public static FightClock ManualClock;

        public static void Hitstop(int frames)
        {
            if (ManualClock != null) { ManualClock.Hitstop(frames); return; }
            _hitstopUntil = Mathf.Max(_hitstopUntil, Time.unscaledTime + frames / 60f);
        }

        public static void Shake(float t) => trauma = Mathf.Min(1f, trauma + t);
        public static void Punch(float p) { punch = Mathf.Max(punch, p); aberration = Mathf.Max(aberration, p * 4f); }

        public static void Kick(Vector2 dir, float strength)
        {
            kick = Vector2.ClampMagnitude(kick + dir.normalized * strength, 0.19f);
            bloomPulse = Mathf.Max(bloomPulse, strength * 6f);
            vignettePulse = Mathf.Max(vignettePulse, strength);
        }

        public static void ImpactFrame(Color tint, float duration)
        {
            impactTint = tint;
            _impactFrame = Time.frameCount;
            _impactUntil = Time.unscaledTime + Mathf.Clamp(duration, 1f / 60f, 3f / 60f);
            bloomPulse = Mathf.Max(bloomPulse, 0.8f);
            vignettePulse = Mathf.Max(vignettePulse, 0.14f);
        }

        internal static void ApplyPost(Bloom bloom, Vignette vignette, ChromaticAberration ca, Fighter player)
        {
            bool dodge = player != null && player.InDodgeAfterglow;
            if (bloom != null) bloom.intensity.Override(1.15f + bloomPulse);
            if (ca != null) ca.intensity.Override(Mathf.Clamp01(aberration + (dodge ? 0.16f : 0f)));
            if (vignette == null) return;
            vignette.color.Override(dodge ? new Color(0.055f, 0.13f, 0.29f) : Color.black);
            vignette.intensity.Override(0.34f + vignettePulse + (dodge ? 0.1f : 0f) +
                (player != null && player.state == FState.Ultimate ? 0.22f : 0f));
        }

        public static void SlowMo(float scale, float duration)
        {
            if (ManualClock != null) { ManualClock.SlowMo(scale, Mathf.RoundToInt(duration * 60f)); return; }
            _slowScale = scale; _slowUntil = Time.unscaledTime + duration;
        }
        public static void Flash(Color c, float duration) { flashColor = c; flashAmount = c.a; flashDecay = c.a / Mathf.Max(0.02f, duration); }
        public static bool InHitstop => ManualClock != null ? ManualClock.InHitstop : Time.unscaledTime < _hitstopUntil;

        public static void Tick()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float ts = 1f;
            if (now < _slowUntil) ts = _slowScale;
            if (now < _hitstopUntil) ts = 0f;
            if (paused) ts = 0f;
            Time.timeScale = ts * debugTimeScale;
            kick *= Mathf.Exp(-23f * dt);
            bloomPulse = Mathf.MoveTowards(bloomPulse, 0f, 5f * dt);
            vignettePulse = Mathf.MoveTowards(vignettePulse, 0f, dt);
            trauma = Mathf.Max(0f, trauma - dt * 1.5f);
            punch = Mathf.Max(0f, punch - dt * 0.9f);
            aberration = Mathf.Max(0f, aberration - dt * 2.5f);
            flashAmount = Mathf.Max(0f, flashAmount - dt * flashDecay);
        }
    }
}
