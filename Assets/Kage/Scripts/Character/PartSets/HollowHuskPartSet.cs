using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The Hollow Husk's look (SAN-112), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the fists brute of the JS Hollow
    /// skin (<c>hollow_husk</c>). A heavy cowl with a horned brow mask (the horns broken to stubs, the mask stopping over the eyes so the
    /// heavy jaw hangs open below), a ragged mantle humped over the shoulders, a torn robe open on a chest of glowing violet cracks,
    /// thick bare forearms in wraps with huge bandaged fists, and a shimenawa rope belt hung with white shide papers. Violet-black cloth
    /// like every Hollow (<see cref="HollowPalette"/>), but a head broader and about a fifth wider than the Shade at the shoulders and the
    /// hips, with no sword and no saya: the heavy fists set needs only its hands. Shared pieces: <see cref="HollowBody"/>.
    /// </summary>
    public sealed class HollowHuskPartSet : IPartSet
    {
        public string Id => "hollow_husk";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW HUSK");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
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
            return d;
        }

        // ---------------------------------------------------------------------------------------------- arms: bare thick forearms, huge fists
        static void Fist(RigDef d, string name, B bone, int slot, bool sword, params float[] xy)
        {
            var pts = V(xy);
            var polys = new List<(Vector2[], Color)> { Poly(pts, H.Wrap) };
            AddMulti(d, name, bone, slot, polys, 1, pts, PartRole.Body, sword);
        }

        static void FarArm(RigDef d)
        {
            const float k = 1.3f;
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, H.ClothFar, Tube(862, 272, 822, 385, 62 * k, 58 * k), swordLayer: true);
            Add(d, "ArmF_Fray", B.ArmFU, Slot.FarArm, H.ClothFar, V(796, 376, 846, 380, 852, 410, 836, 400, 828, 424, 816, 402, 802, 414, 800, 392), 1, PartRole.Body, true, null, 1f, true);
            Add(d, "ArmF_Forearm", B.ArmFL, Slot.FarArm + 1, H.WrapDark, Tube(822, 385, 797, 452, 50 * k, 46 * k), 0, PartRole.Body, true, null, 1f, true);
            Add(d, "ArmF_Wrap1", B.ArmFL, Slot.FarArm + 1, H.Wrap, Band(6f, 800, 408, 836, 420), 1, PartRole.Trim, false, null, 1f, true);
            Add(d, "ArmF_Wrap2", B.ArmFL, Slot.FarArm + 1, H.Wrap, Band(6f, 790, 434, 826, 444), 1, PartRole.Trim, false, null, 1f, true);
            // the huge bandaged fist
            var fist = V(762, 436, 806, 430, 826, 452, 828, 486, 806, 510, 772, 512, 750, 492, 746, 462);
            var polys = new List<(Vector2[], Color)>
            {
                Poly(fist, H.WrapDark),
                Poly(Band(5f, 752, 458, 826, 450), H.Wrap),
                Poly(Band(5f, 750, 480, 828, 474), H.Wrap),
                Poly(Band(5f, 756, 500, 818, 496), H.Wrap),
                Poly(Band(2f, 764, 440, 800, 436), H.ClothLit),
            };
            AddMulti(d, "HandF", B.HandF, Slot.FarArm + 2, polys, 0, fist, PartRole.Body, true);
        }

        static void NearArm(RigDef d)
        {
            const float k = 1.3f;
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, H.Cloth, Tube(905, 268, 926, 375, 68 * k, 62 * k));
            Add(d, "ArmN_Fray", B.ArmNU, Slot.NearArm, H.Cloth, V(884, 368, 954, 370, 964, 404, 944, 392, 936, 424, 920, 398, 902, 422, 894, 396, 878, 404), 1);
            Add(d, "ArmN_Crack", B.ArmNU, Slot.NearArm, H.Violet, Band(2.2f, 912, 296, 922, 322, 910, 348), 2, PartRole.Trim, false, null, 1.6f);
            Add(d, "ArmN_Forearm", B.ArmNL, Slot.NearArm + 1, H.WrapDark, Tube(926, 375, 914, 444, 54 * k, 48 * k));
            Add(d, "ArmN_Wrap1", B.ArmNL, Slot.NearArm + 1, H.Wrap, Band(6f, 884, 400, 944, 410), 1, PartRole.Trim, false);
            Add(d, "ArmN_Wrap2", B.ArmNL, Slot.NearArm + 1, H.Wrap, Band(6f, 888, 426, 940, 434), 1, PartRole.Trim, false);
            var fist = V(878, 436, 928, 432, 946, 458, 948, 494, 926, 520, 892, 522, 870, 502, 866, 468);
            var polys = new List<(Vector2[], Color)>
            {
                Poly(fist, H.WrapDark),
                Poly(Band(5.5f, 868, 460, 948, 456), H.Wrap),
                Poly(Band(5.5f, 868, 484, 950, 480), H.Wrap),
                Poly(Band(5.5f, 872, 506, 940, 502), H.Wrap),
                Poly(Band(2.2f, 884, 440, 922, 436), H.ClothLit),
                Poly(Band(2f, 900, 462, 906, 490), H.Violet),
            };
            AddMulti(d, "HandN_Fist", B.HandN, Slot.NearHand, polys, 0, fist, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- legs and pelvis: wide torn hakama
        static void FarLeg(RigDef d)
        {
            const float k = 1.25f;
            Add(d, "LegF_Hakama", B.LegFU, Slot.FarLeg, H.ClothFar, V(856, 536, 960, 536, 1006, 612, 1028, 692, 998, 674, 976, 708, 944, 690, 922, 714, 898, 642, 866, 592));
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, H.ClothFar, Tube(955, 672, 957, 764, 70 * k, 58 * k));
            Add(d, "LegF_Wrap", B.LegFL, Slot.FarLeg + 1, H.WrapDark, V(928, 700, 986, 700, 984, 756, 930, 756), 1);
            Add(d, "LegF_WrapBand", B.LegFL, Slot.FarLeg + 1, H.Wrap, Band(4.4f, 928, 720, 986, 728, 930, 744, 984, 750), 2, PartRole.Trim, false);
            Add(d, "FootF_Tabi", B.FootF, Slot.FarFoot, H.ClothFar, V(934, 752, 978, 752, 988, 790, 994, 816, 904, 816, 900, 804, 916, 792, 934, 782));
        }

        static void NearLeg(RigDef d)
        {
            const float k = 1.25f;
            Add(d, "LegN_Hakama", B.LegNU, Slot.NearLeg, H.Cloth, V(792, 536, 898, 536, 900, 600, 892, 664, 888, 698, 868, 678, 852, 710, 826, 688, 804, 714, 780, 692, 770, 640, 780, 580));
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, H.Cloth, Tube(835, 670, 836, 766, 78 * k, 62 * k));
            Add(d, "LegN_Wrap", B.LegNL, Slot.NearLeg + 1, H.WrapDark, V(806, 698, 866, 698, 864, 756, 810, 756), 1);
            Add(d, "LegN_WrapBand", B.LegNL, Slot.NearLeg + 1, H.Wrap, Band(4.4f, 806, 720, 866, 728, 808, 742, 864, 750), 2, PartRole.Trim, false);
            Add(d, "LegN_Crack", B.LegNL, Slot.NearLeg + 1, H.Violet, Band(2f, 826, 706, 830, 722, 824, 738), 2, PartRole.Trim, false, null, 1.6f);
            Add(d, "FootN_Tabi", B.FootN, Slot.NearFoot, H.ClothDeep, V(814, 752, 858, 752, 862, 790, 865, 816, 774, 816, 770, 804, 788, 792, 812, 782));
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, H.Cloth, V(806, 392, 958, 392, 952, 568, 810, 568), 0, PartRole.Body, false);
            Add(d, "Neck", B.Neck, Slot.Neck, H.ClothDeep, V(832, 198, 872, 198, 878, 240, 836, 240), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- torso
        static void Torso(RigDef d)
        {
            // a massive bare torso, wider than the Shade's, under a torn robe
            Add(d, "Torso", B.Chest, Slot.Torso, H.Cloth, V(822, 224, 870, 210, 916, 218, 946, 240, 962, 270, 966, 312, 968, 360, 970, 428, 880, 432, 790, 428, 790, 360, 796, 318, 804, 282, 810, 252));
            // the robe hangs open on the chest: two torn lapels with a dark gap between them full of glowing cracks
            var chest = V(826, 250, 862, 244, 900, 270, 924, 318, 922, 392, 890, 400, 850, 392, 826, 330);
            var polys = new List<(Vector2[], Color)>
            {
                Poly(chest, H.FaceDark),
                Poly(Band(3f, 850, 262, 868, 296, 858, 330, 880, 366), H.Violet),             // the long crack down the breastbone
                Poly(Band(2.2f, 868, 296, 898, 288, 914, 300), H.Violet),
                Poly(Band(2.2f, 858, 330, 832, 336, 826, 352), H.Violet),
                Poly(Band(2.2f, 880, 366, 906, 372, 918, 388), H.Violet),
                Poly(Band(1.8f, 862, 312, 888, 318), H.Eye),
            };
            AddMulti(d, "Chest_Cracks", B.Chest, Slot.TorsoTrim, polys, 0, chest);
            var lapelNear = V(900, 236, 960, 258, 976, 340, 972, 428, 954, 412, 940, 436, 922, 404, 918, 320, 904, 280);
            AddMulti(d, "Robe_LapelNear", B.Chest, Slot.TorsoTrim, new List<(Vector2[], Color)>
            {
                Poly(lapelNear, H.ClothDeep),
                Poly(Band(2.6f, 904, 244, 946, 262, 960, 300), H.ClothLit),
            }, 1, lapelNear);
            var lapelFar = V(840, 232, 868, 232, 828, 300, 812, 372, 808, 428, 794, 410, 784, 380, 792, 300);
            AddMulti(d, "Robe_LapelFar", B.Chest, Slot.TorsoTrim, new List<(Vector2[], Color)>
            {
                Poly(lapelFar, H.Cloth),
                Poly(Band(2.6f, 836, 238, 806, 290, 794, 350), H.ClothLit),
            }, 2, lapelFar);

            // the mantle: a ragged hump of cloth over the shoulders, its hem torn into hanging points
            var hump = V(808, 244, 858, 214, 916, 196, 976, 190, 1030, 214, 1048, 268, 1036, 316, 1050, 344, 1020, 336, 1012, 372, 988, 340, 970, 372, 948, 322, 940, 282, 900, 258, 846, 262);
            AddMulti(d, "Mantle", B.Chest, Slot.TorsoTrim, new List<(Vector2[], Color)>
            {
                Poly(hump, H.ClothDeep),
                Poly(Band(3f, 820, 246, 862, 220, 920, 202, 976, 198, 1022, 220), H.ClothLit),
                Poly(Band(2.4f, 940, 224, 996, 250, 1022, 300), H.ClothFar),
                Poly(Band(2.4f, 900, 232, 960, 262, 988, 322), H.ClothFar),
                Poly(Band(2f, 1008, 238, 1020, 266, 1010, 300), H.Violet),
            }, 3, hump);

            // the shimenawa: a thick twisted rope belt with hanging shide papers
            var rope = V(786, 392, 972, 396, 974, 424, 786, 420);
            var rp = new List<(Vector2[], Color)> { Poly(rope, H.RopeDark) };
            for (float x = 794; x < 970; x += 17f) rp.Add(Poly(Band(5f, x, 394, x + 10f, 420), H.Rope));
            AddMulti(d, "Shimenawa", B.Chest, Slot.Belt, rp, 0, rope, PartRole.Trim);
            AddShide(d, "Shide_1", 826, 418, 0.9f, 0);
            AddShide(d, "Shide_2", 886, 420, 1.1f, 1);
            AddShide(d, "Shide_3", 940, 418, 0.8f, 2);
        }

        /// <summary>A shide: a zigzag strip of white paper hanging from the rope, swinging.</summary>
        static void AddShide(RigDef d, string name, float x, float y, float swing, int sub)
        {
            var paper = KMath.Hex("#CFC8E4");
            AddSwinging(d, name, B.Chest, Slot.BeltTrim, paper, V(x - 8, y, x + 8, y, x + 12, y + 22, x - 4, y + 40, x + 10, y + 58, x - 6, y + 76, x - 12, y + 52, x + 2, y + 36, x - 10, y + 20),
                x, y, swing, sub, PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- the torn skirt
        static void Skirt(RigDef d)
        {
            void Front(string n, float sw, int sub, float px, float py, Vector2[] pts, Color col) => AddSwinging(d, n, B.Hips, Slot.FrontPlates, col, pts, px, py, sw, sub);
            Front("Skirt_F1", 0.4f, 0, 838, 410, HollowBody.Torn(786, 904, 408, 580, 6, 28, 1), H.Cloth);
            Front("Skirt_F2", 0.7f, 1, 830, 410, HollowBody.Torn(776, 872, 416, 660, 5, 32, 3, -4), H.ClothDeep);
            Front("Skirt_F2_Glow", 0.7f, 2, 830, 410, V(794, 480, 800, 484, 826, 580, 818, 584), H.Violet);
            AddSwinging(d, "Skirt_B1", B.Hips, Slot.BackPlates, H.ClothDeep, HollowBody.Torn(922, 1006, 408, 600, 4, 28, 2), 962, 408, 0.7f, 1);
            AddSwinging(d, "Skirt_B2", B.Hips, Slot.BackPlates, H.ClothFar, HollowBody.Torn(938, 1040, 416, 670, 5, 34, 4, 8), 984, 416, 1.0f, 0);
        }

        // ---------------------------------------------------------------------------------------------- head
        static void Head(RigDef d)
        {
            // a heavy cowl over the head with a torn tail
            AddSwinging(d, "Cowl_Tail", B.Head, Slot.Neck, H.ClothDeep, V(898, 140, 936, 158, 954, 210, 972, 252, 944, 240, 936, 270, 914, 232, 896, 238, 904, 192), 912, 150, 0.9f, 2);
            Add(d, "Cowl", B.Head, Slot.Head, H.Cloth, V(796, 170, 798, 134, 822, 106, 860, 94, 896, 104, 918, 136, 922, 180, 914, 216, 926, 240, 898, 230, 886, 248, 862, 228, 834, 230, 812, 214), 3);

            // the brute's face: a heavy jaw hanging open, deep-set, hollow-cheeked
            var face = V(810, 148, 856, 144, 862, 182, 858, 222, 844, 250, 800, 254, 784, 244, 776, 226, 774, 208, 788, 198, 796, 174, 802, 158);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, H.Face),
                Poly(V(778, 214, 790, 220, 800, 214, 784, 208), H.FaceDark),
                Poly(V(792, 224, 832, 232, 842, 246, 800, 250, 786, 240), H.FaceDark),         // the open mouth
                Poly(Band(2f, 796, 226, 836, 234), H.Violet),
                Poly(Band(2.4f, 798, 172, 800, 192, 790, 200), H.Violet),
            }, 0, face);

            // the horned brow mask: it stops over the eyes, the horns broken off to stubs
            var mask = V(792, 150, 836, 138, 864, 150, 864, 182, 846, 192, 822, 186, 796, 196, 786, 176);
            AddMulti(d, "Mask", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(mask, H.Mask),
                Poly(Band(3f, 794, 156, 836, 144, 860, 154), H.MaskLit),
                Poly(Band(2.4f, 822, 146, 818, 166, 830, 184), H.Violet),
                Poly(Band(2f, 800, 172, 808, 186, 798, 194), H.Violet),
            }, 1, mask);
            Add(d, "Eye_Slit", B.Head, Slot.Face, H.FaceDark, V(800, 164, 840, 158, 856, 166, 838, 178, 806, 176), 2, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, H.Eye, V(818, 167, 838, 162, 852, 167, 838, 174, 822, 174), 3, PartRole.Eye, false, null, 3.2f);
            // broken horns: two thick stumps with a violet fracture
            var h1 = V(794, 150, 800, 112, 816, 96, 820, 118, 818, 144);
            var h2 = V(844, 140, 850, 100, 870, 86, 874, 112, 862, 144);
            Add(d, "Horn_Near", B.Head, Slot.HeadTop, H.MaskDark, h1, 0, PartRole.Metal);
            Add(d, "Horn_Far", B.Head, Slot.HeadTop, H.Mask, h2, 1, PartRole.Metal);
            Add(d, "Horn_Break", B.Head, Slot.HeadTop, H.Violet, Band(2.6f, 802, 100, 816, 98, 830, 92), 2, PartRole.Trim, false, null, 1.6f);
            Add(d, "Horn_Break2", B.Head, Slot.HeadTop, H.Violet, Band(2.6f, 852, 90, 866, 88), 2, PartRole.Trim, false, null, 1.6f);
        }
    }
}
