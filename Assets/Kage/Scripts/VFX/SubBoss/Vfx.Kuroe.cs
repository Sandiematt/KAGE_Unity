using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kuroe, the Ink Censor — "living calligraphy" (sub-boss I; brief in docs/act1-subbosses-vfx.md). Every effect is ink: a brush stroke written
    /// fast (the head races out on an ease-out), held wet, then dried into broken dry-brush (<i>kasure</i>) streaks; a splat thrown along the blow
    /// with tendrils and satellite drops; a calligraphy seal (封 "seal", 斬 "cut") written stroke by stroke in paper-gold light on a disc of ink,
    /// floating, then burning away from its rim; an ink puddle on the floor that erupts into curved blades and melts back. Value: void-black alpha
    /// sumi body, a wet sheen lane, a paper-gold edge that only shines while the ink is fresh, lit paper behind fresh ink so the black reads on a
    /// dark stage; matter is falling ink drops, ink mist, paper ash and floor stains. Phase 2 threads Raikon's crimson static through the wet ink.
    /// </summary>
    public partial class Vfx
    {
        public static readonly Color Sumi = KMath.Hex("#07060A");        // the ink body (alpha)
        public static readonly Color SumiSheen = KMath.Hex("#3A3344");   // a lit wet streak inside the stroke
        public static readonly Color SumiMist = KMath.Hex("#5A5462");    // ink mist matter: value high enough to read on a dark stage

        // =========================================================================================== the brush stroke

        /// <summary>
        /// A calligraphy stroke written from <paramref name="a"/> to <paramref name="b"/> (bent sideways by <paramref name="bend"/> metres): ton (press)
        /// at entry, a firm body, a harai flick tapering at the exit; six bristle lanes that open into dry-brush gaps as the ink dries, edges first;
        /// with <paramref name="gold"/>, a paper-gold sheen on the fresh edge. <paramref name="write"/> is the share of the life the writing takes.
        /// </summary>
        public void InkStroke(Vector2 a, Vector2 b, float width, float life, float write = 0.18f, float bend = 0f, bool gold = true,
            int order = Order + 40, bool unscaled = false)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 n = len > 1e-5f ? new Vector2(-d.y, d.x) / len : Vector2.up;
            Vector2 ctrl = (a + b) * 0.5f + n * bend;
            float seed = Random.Range(0f, 100f);
            Vector2 P(float s) { float o = 1f - s; return o * o * a + 2f * o * s * ctrl + s * s * b; }
            Vector2 N(float s)
            {
                Vector2 t = 2f * (1f - s) * (ctrl - a) + 2f * s * (b - ctrl);
                return t.sqrMagnitude > 1e-8f ? new Vector2(-t.y, t.x).normalized : n;
            }
            float W(float s, float head)
            {
                float ton = s < 0.12f ? Mathf.Lerp(0.6f, 1.15f, s / 0.12f) : Mathf.Lerp(1.15f, 0.95f, Mathf.Clamp01((s - 0.12f) / 0.2f));
                float harai = s > 0.7f ? Mathf.Lerp(1f, 0.12f, (s - 0.7f) / 0.3f) : 1f;
                float nib = Mathf.Clamp01((head - s) / 0.05f) * 0.7f + 0.3f;       // the writing head is rounded off
                return width * ton * harai * nib;
            }
            const int lanes = 6;
            // a lit-paper glow behind the stroke while it is fresh: sumi on paper, so the black reads on a dark stage (craft §5.3)
            Dyn("InkStrokeGlow", AddHDR(1.3f), order - 1, life, unscaled, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / Mathf.Max(0.01f, write)));
                float fade = 1f - Mathf.Clamp01(u / 0.55f);
                if (fade <= 0f || head <= 0.004f) return;
                const int seg = 16;
                for (int half = 0; half < 2; half++)
                {
                    rib.Break();
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = head * i / seg;
                        Vector2 p = P(s), nn = N(s) * (half == 0 ? 1f : -1f);
                        float w = W(s, head) * 2.2f + 0.04f;
                        Color c = KuroePalette.Paper.WithA(0.2f * fade * fade);
                        rib.Add(p, p + nn * w, c, c.WithA(0f));
                    }
                }
            });
            Dyn("InkStroke", MeshKit.Alpha, order, life, unscaled, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / Mathf.Max(0.01f, write)));
                float dry = Mathf.Clamp01((u - 0.32f) / 0.6f);
                float tail = KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.55f) / 0.45f));
                if (head - tail <= 0.004f) return;
                const int seg = 28;
                for (int k = 0; k < lanes; k++)
                {
                    rib.Break();
                    float l0 = -1f + 2f * k / lanes, l1 = -1f + 2f * (k + 1) / lanes;
                    bool edge = k == 0 || k == lanes - 1;
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = Mathf.Lerp(tail, head, i / (float)seg);
                        float g = Noise(s * len * 2.4f, seed + k * 5.3f);
                        float runOut = Mathf.Max(0f, (s - 0.72f) / 0.28f) * 0.45f;      // the brush runs dry at the end of the stroke
                        float thr = dry * 0.95f + (edge ? 0.22f : 0f) + runOut;
                        float alpha = Mathf.Clamp01((g + 0.15f - thr) / 0.15f);
                        if (alpha < 0.03f) { rib.Break(); continue; }
                        Vector2 p = P(s), nn = N(s);
                        float w = W(s, head);
                        Color c = (k == 2 ? Color.Lerp(Sumi, SumiSheen, 0.75f * (1f - dry)) : Sumi).WithA(0.95f * alpha);
                        rib.Add(p + nn * (w * l1), p + nn * (w * l0), c, c);
                    }
                }
            });
            if (!gold) return;
            Dyn("InkStrokeSheen", AddHDR(2.2f), order + 2, life, unscaled, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / Mathf.Max(0.01f, write)));
                float fade = 1f - Mathf.Clamp01(u / 0.4f);
                if (fade <= 0f) return;
                float from = Mathf.Max(0f, head - 0.45f);
                const int seg = 14;
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(from, head, i / (float)seg);
                    float along = (s - from) / Mathf.Max(0.001f, head - from);
                    Vector2 p = P(s), nn = N(s);
                    float w = W(s, head);
                    Color c = Color.Lerp(KuroePalette.Gold, KuroePalette.Paper, along).WithA(0.9f * fade * along);
                    rib.Add(p + nn * (w * 0.98f), p + nn * (w * 0.78f), c.WithA(0f), c);
                }
            });
        }

        /// <summary>A sumi stain on the floor: spreads once the drops come down, holds a glossy rim briefly, then dries from the edge in.</summary>
        public void KuroeStain(Vector2 ground, float halfWidth, float life = 1.2f, float delay = 0.15f)
        {
            Vector2 p = ground + Vector2.up * 0.01f;
            float seed = Random.Range(0f, 100f);
            Dyn("KuroeStain", MeshKit.Alpha, Order - 26, life, false, (rib, u) =>
            {
                float uu = Mathf.Clamp01((u - delay) / (1f - delay));
                if (uu <= 0f) return;
                float spread = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(uu / 0.2f));
                float a = 0.88f * Mathf.Pow(1f - uu, 0.6f);
                const int seg = 22;
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg;
                    float wob = 0.75f + 0.5f * Noise(t * 3f, seed);
                    float x = Mathf.Lerp(-1f, 1f, t) * halfWidth * spread * wob;
                    float y = Mathf.Sin(t * Mathf.PI) * halfWidth * 0.12f * spread * wob;
                    rib.Add(p + new Vector2(x, y), p + new Vector2(x, -y * 0.6f), Sumi.WithA(a), Sumi.WithA(a));
                }
            });
            Ring(p + Vector2.up * 0.01f, halfWidth * 0.2f, halfWidth * 0.95f, 0.025f, KuroePalette.Gold.WithA(0.7f), life * 0.3f, false, 0.12f, 1.4f, Order - 22);
        }

        // =========================================================================================== the splat

        /// <summary>
        /// An ink splat thrown along <paramref name="dir"/>: a wobbling core blob, tendrils that shoot out (most of them inside ±48° of the blow) with
        /// a bulb of ink at each tip, and satellite drops that fly out behind them; it holds wet with a paper gloss, then dries from the outside in
        /// (the satellites first, the core last). Lit paper glows behind it while it is fresh, so the black reads on a dark stage.
        /// </summary>
        public void InkSplat(Vector2 p, Vector2 dir, float size, float life = 0.8f, bool unscaled = false, int order = Order + 38, bool crimson = false)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float baseA = KMath.Ang(d);
            float seed = Random.Range(0f, 100f);
            int nt = Random.Range(8, 12);
            var ta = new float[nt]; var tl = new float[nt]; var tw = new float[nt]; var tb = new float[nt]; var th = new float[nt];
            var drop = new bool[nt];
            for (int i = 0; i < nt; i++)
            {
                bool along = i < nt * 0.65f;
                ta[i] = along ? baseA + Random.Range(-48f, 48f) : Random.Range(0f, 360f);
                tl[i] = size * (along ? Random.Range(0.5f, 1.2f) : Random.Range(0.22f, 0.5f));
                tw[i] = size * Random.Range(0.045f, 0.085f);
                tb[i] = tw[i] * Random.Range(0.55f, 0.85f);
                th[i] = Random.Range(0.2f, 0.85f);
                drop[i] = Random.value < 0.55f;
            }
            int ns = Random.Range(8, 13);
            var sp = new Vector2[ns]; var sr = new float[ns]; var sh = new float[ns];
            for (int i = 0; i < ns; i++)
            {
                sp[i] = p + KMath.Dir(baseA + Random.Range(-62f, 62f)) * (size * Random.Range(0.8f, 1.75f));
                sr[i] = size * Random.Range(0.022f, 0.06f);
                sh[i] = Random.Range(0f, 0.55f);
            }
            Dyn("InkSplatGlow", AddHDR(1.2f), order - 2, life, unscaled, (rib, u) =>
            {
                float f = 1f - Mathf.Clamp01(u / 0.5f);
                if (f > 0f) Blob(rib, p + d * (size * 0.3f), size * 1.3f, KuroePalette.Paper.WithA(0.15f * f * f), KuroePalette.Paper.WithA(0f), 20);
            });
            Dyn("InkSplat", MeshKit.Alpha, order, life, unscaled, (rib, u) =>
            {
                float grow = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.1f));
                float dry = Mathf.Clamp01((u - 0.4f) / 0.55f);
                float A(float h) => Mathf.Clamp01((h + 0.25f - dry * 1.25f) / 0.25f) * 0.95f;
                float ca = A(1f);
                if (ca > 0.02f)
                    Blob(rib, p, size * 0.36f * Mathf.Lerp(0.5f, 1f, grow) * (1f - dry * 0.3f), Sumi.WithA(ca), Sumi.WithA(ca), 18, 0.32f, seed);
                for (int i = 0; i < nt; i++)
                {
                    float a = A(th[i]);
                    if (a < 0.03f) continue;
                    Color c = Sumi.WithA(a);
                    Vector2 td = KMath.Dir(ta[i]), tn = new(-td.y, td.x);
                    float L = tl[i] * grow;
                    // a tapered spike flung out of the core (wide at the root, a point at the tip), sometimes with a drop torn off past its tip
                    Vector2 root = p + td * (size * 0.1f), tip = root + td * L;
                    Vector2 mid = Vector2.Lerp(root, tip, 0.45f) + tn * ((Noise(i * 1.3f, seed) - 0.5f) * tw[i] * 1.2f);
                    rib.Break();
                    rib.Add(root + tn * (tw[i] * 1.9f), root - tn * (tw[i] * 1.9f), c, c);
                    rib.Add(mid + tn * (tw[i] * 0.75f), mid - tn * (tw[i] * 0.75f), c, c);
                    rib.Add(tip + tn * 0.002f, tip - tn * 0.002f, c, c);
                    if (drop[i]) Blob(rib, tip + td * (tw[i] * 1.8f), tb[i] * grow, c, c, 8, 0.15f, seed + i);
                }
                for (int j = 0; j < ns; j++)
                {
                    float fly = KMath.Apply(Ease.OutCubic, Mathf.Clamp01((u - 0.012f * j) / 0.14f));
                    float a = A(sh[j]);
                    if (fly <= 0f || a < 0.03f) continue;
                    Blob(rib, Vector2.Lerp(p, sp[j], fly), sr[j], Sumi.WithA(a), Sumi.WithA(a), 7, 0.12f, seed + j * 3f);
                }
            });
            // the wet gloss: a short lit arc on the core's upper side while the ink is fresh
            Dyn("InkSplatGloss", AddHDR(1.8f), order + 1, life * 0.4f, unscaled, (rib, u) =>
            {
                float r = size * 0.21f, w = size * 0.03f;
                Color c = KuroePalette.Paper.WithA(0.45f * (1f - u));
                const int seg = 8;
                for (int i = 0; i <= seg; i++)
                {
                    float th2 = (95f + 70f * i / seg) * Mathf.Deg2Rad;
                    Vector2 q = new(Mathf.Cos(th2), Mathf.Sin(th2));
                    float prof = Mathf.Sin(i / (float)seg * Mathf.PI);
                    rib.Add(p + q * (r + w * prof), p + q * (r - w * prof), c.WithA(0f), c.WithA(c.a * prof));
                }
            });
            if (crimson) Crackle(p, size * 0.6f, size * 0.4f, 2, Crimson);
        }

        // =========================================================================================== the seals

        /// <summary>The seal glyphs: 封 (seal, redaction) and 斬 (cut, execution), as calligraphy strokes in a unit box (y up).</summary>
        public const int GlyphSeal = 0, GlyphCut = 1;

        // per stroke: ax, ay, bx, by, bend (sideways, unit box), width multiplier; written in this order
        static readonly float[] GlyphFu =
        {
            -0.38f, 0.30f, -0.10f, 0.31f, 0.01f, 0.9f,      // 圭, upper 土
            -0.24f, 0.45f, -0.24f, 0.12f, 0f, 1f,
            -0.42f, 0.12f, -0.06f, 0.15f, 0.01f, 1f,
            -0.38f, -0.08f, -0.10f, -0.07f, 0.01f, 0.9f,    // 圭, lower 土
            -0.24f, 0.05f, -0.24f, -0.36f, 0f, 1f,
            -0.44f, -0.34f, -0.04f, -0.24f, -0.02f, 1.05f,  // its rising stroke
            0.02f, 0.19f, 0.46f, 0.21f, 0.01f, 1f,          // 寸
            0.32f, 0.46f, 0.32f, -0.38f, 0f, 1.1f,
            0.32f, -0.38f, 0.20f, -0.30f, 0f, 0.8f,         // its hook
            0.11f, 0.01f, 0.19f, -0.10f, 0f, 1.25f,         // its dot
        };

        static readonly float[] GlyphZan =
        {
            -0.42f, 0.38f, -0.06f, 0.39f, 0.01f, 0.9f,      // 車
            -0.38f, 0.26f, -0.38f, -0.06f, 0f, 0.85f,
            -0.38f, 0.26f, -0.10f, 0.26f, 0f, 0.85f,
            -0.10f, 0.26f, -0.10f, -0.06f, 0f, 0.85f,
            -0.36f, 0.10f, -0.12f, 0.10f, 0f, 0.8f,
            -0.36f, -0.06f, -0.12f, -0.06f, 0f, 0.8f,
            -0.46f, -0.23f, -0.03f, -0.17f, -0.02f, 1f,
            -0.24f, 0.47f, -0.24f, -0.47f, 0f, 1.1f,
            0.40f, 0.43f, 0.14f, 0.33f, 0.02f, 0.9f,        // 斤
            0.14f, 0.33f, 0.05f, -0.43f, 0.045f, 1.05f,
            0.14f, 0.08f, 0.47f, 0.08f, 0f, 0.95f,
            0.32f, 0.08f, 0.32f, -0.47f, 0f, 1.1f,
        };

        /// <summary>
        /// One calligraphy stroke as a strip, written up to <paramref name="head"/> (0..1) along a quadratic from a to b: ton at the entry, harai
        /// taper at the exit. <paramref name="xf"/> maps the glyph's local space to the world (the seal's float, turn and scale).
        /// </summary>
        static void BrushStrip(Ribbon rib, Vector2 a, Vector2 b, float bend, float width, float head, Color c, System.Func<Vector2, Vector2> xf)
        {
            if (head <= 0.01f) return;
            Vector2 d = b - a;
            Vector2 n = d.sqrMagnitude > 1e-8f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
            Vector2 ctrl = (a + b) * 0.5f + n * bend;
            const int seg = 9;
            rib.Break();
            for (int i = 0; i <= seg; i++)
            {
                float s = head * i / seg;
                float o = 1f - s;
                Vector2 p = o * o * a + 2f * o * s * ctrl + s * s * b;
                Vector2 t = 2f * o * (ctrl - a) + 2f * s * (b - ctrl);
                Vector2 nn = t.sqrMagnitude > 1e-8f ? new Vector2(-t.y, t.x).normalized : n;
                float ton = s < 0.15f ? Mathf.Lerp(0.75f, 1.2f, s / 0.15f) : 1f;
                float harai = s > 0.75f ? Mathf.Lerp(1f, 0.45f, (s - 0.75f) / 0.25f) : 1f;
                float w = width * ton * harai;
                rib.Add(xf(p + nn * w), xf(p - nn * w), c, c);
            }
        }

        /// <summary>
        /// A floating calligraphy seal: a disc of ink blooms open, a paper-gold ensō is written round it, then the glyph (<see cref="GlyphSeal"/> 封 or
        /// <see cref="GlyphCut"/> 斬) is written stroke by stroke in paper-gold light; it floats up <paramref name="rise"/> metres, bobbing and turning a
        /// little, and in its last quarter burns away from the rim inward, shedding paper ash and gold embers. With <paramref name="follow"/> it rides
        /// that transform (plus <paramref name="offset"/>). <paramref name="crimson"/>: Raikon's crimson in the ring and static over the seal.
        /// <paramref name="write"/> is the share of the life the glyph takes to write: a seal stamped on a hit writes fast so it reads at once.
        /// </summary>
        public void KuroeSeal(Vector2 c, int glyph, float size, float life, float rise = 0.3f, bool crimson = false, Transform follow = null,
            Vector2 offset = default, bool unscaled = false, int order = Order + 42, float write = 0.34f)
        {
            float seed = Random.Range(0f, 100f);
            float spin0 = Random.Range(-9f, 9f);
            float[] g = glyph == GlyphCut ? GlyphZan : GlyphFu;
            int strokes = g.Length / 6;
            Color ringCol = crimson ? Color.Lerp(KuroePalette.Gold, Crimson, 0.55f) : KuroePalette.Gold;
            bool followed = follow != null;
            Vector2 last = c;
            Vector2 Centre(float u)
            {
                if (followed && follow != null) last = (Vector2)follow.position + offset;
                return last + Vector2.up * (rise * KMath.Apply(Ease.OutQuad, u) + 0.025f * Mathf.Sin(u * life * 5f + seed));
            }
            float Scale(float u) => size * Mathf.LerpUnclamped(0.55f, 1f, KMath.Apply(Ease.OutBack, Mathf.Clamp01(u / 0.14f)));
            float Turn(float u) => spin0 + 5f * Mathf.Sin(u * life * 2.3f + seed);
            float Burn(float u) => Mathf.Clamp01((u - 0.72f) / 0.28f);

            Dyn("SealHalo", AddHDR(1.3f), order - 2, life, unscaled, (rib, u) =>
            {
                float f = 1f - Burn(u);
                Blob(rib, Centre(u), Scale(u) * 0.9f, KuroePalette.Paper.WithA(0.17f * f), KuroePalette.Paper.WithA(0f), 22);
            });
            Dyn("SealDisc", MeshKit.Alpha, order - 1, life, unscaled, (rib, u) =>
            {
                Vector2 cc = Centre(u);
                float R = Scale(u) * 0.6f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.1f));
                float burn = Burn(u);
                const int seg = 26;
                Vector2 prev = default;
                for (int i = 0; i <= seg; i++)
                {
                    int k = i % seg;
                    float th = i / (float)seg * Mathf.PI * 2f;
                    float eat = Mathf.Clamp01(burn * 1.7f - Hash(k, seed) * 0.7f);          // the rim burns in irregularly
                    float r = R * (0.92f + 0.16f * Noise(k * 0.41f, seed)) * (1f - eat);
                    Vector2 q = cc + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r;
                    if (i > 0)
                    {
                        Color ci = Sumi.WithA(0.9f * (1f - burn * burn)), ce = Sumi.WithA(0.9f * (1f - burn));
                        rib.AddTriangle(cc, prev, q, ci, ce, ce);
                    }
                    prev = q;
                }
            });
            Dyn("SealRing", AddHDR(2.2f), order, life, unscaled, (rib, u) =>
            {
                Vector2 cc = Centre(u);
                float R = Scale(u) * 0.66f;
                float drawn = KMath.Apply(Ease.OutCubic, Mathf.Clamp01((u - 0.02f) / 0.16f));
                float burn = Burn(u);
                const int seg = 30;
                for (int i = 0; i <= seg; i++)
                {
                    float s = i / (float)seg;
                    if (s > drawn) break;
                    if (burn > 0f && Hash(i, seed + 3f) < burn * 1.3f) { rib.Break(); continue; }
                    float th = (Turn(u) + 200f + 340f * s) * Mathf.Deg2Rad;
                    Vector2 q = new(Mathf.Cos(th), Mathf.Sin(th));
                    float w = Scale(u) * 0.045f * Mathf.Lerp(1.3f, 0.35f, s);
                    Color col = Color.Lerp(ringCol, KuroePalette.Paper, 0.3f * (1f - s)).WithA(0.9f * (1f - burn));
                    rib.Add(cc + q * (R + w), cc + q * (R - w), col, col);
                }
            });
            Dyn("SealGlyph", AddHDR(2.4f), order + 1, life, unscaled, (rib, u) =>
            {
                Vector2 cc = Centre(u);
                float s = Scale(u) * 0.86f, turn = Turn(u);
                float burn = Burn(u);
                float flick = burn > 0f ? (0.55f + 0.45f * Noise(u * 40f, seed)) * (1f - burn) : 1f;
                float pulse = 0.88f + 0.12f * Mathf.Sin(u * life * 9f + seed);
                Vector2 X(Vector2 v) => cc + KMath.Rot(v * s, turn);
                float window = write / strokes;
                for (int k = 0; k < strokes; k++)
                {
                    float t0 = 0.08f + k * window;
                    float head = KMath.Apply(Ease.OutCubic, Mathf.Clamp01((u - t0) / (window * 1.7f)));
                    if (head <= 0f) continue;
                    int o = k * 6;
                    Vector2 a = new(g[o], g[o + 1]), b = new(g[o + 2], g[o + 3]);
                    float fresh = Mathf.Clamp01((u - t0) / 0.12f);
                    Color col = Color.Lerp(Color.white, Color.Lerp(KuroePalette.Paper, KuroePalette.Gold, 0.45f), fresh).WithA(pulse * flick);
                    BrushStrip(rib, a, b, g[o + 4], 0.05f * g[o + 5], head, col, X);
                }
            });
            // the burn sheds paper ash from the rim and gold embers that rise; Phase 2 static crawls over the seal as it is finished
            int shed = 0;
            bool zapped = false;
            Dyn("SealAsh", MeshKit.Alpha, order, life, unscaled, (rib, u) =>
            {
                Vector2 cc = Centre(u);
                if (crimson && !zapped && u > 0.45f) { zapped = true; Crackle(cc, size * 0.5f, size * 0.5f, 2, Crimson); }
                int want = Mathf.FloorToInt(Burn(u) * 9f);
                for (; shed < want; shed++)
                {
                    Vector2 rim = cc + KMath.Dir(Random.Range(0f, 360f)) * (Scale(u) * 0.55f);
                    Chip(rim, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 1.1f)), Color.Lerp(KuroePalette.Paper, KuroePalette.PaperShade, Random.value), Random.Range(0.03f, 0.06f), Random.Range(0.5f, 0.8f));
                    if (shed % 2 == 0) Embers(rim, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.3f)), KuroePalette.Gold, 0.035f, 0.45f);
                }
            });
        }

        // =========================================================================================== the puddle blades

        /// <summary>
        /// An ink puddle that erupts into blades: the puddle spreads along the floor (±<paramref name="halfWidth"/>), then <paramref name="n"/> curved,
        /// tapering blades of ink shoot up out of it (the middle ones first and tallest, the outer ones leaning outward, each bowed into a crescent with
        /// a paper-gold light on its outer edge), hold, then melt back down into dry-brush streaks while ink drips off them. <paramref name="height"/>
        /// is the tallest blade; <paramref name="crimson"/>: Raikon's static jumps between the blades as they rise.
        /// </summary>
        public void KuroeInkBlades(Vector2 ground, float halfWidth, int n, float height, bool crimson, float delay = 0f, float life = 0.8f)
        {
            n = Mathf.Max(1, n);
            Vector2 g = new(ground.x, Mathf.Max(0f, ground.y) + 0.02f);
            var bx = new float[n]; var bh = new float[n]; var lean = new float[n]; var bw = new float[n]; var bend = new float[n]; var bd = new float[n];
            var erupted = new bool[n]; var melted = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (n - 1f);
                float centre = 1f - Mathf.Abs(t * 2f - 1f);
                bx[i] = (Mathf.Lerp(-halfWidth, halfWidth, t) + Random.Range(-0.06f, 0.06f) * halfWidth) * 0.85f;
                float side = Mathf.Abs(bx[i]) < 0.02f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(bx[i]);
                bh[i] = height * Mathf.Lerp(0.55f, 1f, centre) * Random.Range(0.85f, 1.1f);
                lean[i] = side * Mathf.Lerp(30f, 5f, centre) + Random.Range(-5f, 5f);
                bw[i] = Random.Range(0.065f, 0.1f) * Mathf.Lerp(0.85f, 1.25f, centre) * Mathf.Clamp(height, 0.6f, 1.4f);
                bend[i] = side * Random.Range(0.07f, 0.15f) * bh[i];
                bd[i] = delay + (1f - centre) * 0.06f;
            }
            float seed = Random.Range(0f, 100f);
            float L = life + delay + 0.08f;
            KuroeStain(g, halfWidth * 1.3f + 0.15f, L + 0.5f, 0f);

            Vector2 Bez(Vector2 a, Vector2 c, Vector2 b, float s) { float o = 1f - s; return o * o * a + 2f * o * s * c + s * s * b; }
            void Blade(int i, float lt, out Vector2 baseP, out Vector2 ctrl, out Vector2 tip, out float rise, out float melt)
            {
                rise = KMath.Apply(Ease.OutBack, Mathf.Clamp01(lt / 0.13f));
                melt = KMath.Apply(Ease.InQuad, Mathf.Clamp01((lt - 0.5f) / 0.42f));
                float h = bh[i] * rise * (1f - melt * 0.85f);
                float lr = lean[i] * Mathf.Deg2Rad;
                Vector2 up = new(Mathf.Sin(lr), Mathf.Cos(lr)), right = new(up.y, -up.x);
                baseP = g + Vector2.right * bx[i];
                tip = baseP + up * h;
                ctrl = (baseP + tip) * 0.5f + right * (bend[i] * (h / Mathf.Max(0.01f, bh[i])));
            }

            Dyn("InkBlades", MeshKit.Alpha, Order + 36, L, false, (rib, u) =>
            {
                float t = u * L;
                for (int i = 0; i < n; i++)
                {
                    float lt = (t - bd[i]) / life;
                    if (lt <= 0f || lt >= 1f) continue;
                    Blade(i, lt, out var baseP, out var ctrl, out var tip, out float rise, out float melt);
                    if (!erupted[i])
                    {
                        erupted[i] = true;
                        for (int k = 0; k < 4; k++)
                            Drop(baseP + Vector2.up * 0.05f, KMath.Dir(90f + Random.Range(-35f, 35f)) * Random.Range(2.5f, 5f), Sumi.WithA(0.92f), Random.Range(0.035f, 0.08f), Random.Range(0.35f, 0.6f));
                        Emit(_sparks, baseP + Vector2.up * 0.1f, new Vector2(Random.Range(-1f, 1f), Random.Range(2f, 4f)), 0.02f, 0.22f, KuroePalette.Gold);
                    }
                    if (!melted[i] && lt > 0.5f)
                    {
                        melted[i] = true;
                        for (int k = 0; k < 3; k++) Drop(Bez(baseP, ctrl, tip, Random.Range(0.4f, 0.9f)), new Vector2(Random.Range(-0.3f, 0.3f), -0.3f), Sumi.WithA(0.92f), Random.Range(0.04f, 0.07f), 0.5f);
                    }
                    if ((tip - baseP).sqrMagnitude < 0.0004f) continue;
                    const int seg = 12, lanes = 3;
                    for (int lane = 0; lane < lanes; lane++)
                    {
                        rib.Break();
                        float l0 = -1f + 2f * lane / lanes, l1 = -1f + 2f * (lane + 1) / lanes;
                        for (int k = 0; k <= seg; k++)
                        {
                            float s = k / (float)seg;
                            Vector2 p = Bez(baseP, ctrl, tip, s);
                            Vector2 tg = Bez(baseP, ctrl, tip, Mathf.Min(1f, s + 0.02f)) - Bez(baseP, ctrl, tip, Mathf.Max(0f, s - 0.02f));
                            Vector2 nn = tg.sqrMagnitude > 1e-9f ? new Vector2(-tg.y, tg.x).normalized : Vector2.right;
                            float w = bw[i] * Mathf.Pow(1f - s, 0.75f) * (1f + 0.25f * (1f - rise));
                            float gN = Noise(s * 4f + lane * 1.7f, seed + i * 3.1f);
                            float thr = melt * 0.95f + (lane != 1 ? 0.18f : 0f) * melt;
                            float a = Mathf.Clamp01((gN + 0.2f - thr) / 0.2f);
                            if (a < 0.03f) { rib.Break(); continue; }
                            Color c = (lane == 1 ? Color.Lerp(Sumi, SumiSheen, 0.65f * (1f - melt)) : Sumi).WithA(0.95f * a);
                            rib.Add(p + nn * (w * l1), p + nn * (w * l0), c, c);
                        }
                    }
                }
            });
            Dyn("InkBladeEdge", AddHDR(2.2f), Order + 37, L, false, (rib, u) =>
            {
                float t = u * L;
                for (int i = 0; i < n; i++)
                {
                    float lt = (t - bd[i]) / life;
                    float fresh = 1f - Mathf.Clamp01(lt / 0.5f);
                    if (lt <= 0f || fresh <= 0f) continue;
                    Blade(i, lt, out var baseP, out var ctrl, out var tip, out _, out _);
                    Vector2 bow = ctrl - (baseP + tip) * 0.5f;                                      // the convex side of the blade
                    if (bow.sqrMagnitude < 1e-8f) bow = Vector2.right * Mathf.Sign(bend[i]);
                    rib.Break();
                    const int seg = 10;
                    for (int k = 0; k <= seg; k++)
                    {
                        float s = k / (float)seg;
                        Vector2 p = Bez(baseP, ctrl, tip, s);
                        Vector2 tg = Bez(baseP, ctrl, tip, Mathf.Min(1f, s + 0.02f)) - Bez(baseP, ctrl, tip, Mathf.Max(0f, s - 0.02f));
                        Vector2 nn = tg.sqrMagnitude > 1e-9f ? new Vector2(-tg.y, tg.x).normalized : Vector2.right;
                        Vector2 outer = Vector2.Dot(nn, bow) >= 0f ? nn : -nn;
                        float w = bw[i] * Mathf.Pow(1f - s, 0.75f);
                        Color c = Color.Lerp(KuroePalette.Paper, KuroePalette.Gold, s).WithA(0.85f * fresh * (1f - s * 0.4f));
                        rib.Add(p + outer * (w * 0.55f), p + outer * (w * 1.08f), c.WithA(0f), c);
                    }
                }
            });
            if (crimson)
            {
                for (int i = 0; i + 1 < n; i += 2)
                    Arc(g + new Vector2(bx[i], bh[i] * 0.5f), g + new Vector2(bx[i + 1], bh[i + 1] * 0.45f), 0.16f, 0.016f, Crimson);
            }
        }

        // =========================================================================================== contact

        /// <summary>
        /// A blow of Kuroe's lands: a nib-thin paper star (4 frames); an ink splat thrown along the blow; the hit is written across the target as a
        /// broad slashing stroke (a second crossing stroke and a stamped 斬 seal on a heavy blow); fine ink spray along the blow; ink mist hangs;
        /// the drops stain the floor. <paramref name="crimson"/>: Raikon's static in the splash (Phase 2, a crit).
        /// </summary>
        public void KuroeInkHit(Vector2 p, Vector2 dir, float s, float floorY, bool crimson)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float baseAng = KMath.Ang(d);
            Star(p, 0.3f * s, KuroePalette.Paper, KuroePalette.Gold, 0.06f, 4, true, Order + 64, 0.35f);
            InkSplat(p + d * 0.1f, d, 0.55f * s, 0.75f, true, Order + 38, crimson);
            float tilt = Random.value < 0.5f ? 32f : -32f;
            Vector2 sd = KMath.Dir(baseAng + tilt);
            InkStroke(p - sd * (0.62f * s), p + sd * (0.8f * s), 0.095f * s, 0.5f, 0.11f, Random.Range(-0.12f, 0.12f), true, Order + 46, true);
            if (s > 1.1f)
            {
                Vector2 sd2 = KMath.Dir(baseAng - tilt * 2.2f);
                InkStroke(p - sd2 * (0.38f * s), p + sd2 * (0.5f * s), 0.07f * s, 0.48f, 0.13f, 0.06f, false, Order + 45, true);
                KuroeSeal(p + new Vector2(-d.x * 0.15f, 0.42f), GlyphCut, 0.46f, 0.6f, 0.22f, crimson, null, default, true, Order + 48, 0.16f);
            }
            for (int i = 0; i < 14; i++)
            {
                bool blob = i < 3;
                Vector2 vel = KMath.Dir(baseAng + 12f + Random.Range(-44f, 44f)) * (Random.Range(blob ? 2f : 3.5f, blob ? 4.5f : 8f) * Mathf.Sqrt(s));
                Drop(p + Random.insideUnitCircle * 0.05f, vel, Sumi.WithA(0.95f), blob ? Random.Range(0.12f, 0.2f) : Random.Range(0.04f, 0.08f), Random.Range(0.4f, 0.75f));
            }
            for (int i = 0; i < 5; i++)
                Emit(_sparks, p, KMath.Dir(baseAng + Random.Range(-40f, 40f)) * Random.Range(4f, 8f), 0.022f, Random.Range(0.16f, 0.3f), i == 0 ? KuroePalette.Paper : KuroePalette.Gold);
            for (int i = 0; i < 3; i++)
                Emit(_dust, p + Random.insideUnitCircle * 0.1f, d * Random.Range(0.3f, 0.8f) + Random.insideUnitCircle * 0.3f, Random.Range(0.25f, 0.4f) * s, Random.Range(0.5f, 0.8f), SumiMist.WithA(0.55f));
            KuroeStain(new Vector2(p.x + d.x * 0.7f, floorY), 0.34f * s, 1.2f, 0.2f);
            if (crimson) Crackle(p, 0.3f * s, 0.25f * s, 2, Crimson);
        }

        /// <summary>A blow of Kuroe's meets a guard: the ink smacks the guard in a curved smear and splats back toward her; gold sparks skate off.</summary>
        public void KuroeInkGuarded(Vector2 p, Vector2 dir)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Vector2 perp = new(-d.y, d.x);
            Star(p, 0.22f, KuroePalette.Paper, KuroePalette.Gold, 0.05f, 4, true, Order + 62, 0.5f);
            InkStroke(p + perp * 0.36f, p - perp * 0.36f, 0.07f, 0.36f, 0.1f, (Vector2.Dot(perp, Vector2.up) >= 0f ? -1f : 1f) * 0.12f * Mathf.Sign(d.x), false, Order + 44, true);
            InkSplat(p - d * 0.06f, -d, 0.3f, 0.55f, true, Order + 38);
            for (int i = 0; i < 8; i++)
                Drop(p, KMath.Dir(KMath.Ang(-d) + Random.Range(-55f, 55f) + 20f * Mathf.Sign(-d.x)) * Random.Range(2f, 4.5f), Sumi.WithA(0.9f), Random.Range(0.04f, 0.09f), Random.Range(0.3f, 0.5f));
            for (int i = 0; i < 6; i++)
                Emit(_sparks, p, KMath.Dir(KMath.Ang(-d) + Random.Range(-70f, 70f)) * Random.Range(3f, 6f), 0.02f, Random.Range(0.15f, 0.28f), KuroePalette.Gold);
        }

        // =========================================================================================== set pieces

        /// <summary>Ink flung off the brush tip at the moment of the cut, along the swing: drops, a few blobs, gold sparks.</summary>
        public void KuroeInkFling(Vector2 tip, Vector2 swing, bool crimson)
        {
            Vector2 d = swing.sqrMagnitude > 1e-6f ? swing.normalized : Vector2.right;
            for (int i = 0; i < 14; i++)
            {
                bool blob = i < 3;
                Drop(tip, KMath.Dir(KMath.Ang(d) + Random.Range(-22f, 22f)) * Random.Range(3f, 7.5f), Sumi.WithA(0.92f),
                    blob ? Random.Range(0.1f, 0.16f) : Random.Range(0.035f, 0.07f), Random.Range(0.35f, 0.6f));
            }
            for (int i = 0; i < 3; i++) Emit(_sparks, tip, d * Random.Range(5f, 8f) + Random.insideUnitCircle, 0.02f, 0.2f, KuroePalette.Gold);
            if (crimson) Crackle(tip, 0.18f, 0.18f, 1, Crimson);
        }

        /// <summary>The anticipation glint on the nib: a thin paper-gold star and one bead of ink that falls from the tip.</summary>
        public void KuroeNibGlint(Vector2 tip)
        {
            Star(tip, 0.26f, KuroePalette.Paper, KuroePalette.Gold, 0.14f, 4, true, Order + 66, 0.28f);
            LensStreak(tip, 0.6f, KuroePalette.Gold, 0.14f, 28f, 0.01f);
            Drop(tip, new Vector2(0f, -0.4f), Sumi.WithA(0.95f), 0.07f, 0.5f);
        }

        /// <summary>Ink gathering into the brush (a charge: the structure inverted): dark motes drawn in to the tip, a slow gold glint, crimson static in Phase 2.</summary>
        public void KuroeInkGather(Vector2 tip, bool crimson)
        {
            for (int i = 0; i < 7; i++)
            {
                Vector2 from = tip + KMath.Dir(Random.Range(0f, 360f)) * Random.Range(0.4f, 0.65f);
                Emit(_motes, from, (tip - from) / 0.26f, Random.Range(0.05f, 0.09f), 0.26f, Sumi.WithA(0.85f));
            }
            Star(tip, 0.28f, KuroePalette.Paper, KuroePalette.Gold, 0.22f, 4, false, Order + 60, 0.4f);
            if (crimson) Crackle(tip, 0.2f, 0.2f, 1, Crimson);
        }

        /// <summary>
        /// The Censure punish, Kuroe's execution: one great Ichimonji stroke written across the dash with a paper flash on the same frame, a splat
        /// thrown off its end, ink flung on, and a large 斬 seal stamped over the cut that floats and burns away.
        /// </summary>
        public void KuroeIchimonji(Vector2 from, Vector2 to, bool crimson)
        {
            InkStroke(from, to, 0.19f, 0.8f, 0.12f, 0.12f, true, Order + 47, true);
            InkSplat(to - (to - from).normalized * 0.25f, to - from, 0.8f, 0.9f, true, Order + 38, crimson);
            KuroeSeal(Vector2.Lerp(from, to, 0.5f) + Vector2.up * 0.45f, GlyphCut, 0.8f, 0.9f, 0.22f, crimson, null, default, true, Order + 49, 0.2f);
            KuroeInkFling(to, to - from, crimson);
            GameFeel.Flash(KuroePalette.Paper.WithA(0.18f), 0.08f);
            GameFeel.Shake(0.2f);
            if (crimson) Bolt(from, to, 0.024f, 0.22f, Crimson, 10, 0.06f, 1);
        }

        /// <summary>The low sweep: a broad brush stroke dragged along the floor, drops skimming low, a splash and a stain where it ends.</summary>
        public void KuroeFloorStroke(Vector2 from, Vector2 to)
        {
            InkStroke(from + Vector2.up * 0.07f, to + Vector2.up * 0.07f, 0.12f, 0.62f, 0.16f, 0.05f, true, Order + 30);
            Vector2 d = (to - from).normalized;
            for (int i = 0; i < 10; i++)
                Drop(Vector2.Lerp(from, to, Random.Range(0.4f, 1f)) + Vector2.up * 0.08f, d * Random.Range(2f, 5f) + Vector2.up * Random.Range(0.4f, 1.4f),
                    Sumi.WithA(0.9f), Random.Range(0.04f, 0.08f), Random.Range(0.3f, 0.5f));
            InkSplat(to + Vector2.up * 0.06f, d + Vector2.up * 0.6f, 0.4f, 0.6f, false, Order + 30);
            KuroeStain(to, 0.4f, 1.1f, 0.05f);
        }

        /// <summary>
        /// The overhead brush smash: a splash crown thrown up off the floor, a paper star, a flat gold pressure ring, a ring of ink blades erupting from
        /// the puddle round the impact, ink mist and a broad stain.
        /// </summary>
        public void KuroeInkSlam(Vector2 ground, float s, bool crimson)
        {
            Vector2 g = ground + Vector2.up * 0.05f;
            Star(g + Vector2.up * 0.12f, 0.4f * s, KuroePalette.Paper, KuroePalette.Gold, 0.07f, 6, true, Order + 64, 0.5f);
            Ring(g, 0.06f, 1.0f * s, 0.05f, KuroePalette.Gold, 0.3f, false, 0.18f, 1.8f);
            InkSplat(g + Vector2.up * 0.08f, Vector2.up, 0.62f * s, 0.8f, false, Order + 32, crimson);
            KuroeInkBlades(ground, 0.6f * s, 4, 0.7f * s, crimson, 0.03f, 0.7f);
            for (int i = 0; i < 16; i++)
            {
                bool blob = i < 4;
                Drop(g + new Vector2(Random.Range(-0.15f, 0.15f), 0f), KMath.Dir(Random.Range(35f, 145f)) * Random.Range(blob ? 2.2f : 3.2f, blob ? 4f : 6.5f) * s,
                    Sumi.WithA(0.95f), blob ? Random.Range(0.12f, 0.2f) : Random.Range(0.04f, 0.08f), Random.Range(0.45f, 0.8f));
            }
            for (int i = 0; i < 4; i++)
                Emit(_dust, g + new Vector2(Random.Range(-0.3f, 0.3f), 0.05f), new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.1f, 0.4f)), Random.Range(0.3f, 0.45f), Random.Range(0.6f, 0.9f), SumiMist.WithA(0.5f));
            if (crimson) Crackle(g + Vector2.up * 0.3f, 0.5f, 0.3f, 2, Crimson);
            GameFeel.Shake(0.22f);
        }

        /// <summary>Telegraph of the Phase 2 ink line: the line is written along the floor over the first part of the tell, then crimson sparks crawl in the wet ink.</summary>
        public void KuroeInkLineMark(Vector2 a, Vector2 b, float seconds)
        {
            if (seconds <= 0f) return;
            InkStroke(a + Vector2.up * 0.03f, b + Vector2.up * 0.03f, 0.06f, seconds + 0.2f, 0.4f, 0f, true, Order - 12);
            Dyn("InkLineSparks", MeshKit.Alpha, Order - 12, seconds, false, (rib, u) =>
            {
                if (u < 0.35f || Random.value > 0.2f + 0.3f * u) return;
                Vector2 p = Vector2.Lerp(a, b, Random.value) + Vector2.up * 0.05f;
                Emit(_sparks, p, new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 1.8f)), 0.02f, 0.2f, u > 0.75f ? Color.white : Crimson);
            });
        }

        /// <summary>Strike of the ink line: the written line pools and erupts into a row of ink blades along its length, short crimson bolts drop into it, drops spray, a stain stays.</summary>
        public void KuroeInkLineStrike(Vector2 a, Vector2 b)
        {
            float len = (b - a).magnitude;
            int n = Mathf.Max(3, Mathf.CeilToInt(len / 0.32f));
            KuroeInkBlades((a + b) * 0.5f, len * 0.5f, n, 0.95f, true, 0f, 0.75f);
            for (int i = 0; i <= 2; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / 2f);
                Bolt(p + new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.8f, 1.1f)), p, 0.026f, 0.22f, Crimson, 6, 0.05f, 1);
            }
            for (int i = 0; i < 10; i++)
                Drop(Vector2.Lerp(a, b, Random.value) + Vector2.up * 0.1f, KMath.Dir(Random.Range(60f, 120f)) * Random.Range(2.5f, 5f), Sumi.WithA(0.92f), Random.Range(0.04f, 0.09f), Random.Range(0.35f, 0.6f));
            GameFeel.Shake(0.2f);
        }

        /// <summary>
        /// Strike of Thunder Redaction: a sub-boss crimson bolt (3.6 m, half Raikon's) on the same frame as a paper flash; the seal's puddle erupts
        /// into a crown of ink blades round a tall central one; a 封 seal stamps over the strike and burns; the slip shreds into paper flakes; ink
        /// splashes up; a stain and a scorch stay.
        /// </summary>
        public void KuroeRedactionStrike(Vector2 ground, float halfWidth)
        {
            Vector2 g = new(ground.x, Mathf.Max(0f, ground.y));
            Bolt(g + new Vector2(Random.Range(-0.2f, 0.2f), 3.6f), g, 0.05f, 0.34f, Crimson, 12, 0.14f, 2);
            Star(g + Vector2.up * 0.3f, 0.55f, KuroePalette.Paper, Crimson, 0.08f, 6, true, Order + 66);
            KuroeInkBlades(g, halfWidth * 1.1f, 5, 1.5f, true, 0.02f, 0.85f);
            KuroeSeal(g + Vector2.up * 1.25f, GlyphSeal, 0.75f, 0.75f, 0.35f, true);
            for (int i = 0; i < 16; i++)
                Drop(g + Vector2.up * 0.15f, KMath.Dir(Random.Range(40f, 140f)) * Random.Range(2.5f, 6f), Sumi.WithA(0.95f), Random.Range(0.05f, 0.14f), Random.Range(0.45f, 0.8f));
            for (int i = 0; i < 10; i++)
                Chip(g + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(0.2f, 1.2f)), new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.5f, 2.2f)),
                    Color.Lerp(KuroePalette.Paper, KuroePalette.PaperShade, Random.value), Random.Range(0.05f, 0.09f), Random.Range(0.6f, 1.0f));
            Ring(g + Vector2.up * 0.03f, 0.08f, halfWidth * 1.6f, 0.06f, KuroePalette.Gold, 0.3f, false, 0.2f, 2f);
            Scorch(g, halfWidth, 0.9f);
            GameFeel.Flash(Crimson.WithA(0.16f), 0.1f);
            GameFeel.Shake(0.3f);
        }

        // =========================================================================================== presence

        /// <summary>
        /// Kuroe's presence between moves: ink beads off the brush tip, and now and then a small calligraphy seal floats up off her shoulder and burns
        /// away; in Phase 2 the seals come often and carry crimson, crimson static crawls the nib and paper ash drifts up.
        /// </summary>
        public void KuroeAura(Fighter f, int level)
        {
            Vector2 tip = f.rig.World(B.Sword, f.BladeTipLocal * 0.97f);
            Puff(tip, new Vector2(-f.facing * 0.22f, 0.38f), Sumi.WithA(0.58f), 0.13f, 0.65f);
            Drop(tip, new Vector2(Random.Range(-0.1f, 0.1f), -0.3f), Sumi.WithA(0.95f), Random.Range(0.05f, 0.08f), 0.5f);
            if (Random.value < (level < 1 ? 0.08f : 0.2f))
                KuroeSeal(f.Pos + new Vector2(-f.facing * Random.Range(0.3f, 0.65f), Random.Range(1.3f, 1.85f)), Random.value < 0.5f ? GlyphSeal : GlyphCut,
                    Random.Range(0.24f, 0.32f), 1.3f, 0.5f, level >= 1);
            if (level < 1) return;
            Arc(f.rig.World(B.Sword, f.BladeTipLocal * 0.7f), tip, 0.1f, 0.012f, Random.value < 0.6f ? Crimson : KuroePalette.Gold);
            Emit(_motes, f.Pos + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.2f, 1.6f)), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.3f, 0.6f)),
                0.05f, 0.9f, KuroePalette.Paper.WithA(0.5f));
        }

        /// <summary>Kuroe turns to "Redacted": an ink smash and blade crown at her feet, an ensō written round them, a pillar stroke up through her, three seals blooming round her.</summary>
        public void KuroePhaseBurst(Fighter f)
        {
            Vector2 g = f.Pos;
            KuroeInkSlam(g, 1.2f, true);
            InkStroke(g + new Vector2(-0.95f, 0.04f), g + new Vector2(0.95f, 0.04f), 0.09f, 0.9f, 0.25f, -0.22f, true, Order - 10);
            InkStroke(g, g + new Vector2(0f, 2.3f), 0.22f, 0.7f, 0.12f, 0.15f * f.facing, true, Order + 30);
            KuroeSeal(g + new Vector2(-0.95f, 1.45f), GlyphSeal, 0.5f, 1.4f, 0.4f, true);
            KuroeSeal(g + new Vector2(0f, 2.45f), GlyphCut, 0.55f, 1.5f, 0.4f, true);
            KuroeSeal(g + new Vector2(0.95f, 1.45f), GlyphSeal, 0.5f, 1.4f, 0.4f, true);
            Crackle(g + Vector2.up * 1.1f, 0.5f, 0.8f, 3, Crimson);
        }
    }

    /// <summary>Kuroe's combat look: the ink-brush trail on her brush and her written-ink impacts.</summary>
    public sealed class KuroeLook : CombatLook
    {
        public override WeaponTrail Trail(Fighter f, TrailSlot slot, Transform parent, int order) =>
            slot == TrailSlot.Blade ? new KuroeInkTrail(parent, order, f) : null;

        public override bool Hit(Fighter a, Vector2 p, Vector2 dir, HitWindow w, bool crit)
        {
            var v = Vfx.I;
            if (v == null) return false;
            v.KuroeInkHit(p, dir, w.heavy || crit ? 1.3f : 1f, a.Pos.y, PhaseTwo(a) || crit);
            return true;
        }

        public override bool Block(Fighter a, Vector2 p, Vector2 dir, HitWindow w)
        {
            var v = Vfx.I;
            if (v == null) return false;
            v.KuroeInkGuarded(p, dir);
            return true;
        }
    }

    /// <summary>
    /// The brush's real path painted in wet sumi: five bristle lanes across the brush belly and a little past its tip (the stroke is broader than the
    /// brush), each a concentric streak of the swing; the lanes break into dry-brush dashes as the ink ages (the edge lanes first); a fringe of ink
    /// specks flung just beyond the outer edge; a paper-gold sheen on the fresh outer edge; ink flung off the tip in proportion to its speed. Lives
    /// 0.5 s: the stroke is read, then dries away.
    /// </summary>
    public sealed class KuroeInkTrail : SampledTrail
    {
        readonly float _seed = Random.Range(0f, 100f);

        public KuroeInkTrail(Transform parent, int order, Fighter f)
            : base("KuroeInkTrail", parent, 0.5f, order, f, Vfx.I != null ? Vfx.I.AddHDR(1.3f) : MeshKit.Additive, MeshKit.Alpha,
                Vfx.I != null ? Vfx.I.AddHDR(2.0f) : MeshKit.Additive) { }

        protected override void DrawStroke(List<Section> secs, bool live)
        {
            var glow = ribs[0];
            var ink = ribs[1];
            var sheen = ribs[2];
            // lit paper behind the fresh ink (soft both ways from the brush belly), so the black reads on a dark stage
            for (int half = 0; half < 2; half++)
            {
                glow.Break();
                for (int i = 0; i < secs.Count; i++)
                {
                    var s = secs[i];
                    float a = Mathf.Clamp01(1f - s.age / 0.6f);
                    Color c = KuroePalette.Paper.WithA(0.22f * a * a * Mathf.Clamp01(s.along * 6f));
                    Vector2 mid = Vector2.LerpUnclamped(s.a, s.b, 0.62f);
                    Vector2 edge = Vector2.LerpUnclamped(s.a, s.b, half == 0 ? 1.28f : 0.0f);
                    glow.Add(mid, edge, c, c.WithA(0f));
                }
            }
            const int lanes = 5;
            for (int k = 0; k < lanes; k++)
            {
                ink.Break();
                float f0 = 0.08f + 1.06f * k / lanes, f1 = 0.08f + 1.06f * (k + 1) / lanes - 0.012f;
                bool edge = k == 0 || k == lanes - 1;
                for (int i = 0; i < secs.Count; i++)
                {
                    var s = secs[i];
                    float dry = Mathf.Clamp01((s.age - 0.28f) / 0.62f);
                    float g = Vfx.Noise(s.t * 22f, _seed + k * 4.7f);
                    float thr = dry * 0.95f + (edge ? 0.2f : 0f) * Mathf.Clamp01(s.age * 4f);
                    float alpha = Mathf.Clamp01((g + 0.15f - thr) / 0.15f);
                    alpha *= Mathf.Clamp01(s.along * 8f);                           // the oldest end feathers out
                    if (alpha < 0.03f) { ink.Break(); continue; }
                    Color c = (k == 2 ? Color.Lerp(Vfx.Sumi, Vfx.SumiSheen, 0.7f * (1f - dry)) : Vfx.Sumi).WithA(0.94f * alpha);
                    ink.Add(Vector2.LerpUnclamped(s.a, s.b, f0), Vector2.LerpUnclamped(s.a, s.b, f1), c, c);
                }
            }
            // flung specks just past the outer edge, keyed to the sample time so they stay put while the stroke dries
            for (int i = 0; i < secs.Count; i += 3)
            {
                var s = secs[i];
                float h = Vfx.Hash(Mathf.RoundToInt(s.t * 240f), _seed);
                if (h < 0.55f || s.age > 0.75f) continue;
                Vector2 q = Vector2.LerpUnclamped(s.a, s.b, 1.16f + 0.22f * h);
                float r = 0.012f + 0.032f * (h - 0.55f) / 0.45f;
                float a = 0.92f * Mathf.Clamp01((0.75f - s.age) / 0.2f) * Mathf.Clamp01(s.along * 6f);
                Vfx.Blob(ink, q, r, Vfx.Sumi.WithA(a), Vfx.Sumi.WithA(a), 6, 0.15f, h * 50f);
            }
            sheen.Break();
            for (int i = 0; i < secs.Count; i++)
            {
                var s = secs[i];
                float a = Mathf.Clamp01(1f - s.age / 0.24f);
                Color c = Color.Lerp(KuroePalette.Paper, KuroePalette.Gold, Mathf.Clamp01(s.age * 4f)).WithA(0.85f * a * a);
                sheen.Add(Vector2.LerpUnclamped(s.a, s.b, 1.06f), Vector2.LerpUnclamped(s.a, s.b, 1.14f), c.WithA(0f), c);
            }
        }

        protected override void OnMove(Vector2 pa, Vector2 pb, Vector2 a, Vector2 b, float dt)
        {
            var v = Vfx.I;
            if (v == null || dt <= 0f) return;
            Vector2 vel = (b - pb) / dt;
            float sp = vel.magnitude;
            if (sp < 3f) return;
            int n = sp > 9f ? 2 : 1;
            for (int i = 0; i < n; i++)
                v.Drop(Vector2.Lerp(pb, b, Random.value), vel * Random.Range(0.16f, 0.3f) + Vector2.up * Random.Range(0.2f, 1.2f), Vfx.Sumi.WithA(0.92f),
                    Random.Range(0.045f, 0.09f), Random.Range(0.35f, 0.6f));
            if (PhaseTwo && Random.value < 0.12f) v.Arc(Vector2.Lerp(a, b, 0.6f), b, 0.08f, 0.012f, Vfx.Crimson);
        }
    }

    /// <summary>
    /// Kuroe's Bokuseki Wave in flight: a broad crescent of sumi whose bristle streaks roll as it flies, a gold leading edge, ink specks thrown
    /// ahead of it, and a dry-brush tail written along the real flight path that dries out behind it. Lives in the Vfx root, so on impact the body
    /// is gone but the tail finishes drying.
    /// </summary>
    public sealed class InkWaveLook : MonoBehaviour
    {
        Transform _proj;
        float _dir, _t, _dead, _drip, _seed;
        Vector2 _pos;
        Ribbon _body, _edge, _tail;
        readonly List<Vector2> _path = new();
        readonly List<float> _pathT = new();
        const float TailLife = 0.34f, DeadLife = 0.35f, R = 0.42f, Thick = 0.26f;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || Vfx.I == null) return;
            var go = new GameObject("InkWaveLook");
            go.transform.SetParent(Vfx.I.transform, false);
            var l = go.AddComponent<InkWaveLook>();
            l._proj = a.follow;
            l._pos = a.follow.position;
            l._dir = a.dir.x >= 0f ? 1f : -1f;
            l._seed = Random.Range(0f, 100f);
            int o = Vfx.Order + 32;
            l._tail = new Ribbon("InkWaveTail", go.transform, MeshKit.Alpha, o - 1);
            l._body = new Ribbon("InkWaveBody", go.transform, MeshKit.Alpha, o);
            l._edge = new Ribbon("InkWaveEdge", go.transform, Vfx.I.AddHDR(2.0f), o + 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_proj != null)
            {
                _pos = _proj.position;
                if (_path.Count == 0 || (_path[^1] - _pos).sqrMagnitude > 1e-5f) { _path.Add(_pos); _pathT.Add(_t); }
                _drip += dt;
                if (_drip > 0.03f && Vfx.I != null)
                {
                    _drip = 0f;
                    Vfx.I.Drop(_pos + Random.insideUnitCircle * 0.2f, new Vector2(-_dir * 0.6f, -0.4f), Vfx.Sumi.WithA(0.92f), Random.Range(0.05f, 0.1f), 0.5f);
                    if (Random.value < 0.5f)
                        Vfx.I.Drop(_pos + new Vector2(_dir * 0.2f, Random.Range(-0.3f, 0.3f)), new Vector2(_dir * Random.Range(2.5f, 4f), Random.Range(-0.3f, 0.6f)), Vfx.Sumi.WithA(0.9f),
                            Random.Range(0.03f, 0.06f), 0.3f);
                }
            }
            else
            {
                _dead += dt;
                if (_dead > DeadLife) { Destroy(gameObject); return; }
            }
            while (_pathT.Count > 0 && _t - _pathT[0] > TailLife) { _pathT.RemoveAt(0); _path.RemoveAt(0); }
            DrawTail();
            DrawBody(_proj != null ? 1f : Mathf.Clamp01(1f - _dead / 0.12f));
        }

        void DrawBody(float k)
        {
            _body.Begin(); _edge.Begin();
            if (k > 0.01f)
            {
                const int seg = 20, lanes = 5;
                Vector2 c = _pos - new Vector2(0.3f * _dir, 0f);
                for (int lane = 0; lane < lanes; lane++)
                {
                    _body.Break();
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = i / (float)seg;
                        float th = Mathf.Lerp(-78f, 78f, s) * Mathf.Deg2Rad;
                        float thick = Thick * Mathf.Sin(s * Mathf.PI) * k;
                        float r0 = R + thick * (lane / (float)lanes), r1 = R + thick * ((lane + 1) / (float)lanes) - 0.006f;
                        Vector2 d = new(Mathf.Cos(th) * _dir, Mathf.Sin(th));
                        float g = Vfx.Noise(s * 3f - _t * 9f, _seed + lane * 3.1f);     // the streaks roll with the flight
                        float a = Mathf.Clamp01((g - 0.16f) / 0.15f) * 0.95f;
                        if (a < 0.03f) { _body.Break(); continue; }
                        Color col = (lane == 1 ? Vfx.SumiSheen : Vfx.Sumi).WithA(a);
                        _body.Add(c + d * r1, c + d * r0, col, col);
                    }
                }
                for (int i = 0; i <= seg; i++)
                {
                    float s = i / (float)seg;
                    float th = Mathf.Lerp(-78f, 78f, s) * Mathf.Deg2Rad;
                    Vector2 d = new(Mathf.Cos(th) * _dir, Mathf.Sin(th));
                    float r = R + Thick * Mathf.Sin(s * Mathf.PI);
                    Color col = Color.Lerp(KuroePalette.Gold, KuroePalette.Paper, Mathf.Sin(s * Mathf.PI)).WithA(0.85f * k * Mathf.Sin(s * Mathf.PI));
                    _edge.Add(c + d * (r + 0.025f), c + d * (r - 0.014f), col.WithA(0f), col);
                }
            }
            _body.End(); _edge.End();
        }

        void DrawTail()
        {
            _tail.Begin();
            if (_path.Count >= 2)
            {
                const int lanes = 4;
                for (int lane = 0; lane < lanes; lane++)
                {
                    _tail.Break();
                    for (int i = 0; i < _path.Count; i++)
                    {
                        float age = Mathf.Clamp01((_t - _pathT[i]) / TailLife);
                        float h = Mathf.Lerp(0.34f, 0.06f, age);
                        float y0 = Mathf.Lerp(-h, h, lane / (float)lanes), y1 = Mathf.Lerp(-h, h, (lane + 1) / (float)lanes) - 0.006f;
                        float g = Vfx.Noise(_pathT[i] * 18f, _seed + lane * 2.3f);
                        float a = Mathf.Clamp01((g - age * 0.95f) / 0.14f) * 0.9f;
                        if (a < 0.03f) { _tail.Break(); continue; }
                        Color col = Vfx.Sumi.WithA(a);
                        _tail.Add(_path[i] + new Vector2(0f, y1), _path[i] + new Vector2(0f, y0), col, col);
                    }
                }
            }
            _tail.End();
        }
    }

    /// <summary>
    /// The tell of Thunder Redaction, under Kage's feet: an ensō written round him in one brush circle (open at the end, as an ensō is), tightening;
    /// then three redaction bars slam across it, one by one, while crimson static gathers in the wet ink; a 封 seal hangs over him for the whole tell
    /// and burns as the strike comes. Follows the hazard while it tracks.
    /// </summary>
    public sealed class KuroeRedactionMark : MonoBehaviour
    {
        float _t, _life, _w, _spark, _seed;
        Ribbon _ink, _gold;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (Vfx.I == null) return;
            if (a.follow == null) { Vfx.I.KuroeStain(pos, 0.5f, Mathf.Max(0.25f, a.seconds), 0f); return; }
            var go = new GameObject("KuroeRedactionMark");
            go.transform.SetParent(a.follow, false);
            var m = go.AddComponent<KuroeRedactionMark>();
            m._life = Mathf.Max(0.1f, a.seconds);
            m._w = a.size.x > 0f ? a.size.x * 0.5f : 0.55f;
            m._seed = Random.Range(0f, 100f);
            m._ink = new Ribbon("RedactionInk", go.transform, MeshKit.Alpha, Vfx.Order - 12);
            m._gold = new Ribbon("RedactionGold", go.transform, Vfx.I.AddHDR(2.0f), Vfx.Order - 10);
            Vfx.I.KuroeSeal(pos, Vfx.GlyphSeal, 0.5f, m._life + 0.1f, 0.15f, true, a.follow, new Vector2(0f, 2.05f));
        }

        void Update()
        {
            var v = Vfx.I;
            _t += Time.deltaTime;
            float u = _t / _life;
            if (v == null || u >= 1f) { Destroy(gameObject); return; }
            Vector2 c = (Vector2)transform.position + Vector2.up * 0.03f;
            float rx = Mathf.Lerp(_w * 1.35f, _w * 0.95f, KMath.Apply(Ease.InOutQuad, u)), ry = rx * 0.24f;
            _ink.Begin(); _gold.Begin();

            // the ensō: written over the first 45 % of the tell, from 200° round 330°
            float drawn = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / 0.45f));
            const int seg = 40;
            for (int lane = 0; lane < 3; lane++)
            {
                _ink.Break();
                for (int i = 0; i <= seg; i++)
                {
                    float s = i / (float)seg * drawn;
                    float th = (200f + 330f * s) * Mathf.Deg2Rad;
                    float press = Mathf.Lerp(1.1f, 0.25f, s) * 0.07f;
                    float g = Vfx.Noise(s * 7f, _seed + lane * 3.7f);
                    float a = Mathf.Clamp01((g - (lane == 1 ? 0f : 0.25f) - Mathf.Max(0f, s - 0.8f) * 1.5f) / 0.12f + (lane == 1 ? 1f : 0f)) * 0.92f;
                    if (a < 0.03f) { _ink.Break(); continue; }
                    Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th));
                    Vector2 p = c + new Vector2(d.x * rx, d.y * ry);
                    float l0 = -1f + 2f * lane / 3f, l1 = -1f + 2f * (lane + 1) / 3f;
                    Vector2 nn = new Vector2(d.x * ry, d.y * rx).normalized;
                    Color col = Vfx.Sumi.WithA(a);
                    _ink.Add(p + nn * (press * l1), p + nn * (press * l0), col, col);
                }
            }
            // the gold rim pulses as the seal tightens
            float pulse = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_t * Mathf.Lerp(6f, 18f, u)));
            for (int i = 0; i <= seg; i++)
            {
                float s = i / (float)seg * drawn;
                float th = (200f + 330f * s) * Mathf.Deg2Rad;
                Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th));
                Vector2 p = c + new Vector2(d.x * (rx + 0.05f), d.y * (ry + 0.012f));
                Color col = Color.Lerp(KuroePalette.Gold, Vfx.Crimson, u).WithA(0.7f * pulse);
                _gold.Add(p + Vector2.up * 0.012f, p - Vector2.up * 0.012f, col.WithA(0f), col);
            }
            // three redaction bars slam across the seal
            for (int bar = 0; bar < 3; bar++)
            {
                float at = 0.52f + bar * 0.14f;
                float k = Mathf.Clamp01((u - at) / 0.05f);
                if (k <= 0f) continue;
                float y = (bar - 1) * ry * 0.55f;
                float half = rx * 0.7f * KMath.Apply(Ease.OutExpo, k);
                _ink.Break();
                Color col = Vfx.Sumi.WithA(0.95f);
                _ink.Add(c + new Vector2(-half, y + 0.035f), c + new Vector2(-half, y - 0.035f), col, col);
                _ink.Add(c + new Vector2(half, y + 0.04f), c + new Vector2(half, y - 0.03f), col, col);
            }
            _ink.End(); _gold.End();

            _spark -= Time.deltaTime;
            if (u > 0.5f && _spark <= 0f)
            {
                _spark = Mathf.Lerp(0.12f, 0.05f, u);
                v.Crackle(c + Vector2.up * 0.02f, rx * 0.85f, ry * 1.5f, 1, Vfx.Crimson);
            }
        }
    }
}
