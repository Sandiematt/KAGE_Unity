using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Garan, the Storm Bearer — "the storm brought down" (sub-boss II; brief in docs/act1-subbosses-vfx.md). Weight and thunder: his blows carry
    /// blue-white lightning, and the heavy ones call it down. Shape: thick vertical strikes from the sky with side strands, short bolts bursting
    /// radially out of a contact, a dome of air pushed up off the floor, glowing cracks racing along it. Motion: falls, slams, bursts outward, never
    /// sweeps. Rhythm: heavy — a big contact, a held crackle, then rock and dust settle. Material: a white-hot core in an electric-blue body inside a
    /// storm-navy rim, so it reads on bright stages too; matter is rock chips, dust and a scorch with a blue glow. His fists and shins leave short
    /// electric trails re-struck every 3 frames. Phase 2 forks Raikon's crimson through the strikes.
    /// </summary>
    public partial class Vfx
    {
        public static readonly Color GaranVolt = KMath.Hex("#58A6FF");       // body: electric blue (bluer than Hisame's aqua water)
        public static readonly Color GaranVoltHot = KMath.Hex("#E6F2FF");    // core: blue-white
        public static readonly Color GaranVoltDeep = KMath.Hex("#2A55FF");   // outer glow, a cooling crack
        public static readonly Color GaranNight = KMath.Hex("#050A20");      // the rim of his bolts: storm navy
        public static readonly Color GaranStone = KMath.Hex("#6A625A");      // rock chips
        public static readonly Color GaranStoneDark = KMath.Hex("#3E3833");
        public static readonly Color GaranDust = KMath.Hex("#7D7268");       // dust thrown off the floor

        // =========================================================================================== lightning pieces

        /// <summary>One of Garan's bolts: blue-white core, electric-blue body (Raikon's crimson when <paramref name="crimson"/>), storm-navy rim.</summary>
        public void GaranBolt(Vector2 a, Vector2 b, float width, float life, bool crimson = false, int forks = 2, int segments = 14, float jitter = -1f, int order = Order + 55)
        {
            Bolt(a, b, width, life, crimson ? Crimson : GaranVolt, GaranNight, 2.8f, 0.6f, segments, jitter, forks, false, order, 1f, 1.1f);
        }

        /// <summary>A short crawling arc of Garan's static (no forks), storm navy behind it.</summary>
        public void GaranArc(Vector2 a, Vector2 b, float life = 0.11f, float width = 0.015f, bool crimson = false)
        {
            float len = (b - a).magnitude;
            Bolt(a, b, width, life, crimson ? Crimson : GaranVolt, GaranNight, 2.4f, 0.5f, Mathf.Clamp(Mathf.RoundToInt(len / 0.07f), 4, 9), len * 0.18f, 0, false, Order + 48);
        }

        /// <summary><paramref name="n"/> of Garan's arcs crawling on an ellipse round <paramref name="c"/> (his aura surging round his body).</summary>
        public void GaranCrackle(Vector2 c, float rx, float ry, int n, bool crimson = false)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = Random.Range(0f, 360f), a1 = a0 + Random.Range(35f, 90f) * (Random.value < 0.5f ? 1f : -1f);
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad) * rx, Mathf.Sin(a0 * Mathf.Deg2Rad) * ry);
                Vector2 p1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad) * rx, Mathf.Sin(a1 * Mathf.Deg2Rad) * ry);
                GaranArc(p0, p1, Random.Range(0.08f, 0.14f), Random.Range(0.013f, 0.022f), crimson && Random.value < 0.5f);
            }
        }

        /// <summary>Short bolts bursting out of a contact: most inside ±70° of the blow, one kicked back; each its own reach, re-struck as they live.</summary>
        public void GaranVoltBurst(Vector2 p, Vector2 dir, float s, int n, bool crimson)
        {
            float baseA = KMath.Ang(dir);
            for (int i = 0; i < n; i++)
            {
                float ang = baseA + (i == n - 1 ? 180f + Random.Range(-40f, 40f) : Random.Range(-70f, 70f));
                float len = Random.Range(0.35f, 0.75f) * s;
                GaranBolt(p, p + KMath.Dir(ang) * len, Random.Range(0.018f, 0.028f) * s, Random.Range(0.14f, 0.2f), crimson && i == 0, 1, 7, len * 0.2f);
            }
        }

        /// <summary>
        /// The thunder shockwave: a dome of air pushed up and out from a strike on the floor (the upper half of an expanding ellipse, brighter on its
        /// rising flanks), thinning as it grows.
        /// </summary>
        public void GaranThunderDome(Vector2 g, float r1, float life, Color col)
        {
            Dyn("ThunderDome", AddHDR(2.2f), Order + 24, life, false, (rib, u) =>
            {
                float e = KMath.Apply(Ease.OutExpo, u);
                float r = Mathf.Lerp(0.1f, r1, e);
                float w = 0.1f * Mathf.Clamp(r1 / 1.4f, 0.5f, 1.4f) * (1f - KMath.Apply(Ease.InQuad, u));
                Color cc = col.WithA(col.a * (1f - u));
                const int seg = 26;
                for (int i = 0; i <= seg; i++)
                {
                    float th = i / (float)seg * Mathf.PI;
                    Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th) * 0.8f);
                    float flank = 0.45f + 0.55f * Mathf.Abs(Mathf.Cos(th));
                    rib.Add(g + d * (r + w), g + d * Mathf.Max(0f, r - w), cc.WithA(0f), cc.WithA(cc.a * flank));
                }
            });
        }

        /// <summary>A scorch where Garan's lightning hit the floor: storm-navy ellipse, a blue glow ring that dies first, dust.</summary>
        public void GaranScorch(Vector2 p, float halfWidth, float life = 0.9f) =>
            TintedScorch(p, halfWidth, life, GaranNight.WithA(0.6f), GaranVolt, GaranDust.WithA(0.45f));

        /// <summary>
        /// A heavy lightning strike onto <paramref name="ground"/> from <paramref name="height"/> metres: a thick main bolt with three forks and two side
        /// strands (main strike, return stroke and after-flicker on different frames), a blue-white contact star, flat rings and a thunder dome off the
        /// floor; with <paramref name="full"/> also glowing cracks racing out, rock and dust thrown, sparks, a scorch and a pale-blue screen flash.
        /// </summary>
        public void GaranThunderStrike(Vector2 ground, float height, float scale, bool crimson, bool full = true)
        {
            Vector2 g = new(ground.x, Mathf.Max(0.02f, ground.y));
            Vector2 top = g + new Vector2(Random.Range(-0.35f, 0.35f), height);
            GaranBolt(top, g, 0.085f * scale, 0.42f, false, 3, 18, height * 0.05f);
            GaranBolt(top + new Vector2(-0.45f * scale, -0.2f), g + new Vector2(-0.12f * scale, 0f), 0.035f * scale, 0.3f, false, 1, 14, height * 0.04f);
            GaranBolt(top + new Vector2(0.4f * scale, -0.35f), g + new Vector2(0.1f * scale, 0f), 0.03f * scale, 0.26f, false, 1, 14, height * 0.04f);
            if (crimson) GaranBolt(top + new Vector2(0.2f, 0f), g, 0.035f * scale, 0.32f, true, 1, 14, height * 0.05f);
            Star(g + Vector2.up * 0.25f, 0.9f * scale, GaranVoltHot, GaranVolt, 0.09f, 8, true, Order + 64);
            Ring(g + Vector2.up * 0.03f, 0.1f, 1.5f * scale, 0.1f * scale, GaranVolt, 0.36f, false, 0.18f, 2.2f);
            Ring(g + Vector2.up * 0.03f, 0.05f, 0.8f * scale, 0.06f, Color.white, 0.2f, false, 0.18f, 1.8f);
            GaranThunderDome(g, 1.25f * scale, 0.32f, GaranVoltHot.WithA(0.85f));
            int sparks = full ? 14 : 8;
            for (int i = 0; i < sparks; i++)
                Emit(_sparks, g + Vector2.up * 0.05f, KMath.Dir(Random.Range(25f, 155f)) * Random.Range(2.5f, 7.5f) * scale, Random.Range(0.025f, 0.05f), Random.Range(0.3f, 0.6f),
                    i % 3 == 0 ? Color.white : GaranVolt);
            if (!full) return;
            GroundCracks(g.x, 6, 0.3f * scale, 1.1f * scale, GaranVoltDeep, GaranVoltHot, 0.9f);
            for (int i = 0; i < Mathf.RoundToInt(10 * scale); i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Chip(g + new Vector2(Random.Range(-0.25f, 0.25f), 0.04f), new Vector2(side * Random.Range(0.6f, 2.8f), Random.Range(2.4f, 5.2f)) * Mathf.Sqrt(scale),
                    Color.Lerp(GaranStone, GaranStoneDark, Random.value), Random.Range(0.05f, 0.12f), Random.Range(0.6f, 0.95f));
            }
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Emit(_dust, g + new Vector2(side * Random.Range(0.1f, 0.4f), 0.06f), new Vector2(side * Random.Range(1.6f, 3.4f), Random.Range(0.05f, 0.35f)),
                    Random.Range(0.28f, 0.42f), Random.Range(0.6f, 0.95f), GaranDust.WithA(0.55f));
            }
            GaranScorch(g, 0.7f * scale, 0.9f);
            GameFeel.Flash(new Color(0.75f, 0.88f, 1f, 0.16f), 0.12f);
        }

        // =========================================================================================== contact

        /// <summary>
        /// A blow of Garan's lands: a blue-white impact star (4 frames), compressed-air discs edge-on across the blow, short bolts bursting out of the
        /// contact, a round thunder ring, speed lines, sparks and blown dust; a heavy blow calls a lightning strike down onto the target's feet and
        /// punches the camera; <paramref name="crimson"/> forks Raikon's crimson out of the contact.
        /// </summary>
        public void GaranConcussion(Vector2 p, Vector2 dir, float s, bool heavy, float floorY, bool crimson)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Star(p, 0.42f * s, GaranVoltHot, GaranVolt, 0.06f, 8, true, Order + 64, 0.55f);
            OrientedRing(p, d, 0.06f, 0.45f * s, 0.07f * s, Color.white, 0.14f, 0.28f, 0.18f, 2.6f);
            OrientedRing(p, d, 0.1f, 0.75f * s, 0.06f * s, GaranVolt, 0.24f, 0.32f, 0.38f, 2.1f);
            GaranVoltBurst(p, d, s, heavy ? 6 : 4, crimson);
            Shards(p, d, 8, 0.7f * s, 0.028f * s, Color.white, GaranVolt, 0.2f, 20f);
            for (int i = 0; i < 4; i++)
                Emit(_dust, p + Random.insideUnitCircle * 0.12f, d * Random.Range(1.5f, 3.2f) + new Vector2(0f, Random.Range(-0.3f, 0.6f)),
                    Random.Range(0.22f, 0.36f) * s, Random.Range(0.4f, 0.65f), GaranDust.WithA(0.5f));
            for (int i = 0; i < 12; i++)
                Emit(_sparks, p, KMath.Dir(KMath.Ang(d) + Random.Range(-40f, 40f)) * Random.Range(4f, 9f) * s, Random.Range(0.025f, 0.045f), Random.Range(0.2f, 0.4f),
                    i % 3 == 0 ? Color.white : GaranVolt);
            if (heavy)
            {
                GaranThunderStrike(new Vector2(p.x + d.x * 0.15f, floorY), 3.2f, 0.7f, crimson, false);
                for (int i = 0; i < 6; i++)
                    Chip(new Vector2(p.x + d.x * 0.25f, floorY + 0.04f) + Vector2.right * Random.Range(-0.3f, 0.3f),
                        new Vector2(d.x * Random.Range(0.5f, 2.5f) + Random.Range(-0.8f, 0.8f), Random.Range(2f, 4.2f)),
                        Color.Lerp(GaranStone, GaranStoneDark, Random.value), Random.Range(0.05f, 0.1f), Random.Range(0.5f, 0.8f));
                GameFeel.Punch(0.1f);
            }
        }

        /// <summary>A blow of Garan's is guarded: the guard dents (a half ring toward him), static crawls over the guard, sparks skate off, the blocker's feet skid up dust.</summary>
        public void GaranGuardDent(Vector2 p, Vector2 dir, float floorY)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Star(p, 0.26f, Color.white, GaranVolt, 0.05f, 4, true, Order + 62, 0.6f);
            OrientedRing(p, d, 0.05f, 0.38f, 0.05f, GaranVolt, 0.16f, 0.22f, 0.06f, 2f);
            GaranCrackle(p, 0.22f, 0.36f, 2);
            for (int i = 0; i < 9; i++)
                Emit(_sparks, p, KMath.Dir(KMath.Ang(-d) + Random.Range(-70f, 70f)) * Random.Range(3f, 6f), 0.03f, Random.Range(0.18f, 0.32f), i % 2 == 0 ? Color.white : GaranVolt);
            for (int i = 0; i < 3; i++)
                Emit(_dust, new Vector2(p.x + d.x * 0.25f, floorY + 0.06f), new Vector2(d.x * Random.Range(0.8f, 1.8f), Random.Range(0.1f, 0.4f)), Random.Range(0.18f, 0.28f),
                    Random.Range(0.4f, 0.6f), GaranDust.WithA(0.45f));
        }

        // =========================================================================================== set pieces

        /// <summary>
        /// A ground slam (Storm Fist, the Shiko stomp, the phase change): a heavy strike into the crater with two side strikes landing either side,
        /// the thunder dome, glowing cracks, rock thrown up and out, a dust wall rolling outward, and static crawling over the floor for a moment;
        /// <paramref name="crimson"/> forks Raikon's lightning across the crater.
        /// </summary>
        public void GaranGroundSlam(Vector2 ground, float halfWidth, bool crimson)
        {
            Vector2 g = new(ground.x, Mathf.Max(0.02f, ground.y));
            float sc = 0.75f + halfWidth * 0.25f;
            GaranThunderStrike(g, 4.2f, sc, crimson);
            GaranBolt(g + new Vector2(-halfWidth * 0.9f - 0.3f, 3.2f), g + new Vector2(-halfWidth * 0.75f, 0f), 0.04f, 0.28f, false, 1, 14);
            GaranBolt(g + new Vector2(halfWidth * 0.9f + 0.3f, 3.0f), g + new Vector2(halfWidth * 0.75f, 0f), 0.04f, 0.26f, false, 1, 14);
            float next = 0f;
            Dyn("GroundStatic", MeshKit.Alpha, Order, 0.4f, false, (rib, u) =>
            {
                if (u * 0.4f < next) return;
                next += 0.06f;
                float x0 = g.x + Random.Range(-halfWidth, halfWidth) * 1.2f;
                GaranArc(new Vector2(x0, g.y + 0.03f), new Vector2(x0 + Random.Range(-0.5f, 0.5f), g.y + Random.Range(0.03f, 0.2f)), 0.1f, 0.014f, crimson && Random.value < 0.4f);
            });
            if (crimson) Bolt(g + new Vector2(-halfWidth * 0.8f, 0.04f), g + new Vector2(halfWidth * 0.8f, 0.04f), 0.026f, 0.24f, Crimson, 9, 0.06f, 1);
        }

        /// <summary>
        /// A fissure racing along the floor from <paramref name="from"/> to <paramref name="to"/> in <paramref name="travel"/> seconds (the Storm Fist's
        /// shock): a jagged crack glowing blue-white that cools to deep blue, static leaping out of it at the racing head, rock spouting and dust kicked
        /// there, and a lightning strike where it ends.
        /// </summary>
        public void GaranFissure(Vector2 from, Vector2 to, float travel, bool crimson)
        {
            var pts = Jagged(from, to, 0.14f, 0.025f);
            float life = travel + 0.8f;
            int emitted = 0;
            bool ended = false;
            System.Action<Ribbon, float, bool> build = (rib, u, glow) =>
            {
                float t = u * life;
                float head = Mathf.Clamp01(t / Mathf.Max(0.01f, travel));
                float cool = Mathf.Clamp01((t - travel * 0.5f) / 0.8f);
                int last = Mathf.FloorToInt(head * (pts.Count - 1));
                if (last < 1) return;
                for (int i = 0; i <= last; i++)
                {
                    float w = glow ? 0.014f : 0.034f;
                    Color c = glow ? Color.Lerp(GaranVoltHot, GaranVoltDeep, cool).WithA(1f - cool) : GaranStoneDark.WithA(0.85f * (1f - u * 0.6f));
                    rib.Add(pts[i] + Vector2.up * w, pts[i] - Vector2.up * w, c, c);
                }
                if (glow) return;
                if (head >= 1f && !ended)
                {
                    ended = true;
                    GaranThunderStrike(to, 3f, 0.6f, crimson, false);
                }
                for (; emitted < last; emitted++)
                {
                    if (emitted % 2 != 0) continue;
                    Vector2 p = pts[emitted];
                    Chip(p, new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(2f, 3.6f)), Color.Lerp(GaranStone, GaranStoneDark, Random.value), Random.Range(0.05f, 0.09f), 0.6f);
                    Emit(_dust, p + Vector2.up * 0.05f, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 0.6f)), 0.25f, 0.6f, GaranDust.WithA(0.5f));
                    Emit(_sparks, p, new Vector2(Random.Range(-1f, 1f), Random.Range(1.5f, 3f)), 0.03f, 0.3f, GaranVolt);
                    if (emitted % 4 == 0) GaranArc(p, p + new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.25f, 0.45f)), 0.1f, 0.016f, crimson && Random.value < 0.4f);
                }
            };
            Dyn("Fissure", MeshKit.Alpha, Order - 6, life, false, (rib, u) => build(rib, u, false));
            Dyn("FissureGlow", AddHDR(2.4f), Order - 5, life, false, (rib, u) => build(rib, u, true));
        }

        /// <summary>The tell of the Shiko line: the floor cracks along the line, glowing faintly blue, a little further each beat, dust trembling up out of it, a spark of static now and then.</summary>
        public void GaranFissureMark(Vector2 a, Vector2 b, float seconds)
        {
            if (seconds <= 0f) return;
            var pts = Jagged(a, b, 0.14f, 0.02f);
            Dyn("FissureMark", AddHDR(1.6f), Order - 6, seconds, false, (rib, u) =>
            {
                int last = Mathf.FloorToInt(KMath.Apply(Ease.OutQuad, Mathf.Clamp01(u / 0.6f)) * (pts.Count - 1));
                float pulse = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(u * 18f));
                for (int i = 0; i <= last; i++)
                {
                    Color c = GaranVolt.WithA((0.25f + 0.5f * u) * pulse);
                    rib.Add(pts[i] + Vector2.up * 0.01f, pts[i] - Vector2.up * 0.01f, c, c);
                }
                if (Random.value < 0.25f)
                    Emit(_dust, Vector2.Lerp(a, b, Random.value) + Vector2.up * 0.03f, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.2f, 0.5f)), 0.12f, 0.4f, GaranDust.WithA(0.35f));
                if (u > 0.5f && Random.value < 0.12f)
                    Emit(_sparks, Vector2.Lerp(a, b, Random.value) + Vector2.up * 0.03f, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 2.4f)), 0.022f, 0.22f, GaranVoltHot);
            });
        }

        /// <summary>The Shiko line strikes: rock spikes punch up out of the crack along its length, lightning leaps up out of the crack between them, rock and dust fly.</summary>
        public void GaranFissureStrike(Vector2 a, Vector2 b)
        {
            float len = (b - a).magnitude;
            int n = Mathf.Max(3, Mathf.CeilToInt(len / 0.3f));
            var h = new float[n + 1]; var lean = new float[n + 1];
            for (int i = 0; i <= n; i++) { h[i] = Random.Range(0.3f, 0.6f); lean[i] = Random.Range(-0.08f, 0.08f); }
            System.Action<Ribbon, float, bool> build = (rib, u, edge) =>
            {
                float rise = KMath.Apply(Ease.OutBack, Mathf.Clamp01(u / 0.18f));
                float sink = KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.45f) / 0.55f));
                for (int i = 0; i <= n; i++)
                {
                    Vector2 bp = Vector2.Lerp(a, b, i / (float)n);
                    float hh = h[i] * rise * (1f - sink);
                    float hw = 0.08f * (1f - sink * 0.5f);
                    if (hh <= 0.01f) continue;
                    Vector2 top = bp + new Vector2(lean[i], hh);
                    if (edge)
                    {
                        Color c = GaranVolt.WithA(0.9f * (1f - u));
                        rib.AddTriangle(bp + Vector2.left * hw * 0.4f, bp + Vector2.right * hw * 0.4f, top, c.WithA(0f), c.WithA(0f), c);
                    }
                    else
                    {
                        Color c = Color.Lerp(GaranStone, GaranStoneDark, (i % 3) / 2f).WithA(0.95f * (1f - sink));
                        rib.AddTriangle(bp + Vector2.left * hw, bp + Vector2.right * hw, top, c, c, c);
                    }
                }
            };
            Dyn("FissureSpikes", MeshKit.Alpha, Order + 34, 0.5f, false, (rib, u) => build(rib, u, false));
            Dyn("FissureSpikeEdge", AddHDR(2f), Order + 35, 0.3f, false, (rib, u) => build(rib, u, true));
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (i + 0.5f) / 3f);
                GaranBolt(p, p + new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.7f, 1.1f)), 0.03f, 0.22f, i == 1, 1, 8);
            }
            for (int i = 0; i < 10; i++)
                Chip(Vector2.Lerp(a, b, Random.value) + Vector2.up * 0.1f, new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(2.4f, 4.4f)), Color.Lerp(GaranStone, GaranStoneDark, Random.value),
                    Random.Range(0.05f, 0.1f), Random.Range(0.55f, 0.85f));
            for (int i = 0; i < 6; i++)
                Emit(_dust, Vector2.Lerp(a, b, Random.value) + Vector2.up * 0.08f, new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 0.8f)), Random.Range(0.25f, 0.4f), 0.75f, GaranDust.WithA(0.5f));
            GaranScorch((a + b) * 0.5f, len * 0.5f + 0.15f, 0.8f);
            GameFeel.Shake(0.22f);
        }

        /// <summary>
        /// The Harite palm: a storm cannon. Three compression discs leave the palm one after another and ride forward as they widen (each edge-on across
        /// the thrust), a thick bolt is driven straight down the blast, speed lines and a jet of dust; <paramref name="heavy"/> (the last palm, the Static
        /// Palm) is bigger, adds a second bolt and static crackling round the palm; <paramref name="crimson"/> turns the second bolt Raikon's.
        /// </summary>
        public void GaranPalmBlast(Vector2 palm, float facing, bool heavy, bool crimson)
        {
            Vector2 d = new(facing, 0f);
            float s = heavy ? 1.3f : 0.85f;
            Star(palm, 0.32f * s, GaranVoltHot, GaranVolt, 0.05f, 6, true, Order + 63, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 c = palm + d * (0.05f + i * 0.12f * s);
                OrientedRing(c, d, 0.06f, (0.32f + i * 0.12f) * s, (0.05f - i * 0.01f) * s, i == 0 ? Color.white : GaranVolt, 0.16f + i * 0.05f, 0.3f, (0.35f + i * 0.2f) * s, 2.3f - i * 0.2f);
            }
            GaranBolt(palm, palm + d * (1.25f * s) + Vector2.up * Random.Range(-0.1f, 0.1f), 0.045f * s, 0.22f, false, 2, 10);
            Shards(palm, d, heavy ? 9 : 6, 0.7f * s, 0.022f, Color.white, GaranVolt, 0.2f, 14f);
            for (int i = 0; i < (heavy ? 6 : 3); i++)
                Emit(_dust, palm + d * 0.2f + Random.insideUnitCircle * 0.1f, d * Random.Range(2.5f, 4.5f) + Random.insideUnitCircle * 0.4f, Random.Range(0.18f, 0.3f), Random.Range(0.35f, 0.55f), GaranDust.WithA(0.42f));
            if (heavy || crimson)
            {
                GaranBolt(palm + Vector2.up * 0.08f, palm + d * (1.0f * s) + Vector2.up * Random.Range(0.1f, 0.3f), 0.03f, 0.2f, crimson, 1, 9);
                GaranCrackle(palm, 0.3f, 0.3f, 3, crimson);
            }
        }

        /// <summary>The Seiken body punch: a tight compression ring at the knuckles, a short bolt cracking forward off them, speed lines.</summary>
        public void GaranSeiken(Vector2 fist, float facing)
        {
            Vector2 d = new(facing, 0f);
            Star(fist, 0.26f, GaranVoltHot, GaranVolt, 0.05f, 4, true, Order + 61, 0.5f);
            OrientedRing(fist, d, 0.04f, 0.3f, 0.04f, Color.white, 0.12f, 0.25f, 0.22f, 2.4f);
            GaranBolt(fist, fist + d * 0.6f + Vector2.up * Random.Range(-0.08f, 0.08f), 0.022f, 0.14f, false, 1, 7);
            Shards(fist, d, 5, 0.5f, 0.02f, Color.white, GaranVolt, 0.14f, 10f);
        }

        /// <summary>The Gedan low kick lands on the shin line: dust skidded forward along the floor, blue sparks that arc and fall, static skittering over the floor.</summary>
        public void GaranLowKick(Vector2 ground, float facing)
        {
            Vector2 g = new(ground.x, Mathf.Max(0f, ground.y) + 0.05f);
            Star(g + new Vector2(facing * 0.2f, 0.2f), 0.28f, GaranVoltHot, GaranVolt, 0.05f, 4, true, Order + 61, 0.5f);
            for (int i = 0; i < 5; i++)
                Emit(_dust, g + Vector2.right * (facing * Random.Range(0f, 0.4f)), new Vector2(facing * Random.Range(1.6f, 3f), Random.Range(0.05f, 0.35f)), Random.Range(0.2f, 0.32f), 0.6f, GaranDust.WithA(0.5f));
            for (int i = 0; i < 7; i++)
                Emit(_sparks, g + new Vector2(facing * 0.25f, 0.2f), new Vector2(facing * Random.Range(1.5f, 4f), Random.Range(0.8f, 2.6f)), 0.03f, Random.Range(0.3f, 0.5f), i % 3 == 0 ? Color.white : GaranVolt);
            GaranArc(g + new Vector2(facing * 0.1f, -0.03f), g + new Vector2(facing * 0.65f, 0f), 0.12f, 0.014f);
        }

        /// <summary>The storm gathering in him: a slow blue-white glow on the chest, sparks drawn into the striking hand, arcs from the chest to the hand, the hand glowing.</summary>
        public void GaranScarCharge(Vector2 chest, Vector2 hand, bool crimson)
        {
            Star(chest, 0.28f, GaranVoltHot, GaranVolt, 0.18f, 6, false, Order + 58, 0.6f);
            Star(hand, 0.22f, GaranVoltHot, GaranVolt, 0.12f, 4, false, Order + 59, 0.5f);
            GaranArc(chest, hand, 0.11f, 0.016f, crimson || Random.value < 0.25f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.7f);
                Emit(_sparks, hand + off, -off / 0.22f, 0.024f, 0.22f, i == 0 ? Color.white : GaranVolt);
            }
        }

        /// <summary>The Shiko leg lift charging: arcs crawl the raised leg, static skitters round the planted foot, and dust is drawn in toward it (pressure gathering).</summary>
        public void GaranShikoCharge(Vector2 knee, Vector2 foot, Vector2 planted)
        {
            GaranArc(knee, foot, 0.11f, 0.016f, Random.value < 0.35f);
            GaranArc(planted + new Vector2(Random.Range(-0.4f, 0.4f), 0.03f), planted + new Vector2(Random.Range(-0.15f, 0.15f), 0.12f), 0.09f, 0.012f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 from = planted + new Vector2(Random.Range(-0.7f, 0.7f), 0.04f);
                Emit(_dust, from, (planted - from) * 2.4f + Vector2.up * 0.1f, 0.14f, 0.4f, GaranDust.WithA(0.35f));
            }
        }

        // =========================================================================================== presence

        /// <summary>
        /// Garan between moves, his electric aura: arcs surge over his body outline, sparks lift off his fists; in Phase 2 the surge is constant, a bolt
        /// climbs from the floor to his shoulders now and then, static skitters round his feet and crimson runs through it.
        /// </summary>
        public void GaranAura(Fighter f, int level)
        {
            Vector2 body = f.rig.World(B.Chest, Vector2.zero);
            GaranCrackle(body, 0.42f, 0.85f, level < 1 ? 1 : 2, level >= 1);
            Vector2 fist = f.rig.World(Random.value < 0.5f ? B.HandN : B.HandF, Vector2.zero);
            Emit(_sparks, fist + Random.insideUnitCircle * 0.08f, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.4f)), 0.022f, 0.24f, Random.value < 0.3f ? Color.white : GaranVolt);
            if (level < 1) return;
            GaranArc(body + Vector2.up * 0.1f, fist, 0.11f, 0.014f, Random.value < 0.5f);
            if (Random.value < 0.3f)
                GaranBolt(f.Pos + new Vector2(Random.Range(-0.35f, 0.35f), 0.02f), f.Pos + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(1.4f, 1.8f)), 0.02f, 0.14f, Random.value < 0.4f, 1, 9);
            if (Random.value < 0.4f)
                GaranArc(f.Pos + new Vector2(Random.Range(-0.6f, 0.6f), 0.03f), f.Pos + new Vector2(Random.Range(-0.6f, 0.6f), 0.08f), 0.1f, 0.012f);
            if (Random.value < 0.3f) Emit(_dust, f.Pos + new Vector2(Random.Range(-0.4f, 0.4f), 0.05f), new Vector2(Random.Range(-0.3f, 0.3f), 0.25f), 0.2f, 0.6f, GaranDust.WithA(0.3f));
        }

        /// <summary>Garan turns "Stormbroken": the storm comes down on him — a great strike and two flanking strikes, the thunder dome, his aura surging with crimson.</summary>
        public void GaranPhaseBurst(Fighter f)
        {
            GaranGroundSlam(f.Pos, 1.2f, true);
            Vector2 chest = f.Pos + Vector2.up * 1.1f;
            GaranThunderDome(f.Pos, 2.2f, 0.45f, GaranVolt);
            GaranCrackle(chest, 0.5f, 0.9f, 4, true);
            GameFeel.Punch(0.12f);
        }

        /// <summary>Points on a jagged line from a to b, every <paramref name="step"/> metres, wobbling up to <paramref name="jitter"/>.</summary>
        static List<Vector2> Jagged(Vector2 a, Vector2 b, float step, float jitter)
        {
            int n = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / step));
            var pts = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
                pts.Add(Vector2.Lerp(a, b, i / (float)n) + new Vector2(0f, (i == 0 || i == n) ? 0f : Random.Range(-jitter, jitter)));
            return pts;
        }
    }

    /// <summary>Garan's combat look: electric trails on his fists and shins, thunder impacts.</summary>
    public sealed class GaranLook : CombatLook
    {
        public override WeaponTrail Trail(Fighter f, TrailSlot slot, Transform parent, int order) =>
            slot is TrailSlot.Fist or TrailSlot.FootN or TrailSlot.FootF ? new GaranSmearTrail(parent, order, f, slot != TrailSlot.Fist) : null;

        public override bool Hit(Fighter a, Vector2 p, Vector2 dir, HitWindow w, bool crit)
        {
            var v = Vfx.I;
            if (v == null) return false;
            bool heavy = w.heavy || crit || w.knockdown;
            v.GaranConcussion(p, dir, heavy ? 1.3f : 1f, heavy, a.Pos.y, PhaseTwo(a) && (heavy || Random.value < 0.35f));
            return true;
        }

        public override bool Block(Fighter a, Vector2 p, Vector2 dir, HitWindow w)
        {
            var v = Vfx.I;
            if (v == null) return false;
            v.GaranGuardDent(p, dir, a.opponent != null ? a.opponent.Pos.y : a.Pos.y);
            return true;
        }
    }

    /// <summary>
    /// A storm trail on a fist or a shin: a storm-navy backing, an electric-blue body whose edges are re-struck every 3 frames (so it crackles instead
    /// of smearing), and a blue-white core only on its leading part; arcs crawl back along the path and sparks fly off the head. Short (0.14 s):
    /// a punch, not a sword. A kick's trail spans the shin (foot tip to below the knee); a punch's is a band of fixed width round the fist.
    /// </summary>
    public sealed class GaranSmearTrail : SampledTrail
    {
        readonly bool _limb;
        readonly float _seed = Random.Range(0f, 100f);

        public GaranSmearTrail(Transform parent, int order, Fighter f, bool limb)
            : base("GaranStormTrail", parent, 0.14f, order, f, MeshKit.Alpha, Vfx.I != null ? Vfx.I.AddHDR(2.2f) : MeshKit.Additive,
                Vfx.I != null ? Vfx.I.AddHDR(3.0f) : MeshKit.Additive)
        {
            _limb = limb;
        }

        protected override void DrawStroke(List<Section> secs, bool live)
        {
            var smear = ribs[0];
            var body = ribs[1];
            var core = ribs[2];
            float now = TestClock != null ? TestClock() : Time.time;
            int strike = Mathf.FloorToInt(now / Vfx.RestrikeSeconds);
            smear.Break(); core.Break();
            for (int half = 0; half < 2; half++)
            {
                body.Break();
                for (int i = 0; i < secs.Count; i++)
                {
                    Edges(secs, i, out var a, out var b);
                    var s = secs[i];
                    float k = 1f - s.age;
                    float ramp = Mathf.Clamp01(s.along * 1.5f);
                    Vector2 across = b - a;
                    Vector2 nA = across.sqrMagnitude > 1e-8f ? across.normalized : Vector2.up;
                    float j0 = (Vfx.Noise(s.t * 31f + strike * 3.7f, _seed) - 0.5f) * 0.08f * k;
                    float j1 = (Vfx.Noise(s.t * 29f + strike * 5.1f, _seed + 9f) - 0.5f) * 0.08f * k;
                    Vector2 a2 = a - nA * (0.02f + j0), b2 = b + nA * (0.02f + j1), mid = (a2 + b2) * 0.5f;
                    Color bc = Color.Lerp(Vfx.GaranVoltDeep, Vfx.GaranVolt, k).WithA(0.85f * Mathf.Pow(k, 1.2f) * ramp);
                    if (half == 0) body.Add(a2, mid, bc.WithA(0f), bc);
                    else body.Add(mid, b2, bc, bc.WithA(0f));
                    if (half != 0) continue;
                    Color dark = Vfx.GaranNight.WithA(0.5f * k * k * ramp);
                    smear.Add(a - nA * 0.05f, b + nA * 0.05f, dark, dark);
                    float lead = Mathf.Clamp01((s.along - 0.55f) / 0.45f) * k;
                    float w = 0.016f * lead + 0.002f;
                    Color hot = Vfx.GaranVoltHot.WithA(lead);
                    core.Add(mid + nA * w, mid - nA * w, hot, hot);
                }
            }
        }

        /// <summary>The two edges of section <paramref name="i"/>: the shin itself for a kick, a band across the fist's travel for a punch.</summary>
        void Edges(List<Section> secs, int i, out Vector2 a, out Vector2 b)
        {
            var s = secs[i];
            if (_limb) { a = s.a; b = s.b; return; }
            Vector2 c = (s.a + s.b) * 0.5f;
            Vector2 next = i + 1 < secs.Count ? (secs[i + 1].a + secs[i + 1].b) * 0.5f : c;
            Vector2 prev = i > 0 ? (secs[i - 1].a + secs[i - 1].b) * 0.5f : c;
            Vector2 t = next - prev;
            Vector2 n = t.sqrMagnitude > 1e-8f ? new Vector2(-t.y, t.x).normalized : Vector2.up;
            float hw = 0.13f * (0.4f + 0.6f * s.along);
            a = c + n * hw; b = c - n * hw;
        }

        protected override void OnMove(Vector2 pa, Vector2 pb, Vector2 a, Vector2 b, float dt)
        {
            var v = Vfx.I;
            if (v == null || dt <= 0f) return;
            Vector2 head = _limb ? a : (a + b) * 0.5f, prev = _limb ? pa : (pa + pb) * 0.5f;
            Vector2 vel = (head - prev) / dt;
            if (vel.sqrMagnitude < 36f) return;
            if (Random.value < 0.5f) v.Spark(head, -vel * 0.15f + Random.insideUnitCircle, Random.value < 0.3f ? Color.white : Vfx.GaranVolt, 0.028f, 0.22f);
            if (Random.value < 0.3f)
            {
                Vector2 back = head - vel.normalized * Random.Range(0.25f, 0.45f) + Random.insideUnitCircle * 0.08f;
                v.GaranArc(head, back, 0.09f, 0.014f, PhaseTwo && Random.value < 0.4f);
            }
        }
    }
}
