using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The pieces the light-armoured Order soldiers share (SAN-97 Runner, SAN-99 Chainthrower), drawn with <see cref="OpponentKit"/>
    /// on Kage's skeleton: slate sleeves over silver kote and no sode, wide hakama over silver suneate, a plain pelvis. Each soldier
    /// adds its own torso dressing, skirt and head on top. The Footman has his own heavier pieces (sode, saya).
    /// </summary>
    public static class OrderBody
    {
        /// <summary>The far (weapon) arm: a slate sleeve, kote and a glove. <paramref name="k"/> scales the width (the Runner's is lean).</summary>
        public static void FarArm(RigDef d, float k = 1f)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, O.ClothFar, Tube(862, 272, 822, 385, 60 * k, 54 * k), swordLayer: true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, Shade(O.SilverDark, 0.75f), Tube(822, 385, 797, 452, 44 * k, 38 * k), 0, PartRole.Metal, swordLayer: true);
            Add(d, "ArmF_KoteBand", B.ArmFL, Slot.FarArm + 1, O.SilverMid, Band(4f, 785, 424, 822, 432), 1, PartRole.Metal, false, swordLayer: true);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, O.Glove, V(780, 450, 810, 452, 814, 478, 802, 494, 780, 490, 772, 470), swordLayer: true);
        }

        public static void NearArm(RigDef d, float k = 1f)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, O.Cloth, Tube(905, 268, 926, 375, 66 * k, 58 * k));
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, O.SilverDark, Tube(926, 375, 914, 444, 48 * k, 42 * k), 0, PartRole.Metal);
            Add(d, "ArmN_KoteBand", B.ArmNL, Slot.NearArm + 1, O.SilverMid, Band(4f, 888, 408, 944, 408), 1, PartRole.Metal, false);
            Add(d, "HandN_Fist", B.HandN, Slot.NearHand, O.Glove, V(888, 442, 926, 440, 932, 462, 924, 486, 898, 490, 880, 480, 877, 458));
        }

        public static void FarLeg(RigDef d, float k = 1f)
        {
            Add(d, "LegF_Hakama", B.LegFU, Slot.FarLeg, O.ClothFar, V(860, 536, 954, 536, 998, 612, 1018, 692, 932, 698, 902, 642, 870, 592));
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, O.ClothFar, Tube(955, 672, 957, 764, 70 * k, 56 * k));
            Add(d, "LegF_Suneate", B.LegFL, Slot.FarLeg + 1, Shade(O.SilverDark, 0.7f), V(934, 700, 980, 700, 978, 752, 936, 752), 1, PartRole.Metal);
            Add(d, "FootF_Tabi", B.FootF, Slot.FarFoot, O.ClothFar, V(934, 752, 978, 752, 988, 790, 994, 816, 904, 816, 900, 804, 916, 792, 934, 782));
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, O.LacquerFar, V(900, 808, 996, 808, 996, 818, 904, 818), 1, PartRole.Detail, false);
        }

        public static void NearLeg(RigDef d, float k = 1f)
        {
            Add(d, "LegN_Hakama", B.LegNU, Slot.NearLeg, O.Cloth, V(798, 536, 892, 536, 894, 600, 888, 664, 880, 700, 794, 700, 778, 640, 786, 580));
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, O.Cloth, Tube(835, 670, 836, 766, 78 * k, 60 * k));
            Add(d, "LegN_Suneate", B.LegNL, Slot.NearLeg + 1, Shade(O.SilverDark, 1.1f), V(813, 698, 861, 698, 859, 752, 817, 752), 1, PartRole.Metal);
            Add(d, "LegN_SuneateLit", B.LegNL, Slot.NearLeg + 1, O.SilverMid, Band(3.4f, 819, 704, 818, 748), 2, PartRole.Metal, false);
            Add(d, "FootN_Tabi", B.FootN, Slot.NearFoot, O.ClothDeep, V(814, 752, 858, 752, 862, 790, 865, 816, 774, 816, 770, 804, 788, 792, 812, 782));
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, O.Lacquer, V(768, 808, 866, 808, 866, 818, 772, 818), 1, PartRole.Detail, false);
        }

        /// <summary>A plain pelvis and the neck.</summary>
        public static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, O.Cloth, V(812, 392, 952, 392, 946, 566, 816, 566), 0, PartRole.Body, false);
            Add(d, "Neck", B.Neck, Slot.Neck, O.ClothDeep, V(838, 200, 868, 200, 872, 240, 842, 240), 0, PartRole.Body, false);
        }

        /// <summary>The slate gi torso under the armour.</summary>
        public static void Gi(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, O.Cloth, V(834, 226, 870, 214, 908, 220, 932, 240, 946, 268, 950, 310, 951, 355, 953, 425, 880, 428, 808, 425, 807, 360, 812, 322, 818, 290, 824, 260));
        }
    }
}
