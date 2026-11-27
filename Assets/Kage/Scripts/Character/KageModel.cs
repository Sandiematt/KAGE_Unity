using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kage, rebuilt in code from the reference art (kage.png / kage.svg, 1774x887).
    /// Every coordinate below was measured on the reference in image pixels (x right, y down,
    /// character facing left) and is converted to Unity units facing right by <see cref="P"/>.
    /// The SVG itself is never loaded: the shapes are re-authored as polygons on a bone rig.
    /// </summary>
    public static class KageModel
    {
        const float Scale = 400f;          // 400 reference px = 1 Unity unit (Kage is ~1.9 units tall)
        const float OriginX = 880f, OriginY = 815f;   // ground point between the feet

        /// <summary>The reference-image scale and ground point, for art authored in other frames (helmet visuals).</summary>
        public const float RefScale = Scale, OriginXRef = OriginX, OriginYRef = OriginY;

        public static Vector2 P(float x, float y) => new((OriginX - x) / Scale, (OriginY - y) / Scale);

        static Vector2[] V(params float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = P(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        public enum Look { Kage, Ronin }

        /// <summary>
        /// The gameplay skeleton alone (SAN-69): the 20 bones' pivots and parents, the hurt circles, and the fist, hilt, foot and
        /// blade hit points, in bind pose, with no parts. Every part set draws on this one skeleton: art never moves a joint.
        /// </summary>
        public static RigDef Skeleton(string name)
        {
            var d = new RigDef { name = name };
            void Bone(B b, B parent, float x, float y) { d.pivot[(int)b] = P(x, y); d.parent[(int)b] = parent; }
            Bone(B.Root, B.Root, 880, 815);
            Bone(B.Hips, B.Root, 878, 520);
            Bone(B.Chest, B.Hips, 878, 395);
            Bone(B.Neck, B.Chest, 852, 222);
            Bone(B.Head, B.Neck, 852, 205);
            Bone(B.ArmNU, B.Chest, 905, 268);
            Bone(B.ArmNL, B.ArmNU, 926, 375);
            Bone(B.HandN, B.ArmNL, 914, 442);
            Bone(B.ArmFU, B.Chest, 862, 272);
            Bone(B.ArmFL, B.ArmFU, 822, 385);
            Bone(B.HandF, B.ArmFL, 797, 455);
            Bone(B.Sword, B.HandF, 787, 478);
            Bone(B.LegNU, B.Hips, 848, 545);
            Bone(B.LegNL, B.LegNU, 835, 680);
            Bone(B.FootN, B.LegNL, 835, 763);
            Bone(B.LegFU, B.Hips, 905, 545);
            Bone(B.LegFL, B.LegFU, 955, 680);
            Bone(B.FootF, B.LegFL, 955, 763);
            Bone(B.CoatN, B.Hips, 835, 398);
            Bone(B.CoatF, B.Hips, 935, 398);

            HurtDef H(B b, float x, float y, float r, HitZone z) => new(b, P(x, y) - d.pivot[(int)b], r, z);
            d.hurt.Add(H(B.Head, 852, 168, 0.13f, HitZone.Head));
            d.hurt.Add(H(B.Chest, 880, 300, 0.165f, HitZone.Body));
            d.hurt.Add(H(B.Hips, 878, 455, 0.16f, HitZone.Body));
            d.hurt.Add(H(B.LegNU, 842, 612, 0.1f, HitZone.Legs));
            d.hurt.Add(H(B.LegNL, 835, 725, 0.085f, HitZone.Legs));
            d.hurt.Add(H(B.LegFU, 925, 612, 0.1f, HitZone.Legs));
            d.hurt.Add(H(B.LegFL, 957, 725, 0.085f, HitZone.Legs));

            d.fistLocal = P(902, 468) - d.pivot[(int)B.HandN];
            d.hiltLocal = P(792, 474) - d.pivot[(int)B.HandF];
            d.footTipLocal = P(788, 800) - d.pivot[(int)B.FootN];
            d.heelLocal = P(860, 806) - d.pivot[(int)B.FootN];

            // the blade's line: reference guard (772,500) -> tip (522,658), mirrored to face right; its sword-bone local ends
            float ang = KMath.Ang(P(522, 658) - P(772, 500));
            d.bladeBindAngle = ang;
            d.bladeBaseLocal = KMath.Rot(new Vector2(0.08f, 0f), ang);
            d.bladeTipLocal = KMath.Rot(new Vector2(0.80f, 0.03f), ang);
            return d;
        }

        public static RigDef Build(Look look)
        {
            bool ronin = look == Look.Ronin;
            var d = Skeleton(ronin ? "RONIN" : "KAGE");
            Color trim = ronin ? Palette.Amber : Palette.Trim;
            Color trimDeep = ronin ? Palette.AmberDeep : Palette.TrimDeep;
            Color body = Palette.Body, far = Palette.BodyFar;
            d.rimColor = ronin ? Palette.Amber : Palette.Rim;
            d.bodyColor = body;

            void Part(string name, B bone, int order, Color c, PartRole role, Vector2[] pts, bool rim = true, int sub = 0, Color? rimCol = null, float glow = 1f, bool swordLayer = false)
            {
                d.parts.Add(new PartDef
                {
                    name = name, bone = bone, order = order * 4 + sub, color = c, role = role, pts = pts,
                    rim = rim, rimColor = rimCol, glow = glow, swordLayer = swordLayer
                });
            }

            // ---------- far (katana) arm, behind the torso ----------
            Part("ArmF_Upper", B.ArmFU, 1, far, PartRole.Body, V(845,262, 882,268, 866,330, 842,392, 806,382, 826,322), swordLayer: true);
            Part("ArmF_Cuff", B.ArmFU, 1, trimDeep, PartRole.TrimDeep, V(810,360, 850,370, 843,394, 805,385), false, 1, swordLayer: true);
            Part("ArmF_Fore", B.ArmFL, 2, far, PartRole.Body, V(806,382, 842,390, 816,440, 806,462, 786,454, 796,420), swordLayer: true);
            Part("ArmF_Wrist", B.ArmFL, 2, trim, PartRole.Trim, V(791,440, 815,446, 810,459, 786,452), false, 1, swordLayer: true);
            Part("HandF", B.HandF, 3, far, PartRole.Body, V(780,450, 810,452, 814,478, 802,494, 780,490, 772,470), swordLayer: true);
            BuildSword(d, ronin);

            // ---------- far / rear leg ----------
            Part("LegF_Thigh", B.LegFU, 5, far, PartRole.Body, V(875,540, 940,540, 968,610, 982,676, 940,676, 912,630, 886,590));
            Part("LegF_Shin", B.LegFL, 6, far, PartRole.Body, V(934,668, 980,668, 979,720, 976,766, 938,766, 935,720));
            Part("LegF_KneeStrap", B.LegFL, 6, trimDeep, PartRole.TrimDeep, V(931,662, 984,662, 984,690, 931,690), false, 1);
            Part("LegF_AnkleStrap", B.LegFL, 6, trimDeep, PartRole.TrimDeep, V(934,742, 978,742, 978,760, 934,760), false, 1);
            Part("FootF_Boot", B.FootF, 7, far, PartRole.Boot, V(934,752, 978,752, 988,790, 994,816, 904,816, 900,804, 916,792, 934,782), rimCol: Palette.BootRim * 0.7f);

            // ---------- pelvis + coat back ----------
            Part("Pelvis", B.Hips, 8, body, PartRole.Body, V(812,392, 952,392, 946,566, 816,566), false);

            // ---------- torso ----------
            Part("Torso", B.Chest, 11, body, PartRole.Body, V(834,226, 870,214, 908,220, 932,240, 946,268, 950,310, 951,355, 953,425, 880,428, 808,425, 807,360, 812,322, 818,290, 824,260));
            Part("Lapel", B.Chest, 12, trim, PartRole.Trim, V(838,238, 850,236, 860,300, 862,398, 850,398, 846,300), false);
            Part("Collar", B.Chest, 12, trimDeep, PartRole.TrimDeep, V(822,250, 842,232, 853,262, 838,292), false, 1);
            if (ronin)
                Part("Mantle", B.Chest, 12, Palette.Straw, PartRole.Body, V(818,228, 952,232, 988,300, 966,322, 944,300, 922,334, 896,302, 866,328, 842,292, 812,300), true, 2);

            // ---------- near / lead leg ----------
            Part("LegN_Thigh", B.LegNU, 13, body, PartRole.Body, V(812,540, 884,540, 878,600, 866,650, 858,676, 816,676, 806,630, 806,580));
            Part("LegN_Shin", B.LegNL, 14, body, PartRole.Body, V(814,668, 860,668, 858,720, 854,766, 818,766, 814,720));
            Part("LegN_KneeStrap", B.LegNL, 14, trim, PartRole.Trim, V(811,662, 863,662, 862,690, 812,690), false, 1);
            Part("LegN_KneeBuckle", B.LegNL, 14, Palette.Silver, PartRole.Metal, V(807,659, 823,659, 823,693, 807,693), false, 2);
            Part("LegN_AnkleStrap", B.LegNL, 14, trim, PartRole.Trim, V(814,742, 857,742, 857,760, 814,760), false, 1);
            Part("FootN_Boot", B.FootN, 15, body, PartRole.Boot, V(814,752, 858,752, 862,790, 865,816, 774,816, 770,804, 788,792, 812,782), rimCol: Palette.BootRim);
            Part("FootN_Sole", B.FootN, 15, KMath.Hex("#2A2427"), PartRole.Detail, V(770,808, 865,808, 865,816, 774,816), false, 1);

            // ---------- coat: hand-cut panels + the SVG's own trim and cloud pattern, cloth driven ----------
            BuildCoat(d, ronin);

            // ---------- belt ----------
            Part("Belt", B.Chest, 18, ronin ? trimDeep : KMath.Hex("#B8323A"), PartRole.Trim, V(805,366, 953,370, 954,397, 805,395), false);
            Part("Belt_Line", B.Chest, 18, ronin ? Palette.Straw : trimDeep, PartRole.TrimDeep, V(805,378, 953,382, 953,386, 805,383), false, 1);
            Part("Buckle", B.Chest, 19, Palette.Silver, PartRole.Metal, V(808,363, 834,363, 834,399, 808,399), false);
            Part("Buckle_In", B.Chest, 19, ronin ? trimDeep : KMath.Hex("#B8323A"), PartRole.Trim, V(813,369, 829,369, 829,393, 813,393), false, 1);
            Part("Belt_Loop", B.Chest, 19, Palette.Silver, PartRole.Metal, V(856,371, 866,371, 866,396, 856,396), false);

            // ---------- head ----------
            Part("Neck", B.Neck, 20, body, PartRole.Body, V(838,200, 868,200, 872,240, 842,240), false);
            if (!ronin) BuildHeadFromSvg(d);
            else
            {
                Part("Head", B.Head, 21, body, PartRole.Body, V(813,146, 805,160, 806,178, 799,191, 806,197, 809,205, 818,214, 832,223, 852,225, 872,216, 890,198, 899,172, 896,146, 882,126, 858,116, 833,121));
                Part("Hat", B.Head, 23, Palette.Straw, PartRole.Hair, V(748,170, 796,130, 846,94, 868,92, 918,128, 968,166, 958,176, 900,158, 860,150, 806,166, 758,182));
                Part("Hat_Band", B.Head, 23, trimDeep, PartRole.TrimDeep, V(808,150, 900,142, 902,152, 808,162), false, 1);
                Part("Eye", B.Head, 24, Palette.Amber, PartRole.Eye, V(825,180, 831,171, 840,165, 850,162, 846,173, 836,181), false, 0, null, 3.2f);
            }

            // ---------- scarf wrap (tails are simulated by ScarfRibbon) ----------
            if (!ronin)
            {
                Part("Scarf_Wrap", B.Chest, 25, trim, PartRole.Trim, V(818,228, 843,223, 871,214, 915,203, 946,207, 958,217, 953,233, 927,237, 900,234, 865,242, 832,254, 822,245), rimCol: Palette.TrimBright);
                Part("Scarf_Fold", B.Chest, 25, trimDeep, PartRole.TrimDeep, V(823,233, 853,230, 889,217, 936,211, 905,219, 876,231, 850,238, 830,246), false, 1);
                Part("Scarf_Knot", B.Chest, 25, trim, PartRole.Trim, V(942,204, 960,207, 968,220, 962,236, 948,234, 944,220), false, 2);
                Part("Scarf_KnotShade", B.Chest, 25, trimDeep, PartRole.TrimDeep, V(950,214, 962,218, 960,230, 952,228), false, 3);
                d.scarf = true;
                d.scarfAnchorA = P(944, 212);
                d.scarfAnchorB = P(948, 234);
            }
            else d.scarf = false;

            // ---------- near (fist) arm ----------
            Part("ArmN_Upper", B.ArmNU, 26, body, PartRole.Body, V(884,258, 924,248, 944,286, 950,334, 950,372, 904,380, 897,334, 888,296));
            Part("ArmN_Cuff", B.ArmNU, 26, trim, PartRole.Trim, V(901,352, 950,350, 952,390, 903,392), false, 1);
            Part("ArmN_CuffLine", B.ArmNU, 26, trimDeep, PartRole.TrimDeep, V(902,368, 951,366, 951,372, 902,374), false, 2);
            Part("ArmN_Fore", B.ArmNL, 27, body, PartRole.Body, V(905,385, 942,385, 934,420, 930,446, 902,446, 900,415));
            Part("ArmN_Wrist", B.ArmNL, 27, trim, PartRole.Trim, V(900,430, 932,430, 932,447, 900,447), false, 1);
            Part("HandN_Fist", B.HandN, 28, body, PartRole.Body, V(888,442, 926,440, 932,462, 924,486, 898,490, 880,480, 877,458));

            return d;
        }

        /// <summary>
        /// Recolours a built rig: every part, painted polygon, rim and scarf colour equal to <paramref name="from"/>
        /// becomes <paramref name="to"/> (a part set's trim in the character's colours). Nothing changes when they're equal.
        /// </summary>
        public static void Recolour(RigDef d, Color from, Color to)
        {
            if (from == to) return;
            Color Swap(Color c) => c == from ? new Color(to.r, to.g, to.b, c.a) : c;
            foreach (var p in d.parts)
            {
                p.color = Swap(p.color);
                if (p.rimColor.HasValue) p.rimColor = Swap(p.rimColor.Value);
                if (p.polys == null) continue;
                for (int i = 0; i < p.polys.Count; i++) p.polys[i] = (p.polys[i].pts, Swap(p.polys[i].col));
            }
            d.rimColor = Swap(d.rimColor);
            d.scarfColor = Swap(d.scarfColor);
            d.scarfBack = Swap(d.scarfBack);
        }

        // ---------------------------------------------------------------------------------------
        static Vector2[] FromSvg(float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = P(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        static Vector2[] Shift(Vector2[] pts, Vector2 by)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < r.Length; i++) r[i] = pts[i] + by;
            return r;
        }

        /// <summary>Kage's head and hair exactly as drawn: the SVG's 99 head shapes (silhouette, shaded
        /// planes, red highlight strokes, eye) painted in SVG order into one deformable mesh.</summary>
        static void BuildHeadFromSvg(RigDef d)
        {
            d.hairCentre = P(862, 150);
            var polys = new List<(Vector2[] pts, Color col)>();
            foreach (var sp in KageSvg.Hair)
            {
                var pts = FromSvg(sp.xy);
                if (sp.role == 6)
                {
                    d.parts.Add(new PartDef { name = "Eye", bone = B.Head, order = 24 * 4, color = Palette.Eye, role = PartRole.Eye, pts = pts, rim = false, glow = 3.6f });
                    continue;
                }
                Color c = new Color32(sp.r, sp.g, sp.b, 255);
                if (sp.role >= 4) c = Color.Lerp(c, Palette.TrimBright, 0.18f);     // keep the strokes readable against a red sky
                polys.Add((pts, c));
            }
            d.parts.Add(new PartDef { name = "Head_Hair", bone = B.Head, order = 21 * 4, role = PartRole.Hair, polys = polys, soft = SoftKind.Hair, rim = false });
        }

        /// <summary>
        /// The long coat, cut as three panels that reach mid-calf:
        /// a front flap in front of the lead leg (carrying the lapel trim down to the hem), a side panel
        /// wrapping the hip over the rear leg, and the far half of the coat behind both legs showing its
        /// deep-red lining. Clean hem bands, fold lines and a cloud motif replace the old knee-length skirt.
        /// The flap rides one cloth patch; the side and back panels share a second, looser one, so the
        /// tails split from the flap at the hip and fly on their own.
        /// </summary>
        static void BuildCoat(RigDef d, bool ronin)
        {
            d.softCoat = true;
            d.coatF0 = P(806, 398); d.coatF1 = P(780, 736);      // front flap patch
            d.coatB0 = P(890, 398); d.coatB1 = P(848, 738);
            d.coatTail = true;
            d.tailF0 = P(876, 398); d.tailF1 = P(928, 740);      // tail patch (side + back panels)
            d.tailB0 = P(953, 398); d.tailB1 = P(1080, 724);

            Color cloth = KMath.Hex("#1A161B"), side = KMath.Hex("#151116"), inside = KMath.Hex("#120A0C");
            Color fold = KMath.Hex("#2B242A"), foldDark = KMath.Hex("#0C090B");
            Color trim = ronin ? Palette.Amber : Palette.Trim;
            Color trimDeep = ronin ? Palette.AmberDeep : Palette.TrimDeep;
            Color motif = Color.Lerp(trimDeep, trim, 0.55f);
            Color rim = d.rimColor;
            Vector2 light = new Vector2(-0.0055f, 0.0055f);

            // ---- back: the far half of the coat, behind both legs (inside / lining shows) ----
            var back = Dense(V(870,398, 953,398, 976,450, 1004,522, 1034,600, 1060,672, 1082,726, 1046,734, 1000,746, 950,750, 906,744, 890,660, 880,560, 874,470));
            var backPolys = new List<(Vector2[] pts, Color col)>
            {
                (Shift(MeshKit.Grow(back, 0.004f), light), rim.Mul(0.75f)),
                (back, inside),
                (Band(10f, 968,424, 994,500, 1022,578, 1050,656, 1074,716), trimDeep),          // lining along the back edge
                (Band(9f, 908,738, 950,744, 1000,740, 1046,728, 1078,719), trimDeep),           // hem
            };
            d.parts.Add(new PartDef { name = "Coat_Back", bone = B.Hips, order = 4 * 4 + 3, role = PartRole.Body, polys = backPolys, soft = SoftKind.Coat, clothLayer = 1, rim = false });

            // ---- side panel: wraps the hip, over the rear leg, behind the lead leg ----
            var sidePanel = Dense(V(876,398, 953,398, 966,444, 986,512, 1008,590, 1030,664, 1056,726, 1010,738, 964,744, 928,740, 916,690, 904,630, 892,560, 882,470));
            var sidePolys = new List<(Vector2[] pts, Color col)>
            {
                (Shift(MeshKit.Grow(sidePanel, 0.0045f), light), rim),
                (sidePanel, side),
                (Band(3f, 930,430, 956,530, 982,630, 1006,716), fold),                          // folds falling from the hip
                (Band(2.5f, 904,500, 918,600, 936,705), fold),
                (Band(2f, 962,470, 990,580, 1018,690), foldDark),
                (Band(3f, 894,575, 906,640, 918,690, 928,736), trimDeep),                       // slit edge
                (Band(3f, 929,719, 964,723, 1008,717, 1048,708), trimDeep),                      // hem: thin line + band
                (Band(10f, 928,733, 964,737, 1010,731, 1052,721), trim),
            };
            AddCloudPolys(sidePolys, motif, 1000, 690, 15f);
            AddCloudPolys(sidePolys, motif, 956, 702, 10f);
            d.parts.Add(new PartDef { name = "Coat_Side", bone = B.Hips, order = 8 * 4 + 1, role = PartRole.Body, polys = sidePolys, soft = SoftKind.Coat, clothLayer = 1, rim = false });

            // ---- front flap: in front of the lead leg, the lapel trim runs on down to the hem ----
            var flap = Dense(V(806,398, 890,398, 892,470, 892,560, 882,620, 864,680, 848,738, 816,744, 780,736, 790,650, 798,560, 802,470));
            var front = new List<(Vector2[] pts, Color col)>
            {
                (Shift(MeshKit.Grow(flap, 0.0045f), light), rim),
                (flap, cloth),
                (Band(2.5f, 872,430, 874,520, 868,600), fold),
                (Band(12f, 856,398, 853,470, 842,560, 822,650, 800,730), trim),                 // lapel sweeping to the front hem
                (Band(2.5f, 864,404, 861,470, 851,560, 832,650, 811,726), trimDeep),
                (Band(3f, 890,560, 880,620, 863,680, 848,734), trimDeep),                       // slit edge
                (Band(9f, 784,728, 816,735, 846,730), trim),                                     // hem band
            };
            d.parts.Add(new PartDef { name = "Coat", bone = B.Hips, order = 16 * 4, role = PartRole.Body, polys = front, soft = SoftKind.Coat, clothLayer = 0, rim = false });
        }

        /// <summary>Subdivides a closed polygon so no edge is longer than maxLen (units): cloth bends smoothly.</summary>
        static Vector2[] Dense(Vector2[] pts, float maxLen = 0.03f)
        {
            var r = new List<Vector2>();
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / maxLen));
                for (int k = 0; k < n; k++) r.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            return r.ToArray();
        }

        /// <summary>A strip of the given width (reference px) along an open polyline (reference px), tapered at the ends.</summary>
        static Vector2[] Band(float widthPx, params float[] xy)
        {
            var c = new List<Vector2>();
            for (int i = 0; i + 3 < xy.Length; i += 2)
            {
                Vector2 a = new(xy[i], xy[i + 1]), b = new(xy[i + 2], xy[i + 3]);
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 10f));
                for (int k = 0; k < n; k++) c.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            c.Add(new Vector2(xy[xy.Length - 2], xy[xy.Length - 1]));
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < c.Count; i++)
            {
                Vector2 t = (c[Mathf.Min(c.Count - 1, i + 1)] - c[Mathf.Max(0, i - 1)]).normalized;
                Vector2 n = new Vector2(-t.y, t.x);
                float u = i / (float)(c.Count - 1);
                float w = widthPx * 0.5f * Mathf.Lerp(0.55f, 1f, Mathf.Min(1f, Mathf.Min(u, 1f - u) * 6f));
                left.Add(P(c[i].x + n.x * w, c[i].y + n.y * w));
                right.Add(P(c[i].x - n.x * w, c[i].y - n.y * w));
            }
            right.Reverse();
            left.AddRange(right);
            return left.ToArray();
        }

        static Vector2[] ArcPts(float cx, float cy, float r, float th, float a0, float a1)
        {
            const int seg = 14;
            var pts = new List<Vector2>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                pts.Add(P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            for (int i = seg; i >= 0; i--)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                pts.Add(P(cx + Mathf.Cos(a) * (r - th), cy + Mathf.Sin(a) * (r - th)));
            }
            return pts.ToArray();
        }

        /// <summary>The cloud swirl motif (outer swirl, inner swirl, curl) painted into a soft mesh.</summary>
        static void AddCloudPolys(List<(Vector2[] pts, Color col)> into, Color c, float cx, float cy, float r)
        {
            into.Add((ArcPts(cx, cy, r, 3.0f, 200f, 470f), c));
            into.Add((ArcPts(cx, cy, r * 0.56f, 2.6f, 20f, 290f), c));
            into.Add((ArcPts(cx + r * 0.95f, cy + r * 0.25f, r * 0.42f, 2.4f, 120f, 330f), c));
        }

        static void AddHair(RigDef d, Color body)
        {
            // (root x, root y) -> (tip x, tip y), width : measured spike by spike on the reference
            float[,] s =
            {
                { 838,130, 787,102, 16 }, { 832,142, 780,147, 12 }, { 834,136, 790,128, 12 },
                { 846,120, 812, 83, 18 }, { 856,114, 833, 63, 18 }, { 866,111, 870, 60, 20 },
                { 874,113, 887, 69, 14 }, { 882,116, 925, 53, 20 }, { 890,122, 945, 77, 18 },
                { 896,130, 982, 92, 20 }, { 900,140, 960,120, 14 }, { 903,150, 990,137, 20 },
                { 904,162, 970,167, 18 }, { 900,174, 960,187, 14 }, { 894,182, 942,197, 12 },
                { 888,190, 925,203, 10 },
            };
            for (int i = 0; i < s.GetLength(0); i++)
            {
                var root = new Vector2(s[i, 0], s[i, 1]);
                var tip = new Vector2(s[i, 2], s[i, 3]);
                if (tip.x > root.x) tip = root + (tip - root) * 1.18f + new Vector2(0f, -4f);   // sweep the mane back like the art
                float w = s[i, 4];
                var px = SpikePx(root, tip, w);
                var pts = new Vector2[px.Count];
                for (int k = 0; k < pts.Length; k++) pts[k] = P(px[k].x, px[k].y);
                d.parts.Add(new PartDef
                {
                    name = "Hair_Spike_" + i, bone = B.Head, order = 23 * 4 + (i % 2), color = body, role = PartRole.Hair,
                    pts = pts, rim = true, rimGrow = 0.004f, jigglePivot = P(root.x, root.y), jiggle = 0.6f + 0.05f * (i % 5)
                });
            }
        }

        static List<Vector2> SpikePx(Vector2 root, Vector2 tip, float w)
        {
            Vector2 dir = tip - root;
            float len = dir.magnitude;
            Vector2 n = new Vector2(-dir.y, dir.x) / len;
            if (n.y < 0) n = -n;                       // bend towards image-down (gravity side)
            Vector2 ctrl = root + dir * 0.5f + n * (0.1f * len);
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            const int steps = 5;
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)(steps + 1);
                Vector2 c = (1 - t) * (1 - t) * root + 2 * (1 - t) * t * ctrl + t * t * tip;
                Vector2 tan = 2 * (1 - t) * (ctrl - root) + 2 * t * (tip - ctrl);
                Vector2 nn = new Vector2(-tan.y, tan.x).normalized;
                float hw = w * 0.5f * Mathf.Pow(1f - t, 0.85f);
                left.Add(c + nn * hw);
                right.Add(c - nn * hw);
            }
            var r = new List<Vector2>(left) { tip };
            for (int k = right.Count - 1; k >= 0; k--) r.Add(right[k]);
            return r;
        }

        static void AddCloud(RigDef d, B bone, int order, Color c, float cx, float cy, float r)
        {
            // outer swirl, inner swirl, curl: open arcs read as the cloud pattern on the coat hem
            AddArc(d, bone, order, c, cx, cy, r, 3.2f, 200f, 520f);
            AddArc(d, bone, order, c, cx, cy, r * 0.58f, 2.8f, 20f, 300f);
            AddArc(d, bone, order, c, cx + r * 0.95f, cy + r * 0.25f, r * 0.42f, 2.6f, 120f, 330f);
        }

        static void AddArc(RigDef d, B bone, int order, Color c, float cx, float cy, float r, float th, float a0, float a1)
        {
            const int seg = 14;
            var pts = new List<Vector2>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                pts.Add(P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            for (int i = seg; i >= 0; i--)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                pts.Add(P(cx + Mathf.Cos(a) * (r - th), cy + Mathf.Sin(a) * (r - th)));
            }
            d.parts.Add(new PartDef { name = "Coat_Cloud", bone = bone, order = order * 4 + 1, color = c, role = PartRole.Trim, pts = pts.ToArray(), rim = false });
        }

        /// <summary>Katana built along its own axis, then laid at the reference angle in the far hand.</summary>
        static void BuildSword(RigDef d, bool ronin)
        {
            Vector2 grip = d.pivot[(int)B.Sword];
            float ang = d.bladeBindAngle;      // set by Skeleton: the reference blade line, guard (772,500) -> tip (522,658)

            Vector2[] L(params float[] xy)
            {
                var r = new Vector2[xy.Length / 2];
                for (int i = 0; i < r.Length; i++) r[i] = grip + KMath.Rot(new Vector2(xy[i * 2], xy[i * 2 + 1]), ang);
                return r;
            }
            void SP(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim = false, float glow = 1f)
            {
                d.parts.Add(new PartDef { name = name, bone = B.Sword, order = 4 * 4 + sub, color = c, role = role, pts = pts, rim = rim, rimGrow = 0.003f, rimColor = KMath.Hex("#050304"), glow = glow, swordLayer = true });
            }

            const float b0 = 0.07f, b1 = 0.80f;
            float Sori(float t) => 0.03f * t * t;
            var blade = new List<Vector2>();
            const int n = 14;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                blade.Add(new Vector2(Mathf.Lerp(b0, b1, t), 0.013f + Sori(t)));
            }
            for (int i = n; i >= 0; i--)
            {
                float t = i / (float)n;
                float x = Mathf.Lerp(b0, b1, t);
                float kiss = Mathf.Clamp01((t - 0.88f) / 0.12f);        // kissaki: edge sweeps up into the point
                float y = Mathf.Lerp(-0.016f, 0.013f, kiss * kiss) + Sori(t);
                if (i == n) continue;
                blade.Add(new Vector2(x, y));
            }
            var bladePts = new Vector2[blade.Count];
            for (int i = 0; i < blade.Count; i++) bladePts[i] = grip + KMath.Rot(blade[i], ang);

            SP("Blade", 0, Palette.Steel, PartRole.Metal, bladePts, true);
            var glint = new List<Vector2>();
            for (int i = 0; i <= 10; i++) { float t = i / 10f * 0.9f; glint.Add(new Vector2(Mathf.Lerp(b0, b1, t), 0.012f + Sori(t))); }
            for (int i = 10; i >= 0; i--) { float t = i / 10f * 0.9f; glint.Add(new Vector2(Mathf.Lerp(b0, b1, t), 0.006f + Sori(t))); }
            var gl = new Vector2[glint.Count];
            for (int i = 0; i < gl.Length; i++) gl[i] = grip + KMath.Rot(glint[i], ang);
            SP("Blade_Glint", 1, Color.white, PartRole.Metal, gl, false, 1.8f);
            var hi = new List<Vector2>();
            for (int i = 0; i <= 10; i++) { float t = i / 10f * 0.86f; hi.Add(new Vector2(Mathf.Lerp(b0 + 0.02f, b1, t), 0.0f + Sori(t))); }
            for (int i = 10; i >= 0; i--) { float t = i / 10f * 0.86f; hi.Add(new Vector2(Mathf.Lerp(b0 + 0.02f, b1, t), -0.004f + Sori(t))); }
            var hp = new Vector2[hi.Count];
            for (int i = 0; i < hp.Length; i++) hp[i] = grip + KMath.Rot(hi[i], ang);
            SP("Blade_Groove", 1, Palette.SteelDark, PartRole.Metal, hp);

            SP("Tsuka", 0, KMath.Hex("#1C1214"), PartRole.Body, L(-0.17f, -0.014f, 0.03f, -0.014f, 0.03f, 0.014f, -0.17f, 0.014f), true);
            Color wrap = ronin ? Palette.AmberDeep : Palette.TrimDeep;
            for (int i = 0; i < 4; i++)
            {
                float x = -0.14f + i * 0.04f;
                SP("Tsuka_Wrap", 1, wrap, PartRole.TrimDeep, L(x - 0.014f, 0f, x, -0.011f, x + 0.014f, 0f, x, 0.011f));
            }
            SP("Kashira", 1, Palette.Silver, PartRole.Metal, L(-0.188f, -0.016f, -0.168f, -0.016f, -0.168f, 0.016f, -0.188f, 0.016f));
            var tsuba = new List<float>();
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                tsuba.Add(0.042f + Mathf.Cos(a) * 0.011f);
                tsuba.Add(Mathf.Sin(a) * 0.046f);
            }
            SP("Tsuba", 2, Palette.Silver, PartRole.Metal, L(tsuba.ToArray()), true);
            SP("Habaki", 2, KMath.Hex("#B89A5A"), PartRole.Metal, L(0.052f, -0.017f, 0.074f, -0.016f, 0.074f, 0.016f, 0.052f, 0.017f));
            d.parts[d.parts.Count - 1].underSlot = -1;   // under the tsuba where they overlap, every time (SAN-114)
        }
    }
}
