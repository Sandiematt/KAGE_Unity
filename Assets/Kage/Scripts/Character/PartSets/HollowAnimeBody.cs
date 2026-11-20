using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The smooth anime body a gi-wearing Hollow is drawn on (SAN-76 rework): a port of the JS anime body of
    /// <c>21-characters/_shared/</c> (<c>animeArm</c>, <c>animeLeg</c>, <c>animeTorsoDraw</c>, <c>animeFoot</c>, <c>animeHand</c>,
    /// <c>animeNeck</c>, <c>face.js</c>) with the Hollow skin of <c>enemies/hollow/</c> (<c>hollowWrap</c>, <c>hollowCuff</c>,
    /// <c>hollowRobe</c>), shape for shape, on Kage's bones with the smooth half of <see cref="OpponentKit"/>. The Hollow Shade and
    /// the tutorial Hollow are built on it, each with its own sizes (<see cref="U"/> on the limbs, <see cref="Sp"/> across the trunk,
    /// the head frame) and its own dressing, head and cloth. The Husk (SAN-112) keeps its own copy of these pieces, and the Flicker the
    /// flat <see cref="HollowBody"/>: nothing here is shared with them, so they draw as before.
    /// <para>
    /// Limbs run pivot to pivot with the JS radii times <see cref="U"/> (reference px per JS design unit); trunk pieces are written in
    /// a JS spine frame from the waist (u 0) to the neck (u 1); head pieces in a JS head frame (+x the face side).
    /// </para>
    /// </summary>
    public sealed class HollowAnimeBody
    {
        /// <summary>Reference px per JS design unit on the legs, and on the arms (times the skin's sleeve width).</summary>
        public readonly float U, Ua;

        /// <summary>The spine frame: the waist (JS hip, u = 0) to the neck (u = 1), w across it in the trunk's own unit.</summary>
        public readonly Frame Sp;

        /// <summary>The head frame: the JS head centre and radius in reference px (+x the face side), and the face radius (0.9 of it).</summary>
        public readonly float HeadX, HeadY, R, Rf;
        public readonly Frame Hd;

        public HollowAnimeBody(float u, float sleeve, float trunkUnit, float headX, float headY, float r)
        {
            U = u; Ua = u * sleeve;
            Sp = Frame.Spine(878f, 412f, 852f, 222f, trunkUnit);
            HeadX = headX; HeadY = headY; R = r; Rf = r * 0.9f;
            Hd = Frame.Head(headX, headY, r);
        }

        // tones (shadow, base, lit) in the Hollow palette, as the Husk's
        public static readonly Tone Fab = new(H.ClothDeep, H.Cloth, H.ClothLit);
        public static readonly Tone FabFar = new(H.ClothFar, H.ClothFar, H.ClothDeep);
        public static readonly Tone Back = new(KMath.Hex("#0C0A14"), H.ClothFar, H.ClothDeep);
        public static readonly Tone Wrap = new(KMath.Hex("#0E0C12"), KMath.Hex("#1B1822"), KMath.Hex("#312D3B"));
        public static readonly Tone WrapFar = new(KMath.Hex("#0E0C12"), KMath.Hex("#0E0C12"), KMath.Hex("#1B1822"));
        public static readonly Tone Glove = new(KMath.Hex("#0B0910"), H.Glove, KMath.Hex("#2C2638"));
        public static readonly Tone GloveFar = new(KMath.Hex("#0B0910"), KMath.Hex("#0B0910"), H.Glove);
        public static readonly Tone Sandal = new(KMath.Hex("#09080C"), KMath.Hex("#15121B"), KMath.Hex("#29243A"));
        public static readonly Tone Mask = new(KMath.Hex("#1D1A25"), KMath.Hex("#363143"), KMath.Hex("#625B76"));
        public static readonly Tone MaskFar = new(KMath.Hex("#121017"), KMath.Hex("#1E1B26"), KMath.Hex("#2E2A38"));
        public static readonly Tone Skin = new(KMath.Hex("#0A080E"), KMath.Hex("#110E17"), KMath.Hex("#1F1A2A"));
        public static readonly Tone Straw = new(H.StrawDark, H.Straw, KMath.Hex("#8C7650"));
        public static readonly Tone Rope = new(KMath.Hex("#17130D"), H.RopeDark, H.Rope);

        public static readonly Color Black = KMath.Hex("#0A080E");                 // the face, the rip, the eye holes (JS ANIME.face)
        public static readonly Color FaceLight = KMath.Hex("#9A80F6");             // the strip of light down the profile (HOLLOW_FACE_LIGHT)
        public static readonly Color AccHi = KMath.Hex("#B9A8FF");                 // the lit front edge of a mask (JS pal.accHi)
        public static readonly Color Bloom = H.Violet.WithA(0.42f);               // a crack's soft glow (JS HOLLOW_GLOW)
        public static readonly Color WrapLine = KMath.Hex("#312D3B");

        /// <summary>A part set's pivot of a bone in reference px.</summary>
        public static Vector2 Pv(RigDef d, B b) => new(KageModel.OriginXRef - d.pivot[(int)b].x * KageModel.RefScale, KageModel.OriginYRef - d.pivot[(int)b].y * KageModel.RefScale);

        /// <summary>The JS canvas normal of a segment, mirrored into reference px (the JS figure faces right, the reference faces left).</summary>
        public static Vector2 Nrm(Vector2 a, Vector2 b) { Vector2 d = (b - a).normalized; return new Vector2(d.y, -d.x); }

        public static float[] Xy(params Vector2[] p)
        {
            var r = new float[p.Length * 2];
            for (int i = 0; i < p.Length; i++) { r[i * 2] = p[i].x; r[i * 2 + 1] = p[i].y; }
            return r;
        }

        public static float[] QuadPx(Vector2 p0, Vector2 c, Vector2 p1, int n = 8)
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

        public static Vector2[] ConvexHull(Vector2[] pts)
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

        /// <summary>Bandage turns across a limb a -> b (JS <c>hollowWrap</c> detail): a dark line with a lit one beside it.</summary>
        static List<(Vector2[] pts, Color col)> Turns(Vector2 a, Vector2 b, float rr, float unit, float skew)
        {
            var turns = new List<(Vector2[] pts, Color col)>();
            Vector2 dd = b - a, n = Nrm(a, b);
            int nt = Mathf.Max(3, Mathf.RoundToInt(dd.magnitude / (3.4f * unit)));
            for (int i = 1; i < nt; i++)
            {
                float k = i / (float)nt, sk = 0.07f * (i % 2 == 1 ? 1f : 0.6f) * skew;
                Vector2 x = a + dd * k, p = x + n * rr - dd * sk, q = x - n * rr + dd * sk;
                turns.Add((Band(2.8f, p.x, p.y, q.x, q.y), Seam));
                turns.Add((Band(1.9f, p.x + 2.2f, p.y - 3f, q.x + 2.2f, q.y - 3f), WrapLine));
            }
            return turns;
        }

        // ---------------------------------------------------------------------------------------------- the arm (JS animeArm with a Hollow skin)

        /// <summary>How a sleeve's opening ends: torn into hanging points (the Shade), or straw bursting out (the training husk).</summary>
        public enum Cuff { Torn, Straw }

        /// <summary>
        /// One arm, in the JS draw order: the full upper sleeve with creases, the bandaged kote (JS <c>hollowWrap</c> 0.3 to 0.9 down the
        /// forearm), the forearm sleeve bunched at the elbow and flaring to its opening at mid-forearm, the opening torn or stuffed with
        /// straw (JS <c>hollowCuff</c>), the small dark glove (JS <c>animeHand</c>). The far (katana) arm is one tone darker with no detail.
        /// </summary>
        public void Arm(RigDef d, bool far, Cuff cuff, float fray = 1f)
        {
            B upper = far ? B.ArmFU : B.ArmNU, lower = far ? B.ArmFL : B.ArmNL, hand = far ? B.HandF : B.HandN;
            Vector2 sh = Pv(d, upper), el = Pv(d, lower), ha = Pv(d, hand);
            int slot = far ? Slot.FarArm : Slot.NearArm;
            string s = far ? "ArmF" : "ArmN";
            var tone = far ? FabFar : Fab;
            bool sw = far;
            float u = Ua;
            Vector2 ex = ha - el, fn = Nrm(el, ha);
            Vector2 At(float k, float o) => el + ex * k + fn * o;

            // the upper sleeve: full at the shoulder, gathered to the elbow, creases and a shoulder seam
            var sleeve = Capsule(sh.x, sh.y, el.x, el.y, 8.2f * u, 6.6f * u, 1.5f * u, 0.38f);
            var creases = new List<(Vector2[] pts, Color col)>();
            if (!far)
            {
                Vector2 dd = el - sh, n = Nrm(sh, el);
                foreach (var (k, o0, o1) in new[] { (0.5f, -6f, 2f), (0.66f, -5.6f, 3.4f), (0.8f, -5f, 1.4f) })
                {
                    var p = sh + dd * k + n * o0 * u;
                    var q = sh + dd * (k + 0.04f) + n * o1 * u;
                    var c = sh + dd * (k + 0.08f) + n * (o0 + o1) * 0.5f * u;
                    creases.AddRange(Fold(H.ClothLit, 2.4f, QuadPx(p, c, q)));
                }
                creases.AddRange(Fold(H.ClothLit, 2.4f, QuadPx(sh + dd * 0.18f + n * 8f * u, sh + dd * 0.27f, sh + dd * 0.18f - n * 8f * u)));
            }
            AddCel(d, s + "_Sleeve", upper, slot, sleeve, tone, 4.2f * u, far ? 0f : 1.1f * u, 0, !far, Inside(sleeve, creases), swordLayer: sw);

            // the kote: bandage wraps from under the sleeve to the wrist
            {
                Vector2 a = At(0.3f, 0f), b = At(0.9f, 0f);
                float ra = 4.4f * u, rb = 3.6f * u;
                var kote = Capsule(a.x, a.y, b.x, b.y, ra, rb, 0.3f * u, 0.4f);
                AddCel(d, s + "_Kote", lower, slot, kote, far ? WrapFar : Wrap, 2.2f * u, far ? 0f : 0.7f * u, 1, !far,
                    far ? null : Inside(kote, Turns(a, b, Mathf.Max(ra, rb) * 1.2f, u, 1.6f)), swordLayer: sw);
            }

            // the forearm sleeve: bunched at the elbow, flaring to a wide kimono opening at mid-forearm
            const float u1 = 0.5f;
            float r0 = 6.2f * u, r1 = 8.4f * u;
            Vector2 m = At(u1, 0f);
            var fore = Capsule(el.x, el.y, m.x, m.y, r0, r1, 0.4f * u, 0.4f);
            var folds = new List<(Vector2[] pts, Color col)>();
            if (!far)
                foreach (var (k, o0, o1) in new[] { (0.04f, 5.4f, -3f), (0.18f, -5.6f, 2.6f), (u1 * 0.6f, 6f, -1f) })
                    folds.AddRange(Fold(H.ClothLit, 2.4f, QuadPx(At(k, o0 * u), At(k + 0.1f, (o0 + o1) * 0.2f * u), At(k + 0.1f, o1 * u))));
            AddCel(d, s + "_Forearm", lower, slot, fore, tone, 3.6f * u, far ? 0f : 0.9f * u, 2, !far, Inside(fore, folds), swordLayer: sw);

            // the opening: torn into hanging points with a violet lining, or stuffed with straw
            Vector2 p0 = At(u1, r1 * 0.98f), q0 = At(u1, -r1 * 0.98f), f = ex.normalized;
            if (cuff == Cuff.Torn)
            {
                Vector2 dir = new Vector2(f.x * 0.6f, f.y * 0.6f + 0.55f).normalized, back = -f * 2.4f * u;
                const int K = 4;
                float rag = 7f * u * fray;
                var pts = new List<Vector2> { p0 + back, p0 };
                for (int k = 0; k < K; k++)
                {
                    float t0 = (k + 0.5f) / K, t1 = (k + 1f) / K, dd = Teeth[(k + 2) % Teeth.Length] * rag + 2f * u;
                    pts.Add(Vector2.Lerp(p0, q0, t0) + dir * dd);
                    if (k < K - 1) pts.Add(Vector2.Lerp(p0, q0, t1) + dir * 1.2f * u);
                }
                pts.Add(q0); pts.Add(q0 + back);
                var torn = V(Xy(pts.ToArray()));
                var edge = new List<(Vector2[] pts, Color col)>();
                if (!far)
                {
                    Vector2 e0 = p0 + dir * 1.4f * u, e1 = q0 + dir * 1.4f * u;
                    edge.Add((Band(2.6f, e0.x, e0.y, e1.x, e1.y), H.Violet));
                }
                AddCel(d, s + "_Fray", lower, slot, torn, tone, 2.4f * u, far ? 0f : 0.7f * u, 3, !far, edge, swordLayer: sw);
            }
            else
            {
                var polys = new List<(Vector2[] pts, Color col)>();
                Vector2[] rimShape = null;
                for (int j = 0; j < 6; j++)
                {
                    float t = (j + 0.5f) / 6f, sp = (t - 0.5f) * 1.1f, c = Mathf.Cos(sp), sn = Mathf.Sin(sp);
                    Vector2 b = Vector2.Lerp(p0, q0, t), uu = new(f.x * c - f.y * sn, f.x * sn + f.y * c);
                    float len = (5f + Teeth[j] * 5f) * u;
                    Vector2 nn = new Vector2(-uu.y, uu.x) * 0.9f * u * 1.4f;
                    Vector2 a = b - f * 2f * u + nn, tip = b + uu * len, z = b - f * 2f * u - nn;
                    var tri = V(a.x, a.y, tip.x, tip.y, z.x, z.y);
                    polys.AddRange(Cel(tri, far ? new Tone(H.StrawDark, H.Straw, H.Straw) : Straw, 0.6f * u, 0f, 0f));
                    if (j == 2) rimShape = tri;
                }
                d.parts.Add(new PartDef { name = s + "_Straw", bone = lower, order = slot * 4 + 3, role = PartRole.Detail, polys = polys, rim = false, swordLayer = sw, rimPts = rimShape });
            }

            // the glove: a mitten round the grip, the fingers one line across it
            {
                float uh = far ? u : U;
                float r = 2.9f * uh * 1.2f, ang = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
                Vector2 c = ha + f * r * 0.25f, n = new(-f.y, f.x);
                var glove = Ellipse(c.x, c.y, r, r * 0.82f, ang, 28);   // a small oval, but round at close range
                var detail = new List<(Vector2[] pts, Color col)>();
                if (!far)
                {
                    Vector2 a = ha + f * 1.6f * uh + n * 2.3f * uh * 1.2f, b = ha + f * 1.6f * uh - n * 2.3f * uh * 1.2f;
                    detail.Add((Band(2f, a.x, a.y, b.x, b.y), KMath.Hex("#2C2638")));
                }
                AddCel(d, far ? "HandF" : "HandN_Fist", hand, far ? Slot.FarArm + 2 : Slot.NearHand, glove, far ? GloveFar : Glove, 1.4f * uh, far ? 0f : 0.6f * uh,
                    0, !far, Inside(glove, detail), swordLayer: sw);
            }
        }

        // ---------------------------------------------------------------------------------------------- the leg (JS animeLeg, hollowWrap, animeFoot)

        /// <summary>
        /// One leg: the hakama shin wide to its hem (0.6 down to the sole) with pleats, the thigh over it so the knee is the thigh's
        /// fold, the wrapped shin below the hakama, the tabi and straw sandal. Kage's ankle is high, so the lower leg is laid knee to sole.
        /// </summary>
        public void Leg(RigDef d, bool far)
        {
            B up = far ? B.LegFU : B.LegNU, low = far ? B.LegFL : B.LegNL, foot = far ? B.FootF : B.FootN;
            Vector2 hp = Pv(d, up), kn = Pv(d, low), an = Pv(d, foot);
            int slot = far ? Slot.FarLeg : Slot.NearLeg;
            string s = far ? "LegF" : "LegN";
            var tone = far ? FabFar : Fab;
            Vector2 ft = new(an.x, 797f);

            Vector2 shinEnd = Vector2.Lerp(kn, ft, 0.6f);
            var shin = Capsule(kn.x, kn.y, shinEnd.x, shinEnd.y, 9.8f * U, 10.6f * U, 1f * U, 0.5f);
            AddCel(d, s + "_Shin", low, slot, shin, tone, 4.4f * U, far ? 0f : 0.8f * U, 0, !far, far ? null : Inside(shin, Pleats(kn, shinEnd, -3.6f, 1f, 5f)));

            var thigh = Capsule(hp.x, hp.y, kn.x, kn.y, 11.6f * U, 10f * U, 1.6f * U, 0.42f);
            AddCel(d, s + "_Hakama", up, slot, thigh, tone, 5f * U, far ? 0f : 1f * U, 2, !far, far ? null : Inside(thigh, Pleats(hp, kn, -4.2f, 0.2f, 4.4f)));

            Vector2 wa = Vector2.Lerp(kn, ft, 0.4f), wb = Vector2.Lerp(kn, ft, 0.93f);
            var wrap = Capsule(wa.x, wa.y, wb.x, wb.y, 6f * U, 4.6f * U, 0.3f * U, 0.4f);
            AddCel(d, s + "_Wrap", low, slot + 1, wrap, far ? WrapFar : Wrap, 2.2f * U, far ? 0f : 0.7f * U, 0, !far,
                far ? null : Inside(wrap, Turns(wa, wb, 7.2f * U, U, 1.6f)));

            // the tabi and straw sandal (JS animeFootPath), stretched down to Kage's sole
            float hh = 5f * U, hv = 55f, len = 12.5f * U;
            Vector2 F(float fw, float sv) => new(an.x - fw, an.y + sv);
            var o = new Outline();
            var a0 = F(-0.95f * hh, -0.7f * hv); o.MoveTo(a0.x, a0.y);
            var a1 = F(-1.05f * hh, 0.95f * hv); o.LineTo(a1.x, a1.y);
            var a2 = F(0.92f * len, 0.95f * hv); o.LineTo(a2.x, a2.y);
            var c1 = F(len + 0.5f * hh, 0.7f * hv); var e1 = F(0.82f * len, -0.05f * hv); o.QuadTo(c1.x, c1.y, e1.x, e1.y);
            var c2 = F(0.9f * hh, -0.5f * hv); var e2 = F(0.55f * hh, -0.85f * hv); o.QuadTo(c2.x, c2.y, e2.x, e2.y);
            var footShape = o.Bind();
            var sole = F(-1f * hh, 0.86f * hv); var toe = F(11f * U, 0.86f * hv);
            AddCel(d, s + "_Foot", foot, slot + 2, footShape, far ? WrapFar : Sandal, 1.8f * U, far ? 0f : 0.6f * U, 0, !far,
                Inside(footShape, new[] { (Band(1.4f * U, sole.x, sole.y, toe.x, toe.y), KMath.Hex("#3E3326")) }));
        }

        IEnumerable<(Vector2[] pts, Color col)> Pleats(Vector2 a, Vector2 b, params float[] os)
        {
            Vector2 dd = b - a, n = Nrm(a, b);
            foreach (float o in os)
            {
                Vector2 p = a + n * o * U + dd * 0.12f, q = b + n * o * 1.1f * U;
                foreach (var f in Fold(H.ClothLit, 2.2f, p.x, p.y, q.x, q.y)) yield return f;
            }
        }

        // ---------------------------------------------------------------------------------------------- the trunk

        /// <summary>The hakama's seat bridging Kage's long waist-to-hip (no JS piece: Kage's trunk is longer than the JS body's), and the neck.</summary>
        public void Pelvis(RigDef d)
        {
            var seat = RoundedAll(Frame.Ref, 822, 396, 934, 400, 944, 470, 934, 548, 878, 562, 822, 548, 812, 470);
            AddCel(d, "Pelvis", B.Hips, Slot.Pelvis, seat, Back, 20f, 0f, 0, false);
        }

        /// <summary>The neck (JS <c>animeNeck</c>): black like the face, from the top of the spine towards the head.</summary>
        public void Neck(RigDef d)
        {
            Vector2 n0 = Sp.Px(0.96f, 0f), n1 = Vector2.Lerp(new Vector2(852f, 222f), new Vector2(HeadX, HeadY), 0.62f);
            AddCel(d, "Neck", B.Neck, Slot.Neck, Capsule(n0.x, n0.y, n1.x, n1.y, 3.6f * U * 1.3f, 3.2f * U * 1.3f), Skin, 2f * U, 0f, 0, false);
        }

        /// <summary>The gi trunk (JS <c>ANIME_TORSO.gi</c>) with its crossed collar, a fold under the far armpit and the Hollow's violet collar edge.</summary>
        public Vector2[] Torso(RigDef d)
        {
            var torso = RoundedAll(Sp, -0.14f, 11.6f, 0.1f, 11f, 0.36f, 10.4f, 0.62f, 11.8f, 0.82f, 13f, 0.95f, 11f, 1.03f, 6.2f, 1.07f, 3.6f,
                1.07f, -5.2f, 0.97f, -13f, 0.8f, -13.8f, 0.54f, -11.4f, 0.28f, -11.4f, 0.04f, -12.8f, -0.14f, -12.8f);
            var detail = new List<(Vector2[] pts, Color col)>
            {
                (Band(1.4f * U, Sp.Xy(1.04f, 0.5f, 0.62f, 6.6f, 0.3f, 8.6f)), H.ClothLit),     // the crossed collar
                (Band(1.2f * U, Sp.Xy(1.04f, 0.5f, 0.62f, 6.6f, 0.3f, 8.6f)), H.Violet),       // its violet edge (drawHollowArmor)
                (Band(2.4f, Sp.Xy(1.0f, 4.4f, 0.74f, 8.4f)), H.ClothDeep),
            };
            detail.AddRange(Fold(H.ClothLit, 2.6f, Sp.Xy(0.8f, -2.5f, 0.5f, -4f, 0.3f, -3.2f)));
            AddCel(d, "Torso", B.Chest, Slot.Torso, torso, Fab, 7.5f * U, 1f * U, 0, true, Inside(ConvexHull(torso), detail));
            return torso;
        }

        // ---------------------------------------------------------------------------------------------- the torn robe (JS hollowRobe, cloth from the waist)

        /// <summary>
        /// A torn robe panel hung from the waist (JS <c>hollowRobe</c>, <c>tornPanelPath</c>): smooth sides widening from
        /// <paramref name="w0"/> to <paramref name="w1"/> JS units, the hem torn into <paramref name="teeth"/> points, fold lines, a broken
        /// violet band near the hem, two tears; cloth on three chains (both edges and the middle). The back panel behind everything in
        /// the far tone, the front flap over the near leg.
        /// </summary>
        public void Robe(RigDef d, bool back, int links, float w0, float w1, int teeth, float rag, Vector2 top, Vector2 hem, float widthScale)
        {
            int seed = back ? 0 : 3;
            w0 *= U * widthScale; w1 *= U * widthScale;
            var left = new float[(links + 1) * 2];
            var right = new float[(links + 1) * 2];
            var mid = new float[(links + 1) * 2];
            for (int i = 0; i <= links; i++)
            {
                float t = i / (float)links, w = (w0 + (w1 - w0) * Mathf.Pow(t, 0.85f)) * 0.5f;
                Vector2 c = Vector2.LerpUnclamped(top, hem, t) + new Vector2((back ? 8f : -4f) * Mathf.Sin(t * Mathf.PI), 0f);
                left[i * 2] = c.x - w; left[i * 2 + 1] = c.y;
                right[i * 2] = c.x + w; right[i * 2 + 1] = c.y;
                mid[i * 2] = c.x; mid[i * 2 + 1] = c.y;
            }
            var panel = TornPanel(left, right, teeth, rag * U, seed);

            var detail = new List<(Vector2[] pts, Color col)>();
            foreach (float t in new[] { 0.3f, 0.62f, 0.84f })
            {
                var line = new List<float>();
                for (int i = 1; i <= links; i++)
                {
                    float tt = t + Mathf.Sin(i * 1.3f + t * 5f) * 0.05f;
                    line.Add(Mathf.Lerp(left[i * 2], right[i * 2], tt)); line.Add(Mathf.Lerp(left[i * 2 + 1], right[i * 2 + 1], t));
                }
                detail.AddRange(Fold(back ? H.ClothDeep : H.ClothLit, 2.2f, line.ToArray()));
            }
            if (links > 2)
            {
                int i = links - 1;
                detail.AddRange(Dashes(H.Violet.WithA(0.55f), 1.1f * U, 5f * U, 3.4f * U, left[i * 2], left[i * 2 + 1], right[i * 2], right[i * 2 + 1]));
            }
            foreach (var (ii, t, sz) in new[] { (Mathf.FloorToInt((links + 1) * 0.55f), 0.3f, 1f), (links - 1, 0.7f, 0.8f) })
            {
                if (ii < 1 || ii > links) continue;
                float x = Mathf.Lerp(left[ii * 2], right[ii * 2], t), y = Mathf.Lerp(left[ii * 2 + 1], right[ii * 2 + 1], t) - 12f;
                float w = 3.2f * U * sz, h = 2.4f * U * sz;
                detail.Add((V(x - w, y, x - w * 0.4f, y - h * 0.7f, x, y - h * 0.2f, x + w * 0.5f, y - h, x + w, y + 0.1f * h, x + w * 0.2f, y + h * 0.5f, x - w * 0.3f, y + h * 0.9f), Black));
                detail.Add((Band(1.8f, x + w, y + 0.1f * h, x + w * 0.2f, y + h * 0.5f, x - w * 0.3f, y + h * 0.9f), H.Violet));
            }
            detail = new List<(Vector2[] pts, Color col)>(Inside(ConvexHull(panel), detail));

            var feel = back
                ? new ChainFeel { stiffRoot = 0.3f, stiffTip = 0.05f, drag = 2.2f, gravity = 5f, breeze = 0.9f }
                : new ChainFeel { stiffRoot = 0.35f, stiffTip = 0.07f, drag = 2f, gravity = 6f, breeze = 0.5f };
            var chains = new[] { Edge(left, -8f), mid, Edge(right, 8f) };
            AddCloth(d, back ? "Robe_Back" : "Robe_Flap", B.Hips, back ? 0 : Slot.FrontPlates, panel, back ? Back : Fab,
                (back ? 7f : 5f) * U, back ? 0f : 0.9f * U, feel, chains, back ? 0 : 1, true, detail);
        }

        static float[] Edge(float[] xy, float dx)
        {
            var r = (float[])xy.Clone();
            for (int i = 0; i < r.Length; i += 2) r[i] += dx * (i / 2) / (r.Length / 2f);
            return r;
        }

        // ---------------------------------------------------------------------------------------------- the face (JS face.js) and the eye (eyes.js)

        /// <summary>A JS face profile (<c>FACE_PROFILES</c> fields; 1 / 0 is Kage's face).</summary>
        public struct Profile
        {
            public float nose, hook, up, lip, chin, jaw, brow, open, slope;
            public static Profile Gaunt => new() { nose = 1.1f, lip = 0.7f, chin = 1f, jaw = 0.88f, brow = 1.1f, open = 0.09f };
        }

        /// <summary>The JS <c>animeFacePath</c> in a head frame of face radius (closed outline in bind space).</summary>
        public static Vector2[] FacePath(Frame f, Profile p)
        {
            float bx = 0.95f + 0.035f * (p.brow - 1f), tx = 0.93f + 0.2f * p.nose, ty = 0.36f + p.hook - p.up;
            float cx = 0.6f + 0.13f * (p.chin - 1f), cy = 1.1f + 0.07f * (p.chin - 1f) + p.open * 0.5f, sl = p.slope;
            var o = new Outline(f).MoveTo(-0.94f, 0.12f)
                .CubicTo(-1.02f, -0.76f, -0.42f, -1.06f, 0.12f - sl, -1.03f)
                .CubicTo(0.66f - sl, -0.98f, bx + 0.01f - sl * 0.5f, -0.6f, bx, -0.2f)
                .LineTo(0.93f, 0.04f).LineTo(tx, ty).LineTo(0.94f, ty + 0.09f).LineTo(0.9f + 0.07f * p.lip, 0.6f);
            if (p.open > 0f) o.LineTo(0.84f, 0.645f).LineTo(0.89f + 0.05f * p.lip, 0.69f + p.open);
            return o.LineTo(0.88f, 0.7f + p.open * 1.25f)
                .QuadTo(0.86f + 0.09f * (p.chin - 1f), 0.98f + 0.04f * (p.chin - 1f) + p.open * 0.6f, cx, cy)
                .QuadTo(0.1f, 0.98f + 0.2f * (p.jaw - 1f) + p.open * 0.3f, -(0.36f + 0.12f * (p.jaw - 1f)), 0.72f + 0.1f * (p.jaw - 1f))
                .QuadTo(-0.82f, 0.6f, -0.94f, 0.12f)
                .Bind();
        }

        /// <summary>The face: black, with the Hollow's violet strip of light down the profile (JS <c>animeFace</c>, depth 0.1).</summary>
        public Vector2[] Face(RigDef d, Profile prof)
        {
            var face = FacePath(Frame.Head(HeadX, HeadY, Rf), prof);
            var lit = Inset(face, new Vector2(0.99f, 0.14f).normalized, Mathf.Max(2.5f, 0.1f * Rf) / KageModel.RefScale, 0f);
            d.parts.Add(new PartDef
            {
                name = "Face", bone = B.Head, order = Slot.Head * 4, role = PartRole.Body, rim = false, rimPts = face,
                polys = new List<(Vector2[] pts, Color col)> { (MeshKit.Grow(face, 0.004f), Seam), (face, FaceLight), (lit, Black) },
            });
            return face;
        }

        /// <summary>The eye holes of a mask (JS <c>_eyeHoles</c>), cut to <paramref name="clip"/>.</summary>
        public IEnumerable<(Vector2[] pts, Color col)> EyeHoles(Vector2[] clip)
        {
            float ey = Rf * 0.08f;
            var e = Frame.Head(HeadX, HeadY, 1f);
            foreach (var (x, y, w, h) in new[] { (Rf * 0.48f, ey, Rf * 0.5f, Rf * 0.3f), (Rf * 0.88f, ey - Rf * 0.03f, Rf * 0.22f, Rf * 0.24f) })
            {
                var hole = new Outline(e).MoveTo(x + w * 0.55f, y + h * 0.2f)
                    .QuadTo(x + w * 0.05f, y - h, x - w * 0.55f, y - h * 0.65f)
                    .QuadTo(x - w * 0.05f, y + h * 0.9f, x + w * 0.55f, y + h * 0.2f).Bind();
                var c = ClipConvex(hole, ConvexHull(clip));
                if (c.Length >= 3) yield return (c, Black);
            }
        }

        /// <summary>
        /// A mask piece (JS <c>_maskPiece</c> and <c>enemyFaceMask</c>): lacquer in the shadow tone over its back, a strip of violet
        /// light down its front edge, its eye holes and a glowing crack. The outline is in bind space; crack points in head units.
        /// </summary>
        public PartDef MaskPart(RigDef d, string name, int slot, int sub, Vector2[] mask, Tone t, bool holes, float[] crackHead, bool edgeLight)
        {
            var polys = new List<(Vector2[] pts, Color col)> { (MeshKit.Grow(mask, 1.6f / KageModel.RefScale), Seam) };
            if (edgeLight)
            {
                var unlit = Inset(mask, new Vector2(0.99f, 0.14f).normalized, Mathf.Max(2.5f, R * 0.09f) / KageModel.RefScale, 0f);
                polys.Add((mask, AccHi));
                polys.Add((unlit, t.shade));
                polys.Add((Inset(unlit, ShadeDir, R * 0.2f / KageModel.RefScale, 0f), t.mid));
            }
            else polys.AddRange(Cel(mask, t, R * 0.2f, R * 0.07f, 0f));
            if (holes) polys.AddRange(EyeHoles(mask));
            if (crackHead != null) polys.AddRange(Inside(ConvexHull(mask), Crack(Bloom, H.Violet, Mathf.Max(1.6f, R * 0.035f), Hd.Xy(crackHead))));
            var p = new PartDef { name = name, bone = B.Head, order = slot * 4 + sub, role = PartRole.Body, polys = polys, rim = false, rimPts = mask };
            d.parts.Add(p);
            return p;
        }

        /// <summary>The eye: the JS angry triangle, white fading to violet at the inner corner.</summary>
        public void Eye(RigDef d)
        {
            float ex = Rf * 0.56f, ey = Rf * 0.08f, ew = Rf * 0.42f, eh = Rf * 0.24f;
            var e = Frame.Head(HeadX, HeadY, 1f);
            Vector2 o = e.Px(ex - ew * 0.5f, ey - eh * 0.62f), i = e.Px(ex + ew * 0.5f, ey + eh * 0.12f), b = e.Px(ex - ew * 0.08f, ey + eh * 0.5f);
            Add(d, "Eye", B.Head, Slot.Face, H.Eye, V(o.x, o.y, i.x, i.y, b.x, b.y), 0, PartRole.Eye, false, null, 3.2f);
        }
    }
}
