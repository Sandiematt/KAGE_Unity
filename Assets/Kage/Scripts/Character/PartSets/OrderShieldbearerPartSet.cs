using System;
using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Shieldbearer's look (SAN-98, redrawn in the SAN-98 rework with the smooth half of <see cref="OpponentKit"/>): a port of
    /// the JS <c>order_shieldbearer</c> (the anime body of <c>21-characters/_shared/</c> with the Order pieces of <c>enemies/order/</c>),
    /// shape for shape, in JS draw order. A kabuto with a tall silver crescent maedate, a flared shikoro of laced lames and a cloth
    /// hood under it, over a stern hook-nosed face; a full dō laced in crimson with the Order mark; big sode; two kusazuri plates in
    /// front and two behind, hung from a dark belt; kote and suneate over a slate gi and hakama; straw sandals; and the thing that
    /// names him, a tall lacquered tate of iron-banded planks with the Order mark, strapped to the near forearm in front of the body.
    /// The spear is the spear family's (<c>sentinel_spear</c>), drawn by <see cref="SpearVisual"/>.
    /// <para>
    /// Every piece is built on the bone it moves with, from the JS drawing's own numbers: limbs run pivot to pivot (<see cref="U"/>
    /// reference px per JS unit), sode hang from the shoulder on the upper arm, kote and the tate ride the forearm, suneate the shin,
    /// the trunk pieces sit in a spine frame from the waist (JS u = 0) to the neck (u = 1) on the chest, the kusazuri hang from the
    /// belt on the hips and swing, the head pieces sit in a head frame on the head. So the armour is aligned by construction.
    /// </para>
    /// </summary>
    public sealed class OrderShieldbearerPartSet : IPartSet
    {
        public string Id => "order_shieldbearer";

        /// <summary>Reference px per JS design unit on the limbs (the JS U of 1.42 JS px at a skeleton 2.3 times smaller), and on the
        /// sleeves (the JS sleeve width 1.06).</summary>
        const float U = 3.3f, Ua = U * 1.06f;

        /// <summary>Reference px per JS w unit across the trunk: Kage's trunk is a little wider than the JS body's.</summary>
        const float Uw = 5f;

        /// <summary>Reference px per JS px of line width (the JS <c>A.s</c>).</summary>
        const float Js = 3f;

        // the spine frame: the waist (JS hip) to the neck
        static readonly Frame Sp = Frame.Spine(878f, 412f, 852f, 222f, Uw);

        // the head frame: the JS head centre and radius in reference px (+x the face side)
        const float HeadX = 846f, HeadY = 160f, R = 56f;
        static readonly Frame Hd = Frame.Head(HeadX, HeadY, R);

        // tones (shadow, base, lit): the JS ORDER_LACQ, ORDER_SILVER and anime metal, over the Order slate cloth
        static readonly Tone Lacq = new(KMath.Hex("#08090B"), O.Lacquer, O.LacquerLit);
        static readonly Tone LacqFar = new(KMath.Hex("#060708"), O.LacquerFar, KMath.Hex("#161A1F"));
        static readonly Tone Silver = new(O.SilverDark, O.SilverMid, O.Silver);
        /// <summary>The tate's planks: the same lacquer weathered a step lighter than the dō, so the shield reads in front of the armour
        /// it covers (the JS holds it out clear of the body; Kage's near arm keeps it over the trunk).</summary>
        static readonly Tone Plank = new(KMath.Hex("#12161C"), KMath.Hex("#252C35"), KMath.Hex("#46515F"));
        static readonly Tone Fab = new(O.ClothDeep, O.Cloth, KMath.Hex("#3A4757"));
        static readonly Tone FabFar = new(O.ClothFar, O.ClothFar, O.ClothDeep);
        static readonly Tone Lining = new(O.ClothFar, O.ClothFar, O.ClothDeep);
        static readonly Tone Metal = new(KMath.Hex("#0B0B0E"), KMath.Hex("#1A1D23"), KMath.Hex("#3C4048"));
        static readonly Tone GloveT = new(KMath.Hex("#0C0F13"), O.Glove, KMath.Hex("#323A45"));
        static readonly Tone BeltT = new(KMath.Hex("#080606"), KMath.Hex("#111214"), KMath.Hex("#26282C"));
        static readonly Tone Sandal = new(O.ClothFar, O.ClothDeep, O.Cloth);
        static readonly Tone SandalFar = new(O.ClothFar, O.ClothFar, O.ClothDeep);
        static readonly Tone Black = new(KMath.Hex("#050404"), KMath.Hex("#050404"), KMath.Hex("#050404"));

        static readonly Color Acc = O.Lacing;                               // the crimson lacing
        static readonly Color AccHi = KMath.Hex("#C4313A");                 // the Order mark
        static readonly Color FaceInk = KMath.Hex("#050404");               // the face and neck (JS ANIME.face)
        static readonly Color FaceLight = KMath.Hex("#B8574B");             // the cold crimson light down the profile (ORDER_FACE_LIGHT)
        static readonly Color Sole = KMath.Hex("#3E3326");

        /// <summary>The light (towards the front-top) in reference px, the JS <c>A.lx, A.ly</c>.</summary>
        static readonly Vector2 LightDir = new(-0.55f, -0.83f);

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER SHIELDBEARER");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            Arm(d, far: true);
            Leg(d, far: true);
            Pelvis(d);
            Torso(d);
            Kusazuri(d, front: false);
            Do(d);
            Leg(d, far: false);
            Kusazuri(d, front: true);
            Belt(d);
            Neck(d);
            Head(d);
            Arm(d, far: false);
            Tate(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- helpers

        static Vector2 Lerp(Vector2 a, Vector2 b, float t) => Vector2.LerpUnclamped(a, b, t);

        /// <summary>The JS canvas normal of a segment, mirrored into reference px (the JS figure faces right, the reference faces left).</summary>
        static Vector2 Nrm(Vector2 a, Vector2 b) { Vector2 d = (b - a).normalized; return new Vector2(d.y, -d.x); }

        static float[] Xy(params Vector2[] p)
        {
            var r = new float[p.Length * 2];
            for (int i = 0; i < p.Length; i++) { r[i * 2] = p[i].x; r[i * 2 + 1] = p[i].y; }
            return r;
        }

        static Vector2 Pv(RigDef d, B b) => new(KageModel.OriginXRef - d.pivot[(int)b].x * KageModel.RefScale, KageModel.OriginYRef - d.pivot[(int)b].y * KageModel.RefScale);

        static Vector2 ToRef(Vector2 bind) => new(KageModel.OriginXRef - bind.x * KageModel.RefScale, KageModel.OriginYRef - bind.y * KageModel.RefScale);

        static float[] QuadPx(Vector2 p0, Vector2 c, Vector2 p1, int n = 8)
        {
            var r = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                Vector2 p = u * u * p0 + 2f * u * t * c + t * t * p1;
                r[i * 2] = p.x; r[i * 2 + 1] = p.y;
            }
            return r;
        }

        /// <summary>A line to stroke: a polyline in reference px, its width in px and its colour.</summary>
        readonly struct Line
        {
            public readonly float[] xy; public readonly float w; public readonly Color col;
            public Line(float[] xy, float w, Color col) { this.xy = xy; this.w = w; this.col = col; }
        }

        /// <summary>
        /// The runs of a polyline (reference px) that lie inside an outline (bind space, convex or not) and at least
        /// <paramref name="margin"/> px in from its edge: the JS strokes a detail clipped to its part. Sampled every 2.5 px, a fixed
        /// number of samples per segment, so no float step can stall.
        /// </summary>
        static List<float[]> Runs(Vector2[] outline, float[] xy, float margin)
        {
            var ring = new Vector2[outline.Length];
            for (int i = 0; i < ring.Length; i++) ring[i] = ToRef(outline[i]);
            var runs = new List<float[]>();
            var run = new List<float>();
            void Close()
            {
                if (run.Count >= 4)
                {
                    float len = 0f;
                    for (int i = 2; i + 1 < run.Count; i += 2) len += new Vector2(run[i] - run[i - 2], run[i + 1] - run[i - 1]).magnitude;
                    if (len >= 2f) runs.Add(run.ToArray());
                }
                run = new List<float>();
            }
            for (int i = 0; i + 3 < xy.Length; i += 2)
            {
                Vector2 a = new(xy[i], xy[i + 1]), b = new(xy[i + 2], xy[i + 3]);
                int n = Mathf.Clamp(Mathf.CeilToInt((b - a).magnitude / 2.5f), 1, 400);
                for (int k = 0; k < n; k++)
                {
                    Vector2 p0 = Vector2.Lerp(a, b, k / (float)n), p1 = Vector2.Lerp(a, b, (k + 1) / (float)n);
                    Vector2 m = (p0 + p1) * 0.5f;
                    if (InsideRef(ring, m) && EdgeDist(ring, m) >= margin)
                    {
                        if (run.Count == 0) { run.Add(p0.x); run.Add(p0.y); }
                        run.Add(p1.x); run.Add(p1.y);
                    }
                    else Close();
                }
            }
            Close();
            return runs;
        }

        static bool InsideRef(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        static float EdgeDist(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], d = poly[i] - a;
                float t = d.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
                best = Mathf.Min(best, (a + d * t - p).magnitude);
            }
            return best;
        }

        /// <summary>Strokes lines clipped to a part's outline (any shape): one band per run inside it.</summary>
        static IEnumerable<(Vector2[] pts, Color col)> Stroke(Vector2[] outline, IEnumerable<Line> lines)
        {
            foreach (var l in lines)
                foreach (var r in Runs(outline, l.xy, l.w * 0.35f))
                    yield return (Band(l.w, r), l.col);
        }

        /// <summary>
        /// The JS <c>_odoshi</c>: lames on a plate given by its bilinear map <paramref name="q"/> (k across, t down, 0 to 1): rows of
        /// crimson lacing ticks, and from the second row a dark seam across with a lit line just below it.
        /// </summary>
        static List<Line> Lames(Func<float, float, Vector2> q, int rows, int ticks, Tone tone, float lw)
        {
            var r = new List<Line>();
            Vector2 off = -LightDir * 1.2f * Js;
            for (int i = 0; i < rows; i++)
            {
                float t0 = i / (float)rows, t1 = t0 + 0.42f / rows;
                for (int j = 0; j < ticks; j++)
                {
                    float k = (j + 0.5f) / ticks;
                    r.Add(new Line(Xy(q(k, t0 + 0.06f / rows), q(k, t1)), lw, Acc));
                }
                if (i == 0) continue;
                Vector2 a = q(-0.1f, t0), b = q(1.1f, t0);
                r.Add(new Line(Xy(a + off, b + off), 0.8f * Js, tone.lit));
                r.Add(new Line(Xy(a, b), 1.2f * Js, Seam));
            }
            return r;
        }

        /// <summary>
        /// The JS <c>_quad</c>: a plate with a straight top edge a0-a1, its sides and bottom bowed outwards by <paramref name="bow"/> px
        /// (the bottom by 0.6 of it). Reference px in, bind space out.
        /// </summary>
        static Vector2[] Plate(Vector2 a0, Vector2 a1, Vector2 b1, Vector2 b0, float bow)
        {
            Vector2 ctr = (a0 + a1 + b1 + b0) * 0.25f;
            Vector2 Bow(Vector2 p, Vector2 q, float k)
            {
                Vector2 m = (p + q) * 0.5f, e = (q - p).normalized, n = new(e.y, -e.x);
                if (Vector2.Dot(n, m - ctr) < 0f) n = -n;
                return m + n * k;
            }
            Vector2 c1 = Bow(a1, b1, bow), c3 = Bow(b1, b0, bow * 0.6f), c2 = Bow(b0, a0, bow);
            return new Outline().MoveTo(a0.x, a0.y).LineTo(a1.x, a1.y).QuadTo(c1.x, c1.y, b1.x, b1.y)
                .QuadTo(c3.x, c3.y, b0.x, b0.y).QuadTo(c2.x, c2.y, a0.x, a0.y).Bind();
        }

        /// <summary>The Order mark (JS <c>orderMark</c>): a ring barred across like a sealed mouth, on a ground of <paramref name="ground"/>.</summary>
        static IEnumerable<(Vector2[] pts, Color col)> Mark(Vector2 c, float r, float lw, Color col, Color ground)
        {
            yield return (Oval(c.x, c.y, r + lw * 0.5f, r + lw * 0.5f), col);
            yield return (Oval(c.x, c.y, r - lw * 0.5f, r - lw * 0.5f), ground);
            yield return (Band(lw, c.x - r * 0.62f, c.y, c.x + r * 0.62f, c.y), col);
        }

        // ---------------------------------------------------------------------------------------------- arms (JS animeArm, orderSode, animeHand)
        static void Arm(RigDef d, bool far)
        {
            B upper = far ? B.ArmFU : B.ArmNU, lower = far ? B.ArmFL : B.ArmNL, hand = far ? B.HandF : B.HandN;
            Vector2 sh = Pv(d, upper), el = Pv(d, lower), ha = Pv(d, hand);
            // the spear family holds the weapon arm in front of the body at all times (FighterRig.SetSwordAlwaysFront), so the far arm
            // is never behind him: both arms are lit, rimmed and laced alike (no JS far tones); the far arm's parts are sword-layer parts
            bool sw = far;
            int slot = far ? Slot.FarArm : Slot.NearArm;
            string s = far ? "ArmF" : "ArmN";
            var tone = Fab;

            // the upper sleeve: full at the shoulder, gathered to the elbow, creases and a shoulder seam
            var sleeve = Capsule(sh.x, sh.y, el.x, el.y, 8.2f * Ua, 6.6f * Ua, 1.5f * Ua, 0.38f);
            var creases = new List<(Vector2[] pts, Color col)>();
            {
                Vector2 dd = el - sh, n = Nrm(sh, el);
                foreach (var (u, o0, o1) in new[] { (0.5f, -6f, 2f), (0.66f, -5.6f, 3.4f), (0.8f, -5f, 1.4f) })
                {
                    var p = sh + dd * u + n * o0 * Ua;
                    var q = sh + dd * (u + 0.04f) + n * o1 * Ua;
                    var cc = sh + dd * (u + 0.08f) + n * (o0 + o1) * 0.5f * Ua;
                    creases.AddRange(Fold(Fab.lit, 2.6f, QuadPx(p, cc, q)));
                }
                creases.AddRange(Fold(Fab.lit, 2.6f, QuadPx(sh + dd * 0.18f + n * 8f * Ua, sh + dd * 0.27f, sh + dd * 0.18f - n * 8f * Ua)));
            }
            AddCel(d, s + "_Sleeve", upper, slot, sleeve, tone, 4.2f * Ua, 1.1f * Ua, 0, true, Inside(sleeve, creases), swordLayer: sw);

            // the sode over the upper sleeve, hung from the shoulder
            Sode(d, s, upper, slot, sh, el, 1.15f, sw);

            // along the forearm: u from the elbow (0) to the hand (1), o across it
            Vector2 ex = ha - el, fn = Nrm(el, ha);
            Vector2 At(float u, float o) => el + ex * u + fn * o;

            // the kote: a dark lacquered forearm guard from under the sleeve to the wrist, two dark straps with crimson cord
            {
                Vector2 a = At(0.25f, 0f), b = At(0.95f, 0f);
                var kote = Capsule(a.x, a.y, b.x, b.y, 4.4f * Ua, 3.6f * Ua, 0.4f * Ua, 0.4f);
                var straps = new List<Line>();
                foreach (float k in new[] { 0.62f, 0.84f })
                    {
                        Vector2 p = At(k, 5f * Ua), q = At(k + 0.05f, -5f * Ua);
                        straps.Add(new Line(Xy(p, q), 1.6f * Ua, Seam));
                        straps.Add(new Line(Xy(p, q), 0.6f * Js, Acc));
                    }
                AddCel(d, s + "_Kote", lower, slot + 1, kote, Metal, 2.2f * Ua, 0.8f * Ua, 0, true, Stroke(kote, straps), PartRole.Metal, sw);
            }

            // the forearm sleeve: bunched at the elbow, flaring to the gi's wide opening
            const float u1 = 0.45f;
            float r0 = 6.2f * Ua, r1 = 7.6f * Ua;
            {
                Vector2 m = At(u1, 0f);
                var fore = Capsule(el.x, el.y, m.x, m.y, r0, r1, 0.4f * Ua, 0.4f);
                var folds = new List<(Vector2[] pts, Color col)>();
                foreach (var (u, o0, o1) in new[] { (0.04f, 5.4f, -3f), (0.18f, -5.6f, 2.6f), (u1 * 0.6f, 6f, -1f) })
                    {
                        Vector2 p = At(u, o0 * Ua), q = At(u + 0.1f, o1 * Ua), cc = At(u + 0.1f, (o0 + o1) * 0.2f * Ua);
                        folds.AddRange(Fold(Fab.lit, 2.4f, QuadPx(p, cc, q)));
                    }
                AddCel(d, s + "_Forearm", lower, slot + 1, fore, tone, 3.6f * Ua, 0.9f * Ua, 1, true, Inside(fore, folds), swordLayer: sw);
            }

            // the sleeve opening: the gi's dark inner lining (the near arm's: the spear arm's opening faces away)
            if (!far)
            {
                Vector2 p0 = At(u1 - 0.08f, r1 * 0.96f), p1 = At(u1 + 0.01f, r1 * 1.02f), pc = At(u1 + 0.05f, 0f), p2 = At(u1 + 0.01f, -r1 * 1.02f), p3 = At(u1 - 0.08f, -r1 * 0.96f);
                var cuff = new Outline().MoveTo(p0.x, p0.y).LineTo(p1.x, p1.y).QuadTo(pc.x, pc.y, p2.x, p2.y).LineTo(p3.x, p3.y).Bind();
                AddCel(d, s + "_Cuff", lower, slot + 1, cuff, Lining, 1.6f * Ua, 0f, 2, false);
            }

            // the glove round the grip: a rounded block along the forearm, the fingers implied by one line
            {
                Vector2 u = ex.normalized, nn = new(-u.y, u.x);
                float r = 2.9f * Ua * 1.35f;
                Vector2 c = ha + u * r * 0.25f;
                var glove = Oval(c.x, c.y, r, r * 0.82f, Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg);
                Vector2 f0 = ha + u * 1.6f * Ua * 1.35f + nn * 2.3f * Ua * 1.35f, f1 = ha + u * 1.6f * Ua * 1.35f - nn * 2.3f * Ua * 1.35f;
                AddCel(d, far ? "HandF" : "HandN_Fist", hand, far ? Slot.FarArm + 2 : Slot.NearHand, glove, GloveT, 1.4f * Ua, 0.6f * Ua,
                    0, true, Stroke(glove, new[] { new Line(Xy(f0, f1), 0.7f * Js, GloveT.lit) }), swordLayer: sw);
            }
        }

        /// <summary>
        /// The JS <c>orderSode</c>: a laced shoulder plate hung from the shoulder over the upper arm, between straight down the spine and
        /// along the arm, a little behind the shoulder so it flares past the sleeve. Rides the upper arm.
        /// </summary>
        static void Sode(RigDef d, string s, B bone, int slot, Vector2 sh, Vector2 el, float size, bool swordLayer)
        {
            Vector2 down = (Sp.Px(0f, 0f) - Sp.Px(1f, 0f)).normalized, front = Sp.ay.normalized;
            Vector2 dir = ((el - sh).normalized * 0.45f + down * 0.55f).normalized, n = new(-dir.y, dir.x);
            float len = 25f * U * size, w0 = 17f * U * size, w1 = 21f * U * size;
            Vector2 top = sh - dir * 5f * U - front * 1.5f * U;
            Vector2 a0 = top - n * w0 * 0.5f, a1 = top + n * w0 * 0.5f, b1 = top + dir * len + n * w1 * 0.5f, b0 = top + dir * len - n * w1 * 0.5f;
            Vector2 Q(float k, float t) => Vector2.LerpUnclamped(Vector2.LerpUnclamped(a0, a1, k), Vector2.LerpUnclamped(b0, b1, k), t);
            var plate = Plate(a0, a1, b1, b0, 2f * U * size);
            var tone = Lacq;
            var lines = new List<Line>();
            {
                lines.AddRange(Lames(Q, size > 1.4f ? 5 : 4, 5, tone, 1.1f * U));
                Vector2 off = -LightDir * 1.4f * U + new Vector2(0f, 1.4f * U);   // the top plate's lit edge
                lines.Add(new Line(Xy(a0 + off, a1 + off), 1.3f * U, tone.lit));
            }
            AddCel(d, s + "_Sode", bone, slot, plate, tone, 3.4f * U, 1f * U, 1, true, Stroke(plate, lines), PartRole.Metal, swordLayer);
        }

        // ---------------------------------------------------------------------------------------------- legs (JS animeLeg, orderSuneate, animeFoot)
        static void Leg(RigDef d, bool far)
        {
            B up = far ? B.LegFU : B.LegNU, low = far ? B.LegFL : B.LegNL, foot = far ? B.FootF : B.FootN;
            Vector2 hp = Pv(d, up), kn = Pv(d, low), an = Pv(d, foot);
            int slot = far ? Slot.FarLeg : Slot.NearLeg;
            string s = far ? "LegF" : "LegN";
            var tone = far ? FabFar : Fab;
            // the JS ankle sits just over the ground; Kage's is high in a tall boot, so the lower leg is laid knee to sole
            Vector2 ft = new(an.x, 797f);

            // the hakama over the shin, wide to where the suneate takes over, pleats down it
            Vector2 shinEnd = Lerp(kn, ft, 0.66f);
            var shin = Capsule(kn.x, kn.y, shinEnd.x, shinEnd.y, 9.8f * U, 10.6f * U, 1f * U, 0.5f);
            AddCel(d, s + "_Shin", low, slot, shin, tone, 4.4f * U, far ? 0f : 0.8f * U, 0, !far, far ? null : Inside(shin, Pleats(kn, shinEnd, -3.6f, 1f, 5f)));

            // the thigh over it, so the knee is the thigh's fold
            var thigh = Capsule(hp.x, hp.y, kn.x, kn.y, 11.6f * U, 10f * U, 1.6f * U, 0.42f);
            AddCel(d, s + "_Hakama", up, slot, thigh, tone, 5f * U, far ? 0f : 1f * U, 2, !far, far ? null : Inside(thigh, Pleats(hp, kn, -4.2f, 0.2f, 4.4f)));

            // the suneate: a lacquered shin guard with three splints and crimson ties, over the bloused hakama
            {
                Vector2 a = Lerp(kn, ft, 0.32f), b = Lerp(kn, ft, 0.93f), dd = b - a, n = Nrm(a, b);
                var sun = Capsule(a.x, a.y, b.x, b.y, 5.6f * U, 4.4f * U, 0.4f * U, 0.35f);
                var lines = new List<Line>();
                if (!far)
                {
                    foreach (float o in new[] { -2.4f, 0.4f, 3f })
                        lines.Add(new Line(Xy(a + n * o * U + dd * 0.1f, b + n * o * 0.85f * U - dd * 0.04f), 1f * Js, Seam));
                    foreach (float k in new[] { 0.16f, 0.8f })
                        lines.Add(new Line(Xy(a + dd * k + n * 6f * U, a + dd * (k + 0.05f) - n * 6f * U), 1.2f * U, Acc));
                }
                AddCel(d, s + "_Suneate", low, slot + 1, sun, far ? LacqFar : Lacq, 2.4f * U, far ? 0f : 0.7f * U, 0, !far, Stroke(sun, lines), PartRole.Metal);
            }

            // the tabi and straw sandal (JS animeFootPath, stretched down to Kage's sole)
            float hh = 5f * U, hv = 55f, len = 12.5f * U;
            Vector2 F(float f, float sv) => new(an.x - f, an.y + sv);
            var o2 = new Outline();
            var p0 = F(-0.95f * hh, -0.7f * hv); o2.MoveTo(p0.x, p0.y);
            var p1 = F(-1.05f * hh, 0.95f * hv); o2.LineTo(p1.x, p1.y);
            var p2 = F(0.92f * len, 0.95f * hv); o2.LineTo(p2.x, p2.y);
            var c1 = F(len + 0.5f * hh, 0.7f * hv); var e1 = F(0.82f * len, -0.05f * hv); o2.QuadTo(c1.x, c1.y, e1.x, e1.y);
            var c2 = F(0.9f * hh, -0.5f * hv); var e2 = F(0.55f * hh, -0.85f * hv); o2.QuadTo(c2.x, c2.y, e2.x, e2.y);
            var footShape = o2.Bind();
            var sole = F(-1f * hh, 0.86f * hv); var toe = F(11f * U, 0.86f * hv);
            AddCel(d, far ? "FootF" : "FootN", foot, slot + 2, footShape, far ? SandalFar : Sandal, 1.8f * U, 0f, 0, !far,
                Inside(footShape, new[] { (Band(1.4f * U, sole.x, sole.y, toe.x, toe.y), Sole) }));
        }

        static IEnumerable<(Vector2[] pts, Color col)> Pleats(Vector2 a, Vector2 b, params float[] os)
        {
            Vector2 dd = b - a, n = Nrm(a, b);
            foreach (float o in os)
            {
                Vector2 p = a + n * o * U + dd * 0.12f, q = b + n * o * 1.1f * U;
                foreach (var f in Fold(Fab.lit, 2.4f, p.x, p.y, q.x, q.y)) yield return f;
            }
        }

        // ---------------------------------------------------------------------------------------------- the trunk
        static void Pelvis(RigDef d)
        {
            // Kage's trunk is longer than the JS body's: the hakama's seat bridges the waist and the thighs
            var seat = RoundedAll(Frame.Ref, 812, 392, 948, 396, 966, 470, 956, 556, 880, 582, 806, 556, 794, 470);
            AddCel(d, "Pelvis", B.Hips, Slot.Pelvis, seat, Fab, 30f, 4f, 0, false);
        }

        static void Torso(RigDef d)
        {
            // the gi trunk (JS ANIME_TORSO.gi), its crossed collar and a fold under the far armpit
            var torso = RoundedAll(Sp, -0.14f, 11.6f, 0.1f, 11f, 0.36f, 10.4f, 0.62f, 11.8f, 0.82f, 13f, 0.95f, 11f, 1.03f, 6.2f, 1.07f, 3.6f,
                1.07f, -5.2f, 0.97f, -13f, 0.8f, -13.8f, 0.54f, -11.4f, 0.28f, -11.4f, 0.04f, -12.8f, -0.14f, -12.8f);
            var lines = new[]
            {
                new Line(Sp.Xy(1.04f, 0.5f, 0.62f, 6.6f, 0.3f, 8.6f), 1.4f * U, Fab.lit),
                new Line(Sp.Xy(1.0f, 4.4f, 0.74f, 8.4f), 1f * Js, Fab.shade),
                new Line(Sp.Xy(0.8f, -2.5f, 0.5f, -4f, 0.3f, -3.2f), 1f * Js, Fab.shade),
            };
            AddCel(d, "Torso", B.Chest, Slot.Torso, torso, Fab, 7.5f * U, 1f * U, 0, true, Stroke(torso, lines));
        }

        /// <summary>The full dō (JS <c>ORDER_DO.full</c>, <c>drawOrderArmor</c>): lames laced in crimson, the chest plate's edge, a lit top
        /// edge and the Order mark, over the gi on the chest.</summary>
        static void Do(RigDef d)
        {
            var plate = RoundedAll(Sp, 0.04f, 13.6f, 0.3f, 12.8f, 0.56f, 14.6f, 0.78f, 15.8f, 0.91f, 14f, 0.96f, 8f,
                0.96f, -9f, 0.91f, -15f, 0.74f, -16f, 0.5f, -14f, 0.26f, -13.6f, 0.04f, -14.2f);
            const float top = 0.78f;
            Vector2 Q(float k, float t) => Sp.Px(Mathf.Lerp(0.1f, top, 1f - t), Mathf.LerpUnclamped(-17f, 17f, k));
            var lines = Lames(Q, 4, 12, Lacq, 1.1f * U);
            lines.Add(new Line(Sp.Xy(top, -18f, top, 18f), 1.4f * U, Seam));               // the chest plate (munaita)
            lines.Add(new Line(Sp.Xy(0.94f, -6f, 0.9f, 12f), 1.2f * U, Lacq.lit));         // its lit top edge
            var detail = new List<(Vector2[] pts, Color col)>(Stroke(plate, lines));
            detail.AddRange(Mark(Sp.Px((top + 0.95f) * 0.5f, 7.5f), 2.6f * U, 1.1f * U, AccHi, Lacq.mid));
            AddCel(d, "Do", B.Chest, Slot.TorsoTrim, plate, Lacq, 7f * U, 1.1f * U, 0, true, detail, PartRole.Metal);
        }

        /// <summary>
        /// The JS <c>orderKusazuri</c>: plates hung from the waist, each between straight down and along its thigh, laced in four rows.
        /// The back pair (over the far thigh) is drawn before the near leg, the front pair after it. Each rides the hips and swings from
        /// its top. Kage's waist sits far above his hips, so the plates are long enough to cover the hakama seat and the top of the thigh.
        /// </summary>
        static void Kusazuri(RigDef d, bool front)
        {
            Vector2 up = (Sp.Px(1f, 0f) - Sp.Px(0f, 0f)).normalized;
            const float len = 205f;
            void Panel(string name, float w, B thighA, B thighB, float wid, bool far, int slot, int sub, float swing)
            {
                Vector2 top = Sp.Px(0.06f, w);
                Vector2 t = (Pv(d, thighB) - Pv(d, thighA)).normalized;
                Vector2 dir = (-up * 0.5f + t * 0.5f).normalized, n = new(-dir.y, dir.x);
                float w0 = wid * Uw, w1 = wid * 1.16f * Uw;
                Vector2 a0 = top - n * w0 * 0.5f, a1 = top + n * w0 * 0.5f, b1 = top + dir * len + n * w1 * 0.5f, b0 = top + dir * len - n * w1 * 0.5f;
                Vector2 Q(float k, float tt) => Vector2.LerpUnclamped(Vector2.LerpUnclamped(a0, a1, k), Vector2.LerpUnclamped(b0, b1, k), tt);
                var plate = Plate(a0, a1, b1, b0, 1.2f * U);
                var tone = far ? LacqFar : Lacq;
                var p = AddCel(d, name, B.Hips, slot, plate, tone, 3f * U, far ? 0f : 0.8f * U, sub, !far, Stroke(plate, Lames(Q, 4, far ? 3 : 5, tone, 1f * U)), PartRole.Metal);
                Swing(p, top.x, top.y, swing);
            }
            if (!front)
            {
                Panel("Kusazuri_B1", -8f, B.LegFU, B.LegFL, 14f, true, Slot.Torso, 1, 0.3f);
                Panel("Kusazuri_B2", 1f, B.LegFU, B.LegFL, 14f, true, Slot.Torso, 2, 0.4f);
            }
            else
            {
                Panel("Kusazuri_F1", -3f, B.LegNU, B.LegNL, 14f, false, Slot.FrontPlates, 0, 0.35f);
                Panel("Kusazuri_F2", 7f, B.LegNU, B.LegNL, 15f, false, Slot.FrontPlates, 1, 0.45f);
            }
        }

        /// <summary>The JS <c>orderBelt</c>: a dark uwa-obi over the dō's lower edge, a crimson cord along it knotted at the front.</summary>
        static void Belt(RigDef d)
        {
            const float wf = 13.4f, wb = 13.8f;
            var belt = RoundedAll(Sp, 0f, -wb + 1.2f, 0f, -wb, 0.03f, -wb - 0.3f, 0.09f, -wb - 0.3f, 0.12f, -wb, 0.12f, -wb + 1.2f,
                0.12f, wf - 1.2f, 0.12f, wf, 0.09f, wf + 0.3f, 0.03f, wf + 0.3f, 0f, wf, 0f, wf - 1.2f);
            var detail = new List<(Vector2[] pts, Color col)>(Stroke(belt, new[] { new Line(Sp.Xy(0.06f, -wb, 0.06f, wf), 1.2f * U, Acc) }));
            Vector2 k = Sp.Px(0.06f, wf - 3f);
            detail.Add((Oval(k.x, k.y, 1.7f * U, 1.7f * U), AccHi));
            AddCel(d, "Sash", B.Chest, Slot.Belt, belt, BeltT, 2.6f * U, 0.6f * U, 1, true, detail, PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- the neck and the head
        static void Neck(RigDef d)
        {
            // black like the face (JS animeNeck): from the top of the spine to most of the way to the head's centre
            Vector2 n0 = Sp.Px(0.96f, 0f), n1 = Lerp(new Vector2(852f, 222f), new Vector2(HeadX, HeadY), 0.62f);
            AddCel(d, "Neck", B.Neck, Slot.Neck, Capsule(n0.x, n0.y, n1.x, n1.y, 0.24f * R, 0.21f * R), Black, 0f, 0f, 0, false);
        }

        static void Head(RigDef d)
        {
            float rf = R * 0.9f;

            // the face: the JS hawk profile with the Shieldbearer's numbers (a hooked nose under a heavy brow, a firm chin), black, a
            // thin strip of cold crimson light down the profile
            var face = FacePath(Frame.Head(HeadX, HeadY, rf));
            var lit = Inset(face, new Vector2(0.99f, 0.14f).normalized, rf * 0.1f / KageModel.RefScale, 0f);
            d.parts.Add(new PartDef
            {
                name = "Face", bone = B.Head, order = Slot.Head * 4, role = PartRole.Body, rim = false,
                polys = new List<(Vector2[] pts, Color col)> { (MeshKit.Grow(face, 1.6f / KageModel.RefScale), Seam), (face, FaceLight), (lit, FaceInk) },
            });

            // the cloth hood under the helmet: over the crown and the back of the head, open at the face (JS ZUKIN)
            var hood = RoundedAll(Hd, 0.5f, -0.96f, -0.5f, -1.12f, -1.08f, -0.5f, -1.06f, 0.42f, -0.8f, 1.24f, -0.14f, 1.26f, 0.22f, 1.12f,
                -0.02f, 0.7f, -0.26f, 0f, -0.22f, -0.66f);
            AddCel(d, "Hood", B.Head, Slot.Head, hood, Fab, 0.3f * R, 0.05f * R, 1);

            // the shikoro: lames flaring back and down from the bowl, crimson lacing under each seam (JS _kabuto)
            var shikoro = Rounded(Hd, 0.12f, -0.56f, 0, -0.86f, -0.82f, 0, -1.46f, 0.02f, 0, -1.74f, 0.64f, 1, -0.9f, 0.86f, 0, -0.42f, 0.46f, 0, -0.2f, -0.2f, 0);
            var sd = new List<(Vector2[] pts, Color col)>();
            for (int i = 1; i <= 3; i++)
            {
                float t = i / 4f;
                foreach (var run in Runs(shikoro, QuadXy(Hd, -0.3f - t * 0.2f, -0.4f + t * 0.95f + 0.1f, -0.9f - t * 0.3f, -0.62f + t + 0.1f, -1.2f - t * 0.6f, -0.5f + t * 1.25f + 0.1f, 16), 0.04f * R))
                    sd.AddRange(Dashes(Acc, 0.07f * R, 0.06f * R, 0.07f * R, run));
                sd.AddRange(Stroke(shikoro, new[] { new Line(QuadXy(Hd, -0.3f - t * 0.2f, -0.4f + t * 0.95f, -0.9f - t * 0.3f, -0.62f + t, -1.2f - t * 0.6f, -0.5f + t * 1.25f, 16), 0.06f * R, Seam) }));
            }
            AddCel(d, "Shikoro", B.Head, Slot.Head, shikoro, Lacq, 0.26f * R, 0.05f * R, 2, true, sd, PartRole.Metal);

            // the bowl: riveted ribs, a crimson band at the brim
            var bowl = Rounded(Hd, 1.0f, -0.36f, 1, 0.86f, -0.92f, 0, 0.14f, -1.24f, 0, -0.7f, -1.06f, 0, -1.08f, -0.4f, 1);
            var ribs = new List<Line>();
            foreach (float x in new[] { -0.55f, -0.15f, 0.25f, 0.6f })
                ribs.Add(new Line(QuadXy(Hd, x * 0.5f, -1.22f, x * 1.1f, -0.9f, x * 1.25f, -0.4f), 0.045f * R, Seam));
            ribs.Add(new Line(QuadXy(Hd, -1.1f, -0.47f, 0f, -0.6f, 1.05f, -0.43f, 16), 0.1f * R, Acc));
            AddCel(d, "Kabuto", B.Head, Slot.Head, bowl, Lacq, 0.3f * R, 0.08f * R, 3, true, Stroke(bowl, ribs), PartRole.Metal);

            // the visor, and the turned-back wing (fukigaeshi) at the temple with the Order mark; the silver tehen at the crown
            var visor = Rounded(Hd, 0.42f, -0.5f, 0, 1.32f, -0.44f, 1, 1.24f, -0.3f, 1, 0.4f, -0.34f, 0);
            AddCel(d, "Kabuto_Visor", B.Head, Slot.HeadTop, visor, Lacq, 0.06f * R, 0f, 0, false, null, PartRole.Metal);
            var wing = Rounded(Hd, 0.34f, -0.5f, 0, 0.24f, 0.04f, 1, -0.22f, -0.04f, 0, -0.1f, -0.52f, 0);
            var wd = new List<(Vector2[] pts, Color col)>(Mark(Hd.Px(0.07f, -0.24f), 0.12f * R, 0.05f * R, Acc, Lacq.mid));
            Vector2 tehen = Hd.Px(0.12f, -1.24f);
            wd.Add((Oval(tehen.x, tehen.y, 0.09f * R, 0.09f * R), O.SilverMid));
            AddCel(d, "Kabuto_Wing", B.Head, Slot.HeadTop, wing, Lacq, 0.12f * R, 0.04f * R, 1, true, wd, PartRole.Metal);

            // the tall silver crescent maedate on the brow, and its lacquer boss
            var crest = new Outline(Hd).MoveTo(0.56f, -1.02f).QuadTo(1.62f, -1.24f, 1.58f, -2.36f).QuadTo(1.36f, -1.48f, 0.5f, -1.3f)
                .QuadTo(-0.4f, -1.48f, -0.62f, -2.36f).QuadTo(-0.62f, -1.24f, 0.56f, -1.02f).Bind();
            AddCel(d, "Maedate", B.Head, Slot.HeadTop, crest, Silver, 0.1f * R, 0.05f * R, 2, true, null, PartRole.Metal);
            Vector2 boss = Hd.Px(0.53f, -1.06f);
            AddCel(d, "Maedate_Boss", B.Head, Slot.HeadTop, Oval(boss.x, boss.y, 0.16f * R, 0.12f * R), Lacq, 0.05f * R, 0f, 3, false, null, PartRole.Metal);

            // the eye: the JS angry triangle under the brow
            {
                float ex = rf * 0.56f, ey = rf * 0.08f, ew = rf * 0.42f, eh = rf * 0.24f;
                var e = Frame.Head(HeadX, HeadY, 1f);
                Vector2 o = e.Px(ex - ew * 0.5f, ey - eh * 0.62f), i = e.Px(ex + ew * 0.5f, ey + eh * 0.12f), b = e.Px(ex - ew * 0.08f, ey + eh * 0.5f);
                Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(o.x, o.y, i.x, i.y, b.x, b.y), 0, PartRole.Eye, false, null, 2.8f);
            }
        }

        /// <summary>The JS <c>animeFacePath</c> with the Shieldbearer's profile (<c>hawk</c> with brow 1.9, chin 1.14, lip 0.8), in a head
        /// frame of face radius (closed outline).</summary>
        static Vector2[] FacePath(Frame f)
        {
            const float nose = 1.34f, hook = 0.1f, lip = 0.8f, ch = 1.14f, jw = 1.04f, br = 1.9f;
            float bx = 0.95f + 0.035f * (br - 1f), tx = 0.93f + 0.2f * nose, ty = 0.36f + hook;
            float cx = 0.6f + 0.13f * (ch - 1f), cy = 1.1f + 0.07f * (ch - 1f);
            return new Outline(f).MoveTo(-0.94f, 0.12f)
                .CubicTo(-1.02f, -0.76f, -0.42f, -1.06f, 0.12f, -1.03f)
                .CubicTo(0.66f, -0.98f, bx + 0.01f, -0.6f, bx, -0.2f)
                .LineTo(0.93f, 0.04f).LineTo(tx, ty).LineTo(0.94f, ty + 0.09f).LineTo(0.9f + 0.07f * lip, 0.6f)
                .LineTo(0.88f, 0.7f)
                .QuadTo(0.86f + 0.09f * (ch - 1f), 0.98f + 0.04f * (ch - 1f), cx, cy)
                .QuadTo(0.1f, 0.98f + 0.2f * (jw - 1f), -(0.36f + 0.12f * (jw - 1f)), 0.72f + 0.1f * (jw - 1f))
                .QuadTo(-0.82f, 0.6f, -0.94f, 0.12f)
                .Bind();
        }

        // ---------------------------------------------------------------------------------------------- the tate
        /// <summary>
        /// The JS <c>orderShield</c>: a tall lacquered plank shield, bowed at top and bottom, two plank seams, two iron bands with
        /// silver rivets, the Order mark and a crimson brush stroke; centred a little in front of the near hand and strapped to the near
        /// forearm, so it follows the arm. The JS shield (26 x 80 JS px) at the scale that makes it about a sixth taller than the JS
        /// proportion of the body, so it reads at fighting distance: 107 px wide, about 345 px tall.
        /// </summary>
        static void Tate(RigDef d)
        {
            Vector2 c = Pv(d, B.HandN) + new Vector2(-24f, 0f);
            const float k = 4.125f, w = 13f * k, h = 40f * k;
            var f = new Frame(c, Vector2.right, Vector2.up);
            var shape = new Outline(f).MoveTo(-w, -h + 4f * k).QuadTo(0f, -h - 3f * k, w, -h + 4f * k).LineTo(w * 0.92f, h - 2f * k)
                .QuadTo(0f, h + 3f * k, -w * 0.92f, h - 2f * k).Bind();
            var detail = new List<(Vector2[] pts, Color col)>();
            var lines = new List<Line>();
            foreach (float x in new[] { -w / 3f, w / 3f }) lines.Add(new Line(f.Xy(x, -h, x * 0.95f, h), 1f * k, Seam));
            detail.AddRange(Stroke(shape, lines));
            foreach (float y in new[] { -h * 0.62f, h * 0.62f })
            {
                detail.AddRange(Inside(shape, new[]
                {
                    (V(f.Xy(-w * 1.1f, y - 2.6f * k, w * 1.1f, y - 2.6f * k, w * 1.1f, y + 2.6f * k, -w * 1.1f, y + 2.6f * k)), Lacq.mid),
                    (V(f.Xy(-w * 1.1f, y - 2.6f * k, w * 1.1f, y - 2.6f * k, w * 1.1f, y - 1.6f * k, -w * 1.1f, y - 1.6f * k)), Plank.lit),
                }));
                foreach (float x in new[] { -w * 0.6f, 0f, w * 0.6f })
                {
                    Vector2 p = f.Px(x, y);
                    detail.Add((Oval(p.x, p.y, 1.1f * k, 1.1f * k), O.SilverMid));
                }
            }
            detail.AddRange(Mark(c, 6.4f * k, 2f * k, AccHi, Plank.mid));
            detail.AddRange(Stroke(shape, new[] { new Line(QuadXy(f, -w * 0.7f, -h * 0.3f, 0f, -h * 0.18f, w * 0.7f, -h * 0.3f, 16), 1.6f * k, Acc) }));
            AddCel(d, "Tate", B.ArmNL, Slot.NearArm + 1, shape, Plank, 4f * k, 1.2f * k, 3, true, detail, PartRole.Metal);
        }
    }
}
