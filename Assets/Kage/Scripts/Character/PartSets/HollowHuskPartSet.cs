using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The Hollow Husk's look (SAN-112), drawn on Kage's skeleton with the smooth half of <see cref="OpponentKit"/>: a port of the JS
    /// <c>hollow_husk</c> (the anime body of <c>21-characters/_shared/</c> with the Hollow skin of <c>enemies/hollow/</c>), shape for
    /// shape. A heavy rounded cowl with a horned brow mask (the horns broken to stubs, the mask cut over the eyes so the heavy jaw
    /// hangs open below), a ragged mantle humped over the shoulders, a torn robe open on a black chest of glowing violet cracks,
    /// full sleeves torn off at the elbow over thick bound forearms, huge bandaged fists, a shimenawa rope belt hung with shide, a
    /// torn robe hanging front and back, wide hakama over wrapped shins. No blade and no saya.
    /// <para>
    /// The JS draws on its own skeleton; here every piece is set on Kage's bones: limbs run from pivot to pivot with the JS radii
    /// (<see cref="U"/> reference px per JS unit), the trunk pieces in a spine frame from the waist (JS u = 0) to the neck (u = 1)
    /// whose across unit (<see cref="Uw"/>) is wider than the limbs', since Kage's trunk is longer than the JS body's. The robe
    /// panels, the mantle's hanging points, the shide and the fists' loose ends are cloth on spring chains.
    /// </para>
    /// </summary>
    public sealed class HollowHuskPartSet : IPartSet
    {
        public string Id => "hollow_husk";

        /// <summary>Reference px per JS design unit (limbs), and on the arms (the JS sleeve width 1.12).</summary>
        const float U = JsUnit, Ua = JsUnit * 1.12f;

        /// <summary>Reference px per JS w unit across the trunk.</summary>
        const float Uw = 6.45f;

        // the spine frame: the waist (JS hip) to the neck
        static readonly Frame Sp = Frame.Spine(878f, 412f, 852f, 222f, Uw);

        // the head frame: the JS head centre and radius in reference px (+x the face side)
        const float HeadX = 846f, HeadY = 166f, R = 58f;
        static readonly Frame Hd = Frame.Head(HeadX, HeadY, R);

        // tones (shadow, base, lit), the JS palette of a Hollow
        static readonly Tone Fab = new(H.ClothDeep, H.Cloth, H.ClothLit);
        static readonly Tone FabFar = new(H.ClothFar, H.ClothFar, H.ClothDeep);
        static readonly Tone CoatB = new(KMath.Hex("#0C0A14"), H.ClothFar, H.ClothDeep);
        static readonly Tone MantleT = new(KMath.Hex("#141020"), KMath.Hex("#2A2340"), KMath.Hex("#463B70"));
        static readonly Tone Wrap = new(KMath.Hex("#0E0C12"), KMath.Hex("#1B1822"), KMath.Hex("#312D3B"));
        static readonly Tone WrapFar = new(KMath.Hex("#0E0C12"), KMath.Hex("#0E0C12"), KMath.Hex("#1B1822"));
        static readonly Tone Mask = new(KMath.Hex("#1D1A25"), KMath.Hex("#363143"), KMath.Hex("#625B76"));
        static readonly Tone MaskFar = new(KMath.Hex("#121017"), KMath.Hex("#1E1B26"), KMath.Hex("#2E2A38"));
        static readonly Tone Rope = new(KMath.Hex("#17130D"), KMath.Hex("#2B241A"), KMath.Hex("#4A3F2C"));
        static readonly Tone Paper = new(KMath.Hex("#3A3548"), KMath.Hex("#625C78"), KMath.Hex("#8E88A6"));
        static readonly Tone Sandal = new(KMath.Hex("#09080C"), KMath.Hex("#15121B"), KMath.Hex("#29243A"));

        static readonly Color Black = KMath.Hex("#0A080E");                 // the face and the open chest (JS ANIME.face)
        static readonly Color FaceLight = KMath.Hex("#9A80F6");             // the strip of light down the profile
        static readonly Color Bloom = H.Violet.WithA(0.42f);               // a crack's soft glow
        static readonly Color WrapLine = KMath.Hex("#312D3B");

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW HUSK");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
            d.scarf = false;
            d.softCoat = false;

            Robe(d, back: true);
            Arm(d, far: true);
            Leg(d, far: true);
            Pelvis(d);
            Torso(d);
            Leg(d, far: false);
            Robe(d, back: false);
            Sash(d);
            Mantle(d);
            Head(d);
            Arm(d, far: false);
            return d;
        }

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

        // ---------------------------------------------------------------------------------------------- arms (JS animeArm, hollowWrap, hollowCuff, hollowFist)
        static void Arm(RigDef d, bool far)
        {
            B upper = far ? B.ArmFU : B.ArmNU, lower = far ? B.ArmFL : B.ArmNL, hand = far ? B.HandF : B.HandN;
            Vector2 sh = Pv(d, upper), el = Pv(d, lower), ha = Pv(d, hand);
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
                    creases.AddRange(Fold(H.ClothLit, 2.6f, QuadPx(p, cc, q)));
                }
                creases.AddRange(Fold(H.ClothLit, 2.6f, QuadPx(sh + dd * 0.18f + n * 8f * Ua, sh + dd * 0.27f, sh + dd * 0.18f - n * 8f * Ua)));
            }
            AddCel(d, s + "_Sleeve", upper, slot, sleeve, tone, 4.2f * Ua, far ? 0f : 1.1f * Ua, 0, !far, Inside(sleeve, creases), swordLayer: sw);

            // the bare forearm, bound in wraps (diagonal turns, each a dark line with a lit one beside it)
            Vector2 a = Lerp(el, ha, 0.02f), b = Lerp(el, ha, 0.92f);
            float ra = 5.4f * Ua, rb = 4.6f * Ua;
            var fore = Capsule(a.x, a.y, b.x, b.y, ra, rb, 0.3f * U, 0.4f);
            var turns = new List<(Vector2[] pts, Color col)>();
            if (!far)
            {
                Vector2 dd = b - a, n = Nrm(a, b);
                float l = dd.magnitude, rr = Mathf.Max(ra, rb) * 1.2f;
                int nt = Mathf.Max(3, Mathf.RoundToInt(l / (3.4f * Ua)));
                for (int i = 1; i < nt; i++)
                {
                    float k = i / (float)nt, skw = 0.07f * (i % 2 == 1 ? 1f : 0.6f) * 2.2f;
                    Vector2 x = a + dd * k;
                    Vector2 p = x + n * rr - dd * skw, q = x - n * rr + dd * skw;
                    turns.Add((Band(3.4f, p.x, p.y, q.x, q.y), Seam));
                    turns.Add((Band(2.2f, p.x + 2.6f, p.y - 3.6f, q.x + 2.6f, q.y - 3.6f), WrapLine));
                }
            }
            AddCel(d, s + "_Forearm", lower, slot, fore, far ? WrapFar : Wrap, 2.2f * Ua, far ? 0f : 0.7f * Ua, 1, !far, Inside(fore, turns), swordLayer: sw);

            // the sleeve torn off at the elbow: its frayed end hanging in uneven points, a violet edge along the tear
            {
                Vector2 f = (el - sh).normalized, n = Nrm(sh, el) * 6.6f * Ua;
                Vector2 p = el + n, q = el - n;
                Vector2 dir = new Vector2(f.x * 0.6f, f.y * 0.6f + 0.55f).normalized;
                Vector2 back = -f * 2.4f * Ua;
                const int K = 4;
                float rag = 7f * Ua;
                var pts = new List<Vector2> { p + back, p };
                for (int k = 0; k < K; k++)
                {
                    float t0 = (k + 0.5f) / K, t1 = (k + 1f) / K, dd = Teeth[(k + 2) % Teeth.Length] * rag + 2f * Ua;
                    pts.Add(Lerp(p, q, t0) + dir * dd);
                    if (k < K - 1) pts.Add(Lerp(p, q, t1) + dir * 1.2f * Ua);
                }
                pts.Add(q); pts.Add(q + back);
                var cuff = V(Xy(pts.ToArray()));
                var edge = new List<(Vector2[] pts, Color col)>();
                if (!far)
                {
                    Vector2 e0 = p + dir * 1.4f * Ua, e1 = q + dir * 1.4f * Ua;
                    edge.Add((Band(2.6f, e0.x, e0.y, e1.x, e1.y), H.Violet));
                }
                AddCel(d, s + "_Cuff", upper, slot, cuff, tone, 2.4f * Ua, far ? 0f : 0.7f * Ua, 2, !far, edge, swordLayer: sw);
            }

            // the huge bandaged fist: an oval along the forearm, wrap lines across it, a glowing crack over the knuckles
            {
                Vector2 u = (ha - el).normalized;
                float Rf = 6.6f * Ua * 1.25f, ang = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
                Vector2 c = ha + u * Rf * 0.3f;
                var fist = Oval(c.x, c.y, Rf * 1.12f, Rf * 0.94f, ang);
                var fr = new Frame(c, u * Rf, new Vector2(-u.y, u.x) * Rf);
                var lines = new List<(Vector2[] pts, Color col)>();
                if (!far)
                {
                    foreach (float x in new[] { -0.72f, -0.3f, 0.12f, 0.52f })
                    {
                        lines.Add((Band(3.4f, fr.Xy(x - 0.18f, -1f, x + 0.18f, 1f)), Seam));
                        lines.Add((Band(2f, fr.Xy(x - 0.1f, -1f, x + 0.26f, 1f)), WrapLine));
                    }
                }
                var detail = new List<(Vector2[] pts, Color col)>(Inside(fist, lines));
                if (!far) detail.AddRange(Crack(Bloom, H.Violet, 2.4f, fr.Xy(0.86f, -0.5f, 0.66f, -0.1f, 0.82f, 0.2f, 0.62f, 0.58f)));
                AddCel(d, far ? "HandF" : "HandN_Fist", hand, far ? Slot.FarArm + 2 : Slot.NearHand, fist, far ? WrapFar : Wrap, 2.8f * Ua, far ? 0f : 0.9f * Ua,
                    0, !far, detail, swordLayer: sw);

                if (!far)
                {
                    // the loose end of the binding, trailing back from the wrist: a ribbon of cloth that swings
                    Vector2 bb = ha - u * Rf * 0.6f, nn = new(-u.y, u.x);
                    var end = new Outline()
                        .MoveTo(bb.x + nn.x * 1.6f * U, bb.y + nn.y * 1.6f * U)
                        .QuadTo(bb.x - u.x * 8f * U + nn.x * 4f * U, bb.y - u.y * 8f * U + 5f * U, bb.x - u.x * 15f * U, bb.y - u.y * 15f * U + 9f * U)
                        .QuadTo(bb.x - u.x * 8f * U + nn.x * 1f * U, bb.y - u.y * 8f * U + 4f * U, bb.x - nn.x * 1.6f * U, bb.y - nn.y * 1.6f * U)
                        .Bind();
                    var tip = new Vector2(bb.x - u.x * 15f * U, bb.y - u.y * 15f * U + 9f * U);
                    var mid = new Vector2(bb.x - u.x * 8f * U + nn.x * 2.5f * U, bb.y - u.y * 8f * U + 4.5f * U);
                    AddCloth(d, "HandN_LooseEnd", hand, Slot.NearArm + 1, end, Wrap, 1f * U, 0.5f * U,
                        new ChainFeel { stiffRoot = 0.35f, stiffTip = 0.06f, drag = 2.2f, gravity = 6f, breeze = 0.9f },
                        new[] { Xy(bb, mid, tip) }, 3, rim: false);
                }
            }
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

        // ---------------------------------------------------------------------------------------------- legs (JS animeLeg, hollowWrap, animeFoot)
        static void Leg(RigDef d, bool far)
        {
            B up = far ? B.LegFU : B.LegNU, low = far ? B.LegFL : B.LegNL, foot = far ? B.FootF : B.FootN;
            Vector2 hp = Pv(d, up), kn = Pv(d, low), an = Pv(d, foot);
            int slot = far ? Slot.FarLeg : Slot.NearLeg;
            string s = far ? "LegF" : "LegN";
            var tone = far ? FabFar : Fab;
            // the JS ankle sits just over the ground; Kage's is high in a tall boot, so the lower leg is laid knee to sole
            Vector2 ft = new(an.x, 797f);

            // the hakama over the shin, wide to the bloused hem, pleats down it
            Vector2 shinEnd = Lerp(kn, ft, 0.6f);
            var shin = Capsule(kn.x, kn.y, shinEnd.x, shinEnd.y, 9.8f * U, 10.6f * U, 1f * U, 0.5f);
            AddCel(d, s + "_Shin", low, slot, shin, tone, 4.4f * U, far ? 0f : 0.8f * U, 0, !far, far ? null : Inside(shin, Pleats(kn, shinEnd, -3.6f, 1f, 5f)));

            // the thigh over it, so the knee is the thigh's fold
            var thigh = Capsule(hp.x, hp.y, kn.x, kn.y, 11.6f * U, 10f * U, 1.6f * U, 0.42f);
            AddCel(d, s + "_Hakama", up, slot, thigh, tone, 5f * U, far ? 0f : 1f * U, 2, !far, far ? null : Inside(thigh, Pleats(hp, kn, -4.2f, 0.2f, 4.4f)));

            // the wrapped shin below the hakama
            Vector2 wa = Lerp(kn, ft, 0.4f), wb = Lerp(kn, ft, 0.93f);
            var wrap = Capsule(wa.x, wa.y, wb.x, wb.y, 6f * U, 4.6f * U, 0.3f * U, 0.4f);
            var turns = new List<(Vector2[] pts, Color col)>();
            if (!far)
            {
                Vector2 dd = wb - wa, n = Nrm(wa, wb);
                int nt = Mathf.Max(3, Mathf.RoundToInt(dd.magnitude / (3.4f * U)));
                for (int i = 1; i < nt; i++)
                {
                    float k = i / (float)nt, skw = 0.07f * (i % 2 == 1 ? 1f : 0.6f) * 1.6f;
                    Vector2 x = wa + dd * k, p = x + n * 7.2f * U - dd * skw, q = x - n * 7.2f * U + dd * skw;
                    turns.Add((Band(3.4f, p.x, p.y, q.x, q.y), Seam));
                    turns.Add((Band(2.2f, p.x + 2.6f, p.y - 3.6f, q.x + 2.6f, q.y - 3.6f), WrapLine));
                }
            }
            AddCel(d, s + "_Wrap", low, slot + 1, wrap, far ? WrapFar : Wrap, 2.2f * U, far ? 0f : 0.7f * U, 0, !far, Inside(wrap, turns));

            // the tabi and straw sandal (JS animeFootPath, stretched down to Kage's sole)
            float hh = 5f * U, hv = 55f, len = 12.5f * U;
            Vector2 F(float f, float sv) => new(an.x - f, an.y + sv);
            var o = new Outline();
            var p0 = F(-0.95f * hh, -0.7f * hv); o.MoveTo(p0.x, p0.y);
            var p1 = F(-1.05f * hh, 0.95f * hv); o.LineTo(p1.x, p1.y);
            var p2 = F(0.92f * len, 0.95f * hv); o.LineTo(p2.x, p2.y);
            var c1 = F(len + 0.5f * hh, 0.7f * hv); var e1 = F(0.82f * len, -0.05f * hv); o.QuadTo(c1.x, c1.y, e1.x, e1.y);
            var c2 = F(0.9f * hh, -0.5f * hv); var e2 = F(0.55f * hh, -0.85f * hv); o.QuadTo(c2.x, c2.y, e2.x, e2.y);
            var footShape = o.Bind();
            var sole = F(-1f * hh, 0.86f * hv); var toe = F(11f * U, 0.86f * hv);
            AddCel(d, s + "_Foot", foot, slot + 2, footShape, far ? WrapFar : Sandal, 1.8f * U, far ? 0f : 0.6f * U, 0, !far,
                Inside(footShape, new[] { (Band(1.4f * U, sole.x, sole.y, toe.x, toe.y), KMath.Hex("#3E3326")) }));
        }

        static IEnumerable<(Vector2[] pts, Color col)> Pleats(Vector2 a, Vector2 b, params float[] os)
        {
            Vector2 dd = b - a, n = Nrm(a, b);
            foreach (float o in os)
            {
                Vector2 p = a + n * o * U + dd * 0.12f, q = b + n * o * 1.1f * U;
                foreach (var f in Fold(H.ClothLit, 2.4f, p.x, p.y, q.x, q.y)) yield return f;
            }
        }

        // ---------------------------------------------------------------------------------------------- the trunk
        static void Pelvis(RigDef d)
        {
            // Kage's trunk is longer than the JS body's: the hakama's seat bridges the waist and the thighs
            var seat = RoundedAll(Frame.Ref, 812, 392, 948, 396, 966, 470, 956, 556, 880, 582, 806, 556, 794, 470);
            AddCel(d, "Pelvis", B.Hips, Slot.Pelvis, seat, Fab, 30f, 4f, 0, false);
            AddCel(d, "Neck", B.Neck, Slot.Neck, Capsule(852, 252, 858, 196, 24, 22), Fab, 6f, 0f, 0, false);
        }

        static void Torso(RigDef d)
        {
            // the gi trunk (JS ANIME_TORSO.gi), a fold under the far armpit
            var torso = RoundedAll(Sp, -0.14f, 11.6f, 0.1f, 11f, 0.36f, 10.4f, 0.62f, 11.8f, 0.82f, 13f, 0.95f, 11f, 1.03f, 6.2f, 1.07f, 3.6f,
                1.07f, -5.2f, 0.97f, -13f, 0.8f, -13.8f, 0.54f, -11.4f, 0.28f, -11.4f, 0.04f, -12.8f, -0.14f, -12.8f);
            AddCel(d, "Torso", B.Chest, Slot.Torso, torso, Fab, 7.5f * U, 1f * U, 0, true, Fold(H.ClothLit, 3f, Sp.Xy(0.8f, -2.5f, 0.5f, -4f, 0.3f, -3.2f)));

            // the robe hangs open on a black chest split by glowing cracks (JS 'veins'): the opening is cut out of the trunk
            var open = Rounded(Sp, 1.04f, 1f, 1, 0.7f, 3f, 0, 0.36f, 6f, 1, 0.36f, 15f, 1, 1.04f, 15f, 1);
            var chest = ClipConvex(torso, open);
            var cracks = new List<(Vector2[] pts, Color col)>();
            cracks.Add((Band(1.4f * U, QuadXy(Sp, 1.04f, 1f, 0.7f, 3f, 0.36f, 6f)), H.ClothLit));      // the robe's torn edge
            cracks.AddRange(Crack(Bloom, H.Violet, 2.6f, Sp.Xy(0.92f, 6f, 0.78f, 8.4f, 0.7f, 7f, 0.56f, 10.6f, 0.44f, 9f)));
            cracks.AddRange(Crack(Bloom, H.Violet, 2f, Sp.Xy(0.78f, 8.4f, 0.74f, 12f)));
            var polys = new List<(Vector2[] pts, Color col)> { (chest, Black) };
            polys.AddRange(cracks);
            d.parts.Add(new PartDef { name = "Chest_Cracks", bone = B.Chest, order = Slot.TorsoTrim * 4, role = PartRole.Body, polys = polys, rim = false });
        }

        // ---------------------------------------------------------------------------------------------- the torn robe (JS hollowRobe, cloth from the waist)
        static void Robe(RigDef d, bool back)
        {
            // JS robe: back [5 links, 11 px, 30 -> 46 wide, 6 teeth], flap [5, 10.5, 22 -> 34, 5 teeth], rag 7, hung from the hip
            int links = 5, teeth = back ? 6 : 5, seed = back ? 0 : 3;
            float w0 = (back ? 30f : 22f) * U * 0.8f, w1 = (back ? 46f : 34f) * U * 0.8f;
            Vector2 top = back ? new Vector2(918f, 412f) : new Vector2(850f, 404f), hem = back ? new Vector2(958f, 622f) : new Vector2(840f, 598f);
            var left = new float[(links + 1) * 2];
            var right = new float[(links + 1) * 2];
            var mid = new float[(links + 1) * 2];
            for (int i = 0; i <= links; i++)
            {
                float u = i / (float)links, w = (w0 + (w1 - w0) * Mathf.Pow(u, 0.85f)) * 0.5f;
                Vector2 c = Lerp(top, hem, u) + new Vector2((back ? 8f : -4f) * Mathf.Sin(u * Mathf.PI), 0f);
                left[i * 2] = c.x - w; left[i * 2 + 1] = c.y;
                right[i * 2] = c.x + w; right[i * 2 + 1] = c.y;
                mid[i * 2] = c.x; mid[i * 2 + 1] = c.y;
            }
            var panel = TornPanel(left, right, teeth, 7f * U, seed);

            var detail = new List<(Vector2[] pts, Color col)>();
            // fold lines running down the panel
            foreach (float t in new[] { 0.3f, 0.62f, 0.84f })
            {
                var line = new List<float>();
                for (int i = 1; i <= links; i++)
                {
                    float tt = t + Mathf.Sin(i * 1.3f + t * 5f) * 0.05f;
                    line.Add(Mathf.Lerp(left[i * 2], right[i * 2], tt)); line.Add(Mathf.Lerp(left[i * 2 + 1], right[i * 2 + 1], t));
                }
                detail.AddRange(Fold(back ? H.ClothDeep : H.ClothLit, 2.4f, line.ToArray()));
            }
            // the remnant of a violet band near the hem, broken where the cloth tore
            {
                int i = links - 1;
                detail.AddRange(Dashes(H.Violet.WithA(0.55f), 1.1f * U, 5f * U, 3.4f * U, left[i * 2], left[i * 2 + 1], right[i * 2], right[i * 2 + 1]));
            }
            // tears through the cloth: the dark layer behind, a violet fray on the lower lip
            foreach (var (ii, t, sz) in new[] { (3, 0.3f, 1f), (links - 1, 0.7f, 0.8f) })
            {
                float x = Mathf.Lerp(left[ii * 2], right[ii * 2], t), y = Mathf.Lerp(left[ii * 2 + 1], right[ii * 2 + 1], t) - 18f;
                float w = 3.2f * U * sz, h = 2.4f * U * sz;
                detail.Add((V(x - w, y, x - w * 0.4f, y - h * 0.7f, x, y - h * 0.2f, x + w * 0.5f, y - h, x + w, y + 0.1f * h, x + w * 0.2f, y + h * 0.5f, x - w * 0.3f, y + h * 0.9f), Black));
                detail.Add((Band(1.8f, x + w, y + 0.1f * h, x + w * 0.2f, y + h * 0.5f, x - w * 0.3f, y + h * 0.9f), H.Violet));
            }
            detail = new List<(Vector2[] pts, Color col)>(Inside(ConvexHull(panel), detail));

            var feel = back
                ? new ChainFeel { stiffRoot = 0.3f, stiffTip = 0.05f, drag = 2.2f, gravity = 5f, breeze = 0.9f }
                : new ChainFeel { stiffRoot = 0.35f, stiffTip = 0.07f, drag = 2f, gravity = 6f, breeze = 0.5f };
            var chains = new[] { Edge(left, -10f), mid, Edge(right, 10f) };
            AddCloth(d, back ? "Robe_Back" : "Robe_Flap", B.Hips, back ? 0 : Slot.FrontPlates, panel, back ? CoatB : Fab,
                (back ? 7f : 5f) * U, back ? 0f : 0.9f * U, feel, chains, back ? 0 : 1, true, detail);
        }

        /// <summary>A chain along a panel edge, pushed sideways a little so the edge vertices bind to it and not the middle one.</summary>
        static float[] Edge(float[] xy, float dx)
        {
            var r = (float[])xy.Clone();
            for (int i = 0; i < r.Length; i += 2) r[i] += dx * (i / 2) / (r.Length / 2f);
            return r;
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

        // ---------------------------------------------------------------------------------------------- the shimenawa and its shide (JS hollowSash 'shimenawa')
        static void Sash(RigDef d)
        {
            // shide: zigzag papers hanging from the rope, swinging
            Vector2 down = (Sp.Px(0f, 0f) - Sp.Px(1f, 0f)).normalized, nf = Sp.ay.normalized;
            float k = 4.2f * U * 0.62f, pw = U * 0.7f;
            int i = 0;
            foreach (float w in new[] { -8f, 1f, 9f })
            {
                Vector2 a = Sp.Px(0.02f, w);
                var zz = new List<Vector2> { a, a + nf * 3f * pw };
                foreach (var (x, y) in new[] { (3f, 1f), (0.6f, 1f), (3.4f, 2f), (1f, 2f), (3.6f, 3f), (1.4f, 3.6f), (0f, 2.3f), (2.4f, 2.3f), (-0.2f, 1.3f), (2.2f, 1.3f), (0f, 0.4f) })
                    zz.Add(a + nf * x * pw + down * y * k);
                var paper = V(Xy(zz.ToArray()));
                AddCloth(d, "Shide_" + (i + 1), B.Chest, Slot.Belt, paper, Paper, 1.2f * pw, 0.4f * pw,
                    new ChainFeel { stiffRoot = 0.4f, stiffTip = 0.1f, drag = 2.4f, gravity = 4f, breeze = 0.6f },
                    new[] { Xy(a + nf * 1.6f * pw, a + nf * 1.6f * pw + down * 1.8f * k, a + nf * 1.6f * pw + down * 3.6f * k) }, 0, rim: false);
                i++;
            }

            // the rope: a thick twisted band across the waist, the twist of its strands, a knot at the front
            // the JS band (u 0 to 0.17 across the waist) with its corners eased, as a thick rope's ends are
            var rope = RoundedAll(Sp, 0f, -12.2f, 0f, -13.4f, 0.05f, -13.6f, 0.12f, -13.6f, 0.17f, -13.4f, 0.17f, -12.2f,
                0.17f, 11.4f, 0.17f, 12.6f, 0.12f, 12.9f, 0.05f, 12.9f, 0f, 12.6f, 0f, 11.4f);
            var strands = new List<(Vector2[] pts, Color col)>();
            for (int j = 1; j < 14; j++)
            {
                float w = Mathf.Lerp(-14.6f, 13.6f, j / 14f);
                strands.Add((Band(3.2f, Sp.Xy(0f, w - 1.6f, 0.17f, w + 1.6f)), KMath.Hex("#17130D")));
                strands.Add((Band(1.8f, Sp.Xy(0.034f, w - 1.2f + 0.8f, 0.136f, w + 1.2f + 0.8f)), KMath.Hex("#4A3F2C")));
            }
            Vector2 kc = Sp.Px(0.085f, 13.6f - 2.6f);
            var detail = new List<(Vector2[] pts, Color col)>(Inside(rope, strands));
            var knot = Oval(kc.x, kc.y, 2.4f * U * 1.4f, 2.4f * U * 1.4f);
            detail.AddRange(Cel(knot, Rope, 1f * U, 0.5f * U));
            AddCel(d, "Shimenawa", B.Chest, Slot.Belt, rope, Rope, 2.2f * U, 0.6f * U, 1, true, detail, PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- the mantle (JS hollowMantle): a ragged hump over the shoulders
        static void Mantle(RigDef d)
        {
            var hump = Rounded(Sp, 1.14f, 3f, 0, 1.14f, -8f, 0, 1.04f, -18f, 0, 0.84f, -22f, 0, 0.58f, -23f, 1, 0.64f, -16f, 0, 0.48f, -17f, 1,
                0.58f, -9f, 0, 0.42f, -9f, 1, 0.56f, -2f, 0, 0.46f, 2f, 1, 0.62f, 5f, 0, 0.56f, 10f, 1, 0.72f, 9f, 0, 0.72f, 15f, 1, 0.88f, 12f, 0, 1.02f, 11f, 0);
            var detail = new List<(Vector2[] pts, Color col)>();
            foreach (var (a, b, c) in new[] { ((1.02f, -6f), (0.8f, -10f), (0.6f, -14f)), ((1.0f, 2f), (0.8f, -2f), (0.6f, -4f)), ((0.98f, 8f), (0.84f, 6f), (0.66f, 8f)) })
                detail.AddRange(Fold(MantleT.lit, 2.6f, QuadXy(Sp, a.Item1, a.Item2, b.Item1, b.Item2, c.Item1, c.Item2)));
            detail.AddRange(Dashes(H.Violet, 1.4f * U, 3f * U, 2f * U, Sp.Xy(0.68f, -18f, 0.66f, -6f)));
            float[] Ch(params float[] uw) => Sp.Xy(uw);
            AddCloth(d, "Mantle", B.Chest, Slot.BeltTrim, hump, MantleT, 6f * U, 1f * U,
                new ChainFeel { stiffRoot = 0.6f, stiffTip = 0.2f, drag = 2f, gravity = 2.5f, breeze = 0.5f },
                new[] { Ch(1.12f, -12f, 0.84f, -20f, 0.58f, -23f), Ch(1.12f, -4f, 0.8f, -12f, 0.48f, -17f), Ch(1.12f, 0f, 0.8f, -6f, 0.42f, -9f),
                        Ch(1.12f, 4f, 0.8f, 2f, 0.46f, 2f), Ch(1.12f, 8f, 0.8f, 9f, 0.56f, 10f), Ch(1.1f, 11f, 0.9f, 13f, 0.72f, 15f) },
                0, true, detail);
        }

        // ---------------------------------------------------------------------------------------------- the head (JS drawHollowHead 'brute' over a gaunt, heavy-jawed face)
        static void Head(RigDef d)
        {
            // the face: the JS gaunt profile with the husk's numbers (nose 0.98, lip 0.95, chin 1.35, jaw 1.35, brow 2.1, open 0.13)
            float rf = R * 0.9f;
            var face = FacePath(Frame.Head(HeadX, HeadY, rf));
            var lit = Inset(face, new Vector2(0.99f, 0.14f).normalized, Mathf.Max(2.5f, 0.1f * rf) / KageModel.RefScale, 0f);
            d.parts.Add(new PartDef
            {
                name = "Face", bone = B.Head, order = Slot.Head * 4, role = PartRole.Body, rim = false,
                polys = new List<(Vector2[] pts, Color col)> { (MeshKit.Grow(face, 0.004f), Seam), (face, FaceLight), (lit, Black) },
            });

            // the far horn stub, behind the cowl
            AddCel(d, "Horn_Far", B.Head, Slot.Head, Rounded(Hd, -0.36f, -0.86f, 0, -0.66f, -1.2f, 0, -0.74f, -1.42f, 1, -0.62f, -1.36f, 1, -0.5f, -1.46f, 1, -0.34f, -1.16f, 0, -0.14f, -0.98f, 0), MaskFar, R * 0.2f, 0f, 1, true, null, PartRole.Metal);

            // the heavy cowl, open at the face, a dashed violet seam over it
            var cowl = RoundedAll(Hd, 0.6f, -0.84f, 0.1f, -1.2f, -0.7f, -1.12f, -1.24f, -0.5f, -1.34f, 0.4f, -1.2f, 1.1f, -0.4f, 1.3f, 0.1f, 1.22f,
                -0.04f, 0.8f, -0.12f, 0.3f, -0.04f, -0.3f, 0.36f, -0.66f);
            AddCel(d, "Cowl", B.Head, Slot.Head, cowl, Fab, R * 0.3f, R * 0.05f, 2, true,
                Inside(ConvexHull(cowl), Dashes(H.Violet, R * 0.06f, R * 0.3f, R * 0.14f, QuadXy(Hd, 0.5f, -0.72f, -0.2f, -0.6f, -0.5f, 0.6f, 16))));

            // the horned brow mask: the face grown 1.1 times, broken off over the eyes (JS HOLLOW_BROW_CUT), lacquer with eye holes,
            // a lit front edge and a glowing crack
            var top = FaceTop(Frame.Head(HeadX, HeadY, rf * 1.1f));
            var cut = Rounded(Hd, -0.12f, -1.6f, 1, 1.8f, -1.6f, 1, 1.8f, -0.02f, 1, 1.0f, 0.0f, 1, 0.86f, -0.1f, 1, 0.7f, 0.02f, 1, 0.24f, -0.04f, 1, 0.08f, 0.1f, 1, -0.1f, -0.2f, 1);
            var mask = ClipConvex(cut, top);
            var md = new List<(Vector2[] pts, Color col)>();
            foreach (var (x, y, w, h) in new[] { (rf * 0.48f, rf * 0.08f, rf * 0.5f, rf * 0.3f), (rf * 0.88f, rf * 0.08f - rf * 0.03f, rf * 0.22f, rf * 0.24f) })
            {
                var eh = Frame.Head(HeadX, HeadY, 1f);
                var hole = new Outline(eh).MoveTo(x + w * 0.55f, y + h * 0.2f)
                    .QuadTo(x + w * 0.05f, y - h, x - w * 0.55f, y - h * 0.65f)
                    .QuadTo(x - w * 0.05f, y + h * 0.9f, x + w * 0.55f, y + h * 0.2f).Bind();
                var c = ClipConvex(hole, mask.Length >= 3 ? ConvexHull(mask) : hole);
                if (c.Length >= 3) md.Add((c, Black));
            }
            // the lacquer: the shadow tone over the back, a strip of light down its front (JS enemyEdgeLight), the eye holes over it
            Vector2 front = new Vector2(0.99f, 0.14f).normalized;
            var unlit = Inset(mask, front, Mathf.Max(2.5f, R * 0.09f) / KageModel.RefScale, 0f);
            var maskPolys = new List<(Vector2[] pts, Color col)>
            {
                (MeshKit.Grow(mask, 1.6f / KageModel.RefScale), Seam),
                (mask, KMath.Hex("#B9A8FF")),
                (unlit, Mask.shade),
                (Inset(unlit, ShadeDir, R * 0.2f / KageModel.RefScale, 0f), Mask.mid),
            };
            maskPolys.AddRange(md);
            maskPolys.AddRange(Crack(Bloom, H.Violet, Mathf.Max(1.6f, R * 0.035f), Hd.Xy(0.78f, -1.0f, 0.68f, -0.6f, 0.8f, -0.34f, 0.7f, -0.04f)));
            d.parts.Add(new PartDef { name = "Mask", bone = B.Head, order = Slot.HeadTop * 4, role = PartRole.Body, polys = maskPolys, rim = false, rimPts = mask });

            // the near horn stub over it
            AddCel(d, "Horn_Near", B.Head, Slot.HeadTop, Rounded(Hd, 0.2f, -0.92f, 0, 0.1f, -1.24f, 0, 0.08f, -1.54f, 1, 0.2f, -1.5f, 1, 0.32f, -1.62f, 1, 0.38f, -1.36f, 0, 0.54f, -0.96f, 0), Mask, R * 0.2f, R * 0.07f, 1, true, null, PartRole.Metal);

            // the eye: the JS angry triangle, white fading to violet at the inner corner
            {
                float ex = rf * 0.56f, ey = rf * 0.08f, ew = rf * 0.42f, eh = rf * 0.24f;
                var e = Frame.Head(HeadX, HeadY, 1f);
                Vector2 o = e.Px(ex - ew * 0.5f, ey - eh * 0.62f), i = e.Px(ex + ew * 0.5f, ey + eh * 0.12f), b = e.Px(ex - ew * 0.08f, ey + eh * 0.5f);
                Add(d, "Eye", B.Head, Slot.Face, H.Eye, V(o.x, o.y, i.x, i.y, b.x, b.y), 0, PartRole.Eye, false, null, 3.2f);
            }
        }

        /// <summary>The JS <c>animeFacePath</c> with the Husk's profile, in a head frame of face radius (closed outline).</summary>
        static Vector2[] FacePath(Frame f)
        {
            const float nose = 0.98f, lip = 0.95f, ch = 1.35f, jw = 1.35f, br = 2.1f, open = 0.13f;
            float bx = 0.95f + 0.035f * (br - 1f), tx = 0.93f + 0.2f * nose, ty = 0.36f;
            float cx = 0.6f + 0.13f * (ch - 1f), cy = 1.1f + 0.07f * (ch - 1f) + open * 0.5f;
            return new Outline(f).MoveTo(-0.94f, 0.12f)
                .CubicTo(-1.02f, -0.76f, -0.42f, -1.06f, 0.12f, -1.03f)
                .CubicTo(0.66f, -0.98f, bx + 0.01f, -0.6f, bx, -0.2f)
                .LineTo(0.93f, 0.04f).LineTo(tx, ty).LineTo(0.94f, ty + 0.09f).LineTo(0.9f + 0.07f * lip, 0.6f)
                .LineTo(0.84f, 0.645f).LineTo(0.89f + 0.05f * lip, 0.69f + open).LineTo(0.88f, 0.7f + open * 1.25f)
                .QuadTo(0.86f + 0.09f * (ch - 1f), 0.98f + 0.04f * (ch - 1f) + open * 0.6f, cx, cy)
                .QuadTo(0.1f, 0.98f + 0.2f * (jw - 1f) + open * 0.3f, -(0.36f + 0.12f * (jw - 1f)), 0.72f + 0.1f * (jw - 1f))
                .QuadTo(-0.82f, 0.6f, -0.94f, 0.12f)
                .Bind();
        }

        /// <summary>The convex top of the face (crown, forehead, brow, the bridge of the nose), closed straight across: the clip for a brow mask.</summary>
        static Vector2[] FaceTop(Frame f)
        {
            const float br = 2.1f;
            float bx = 0.95f + 0.035f * (br - 1f);
            return new Outline(f).MoveTo(-0.94f, 0.12f)
                .CubicTo(-1.02f, -0.76f, -0.42f, -1.06f, 0.12f, -1.03f)
                .CubicTo(0.66f, -0.98f, bx + 0.01f, -0.6f, bx, -0.2f)
                .LineTo(0.93f, 0.04f)
                .Bind();
        }
    }
}
