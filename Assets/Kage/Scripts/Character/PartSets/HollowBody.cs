using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The pieces every Hollow shares (SAN-76), drawn with <see cref="OpponentKit"/> on Kage's skeleton: frayed sleeves over
    /// wrapped forearms, ragged hakama over wrapped shins, bare dark hands, a saya, an echo of a katana with a violet edge. A
    /// Hollow's part set adds its own head, torso dressing and skirt on top (Shade, tutorial Hollow; Flicker and Husk later).
    /// </summary>
    public static class HollowBody
    {
        /// <summary>A panel with a straight top edge and a torn, toothed hem: top from (xl,yTop) to (xr,yTop), hem about yBot.</summary>
        public static Vector2[] Torn(float xl, float xr, float yTop, float yBot, int teeth, float depth, int seed = 0, float lean = 0f)
        {
            var xy = new List<float> { xl, yTop, xr, yTop };
            for (int i = 0; i <= teeth; i++)
            {
                float k = 1f - i / (float)teeth;
                float x = Mathf.Lerp(xl, xr, k) + lean;
                float h = Mathf.Abs(Mathf.Sin((i + seed) * 2.3f + seed)) * depth;
                float y = yBot - (i % 2 == 0 ? 0f : depth * 1.4f) + h;
                xy.Add(x); xy.Add(y);
            }
            return V(xy.ToArray());
        }

        /// <summary>The far (sword) arm: a frayed sleeve, a bandaged forearm, a dark hand.</summary>
        public static void FarArm(RigDef d, bool straw = false)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, H.ClothFar, Tube(862, 272, 822, 385, 60, 54), swordLayer: true);
            Add(d, "ArmF_Fray", B.ArmFU, Slot.FarArm, H.ClothFar, V(802, 376, 842, 380, 846, 404, 832, 396, 826, 416, 814, 398, 802, 408, 804, 390), 1, PartRole.Body, true, null, 1f, true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, H.WrapDark, Tube(822, 385, 797, 452, 44, 38), 0, PartRole.Body, true, null, 1f, true);
            Add(d, "ArmF_Wrap1", B.ArmFL, Slot.FarArm + 1, H.Wrap, Band(5f, 806, 410, 836, 420), 1, PartRole.Trim, false, null, 1f, true);
            Add(d, "ArmF_Wrap2", B.ArmFL, Slot.FarArm + 1, H.Wrap, Band(5f, 796, 434, 822, 442), 1, PartRole.Trim, false, null, 1f, true);
            if (straw) Straw(d, "ArmF_Straw", B.ArmFL, Slot.FarArm + 1, true, 800, 446, 808, 436);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, H.Glove, V(780, 450, 810, 452, 814, 478, 802, 494, 780, 490, 772, 470), 0, PartRole.Body, true, null, 1f, true);
        }

        /// <summary>The far leg: hakama torn at the shin, a wrapped shin, a tabi.</summary>
        public static void FarLeg(RigDef d)
        {
            Add(d, "LegF_Hakama", B.LegFU, Slot.FarLeg, H.ClothFar, V(860, 536, 954, 536, 998, 612, 1018, 692, 990, 676, 970, 706, 940, 690, 920, 712, 902, 642, 870, 592));
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, H.ClothFar, Tube(955, 672, 957, 764, 70, 56));
            Add(d, "LegF_Wrap", B.LegFL, Slot.FarLeg + 1, H.WrapDark, V(932, 702, 982, 702, 980, 756, 934, 756), 1);
            Add(d, "LegF_WrapBand", B.LegFL, Slot.FarLeg + 1, H.Wrap, Band(4f, 932, 722, 982, 730, 934, 744, 980, 750), 2, PartRole.Trim, false);
            Add(d, "FootF_Tabi", B.FootF, Slot.FarFoot, H.ClothFar, V(934, 752, 978, 752, 988, 790, 994, 816, 904, 816, 900, 804, 916, 792, 934, 782));
        }

        /// <summary>Pelvis, and the saya through the sash: the Shade's own, and the tutorial Hollow's too (a Hollow remembers its blade).</summary>
        public static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, H.Cloth, V(812, 392, 952, 392, 946, 566, 816, 566), 0, PartRole.Body, false);
            Add(d, "Saya", B.Hips, Slot.Pelvis, H.ClothDeep, Tube(900, 436, 1070, 556, 24, 18), 1, PartRole.Detail);
            Add(d, "Saya_Crack", B.Hips, Slot.Pelvis, H.Violet, Band(2.4f, 980, 490, 1000, 506, 1024, 520), 2, PartRole.Trim, false, null, 1.6f);
            Add(d, "Saya_Tip", B.Hips, Slot.Pelvis, H.MaskDark, Tube(1052, 543, 1080, 563, 20, 14), 2, PartRole.Metal);
        }

        /// <summary>The near leg: hakama torn at the shin, a wrapped shin and a tabi.</summary>
        public static void NearLeg(RigDef d)
        {
            Add(d, "LegN_Hakama", B.LegNU, Slot.NearLeg, H.Cloth, V(798, 536, 892, 536, 894, 600, 888, 664, 884, 696, 866, 676, 850, 708, 826, 686, 806, 712, 786, 690, 778, 640, 786, 580));
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, H.Cloth, Tube(835, 670, 836, 766, 78, 60));
            Add(d, "LegN_Wrap", B.LegNL, Slot.NearLeg + 1, H.WrapDark, V(811, 700, 861, 700, 859, 756, 815, 756), 1);
            Add(d, "LegN_WrapBand", B.LegNL, Slot.NearLeg + 1, H.Wrap, Band(4f, 811, 720, 861, 728, 813, 742, 859, 750), 2, PartRole.Trim, false);
            Add(d, "LegN_Crack", B.LegNL, Slot.NearLeg + 1, H.Violet, Band(2f, 826, 706, 830, 722, 824, 736), 2, PartRole.Trim, false, null, 1.6f);
            Add(d, "FootN_Tabi", B.FootN, Slot.NearFoot, H.ClothDeep, V(814, 752, 858, 752, 862, 790, 865, 816, 774, 816, 770, 804, 788, 792, 812, 782));
        }

        /// <summary>The near arm: a frayed sleeve, a wrapped forearm, a bare dark fist.</summary>
        public static void NearArm(RigDef d, bool straw = false)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, H.Cloth, Tube(905, 268, 926, 375, 66, 58));
            Add(d, "ArmN_Fray", B.ArmNU, Slot.NearArm, H.Cloth, V(890, 370, 950, 372, 958, 402, 940, 392, 934, 420, 918, 396, 904, 418, 896, 394, 884, 400), 1);
            Add(d, "ArmN_Crack", B.ArmNU, Slot.NearArm, H.Violet, Band(2f, 912, 300, 920, 322, 910, 344), 2, PartRole.Trim, false, null, 1.6f);
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, H.WrapDark, Tube(926, 375, 914, 444, 48, 42));
            Add(d, "ArmN_Wrap1", B.ArmNL, Slot.NearArm + 1, H.Wrap, Band(5f, 892, 402, 942, 410), 1, PartRole.Trim, false);
            Add(d, "ArmN_Wrap2", B.ArmNL, Slot.NearArm + 1, H.Wrap, Band(5f, 896, 426, 936, 432), 1, PartRole.Trim, false);
            if (straw) Straw(d, "ArmN_Straw", B.ArmNL, Slot.NearArm + 1, false, 916, 440, 924, 428);
            Add(d, "HandN_Fist", B.HandN, Slot.NearHand, H.Glove, V(888, 442, 926, 440, 932, 462, 924, 486, 898, 490, 880, 480, 877, 458));
        }

        /// <summary>A tuft of straw bursting out of a cuff (the tutorial Hollow).</summary>
        public static void Straw(RigDef d, string name, B bone, int slot, bool sword, float x, float y, float px, float py)
        {
            var polys = new List<(Vector2[], Color)>();
            float[,] st = { { -26, 6 }, { -14, 20 }, { -2, 24 }, { 12, 18 }, { 22, 4 } };
            for (int i = 0; i < 5; i++)
                polys.Add(Poly(V(x - 6 + i * 3, y - 2, x + st[i, 0], y + st[i, 1], x + 5 + i * 3, y), i % 2 == 0 ? H.Straw : H.StrawDark));
            AddMulti(d, name, bone, slot, polys, 2, null, PartRole.Detail, sword);
        }

        /// <summary>The echo of a blade lying on the hit line (guard (0.08, 0) to tip (0.80, 0.03)): pale violet steel, a glowing edge, a wrapped hilt.</summary>
        public static void Sword(RigDef d)
        {
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
            void S(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim = false, float glow = 1f)
            {
                var p = Add(d, name, B.Sword, 4, c, pts, sub, role, rim, KMath.Hex("#0A0812"), glow, true);
                p.rimGrow = 0.003f;
            }
            S("Blade", 0, H.Steel, PartRole.Metal, AlongSword(d, blade.ToArray()), true);
            S("Blade_Fuller", 1, H.SteelDark, PartRole.Metal, AlongSword(d, 0.12f, Yc(0.12f), 0.70f, Yc(0.70f), 0.70f, Yc(0.70f) + 0.006f, 0.12f, Yc(0.12f) + 0.006f));
            S("Blade_Edge", 1, H.Eye, PartRole.Metal, AlongSword(d, 0.10f, Yc(0.10f) - 0.0165f, 0.74f, Yc(0.74f) - 0.0165f, 0.74f, Yc(0.74f) - 0.011f, 0.10f, Yc(0.10f) - 0.011f), false, 1.8f);
            // a chip out of the blade, a crack of violet through it
            S("Blade_Crack", 1, H.Violet, PartRole.Trim, AlongSword(d, 0.40f, Yc(0.40f) + 0.016f, 0.43f, Yc(0.43f) - 0.002f, 0.46f, Yc(0.46f) + 0.012f, 0.43f, Yc(0.43f) + 0.017f), false, 2f);
            S("Tsuka", 0, H.ClothDeep, PartRole.Body, AlongSword(d, -0.17f, -0.014f, 0.03f, -0.014f, 0.03f, 0.014f, -0.17f, 0.014f), true);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.14f + i * 0.04f;
                S("Tsuka_Wrap" + i, 1, H.Wrap, PartRole.Trim, AlongSword(d, x - 0.014f, 0f, x, -0.011f, x + 0.014f, 0f, x, 0.011f));
            }
            S("Tsuba", 2, H.MaskDark, PartRole.Metal, AlongSword(d, 0.03f, -0.036f, 0.056f, -0.04f, 0.056f, 0.04f, 0.03f, 0.036f), true);
            S("Habaki", 2, H.Mask, PartRole.Metal, AlongSword(d, 0.056f, -0.017f, 0.076f, -0.016f, 0.076f, 0.016f, 0.056f, 0.017f));
        }
    }
}
