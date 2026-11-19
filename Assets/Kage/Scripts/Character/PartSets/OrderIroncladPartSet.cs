using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Ironclad's look (SAN-100), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the raider of the JS Order skin
    /// (<c>order_ironclad</c>). A horned kabuto (ox-horn wakidate) with a broad shikoro over a heavy-jawed bare face and a throat guard
    /// of lacquer lames, a heavy dō with a wide gorget, huge o-sode on both shoulders, haidate thigh guards and a long five-row kusazuri.
    /// Everything is wider than the Footman's: the dō is 190 px across where his is 160, the sleeves and shins are 1.3 times as thick, the
    /// shoulders carry a plate each. With the JS build scale (1.14) it is the broadest thing in the Order. The cleaver is the cleaver
    /// family's (<c>raider_cleaver</c>), drawn by <see cref="CleaverVisual"/>.
    /// </summary>
    public sealed class OrderIroncladPartSet : IPartSet
    {
        public string Id => "order_ironclad";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER IRONCLAD");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            FarArm(d);
            FarLeg(d);
            OrderBody.Pelvis(d);
            Torso(d);
            NearLeg(d);
            Skirt(d);
            Head(d);
            NearArm(d);
            return d;
        }

        static void Sode(RigDef d, string name, B bone, int slot, bool far, bool sword)
        {
            // an o-sode: three broad lames, silver-edged and laced, hanging off the shoulder
            var body = far ? O.LacquerFar : O.Lacquer;
            float dx = far ? -22f : 0f;
            var plate = V(836 + dx, 238, 950 + dx, 230, 990 + dx, 300, 986 + dx, 392, 872 + dx, 408, 846 + dx, 320);
            var polys = new List<(Vector2[], Color)>
            {
                Poly(plate, body),
                Poly(Band(2.6f, 842 + dx, 244, 950 + dx, 236, 986 + dx, 296), O.LacquerLit),
                Poly(Band(3.4f, 846 + dx, 296, 988 + dx, 294), O.SilverDark),
                Poly(Band(3.4f, 850 + dx, 346, 988 + dx, 344), O.SilverDark),
                Poly(Band(4.4f, 872 + dx, 400, 986 + dx, 388), O.SilverMid),
            };
            for (int r = 0; r < 3; r++)
            {
                float y0 = 254 + r * 50, y1 = y0 + 38;
                for (float x = 862 + dx; x < 985 + dx; x += 22f) polys.Add(Poly(Band(3.6f, x, y0, x, y1), O.Lacing));
            }
            if (!far)
            {
                polys.Add(Poly(Ellipse(920, 296, 17, 17), O.SilverMid));
                polys.Add(Poly(Ellipse(920, 296, 13.5f, 13.5f), O.Lacquer));
                polys.Add(Poly(Band(4f, 906, 296, 934, 296), O.SilverMid));
            }
            AddMulti(d, name, bone, slot, polys, far ? 1 : 2, plate, PartRole.Body, sword);
        }

        // ---------------------------------------------------------------------------------------------- arms
        static void FarArm(RigDef d)
        {
            const float k = 1.3f;
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, O.ClothFar, Tube(862, 272, 822, 385, 60 * k, 54 * k), swordLayer: true);
            Sode(d, "ArmF_Sode", B.ArmFU, Slot.FarArm, true, true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, Shade(O.SilverDark, 0.75f), Tube(822, 385, 797, 452, 44 * k, 38 * k), 0, PartRole.Metal, swordLayer: true);
            Add(d, "ArmF_KoteBand", B.ArmFL, Slot.FarArm + 1, O.SilverMid, Band(5f, 780, 422, 826, 432), 1, PartRole.Metal, false, swordLayer: true);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, O.Glove, V(776, 446, 812, 450, 818, 480, 804, 498, 778, 494, 768, 470), swordLayer: true);
        }

        static void NearArm(RigDef d)
        {
            const float k = 1.3f;
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, O.Cloth, Tube(905, 268, 926, 375, 66 * k, 58 * k));
            Sode(d, "ArmN_Sode", B.ArmNU, Slot.NearArm, false, false);
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, O.SilverDark, Tube(926, 375, 914, 444, 48 * k, 42 * k), 0, PartRole.Metal);
            Add(d, "ArmN_KoteBand", B.ArmNL, Slot.NearArm + 1, O.SilverMid, Band(5f, 884, 408, 948, 408), 1, PartRole.Metal, false);
            Add(d, "HandN_Fist", B.HandN, Slot.NearHand, O.Glove, V(884, 440, 928, 438, 936, 462, 928, 490, 898, 494, 878, 482, 874, 458));
        }

        // ---------------------------------------------------------------------------------------------- legs
        static void FarLeg(RigDef d)
        {
            const float k = 1.3f;
            Add(d, "LegF_Hakama", B.LegFU, Slot.FarLeg, O.ClothFar, V(856, 536, 960, 536, 1004, 612, 1024, 692, 930, 700, 898, 642, 866, 592));
            Add(d, "LegF_Haidate", B.LegFU, Slot.FarLeg, Shade(O.SilverDark, 0.7f), V(862, 542, 954, 542, 984, 610, 940, 620, 886, 600), 1, PartRole.Metal);
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, O.ClothFar, Tube(955, 672, 957, 764, 70 * k, 56 * k));
            Add(d, "LegF_Suneate", B.LegFL, Slot.FarLeg + 1, Shade(O.SilverDark, 0.7f), V(930, 696, 984, 696, 982, 754, 932, 754), 1, PartRole.Metal);
            Add(d, "FootF_Tabi", B.FootF, Slot.FarFoot, O.ClothFar, V(934, 752, 978, 752, 988, 790, 994, 816, 904, 816, 900, 804, 916, 792, 934, 782));
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, O.LacquerFar, V(900, 808, 996, 808, 996, 818, 904, 818), 1, PartRole.Detail, false);
        }

        static void NearLeg(RigDef d)
        {
            const float k = 1.3f;
            Add(d, "LegN_Hakama", B.LegNU, Slot.NearLeg, O.Cloth, V(792, 536, 898, 536, 900, 600, 892, 664, 884, 704, 788, 704, 770, 640, 780, 580));
            // haidate: thigh guards of lacquer strips over the hakama
            var hai = V(794, 540, 896, 540, 900, 610, 790, 616);
            AddMulti(d, "LegN_Haidate", B.LegNU, Slot.NearLeg, new List<(Vector2[], Color)>
            {
                Poly(hai, O.Lacquer),
                Poly(Band(3f, 792, 566, 898, 566), O.SilverDark),
                Poly(Band(3f, 790, 592, 898, 592), O.SilverDark),
                Poly(Band(2.4f, 798, 544, 892, 544), O.LacquerLit),
            }, 1, hai);
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, O.Cloth, Tube(835, 670, 836, 766, 78 * k, 60 * k));
            Add(d, "LegN_Suneate", B.LegNL, Slot.NearLeg + 1, Shade(O.SilverDark, 1.1f), V(808, 694, 866, 694, 864, 754, 812, 754), 1, PartRole.Metal);
            Add(d, "LegN_SuneateLit", B.LegNL, Slot.NearLeg + 1, O.SilverMid, Band(3.8f, 816, 700, 815, 750), 2, PartRole.Metal, false);
            Add(d, "FootN_Tabi", B.FootN, Slot.NearFoot, O.ClothDeep, V(814, 752, 858, 752, 862, 790, 865, 816, 774, 816, 770, 804, 788, 792, 812, 782));
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, O.Lacquer, V(768, 808, 866, 808, 866, 818, 772, 818), 1, PartRole.Detail, false);
        }

        // ---------------------------------------------------------------------------------------------- torso
        static void Torso(RigDef d)
        {
            // a bulky gi under the armour, wider than Kage's chest
            Add(d, "Torso", B.Chest, Slot.Torso, O.Cloth, V(822, 224, 870, 210, 916, 218, 946, 240, 962, 270, 966, 312, 968, 360, 970, 428, 880, 432, 790, 428, 790, 360, 796, 318, 804, 282, 810, 252));

            // the heavy dō: five rows of lames, a bold crimson lacing and the Order mark
            var plate = V(808, 238, 866, 222, 922, 228, 960, 260, 970, 320, 974, 414, 786, 418, 790, 330, 796, 280);
            var polys = new List<(Vector2[], Color)> { Poly(plate, O.Lacquer) };
            float[,] rows = { { 250, 284 }, { 290, 322 }, { 328, 360 }, { 366, 394 }, { 398, 414 } };
            for (int r = 0; r < 5; r++)
            {
                float y0 = rows[r, 0], y1 = rows[r, 1];
                polys.Add(Poly(Band(3.4f, 788, y1 + 2, 974, y1 + 2), O.SilverDark));
                for (float x = 812; x < 966; x += 20f) polys.Add(Poly(Band(4f, x, y0 + 5, x, y1 - 3), O.Lacing));
            }
            polys.Add(Poly(Band(3f, 806, 244, 866, 228, 922, 234, 958, 264), O.LacquerLit));
            polys.Add(Poly(Ellipse(860, 332, 22, 22), O.SilverMid));
            polys.Add(Poly(Ellipse(860, 332, 17, 17), O.Lacquer));
            polys.Add(Poly(Band(4.6f, 842, 332, 878, 332), O.SilverMid));
            AddMulti(d, "Do", B.Chest, Slot.TorsoTrim, polys, 1, plate);

            // the wide gorget over the collar
            Add(d, "Gorget", B.Chest, Slot.TorsoTrim, O.Lacquer, V(808, 226, 900, 216, 914, 252, 796, 266), 2, PartRole.Detail);
            Add(d, "Gorget_Edge", B.Chest, Slot.TorsoTrim, O.SilverMid, Band(3.6f, 800, 260, 912, 250), 3, PartRole.Metal, false);

            Add(d, "Sash", B.Chest, Slot.Belt, O.ClothDeep, V(786, 400, 974, 404, 975, 428, 786, 424), 0, PartRole.Trim, false);
            Add(d, "Sash_Knot", B.Chest, Slot.BeltTrim, O.Cloth, Ellipse(812, 416, 19, 14), 0, PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- kusazuri
        static void Skirt(RigDef d)
        {
            void Front(string n, float swing, int sub, float pivX, float pivY, params float[] xy) =>
                AddSwinging(d, n, B.Hips, Slot.FrontPlates, sub % 2 == 0 ? O.Lacquer : Shade(O.Lacquer, 1.35f), V(xy), pivX, pivY, swing, sub);
            Front("Kusazuri_F1", 0.2f, 0, 842, 416, 782, 414, 904, 414, 908, 480, 778, 480);
            Front("Kusazuri_F2", 0.3f, 1, 840, 474, 776, 472, 908, 472, 904, 540, 770, 540);
            Front("Kusazuri_F3", 0.4f, 2, 838, 534, 772, 532, 902, 532, 896, 600, 766, 600);
            Front("Kusazuri_F4", 0.5f, 3, 836, 594, 768, 592, 898, 592, 890, 656, 764, 656);
            Front("Kusazuri_F5", 0.6f, 2, 834, 650, 764, 648, 890, 648, 882, 706, 760, 706);
            AddSwinging(d, "Kusazuri_F2_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 840, 480, 838, 536), 840, 474, 0.3f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_F4_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 836, 600, 832, 652), 836, 594, 0.5f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_B1", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 0.9f), V(936, 412, 994, 416, 1014, 490, 930, 486), 960, 412, 0.25f, 1);
            AddSwinging(d, "Kusazuri_B2", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 1.3f), V(926, 480, 1014, 484, 1044, 570, 922, 566), 962, 480, 0.4f, 2);
            AddSwinging(d, "Kusazuri_B2_Hem", B.Hips, Slot.BackPlates, O.SilverDark, Band(4f, 924, 564, 1040, 568), 962, 480, 0.4f, 3, PartRole.Metal, false);
        }

        // ---------------------------------------------------------------------------------------------- head
        static void Head(RigDef d)
        {
            // the ox horns, curving up and forward from the bowl: the far one behind, the near one in front
            Add(d, "Horn_Far", B.Head, Slot.Head, O.LacquerFar, Tube(892, 122, 924, 92, 28, 20), 0, PartRole.Detail);
            Add(d, "Horn_FarTip", B.Head, Slot.Head, O.LacquerFar, Tube(924, 92, 942, 52, 20, 6), 0, PartRole.Detail);
            // the broad shikoro: lames flaring out behind the head and over the shoulders
            AddSwinging(d, "Shikoro", B.Head, Slot.Neck, O.Lacquer, V(884, 148, 944, 160, 1000, 218, 1010, 274, 866, 262, 850, 214), 900, 154, 0.25f, 2);
            AddSwinging(d, "Shikoro_Lace", B.Head, Slot.Neck, O.SilverDark, Band(3.6f, 876, 192, 990, 226, 1000, 270), 900, 154, 0.25f, 3, PartRole.Metal, false);

            // the heavy-jawed face: a block of a jaw jutting past a short broad nose, a deep brow
            var face = V(818, 146, 856, 142, 862, 180, 858, 218, 846, 244, 802, 248, 786, 240, 778, 226, 776, 210, 772, 202, 790, 194, 798, 172, 804, 156);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, O.Skin),
                Poly(V(776, 210, 788, 214, 796, 206, 780, 202), O.SkinShade),                     // under the broad nose
                Poly(V(786, 226, 802, 246, 846, 242, 858, 220, 820, 230), O.SkinShade),           // the jaw's shadow
                Poly(Band(2.6f, 790, 224, 824, 226), O.SkinShade),
            }, 0, face);
            Add(d, "Brow", B.Head, Slot.Face, O.Lacquer, V(788, 160, 838, 148, 862, 158, 860, 170, 832, 166, 794, 178), 1, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(822, 174, 838, 170, 850, 174, 838, 179, 825, 180), 2, PartRole.Eye, false, null, 2.8f);
            // the throat guard (yodare-kake): lacquer lames hung under the jaw
            var guard = V(804, 244, 858, 238, 868, 270, 856, 286, 812, 280, 800, 262);
            AddMulti(d, "Throat", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(guard, O.Lacquer),
                Poly(Band(3f, 806, 258, 864, 254), O.SilverDark),
                Poly(Band(3f, 808, 272, 862, 268), O.SilverDark),
            }, 3, guard);

            // the kabuto bowl and the near horn
            Add(d, "Kabuto", B.Head, Slot.Head, O.Lacquer, V(796, 164, 800, 130, 822, 104, 858, 94, 894, 104, 914, 136, 916, 184, 906, 218, 876, 230, 864, 198, 860, 162, 832, 150), 3);
            Add(d, "Kabuto_Brim", B.Head, Slot.HeadTop, O.SilverMid, Band(6f, 792, 162, 806, 152, 840, 146, 862, 154), 0, PartRole.Metal, false);
            Add(d, "Kabuto_Ridge", B.Head, Slot.HeadTop, O.LacquerLit, Band(2.8f, 806, 124, 836, 102, 876, 100, 904, 124), 1, PartRole.Detail, false);
            Add(d, "Horn_Near", B.Head, Slot.HeadTop, O.Lacquer, Tube(852, 118, 808, 88, 34, 24), 2, PartRole.Detail);
            Add(d, "Horn_NearTip", B.Head, Slot.HeadTop, O.Lacquer, Tube(808, 88, 796, 40, 24, 7), 2, PartRole.Detail);
            Add(d, "Horn_Lit", B.Head, Slot.HeadTop, O.LacquerLit, Band(3f, 842, 108, 806, 84, 796, 52), 3, PartRole.Detail, false);
            Add(d, "Horn_Mount", B.Head, Slot.HeadTop, O.SilverMid, Band(10f, 836, 118, 866, 110), 3, PartRole.Metal, false);
        }
    }
}
