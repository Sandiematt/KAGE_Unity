using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary><c>Clang</c> is Kage's parry (a bright ringing steel), <c>FoeParry</c> a foe's parry of his blow (lower, harsher, a warning).</summary>
    public enum SfxKind { Whoosh, SwordWhoosh, Hit, HitHeavy, Slash, Block, Thud, Land, Grab, KO, Counter, Crescent, Ultimate, Gong, Clang, FoeParry, PerfectChime, Shatter, TimeSlice, ClashRing, PunishConfirm }

    /// <summary>
    /// Synthesised sound effects (no audio files): filtered noise for whooshes and cloth,
    /// noise + sine thumps for impacts, inharmonic partials for steel, a low swell for the
    /// ultimate, plus a looping wind-and-taiko ambience.
    /// </summary>
    public class KageAudio : MonoBehaviour
    {
        const int Rate = 44100;
        readonly Dictionary<SfxKind, AudioClip[]> _clips = new();
        readonly List<AudioSource> _pool = new();
        int _next;
        AudioSource _ambience;

        void Awake()
        {
            for (int i = 0; i < 12; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _pool.Add(s);
            }
            // Waveform noise has its own reproducible start without advancing the scene's cosmetic random sequence.
            var previous = Random.state;
            try
            {
                Random.InitState(7);
                BuildClips();
            }
            finally { Random.state = previous; }
        }

        void BuildClips()
        {
            _clips[SfxKind.Whoosh] = Variants(3, v => Whoosh(0.2f, 900f + v * 250f, 0.35f));
            _clips[SfxKind.SwordWhoosh] = Variants(3, v => Whoosh(0.18f, 2600f + v * 400f, 0.4f, true));
            _clips[SfxKind.Hit] = Variants(3, v => Impact(0.16f, 110f + v * 15f, 0.55f, 2200f));
            _clips[SfxKind.HitHeavy] = Variants(2, v => Impact(0.32f, 62f + v * 8f, 0.85f, 1400f));
            _clips[SfxKind.Slash] = Variants(3, v => Slash(0.35f, v));
            _clips[SfxKind.Block] = Variants(2, v => Steel(0.22f, 2100f + v * 300f, 0.45f));
            _clips[SfxKind.Thud] = Variants(2, v => Impact(0.28f, 55f + v * 6f, 0.7f, 700f));
            _clips[SfxKind.Land] = Variants(2, v => Impact(0.12f, 80f, 0.3f, 900f));
            _clips[SfxKind.Grab] = Variants(1, v => Whoosh(0.15f, 600f, 0.4f));
            _clips[SfxKind.KO] = Variants(1, v => Gong(1.6f, 48f));
            _clips[SfxKind.Counter] = Variants(1, v => Chime(0.7f));
            _clips[SfxKind.Crescent] = Variants(1, v => Rise(0.55f, 400f, 1800f));
            _clips[SfxKind.Ultimate] = Variants(1, v => Rise(1.2f, 60f, 420f, 0.9f));
            _clips[SfxKind.Gong] = Variants(1, v => Gong(2.2f, 72f));
            _clips[SfxKind.Clang] = Variants(2, v => Clang(0.55f, 1850f + v * 160f, 0.6f, 9f));
            _clips[SfxKind.FoeParry] = Variants(2, v => Clang(0.4f, 980f + v * 90f, 0.62f, 14f));

            _clips[SfxKind.PerfectChime] = Variants(1, v => Chime(0.85f, 990f, true));
            _clips[SfxKind.Shatter] = Variants(2, v => Shatter(v));
            _clips[SfxKind.TimeSlice] = Variants(1, v => Whoosh(0.14f, 4100f, 0.55f, true));
            _clips[SfxKind.ClashRing] = Variants(2, v => Clang(0.66f, 1240f + v * 80f, 0.72f, 7.5f));
            _clips[SfxKind.PunishConfirm] = Variants(1, v => Steel(0.13f, 2780f, 0.43f));

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = Ambience(8f);
            _ambience.loop = true;
            _ambience.volume = 0.32f;
            _ambience.Play();
        }

        void OnDestroy()
        {
            foreach (var variants in _clips.Values)
                foreach (var clip in variants) Destroy(clip);
            if (_ambience != null && _ambience.clip != null) Destroy(_ambience.clip);
        }

        AudioClip[] Variants(int n, System.Func<int, AudioClip> make)
        {
            var a = new AudioClip[n];
            for (int i = 0; i < n; i++) a[i] = make(i);
            return a;
        }

        public void Play(SfxKind k, float vol)
        {
            if (!_clips.TryGetValue(k, out var arr)) return;
            var s = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            s.pitch = Random.Range(0.93f, 1.07f);
            s.PlayOneShot(arr[Random.Range(0, arr.Length)], vol);
        }

        static float Noise() => Random.value * 2f - 1f;

        static AudioClip Make(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        AudioClip Whoosh(float dur, float cutoff, float vol, bool bright = false)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI) * (1f - t * 0.3f);
                float fc = cutoff * (0.4f + 1.4f * Mathf.Sin(t * Mathf.PI));
                float a = Mathf.Clamp01(2f * Mathf.PI * fc / Rate);
                lp += a * (Noise() - lp);
                lp2 += a * (lp - lp2);
                float s = bright ? (lp - lp2 * 0.6f) * 2.2f : lp2 * 2.5f;
                d[i] = s * env * vol;
            }
            return Make("whoosh", d);
        }

        AudioClip Impact(float dur, float freq, float vol, float noiseCut)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, ph = 0f;
            float a = Mathf.Clamp01(2f * Mathf.PI * noiseCut / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = freq * (1f + 1.5f * Mathf.Exp(-t * 40f));
                ph += 2f * Mathf.PI * f / Rate;
                lp += a * (Noise() - lp);
                float body = Mathf.Sin(ph) * Mathf.Exp(-t * 14f);
                float crack = lp * Mathf.Exp(-t * 45f) * 1.6f;
                float s = body + crack;
                d[i] = (float)System.Math.Tanh(s * 1.6f) * vol;
            }
            return Make("impact", d);
        }

        AudioClip Slash(float dur, int v)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float hp = 0f, prev = 0f;
            float[] partials = { 1830f + v * 70f, 2770f, 4120f + v * 90f, 5600f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = Noise();
                hp = 0.92f * (hp + x - prev);
                prev = x;
                float swish = hp * Mathf.Exp(-t * 18f) * 0.7f;
                float ring = 0f;
                for (int k = 0; k < partials.Length; k++) ring += Mathf.Sin(2f * Mathf.PI * partials[k] * t) * Mathf.Exp(-t * (9f + k * 4f)) / (k + 1);
                d[i] = (swish + ring * 0.35f) * 0.6f;
            }
            return Make("slash", d);
        }

        AudioClip Steel(float dur, float f0, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sin(2f * Mathf.PI * f0 * t) * Mathf.Exp(-t * 22f)
                        + 0.6f * Mathf.Sin(2f * Mathf.PI * f0 * 1.51f * t) * Mathf.Exp(-t * 30f)
                        + 0.3f * Noise() * Mathf.Exp(-t * 80f);
                d[i] = s * vol;
            }
            return Make("steel", d);
        }

        /// <summary>
        /// Blade on blade: a hard noise transient, then inharmonic steel partials (the free-bar ratios 1, 2.76, 5.40, 8.93) that ring on,
        /// the higher ones dying first, with a slow beat between two near partials. <paramref name="decay"/> is the fundamental's decay rate.
        /// </summary>
        AudioClip Clang(float dur, float f0, float vol, float decay)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sin(2f * Mathf.PI * f0 * t) * Mathf.Exp(-t * decay)
                        + 0.55f * Mathf.Sin(2f * Mathf.PI * f0 * 1.007f * t) * Mathf.Exp(-t * decay * 1.1f)
                        + 0.5f * Mathf.Sin(2f * Mathf.PI * f0 * 2.76f * t) * Mathf.Exp(-t * decay * 1.8f)
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * f0 * 5.40f * t) * Mathf.Exp(-t * decay * 3f)
                        + 0.18f * Mathf.Sin(2f * Mathf.PI * f0 * 8.93f * t) * Mathf.Exp(-t * decay * 5f)
                        + 0.7f * Noise() * Mathf.Exp(-t * 140f);
                d[i] = s * vol * 0.4f;
            }
            return Make("clang", d);
        }

        AudioClip Gong(float dur, float f0)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += 0.02f * (Noise() - lp);
                float s = Mathf.Sin(2f * Mathf.PI * f0 * t) * Mathf.Exp(-t * 2.2f)
                        + 0.5f * Mathf.Sin(2f * Mathf.PI * f0 * 2.76f * t) * Mathf.Exp(-t * 3.5f)
                        + 0.25f * Mathf.Sin(2f * Mathf.PI * f0 * 5.4f * t) * Mathf.Exp(-t * 5f)
                        + lp * 3f * Mathf.Exp(-t * 6f);
                d[i] = (float)System.Math.Tanh(s * 1.3f) * 0.8f;
            }
            return Make("gong", d);
        }

        AudioClip Shatter(int variant)
        {
            var d = new float[Rate / 4];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                // Three splinters crack in succession, each a little quieter than the first.
                float second = Mathf.Max(0f, t - 0.018f);
                float third = Mathf.Max(0f, t - 0.044f);
                float env = Mathf.Exp(-95f * t);
                if (second > 0f) env += 0.63f * Mathf.Exp(-120f * second);
                if (third > 0f) env += 0.42f * Mathf.Exp(-145f * third);
                d[i] = (0.7f * Noise() + 0.27f * Mathf.Sin((7800f + variant * 520f) * t)) * env;
            }
            return Make("posture crack", d);
        }

        AudioClip Chime(float dur, float root = 1320f, bool overtone = false)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sin(2f * Mathf.PI * root * t) * Mathf.Exp(-t * 6f)
                        + 0.6f * Mathf.Sin(2f * Mathf.PI * (root * 1.5f) * t) * Mathf.Exp(-t * 8f)
                        + 0.4f * Mathf.Sin(2f * Mathf.PI * (root * 2f) * t) * Mathf.Exp(-t * 10f);
                if (overtone) s += 0.35f * Mathf.Sin(2f * Mathf.PI * root * 2.5f * t) * Mathf.Exp(-t * 5f);
                d[i] = s * 0.35f;
            }
            return Make("chime", d);
        }

        AudioClip Rise(float dur, float f0, float f1, float vol = 0.6f)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float ph = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t * t);
                ph += 2f * Mathf.PI * f / Rate;
                lp += 0.08f * (Noise() - lp);
                float env = Mathf.Sin(t * Mathf.PI) * (t < 0.85f ? 1f : (1f - t) / 0.15f);
                d[i] = (Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 2.01f) * 0.2f + lp * 1.5f) * env * vol;
            }
            return Make("rise", d);
        }

        AudioClip Ambience(float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            float[] hits = { 0f, 0.5f, 4f, 4.5f, 5.25f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float gust = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / dur * 2f) * Mathf.Sin(2f * Mathf.PI * t / dur * 3f + 1f);
                float a = 0.006f + 0.01f * gust;
                lp += a * (Noise() - lp);
                lp2 += 0.02f * (lp - lp2);
                float wind = (lp - lp2) * 6f * gust;
                // slow taiko pulse: two strikes every 4 s
                float beat = 0f;
                foreach (var h in hits)
                {
                    float u = t - h;
                    if (u >= 0f && u < 0.6f) beat += Mathf.Sin(2f * Mathf.PI * (58f + 40f * Mathf.Exp(-u * 30f)) * u) * Mathf.Exp(-u * 7f) * (h % 4f == 0f ? 0.55f : 0.35f);
                }
                // loop-safe fade at the seam
                float seam = Mathf.Clamp01(Mathf.Min(t, dur - t) / 0.25f);
                d[i] = (wind * seam + beat) * 0.6f;
            }
            return Make("ambience", d);
        }
    }
}
