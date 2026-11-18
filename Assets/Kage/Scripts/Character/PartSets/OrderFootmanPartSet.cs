using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Footman's look (SAN-69), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the Order's cutter,
    /// a fox-masked soldier in a cloth hood with tall pointed ears, a slate-lacquer dō laced in muted crimson with the Order
    /// mark, broad box-like sode on both shoulders, a kusazuri skirt of swinging plates, wide hakama over silver shin guards,
    /// an empty saya at the hip and a straight cutter blade. No scarf, no coat: his outline is blocky and low where Kage's is
    /// lean and streaming. Based on the JS Order skin (<c>order_footman</c>: fox head, full dō, sode, saya).
    /// </summary>
    public sealed class OrderFootmanPartSet : IPartSet
    {
        public string Id => "order_footman";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER FOOTMAN");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            FarArm(d);
            FarLeg(d);
            Pelvis(d);
            Torso(d);
            NearLeg(d);
            Skirt(d);
            Head(d);
            NearArm(d);
            Sword(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- far (sword) arm
        static void FarArm(RigDef d)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, O.ClothFar, Tube(862, 272, 822, 385, 60, 54), swordLayer: true);
            // far sode: a box of lames hanging from the shoulder
            var sode = V(830, 250, 898, 244, 906, 296, 900, 338, 832, 344, 824, 296);
            AddMulti(d, "ArmF_Sode", B.ArmFU, Slot.FarArm, new List<(Vector2[], Color)>
            {
                Poly(sode, O.LacquerFar),
                Poly(Band(3f, 826, 284, 904, 280), O.SilverDark),
                Poly(Band(3f, 825, 316, 903, 312), O.SilverDark),
                Poly(Band(3.5f, 828, 340, 902, 336), O.SilverMid),
            }, 1, sode, swordLayer: true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, Shade(O.SilverDark, 0.75f), Tube(822, 385, 797, 452, 44, 38), 0, PartRole.Metal, swordLayer: true);
            Add(d, "ArmF_KoteBand", B.ArmFL, Slot.FarArm + 1, O.SilverMid, Band(4f, 785, 424, 822, 432), 1, PartRole.Metal, false, swordLayer: true);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, O.Glove, V(780, 450, 810, 452, 814, 478, 802, 494, 780, 490, 772, 470), swordLayer: true);
        }

        // ---------------------------------------------------------------------------------------------- far leg and pelvis
        static void FarLeg(RigDef d)
        {
            Add(d, "LegF_Hakama", B.LegFU, Slot.FarLeg, O.ClothFar, V(860, 536, 954, 536, 998, 612, 1018, 692, 932, 698, 902, 642, 870, 592));
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, O.ClothFar, Tube(955, 672, 957, 764, 70, 56));
            Add(d, "LegF_Suneate", B.LegFL, Slot.FarLeg + 1, Shade(O.SilverDark, 0.7f), V(934, 700, 980, 700, 978, 752, 936, 752), 1, PartRole.Metal);
            Add(d, "LegF_Cord", B.LegFL, Slot.FarLeg + 1, O.SilverDark, Band(5f, 928, 756, 986, 756), 2, PartRole.Trim, false);
            Add(d, "FootF_Tabi", B.FootF, Slot.FarFoot, O.ClothFar, V(934, 752, 978, 752, 988, 790, 994, 816, 904, 816, 900, 804, 916, 792, 934, 782));
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, O.LacquerFar, V(900, 808, 996, 808, 996, 818, 904, 818), 1, PartRole.Detail, false);
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, O.Cloth, V(812, 392, 952, 392, 946, 566, 816, 566), 0, PartRole.Body, false);
            // the saya, empty, through the sash and out behind him: the soldier's silhouette tail
            Add(d, "Saya", B.Hips, Slot.Pelvis, O.Lacquer, Tube(900, 436, 1070, 556, 26, 20), 1, PartRole.Detail);
            Add(d, "Saya_Mouth", B.Hips, Slot.Pelvis, O.SilverMid, Band(7f, 908, 428, 912, 450), 2, PartRole.Metal, false);
            Add(d, "Saya_Tip", B.Hips, Slot.Pelvis, O.SilverMid, Tube(1052, 543, 1080, 563, 22, 16), 2, PartRole.Metal);
        }

        // ---------------------------------------------------------------------------------------------- torso
        static void Torso(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, O.Cloth, V(834, 226, 870, 214, 908, 220, 932, 240, 946, 268, 950, 310, 951, 355, 953, 425, 880, 428, 808, 425, 807, 360, 812, 322, 818, 290, 824, 260));
            Add(d, "Gi_Collar", B.Chest, Slot.TorsoTrim, Shade(O.Cloth, 1.45f), V(826, 238, 848, 232, 874, 304, 858, 330, 842, 304), 0, PartRole.Trim, false);

            // the dō: lacquered lames laced in crimson, a lit top edge, the Order mark (a barred circle) on the chest
            var plate = V(816, 244, 864, 230, 914, 234, 946, 262, 954, 320, 958, 404, 800, 408, 804, 330, 808, 284);
            var polys = new List<(Vector2[], Color)> { Poly(plate, O.Lacquer) };
            float[,] rows = { { 256, 294 }, { 300, 336 }, { 342, 380 } };
            for (int r = 0; r < 3; r++)
            {
                float y0 = rows[r, 0], y1 = rows[r, 1];
                polys.Add(Poly(Band(3f, 802, y1 + 2, 958, y1 + 2), O.SilverDark));
                for (float x = 824; x < 950; x += 21f)
                    polys.Add(Poly(Band(3.6f, x, y0 + 5, x, y1 - 3), O.Lacing));
            }
            polys.Add(Poly(Band(2.6f, 818, 250, 864, 236, 914, 240, 944, 266), O.LacquerLit));
            polys.Add(Poly(Ellipse(856, 330, 19, 19), O.SilverMid));
            polys.Add(Poly(Ellipse(856, 330, 15, 15), O.Lacquer));
            polys.Add(Poly(Band(4.2f, 840, 330, 872, 330), O.SilverMid));
            AddMulti(d, "Do", B.Chest, Slot.TorsoTrim, polys, 1, plate);

            Add(d, "Gorget", B.Chest, Slot.TorsoTrim, O.Lacquer, V(822, 232, 888, 224, 898, 252, 814, 260), 2, PartRole.Detail);
            Add(d, "Gorget_Edge", B.Chest, Slot.TorsoTrim, O.SilverMid, Band(3.2f, 818, 256, 898, 248), 3, PartRole.Metal, false);

            // sash
            Add(d, "Sash", B.Chest, Slot.Belt, O.ClothDeep, V(802, 384, 956, 388, 957, 410, 802, 406), 0, PartRole.Trim, false);
            Add(d, "Sash_Knot", B.Chest, Slot.BeltTrim, O.Cloth, Ellipse(822, 400, 17, 13), 0, PartRole.Trim);
            AddSwinging(d, "Sash_Tail", B.Chest, Slot.BeltTrim, O.Cloth, V(810, 404, 840, 404, 850, 470, 834, 488, 820, 450), 826, 404, 0.8f, 1, PartRole.Trim);
            Add(d, "Neck", B.Neck, Slot.Neck, O.ClothDeep, V(838, 200, 868, 200, 872, 240, 842, 240), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- near leg
        static void NearLeg(RigDef d)
        {
            Add(d, "LegN_Hakama", B.LegNU, Slot.NearLeg, O.Cloth, V(798, 536, 892, 536, 894, 600, 888, 664, 880, 700, 794, 700, 778, 640, 786, 580));
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, O.Cloth, Tube(835, 670, 836, 766, 78, 60));
            Add(d, "LegN_Suneate", B.LegNL, Slot.NearLeg + 1, Shade(O.SilverDark, 1.1f), V(813, 698, 861, 698, 859, 752, 817, 752), 1, PartRole.Metal);
            Add(d, "LegN_SuneateLit", B.LegNL, Slot.NearLeg + 1, O.SilverMid, Band(3.4f, 819, 704, 818, 748), 2, PartRole.Metal, false);
            Add(d, "LegN_Cord", B.LegNL, Slot.NearLeg + 1, O.SilverMid, Band(5f, 804, 756, 868, 756), 2, PartRole.Trim, false);
            Add(d, "FootN_Tabi", B.FootN, Slot.NearFoot, O.ClothDeep, V(814, 752, 858, 752, 862, 790, 865, 816, 774, 816, 770, 804, 788, 792, 812, 782));
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, O.Lacquer, V(768, 808, 866, 808, 866, 818, 772, 818), 1, PartRole.Detail, false);
        }

        // ---------------------------------------------------------------------------------------------- kusazuri
        static void Skirt(RigDef d)
        {
            // front: three stacked lames, each hanging from its own point and swinging a little behind the walk
            void Front(string n, float swing, int sub, float pivX, float pivY, params float[] xy)
            {
                AddSwinging(d, n, B.Hips, Slot.FrontPlates, sub % 2 == 0 ? O.Lacquer : Shade(O.Lacquer, 1.35f), V(xy), pivX, pivY, swing, sub);
            }
            Front("Kusazuri_F1", 0.25f, 0, 842, 404, 798, 402, 888, 402, 892, 470, 794, 470);
            Front("Kusazuri_F2", 0.4f, 1, 840, 462, 792, 460, 892, 460, 888, 532, 786, 532);
            Front("Kusazuri_F3", 0.55f, 2, 838, 524, 786, 522, 886, 522, 878, 590, 782, 590);
            AddSwinging(d, "Kusazuri_F2_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 840, 468, 838, 526), 840, 462, 0.4f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_F3_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 838, 530, 834, 584), 838, 524, 0.55f, 3, PartRole.Trim, false);

            // back: two plates behind the legs
            AddSwinging(d, "Kusazuri_B1", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 0.9f), V(936, 402, 984, 406, 1004, 470, 928, 468), 958, 402, 0.3f, 1);
            AddSwinging(d, "Kusazuri_B2", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 1.3f), V(926, 462, 1004, 466, 1030, 536, 922, 532), 960, 462, 0.5f, 2);
            AddSwinging(d, "Kusazuri_B2_Lace", B.Hips, Slot.BackPlates, O.Lacing, Band(4f, 962, 470, 974, 528), 960, 462, 0.5f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_B2_Hem", B.Hips, Slot.BackPlates, O.SilverDark, Band(4f, 926, 530, 1028, 534), 960, 462, 0.5f, 3, PartRole.Metal, false);
        }

        // ---------------------------------------------------------------------------------------------- head
        static void Head(RigDef d)
        {
            // the hood's tail, and two tall pointed fox ears that twitch
            AddSwinging(d, "Hood_Tail", B.Head, Slot.Neck, O.ClothDeep, V(902, 144, 930, 152, 950, 196, 950, 238, 934, 222, 922, 196, 908, 178), 912, 150, 0.9f, 2);
            AddSwinging(d, "Ear_Far", B.Head, Slot.Head, O.ClothDeep, V(868, 122, 880, 34, 916, 132), 880, 124, 0.45f, 0);
            AddSwinging(d, "Ear_Near", B.Head, Slot.Head, O.Cloth, V(834, 130, 824, 28, 882, 118), 850, 124, 0.45f, 1);
            AddSwinging(d, "Ear_Inner", B.Head, Slot.Head, Shade(O.Cloth, 1.7f), V(842, 120, 833, 58, 866, 116), 850, 124, 0.45f, 2, PartRole.Detail, false);

            Add(d, "Hood", B.Head, Slot.Head, O.Cloth, V(802, 168, 806, 138, 828, 114, 860, 104, 890, 114, 908, 140, 910, 178, 900, 208, 876, 226, 844, 226, 818, 212), 3);
            Add(d, "Hood_Band", B.Head, Slot.HeadTop, O.SilverDark, Band(7f, 806, 150, 832, 130, 866, 122, 898, 130, 908, 150), 0, PartRole.Metal, false);

            // the fox mask: a long muzzle, a dark slit and a cold eye
            var mask = V(812, 150, 846, 146, 856, 180, 852, 214, 830, 226, 800, 224, 780, 214, 760, 202, 782, 190, 800, 172);
            AddMulti(d, "Mask", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(mask, O.Mask),
                Poly(V(800, 214, 830, 224, 852, 214, 856, 196, 826, 206), O.MaskShade),
                Poly(Ellipse(762, 201, 7, 5.5f), O.Lacquer),
                Poly(Band(3.2f, 806, 184, 786, 196), O.SilverDark),
                Poly(Band(2.6f, 826, 206, 800, 214), O.SilverDark),
            }, 0, mask);
            Add(d, "Eye_Slit", B.Head, Slot.Face, O.Lacquer, V(808, 168, 836, 158, 852, 166, 836, 177, 814, 180), 1, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(822, 170, 838, 164, 849, 168, 838, 174, 825, 176), 2, PartRole.Eye, false, null, 2.8f);
        }

        // ---------------------------------------------------------------------------------------------- near arm
        static void NearArm(RigDef d)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, O.Cloth, Tube(905, 268, 926, 375, 66, 58));
            // near sode: three lames, silver-edged, hanging off the shoulder: the widest thing on him
            var sode = V(852, 246, 962, 238, 990, 296, 986, 370, 886, 384, 866, 322);
            AddMulti(d, "ArmN_Sode", B.ArmNU, Slot.NearArm, new List<(Vector2[], Color)>
            {
                Poly(sode, O.Lacquer),
                Poly(Band(2.6f, 860, 252, 962, 244, 986, 292), O.LacquerLit),
                Poly(Band(3.2f, 862, 298, 990, 296), O.SilverDark),
                Poly(Band(3.2f, 866, 334, 988, 332), O.SilverDark),
                Poly(Band(3.6f, 878, 262, 874, 292), O.Lacing),
                Poly(Band(3.6f, 976, 258, 982, 290), O.Lacing),
                Poly(Band(3.6f, 876, 304, 872, 330), O.Lacing),
                Poly(Band(3.6f, 976, 302, 980, 328), O.Lacing),
                Poly(Band(3.6f, 884, 340, 884, 372), O.Lacing),
                Poly(Band(3.6f, 976, 338, 980, 366), O.Lacing),
                Poly(Ellipse(926, 288, 17, 17), O.SilverMid),
                Poly(Ellipse(926, 288, 13.5f, 13.5f), O.Lacquer),
                Poly(Band(4f, 912, 288, 940, 288), O.SilverMid),
                Poly(Band(4.4f, 888, 378, 986, 366), O.SilverMid),
            }, 2, sode);
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, Shade(O.SilverDark, 1.0f), Tube(926, 375, 914, 444, 48, 42), 0, PartRole.Metal);
            Add(d, "ArmN_KoteBand", B.ArmNL, Slot.NearArm + 1, O.SilverMid, Band(4f, 888, 408, 944, 408), 1, PartRole.Metal, false);
            Add(d, "HandN_Fist", B.HandN, Slot.NearHand, O.Glove, V(888, 442, 926, 440, 932, 462, 924, 486, 898, 490, 880, 480, 877, 458));
        }

        // ---------------------------------------------------------------------------------------------- the cutter blade
        static void Sword(RigDef d)
        {
            // a straight blade lying on the hit line (guard (0.08, 0) to tip (0.80, 0.03)): drawn and hit line agree
            float Yc(float x) => 0.03f * (x - 0.08f) / 0.72f;
            var top = new List<float>();
            var bottom = new List<float>();
            const int n = 10;
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(0.07f, 0.76f, i / (float)n);
                top.Add(x); top.Add(Yc(x) + 0.0165f);
                bottom.Insert(0, Yc(x) - 0.0165f);
                bottom.Insert(0, x);
            }
            var blade = new List<float>(top) { 0.805f, 0.03f };
            blade.AddRange(bottom);
            Color rimDark = KMath.Hex("#050608");
            void S(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim = false, float glow = 1f)
            {
                var p = Add(d, name, B.Sword, 4, c, pts, sub, role, rim, rimDark, glow, true);
                p.rimGrow = 0.003f;
            }
            S("Blade", 0, O.Steel, PartRole.Metal, AlongSword(d, blade.ToArray()), true);
            S("Blade_Fuller", 1, Shade(O.Steel, 0.58f), PartRole.Metal, AlongSword(d, 0.12f, Yc(0.12f), 0.70f, Yc(0.70f), 0.70f, Yc(0.70f) + 0.006f, 0.12f, Yc(0.12f) + 0.006f));
            S("Blade_Edge", 1, Color.white, PartRole.Metal, AlongSword(d, 0.10f, Yc(0.10f) - 0.0165f, 0.74f, Yc(0.74f) - 0.0165f, 0.74f, Yc(0.74f) - 0.011f, 0.10f, Yc(0.10f) - 0.011f), false, 1.5f);
            S("Tsuka", 0, O.Lacquer, PartRole.Body, AlongSword(d, -0.17f, -0.014f, 0.03f, -0.014f, 0.03f, 0.014f, -0.17f, 0.014f), true);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.14f + i * 0.04f;
                S("Tsuka_Wrap" + i, 1, O.SilverDark, PartRole.Trim, AlongSword(d, x - 0.014f, 0f, x, -0.011f, x + 0.014f, 0f, x, 0.011f));
            }
            S("Kashira", 1, O.SilverMid, PartRole.Metal, AlongSword(d, -0.188f, -0.016f, -0.168f, -0.016f, -0.168f, 0.016f, -0.188f, 0.016f));
            // a square-cut tsuba
            S("Tsuba", 2, O.SilverMid, PartRole.Metal, AlongSword(d, 0.03f, -0.04f, 0.056f, -0.044f, 0.056f, 0.044f, 0.03f, 0.04f), true);
            S("Habaki", 2, O.Silver, PartRole.Metal, AlongSword(d, 0.056f, -0.017f, 0.076f, -0.016f, 0.076f, 0.016f, 0.056f, 0.017f));
        }
    }
}
