using UnityEngine;

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
        public static bool paused;
        public static float debugTimeScale = 1f;   // verification aid (slow-motion capture)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _hitstopUntil = _slowUntil = 0f; _slowScale = 1f; trauma = punch = flashAmount = aberration = 0f; paused = false; debugTimeScale = 1f;
        }

        public static void Hitstop(int frames) => _hitstopUntil = Mathf.Max(_hitstopUntil, Time.unscaledTime + frames / 60f);
        public static void Shake(float t) => trauma = Mathf.Min(1f, trauma + t);
        public static void Punch(float p) { punch = Mathf.Max(punch, p); aberration = Mathf.Max(aberration, p * 4f); }
        public static void SlowMo(float scale, float duration) { _slowScale = scale; _slowUntil = Time.unscaledTime + duration; }
        public static void Flash(Color c, float duration) { flashColor = c; flashAmount = c.a; flashDecay = c.a / Mathf.Max(0.02f, duration); }
        public static bool InHitstop => Time.unscaledTime < _hitstopUntil;

        public static void Tick()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float ts = 1f;
            if (now < _slowUntil) ts = _slowScale;
            if (now < _hitstopUntil) ts = 0f;
            if (paused) ts = 0f;
            Time.timeScale = ts * debugTimeScale;
            trauma = Mathf.Max(0f, trauma - dt * 1.5f);
            punch = Mathf.Max(0f, punch - dt * 0.9f);
            aberration = Mathf.Max(0f, aberration - dt * 2.5f);
            flashAmount = Mathf.Max(0f, flashAmount - dt * flashDecay);
        }
    }
}
