using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Lightning Ascendant expansion's effects (docs/raikon-ascendant-vfx.md, "Effect briefs" and "VFX implementation notes"), on the effekseer-vfx
    /// skill's craft rules like the rest of the procedural <see cref="Vfx"/>: every effect has a time structure (anticipation, contact, body, secondary,
    /// dissipation), value layering (a white core, a saturated body, a blood-black alpha rim or residue), a body that changes shape (it sweeps open,
    /// erodes, is re-struck), and eased particles. Phase 1's are steel, air shear, dust and embers only (the pale shear colour, never a tell's star or
    /// streak); phase 2's add ground lightning and static; phase 3's call the storm.
    /// </summary>
    public partial class Vfx
    {
        /// <summary>Phase 1's air: pale shear #EEF1F6, never above 70 % alpha (blade arcs, air cones, wind blades).</summary>
        public static readonly Color Shear = new(0.933f, 0.945f, 0.965f, 1f);

        static Vector2 Quad(Vector2 a, Vector2 c, Vector2 b, float t) { float s = 1f - t; return s * s * a + 2f * s * t * c + t * t * b; }

        static Vector2 QuadTangent(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            Vector2 d = Quad(a, c, b, Mathf.Min(1f, t + 0.01f)) - Quad(a, c, b, Mathf.Max(0f, t - 0.01f));
            return d.sqrMagnitude > 1e-10f ? d.normalized : (b - a).normalized;
        }

        // =========================================================================================
        //  PHASE 1: STEEL, AIR SHEAR, DUST, EMBERS
        // =========================================================================================
        /// <summary>
        /// A pale blade crescent along the curve a → ctrl → b (Sheathe Cross's draws): it sweeps open in 3 frames with a white leading edge and the tail
        /// erodes after it, over a blood-black underlay; two thin crimson air-shear ribbons peel off and run on past it; embers drift off the end.
        /// </summary>
        public void DrawArc(Vector2 a, Vector2 ctrl, Vector2 b, float life = 0.36f)
        {
            void Build(Ribbon rib, float u, float wMul, Color col, float alpha)
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.14f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.25f) / 0.75f));      // it holds a beat open before it erodes, so the X reads
                if (head - tail < 0.01f) return;
                const int seg = 24;
                for (int i = 0; i <= seg; i++)
                {
                    float k = i / (float)seg;
                    float s = Mathf.Lerp(tail, head, k);
                    Vector2 p = Quad(a, ctrl, b, s), tg = QuadTangent(a, ctrl, b, s), n = new(-tg.y, tg.x);
                    float w = 0.15f * wMul * Mathf.Sin(Mathf.Lerp(0.15f, 1f, k) * Mathf.PI * 0.5f) * (1f - 0.45f * u);
                    Color cc = col.WithA(col.a * alpha * (0.25f + 0.75f * k) * (1f - u * u));
                    rib.Add(p + n * w, p - n * w * 0.35f, cc.WithA(0f), cc);
                }
            }
            Dyn("DrawArcUnder", MeshKit.Alpha, Order + 18, life * 1.2f, false, (rib, u) => Build(rib, u, 1.7f, BloodBlack, 0.55f));
            Dyn("DrawArcBody", AddHDR(2.2f), Order + 22, life, false, (rib, u) => Build(rib, u, 1f, Shear, 0.9f));
            Dyn("DrawArcEdge", AddHDR(3.2f), Order + 23, life * 0.6f, false, (rib, u) => Build(rib, u, 0.22f, Color.white, 1f));

            // the air shear: two hair-thin crimson ribbons beside the arc that run on past its end and thin out
            var pts = new List<Vector2>();
            foreach (float off in new[] { 0.14f, -0.11f })
            {
                Dyn("AirShear", AddHDR(2.4f), Order + 21, life * 1.15f, false, (rib, u) =>
                {
                    float head = 1.18f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.3f)), tail = 1.1f * KMath.Apply(Ease.InQuad, u);
                    if (head - tail < 0.02f) return;
                    pts.Clear();
                    for (int i = 0; i <= 16; i++)
                    {
                        float s = Mathf.Lerp(tail, head, i / 16f);
                        Vector2 tg = QuadTangent(a, ctrl, b, Mathf.Clamp01(s));
                        pts.Add(Quad(a, ctrl, b, s) + new Vector2(-tg.y, tg.x) * off);
                    }
                    PolyStrip(rib, pts, 0.006f, 0.022f * (1f - u), Crimson.WithA(0.9f * (1f - u)), true);
                });
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 tg = QuadTangent(a, ctrl, b, 1f);
                Emit(_embers, Quad(a, ctrl, b, Random.Range(0.7f, 1f)), tg * Random.Range(0.4f, 1.2f) + new Vector2(0f, Random.Range(-0.6f, 0.1f)), 0.035f, Random.Range(0.35f, 0.6f), Crimson);
            }
        }

        /// <summary>The backstep's kick: dust off the front foot rolling the way he came from, and a few low clods.</summary>
        public void BackstepDust(Vector2 feet, float face)
        {
            Dust(feet + Vector2.right * face * 0.25f, 0.75f, face);
            for (int i = 0; i < 3; i++)
                Emit(_dust, feet + new Vector2(face * Random.Range(0.1f, 0.4f), 0.04f), new Vector2(face * Random.Range(0.6f, 1.6f), Random.Range(0.1f, 0.5f)), Random.Range(0.14f, 0.22f), Random.Range(0.4f, 0.6f), Ash.WithA(0.5f));
        }

        /// <summary>
        /// Tsuba Press's compressed air: pale lines converge on the hilt (the squeeze), then burst forward in a cone, and a ring opens at the cone's mouth.
        /// No star: the white four-point star is the steel tell's.
        /// </summary>
        public void AirCone(Vector2 hilt, float face, float life = 0.26f)
        {
            const int n = 7;
            var ang = new float[n];
            for (int i = 0; i < n; i++) ang[i] = Mathf.Lerp(-28f, 28f, i / (n - 1f)) + Random.Range(-4f, 4f);
            Dyn("AirCone", AddHDR(2.4f), Order + 24, life, false, (rib, u) =>
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 d = KMath.Dir(face > 0f ? ang[i] : 180f - ang[i]), nrm = new(-d.y, d.x);
                    Vector2 h, tl;
                    float w, a;
                    if (u < 0.35f)
                    {
                        float k = KMath.Apply(Ease.InQuad, u / 0.35f);
                        float r = Mathf.Lerp(0.85f, 0.06f, k);
                        h = hilt + d * r; tl = hilt + d * (r + 0.34f);
                        w = 0.016f; a = 0.45f + 0.35f * k;
                    }
                    else
                    {
                        float k = KMath.Apply(Ease.OutExpo, (u - 0.35f) / 0.65f);
                        tl = hilt + d * (0.1f + 1.2f * k); h = tl + d * Mathf.Lerp(0.55f, 0.2f, k);
                        w = 0.026f * (1f - k); a = 0.85f * (1f - k);
                    }
                    rib.AddTriangle(tl + nrm * w, tl - nrm * w, h, Shear.WithA(0f), Shear.WithA(0f), Shear.WithA(a));
                    rib.AddTriangle(tl + nrm * w, h, tl - nrm * w, Shear.WithA(a * 0.6f), Shear.WithA(a), Shear.WithA(a * 0.6f));
                }
            });
            After(life * 0.35f, () => { if (I != null) I.Ring(hilt + Vector2.right * face * 0.5f, 0.08f, 0.4f, 0.045f, Shear.WithA(0.8f), 0.2f, false, 2.4f, 2.2f); });
        }

        /// <summary>The shove connected: a steel ring at the guard and warm metal sparks thrown back and up, drooping under gravity.</summary>
        public void RingSpark(Vector2 p, float face)
        {
            Ring(p, 0.05f, 0.42f, 0.035f, Steel, 0.18f, false, 1f, 2.2f);
            var metal = new Color(1f, 0.86f, 0.7f, 1f);
            for (int i = 0; i < 11; i++)
            {
                float a = face > 0f ? Random.Range(95f, 200f) : Random.Range(-20f, 85f);
                Emit(_sparks, p, KMath.Dir(a) * Random.Range(2f, 5.2f), Random.Range(0.02f, 0.03f), Random.Range(0.22f, 0.4f), i % 3 == 0 ? Color.white : metal);
            }
            for (int i = 0; i < 3; i++) Emit(_embers, p, new Vector2(-face * Random.Range(0.3f, 0.9f), Random.Range(0.2f, 0.9f)), 0.035f, 0.4f, metal);
        }

        /// <summary>
        /// The cleave's heavy vertical wind-split: a white-cored line along the drop that thins at once, then the air parts: two pale lines bow outward
        /// from it and fade; dust blows out both ways and deeper cracks run from the foot (ember glow, no bolts: phase 1).
        /// </summary>
        public void WindSplit(Vector2 top, Vector2 ground)
        {
            Dyn("WindSplitCore", AddHDR(2.6f), Order + 26, 0.18f, false, (rib, u) =>
            {
                float w = 0.022f * (1f - KMath.Apply(Ease.InQuad, u));
                Color c = Color.Lerp(Color.white, Shear, u).WithA(1f - u);
                rib.Add(top + Vector2.right * w * 0.4f, top - Vector2.right * w * 0.4f, c.WithA(0f), c.WithA(0f));
                rib.Add(Vector2.Lerp(top, ground, 0.5f) + Vector2.right * w, Vector2.Lerp(top, ground, 0.5f) - Vector2.right * w, c, c);
                rib.Add(ground + Vector2.right * w, ground - Vector2.right * w, c, c);
            });
            var pts = new List<Vector2>();
            for (int side = -1; side <= 1; side += 2)
            {
                float sd = side;
                Dyn("WindSplitPart", AddHDR(1.5f), Order + 24, 0.45f, false, (rib, u) =>
                {
                    float bow = 0.3f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.4f));
                    pts.Clear();
                    for (int i = 0; i <= 12; i++)
                    {
                        float t = i / 12f;
                        pts.Add(Vector2.Lerp(top, ground, t) + Vector2.right * sd * bow * Mathf.Sin(t * Mathf.PI));
                    }
                    PolyStrip(rib, pts, 0.004f, 0.014f, Shear.WithA(0.6f * (1f - u)), true);
                });
            }
            Dust(ground, 1.1f, 1f);
            Dust(ground, 1.1f, -1f);
            GroundCracks(ground, 1.1f, 4, 0.9f);
        }

        /// <summary>
        /// Whirlwind Feint's cancelled swing: a wide half-arc of pale shear in three broken pieces over the top, eroding from both ends, with red embers
        /// peeling off it and drifting back. It must never read as a landed cut.
        /// </summary>
        public void FeintSmear(Vector2 c, float face, float radius = 1.15f, float life = 0.42f)
        {
            Vector2 At(float t) { float th = Mathf.Lerp(160f, 20f, t); if (face < 0f) th = 180f - th; Vector2 d = KMath.Dir(th); return c + new Vector2(d.x * radius, d.y * radius * 0.55f); }
            float[] lo = { 0f, 0.36f, 0.7f }, hi = { 0.28f, 0.62f, 1f };
            void Build(Ribbon rib, float u, float wMul, Color col, float alpha)
            {
                float e = 0.5f * KMath.Apply(Ease.InQuad, u);
                for (int k = 0; k < 3; k++)
                {
                    float a0 = Mathf.Max(lo[k], e), a1 = Mathf.Min(hi[k], 1f - e);
                    if (a1 - a0 < 0.01f) continue;
                    rib.Break();
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = Mathf.Lerp(a0, a1, i / 8f);
                        Vector2 p = At(t), n = (p - c).normalized;
                        float w = 0.1f * wMul * (1f - u) * Mathf.Sin(Mathf.Lerp(0.1f, 0.9f, i / 8f) * Mathf.PI);
                        Color cc = col.WithA(col.a * alpha * (1f - u));
                        rib.Add(p + n * w, p - n * w, cc.WithA(0f), cc);
                    }
                }
            }
            Dyn("FeintSmearUnder", MeshKit.Alpha, Order + 20, life, false, (rib, u) => Build(rib, u, 1.6f, BloodBlack, 0.45f));
            Dyn("FeintSmear", AddHDR(2f), Order + 22, life, false, (rib, u) => Build(rib, u, 1f, Shear, 0.8f));
            for (int i = 0; i < 18; i++)
                Emit(_embers, At(Random.value), new Vector2(-face * Random.Range(0.2f, 0.9f), Random.Range(0.1f, 0.8f)), 0.06f, Random.Range(0.55f, 0.9f), i % 4 == 0 ? HotWhite : Crimson);
        }

        /// <summary>The sidestep's skid: dirt thrown low and back, and a dark streak on the floor that fades.</summary>
        public void Skid(Vector2 feet, float face)
        {
            for (int i = 0; i < 5; i++)
                Emit(_dust, feet + new Vector2(Random.Range(-0.2f, 0.2f), 0.04f), new Vector2(-face * Random.Range(0.8f, 1.8f), Random.Range(0.1f, 0.4f)), Random.Range(0.16f, 0.26f), Random.Range(0.4f, 0.55f), Ash.WithA(0.45f));
            Vector2 a = feet - Vector2.right * face * 0.7f, b = feet;
            var dirt = new Color(0.18f, 0.13f, 0.12f, 1f);
            Dyn("Skid", MeshKit.Alpha, Order - 28, 0.7f, false, (rib, u) =>
            {
                float al = 0.4f * (1f - u);
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    Vector2 p = Vector2.Lerp(a, b, t) + Vector2.up * 0.01f;
                    float w = 0.018f * Mathf.Sin(t * Mathf.PI);
                    rib.Add(p + Vector2.up * w, p - Vector2.up * w, dirt.WithA(al), dirt.WithA(al));
                }
            });
        }

        /// <summary>
        /// The feint's thrust: a needle line (far over 40:1) that shoots forward from the blade in 2 frames, holds, and thins; one-sided and tapered to
        /// the point (not the eye tell's symmetric streak), white core then crimson; three sparks off the point.
        /// </summary>
        public void NeedleFlash(Vector2 from, float face, float length = 1.7f, float life = 0.22f)
        {
            void Build(Ribbon rib, float u, float wMul, Color col)
            {
                float reach = length * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.12f));
                float w = 0.02f * wMul * (1f - KMath.Apply(Ease.InQuad, u));
                Color c = col.WithA(col.a * (1f - u * 0.8f));
                Vector2 tip = from + Vector2.right * face * reach;
                rib.AddTriangle(from + Vector2.up * w, from - Vector2.up * w, tip, c, c, c.WithA(0f));
            }
            Dyn("NeedleBody", AddHDR(2.4f), Order + 56, life, false, (rib, u) => Build(rib, u, 1f, Crimson));
            Dyn("NeedleCore", AddHDR(3f), Order + 57, life * 0.5f, false, (rib, u) => Build(rib, u, 0.35f, Color.white));
            for (int i = 0; i < 3; i++)
                Emit(_sparks, from + Vector2.right * face * length, new Vector2(face * Random.Range(2.5f, 5f), Random.Range(-0.6f, 0.6f)), 0.025f, 0.2f, i == 0 ? Color.white : Crimson);
        }

        // =========================================================================================
        //  PHASE 2: STATIC, GROUND LIGHTNING, TETHERS, WIND BLADES
        // =========================================================================================
        static readonly Vector2 CrossArmA = new(0.75f, 0.33f), CrossArmB = new(0.75f, -0.35f);

        /// <summary>
        /// The Static Cross's linger: the X of the two draws stays, dim crimson, re-struck every 4 frames; its flicker and the arcs crawling along its arms
        /// rise towards the snap. It must never look like the snap.
        /// </summary>
        public void StaticCrossMark(Vector2 c, float seconds)
        {
            if (seconds <= 0f) return;
            var jagA = new Jag(c - CrossArmA, c + CrossArmA, 10, 0.025f, 0, Random.Range(1, 1 << 20));
            var jagB = new Jag(c - CrossArmB, c + CrossArmB, 10, 0.025f, 0, Random.Range(1, 1 << 20));
            float arcAt = 0f;
            void Cross(Ribbon rib, float u, float w, Color col, float alpha)
            {
                int s = Mathf.FloorToInt(u * seconds / 0.067f);
                jagA.Strike(s); jagB.Strike(s);
                float flick = (0.45f + 0.4f * u + Random.Range(-0.06f, 0.06f)) * alpha;
                PolyStrip(rib, jagA.main, w, w, col.WithA(col.a * flick), true);
                PolyStrip(rib, jagB.main, w, w, col.WithA(col.a * flick), true);
            }
            Dyn("StaticCrossRim", MeshKit.Alpha, Order + 38, seconds, false, (rib, u) => Cross(rib, u, 0.07f, BloodBlack, 0.6f));
            Dyn("StaticCrossMark", AddHDR(2.4f), Order + 40, seconds, false, (rib, u) =>
            {
                Cross(rib, u, 0.032f, Crimson, 1f);
                if (u * seconds >= arcAt)
                {
                    arcAt += Mathf.Lerp(0.09f, 0.035f, u);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector2 arm = k == 0 ? CrossArmA : CrossArmB;
                        float t0 = Random.Range(-1f, 0.6f);
                        Arc(c + arm * t0, c + arm * (t0 + Random.Range(0.25f, 0.45f)) + Random.insideUnitCircle * 0.08f, 0.09f, 0.016f);
                    }
                    if (Random.value < 0.5f) Emit(_sparks, c + CrossArmA * Random.Range(-1f, 1f), Random.insideUnitCircle * 1.5f, 0.025f, 0.2f, Crimson);
                }
            });
        }

        /// <summary>The Static Cross snaps: a white pop at the crossing, short arcs out along the four arms, a small ring, sparks; then ash.</summary>
        public void StaticCrossSnap(Vector2 c)
        {
            Star(c, 0.95f, Color.white, Crimson, 0.12f, 8, true, Order + 62);
            foreach (var arm in new[] { CrossArmA, -CrossArmA, CrossArmB, -CrossArmB })
                Bolt(c, c + arm * Random.Range(1.2f, 1.6f), 0.045f, 0.24f, Crimson, 8, 0.08f, 1);
            Ring(c, 0.05f, 0.95f, 0.07f, Crimson, 0.26f, false, 1f, 2.4f);
            for (int i = 0; i < 12; i++)
                Emit(_sparks, c, Random.insideUnitCircle.normalized * Random.Range(2f, 5f), 0.03f, Random.Range(0.25f, 0.4f), i % 3 == 0 ? Color.white : Crimson);
            for (int i = 0; i < 3; i++)
                Emit(_dust, c + Random.insideUnitCircle * 0.3f, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.2f, 0.5f)), 0.18f, 0.6f, Ash.WithA(0.4f));
            GameFeel.Shake(0.2f);
        }

        /// <summary>The cleave's V: two diagonal floor lines from in front of the blade fanning forward (one nearer the camera, one further), both
        /// telegraphed by the dim crawling crack of the floor spark line.</summary>
        public void VeeMark(Vector2 apex, float face, float length, float seconds)
        {
            SparkLineMark(apex, apex + new Vector2(face * length, -0.14f), seconds);
            SparkLineMark(apex, apex + new Vector2(face * length, 0.18f), seconds);
        }

        /// <summary>The V erupts: both lines strike as floor spark lines.</summary>
        public void VeeStrike(Vector2 apex, float face, float length)
        {
            SparkLineStrike(apex, apex + new Vector2(face * length, -0.14f));
            SparkLineStrike(apex, apex + new Vector2(face * length, 0.18f));
        }

        /// <summary>
        /// One step of Fulmination Drag's rooster tail (every 2 frames): sparks thrown back and up off the dragged tip (eased: fast, then they hang and
        /// fall), the glowing crimson gouge from <paramref name="fromX"/> to the tip over a blood-black residue that outlives it, and, when
        /// <paramref name="microBolt"/>, a micro-bolt hopping off the gouge.
        /// </summary>
        public void RoosterTail(Vector2 tip, float floorY, float face, float fromX, bool microBolt)
        {
            Vector2 at = new(tip.x, floorY + 0.03f);
            for (int i = 0; i < 5; i++)
                Emit(_sparks, at, new Vector2(-face * Random.Range(3f, 7f), Random.Range(1.5f, 4.5f)), Random.Range(0.028f, 0.045f), Random.Range(0.3f, 0.55f),
                    i % 3 == 0 ? Color.white : i % 3 == 1 ? Crimson : HotWhite);
            Vector2 a = new(fromX, floorY + 0.012f), b = new(tip.x, floorY + 0.012f);
            Dyn("Gouge", AddHDR(1.8f), Order - 12, 0.7f, false, (rib, u) =>
            {
                float w = 0.022f * (1f - 0.6f * u);
                Color c = Color.Lerp(HotWhite, Crimson, Mathf.Clamp01(u / 0.2f)).WithA(Mathf.Pow(1f - u, 1.5f));
                rib.Add(a + Vector2.up * w, a - Vector2.up * w, c, c);
                rib.Add(b + Vector2.up * w, b - Vector2.up * w, c, c);
            });
            Dyn("GougeResidue", MeshKit.Alpha, Order - 30, 1.1f, false, (rib, u) =>
            {
                float w = 0.03f;
                Color c = BloodBlack.WithA(0.5f * (1f - u));
                rib.Add(a + Vector2.up * w, a - Vector2.up * w, c, c);
                rib.Add(b + Vector2.up * w, b - Vector2.up * w, c, c);
            });
            if (microBolt)
            {
                float x = Mathf.Lerp(fromX, tip.x, Random.value);
                Arc(new Vector2(x, floorY + 0.02f), new Vector2(x - face * Random.Range(0.05f, 0.15f), floorY + Random.Range(0.18f, 0.35f)), 0.09f, 0.012f);
            }
        }

        /// <summary>The flick's upward fan: a star at the tip, then seven jagged lightning needles fanning up over 3 frames, biased forward; sparks up.</summary>
        public void NeedleFan(Vector2 p, float face)
        {
            Star(p, 0.6f, Color.white, Crimson);
            for (int i = 0; i < 7; i++)
            {
                float ang = 40f + i * 14f + Random.Range(-4f, 4f);
                if (face < 0f) ang = 180f - ang;
                float len = Random.Range(0.8f, 1.8f);
                Vector2 end = p + KMath.Dir(ang) * len;
                After(i * 0.008f, () => { if (I != null) I.Bolt(p, end, 0.022f, 0.2f, Crimson, 6, len * 0.09f, 0); });
            }
            for (int i = 0; i < 10; i++)
                Emit(_sparks, p, KMath.Dir(face > 0f ? Random.Range(45f, 120f) : Random.Range(60f, 135f)) * Random.Range(3f, 7f), 0.03f, Random.Range(0.3f, 0.5f), i % 3 == 0 ? Color.white : Crimson);
        }

        /// <summary>A gouge pillar: a light column and a bolt rising out of the floor (a quick rising streak first), a star at its foot, sparks up, a scorch.</summary>
        public void GougePillar(Vector2 ground, float height = 3f)
        {
            Vector2 top = ground + new Vector2(Random.Range(-0.15f, 0.15f), height);
            Dyn("PillarRise", AddHDR(3f), Order + 57, 0.12f, false, (rib, u) =>
            {
                float h = height * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.5f));
                float w = 0.06f * (1f - u);
                Color c = Color.white.WithA(1f - u);
                rib.Add(ground + Vector2.right * w, ground - Vector2.right * w, c, c);
                rib.Add(ground + new Vector2(w * 0.3f, h), ground + new Vector2(-w * 0.3f, h), c.WithA(0f), c.WithA(0f));
            });
            LightPillar(ground, top, 0.3f, 0.4f, new Color(1f, 0.35f, 0.4f, 0.9f));
            Bolt(top, ground, 0.06f, 0.32f, Crimson, 12, 0.16f, 2);
            Star(ground + Vector2.up * 0.25f, 0.6f, Color.white, Crimson);
            for (int i = 0; i < 10; i++)
                Emit(_sparks, ground + Vector2.up * Random.Range(0.1f, height * 0.5f), new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 4f)), 0.03f, Random.Range(0.3f, 0.5f), i % 2 == 0 ? Color.white : Crimson);
            Scorch(ground, 0.45f, 0.8f);
        }

        /// <summary>
        /// Arc Anchor's thrown tachi: the blade spinning inside a crimson-white buzzsaw halo while it flies (it rides <paramref name="follow"/>), planted in
        /// the floor and humming once it lands, and a flickering static tether from his far hand to it the whole time (dim, no white core: a strike's
        /// value never). It goes when <paramref name="keep"/> says (the blink, the recall, the move ending).
        /// </summary>
        public void AnchorBlade(Fighter owner, Transform follow, Vector2 start, float face, float groundY, System.Func<bool> keep)
        {
            if (owner == null) return;
            var go = new GameObject("AnchorBlade");
            go.transform.SetParent(transform, false);
            var fx = new AnchorFx
            {
                go = go, life = 3f, vfx = this, owner = owner, follow = follow, keep = keep, at = start, face = face, groundY = groundY, seed = Random.Range(1, 1 << 20),
                rim = new Ribbon("TetherRim", go.transform, MeshKit.Alpha, Order + 44),
                tether = new Ribbon("Tether", go.transform, AddHDR(2f), Order + 46),
                halo = new Ribbon("Halo", go.transform, AddHDR(2.4f), Order + 52),
                blade = new Ribbon("Blade", go.transform, AddHDR(2f), Order + 53),
            };
            _live.Add(fx);
            fx.Tick(0f);
        }

        class AnchorFx : Fx
        {
            public Vfx vfx; public Fighter owner; public Transform follow; public System.Func<bool> keep;
            public Ribbon rim, tether, halo, blade;
            public Vector2 at; public float face, groundY; public int seed;
            float _spin, _restrike;
            int _strike;
            bool _planted;
            readonly List<Vector2> _pts = new();

            public override void Tick(float u)
            {
                if (owner == null || owner.rig == null || keep == null || !keep()) { t = life; Clear(); return; }
                float dt = Time.deltaTime;
                bool flying = follow != null;
                if (flying) at = follow.position;
                else if (!_planted) { _planted = true; at = new Vector2(at.x, groundY + 0.42f); }
                _spin += dt * (flying ? 1500f : 0f);
                _restrike += dt;
                bool struck = false;
                if (_restrike >= RestrikeSeconds) { _restrike = 0f; _strike++; struck = true; }

                float bladeAng = flying ? _spin : (face > 0f ? 118f : 62f);       // planted: tip in the floor, hilt up and back
                Vector2 d = KMath.Dir(bladeAng);
                Vector2 hilt = flying ? at : at + d * 0.45f;
                Vector2 hand = owner.rig.World(B.HandF, Vector2.zero);

                // the tether: a long jagged line re-struck every 3 frames, flickering dim
                var r = new System.Random(seed * 31 + _strike);
                float F() => (float)r.NextDouble();
                float len = (hilt - hand).magnitude;
                int segs = Mathf.Clamp(Mathf.RoundToInt(len / 0.25f), 6, 40);
                Vector2 dd = hilt - hand, nn = dd.sqrMagnitude > 1e-8f ? new Vector2(-dd.y, dd.x).normalized : Vector2.up;
                _pts.Clear();
                for (int i = 0; i <= segs; i++)
                {
                    float tt = i / (float)segs, pin = Mathf.Sin(tt * Mathf.PI);
                    _pts.Add(Vector2.Lerp(hand, hilt, tt) + nn * ((F() - 0.5f) * 0.12f * pin));
                }
                float flick = 0.35f + 0.25f * F();
                rim.Begin(); PolyStrip(rim, _pts, 0.04f, 0.04f, BloodBlack.WithA(0.4f), true); rim.End();
                tether.Begin(); PolyStrip(tether, _pts, 0.016f, 0.016f, Crimson.WithA(flick), true); tether.End();

                // the halo: three broken arcs spinning round the flying blade, re-struck; once planted, a faint hum
                halo.Begin();
                if (flying)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float start = _spin * 0.5f + k * 120f, sweep = 70f + 40f * F();
                        halo.Break();
                        for (int i = 0; i <= 10; i++)
                        {
                            float th = (start + sweep * i / 10f) * Mathf.Deg2Rad, rr = 0.42f + (F() - 0.5f) * 0.06f;
                            Vector2 q = at + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * rr;
                            float w = 0.04f * Mathf.Sin(i / 10f * Mathf.PI);
                            Vector2 rd = (q - at).normalized;
                            halo.Add(q + rd * w, q - rd * w, Crimson.WithA(0f), Color.Lerp(Crimson, HotWhite, 0.3f));
                        }
                    }
                }
                halo.End();
                if (!flying && struck && _strike % 3 == 0) vfx.Arc(at + Random.insideUnitCircle * 0.2f, at + d * 0.3f + Random.insideUnitCircle * 0.2f, 0.08f, 0.011f);

                // the blade itself (the rig's is hidden while it is thrown)
                blade.Begin();
                Vector2 b0 = at - d * 0.5f, b1 = at + d * 0.5f, bn = new(-d.y, d.x);
                blade.Add(b0 + bn * 0.022f, b0 - bn * 0.022f, Steel, Steel);
                blade.Add(b1 + bn * 0.008f, b1 - bn * 0.008f, Steel.WithA(0.6f), Steel.WithA(0.6f));
                blade.End();
            }

            void Clear()
            {
                rim.Begin(); rim.End(); tether.Begin(); tether.End(); halo.Begin(); halo.End(); blade.Begin(); blade.End();
            }
        }

        /// <summary>The thrown tachi lands: impact sparks thrown up and back, a small ring, dust and a scorch where it sticks.</summary>
        public void AnchorLand(Vector2 at, float groundY, float face)
        {
            Vector2 g = new(at.x, groundY);
            for (int i = 0; i < 12; i++)
                Emit(_sparks, g + Vector2.up * 0.1f, KMath.Dir(face > 0f ? Random.Range(95f, 165f) : Random.Range(15f, 85f)) * Random.Range(2.5f, 6f), 0.03f, Random.Range(0.25f, 0.45f), i % 3 == 0 ? Color.white : Crimson);
            Ring(g + Vector2.up * 0.03f, 0.05f, 0.55f, 0.05f, Crimson, 0.25f, false, 0.22f, 2f);
            Dust(g, 0.6f, -face);
            Scorch(g, 0.3f, 0.6f);
        }

        /// <summary>The blink along the tether: crimson-white ghosts strung from where he stood to the hilt, and a dim streak along the line.</summary>
        public void TetherGhosts(FighterRig rig, Vector2 from, Vector2 to)
        {
            if (rig == null) return;
            for (int i = 1; i <= 5; i++)
            {
                float k = i / 6f;
                Ghost(rig, Color.Lerp(Crimson, HotWhite, 0.45f), 0.16f + 0.03f * i, (from - to) * k);
            }
            Bolt(from + Vector2.up * 1.0f, to + Vector2.up * 1.0f, 0.03f, 0.16f, Crimson, 14, 0.08f, 0, false, Order + 50, 1f, 0f);
        }

        /// <summary>A deflected blade recalled: the tether yanks it back to the hand (a dim streak) and sparks at the catch.</summary>
        public void AnchorRecall(Vector2 from, Vector2 hand)
        {
            Bolt(from, hand, 0.03f, 0.18f, Crimson, 12, 0.1f, 0, false, Order + 50, 1f, 0f);
            for (int i = 0; i < 6; i++)
                Emit(_sparks, hand, Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f), 0.025f, 0.25f, i % 2 == 0 ? Color.white : Crimson);
        }

        /// <summary>The extraction slash: a ring of broken crimson slash arcs round him that re-strikes as it widens, two crossing slice lines, sparks out.</summary>
        public void RadialSlash(Vector2 ground)
        {
            Vector2 c = ground + Vector2.up * 1.0f;
            WheelArcs(c, 1.3f, 0.3f);
            SlashLine(ground + new Vector2(-1.3f, 0.5f), ground + new Vector2(1.3f, 1.6f), Crimson, Color.white, 0.24f, 0.04f);
            SlashLine(ground + new Vector2(-1.3f, 1.6f), ground + new Vector2(1.3f, 0.5f), Crimson, Color.white, 0.24f, 0.04f);
            Ring(ground + Vector2.up * 0.03f, 0.2f, 1.5f, 0.08f, Crimson, 0.3f, false, 0.25f, 2.2f);
            Crackle(c, 1.1f, 0.6f, 3);
            for (int i = 0; i < 16; i++)
                Emit(_sparks, c, KMath.Dir(i * 22.5f + Random.Range(-8f, 8f)) * Random.Range(3f, 6.5f), 0.032f, Random.Range(0.25f, 0.45f), i % 3 == 0 ? Color.white : Crimson);
            GameFeel.Shake(0.35f);
        }

        /// <summary>The shapes a wind crescent draws: 0 the tall vacuum blade, 1 the low crescent, 2 the X.</summary>
        public enum CrescentKind { Vacuum, Low, X }

        /// <summary>
        /// Gale Sever's wind blades in flight, riding <paramref name="follow"/>: a translucent pale crescent (alpha blended, so it reads as air, not
        /// light), crimson static crawling on its rim and re-struck every 3 frames, and twin dust wakes skimming the floor under it.
        /// </summary>
        public void WindCrescent(Transform follow, Vector2 start, float face, float groundY, CrescentKind kind)
        {
            var go = new GameObject("WindCrescent");
            go.transform.SetParent(transform, false);
            var fx = new CrescentFx
            {
                go = go, life = 3f, follow = follow, at = start, face = face, groundY = groundY, kind = kind, vfx = this, seed = Random.Range(1, 1 << 20),
                body = new Ribbon("CrescentBody", go.transform, MeshKit.Alpha, Order + 30),
                rim = new Ribbon("CrescentRim", go.transform, AddHDR(2.2f), Order + 32),
            };
            _live.Add(fx);
            fx.Tick(0f);
        }

        class CrescentFx : Fx
        {
            public Vfx vfx; public Transform follow; public Ribbon body, rim;
            public Vector2 at; public float face, groundY; public CrescentKind kind; public int seed;
            float _restrike, _wake;
            int _strike;
            readonly List<Vector2> _pts = new();

            public override void Tick(float u)
            {
                if (follow == null) { t = life; body.Begin(); body.End(); rim.Begin(); rim.End(); return; }
                at = follow.position;
                float dt = Time.deltaTime;
                _restrike += dt;
                if (_restrike >= RestrikeSeconds) { _restrike = 0f; _strike++; }
                var r = new System.Random(seed * 17 + _strike);
                body.Begin(); rim.Begin();
                switch (kind)
                {
                    case CrescentKind.Vacuum: Draw(r, at + new Vector2(-face * 0.12f, -0.62f), at + new Vector2(-face * 0.12f, 0.62f), 0.3f, 0.09f); break;
                    case CrescentKind.Low: Draw(r, new Vector2(at.x - face * 0.15f, groundY + 0.02f), new Vector2(at.x - face * 0.15f, groundY + 0.5f), 0.45f, 0.07f); break;
                    default:
                        foreach (float tilt in new[] { 35f, -35f })
                        {
                            Vector2 dd = KMath.Dir(90f + tilt) * 0.55f;
                            Draw(r, at - dd, at + dd, 0.22f, 0.07f);
                        }
                        break;
                }
                body.End(); rim.End();

                _wake -= dt;
                if (_wake <= 0f)
                {
                    _wake = 0.05f;
                    for (int k = 0; k < 2; k++)
                        Emit(vfx._dust, new Vector2(at.x - face * (0.1f + 0.3f * k), groundY + 0.03f), new Vector2(-face * Random.Range(0.3f, 0.9f), Random.Range(0.1f, 0.35f)),
                            Random.Range(0.16f, 0.26f), Random.Range(0.4f, 0.6f), Ash.WithA(0.4f));
                }
            }

            /// <summary>One crescent from a to b bulging <paramref name="bulge"/> forward: the pale translucent body, then the static on its outer rim.</summary>
            void Draw(System.Random r, Vector2 a, Vector2 b, float bulge, float width)
            {
                Vector2 mid = (a + b) * 0.5f, ctrl = mid + Vector2.right * face * bulge * 2f;
                const int seg = 14;
                body.Break();
                _pts.Clear();
                for (int i = 0; i <= seg; i++)
                {
                    float s = i / (float)seg;
                    Vector2 p = Quad(a, ctrl, b, s), tg = QuadTangent(a, ctrl, b, s), n = new(-tg.y, tg.x);
                    if (Vector2.Dot(n, Vector2.right * face) < 0f) n = -n;            // n points out of the bulge
                    float w = width * Mathf.Sin(s * Mathf.PI);
                    body.Add(p + n * w, p - n * w * 0.6f, Shear.WithA(0.55f), Shear.WithA(0.18f));
                    _pts.Add(p + n * (w + ((float)r.NextDouble() - 0.5f) * 0.05f));
                }
                PolyStrip(rim, _pts, 0.012f, 0.026f, Crimson, true);                     // the crackling red static on the rim, re-struck every 3 frames
                for (int i = 0; i < _pts.Count; i++) _pts[i] = Quad(a, ctrl, b, i / (float)seg) + (_pts[i] - Quad(a, ctrl, b, i / (float)seg)) * 0.55f;
                PolyStrip(rim, _pts, 0.003f, 0.008f, Color.white.WithA(0.75f), false);   // the thin white leading edge inside it
            }
        }

        /// <summary>A wind crescent ends: it dissolves where it stopped, a puff of pale shear and a few crimson sparks.</summary>
        public void CrescentEnd(Vector2 at)
        {
            Ring(at, 0.1f, 0.5f, 0.03f, Shear.WithA(0.4f), 0.18f, false, 1.6f, 1.4f);
            for (int i = 0; i < 5; i++) Emit(_sparks, at, Random.insideUnitCircle.normalized * Random.Range(1f, 2.5f), 0.022f, 0.2f, Crimson);
        }

        /// <summary>Faint wind streaks at the screen's edges, blowing the way the wind blades fly (a wide swing's air across the whole view).</summary>
        public void ScreenWind(float face, Fighter owner = null)
        {
            var cam = ViewCam(owner);
            if (cam == null) return;
            var view = ViewRect(cam);
            for (int i = 0; i < 6; i++)
            {
                bool left = i < 3;
                float x = left ? view.xMin + Random.Range(0.2f, 1.4f) : view.xMax - Random.Range(0.2f, 1.4f);
                float y = Mathf.Lerp(view.yMin, view.yMax, Random.Range(0.25f, 0.85f)), len = Random.Range(1.1f, 2f), sx = x;
                Dyn("ScreenWind", AddHDR(1.4f), Order + 70, 0.25f, false, (rib, u) =>
                {
                    float hx = sx + face * len * KMath.Apply(Ease.OutExpo, u);
                    Color c = Shear.WithA(0.25f * Mathf.Sin(u * Mathf.PI));
                    rib.AddTriangle(new Vector2(hx - face * len, y + 0.006f), new Vector2(hx - face * len, y - 0.006f), new Vector2(hx, y), c.WithA(0f), c.WithA(0f), c);
                });
            }
        }

        /// <summary>
        /// The X-crescent's burning static residue along the floor (<paramref name="nearX"/> to <paramref name="farX"/>): crimson embers ignite behind the
        /// crescent as it passes, small arcs crawl on it for <paramref name="seconds"/>, and it cools to blood black.
        /// </summary>
        public void StaticResidue(float nearX, float farX, float floorY, float seconds, float sweepSpeed = 14f)
        {
            float span = Mathf.Abs(farX - nearX);
            float arcAt = 0f;
            Dyn("ResidueGlow", AddHDR(1.6f), Order - 11, seconds, false, (rib, u) =>
            {
                float lit = Mathf.Clamp01(u * seconds * sweepSpeed / Mathf.Max(0.1f, span));
                float x1 = Mathf.Lerp(nearX, farX, lit);
                float a = 0.85f * Mathf.Sqrt(1f - u) * (0.8f + Random.Range(-0.15f, 0.15f));
                Color c = Crimson.WithA(a);
                const int seg = 10;
                for (int i = 0; i <= seg; i++)
                {
                    Vector2 p = new(Mathf.Lerp(nearX, x1, i / (float)seg), floorY + 0.012f);
                    rib.Add(p + Vector2.up * 0.055f, p - Vector2.up * 0.02f, c.WithA(0f), c);
                }
                if (u * seconds >= arcAt && lit > 0.05f)
                {
                    arcAt += Random.Range(0.05f, 0.1f);
                    float x = Mathf.Lerp(nearX, x1, Random.value);
                    Arc(new Vector2(x, floorY + 0.02f), new Vector2(x + Random.Range(-0.35f, 0.35f), floorY + Random.Range(0.1f, 0.3f)), 0.1f, 0.014f);
                    if (Random.value < 0.4f) Emit(_embers, new Vector2(x, floorY + 0.03f), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 0.9f)), 0.035f, 0.5f, Crimson);
                }
            });
            Dyn("ResidueCool", MeshKit.Alpha, Order - 30, seconds * 1.2f, false, (rib, u) =>
            {
                Color c = BloodBlack.WithA(0.45f * (1f - u));
                rib.Add(new Vector2(nearX, floorY + 0.03f), new Vector2(nearX, floorY - 0.01f), c, c);
                rib.Add(new Vector2(farX, floorY + 0.03f), new Vector2(farX, floorY - 0.01f), c, c);
            });
        }

        // =========================================================================================
        //  PHASE 3: THE STORM CALLED DOWN
        // =========================================================================================
        /// <summary>Tempest Cage's hilt slam: a small star and ring at the foot, cracks, dust both ways.</summary>
        public void HiltSlam(Vector2 p)
        {
            Star(p + Vector2.up * 0.12f, 0.5f, Color.white, Crimson);
            Ring(p + Vector2.up * 0.03f, 0.1f, 1.2f, 0.06f, Crimson, 0.3f, false, 0.22f, 2f);
            GroundCracks(p, 0.9f, 5, 0.6f);
            Dust(p, 1f, 1f);
            Dust(p, 1f, -1f);
        }

        /// <summary>
        /// Tempest Cage's spears standing at <paramref name="feet"/> for <paramref name="seconds"/>: each a jagged crimson-white spear re-struck every 3
        /// frames over a blood-black rim, and buzzing arcs running round the ring's ellipse between them every 4 frames. The drops out of the clouds are
        /// the caller's (they need the clouds).
        /// </summary>
        public void CageSpears(Vector2[] feet, Vector2 centre, float seconds, float height = 2.2f)
        {
            if (feet == null || seconds <= 0f) return;
            var jags = new Jag[feet.Length];
            for (int i = 0; i < feet.Length; i++) jags[i] = new Jag(feet[i] + Vector2.up * height, feet[i], 9, 0.08f, 1, Random.Range(1, 1 << 20));
            void Build(Ribbon rib, float u, float wMul, Color col, float alpha, bool soft)
            {
                int s = Mathf.FloorToInt(u * seconds / RestrikeSeconds);
                float e = (u < 0.06f ? 1f : 0.75f + 0.2f * Mathf.Sin(u * 60f)) * alpha;
                foreach (var j in jags)
                {
                    j.Strike(s);
                    PolyStrip(rib, j.main, 0.035f * wMul * 0.4f, 0.035f * wMul, col.WithA(col.a * e), soft);
                    foreach (var f in j.forks) PolyStrip(rib, f, 0.02f * wMul, 0.004f, col.WithA(col.a * e * 0.7f), soft);
                }
            }
            float buzz = 0f;
            Dyn("CageRim", MeshKit.Alpha, Order + 48, seconds, false, (rib, u) => Build(rib, u, 2.6f, BloodBlack, 0.5f, true));
            Dyn("CageSpear", AddHDR(2.6f), Order + 54, seconds, false, (rib, u) =>
            {
                Build(rib, u, 1.3f, Crimson, 1f, true);
                if (u * seconds >= buzz)
                {
                    buzz += 4f / 60f;
                    Crackle(centre + Vector2.up * 0.05f, Mathf.Abs(feet[0].x - centre.x), 0.2f, 1);
                    int i = Random.Range(0, feet.Length), k = (i + 1) % feet.Length;
                    Arc(feet[i] + Vector2.up * Random.Range(0.1f, 1.2f), feet[k] + Vector2.up * Random.Range(0.1f, 1.2f), 0.08f, 0.012f);
                }
            });
            Dyn("CageCore", AddHDR(3.2f), Order + 55, seconds, false, (rib, u) => Build(rib, u, 0.35f, Color.white, 0.8f, false));
        }

        /// <summary>The cage breaks: every spear shatters into spark shrapnel and a small star as the final cross passes.</summary>
        public void CageShatter(Vector2[] feet)
        {
            if (feet == null) return;
            foreach (var f in feet)
            {
                Vector2 c = f + Vector2.up * Random.Range(0.7f, 1.3f);
                Star(c, 0.5f, Color.white, Crimson);
                for (int i = 0; i < 10; i++)
                    Emit(_sparks, c + Vector2.up * Random.Range(-0.6f, 0.6f), Random.insideUnitCircle.normalized * Random.Range(2f, 6f) + Vector2.up, Random.Range(0.025f, 0.045f), Random.Range(0.3f, 0.55f), i % 3 == 0 ? Color.white : Crimson);
            }
            GameFeel.Shake(0.3f);
        }

        /// <summary>A chord's slice across the ring: a long thin slice line, tilted by <paramref name="tilt"/> metres so the two chords cross.</summary>
        public void CageSlice(Vector2 c, float halfWidth, float tilt) => SlashLine(c + new Vector2(-halfWidth, -tilt), c + new Vector2(halfWidth, tilt), Crimson, Color.white, 0.3f, 0.035f);

        /// <summary>Zenith Guillotine's vanish: a column of crimson-white ghosts rising up from him into the storm, and a thin streak up to the cloud base.</summary>
        public void ZenithColumn(FighterRig rig, Vector2 feet, Fighter owner = null)
        {
            if (rig != null)
                for (int i = 1; i <= 5; i++) Ghost(rig, Color.Lerp(Crimson, HotWhite, 0.45f), 0.16f + 0.04f * i, new Vector2(0f, 0.55f * i));
            var cam = ViewCam(owner);
            float topY = cam != null ? CloudBaseY(cam) : feet.y + 7f;
            Vector2 a = feet + Vector2.up * 1.8f, b = new(feet.x, Mathf.Max(a.y + 1f, topY));
            Dyn("ZenithStreak", AddHDR(2f), Order + 50, 0.22f, false, (rib, u) =>
            {
                float w = 0.03f * (1f - u);
                Color c = Color.Lerp(HotWhite, Crimson, u).WithA(1f - u);
                Vector2 hd = Vector2.Lerp(a, b, KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.4f)));
                rib.Add(a + Vector2.right * w, a - Vector2.right * w, c, c);
                rib.Add(hd + Vector2.right * w * 0.3f, hd - Vector2.right * w * 0.3f, c.WithA(0f), c.WithA(0f));
            });
        }

        /// <summary>
        /// The meteor: a white-hot tapered streak from <paramref name="top"/> down onto <paramref name="ground"/> in 4 frames (white core, crimson body,
        /// blood-black rim), shedding sparks; its tail catches up and it is gone by f10. The landing is the slam's.
        /// </summary>
        public void Meteor(Vector2 top, Vector2 ground, float life = 0.24f)
        {
            void Build(Ribbon rib, float u, float wMul, Color col)
            {
                float k = Mathf.Clamp01(u / 0.3f);
                Vector2 head = Vector2.Lerp(top, ground, KMath.Apply(Ease.OutQuad, k));       // it is down in 4 frames: the streak reads at once
                Vector2 tail = Vector2.Lerp(top, head, KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.2f) / 0.8f)));
                Vector2 d = head - tail;
                if (d.sqrMagnitude < 1e-6f) return;
                Vector2 n = new Vector2(-d.y, d.x).normalized;
                float w = 0.22f * wMul * (1f - 0.6f * u);
                Color c = col.WithA(col.a * (1f - u));
                rib.AddTriangle(head + n * w, head - n * w, tail, c, c, c.WithA(0f));
                rib.AddTriangle(head + n * w, head + (head - tail).normalized * w * 1.2f, head - n * w, c, c, c);
            }
            // drawn over the fighters (it is the fall itself, in front of him), the rim just under it
            Dyn("MeteorRim", MeshKit.Alpha, Order + 66, life, false, (rib, u) => Build(rib, u, 2.2f, BloodBlack.WithA(0.5f)));
            Dyn("MeteorBody", AddHDR(2.6f), Order + 67, life, false, (rib, u) =>
            {
                Build(rib, u, 1f, Crimson);
                if (u < 0.35f) Emit(_sparks, Vector2.Lerp(top, ground, KMath.Apply(Ease.OutQuad, Mathf.Clamp01(u / 0.3f))), new Vector2(Random.Range(-2f, 2f), Random.Range(0.5f, 2.5f)), 0.035f, 0.3f, Random.value < 0.4f ? Color.white : Crimson);
            });
            Dyn("MeteorCore", AddHDR(3.2f), Order + 68, life * 0.8f, false, (rib, u) => Build(rib, u, 0.35f, Color.white));
        }

        /// <summary>The discharge at the landing: a ring expanding out along the floor to 4 m with the waves, a white inner ring, a crackle and low sparks.</summary>
        public void Discharge(Vector2 ground)
        {
            Ring(ground + Vector2.up * 0.03f, 0.3f, 4.2f, 0.12f, Crimson, 0.55f, false, 0.22f, 2.4f);
            Ring(ground + Vector2.up * 0.03f, 0.2f, 2.6f, 0.06f, Color.white, 0.3f, false, 0.22f, 2f);
            Crackle(ground + Vector2.up * 0.05f, 2f, 0.25f, 3);
            for (int i = 0; i < 16; i++)
                Emit(_sparks, ground + Vector2.up * 0.05f, new Vector2((i % 2 == 0 ? 1f : -1f) * Random.Range(3f, 7f), Random.Range(0.3f, 1.6f)), 0.03f, Random.Range(0.3f, 0.5f), i % 3 == 0 ? Color.white : Crimson);
            GameFeel.Shake(0.35f);
        }

        /// <summary>
        /// Mirage Severance's split: a double exposure: two offset silhouettes (one hot white, one crimson) with a static seam between them and a thin
        /// shear across the chest. The seam has no white core: white-crimson body crackle is the aura tell's.
        /// </summary>
        public void MirageSplit(FighterRig rig, Vector2 feet, float face)
        {
            if (rig != null)
            {
                Ghost(rig, HotWhite, 0.22f, new Vector2(-0.25f * face, 0f));
                Ghost(rig, Crimson, 0.22f, new Vector2(0.25f * face, 0f));
            }
            Bolt(feet + Vector2.up * 1.95f, feet + Vector2.up * 0.05f, 0.014f, 0.16f, Crimson, 10, 0.05f, 0, false, Order + 50, 1f, 0f);
            Vector2 c = feet + Vector2.up * 1.1f;
            Dyn("MirageShear", AddHDR(1.6f), Order + 51, 0.2f, false, (rib, u) =>
            {
                float half = 0.9f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.3f));
                Color col = Shear.WithA(0.6f * (1f - u));
                rib.AddTriangle(c + new Vector2(-half, 0f), c + new Vector2(0f, 0.008f), c + new Vector2(half, 0f), col.WithA(0f), col, col.WithA(0f));
                rib.AddTriangle(c + new Vector2(-half, 0f), c + new Vector2(half, 0f), c + new Vector2(0f, -0.008f), col.WithA(0f), col.WithA(0f), col);
            });
        }

        /// <summary>
        /// The mirage decoy: a held, flickering crimson-white snapshot of <paramref name="rig"/> that stands for <paramref name="hold"/> seconds, dashes
        /// <paramref name="dashBy"/> metres in <paramref name="dash"/> seconds (no damage), and breaks into red ash where it stops. No eye trail: that is
        /// the read.
        /// </summary>
        public void MirageDecoy(FighterRig rig, float hold, Vector2 dashBy, float dash)
        {
            if (rig == null) return;
            var root = rig.Snapshot(HotWhite, rig.baseOrder - 30);
            root.transform.SetParent(transform, true);
            var fx = new MirageFx { go = root, rs = root.GetComponentsInChildren<MeshRenderer>(), life = hold + dash, hold = hold, from = root.transform.position, by = dashBy, vfx = this };
            _live.Add(fx);
            fx.Tick(0f);
        }

        class MirageFx : Fx
        {
            public MeshRenderer[] rs; public Vfx vfx; public float hold; public Vector3 from; public Vector2 by;
            readonly MaterialPropertyBlock _mpb = new();
            public override void Tick(float u)
            {
                float tt = u * life;
                if (tt > hold) go.transform.position = from + (Vector3)(by * KMath.Apply(Ease.OutExpo, Mathf.Clamp01((tt - hold) / Mathf.Max(0.01f, life - hold))));
                float a = 0.72f + Random.Range(-0.12f, 0.08f);
                _mpb.SetVector("_Color", new Vector4(1f, 1f, 1f, a));
                _mpb.SetColor("_FlashColor", Color.Lerp(HotWhite, Crimson, 0.3f + 0.4f * u));
                _mpb.SetFloat("_Flash", 0.85f);
                foreach (var r in rs) if (r != null) r.SetPropertyBlock(_mpb);
            }
            public override void Kill()
            {
                if (t >= life && vfx != null && go != null)
                {
                    Vector2 p = go.transform.position;
                    for (int i = 0; i < 16; i++)
                        Emit(vfx._embers, p + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.2f, 1.9f)), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 1.1f)), 0.045f, Random.Range(0.5f, 0.9f), i % 3 == 0 ? EyeRed : Crimson);
                    for (int i = 0; i < 6; i++)
                        Emit(vfx._dust, p + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.3f, 1.7f)), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.2f, 0.6f)), Random.Range(0.2f, 0.32f), Random.Range(0.6f, 0.9f), Ash.WithA(0.5f));
                }
                base.Kill();
            }
        }

        /// <summary>The mirage slash's mark: a razor-thin white-cored line held where the cut passed, steady, a blood-black residue under it, until it pops.</summary>
        public void MirageMark(Vector2 a, Vector2 b, float seconds)
        {
            if (seconds <= 0f) return;
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized;
            void Line(Ribbon rib, float w, Color c)
            {
                rib.AddTriangle(a, b + n * w, b - n * w, c.WithA(0f), c, c);
                rib.AddTriangle(a, (a + b) * 0.5f + n * w, (a + b) * 0.5f - n * w, c.WithA(0f), c, c);
            }
            Dyn("MirageMarkRim", MeshKit.Alpha, Order + 40, seconds, false, (rib, u) => Line(rib, 0.05f, BloodBlack.WithA(0.45f)));
            Dyn("MirageMarkBody", AddHDR(2.4f), Order + 42, seconds, false, (rib, u) => Line(rib, 0.022f, Crimson.WithA(0.75f + Random.Range(-0.1f, 0.1f))));
            Dyn("MirageMarkCore", AddHDR(3f), Order + 43, seconds, false, (rib, u) => Line(rib, 0.006f, Color.white));
        }

        /// <summary>The mark pops: a star burst at its middle, sparks out, and a micro-bolt falling onto it; a small scorch.</summary>
        public void MirageBurst(Vector2 ground)
        {
            Vector2 c = ground + Vector2.up * 0.95f;
            Star(c, 0.8f, Color.white, Crimson, 0.12f, 8, true, Order + 64);
            for (int i = 0; i < 14; i++)
                Emit(_sparks, c, Random.insideUnitCircle.normalized * Random.Range(2.5f, 5.5f), 0.032f, Random.Range(0.25f, 0.45f), i % 2 == 0 ? Color.white : Crimson);
            Bolt(ground + new Vector2(Random.Range(-0.3f, 0.3f), 2.2f), ground + Vector2.up * 0.05f, 0.03f, 0.24f, Crimson, 9, 0.12f, 1);
            Scorch(ground, 0.35f, 0.6f);
            GameFeel.Shake(0.22f);
        }
    }
}
