using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Lightning, built on the effekseer-vfx skill's element signature for lightning (craft §3): a <b>jagged polyline that does not travel</b>,
    /// <b>re-struck</b> on a new path every few frames, with a <b>main strike, a return stroke and an after-flicker</b> on different frames,
    /// <b>ground cracks</b> and <b>crawling arcs</b> as its secondary motion. Every stroke is value-layered: a white-hot core inside a saturated
    /// crimson body inside a dark alpha-blended rim (so it reads on bright stages too), and a hit leaves a scorch that cools and rising ash.
    /// The brief and the palette are in docs/raikon-ascendant-vfx.md.
    /// </summary>
    public partial class Vfx
    {
        // ---- the palette roles of the Lightning Ascendant ----
        public static readonly Color Crimson = new(1f, 0.165f, 0.212f, 1f);        // body #FF2A36
        public static readonly Color HotWhite = new(1f, 0.9f, 0.9f, 1f);           // core #FFE6E6
        public static readonly Color BloodBlack = new(0.165f, 0.012f, 0.024f, 1f); // dark #2A0306
        public static readonly Color Ash = new(0.43f, 0.31f, 0.31f, 1f);          // matter
        public static readonly Color Steel = new(0.847f, 0.894f, 0.941f, 1f);      // accent #D8E4F0: blockable tells only
        public static readonly Color EyeRed = new(1f, 0.063f, 0.063f, 1f);         // accent #FF1010: eye tells and the eye trail only

        /// <summary>Seconds between re-strikes: a new jag every 3 frames.</summary>
        public const float RestrikeSeconds = 0.05f;

        /// <summary>
        /// Brightness of a stroke over its life (0..1): the main strike (full for the first 8 %), a dip, the return stroke at 38 %, an after-flicker at
        /// 68 %, and a low tail. Three read points on three different frames, never one long white hold.
        /// </summary>
        public static float StrokeEnvelope(float u)
        {
            float main = u < 0.08f ? 1f : u < 0.3f ? Mathf.Lerp(1f, 0.2f, (u - 0.08f) / 0.22f) : 0f;
            float ret = Bump(u, 0.38f, 0.07f) * 0.85f;
            float flick = Bump(u, 0.68f, 0.035f) * 0.55f;
            float tail = (1f - u) * 0.18f;
            return Mathf.Max(Mathf.Max(main, ret), Mathf.Max(flick, tail));
        }

        static float Bump(float u, float c, float w) => Mathf.Max(0f, 1f - Mathf.Abs(u - c) / w);

        /// <summary>A jagged path from a to b with forks; <see cref="Strike"/> re-draws it on a new path for each strike index (deterministic per seed).</summary>
        sealed class Jag
        {
            public readonly List<Vector2> main = new();
            public readonly List<List<Vector2>> forks = new();
            readonly Vector2 _a, _b;
            readonly int _segs, _forks, _seed;
            readonly float _jit;
            int _strike = -1;

            public Jag(Vector2 a, Vector2 b, int segs, float jitter, int forks, int seed)
            {
                _a = a; _b = b; _segs = Mathf.Max(2, segs); _jit = jitter; _forks = forks; _seed = seed;
            }

            public void Strike(int s)
            {
                if (s == _strike) return;
                _strike = s;
                var r = new System.Random(_seed * 7919 + s * 104729);
                float F() => (float)r.NextDouble();
                main.Clear();
                Vector2 d = _b - _a;
                Vector2 n = d.sqrMagnitude > 1e-8f ? new Vector2(-d.y, d.x).normalized : Vector2.right;
                for (int i = 0; i <= _segs; i++)
                {
                    float t = i / (float)_segs;
                    float pin = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.6f);   // the ends stay on a and b (Sin(PI) dips below 0 in float: no NaN end)
                    float along = (i > 0 && i < _segs) ? (F() - 0.5f) * 0.6f / _segs : 0f;
                    main.Add(_a + d * (t + along) + n * ((F() - 0.5f) * 2f * _jit * pin));
                }
                forks.Clear();
                for (int k = 0; k < _forks; k++)
                {
                    int at = Mathf.Clamp(Mathf.RoundToInt(_segs * (0.25f + F() * 0.45f)), 1, _segs - 1);
                    Vector2 from = main[at];
                    float ang = KMath.Ang(d) + (F() < 0.5f ? -1f : 1f) * (25f + F() * 25f);
                    float len = d.magnitude * (0.14f + F() * 0.16f);
                    var f = new List<Vector2> { from };
                    Vector2 fd = KMath.Dir(ang), fn = new(-fd.y, fd.x);
                    for (int i = 1; i <= 4; i++) f.Add(from + fd * (len * i / 4f) + fn * ((F() - 0.5f) * _jit));
                    forks.Add(f);
                }
            }
        }

        /// <summary>A polyline as one ribbon; soft = a bright centre fading to alpha 0 at both edges (two half strips).</summary>
        static void PolyStrip(Ribbon rib, List<Vector2> p, float w0, float w1, Color c, bool soft)
        {
            int n = p.Count;
            if (n < 2) return;
            for (int half = 0; half < (soft ? 2 : 1); half++)
            {
                rib.Break();
                for (int i = 0; i < n; i++)
                {
                    Vector2 t = (p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)]);
                    t = t.sqrMagnitude > 1e-10f ? t.normalized : Vector2.right;
                    Vector2 nrm = new(-t.y, t.x);
                    float w = Mathf.Lerp(w0, w1, i / (n - 1f));
                    Color e = c.WithA(0f);
                    if (!soft) rib.Add(p[i] + nrm * w, p[i] - nrm * w, c, c);
                    else if (half == 0) rib.Add(p[i] + nrm * w, p[i], e, c);
                    else rib.Add(p[i], p[i] - nrm * w, c, e);
                }
            }
        }

        /// <summary>
        /// A lightning stroke from <paramref name="a"/> to <paramref name="b"/>: dark rim, crimson body and white core on one jagged path that is re-struck
        /// every 3 frames, with forks, under <see cref="StrokeEnvelope"/>. <paramref name="width"/> is the body's half-width at <paramref name="b"/> (the
        /// struck end is the thick one); <paramref name="jitter"/> &lt; 0 = 7 % of the length. <paramref name="bright"/> scales every layer (a distant
        /// bolt in the backdrop is dimmer) and <paramref name="core"/> the white core alone (0 = none: the persistent aura's arcs, never a tell's white).
        /// </summary>
        public void Bolt(Vector2 a, Vector2 b, float width, float life, Color body, int segments = 14, float jitter = -1f, int forks = 2, bool unscaled = false, int order = Order + 55,
            float bright = 1f, float core = 1f) => Bolt(a, b, width, life, body, BloodBlack, 2.6f, 0.55f, segments, jitter, forks, unscaled, order, bright, core);

        /// <summary>
        /// <see cref="Bolt(Vector2, Vector2, float, float, Color, int, float, int, bool, int, float, float)"/> with its own dark rim: <paramref name="rim"/>
        /// colour, <paramref name="rimWidth"/> × the body's half-width, at <paramref name="rimAlpha"/>. Each sub-boss's lightning is set off by its own dark
        /// (Garan's storm navy, Shiden's purple-black) instead of Raikon's blood black.
        /// </summary>
        public void Bolt(Vector2 a, Vector2 b, float width, float life, Color body, Color rim, float rimWidth, float rimAlpha, int segments = 14, float jitter = -1f,
            int forks = 2, bool unscaled = false, int order = Order + 55, float bright = 1f, float core = 1f)
        {
            float len = (b - a).magnitude;
            var jag = new Jag(a, b, segments, jitter < 0f ? len * 0.07f : jitter, forks, Random.Range(1, 1 << 20));
            void Build(Ribbon rib, float u, float wMul, Color c, float alpha, bool soft)
            {
                jag.Strike(Mathf.FloorToInt(u * life / RestrikeSeconds));
                float e = StrokeEnvelope(u) * alpha;
                if (e <= 0.002f) return;
                var cc = c.WithA(c.a * e);
                PolyStrip(rib, jag.main, width * wMul * 0.35f, width * wMul, cc, soft);
                foreach (var f in jag.forks) PolyStrip(rib, f, width * wMul * 0.5f, width * wMul * 0.1f, cc.WithA(cc.a * 0.8f), soft);
            }
            Dyn("BoltRim", MeshKit.Alpha, order - 6, life, unscaled, (rib, u) => Build(rib, u, rimWidth, rim, rimAlpha * bright, true));
            Dyn("BoltBody", AddHDR(2.4f), order, life, unscaled, (rib, u) => Build(rib, u, 1.4f, body, bright, true));
            if (core > 0f) Dyn("BoltCore", AddHDR(3.2f), order + 1, life, unscaled, (rib, u) => Build(rib, u, 0.4f, Color.white, bright * core, false));
        }

        /// <summary>A short crawling arc between two points (blade static, a body aura): a thin stroke with no forks.</summary>
        public void Arc(Vector2 a, Vector2 b, float life = 0.12f, float width = 0.016f, Color? body = null)
        {
            Bolt(a, b, width, life, body ?? Crimson, Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude / 0.07f), 4, 9), (b - a).magnitude * 0.18f, 0, false, Order + 48);
        }

        /// <summary><paramref name="n"/> crawling arcs on an ellipse round <paramref name="c"/> (rx, ry): chords between random points, each its own short life.</summary>
        public void Crackle(Vector2 c, float rx, float ry, int n = 3, Color? body = null)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = Random.Range(0f, 360f), a1 = a0 + Random.Range(35f, 90f) * (Random.value < 0.5f ? 1f : -1f);
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad) * rx, Mathf.Sin(a0 * Mathf.Deg2Rad) * ry);
                Vector2 p1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad) * rx, Mathf.Sin(a1 * Mathf.Deg2Rad) * ry);
                Arc(p0, p1, Random.Range(0.08f, 0.14f), Random.Range(0.012f, 0.02f), body);
            }
        }

        /// <summary>A scorch where lightning hit the floor: a dark ellipse that cools slowly, a crimson glow ring that dies first, rising ash.</summary>
        public void Scorch(Vector2 p, float halfWidth, float life = 0.9f)
        {
            Dyn("Scorch", MeshKit.Alpha, Order - 30, life, false, (rib, u) =>
            {
                float a = 0.6f * Mathf.Pow(1f - u, 0.7f);
                const int seg = 20;
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg * Mathf.PI;
                    float x = Mathf.Cos(t) * halfWidth, y = Mathf.Sin(t) * halfWidth * 0.12f;
                    rib.Add(p + new Vector2(x, y + 0.01f), p + new Vector2(x, -y + 0.01f), BloodBlack.WithA(a), BloodBlack.WithA(a));
                }
            });
            Ring(p + Vector2.up * 0.02f, halfWidth * 0.2f, halfWidth * 0.9f, 0.05f, Crimson, life * 0.45f, false, 0.16f, 1.6f, Order - 25);
            for (int i = 0; i < 4; i++)
                Emit(_dust, p + new Vector2(Random.Range(-halfWidth, halfWidth) * 0.6f, 0.05f), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.3f, 0.7f)),
                    Random.Range(0.14f, 0.24f), Random.Range(0.6f, 0.9f), Ash.WithA(0.5f));
        }

        /// <summary>
        /// A bolt from the sky onto <paramref name="ground"/>: the stroke (main, return, after-flicker), a short contact star, a ground ellipse that
        /// opens, sparks thrown up and out, a scorch, and a faint crimson environment flash.
        /// </summary>
        public void SkyBolt(Vector2 ground, float height = 7f, float scale = 1f)
        {
            Vector2 top = ground + new Vector2(Random.Range(-0.45f, 0.45f), height);
            Bolt(top, ground, 0.07f * scale, 0.42f, Crimson, 18, height * 0.045f, 3);
            Star(ground + Vector2.up * 0.25f, 0.85f * scale, Color.white, Crimson, 0.09f, 8, true, Order + 62);
            Ring(ground + Vector2.up * 0.03f, 0.1f, 1.3f * scale, 0.09f, Crimson, 0.34f, false, 0.2f, 2.2f);
            Ring(ground + Vector2.up * 0.03f, 0.05f, 0.7f * scale, 0.05f, Color.white, 0.2f, false, 0.2f, 1.6f);
            for (int i = 0; i < 14; i++)
            {
                float ang = Random.Range(25f, 155f);
                Emit(_sparks, ground + Vector2.up * 0.05f, KMath.Dir(ang) * Random.Range(2.5f, 7.5f) * scale, Random.Range(0.025f, 0.05f), Random.Range(0.3f, 0.6f),
                    i % 3 == 0 ? Color.white : Crimson);
            }
            Scorch(ground, 0.75f * scale);
            GameFeel.Flash(new Color(1f, 0.55f, 0.55f, 0.14f), 0.12f);
        }

        /// <summary>
        /// The telegraph of a floor spark line from <paramref name="a"/> to <paramref name="b"/> (on the floor): a dim crimson crack crawling along it,
        /// re-struck every 4 frames, its flicker rising towards the strike, with a spark popping off it now and then. It must never look like the strike.
        /// </summary>
        public void SparkLineMark(Vector2 a, Vector2 b, float seconds)
        {
            if (seconds <= 0f) return;
            var jag = new Jag(a + Vector2.up * 0.02f, b + Vector2.up * 0.02f, Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude / 0.18f), 6, 40), 0.035f, 0, Random.Range(1, 1 << 20));
            float sparkAt = 0f;
            Dyn("SparkLineMark", AddHDR(1.6f), Order - 10, seconds, false, (rib, u) =>
            {
                jag.Strike(Mathf.FloorToInt(u * seconds / 0.067f));
                float flick = 0.22f + 0.38f * u + Random.Range(-0.08f, 0.08f);
                PolyStrip(rib, jag.main, 0.018f, 0.018f, Crimson.WithA(flick), true);
                if (u * seconds >= sparkAt)
                {
                    sparkAt += Random.Range(0.06f, 0.12f);
                    Vector2 p = Vector2.Lerp(a, b, Random.value);
                    Emit(_sparks, p + Vector2.up * 0.03f, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 2.4f)), 0.022f, 0.22f, Crimson);
                }
            });
        }

        /// <summary>The strike of a floor spark line: a row of short bolts up from the floor, staggered over 4 frames, sparks, a ring at each end and a long scorch.</summary>
        public void SparkLineStrike(Vector2 a, Vector2 b)
        {
            float len = (b - a).magnitude;
            int k = Mathf.Max(2, Mathf.CeilToInt(len / 0.45f));
            for (int i = 0; i <= k; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)k);
                float h = Random.Range(0.45f, 1.1f);
                Bolt(p + new Vector2(Random.Range(-0.12f, 0.12f), h), p, 0.035f, Random.Range(0.22f, 0.3f), Crimson, 7, 0.06f, i % 3 == 0 ? 1 : 0);
                Emit(_sparks, p + Vector2.up * 0.05f, new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2f, 4.5f)), 0.03f, Random.Range(0.25f, 0.45f), i % 2 == 0 ? Color.white : Crimson);
            }
            Ring(a + Vector2.up * 0.03f, 0.05f, 0.6f, 0.05f, Crimson, 0.28f, false, 0.22f, 2f);
            Ring(b + Vector2.up * 0.03f, 0.05f, 0.6f, 0.05f, Crimson, 0.28f, false, 0.22f, 2f);
            Scorch((a + b) * 0.5f, len * 0.5f + 0.2f, 0.8f);
            GameFeel.Shake(0.22f);
        }

        /// <summary>
        /// Three crimson spiral arms sweeping out from <paramref name="c"/> and eroding (head out fast, tail catching it), with a white core in each arm,
        /// a contact star, a ring and sparks flung along the arms. Not a symmetric starburst: the arms have different reaches.
        /// </summary>
        public void SpiralBurst(Vector2 c, float radius = 2.4f, float life = 0.55f)
        {
            float spin = Random.Range(0f, 360f);
            var reach = new[] { Random.Range(0.85f, 1f), Random.Range(0.7f, 0.95f), Random.Range(0.75f, 1f) };
            void Arms(Ribbon rib, float u, float wMul, Color col)
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.4f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.12f) / 0.88f));
                for (int k = 0; k < 3; k++)
                {
                    rib.Break();
                    const int seg = 26;
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = Mathf.Lerp(tail, head, i / (float)seg);
                        float th = (spin + k * 120f + s * 260f) * Mathf.Deg2Rad;
                        float r = radius * reach[k] * s;
                        Vector2 p = c + new Vector2(Mathf.Cos(th) * r, Mathf.Sin(th) * r * 0.72f);
                        Vector2 tng = new(-Mathf.Sin(th), Mathf.Cos(th) * 0.72f);
                        Vector2 n = new Vector2(-tng.y, tng.x).normalized;
                        float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                        float w = 0.11f * wMul * prof * (1f - u * 0.6f) * (0.4f + s);
                        Color cc = col.WithA(col.a * (1f - u * 0.7f));
                        rib.Add(p + n * w, p - n * w, cc.WithA(0f), cc);
                    }
                }
            }
            Dyn("SpiralRim", MeshKit.Alpha, Order + 15, life * 1.3f, false, (rib, u) => Arms(rib, u, 2f, BloodBlack.WithA(0.45f)));
            Dyn("SpiralBody", AddHDR(2.4f), Order + 22, life, false, (rib, u) => Arms(rib, u, 1f, Crimson));
            Dyn("SpiralCore", AddHDR(3f), Order + 23, life * 0.7f, false, (rib, u) => Arms(rib, u, 0.3f, Color.white));
            Star(c, 1.2f, Color.white, Crimson, 0.12f, 10, true, Order + 64);
            Ring(c, 0.2f, radius * 1.05f, 0.1f, Crimson, 0.45f, false, 0.72f, 2.2f);
            for (int i = 0; i < 24; i++)
            {
                float th = spin + i * 15f;
                Vector2 d = KMath.Dir(th + 70f);
                Emit(_sparks, c + KMath.Dir(th) * 0.3f, d * Random.Range(3f, 8f), Random.Range(0.03f, 0.05f), Random.Range(0.35f, 0.7f), i % 3 == 0 ? Color.white : Crimson);
            }
            Crackle(c, radius * 0.4f, radius * 0.3f, 4);
            GameFeel.Shake(0.5f);
            GameFeel.Flash(new Color(1f, 0.5f, 0.5f, 0.2f), 0.18f);
        }

        /// <summary>
        /// A charge's draw-in to <paramref name="c"/> (the inverted time structure: a long inward build before one release): thin streaks whose heads
        /// run in from <paramref name="radius"/>, and motes pulled to the centre. It dims rather than brightens; the release flash is the caller's.
        /// </summary>
        public void InwardStreaks(Vector2 c, float radius = 3.2f, float life = 0.35f, int n = 10)
        {
            var ang = new float[n]; var r0 = new float[n]; var len = new float[n];
            for (int i = 0; i < n; i++) { ang[i] = Random.Range(0f, 360f); r0[i] = radius * Random.Range(0.7f, 1.2f); len[i] = Random.Range(0.4f, 0.9f); }
            Dyn("Inward", AddHDR(1.8f), Order + 18, life, false, (rib, u) =>
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 d = KMath.Dir(ang[i]);
                    Vector2 nrm = new(-d.y, d.x);
                    float r = Mathf.Lerp(r0[i], 0.35f, KMath.Apply(Ease.InQuad, u));
                    Vector2 hd = c + new Vector2(d.x * r, d.y * r * 0.7f), tl = c + new Vector2(d.x * (r + len[i]), d.y * (r + len[i]) * 0.7f);
                    float w = 0.014f * (0.4f + u);
                    Color hc = HotWhite.WithA(0.75f * Mathf.Sin(u * Mathf.PI)), tc = Crimson.WithA(0f);
                    rib.AddTriangle(tl + nrm * w, tl - nrm * w, hd, tc, tc, hc);
                }
            });
            for (int i = 0; i < 4; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(1.2f, radius);
                float l = Random.Range(0.25f, 0.4f);
                Emit(_embers, c + off, -off / l, 0.05f, l, i % 2 == 0 ? HotWhite : Crimson);
            }
        }

        /// <summary>A long thin lens streak through <paramref name="p"/> (aspect &gt; 40:1): it shoots out fast, holds, and thins. The eye tell and the steel glint use it.</summary>
        public void LensStreak(Vector2 p, float length, Color c, float life = 0.22f, float angleDeg = 0f, float width = 0.016f)
        {
            Vector2 d = KMath.Dir(angleDeg), n = new(-d.y, d.x);
            Dyn("LensStreak", AddHDR(2.8f), Order + 72, life, true, (rib, u) =>
            {
                float half = length * 0.5f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.25f));
                float w = width * (1f - KMath.Apply(Ease.InQuad, u));
                Color cc = Color.Lerp(Color.white, c, 0.35f + 0.5f * u).WithA(1f - u * 0.8f);
                const int seg = 10;
                for (int i = 0; i <= seg; i++)
                {
                    float s = -1f + 2f * i / seg;
                    float prof = 1f - s * s;
                    Vector2 q = p + d * (half * s);
                    rib.Add(q + n * (w * prof), q - n * (w * prof), cc.WithA(cc.a * prof), cc.WithA(cc.a * prof));
                }
            });
        }

        /// <summary>The steel tell: a white-steel four-point glint and a short diagonal streak. Blockable strikes only (the three-colour rule).</summary>
        public void SteelGlint(Vector2 p, float size = 0.4f)
        {
            Star(p, size, Color.white, Steel, 0.26f, 4, true, Order + 70, 0.35f);
            LensStreak(p, size * 2.2f, Steel, 0.18f, 35f, 0.01f);
        }

        /// <summary>The eye tell: a bright red star on the eye and a long horizontal lens streak. Iaijutsu and blinks only.</summary>
        public void EyeFlash(Vector2 eye, float size = 0.34f)
        {
            Star(eye, size, Color.white, EyeRed, 0.2f, 6, true, Order + 74, 0.5f);
            LensStreak(eye, 1.5f, EyeRed, 0.24f, 0f, 0.014f);
        }

        /// <summary>
        /// One pulse of the unblockable tell round a body centred on <paramref name="c"/>: crawling crimson-white arcs on its outline, sparks drawn in,
        /// white embers. Played every few frames through a wind-up; it must read as "do not block" at a glance.
        /// </summary>
        public void AuraPulse(Vector2 c, float rx = 0.55f, float ry = 1.0f)
        {
            Crackle(c, rx, ry, 2, Random.value < 0.4f ? HotWhite : Crimson);
            for (int i = 0; i < 3; i++)
            {
                Vector2 off = new(Random.Range(-1f, 1f) * rx * 1.6f, Random.Range(-1f, 1f) * ry * 1.3f);
                float l = Random.Range(0.18f, 0.28f);
                Emit(_sparks, c + off, -off / l * 0.9f, 0.028f, l, i == 0 ? Color.white : Crimson);
            }
            Emit(_embers, c + new Vector2(Random.Range(-rx, rx), -ry * 0.8f), new Vector2(0f, Random.Range(0.8f, 1.6f)), 0.04f, 0.4f, HotWhite);
        }

        /// <summary>
        /// The Tempest Wheel: three broken jagged arcs (70–140° each) round <paramref name="c"/>, re-struck every 3 frames as the blade turns. Not a clean ring.
        /// </summary>
        public void WheelArcs(Vector2 c, float radius, float life = 0.3f)
        {
            int seed = Random.Range(1, 1 << 20);
            float a0 = Random.Range(0f, 360f);
            void Build(Ribbon rib, float u, float wMul, Color col)
            {
                var r = new System.Random(seed + Mathf.FloorToInt(u * life / RestrikeSeconds) * 31);
                float e = 1f - KMath.Apply(Ease.InQuad, u);
                for (int k = 0; k < 3; k++)
                {
                    float start = a0 + k * 120f + u * 300f, sweep = 70f + 70f * ((k * 37 + seed) % 100) / 100f;
                    rib.Break();
                    const int seg = 12;
                    for (int i = 0; i <= seg; i++)
                    {
                        float t = i / (float)seg;
                        float th = (start + sweep * t) * Mathf.Deg2Rad;
                        float rr = radius + ((float)r.NextDouble() - 0.5f) * 0.12f;
                        Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th) * 0.55f);
                        float w = 0.05f * wMul * Mathf.Sin(t * Mathf.PI);
                        Color cc = col.WithA(col.a * e);
                        rib.Add(c + d * (rr + w), c + d * (rr - w), cc.WithA(0f), cc);
                    }
                }
            }
            Dyn("WheelBody", AddHDR(2.4f), Order + 24, life, false, (rib, u) => Build(rib, u, 1f, Crimson));
            Dyn("WheelCore", AddHDR(3f), Order + 25, life * 0.8f, false, (rib, u) => Build(rib, u, 0.35f, Color.white));
        }

        /// <summary>
        /// A held afterimage decoy (Afterimage Feint): a frozen <see cref="FighterRig.Snapshot"/> (its frozen soft meshes owned by a <see cref="MeshOwner"/>)
        /// that stands lit crimson-white for <paramref name="hold"/> seconds, flickering, then pops: a star, a ring, sparks and arcs thrown out.
        /// </summary>
        public void Decoy(FighterRig rig, float hold, Vector2 popAt)
        {
            if (rig == null) return;
            var root = rig.Snapshot(HotWhite, rig.baseOrder - 30);
            root.transform.SetParent(transform, true);
            var fx = new DecoyFx { go = root, rs = root.GetComponentsInChildren<MeshRenderer>(), life = hold, popAt = popAt, vfx = this };
            _live.Add(fx);
            fx.Tick(0f);
        }

        class DecoyFx : Fx
        {
            public MeshRenderer[] rs; public Vector2 popAt; public Vfx vfx;
            readonly MaterialPropertyBlock _mpb = new();
            public override void Tick(float u)
            {
                float a = 0.78f + Random.Range(-0.12f, 0.08f);
                _mpb.SetVector("_Color", new Vector4(1f, 1f, 1f, a));
                _mpb.SetColor("_FlashColor", Color.Lerp(HotWhite, Crimson, 0.35f + 0.4f * u));
                _mpb.SetFloat("_Flash", 0.85f);
                foreach (var r in rs) if (r != null) r.SetPropertyBlock(_mpb);
            }
            public override void Kill()
            {
                if (t >= life && vfx != null)
                {
                    vfx.Star(popAt, 0.9f, Color.white, Crimson, 0.12f, 8, true, Order + 64);
                    vfx.Ring(popAt, 0.1f, 1.2f, 0.08f, Crimson, 0.3f, false, 1f, 2.2f);
                    vfx.Crackle(popAt, 0.6f, 0.9f, 4);
                    for (int i = 0; i < 16; i++)
                        Emit(vfx._sparks, popAt, Random.insideUnitCircle.normalized * Random.Range(2.5f, 6f), 0.035f, Random.Range(0.25f, 0.5f), i % 2 == 0 ? Color.white : Crimson);
                }
                base.Kill();
            }
        }

        /// <summary>A crimson-white afterimage of <paramref name="rig"/> for blinks and dashes (<see cref="Ghost"/>: a <see cref="FighterRig.Snapshot"/>).</summary>
        public void BlinkGhost(FighterRig rig, float life = 0.28f) { if (rig != null) Ghost(rig, Color.Lerp(Crimson, HotWhite, 0.45f), life); }

        /// <summary>Dims the screen to <paramref name="amount"/> (0..1) of near black, fading back over <paramref name="seconds"/> (Final Overload's draw-in). Not an impact frame.</summary>
        public static void Dim(float amount, float seconds) => GameFeel.Flash(new Color(0.03f, 0f, 0.01f, Mathf.Clamp01(amount)), seconds);
    }
}
