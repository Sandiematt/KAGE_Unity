using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hisame, the Rain Courier — "flowing water" (sub-boss III; brief in docs/act1-subbosses-vfx.md). Fast, fluid and precise: her fans cut
    /// crescents of water — a translucent teal body, an aqua highlight, an ice-white edge, and thin strands that peel off and wave as it flows —
    /// that shed droplets from their eroding tails; long water ribbons wave along her dashes and spin round her; she leaves watery afterimages and
    /// slanted rain streaks when she moves at speed; cold mist spreads low and sinks (smoke rises, this does not). Never jagged, never solid:
    /// smooth curves, translucent layers, droplets. Value: ice-white core, aqua highlight, a translucent teal body over a dark slate backing; pale
    /// water and slate mist as matter. Rhythm: the shortest contacts of the four, quick to come and quick to go, with a flowing exit. Phase 2
    /// cracks Raikon's crimson static through the water.
    /// </summary>
    public partial class Vfx
    {
        public static readonly Color HisameIce = KMath.Hex("#F0FEFF");     // core
        public static readonly Color HisameAqua = KMath.Hex("#56E3EC");    // the lit water: additive highlight
        public static readonly Color HisameTide = KMath.Hex("#1B8EA6");    // the water body: translucent alpha
        public static readonly Color HisameSlate = KMath.Hex("#0A2630");   // dark backing behind the water
        public static readonly Color HisameWater = KMath.Hex("#CBF6FF");   // drops (alpha: reads pale on the dark stage)
        public static readonly Color HisameMist = KMath.Hex("#8DB5BE");    // cold mist matter

        // =========================================================================================== water shapes

        /// <summary>
        /// A crescent of water swept along an ellipse round <paramref name="c"/> (radii <paramref name="rx"/>, <paramref name="ry"/>, turned by
        /// <paramref name="rot"/>), from <paramref name="a0"/> through <paramref name="sweep"/> degrees: the head races along the arc, thickest just behind
        /// it, the tail erodes after it and sheds droplets; a slate backing, a translucent teal body, an aqua highlight on the outer side, an ice-white
        /// edge that only lives while fresh, and two thin strands peeling off and waving. It rides <paramref name="travel"/> and widens to
        /// <paramref name="grow"/> × its radius as it goes. <paramref name="crimson"/>: a crimson arc rides it.
        /// </summary>
        public void HisameWaterArc(Vector2 c, float rot, float rx, float ry, float a0, float sweep, float thick, float life, Vector2 travel = default,
            float grow = 1f, bool unscaled = false, int order = Order + 40, bool crimson = false)
        {
            float seed = Random.Range(0f, 100f);
            float dropAt = 0f;
            void Span(float u, out float tail, out float head)
            {
                head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.22f));
                tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.12f) / 0.88f));
            }
            Vector2 Pt(float u, float s, out Vector2 n)
            {
                float e = KMath.Apply(Ease.OutExpo, u);
                float k = Mathf.Lerp(1f, grow, e);
                return Ell(c + travel * e, a0 + sweep * s, rx * k, ry * k, rot, out n);
            }
            float Prof(float x) => Mathf.Pow(Mathf.Clamp01(x), 0.7f) * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01(x), 8f)));
            const int seg = 24;
            // lay: 0 slate backing, 1 teal body, 2 aqua highlight, 3 ice edge, 4 strands
            void Build(Ribbon rib, float u, int lay)
            {
                Span(u, out float tail, out float head);
                if (head - tail <= 0.004f) return;
                float fade = 1f - u * 0.45f;
                if (lay == 4)
                {
                    for (int st = 0; st < 2; st++)
                    {
                        rib.Break();
                        for (int i = 0; i <= seg; i++)
                        {
                            float s = Mathf.Lerp(tail, head, i / (float)seg);
                            float x = (s - tail) / Mathf.Max(0.001f, head - tail);
                            Vector2 p = Pt(u, s, out var n);
                            float off = (st == 0 ? 1f : -0.6f) * thick * (0.35f + 0.6f * (1f - x)) + Mathf.Sin(s * 14f + u * 22f + st * 2f + seed) * 0.025f * (1f - x);
                            float w = 0.009f * Prof(x) * (1f - u);
                            Color cc = Color.Lerp(HisameAqua, HisameIce, 0.4f).WithA(0.55f * (1f - x * 0.3f) * (1f - u));
                            Vector2 q = p + n * off;
                            rib.Add(q + n * w, q - n * w, cc, cc);
                        }
                    }
                    return;
                }
                rib.Break();
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float x = (s - tail) / Mathf.Max(0.001f, head - tail);
                    Vector2 p = Pt(u, s, out var n);
                    float t = thick * Prof(x) * (1f - u * 0.35f);
                    switch (lay)
                    {
                        case 0:
                        {
                            Color cc = HisameSlate.WithA(0.5f * fade * Mathf.Sqrt(Prof(x)));
                            rib.Add(p + n * (t * 0.6f), p - n * (t * 0.75f), cc, cc.WithA(0f));
                            break;
                        }
                        case 1:
                        {
                            Color cc = HisameTide.WithA(0.6f * fade);
                            rib.Add(p + n * (t * 0.48f), p - n * (t * 0.55f), cc, cc.WithA(0f));
                            break;
                        }
                        case 2:
                        {
                            Color cc = HisameAqua.WithA(0.95f * fade * (0.4f + 0.6f * x));
                            rib.Add(p + n * (t * 0.46f), p + n * (t * 0.02f), cc, cc.WithA(0f));
                            break;
                        }
                        default:
                        {
                            float fresh = 1f - Mathf.Clamp01(u / 0.55f);
                            float w = 0.012f + t * 0.06f;
                            Color cc = HisameIce.WithA(fresh * fresh * (0.3f + 0.7f * x));
                            Vector2 e = p + n * (t * 0.48f);
                            rib.Add(e + n * w, e - n * w, cc.WithA(0f), cc);
                            break;
                        }
                    }
                }
                if (lay != 1 || u < 0.15f || u * life < dropAt) return;
                dropAt = u * life + 0.025f;
                Vector2 tp = Pt(u, tail, out var tn);
                Vector2 tg = KMath.Rot(tn, sweep >= 0f ? 90f : -90f);
                Drop(tp + tn * Random.Range(-thick, thick) * 0.4f, tg * Random.Range(1.5f, 3.5f) + tn * Random.Range(-0.5f, 1f), HisameWater.WithA(0.85f),
                    Random.Range(0.025f, 0.05f), Random.Range(0.3f, 0.5f));
            }
            Dyn("WaterBack", MeshKit.Alpha, order - 3, life, unscaled, (rib, u) => Build(rib, u, 0));
            Dyn("WaterBody", MeshKit.Alpha, order - 2, life, unscaled, (rib, u) => Build(rib, u, 1));
            Dyn("WaterLit", AddHDR(1.9f), order - 1, life, unscaled, (rib, u) => Build(rib, u, 2));
            Dyn("WaterEdge", AddHDR(2.8f), order, life * 0.75f, unscaled, (rib, u) => Build(rib, u, 3));
            Dyn("WaterStrands", AddHDR(2.2f), order + 1, life, unscaled, (rib, u) => Build(rib, u, 4));
            if (crimson)
            {
                Vector2 p0 = Ell(c, a0 + sweep * 0.2f, rx, ry, rot, out _), p1 = Ell(c, a0 + sweep * 0.8f, rx, ry, rot, out _);
                Arc(p0, p1, 0.14f, 0.015f, Crimson);
            }
        }

        /// <summary>The circle through <paramref name="lo"/> and <paramref name="hi"/> that bulges out to the <paramref name="side"/> (+1 left of lo→hi): centre, radius, start angle and sweep.</summary>
        static void ArcThrough(Vector2 lo, Vector2 hi, float side, out Vector2 c, out float r, out float a0, out float sweep)
        {
            Vector2 ch = hi - lo;
            float lc = Mathf.Max(0.01f, ch.magnitude);
            Vector2 m = (lo + hi) * 0.5f, n = new Vector2(-ch.y, ch.x) / lc;
            r = lc * 0.72f;
            float h = Mathf.Sqrt(Mathf.Max(0f, r * r - lc * lc * 0.25f));
            c = m - n * (h * side);
            a0 = KMath.Ang(lo - c);
            sweep = Mathf.DeltaAngle(a0, KMath.Ang(hi - c));
        }

        /// <summary>
        /// A long ribbon of water from <paramref name="a"/> to <paramref name="b"/>, waving <paramref name="amp"/> metres either side as a wave runs along it:
        /// it is laid down from a to b quickly, flows, then thins from a; a translucent teal body, an aqua highlight on one side, a thin ice core, droplets
        /// shed off its crests.
        /// </summary>
        public void HisameWaterRibbon(Vector2 a, Vector2 b, float amp, float width, float life, float waves = 1.5f, int order = Order + 28)
        {
            Vector2 d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            Vector2 n = new(-d.y / len, d.x / len);
            float seed = Random.Range(0f, 100f);
            float dropAt = 0f;
            Vector2 P(float s, float u) => a + d * s + n * (amp * Mathf.Sin((waves * s - u * 1.6f) * Mathf.PI * 2f + seed) * Mathf.Sin(s * Mathf.PI));
            void Build(Ribbon rib, float u, int lay)
            {
                float head = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / 0.25f));
                float tail = KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.35f) / 0.65f));
                if (head - tail <= 0.004f) return;
                const int seg = 26;
                rib.Break();
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float x = (s - tail) / Mathf.Max(0.001f, head - tail);
                    Vector2 p = P(s, u);
                    Vector2 tg = P(Mathf.Min(1f, s + 0.01f), u) - P(Mathf.Max(0f, s - 0.01f), u);
                    Vector2 nn = tg.sqrMagnitude > 1e-9f ? new Vector2(-tg.y, tg.x).normalized : n;
                    float w = width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)), 0.6f) *(0.7f + 0.3f * Mathf.Sin(s * 9f + u * 12f + seed)) * (1f - u * 0.4f);
                    float fade = 1f - u * 0.5f;
                    if (lay == 0) { Color cc = HisameTide.WithA(0.55f * fade); rib.Add(p + nn * w, p - nn * w, cc.WithA(0.1f * fade), cc); }
                    else if (lay == 1) { Color cc = HisameAqua.WithA(0.9f * fade); rib.Add(p + nn * w, p + nn * (w * 0.15f), cc, cc.WithA(0f)); }
                    else { Color cc = HisameIce.WithA(0.85f * fade * (1f - x * 0.5f)); rib.Add(p + nn * (w * 0.18f), p - nn * (w * 0.08f), cc, cc); }
                }
                if (lay != 0 || u * life < dropAt) return;
                dropAt = u * life + 0.04f;
                Vector2 q = P(Mathf.Lerp(tail, head, Random.value), u);
                Drop(q, new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.5f, 0.8f)), HisameWater.WithA(0.85f), Random.Range(0.025f, 0.045f), Random.Range(0.3f, 0.45f));
            }
            Dyn("RibbonBody", MeshKit.Alpha, order, life, false, (rib, u) => Build(rib, u, 0));
            Dyn("RibbonLit", AddHDR(2f), order + 1, life, false, (rib, u) => Build(rib, u, 1));
            Dyn("RibbonCore", AddHDR(2.6f), order + 2, life * 0.8f, false, (rib, u) => Build(rib, u, 2));
        }

        /// <summary>
        /// A veil of slanted rain over a box round <paramref name="c"/> (±<paramref name="hw"/>, ±<paramref name="hh"/>): <paramref name="n"/> thin streaks that
        /// fall along <paramref name="slant"/> and shrink away, each with its own start. Her presence in the Downpour and the trail of a fast dash.
        /// </summary>
        public void HisameRainVeil(Vector2 c, float hw, float hh, int n, float life, Vector2 slant, float speed = 5f)
        {
            Vector2 sd = slant.sqrMagnitude > 1e-6f ? slant.normalized : new Vector2(-0.3f, -1f).normalized;
            Vector2 sn = new(-sd.y, sd.x);
            var at = new Vector2[n]; var len = new float[n]; var t0 = new float[n];
            for (int i = 0; i < n; i++)
            {
                at[i] = c + new Vector2(Random.Range(-hw, hw), Random.Range(-hh, hh));
                len[i] = Random.Range(0.3f, 0.6f);
                t0[i] = Random.Range(0f, 0.35f);
            }
            Dyn("RainVeil", AddHDR(2.2f), Order + 26, life, false, (rib, u) =>
            {
                for (int i = 0; i < n; i++)
                {
                    float uu = Mathf.Clamp01((u - t0[i]) / (1f - t0[i]));
                    if (uu <= 0f || uu >= 1f) continue;
                    Vector2 head = at[i] + sd * (speed * life * uu * (1f - t0[i]) * 0.35f);
                    Vector2 tl = head - sd * (len[i] * (1f - uu * 0.6f));
                    float w = 0.01f;
                    Color hc = HisameIce.WithA(0.95f * Mathf.Sqrt(Mathf.Sin(uu * Mathf.PI))), tc = HisameAqua.WithA(0f);
                    rib.AddTriangle(tl + sn * w, tl - sn * w, head, tc, tc, hc);
                    rib.AddTriangle(tl - sn * w, head - sn * w, head, tc, hc, hc);
                }
            });
        }

        // =========================================================================================== contact

        /// <summary>
        /// A blow of Hisame's lands: an ice glint (3 frames), a crescent of water slashed across the contact, a crown of water thrown up and along the
        /// blow that falls, a ripple facing the blow, cold mist that sinks; <paramref name="crimson"/> arcs Raikon's static along the slash.
        /// </summary>
        public void HisameRainCut(Vector2 p, Vector2 dir, float s, bool crimson)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float baseA = KMath.Ang(d);
            Star(p, 0.3f * s, HisameIce, HisameAqua, 0.05f, 4, true, Order + 64, 0.3f);
            float r = 0.55f * s;
            float flip = Random.value < 0.5f ? 1f : -1f;
            HisameWaterArc(p - d * (r * 0.85f), 0f, r, r * 0.85f, baseA - 72f * flip, 144f * flip, 0.18f * s, 0.3f, d * 0.12f, 1.08f, true, Order + 46, crimson);
            OrientedRing(p, d, 0.04f, 0.4f * s, 0.035f, HisameAqua, 0.2f, 0.3f, 0.12f, 2f);
            for (int i = 0; i < 16; i++)
            {
                float ang = baseA + Random.Range(-35f, 70f) * (d.x >= 0f ? 1f : -1f);
                Drop(p, KMath.Dir(ang) * Random.Range(3f, 6.5f) * s, HisameWater.WithA(0.85f), Random.Range(0.03f, 0.06f), Random.Range(0.35f, 0.6f));
            }
            for (int i = 0; i < 5; i++)
                Emit(_sparks, p, KMath.Dir(baseA + Random.Range(-30f, 30f)) * Random.Range(5f, 9f), 0.016f, Random.Range(0.12f, 0.22f), i == 0 ? Color.white : HisameIce);
            for (int i = 0; i < 3; i++)
                MistPuff(p + Random.insideUnitCircle * 0.12f, d * Random.Range(0.4f, 1f) + Vector2.down * 0.2f, HisameMist.WithA(0.24f), Random.Range(0.16f, 0.26f) * s, Random.Range(0.6f, 0.9f));
        }

        /// <summary>A blow of Hisame's is guarded: water splashes back off the guard in a short curl, the fan's iron rib glints, drops spray back.</summary>
        public void HisameGuardSpray(Vector2 p, Vector2 dir)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Star(p, 0.22f, Color.white, HisameAqua, 0.05f, 4, true, Order + 62, 0.5f);
            LensStreak(p, 0.7f, HisameAqua, 0.14f, KMath.Ang(d) + 70f, 0.01f);
            float back = KMath.Ang(-d);
            HisameWaterArc(p + d * 0.28f, 0f, 0.34f, 0.4f, back - 55f, 110f, 0.08f, 0.22f, -d * 0.12f, 1.1f, true, Order + 44);
            for (int i = 0; i < 10; i++)
                Drop(p, KMath.Dir(back + Random.Range(-60f, 60f)) * Random.Range(2f, 4.5f), HisameWater.WithA(0.8f), Random.Range(0.03f, 0.05f), Random.Range(0.3f, 0.45f));
        }

        // =========================================================================================== set pieces

        /// <summary>
        /// A fan's water slash thrown forward: a crescent of water that leaves the fan and travels about a metre, widening as it goes, shedding droplets
        /// from its tail, with rain needles flying with it and mist behind; <paramref name="storm"/>: a crimson arc rides it.
        /// </summary>
        public void HisameGust(Vector2 c, float facing, float tiltDeg, bool storm)
        {
            float baseDeg = facing >= 0f ? tiltDeg : 180f - tiltDeg;
            Vector2 fwd = KMath.Dir(baseDeg);
            Star(c + fwd * 0.3f, 0.24f, HisameIce, HisameAqua, 0.05f, 4, true, Order + 62, 0.35f);
            float flip = facing >= 0f ? 1f : -1f;
            HisameWaterArc(c - fwd * 0.3f, 0f, 0.5f, 0.6f, baseDeg + 66f * flip, -132f * flip, 0.21f, 0.34f, fwd * 1.05f, 1.6f, false, Order + 40, storm);
            HisameRainVeil(c + fwd * 0.7f, 0.5f, 0.45f, 8, 0.28f, fwd + Vector2.down * 0.35f, 9f);
            for (int i = 0; i < 2; i++)
                MistPuff(c + fwd * Random.Range(0.3f, 0.7f), fwd * Random.Range(0.6f, 1.2f), HisameMist.WithA(0.2f), Random.Range(0.18f, 0.26f), 0.8f);
        }

        /// <summary>
        /// The 360° twin-fan spin: two spiral arms of water wheel round her (slate backing, translucent teal body, aqua highlight, ice core), a long water
        /// ribbon wraps round them, water is flung off tangentially, a ring of cold mist spreads low round her feet; <paramref name="crimson"/>: static
        /// crackles in the vortex.
        /// </summary>
        public void HisameVortex(Vector2 c, float facing, bool crimson)
        {
            float life = 0.42f;
            float spin = facing >= 0f ? -1f : 1f;
            void Arms(Ribbon rib, float u, float wMul, Color col, float inner)
            {
                float rot = spin * 560f * KMath.Apply(Ease.OutCubic, u);
                float grow = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.4f));
                float a = col.a * (1f - KMath.Apply(Ease.InQuad, u));
                for (int arm = 0; arm < 2; arm++)
                {
                    rib.Break();
                    const int seg = 24;
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = i / (float)seg;
                        float th = (rot + arm * 180f + spin * s * 210f) * Mathf.Deg2Rad;
                        float r = Mathf.Lerp(0.25f, 1.05f, s) * grow;
                        float w = 0.11f * wMul * Mathf.Sin(s * Mathf.PI) * (1f - u * 0.5f);
                        Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th) * 0.55f);
                        rib.Add(c + d * (r + w), c + d * Mathf.Max(0f, r - w * inner), col.WithA(a * Mathf.Sin(s * Mathf.PI)), col.WithA(0f));
                    }
                }
            }
            Dyn("VortexBack", MeshKit.Alpha, Order + 30, life, false, (rib, u) => Arms(rib, u, 1.7f, HisameSlate.WithA(0.5f), 1f));
            Dyn("VortexBody", MeshKit.Alpha, Order + 32, life, false, (rib, u) => Arms(rib, u, 1.2f, HisameTide.WithA(0.6f), 1f));
            Dyn("VortexLit", AddHDR(2.1f), Order + 34, life, false, (rib, u) => Arms(rib, u, 0.8f, HisameAqua, 0.1f));
            Dyn("VortexCore", AddHDR(2.8f), Order + 35, life * 0.6f, false, (rib, u) => Arms(rib, u, 0.25f, HisameIce, 1f));
            HisameWaterArc(c, 0f, 1.0f, 0.55f, 90f, spin * 300f, 0.12f, 0.4f, default, 1.15f, false, Order + 36, crimson);
            for (int i = 0; i < 16; i++)
            {
                float ang = Random.Range(0f, 360f);
                Vector2 at = c + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad) * 0.55f) * Random.Range(0.4f, 0.95f);
                Vector2 tang = KMath.Dir(ang + 90f * spin) * Random.Range(3f, 5.5f);
                Drop(at, tang + Vector2.up * 0.8f, HisameWater.WithA(0.85f), Random.Range(0.03f, 0.05f), Random.Range(0.35f, 0.55f));
            }
            Vector2 feet = new(c.x, 0.04f);
            Ring(feet, 0.2f, 1.3f, 0.05f, HisameAqua.WithA(0.8f), 0.4f, false, 0.16f, 1.7f);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                MistPuff(feet + new Vector2(side * Random.Range(0.1f, 0.5f), 0.05f), new Vector2(side * Random.Range(1f, 2.2f), 0.05f), HisameMist.WithA(0.26f), Random.Range(0.2f, 0.3f), Random.Range(0.8f, 1.1f));
            }
            if (crimson) Crackle(c, 0.7f, 0.4f, 2, Crimson);
        }

        /// <summary>The closed-fan point strike (Atemi): a pinpoint glint, three needle-thin streaks forward, a jet of water off the point.</summary>
        public void HisameAtemi(Vector2 tip, float facing)
        {
            Star(tip, 0.22f, HisameIce, HisameAqua, 0.05f, 4, true, Order + 62, 0.3f);
            for (int i = 0; i < 3; i++)
                LensStreak(tip + new Vector2(facing * 0.2f, (i - 1) * 0.05f), 0.5f + i * 0.08f, i == 1 ? HisameIce : HisameAqua, 0.1f, facing >= 0f ? 0f : 180f, 0.008f);
            HisameWaterRibbon(tip - new Vector2(facing * 0.1f, 0f), tip + new Vector2(facing * 0.75f, 0.02f), 0.04f, 0.05f, 0.2f, 1f, Order + 44);
            for (int i = 0; i < 6; i++)
                Drop(tip, new Vector2(facing * Random.Range(3.5f, 6f), Random.Range(-0.6f, 1f)), HisameWater.WithA(0.8f), 0.03f, 0.35f);
        }

        /// <summary>
        /// The tell of the Inazuma slash: the rain hangs in the air along the line she will cut, each drop a short pale streak trembling a little
        /// more each frame, and a faint line between them that brightens toward the cut.
        /// </summary>
        public void HisameSuspendedRain(Vector2 lo, Vector2 hi, float seconds)
        {
            if (seconds <= 0f) return;
            const int n = 14;
            var at = new Vector2[n];
            for (int i = 0; i < n; i++) at[i] = Vector2.Lerp(lo, hi, (i + Random.value * 0.6f) / n) + Random.insideUnitCircle * 0.09f;
            Dyn("SuspendedRain", AddHDR(1.8f), Order + 30, seconds, false, (rib, u) =>
            {
                float show = Mathf.Clamp01(u / 0.3f);
                for (int i = 0; i < n; i++)
                {
                    if (i / (float)n > show * 1.2f) continue;
                    Vector2 p = at[i] + Random.insideUnitCircle * (0.004f + 0.012f * u);
                    Color c = HisameWater.WithA(0.5f + 0.4f * u);
                    rib.AddTriangle(p + new Vector2(-0.009f, 0.045f), p + new Vector2(0.009f, 0.045f), p - new Vector2(0f, 0.05f), c, c, c.WithA(0f));
                }
                Vector2 d = hi - lo, nn = new Vector2(-d.y, d.x).normalized * 0.006f;
                Color line = Color.Lerp(HisameAqua, Crimson, u * u).WithA(0.12f + 0.35f * u * u);
                rib.Break();
                rib.Add(lo + nn, lo - nn, line.WithA(0f), line.WithA(0f));
                rib.Add((lo + hi) * 0.5f + nn, (lo + hi) * 0.5f - nn, line, line);
                rib.Add(hi + nn, hi - nn, line.WithA(0f), line.WithA(0f));
            });
        }

        /// <summary>The Inazuma slash detonates: the hanging rain is cut by one great crescent of water with an ice-white line and a crimson bolt down it; the drops burst outward and fall.</summary>
        public void HisameRainCleave(Vector2 lo, Vector2 hi)
        {
            Vector2 d = (hi - lo).normalized;
            Star((lo + hi) * 0.5f, 0.38f, HisameIce, HisameAqua, 0.06f, 4, true, Order + 64, 0.3f);
            ArcThrough(lo - d * 0.2f, hi + d * 0.2f, 1f, out var c, out float r, out float a0, out float sweep);
            HisameWaterArc(c, 0f, r, r, a0, sweep, 0.17f, 0.36f, default, 1.04f, false, Order + 46, false);
            Needle(lo - d * 0.25f, hi + d * 0.25f, 0.018f, HisameAqua, HisameIce, 0.22f);
            Bolt(lo, hi, 0.026f, 0.22f, Crimson, 9, 0.08f, 1);
            Vector2 n = new(-d.y, d.x);
            for (int i = 0; i < 18; i++)
            {
                Vector2 p = Vector2.Lerp(lo, hi, Random.value);
                Drop(p, n * Random.Range(-4f, 4f) + d * Random.Range(-1f, 1f), HisameWater.WithA(0.85f), Random.Range(0.03f, 0.05f), Random.Range(0.35f, 0.55f));
            }
            GameFeel.Shake(0.18f);
        }

        /// <summary>The smoke bomb bursts: a bank of cold mist rolls out low along the floor in three values (dark, mid, pale rim), rain falls through it.</summary>
        public void HisameMistScreen(Vector2 pos, float s, bool crimson)
        {
            Vector2 g = new(pos.x, Mathf.Max(0.05f, pos.y));
            Star(g + Vector2.up * 0.2f, 0.32f * s, Color.white, HisameAqua, 0.06f, 6, true, Order + 58);
            Ring(new Vector2(g.x, 0.03f), 0.08f, 1.2f * s, 0.05f, HisameAqua.WithA(0.7f), 0.34f, false, 0.18f, 1.7f);
            for (int i = 0; i < 18; i++)
            {
                float k = (i - 8.5f) / 8.5f, up = 1f - Mathf.Abs(k);
                Color col = i % 3 == 0 ? HisamePalette.SmokeDark.WithA(0.72f) : i % 3 == 1 ? HisamePalette.Smoke.WithA(0.62f) : HisameMist.WithA(0.45f);
                MistPuff(g + new Vector2(k * 0.5f * s, 0.12f + up * 0.3f), new Vector2(k * 2.2f * s, (0.15f + up * 0.45f) * s), col, (0.4f + up * 0.3f) * s, Random.Range(1.0f, 1.4f));
            }
            HisameRainVeil(g + new Vector2(0f, 1.1f), 0.8f * s, 0.6f, 14, 0.45f, new Vector2(0.25f, -1f), 7f);
            if (crimson) Crackle(g + Vector2.up * 0.5f, 0.6f, 0.4f, 3, Crimson);
        }

        /// <summary>A landing out of the Kocho vault: a ripple of water at her feet, a low splash crown, a ring of cold mist, a few drops shaken off.</summary>
        public void HisameLanding(Vector2 feet, float facing)
        {
            Ring(feet + Vector2.up * 0.03f, 0.1f, 0.75f, 0.035f, HisameAqua.WithA(0.6f), 0.3f, false, 0.18f, 1.5f);
            Ring(feet + Vector2.up * 0.03f, 0.05f, 0.45f, 0.025f, HisameIce.WithA(0.7f), 0.22f, false, 0.18f, 1.6f);
            for (int i = -1; i <= 1; i++)
                MistPuff(feet + new Vector2(i * 0.22f, 0.06f), new Vector2(i * 1.1f, 0.05f), HisameMist.WithA(0.4f), 0.26f, 0.8f);
            for (int i = 0; i < 10; i++)
                Drop(feet + new Vector2(Random.Range(-0.25f, 0.25f), 0.05f), KMath.Dir(Random.Range(55f, 125f)) * Random.Range(1.8f, 3.2f), HisameWater.WithA(0.8f), 0.03f, 0.4f);
        }

        /// <summary>Lightning Cut's crouch: crimson static and a few sparks thrown up off the fans.</summary>
        public void HisameCrackle(Vector2 pos)
        {
            Arc(pos + Random.insideUnitCircle * 0.14f, pos + Random.insideUnitCircle * 0.18f, 0.1f, 0.013f, Crimson);
            for (int i = 0; i < 3; i++)
                Emit(_sparks, pos + new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(-0.1f, 0.2f)), new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.6f)), 0.03f, 0.22f,
                    i == 0 ? Color.white : HisameAqua);
        }

        /// <summary>A glint tell: a thin four-point star and a lens streak in <paramref name="c"/>.</summary>
        public void HisameGlint(Vector2 pos, Color c, float streak)
        {
            Star(pos, 0.28f, Color.white, c, 0.14f, 4, true, Order + 66, 0.3f);
            LensStreak(pos, streak, c, 0.18f, -18f, 0.011f);
        }

        static float _lastDashRibbon = -10f;

        /// <summary>
        /// A watery afterimage of her as she dashes: a teal-lit snapshot left behind, slanted rain streaks streaming off the line of the dash, a water
        /// ribbon laid along it (at most one every 0.15 s, so a burst of cues leaves one ribbon), drops shaken off.
        /// </summary>
        public void HisameWaterGhost(Fighter f)
        {
            if (f == null || f.rig == null) return;
            Ghost(f.rig, HisameAqua, 0.3f);
            Vector2 back = new(-f.facing, 0f);
            HisameRainVeil(f.Pos + new Vector2(-f.facing * 0.7f, 1.0f), 0.7f, 0.75f, 12, 0.26f, back * 1.6f + Vector2.down * 0.6f, 9f);
            float now = Time.time;
            if (now - _lastDashRibbon > 0.15f || now < _lastDashRibbon)
            {
                _lastDashRibbon = now;
                HisameWaterRibbon(f.Pos + new Vector2(-f.facing * 1.5f, 0.85f), f.Pos + new Vector2(f.facing * 0.1f, 1.0f), 0.12f, 0.09f, 0.4f, 1.3f);
            }
            for (int i = 0; i < 4; i++)
                Drop(f.Pos + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1.5f)), new Vector2(-f.facing * 1.5f, -1f), HisameWater.WithA(0.75f), 0.03f, 0.35f);
        }

        // =========================================================================================== presence

        /// <summary>
        /// Hisame between moves: water runs off her mino cape and a few rain streaks fall round her; in Phase 2 the rain thickens to a downpour round her,
        /// a water ribbon curls round her now and then and crimson static jumps between the two fans.
        /// </summary>
        public void HisameAura(Fighter f, int level)
        {
            Vector2 shoulder = f.rig.World(B.ArmNU, Vector2.zero);
            Puff(shoulder, new Vector2(-f.facing * 0.34f, -0.12f), HisameWater.WithA(0.26f), 0.19f, 0.72f);
            Drop(shoulder, new Vector2(Random.Range(-0.2f, 0.2f), -0.5f), HisameWater.WithA(0.75f), 0.03f, 0.45f);
            if (Random.value < (level < 1 ? 0.35f : 1f))
                HisameRainVeil(f.Pos + new Vector2(0f, 1.3f), 1.1f, 0.7f, level < 1 ? 4 : 10, 0.4f, new Vector2(-f.facing * 0.3f, -1f), 7f);
            if (level < 1) return;
            Vector2 fanF = f.rig.World(B.Sword, f.BladeTipLocal * 0.85f), fanN = f.rig.World(B.HandN, f.NearBladeTipLocal * 0.85f);
            Arc(fanF, fanN, 0.09f, 0.012f, Random.value < 0.6f ? Crimson : HisameAqua);
            if (Random.value < 0.2f)
            {
                float side = Random.value < 0.5f ? 1f : -1f;
                HisameWaterRibbon(f.Pos + new Vector2(-0.6f * side, 0.3f), f.Pos + new Vector2(0.6f * side, 1.7f), 0.18f, 0.06f, 0.6f, 1.2f, Order - 8);
            }
        }

        /// <summary>Hisame turns to "Downpour": a mist screen bursts round her, the water vortex wheels once, two ribbons rise round her, the rain comes down, crimson static in it.</summary>
        public void HisamePhaseBurst(Fighter f)
        {
            HisameMistScreen(f.Pos, 1.1f, true);
            HisameVortex(f.Pos + Vector2.up * 1.0f, f.facing, true);
            HisameWaterRibbon(f.Pos + new Vector2(-0.9f, 0.2f), f.Pos + new Vector2(0.5f, 2.3f), 0.25f, 0.09f, 0.8f, 1.4f);
            HisameWaterRibbon(f.Pos + new Vector2(0.9f, 0.2f), f.Pos + new Vector2(-0.5f, 2.3f), 0.25f, 0.09f, 0.8f, 1.4f);
            HisameRainVeil(f.Pos + new Vector2(0f, 1.4f), 1.8f, 1.1f, 26, 0.7f, new Vector2(-0.3f, -1f), 8f);
        }
    }

    /// <summary>Hisame's combat look: water trails on both fans, water slashes on contact.</summary>
    public sealed class HisameLook : CombatLook
    {
        public override WeaponTrail Trail(Fighter f, TrailSlot slot, Transform parent, int order) =>
            slot is TrailSlot.Blade or TrailSlot.BladeN ? new HisameFanTrail(parent, order, f) : null;

        public override bool Hit(Fighter a, Vector2 p, Vector2 dir, HitWindow w, bool crit)
        {
            var v = Vfx.I;
            if (v == null) return false;
            v.HisameRainCut(p, dir, w.heavy || crit ? 1.3f : 1f, PhaseTwo(a) || crit);
            return true;
        }

        public override bool Block(Fighter a, Vector2 p, Vector2 dir, HitWindow w)
        {
            var v = Vfx.I;
            if (v == null) return false;
            v.HisameGuardSpray(p, dir);
            return true;
        }
    }

    /// <summary>
    /// A war fan's cut of water along its real path: a crescent on the fan's outer ribs (dark slate backing, translucent teal body, aqua highlight,
    /// an ice-white edge while fresh) that narrows toward the edge as it ages, with two thin strands peeling off the outer edge and waving as the water
    /// flows away; sheds droplets off the tip and a little cold mist that sinks. Gone in 0.2 s.
    /// </summary>
    public sealed class HisameFanTrail : SampledTrail
    {
        public HisameFanTrail(Transform parent, int order, Fighter f)
            : base("HisameFanTrail", parent, 0.2f, order, f, MeshKit.Alpha, MeshKit.Alpha,
                Vfx.I != null ? Vfx.I.AddHDR(1.9f) : MeshKit.Additive, Vfx.I != null ? Vfx.I.AddHDR(2.8f) : MeshKit.Additive) { }

        protected override void DrawStroke(List<Section> secs, bool live)
        {
            var back = ribs[0]; var body = ribs[1]; var lit = ribs[2]; var edge = ribs[3];
            back.Break(); body.Break(); lit.Break(); edge.Break();
            for (int i = 0; i < secs.Count; i++)
            {
                var s = secs[i];
                float k = 1f - s.age;
                float ramp = Mathf.Clamp01(s.along * 3f);
                float inner = Mathf.Lerp(0.45f, 0.86f, s.age);
                Color slate = Vfx.HisameSlate.WithA(0.45f * k * ramp);
                back.Add(Vector2.LerpUnclamped(s.a, s.b, inner - 0.08f), Vector2.LerpUnclamped(s.a, s.b, 1.1f), slate.WithA(0f), slate);
                Color tide = Vfx.HisameTide.WithA(0.55f * Mathf.Pow(k, 1.2f) * ramp);
                body.Add(Vector2.LerpUnclamped(s.a, s.b, inner), Vector2.LerpUnclamped(s.a, s.b, 1.04f), tide.WithA(0f), tide);
                Color aqua = Vfx.HisameAqua.WithA(Mathf.Pow(k, 1.3f) * ramp);
                lit.Add(Vector2.LerpUnclamped(s.a, s.b, Mathf.Lerp(inner, 1.02f, 0.45f)), Vector2.LerpUnclamped(s.a, s.b, 1.02f), aqua.WithA(0f), aqua);
                float hot = Mathf.Clamp01(1f - s.age / 0.5f);
                Color ic = Vfx.HisameIce.WithA(hot * hot * ramp);
                edge.Add(Vector2.LerpUnclamped(s.a, s.b, 0.95f), Vector2.LerpUnclamped(s.a, s.b, 1.03f), ic.WithA(0f), ic);
            }
            // strands of water peeling off the outer edge as it ages, waving
            for (int st = 0; st < 2; st++)
            {
                lit.Break();
                for (int i = 0; i < secs.Count; i++)
                {
                    var s = secs[i];
                    if (s.age < 0.2f) { lit.Break(); continue; }
                    float k = 1f - s.age;
                    float off = 1.06f + (0.1f + 0.12f * st) * s.age + 0.03f * Mathf.Sin(s.t * 45f + st * 2.1f);
                    Vector2 p = Vector2.LerpUnclamped(s.a, s.b, off);
                    Vector2 across = (s.b - s.a).sqrMagnitude > 1e-8f ? (s.b - s.a).normalized : Vector2.up;
                    float w = 0.008f * k;
                    Color c = Vfx.HisameAqua.WithA(0.85f * k * Mathf.Clamp01(s.along * 3f));
                    lit.Add(p + across * w, p - across * w, c, c);
                }
            }
        }

        protected override void OnMove(Vector2 pa, Vector2 pb, Vector2 a, Vector2 b, float dt)
        {
            var v = Vfx.I;
            if (v == null || dt <= 0f) return;
            Vector2 vel = (b - pb) / dt;
            if (vel.sqrMagnitude < 25f) return;
            v.Drop(b, vel * 0.3f + Vector2.down * 1.2f, Vfx.HisameWater.WithA(0.8f), Random.Range(0.025f, 0.04f), 0.35f);
            if (Random.value < 0.2f) v.MistPuff(b, vel * 0.08f + Vector2.down * 0.2f, Vfx.HisameMist.WithA(0.25f), 0.22f, 0.8f);
            if (PhaseTwo && Random.value < 0.1f) v.Arc(Vector2.Lerp(a, b, 0.6f), b, 0.07f, 0.011f, Vfx.Crimson);
        }
    }

    /// <summary>Hisame's Shigure Fan Gust in flight: three water crescents wheeling round a cold core, droplets and mist shed behind.</summary>
    public sealed class RainGustLook : MonoBehaviour
    {
        Transform _proj;
        float _dir, _t, _dead, _drop;
        Vector2 _pos;
        Ribbon _rim, _body, _lit, _core;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || Vfx.I == null) return;
            var go = new GameObject("RainGustLook");
            go.transform.SetParent(Vfx.I.transform, false);
            var l = go.AddComponent<RainGustLook>();
            l._proj = a.follow;
            l._pos = a.follow.position;
            l._dir = a.dir.x >= 0f ? 1f : -1f;
            int o = Vfx.Order + 32;
            l._rim = new Ribbon("RainGustRim", go.transform, MeshKit.Alpha, o);
            l._body = new Ribbon("RainGustBody", go.transform, MeshKit.Alpha, o + 1);
            l._lit = new Ribbon("RainGustLit", go.transform, Vfx.I.AddHDR(2.1f), o + 2);
            l._core = new Ribbon("RainGustCore", go.transform, Vfx.I.AddHDR(2.8f), o + 3);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_proj != null) _pos = _proj.position;
            else { _dead += dt; if (_dead > 0.14f) { Destroy(gameObject); return; } }
            float k = _proj != null ? 1f : 1f - _dead / 0.14f;
            _rim.Begin(); _body.Begin(); _lit.Begin(); _core.Begin();
            float rot = -_dir * _t * 900f;
            for (int arc = 0; arc < 3; arc++)
            {
                _rim.Break(); _body.Break(); _lit.Break(); _core.Break();
                const int seg = 14;
                for (int i = 0; i <= seg; i++)
                {
                    float s = i / (float)seg;
                    float th = (rot + arc * 120f + s * 100f) * Mathf.Deg2Rad;
                    Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th));
                    float prof = Mathf.Pow(s, 0.7f) * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(s, 8f)));
                    float r = 0.3f;
                    Color slate = Vfx.HisameSlate.WithA(0.5f * prof * k);
                    _rim.Add(_pos + d * (r + 0.08f * prof), _pos + d * (r - 0.07f * prof), slate.WithA(0f), slate);
                    Color tide = Vfx.HisameTide.WithA(0.6f * prof * k);
                    _body.Add(_pos + d * (r + 0.06f * prof), _pos + d * (r - 0.05f * prof), tide, tide.WithA(0f));
                    Color aqua = Vfx.HisameAqua.WithA(prof * k);
                    _lit.Add(_pos + d * (r + 0.055f * prof), _pos + d * r, aqua, aqua.WithA(0f));
                    Color ic = Vfx.HisameIce.WithA(prof * k);
                    _core.Add(_pos + d * (r + 0.062f * prof), _pos + d * (r + 0.045f * prof), ic.WithA(0f), ic);
                }
            }
            _rim.End(); _body.End(); _lit.End(); _core.End();
            if (_proj == null || Vfx.I == null) return;
            _drop += dt;
            if (_drop < 0.03f) return;
            _drop = 0f;
            Vfx.I.Drop(_pos + Random.insideUnitCircle * 0.15f, new Vector2(-_dir * 1.5f, -1.5f), Vfx.HisameWater.WithA(0.8f), Random.Range(0.025f, 0.04f), 0.35f);
            if (Random.value < 0.35f) Vfx.I.MistPuff(_pos, new Vector2(-_dir * 0.4f, -0.15f), Vfx.HisameMist.WithA(0.28f), 0.2f, 0.7f);
        }
    }

    /// <summary>The smoke bomb in flight: a black ball with a pale rim, a burning fuse and a thin smoke trail.</summary>
    public sealed class SmokeBombLook : MonoBehaviour
    {
        Transform _proj;
        float _trail;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || Vfx.I == null) return;
            var go = new GameObject("SmokeBombLook");
            go.transform.SetParent(a.follow, false);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<SmokeBombLook>().Init(a.follow);
        }

        void Init(Transform proj)
        {
            _proj = proj;
            int o = Vfx.Order + 30;
            MeshKit.MakeRenderer("Rim", transform, MeshKit.Disc(Vector2.zero, 0.105f, HisamePalette.Pale, HisamePalette.Pale), MeshKit.Alpha, o);
            MeshKit.MakeRenderer("Ball", transform, MeshKit.Disc(Vector2.zero, 0.09f, HisamePalette.RainLit, HisamePalette.RainDeep), MeshKit.Alpha, o + 1);
            MeshKit.MakeRenderer("Fuse", transform, MeshKit.Star(6, 0.07f, 0.025f, Color.white, KMath.Hex("#FFB25A")), Vfx.I.AddHDR(2.4f), o + 2)
                .transform.localPosition = new Vector3(0.05f, 0.11f, 0f);
        }

        void Update()
        {
            if (_proj == null || Vfx.I == null) return;
            _trail += Time.deltaTime;
            if (_trail < 0.035f) return;
            _trail = 0f;
            Vfx.I.Puff((Vector2)_proj.position, new Vector2(0f, 0.3f), HisamePalette.Smoke, 0.1f, 0.4f);
        }
    }
}
