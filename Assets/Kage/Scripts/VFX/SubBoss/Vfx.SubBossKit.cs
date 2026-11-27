using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Low-level pieces the four sub-boss looks (<see cref="CombatLook"/>) share: particle systems the base <see cref="Vfx"/> lacks (alpha
    /// drops that fall, cold mist that sinks, hard rock chips that tumble) and an oriented compression ring. Each boss builds its own effects
    /// from these in its own file (Vfx.Kuroe.cs, Vfx.Garan.cs, Vfx.Hisame.cs, Vfx.Shiden.cs).
    /// </summary>
    public partial class Vfx
    {
        ParticleSystem _drops, _mist, _chips;

        /// <summary>Alpha-blended droplets that streak along their velocity and fall: sumi ink, rain water.</summary>
        ParticleSystem Drops => _drops != null ? _drops
            : (_drops = MakePS("Drops", MeshKit.SoftAlpha, Order + 34, false, ParticleSystemRenderMode.Stretch, drag: 1.1f, gravity: 0.95f, sizeEnd: 0.55f));

        /// <summary>Soft alpha mist that spreads and sinks slowly (cold air is heavy; smoke rises, this does not).</summary>
        ParticleSystem Mist => _mist != null ? _mist
            : (_mist = MakePS("Mist", MeshKit.SoftAlpha, Order - 18, false, ParticleSystemRenderMode.Billboard, drag: 2.6f, gravity: 0.04f, sizeEnd: 2.3f, fadeIn: true));

        /// <summary>Hard-edged alpha chips (rock, iron, paper) that tumble and fall.</summary>
        ParticleSystem Chips => _chips != null ? _chips
            : (_chips = MakePS("Chips", MeshKit.Alpha, Order + 33, false, ParticleSystemRenderMode.Billboard, drag: 0.4f, gravity: 1.5f, sizeEnd: 0.7f));

        public void Drop(Vector2 p, Vector2 v, Color c, float size, float life) => Emit(Drops, p, v, size, life, c);
        public void MistPuff(Vector2 p, Vector2 v, Color c, float size, float life) => Emit(Mist, p, v, size, life, c);
        public void Chip(Vector2 p, Vector2 v, Color c, float size, float life) => Emit(Chips, p, v, size, life, c, Random.Range(0f, 360f));

        /// <summary>
        /// A ring facing along <paramref name="axis"/>: squashed to <paramref name="depth"/> along the axis and full size across it, so it reads as a
        /// disc of compressed air seen edge-on, perpendicular to a punch or a thrust. It rides <paramref name="travel"/> metres down the axis as it
        /// expands; the edge thins as it goes (shape change, not a scale-and-fade).
        /// </summary>
        public void OrientedRing(Vector2 c, Vector2 axis, float r0, float r1, float w0, Color col, float life, float depth = 0.3f, float travel = 0f,
            float k = 2.2f, bool unscaled = true, int order = Order + 26)
        {
            Vector2 u = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector2.right;
            Vector2 v = new(-u.y, u.x);
            Dyn("OrientedRing", AddHDR(k), order, life, unscaled, (rib, t) =>
            {
                float e = KMath.Apply(Ease.OutExpo, t);
                float r = Mathf.Lerp(r0, r1, e);
                float w = w0 * (1f - KMath.Apply(Ease.InQuad, t));
                Vector2 cc = c + u * (travel * e);
                Color cl = col.WithA(col.a * (1f - t));
                const int seg = 32;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector2 d = u * (Mathf.Cos(a) * depth) + v * Mathf.Sin(a);
                    // the side facing the travel is brighter: the leading edge of the pressure front
                    float lead = 0.55f + 0.45f * Mathf.Max(0f, Mathf.Cos(a));
                    rib.Add(cc + d * (r + w * 0.5f), cc + d * Mathf.Max(0f, r - w * 0.5f), cl.WithA(0f), cl.WithA(cl.a * lead));
                }
            });
        }

        /// <summary>A razor line from <paramref name="a"/> to <paramref name="b"/> (aspect well over 40:1): head races out, holds, the tail catches it.</summary>
        public void Needle(Vector2 a, Vector2 b, float width, Color body, Color core, float life, float k = 2.8f, int order = Order + 52)
        {
            Vector2 d = b - a;
            Vector2 n = d.sqrMagnitude > 1e-6f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
            Dyn("NeedleBody", AddHDR(k * 0.8f), order, life, true, (rib, u) => { Strip(rib, u, a, d, n, width * 2.4f, body); });
            Dyn("NeedleCore", AddHDR(k), order + 1, life * 0.8f, true, (rib, u) => { Strip(rib, u, a, d, n, width, core); });
        }

        /// <summary>One straight soft-edged strip under the Needle envelope (head out fast, hold, tail in).</summary>
        static void Strip(Ribbon rib, float u, Vector2 a, Vector2 d, Vector2 n, float width, Color c)
        {
            float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.22f));
            float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.3f) / 0.7f));
            if (head - tail <= 0.003f) return;
            const int seg = 12;
            for (int half = 0; half < 2; half++)
            {
                rib.Break();
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                    float w = width * prof;
                    Vector2 p = a + d * s;
                    Color cc = c.WithA(c.a * (1f - u * 0.6f));
                    if (half == 0) rib.Add(p + n * w, p, cc.WithA(0f), cc);
                    else rib.Add(p, p - n * w, cc, cc.WithA(0f));
                }
            }
        }

        /// <summary>Cheap smooth noise in 0..1 for breaking strokes up (bristle gaps, crack wobble): two sines and a hash.</summary>
        public static float Noise(float x, float seed)
        {
            float s = Mathf.Sin(x * 6.1f + seed * 1.7f) * 0.5f + Mathf.Sin(x * 13.7f + seed * 4.3f) * 0.3f + Mathf.Sin(x * 29.3f + seed * 9.1f) * 0.2f;
            return Mathf.Clamp01(0.5f + 0.5f * s);
        }

        /// <summary>A stable hash in 0..1 for an index and a seed (per-piece random values that do not change from frame to frame).</summary>
        public static float Hash(int i, float seed)
        {
            float h = Mathf.Sin(i * 127.1f + seed * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>
        /// A filled blob (a triangle fan) round <paramref name="c"/>: centre colour <paramref name="cc"/>, rim colour <paramref name="ce"/>. With
        /// <paramref name="wobble"/> &gt; 0 the rim is pushed in and out by noise, so ink and water read as liquid rather than a clean disc.
        /// </summary>
        public static void Blob(Ribbon rib, Vector2 c, float r, Color cc, Color ce, int seg = 12, float wobble = 0f, float seed = 0f, float sx = 1f, float sy = 1f)
        {
            if (r <= 0.0005f) return;
            Vector2 prev = default;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float rr = r * (wobble > 0f ? 1f - wobble + 2f * wobble * Noise((i % seg) * 0.37f, seed) : 1f);
                Vector2 p = c + new Vector2(Mathf.Cos(a) * rr * sx, Mathf.Sin(a) * rr * sy);
                if (i > 0) rib.AddTriangle(c, prev, p, cc, ce, ce);
                prev = p;
            }
        }

        /// <summary>
        /// A point on an ellipse round <paramref name="c"/> (radii <paramref name="rx"/>, <paramref name="ry"/>, the whole ellipse turned by
        /// <paramref name="rot"/> degrees) at <paramref name="th"/> degrees, with its outward normal: the path of every crescent and spiral.
        /// </summary>
        public static Vector2 Ell(Vector2 c, float th, float rx, float ry, float rot, out Vector2 n)
        {
            float t = th * Mathf.Deg2Rad, r = rot * Mathf.Deg2Rad;
            float cr = Mathf.Cos(r), sr = Mathf.Sin(r);
            float ct = Mathf.Cos(t), st = Mathf.Sin(t);
            Vector2 ln = new(ct * ry, st * rx);
            ln = ln.sqrMagnitude > 1e-10f ? ln.normalized : Vector2.up;
            n = new Vector2(ln.x * cr - ln.y * sr, ln.x * sr + ln.y * cr);
            float lx = ct * rx, ly = st * ry;
            return c + new Vector2(lx * cr - ly * sr, lx * sr + ly * cr);
        }

        /// <summary>
        /// A mark where an element hit the floor, in that element's own colours: a <paramref name="dark"/> ellipse that fades slowly, a
        /// <paramref name="glow"/> ring that dies first, and a little <paramref name="ash"/> rising (or, with a negative rise, settling).
        /// </summary>
        public void TintedScorch(Vector2 p, float halfWidth, float life, Color dark, Color glow, Color ash, float rise = 0.5f)
        {
            Dyn("TintedScorch", MeshKit.Alpha, Order - 30, life, false, (rib, u) =>
            {
                float a = dark.a * Mathf.Pow(1f - u, 0.7f);
                const int seg = 20;
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg * Mathf.PI;
                    float x = Mathf.Cos(t) * halfWidth, y = Mathf.Sin(t) * halfWidth * 0.12f;
                    rib.Add(p + new Vector2(x, y + 0.01f), p + new Vector2(x, -y + 0.01f), dark.WithA(a), dark.WithA(a));
                }
            });
            Ring(p + Vector2.up * 0.02f, halfWidth * 0.2f, halfWidth * 0.9f, 0.05f, glow, life * 0.45f, false, 0.16f, 1.6f, Order - 25);
            for (int i = 0; i < 4; i++)
                Emit(_dust, p + new Vector2(Random.Range(-halfWidth, halfWidth) * 0.6f, 0.05f), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.3f, 0.7f) * rise),
                    Random.Range(0.14f, 0.24f), Random.Range(0.6f, 0.9f), ash);
        }
    }
}
