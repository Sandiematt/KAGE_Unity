using System;
using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Ironclad's look (SAN-100, redrawn smooth in the rework), drawn on Kage's skeleton with the smooth half of
    /// <see cref="OpponentKit"/>: a port, shape for shape, of the JS <c>order_ironclad</c> (the anime body of
    /// <c>21-characters/_shared/</c> with the Order skin of <c>enemies/order/</c>). The raider: a horned kabuto (ox-horn wakidate)
    /// with a broad shikoro over a black, heavy-jawed face and a throat guard of lacquer lames, a heavy dō of laced lames with a
    /// gorget and the Order mark, huge o-sode on both shoulders, thick sleeves over kote, hakama over lacquered suneate and a
    /// two-plate kusazuri front and back. The cleaver is the cleaver family's (<c>raider_cleaver</c>), drawn by <see cref="CleaverVisual"/>.
    /// <para>
    /// The JS draws on its own skeleton; here every piece is set on Kage's bones: limbs run from pivot to pivot with the JS radii
    /// (<see cref="U"/> reference px per JS unit), the trunk pieces in a spine frame from the waist (JS u = 0) to the neck (u = 1)
    /// whose across unit (<see cref="Uw"/>) is wider than the limbs', since Kage's trunk is longer than the JS body's and the
    /// Ironclad has to stay the broadest of the Order. The kusazuri plates are cloth on spring chains.
    /// </para>
    /// </summary>
    public sealed class OrderIroncladPartSet : IPartSet
    {
        public string Id => "order_ironclad";

        public const string Resource = "Art/SvgRig/OrderIronclad";

        /// <summary>Reference px per JS design unit on the limbs (the JS body's clothing unit 1.95 at its 1.39 scale), on the sleeves (the JS sleeve 1.1) and on the trunk.</summary>
        const float U = 4.3f, Ua = U * 1.1f, Uw = 6.3f;

        static readonly Frame Sp = Frame.Spine(878f, 412f, 852f, 222f, Uw);

        const float HeadX = 848f, HeadY = 166f, R = 57f;
        static readonly Frame Hd = Frame.Head(HeadX, HeadY, R);

        // tones (shadow, base, lit): the JS ORDER_LACQ, ORDER_LACQ_FAR, ORDER_HORN and ANIME metal / cloth, in the Order's slate
        static readonly Tone Fab = new(KMath.Hex("#0E1217"), O.ClothDeep, KMath.Hex("#2A333E"));
        static readonly Tone FabFar = new(O.ClothFar, O.ClothFar, O.ClothDeep);
        static readonly Tone Lacq = new(KMath.Hex("#08090B"), O.Lacquer, O.LacquerLit);
        static readonly Tone LacqFar = new(KMath.Hex("#060708"), KMath.Hex("#0B0D10"), KMath.Hex("#161A1F"));
        static readonly Tone Horn = new(KMath.Hex("#070707"), KMath.Hex("#141315"), KMath.Hex("#34313A"));
        static readonly Tone Metal = new(KMath.Hex("#0B0B0E"), KMath.Hex("#181A1F"), KMath.Hex("#363A42"));
        static readonly Tone MetalFar = new(KMath.Hex("#0B0B0E"), KMath.Hex("#0B0B0E"), KMath.Hex("#181A1F"));
        static readonly Tone Glove = new(O.ClothFar, O.Glove, O.Cloth);
        static readonly Tone GloveFar = new(O.ClothFar, O.ClothFar, O.Glove);
        static readonly Tone Boot = new(KMath.Hex("#0C0F13"), KMath.Hex("#151A20"), KMath.Hex("#2A323C"));
        static readonly Tone BootFar = new(KMath.Hex("#0C0F13"), KMath.Hex("#0C0F13"), KMath.Hex("#151A20"));
        static readonly Tone Obi = new(KMath.Hex("#080606"), KMath.Hex("#111214"), KMath.Hex("#26282C"));
        static readonly Tone Lining = new(KMath.Hex("#0B0E12"), KMath.Hex("#10141A"), O.ClothDeep);

        static readonly Color Black = KMath.Hex("#050404");             // the face (JS ANIME.face)
        static readonly Color FaceLight = KMath.Hex("#B8574B");         // the strip of light down the profile (JS ORDER_FACE_LIGHT)
        static readonly Color Lace = O.Lacing;                          // the crimson odoshi cords
        static readonly Color LaceHi = KMath.Hex("#B8404A");            // the Order mark (JS crimsonHi)
        static readonly Color Strap = KMath.Hex("#030303");

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER IRONCLAD");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            Arm(d, far: true);
            Leg(d, far: true);
            Pelvis(d);
            Kusazuri(d, back: true);
            Torso(d);
            Leg(d, far: false);
            Kusazuri(d, back: false);
            Belt(d);
            Head(d);
            Arm(d, far: false);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- helpers
        static Vector2 Lerp(Vector2 a, Vector2 b, float t) => Vector2.LerpUnclamped(a, b, t);

        /// <summary>The JS canvas normal of a segment, mirrored into reference px (the JS figure faces right, the reference faces left).</summary>
        static Vector2 Nrm(Vector2 a, Vector2 b) { Vector2 d = (b - a).normalized; return new Vector2(d.y, -d.x); }

        static Vector2 Pv(RigDef d, B b) => new(KageModel.OriginXRef - d.pivot[(int)b].x * KageModel.RefScale, KageModel.OriginYRef - d.pivot[(int)b].y * KageModel.RefScale);

        static float[] Xy(params Vector2[] p)
        {
            var r = new float[p.Length * 2];
            for (int i = 0; i < p.Length; i++) { r[i * 2] = p[i].x; r[i * 2 + 1] = p[i].y; }
            return r;
        }

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

        /// <summary>The JS <c>_quad</c>: a plate with edges bowed outward by <paramref name="bow"/> (a0-a1 straight; the other three edges curved). Convex.</summary>
        static Vector2[] Plate(Vector2 a0, Vector2 a1, Vector2 b1, Vector2 b0, float bow)
        {
            Vector2 n = (a1 - a0).normalized, dn = ((b0 + b1) * 0.5f - (a0 + a1) * 0.5f).normalized;
            Vector2 c1 = (a1 + b1) * 0.5f + n * bow, c3 = (b1 + b0) * 0.5f + dn * bow * 0.6f, c2 = (b0 + a0) * 0.5f - n * bow;
            return new Outline().MoveTo(a0.x, a0.y).LineTo(a1.x, a1.y).QuadTo(c1.x, c1.y, b1.x, b1.y).QuadTo(c3.x, c3.y, b0.x, b0.y).QuadTo(c2.x, c2.y, a0.x, a0.y).Bind();
        }

        /// <summary>The JS <c>_odoshi</c>: crimson lacing ticks under each lame's top edge and, from the second lame down, a lit line and a dark seam across. Q(k, t): k across, t down.</summary>
        static List<(Vector2[] pts, Color col)> Odoshi(Func<float, float, Vector2> Q, int rows, int ticks, Tone tone, float lwPx, float overshoot = 0.1f)
        {
            var r = new List<(Vector2[] pts, Color col)>();
            for (int i = 0; i < rows; i++)
            {
                float t0 = i / (float)rows, t1 = t0 + 0.42f / rows;
                for (int j = 0; j < ticks; j++)
                {
                    float k = (j + 0.5f) / ticks;
                    Vector2 p = Q(k, t0 + 0.06f / rows), q = Q(k, t1);
                    r.Add((Band(lwPx, p.x, p.y, q.x, q.y), Lace));
                }
                if (i == 0) continue;
                Vector2 a = Q(-overshoot, t0), b = Q(1f + overshoot, t0);
                r.Add((Band(1.9f, a.x + 1f, a.y + 2.4f, b.x + 1f, b.y + 2.4f), tone.lit));
                r.Add((Band(2.8f, a.x, a.y, b.x, b.y), Seam));
            }
            return r;
        }

        /// <summary>The JS Order mark: a ring barred across like a sealed mouth, at (cx, cy) reference px.</summary>
        static IEnumerable<(Vector2[] pts, Color col)> Mark(Vector2 c, float rad, float lw, Color col, Color inner)
        {
            yield return (Oval(c.x, c.y, rad + lw * 0.5f, rad + lw * 0.5f), col);
            yield return (Oval(c.x, c.y, rad - lw * 0.5f, rad - lw * 0.5f), inner);
            yield return (Band(lw, c.x - rad * 0.62f, c.y, c.x + rad * 0.62f, c.y), col);
        }

        static Vector2[] ConvexHull(Vector2[] pts)
        {
            var p = new List<Vector2>(pts);
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                foreach (var q in p)
                {
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0f) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
                p.Reverse();
            }
            return h.ToArray();
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        // ---------------------------------------------------------------------------------------------- arms (JS animeArm, orderSode)
        static void Arm(RigDef d, bool far)
        {
            B upper = far ? B.ArmFU : B.ArmNU, lower = far ? B.ArmFL : B.ArmNL, hand = far ? B.HandF : B.HandN;
            Vector2 sh = Pv(d, upper), el = Pv(d, lower), ha = Pv(d, hand);
            // Kage's wrist pivot sits well short of the grip: the JS forearm runs to the grip, so it is laid to the fist's centre
            Vector2 g = ha + (ha - el).normalized * 26f;
            int slot = far ? Slot.FarArm : Slot.NearArm;
            string s = far ? "ArmF" : "ArmN";
            var tone = far ? FabFar : Fab;
            bool sw = far;

            // the upper sleeve: full at the shoulder, gathered to the elbow, creases and a shoulder seam
            var sleeve = Capsule(sh.x, sh.y, el.x, el.y, 8.2f * Ua, 6.6f * Ua, 1.5f * Ua, 0.38f);
            var creases = new List<(Vector2[] pts, Color col)>();
            if (!far)
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
            AddCel(d, s + "_Sleeve", upper, slot, sleeve, tone, 4.2f * Ua, far ? 0f : 1.1f * Ua, 0, !far, Inside(sleeve, creases), swordLayer: sw);

            Sode(d, s + "_Sode", upper, slot, far, sh, el);

            // the kote: a lacquered forearm guard from under the sleeve to the wrist, two dark straps with crimson cord
            Vector2 a = Lerp(el, g, 0.3f), b = Lerp(el, g, 0.9f);
            var kote = Capsule(a.x, a.y, b.x, b.y, 4.4f * Ua, 3.6f * Ua, 0.4f * Ua, 0.4f);
            var straps = new List<(Vector2[] pts, Color col)>();
            Vector2 fn = Nrm(el, g);
            if (!far)
                foreach (float k in new[] { 0.62f, 0.84f })
                {
                    Vector2 p = Lerp(el, g, k) + fn * 5f * Ua, q = Lerp(el, g, k + 0.05f) - fn * 5f * Ua;
                    straps.Add((Band(6.5f, p.x, p.y, q.x, q.y), Strap));
                    straps.Add((Band(1.8f, p.x, p.y, q.x, q.y), Lace));
                }
            AddCel(d, s + "_Kote", lower, slot + 1, kote, far ? MetalFar : Metal, 2.2f * Ua, far ? 0f : 0.8f * Ua, 0, false, Inside(kote, straps), PartRole.Metal, swordLayer: sw);

            // the forearm sleeve: bunched at the elbow, flaring to its opening
            Vector2 m = Lerp(el, g, 0.5f);
            var fore = Capsule(el.x, el.y, m.x, m.y, 6.2f * Ua, 8.4f * Ua, 0.4f * Ua, 0.4f);
            var folds = new List<(Vector2[] pts, Color col)>();
            if (!far)
                foreach (var (u, o0, o1) in new[] { (0.04f, 5.4f, -3f), (0.18f, -5.6f, 2.6f), (0.3f, 6f, -1f) })
                {
                    Vector2 e = g - el;
                    Vector2 p = el + e * u + fn * o0 * Ua, q = el + e * (u + 0.1f) + fn * o1 * Ua, c = el + e * (u + 0.1f) + fn * (o0 + o1) * 0.2f * Ua;
                    folds.AddRange(Fold(Fab.lit, 2.4f, QuadPx(p, c, q)));
                }
            AddCel(d, s + "_ForeSleeve", lower, slot + 1, fore, tone, 3.6f * Ua, far ? 0f : 0.9f * Ua, 1, !far, Inside(fore, folds), swordLayer: sw);

            // the sleeve opening: the gi's dark inner lining across the forearm
            if (!far)
            {
                float u1 = 0.5f, r1 = 8.4f * Ua;
                Vector2 At(float u, float o) => Lerp(el, g, u) + fn * o;
                Vector2 p0 = At(u1 - 0.08f, r1 * 0.96f), p1 = At(u1 + 0.01f, r1 * 1.02f), p2 = At(u1 + 0.01f, -r1 * 1.02f), p3 = At(u1 - 0.08f, -r1 * 0.96f);
                Vector2 pc = At(u1 + 0.05f, 0f), pb = At(u1 - 0.11f, 0f);
                var lining = new Outline().MoveTo(p0.x, p0.y).LineTo(p1.x, p1.y).QuadTo(pc.x, pc.y, p2.x, p2.y).LineTo(p3.x, p3.y).QuadTo(pb.x, pb.y, p0.x, p0.y).Bind();
                AddCel(d, s + "_Cuff", lower, slot + 1, lining, Lining, 1.6f * Ua, 0f, 2, false);
            }

            // the glove closed round the grip, the fingers implied by one line (a little bigger than the JS mitten, so it reads on Kage's grip)
            {
                Vector2 u = (g - el).normalized;
                float rr = 2.9f * Ua * 1.3f, ang = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
                Vector2 c = g + u * rr * 0.1f;
                var glove = Oval(c.x, c.y, rr * 1.0f, rr * 0.82f, ang);
                Vector2 nn = new(-u.y, u.x), lc = c + u * rr * 0.3f;
                var line = new List<(Vector2[] pts, Color col)> { (Band(2.2f, lc.x - nn.x * rr * 0.8f, lc.y - nn.y * rr * 0.8f, lc.x + nn.x * rr * 0.8f, lc.y + nn.y * rr * 0.8f), Fab.lit) };
                AddCel(d, far ? "HandF" : "HandN_Fist", hand, far ? Slot.FarArm + 2 : Slot.NearHand, glove, far ? GloveFar : Glove, 1.4f * Ua, far ? 0f : 0.6f * Ua,
                    0, false, far ? null : Inside(glove, line), swordLayer: sw);
            }
        }

        /// <summary>The JS <c>orderSode</c>: a laced shoulder plate hanging from the shoulder, between straight down and the arm, hung a little outboard so it flares past the sleeve.</summary>
        static void Sode(RigDef d, string name, B bone, int slot, bool far, Vector2 sh, Vector2 el)
        {
            const float size = 1.3f;
            Vector2 a = (el - sh).normalized, up = Sp.ax.normalized, front = Sp.ay.normalized;
            Vector2 dv = (a * 0.45f - up * 0.55f).normalized, nn = new(dv.y, -dv.x);
            float len = 25f * U * size, w0 = 17f * U * size, w1 = 21f * U * size;
            Vector2 top = sh - dv * 5f * U - front * 1.5f * U;
            Vector2 a0 = top - nn * w0 * 0.5f, a1 = top + nn * w0 * 0.5f, b1 = top + dv * len + nn * w1 * 0.5f, b0 = top + dv * len - nn * w1 * 0.5f;
            Vector2 Q(float k, float t) => Lerp(Lerp(a0, a1, k), Lerp(b0, b1, k), t);
            var tone = far ? LacqFar : Lacq;
            var plate = Plate(a0, a1, b1, b0, 2f * U * size);
            var detail = new List<(Vector2[] pts, Color col)>();
            if (!far)
            {
                detail.AddRange(Odoshi(Q, 4, 5, tone, 1.1f * U, 0.04f));
                detail.Add((Band(3.2f, a0.x - 2f, a0.y + 5f, a1.x + 2f, a1.y + 5f), tone.lit));
            }
            AddCel(d, name, bone, slot, plate, tone, 3.4f * U, far ? 0f : 1f * U, 1, true, Inside(plate, detail), swordLayer: far);
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

            // the hakama over the shin, wide to the ankle, pleats down it
            Vector2 shinEnd = Lerp(kn, ft, 0.66f);
            var shin = Capsule(kn.x, kn.y, shinEnd.x, shinEnd.y, 9.8f * U, 10.6f * U, 1f * U, 0.5f);
            AddCel(d, s + "_Shin", low, slot, shin, tone, 4.4f * U, far ? 0f : 0.8f * U, 0, !far, far ? null : Inside(shin, Pleats(kn, shinEnd, -3.6f, 1f, 5f)));

            // the thigh over it, so the knee is the thigh's fold
            var thigh = Capsule(hp.x, hp.y, kn.x, kn.y, 11.6f * U, 10f * U, 1.6f * U, 0.42f);
            AddCel(d, s + "_Hakama", up, slot, thigh, tone, 5f * U, far ? 0f : 1f * U, 2, !far, far ? null : Inside(thigh, Pleats(hp, kn, -4.2f, 0.2f, 4.4f)));

            // the suneate: a lacquered shin guard with three splints and crimson ties
            Vector2 a = Lerp(kn, ft, 0.32f), b = Lerp(kn, ft, 0.93f);
            var sun = Capsule(a.x, a.y, b.x, b.y, 5.6f * U, 4.4f * U, 0.4f * U, 0.35f);
            var det = new List<(Vector2[] pts, Color col)>();
            if (!far)
            {
                Vector2 dd = b - a, n = Nrm(a, b);
                foreach (float o in new[] { -2.4f, 0.4f, 3f })
                {
                    Vector2 p = a + n * o * U + dd * 0.1f, q = b + n * o * 0.85f * U - dd * 0.04f;
                    det.Add((Band(2.4f, p.x, p.y, q.x, q.y), Seam));
                }
                foreach (float k in new[] { 0.16f, 0.8f })
                {
                    Vector2 p = a + dd * k + n * 6f * U, q = a + dd * (k + 0.05f) - n * 6f * U;
                    det.Add((Band(1.2f * U, p.x, p.y, q.x, q.y), Lace));
                }
            }
            AddCel(d, s + "_Suneate", low, slot + 1, sun, far ? LacqFar : Lacq, 2.4f * U, far ? 0f : 0.7f * U, 0, false, Inside(sun, det));

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
            AddCel(d, (far ? "FootF" : "FootN") + "_Tabi", foot, slot + 2, footShape, far ? BootFar : Boot, 1.8f * U, far ? 0f : 0.6f * U, 0, !far,
                Inside(footShape, new[] { (Band(1.4f * U, sole.x, sole.y, toe.x, toe.y), KMath.Hex("#3E3326")) }));
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
            var seat = RoundedAll(Frame.Ref, 806, 392, 952, 396, 970, 470, 960, 556, 880, 584, 800, 556, 788, 470);
            AddCel(d, "Pelvis", B.Hips, Slot.Pelvis, seat, Fab, 30f, 4f, 0, false);
            d.parts.Add(new PartDef { name = "Neck", bone = B.Neck, order = Slot.Neck * 4, role = PartRole.Body, rim = false, color = Black, pts = Capsule(852, 252, 858, 196, 24, 22) });
        }

        // the JS ORDER_DO.heavy profile (u along the spine, w across), listed from the hip up
        static readonly float[] DoFront = { 0.02f, 14.6f, 0.3f, 13.8f, 0.56f, 15.4f, 0.8f, 16.4f, 0.92f, 14.4f, 0.98f, 8.6f };
        static readonly float[] DoBack = { 0.02f, -15.2f, 0.26f, -14.4f, 0.5f, -14.6f, 0.74f, -16.6f, 0.92f, -15.6f, 0.98f, -9.4f };

        static float WAt(float[] prof, float u)
        {
            int n = prof.Length / 2;
            if (u <= prof[0]) return prof[1];
            for (int i = 1; i < n; i++)
                if (u <= prof[i * 2]) return Mathf.Lerp(prof[i * 2 - 1], prof[i * 2 + 1], (u - prof[i * 2 - 2]) / (prof[i * 2] - prof[i * 2 - 2]));
            return prof[n * 2 - 1];
        }

        static void Torso(RigDef d)
        {
            // the gi trunk under the armour (JS ANIME_TORSO.gi), a fold under the far armpit
            var torso = RoundedAll(Sp, -0.14f, 11.6f, 0.1f, 11f, 0.36f, 10.4f, 0.62f, 11.8f, 0.82f, 13f, 0.95f, 11f, 1.03f, 6.2f, 1.07f, 3.6f,
                1.07f, -5.2f, 0.97f, -13f, 0.8f, -13.8f, 0.54f, -11.4f, 0.28f, -11.4f, 0.04f, -12.8f, -0.14f, -12.8f);
            AddCel(d, "Torso", B.Chest, Slot.Torso, torso, Fab, 7.5f * U, 1f * U, 0, true, Inside(ConvexHull(torso), Fold(Fab.lit, 3f, Sp.Xy(0.8f, -2.5f, 0.5f, -4f, 0.3f, -3.2f))));

            // the heavy dō: five rows of lames, a bold crimson lacing, the chest plate's edge and the Order mark
            var plate = RoundedAll(Sp, 0.02f, 14.6f, 0.3f, 13.8f, 0.56f, 15.4f, 0.8f, 16.4f, 0.92f, 14.4f, 0.98f, 8.6f,
                0.98f, -9.4f, 0.92f, -15.6f, 0.74f, -16.6f, 0.5f, -14.6f, 0.26f, -14.4f, 0.02f, -15.2f);
            const float top = 0.82f;
            Vector2 Q(float k, float t)
            {
                float u = Mathf.Lerp(0.1f, top, 1f - t);
                return Sp.Px(u, Mathf.Lerp(WAt(DoBack, u), WAt(DoFront, u), k) * 0.97f);
            }
            var detail = Odoshi(Q, 5, 12, Lacq, 1.1f * U, 0.02f);
            Vector2 m0 = Sp.Px(top, WAt(DoBack, top) * 0.97f), m1 = Sp.Px(top, WAt(DoFront, top) * 0.97f);
            detail.Add((Band(5f, m0.x, m0.y, m1.x, m1.y), Strap));
            Vector2 e0 = Sp.Px(0.94f, -6f), e1 = Sp.Px(0.9f, 12f);
            detail.Add((Band(4f, e0.x, e0.y, e1.x, e1.y), Lacq.lit));
            detail.AddRange(Mark(Sp.Px((top + 0.95f) * 0.5f, 7.5f), 2.6f * U, 1.1f * U, LaceHi, Lacq.mid));
            AddCel(d, "Do", B.Chest, Slot.TorsoTrim, plate, Lacq, 7f * U, 1.1f * U, 1, true, detail);

            // the gorget round the throat
            var gorget = RoundedAll(Sp, 0.9f, 9f, 1f, 9.6f, 1.06f, 5f, 1.06f, -6f, 1f, -11f, 0.9f, -11f);
            Vector2 g0 = Sp.Px(0.97f, -12f), g1 = Sp.Px(0.97f, 11f);
            AddCel(d, "Gorget", B.Chest, Slot.TorsoTrim, gorget, Lacq, 2.6f * U, 0.8f * U, 2, true, Inside(gorget, new[] { (Band(1.2f * U, g0.x, g0.y, g1.x, g1.y), Lace) }));
        }

        // ---------------------------------------------------------------------------------------------- the waist and the kusazuri (JS orderBelt, orderKusazuri)
        static void Belt(RigDef d)
        {
            const float wf = 15f, wb = 15.4f;
            Vector2 a = Sp.Px(0f, -wb), b = Sp.Px(0f, wf), c = Sp.Px(0.12f, wf), e = Sp.Px(0.12f, -wb);
            var band = Plate(a, b, c, e, 2.6f);
            Vector2 l0 = Sp.Px(0.06f, -wb), l1 = Sp.Px(0.06f, wf), kc = Sp.Px(0.06f, wf - 3f);
            var detail = new List<(Vector2[] pts, Color col)> { (Band(1.2f * U, l0.x, l0.y, l1.x, l1.y), Lace), (Oval(kc.x, kc.y, 1.7f * U, 1.7f * U), LaceHi) };
            AddCel(d, "Sash", B.Chest, Slot.Belt, band, Obi, 2.6f * U, 0.6f * U, 0, false, Inside(band, detail), PartRole.Trim);
        }

        static void Kusazuri(RigDef d, bool back)
        {
            // JS: back panels at w -8 and 1 behind the near leg (far tone), front panels at -3 and 7 over it; 5 lames, 14-15 wide, 1.35 long.
            // Kage's waist is 130 px above its thigh joint, so the plates hang 1.25 times the JS length (width 1.3 times the limb unit) to cover the seat
            Vector2 hipB = Pv(d, B.LegFU), kneeB = Pv(d, B.LegFL), hipF = Pv(d, B.LegNU), kneeF = Pv(d, B.LegNL);
            if (back)
            {
                Panel(d, "Kusazuri_B1", true, -8f, hipB, kneeB, 14f, 0);
                Panel(d, "Kusazuri_B2", true, 1f, hipB, kneeB, 14f, 1);
            }
            else
            {
                Panel(d, "Kusazuri_F1", false, -3f, hipF, kneeF, 14f, 0);
                Panel(d, "Kusazuri_F2", false, 7f, hipF, kneeF, 15f, 1);
            }
        }

        static void Panel(RigDef d, string name, bool far, float w, Vector2 thighA, Vector2 thighB, float wid, int sub)
        {
            Vector2 top = Sp.Px(0.06f, w);
            Vector2 t = (thighB - thighA).normalized, up = Sp.ax.normalized;
            Vector2 dv = (-up * 0.5f + t * 0.5f).normalized, nn = new(dv.y, -dv.x);
            float len = 21f * U * 1.35f * 1.25f, w0 = wid * U * 1.3f, w1 = wid * 1.16f * U * 1.3f;
            Vector2 a0 = top - nn * w0 * 0.5f, a1 = top + nn * w0 * 0.5f, b1 = top + dv * len + nn * w1 * 0.5f, b0 = top + dv * len - nn * w1 * 0.5f;
            Vector2 Q(float k, float tt) => Lerp(Lerp(a0, a1, k), Lerp(b0, b1, k), tt);
            var tone = far ? LacqFar : Lacq;
            var plate = Plate(a0, a1, b1, b0, 1.2f * U);
            var detail = Inside(plate, Odoshi(Q, 5, far ? 3 : 5, tone, 1f * U, 0.05f));
            float[] Ch(float k) => Xy(Q(k, 0f), Q(k, 0.5f), Q(k, 1f));
            AddCloth(d, name, B.Hips, far ? Slot.BackPlates : Slot.FrontPlates, plate, tone, 3f * U, far ? 0f : 0.8f * U,
                new ChainFeel { stiffRoot = 0.6f, stiffTip = 0.25f, drag = 2.4f, gravity = 4f, breeze = 0.2f },
                new[] { Ch(0.1f), Ch(0.5f), Ch(0.9f) }, sub, !far, detail);
        }

        // ---------------------------------------------------------------------------------------------- the head (JS drawOrderHead 'horns' over the 'heavy' face)
        static void Head(RigDef d)
        {
            // the face: the JS 'heavy' profile with the Ironclad's numbers (nose 1, brow 2.2, lip 1.1, chin 1.45, jaw 1.4), black with a strip of light down it
            float rf = R * 0.9f;
            var face = FacePath(Frame.Head(HeadX, HeadY, rf));
            var lit = Inset(face, new Vector2(0.99f, 0.14f).normalized, Mathf.Max(2.5f, 0.1f * rf) / KageModel.RefScale, 0f);
            d.parts.Add(new PartDef
            {
                name = "Face", bone = B.Head, order = Slot.Head * 4, role = PartRole.Body, rim = false,
                polys = new List<(Vector2[] pts, Color col)> { (MeshKit.Grow(face, 0.004f), Seam), (face, FaceLight), (lit, Black) },
            });

            // the cloth hood under the helmet: crown and back of the head and the throat, open at the face
            var hood = RoundedAll(Hd, 0.5f, -0.96f, -0.5f, -1.12f, -1.08f, -0.5f, -1.06f, 0.42f, -0.8f, 1.24f, -0.14f, 1.26f, 0.22f, 1.12f, -0.02f, 0.7f, -0.26f, 0f, -0.22f, -0.66f);
            AddCel(d, "Hood", B.Head, Slot.Head, hood, Fab, R * 0.3f, R * 0.05f, 1, true);

            // the far ox horn behind the bowl
            AddCel(d, "Horn_Far", B.Head, Slot.Head, Rounded(Hd, -0.42f, -0.9f, 0, -1.24f, -1.18f, 0, -1.38f, -1.66f, 0, -1.02f, -2.42f, 1, -1.04f, -1.72f, 0, -0.82f, -1.38f, 0, -0.3f, -1.12f, 0),
                LacqFar, R * 0.1f, 0f, 2, true, null, PartRole.Detail);

            // the throat guard (yodare-kake): lacquer lames hung under the jaw, set back so the chin juts over them
            var throat = Rounded(Hd, -0.5f, 0.86f, 0, 0.34f, 1.0f, 1, 0.52f, 1.16f, 1, 0.36f, 1.52f, 1, -0.36f, 1.5f, 0, -0.66f, 1.2f, 0);
            AddCel(d, "Throat", B.Head, Slot.Head, throat, Lacq, R * 0.14f, R * 0.05f, 3, true,
                Inside(ConvexHull(throat), new[] { (Band(R * 0.08f, Hd.Xy(-0.56f, 1.16f, 0.46f, 1.22f)), Lace) }));

            // the shikoro: neck guard lames flaring back, three seams sweeping down and back with crimson lacing under each
            const float k = 1.18f;
            var shikoro = Rounded(Hd, 0.12f, -0.56f, 0, -0.86f, -0.82f, 0, -1.46f * k, 0.02f, 0, -1.74f * k, 0.64f * k, 1, -0.9f, 0.86f * k, 0, -0.42f, 0.46f, 0, -0.2f, -0.2f, 0);
            var sd = new List<(Vector2[] pts, Color col)>();
            for (int i = 1; i <= 3; i++)
            {
                float t = i / 4f;
                var lace = QuadXy(Hd, -0.3f - t * 0.2f, -0.4f + t * 0.95f + 0.1f, -0.9f - t * 0.3f, -0.62f + t * 1f + 0.1f, (-1.2f - t * 0.6f) * k, (-0.5f + t * 1.25f + 0.1f) * k);
                var seam = QuadXy(Hd, -0.3f - t * 0.2f, -0.4f + t * 0.95f, -0.9f - t * 0.3f, -0.62f + t * 1f, (-1.2f - t * 0.6f) * k, (-0.5f + t * 1.25f) * k);
                sd.AddRange(Dashes(Lace, R * 0.07f, R * 0.06f, R * 0.07f, lace));
                sd.Add((Band(R * 0.06f, seam), Seam));
            }
            AddCel(d, "Shikoro", B.Head, 22, shikoro, Lacq, R * 0.26f, R * 0.05f, 0, true, Inside(ConvexHull(shikoro), sd));

            // the kabuto bowl: riveted ribs and a crimson band at the brim
            var bowl = Rounded(Hd, 1.0f, -0.36f, 1, 0.86f, -0.92f, 0, 0.14f, -1.24f, 0, -0.7f, -1.06f, 0, -1.08f, -0.4f, 1);
            var bd = new List<(Vector2[] pts, Color col)>();
            foreach (float x in new[] { -0.55f, -0.15f, 0.25f, 0.6f })
                bd.Add((Band(R * 0.045f, QuadXy(Hd, x * 0.5f, -1.22f, x * 1.1f, -0.9f, x * 1.25f, -0.4f)), Seam));
            bd.Add((Band(R * 0.1f, QuadXy(Hd, -1.1f, -0.47f, 0f, -0.6f, 1.05f, -0.43f)), Lace));
            AddCel(d, "Kabuto", B.Head, 22, bowl, Lacq, R * 0.3f, R * 0.08f, 1, true, Inside(bowl, bd));

            // the visor and the turned-back wing (fukigaeshi) with the Order mark
            AddCel(d, "Kabuto_Visor", B.Head, 22, Rounded(Hd, 0.42f, -0.5f, 0, 1.32f, -0.44f, 1, 1.24f, -0.3f, 1, 0.4f, -0.34f, 0), Lacq, R * 0.06f, 0f, 2, false, null, PartRole.Detail);
            var wing = Rounded(Hd, 0.34f, -0.5f, 0, 0.24f, 0.04f, 1, -0.22f, -0.04f, 0, -0.1f, -0.52f, 0);
            Vector2 mk = Hd.Px(0.07f, -0.24f);
            AddCel(d, "Kabuto_Wing", B.Head, 22, wing, Lacq, R * 0.12f, R * 0.04f, 3, false, Inside(ConvexHull(wing), Mark(mk, R * 0.12f, R * 0.05f, Lace, Lacq.mid)), PartRole.Detail);
            Vector2 rv = Hd.Px(0.12f, -1.24f);
            Add(d, "Kabuto_Rivet", B.Head, Slot.HeadTop, O.SilverMid, Oval(rv.x, rv.y, R * 0.09f, R * 0.09f), 0, PartRole.Metal, false);

            // the near ox horn: thick at the bowl, sweeping out, forward and up to a hooked point, a crimson band at its root
            var horn = Rounded(Hd, 0.3f, -0.8f, 0, 1.12f, -1.0f, 0, 1.56f, -1.42f, 0, 1.52f, -2.42f, 1, 1.24f, -1.62f, 0, 0.86f, -1.32f, 0, 0.16f, -1.16f, 0);
            AddCel(d, "Horn_Near", B.Head, Slot.HeadTop, horn, Horn, R * 0.16f, R * 0.05f, 1, true,
                Inside(ConvexHull(horn), new[] { (Band(R * 0.12f, Hd.Xy(0.62f, -0.82f, 0.5f, -1.24f)), Lace) }), PartRole.Detail);

            // the eye: the JS angry triangle, pure white
            {
                float ex = rf * 0.56f, ey = rf * 0.08f, ew = rf * 0.42f, eh = rf * 0.24f;
                var e = Frame.Head(HeadX, HeadY, 1f);
                Vector2 o = e.Px(ex - ew * 0.5f, ey - eh * 0.62f), i = e.Px(ex + ew * 0.5f, ey + eh * 0.12f), b = e.Px(ex - ew * 0.08f, ey + eh * 0.5f);
                Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(o.x, o.y, i.x, i.y, b.x, b.y), 0, PartRole.Eye, false, null, 2.8f);
            }
        }

        /// <summary>The JS <c>animeFacePath</c> with the Ironclad's profile, in a head frame of face radius (closed outline).</summary>
        static Vector2[] FacePath(Frame f)
        {
            const float nose = 1f, lip = 1.1f, ch = 1.45f, jw = 1.4f, br = 2.2f;
            float bx = 0.95f + 0.035f * (br - 1f), tx = 0.93f + 0.2f * nose, ty = 0.36f;
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
    }
}
