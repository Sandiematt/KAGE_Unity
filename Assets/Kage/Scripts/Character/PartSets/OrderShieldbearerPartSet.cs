using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Shieldbearer's look (SAN-98), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the sentinel spear of the
    /// JS Order skin (<c>order_shieldbearer</c>). A kabuto with a tall silver crescent maedate over a flared shikoro on a stern bare
    /// face with a hooked nose and a heavy brow, a full dō with a gorget, big sode, a kusazuri skirt of four rows, and the thing that
    /// names him: a tall lacquered tate (shield) of iron-banded planks with the Order mark, strapped to the near forearm so it stands
    /// in front of his body. The spear is the spear family's (<c>sentinel_spear</c>, a leaf head), drawn by <see cref="SpearVisual"/>.
    /// His outline is a wall: wide at the shoulders, a tall shield and a tall crest, where the Footman's is low and foxy.
    /// </summary>
    public sealed class OrderShieldbearerPartSet : IPartSet
    {
        public string Id => "order_shieldbearer";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER SHIELDBEARER");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            FarArm(d);
            OrderBody.FarLeg(d, 1.05f);
            OrderBody.Pelvis(d);
            OrderBody.Gi(d);
            Torso(d);
            OrderBody.NearLeg(d, 1.05f);
            Skirt(d);
            Head(d);
            OrderBody.NearArm(d, 1.06f);
            Shield(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- far (spear) arm
        static void FarArm(RigDef d)
        {
            OrderBody.FarArm(d, 1.06f);
            // far sode: a box of lames hanging from the shoulder
            var sode = V(826, 250, 900, 244, 910, 296, 904, 340, 828, 346, 820, 296);
            AddMulti(d, "ArmF_Sode", B.ArmFU, Slot.FarArm, new List<(Vector2[], Color)>
            {
                Poly(sode, O.LacquerFar),
                Poly(Band(3f, 822, 286, 908, 282), O.SilverDark),
                Poly(Band(3f, 821, 318, 907, 314), O.SilverDark),
                Poly(Band(3.5f, 824, 342, 906, 338), O.SilverMid),
            }, 1, sode, swordLayer: true);
        }

        // ---------------------------------------------------------------------------------------------- torso
        static void Torso(RigDef d)
        {
            // the dō: lacquered lames laced in crimson, a lit top edge, the Order mark on the chest
            var plate = V(814, 242, 864, 228, 916, 232, 948, 262, 956, 320, 960, 410, 798, 412, 802, 330, 806, 282);
            var polys = new List<(Vector2[], Color)> { Poly(plate, O.Lacquer) };
            float[,] rows = { { 254, 292 }, { 298, 334 }, { 340, 376 }, { 382, 408 } };
            for (int r = 0; r < 4; r++)
            {
                float y0 = rows[r, 0], y1 = rows[r, 1];
                polys.Add(Poly(Band(3f, 800, y1 + 2, 960, y1 + 2), O.SilverDark));
                for (float x = 822; x < 952; x += 21f)
                    polys.Add(Poly(Band(3.6f, x, y0 + 5, x, y1 - 3), O.Lacing));
            }
            polys.Add(Poly(Band(2.6f, 816, 248, 864, 234, 916, 238, 946, 266), O.LacquerLit));
            polys.Add(Poly(Ellipse(856, 330, 19, 19), O.SilverMid));
            polys.Add(Poly(Ellipse(856, 330, 15, 15), O.Lacquer));
            polys.Add(Poly(Band(4.2f, 840, 330, 872, 330), O.SilverMid));
            AddMulti(d, "Do", B.Chest, Slot.TorsoTrim, polys, 1, plate);

            Add(d, "Gorget", B.Chest, Slot.TorsoTrim, O.Lacquer, V(820, 230, 892, 222, 904, 254, 812, 262), 2, PartRole.Detail);
            Add(d, "Gorget_Edge", B.Chest, Slot.TorsoTrim, O.SilverMid, Band(3.2f, 816, 258, 902, 250), 3, PartRole.Metal, false);

            Add(d, "Sash", B.Chest, Slot.Belt, O.ClothDeep, V(802, 392, 958, 396, 959, 418, 802, 414), 0, PartRole.Trim, false);
            Add(d, "Sash_Knot", B.Chest, Slot.BeltTrim, O.Cloth, Ellipse(824, 408, 17, 13), 0, PartRole.Trim);
        }

        // ---------------------------------------------------------------------------------------------- kusazuri
        static void Skirt(RigDef d)
        {
            void Front(string n, float swing, int sub, float pivX, float pivY, params float[] xy) =>
                AddSwinging(d, n, B.Hips, Slot.FrontPlates, sub % 2 == 0 ? O.Lacquer : Shade(O.Lacquer, 1.35f), V(xy), pivX, pivY, swing, sub);
            Front("Kusazuri_F1", 0.25f, 0, 842, 410, 796, 408, 890, 408, 894, 468, 792, 468);
            Front("Kusazuri_F2", 0.4f, 1, 840, 462, 790, 460, 894, 460, 890, 530, 784, 530);
            Front("Kusazuri_F3", 0.55f, 2, 838, 524, 784, 522, 888, 522, 880, 590, 780, 590);
            Front("Kusazuri_F4", 0.7f, 3, 836, 584, 780, 582, 884, 582, 874, 640, 774, 640);
            AddSwinging(d, "Kusazuri_F2_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 840, 468, 838, 526), 840, 462, 0.4f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_F3_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 838, 530, 834, 584), 838, 524, 0.55f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_B1", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 0.9f), V(936, 402, 986, 406, 1006, 470, 930, 468), 958, 402, 0.3f, 1);
            AddSwinging(d, "Kusazuri_B2", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 1.3f), V(926, 462, 1006, 466, 1034, 540, 922, 536), 960, 462, 0.5f, 2);
            AddSwinging(d, "Kusazuri_B2_Hem", B.Hips, Slot.BackPlates, O.SilverDark, Band(4f, 926, 534, 1030, 538), 960, 462, 0.5f, 3, PartRole.Metal, false);
        }

        // ---------------------------------------------------------------------------------------------- head
        static void Head(RigDef d)
        {
            // the flared shikoro: lames fanning out and down behind the head and over the shoulder
            AddSwinging(d, "Shikoro", B.Head, Slot.Neck, O.Lacquer, V(886, 150, 940, 164, 984, 214, 992, 262, 872, 248, 856, 214), 900, 156, 0.35f, 2);
            AddSwinging(d, "Shikoro_Lace", B.Head, Slot.Neck, O.SilverDark, Band(3.4f, 880, 190, 980, 222, 986, 258), 900, 156, 0.35f, 3, PartRole.Metal, false);

            // the bare face under the helmet: a hooked nose, a heavy brow, a firm chin
            var face = V(820, 146, 852, 142, 858, 178, 852, 214, 838, 234, 808, 236, 804, 222, 800, 212, 786, 209, 782, 201, 794, 192, 800, 174, 806, 156);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, O.Skin),
                Poly(V(800, 213, 786, 210, 794, 204, 802, 206), O.SkinShade),                 // under the hook of the nose
                Poly(V(800, 214, 808, 232, 838, 232, 850, 214, 820, 218), O.SkinShade),       // the jaw's shadow
                Poly(Band(2.4f, 800, 214, 826, 218), O.SkinShade),                           // pressed lips
            }, 0, face);
            // the heavy brow
            Add(d, "Brow", B.Head, Slot.Face, O.Lacquer, V(790, 160, 836, 150, 858, 158, 856, 166, 832, 164, 796, 174), 1, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(820, 172, 836, 168, 848, 172, 836, 177, 823, 178), 2, PartRole.Eye, false, null, 2.8f);

            // the kabuto bowl, a brow ridge and a cheek guard
            Add(d, "Kabuto", B.Head, Slot.Head, O.Lacquer, V(796, 162, 800, 132, 822, 108, 858, 98, 892, 108, 912, 138, 912, 182, 902, 214, 874, 224, 862, 196, 858, 160, 832, 150), 3);
            Add(d, "Kabuto_Brim", B.Head, Slot.HeadTop, O.SilverMid, Band(5f, 794, 160, 806, 150, 840, 144, 860, 152), 0, PartRole.Metal, false);
            Add(d, "Kabuto_Ridge", B.Head, Slot.HeadTop, O.LacquerLit, Band(2.6f, 806, 128, 836, 106, 876, 104, 902, 126), 1, PartRole.Detail, false);
            Add(d, "Kabuto_Cheek", B.Head, Slot.HeadTop, O.Lacquer, V(852, 196, 876, 200, 878, 240, 852, 246), 2, PartRole.Detail);
            // the maedate: a tall silver crescent rising from the brow
            var crest = V(812, 112, 800, 60, 822, 14, 848, -8, 842, 22, 826, 52, 832, 84, 848, 108);
            AddMulti(d, "Maedate", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(crest, O.SilverMid),
                Poly(Band(2.6f, 806, 100, 802, 62, 824, 18, 846, -4), O.Silver),
            }, 3, crest, PartRole.Metal);
        }

        // ---------------------------------------------------------------------------------------------- the tate
        /// <summary>
        /// A tall plank shield with a silver rim, two iron bands and the Order mark (JS <c>orderShield</c>), centred a little in front of
        /// the near hand and strapped to the near forearm so it follows the arm. 116 px wide and about 360 px tall in the reference frame
        /// (JS 26 x 80, drawn a quarter bigger so it reads at fighting distance). Its planks are a lighter slate than the armour and its rim is silver, so the dark soldier's one big shape reads.
        /// </summary>
        static void Shield(RigDef d)
        {
            float cx = 880, cy = 420, w = 58, h = 176;
            Vector2[] Outline(float k) => V(cx - w * k, cy - (h - 14) * k, cx - w * 0.5f * k, cy - (h + 4) * k, cx, cy - (h + 10) * k, cx + w * 0.5f * k, cy - (h + 4) * k, cx + w * k, cy - (h - 14) * k,
                                            cx + w * 0.92f * k, cy + (h - 8) * k, cx + w * 0.5f * k, cy + (h + 8) * k, cx, cy + (h + 10) * k, cx - w * 0.5f * k, cy + (h + 8) * k, cx - w * 0.92f * k, cy + (h - 8) * k);
            var outline = Outline(1f);
            var plank = Shade(O.SilverDark, 1.05f);
            var polys = new List<(Vector2[], Color)> { Poly(outline, O.SilverMid), Poly(Outline(0.86f), plank) };      // silver rim, then the planks inside it
            for (int i = 0; i < 3; i++)                                                                                  // three planks, the middle one lighter
            {
                float x0 = cx - w * 0.8f + i * w * 0.53f, x1 = x0 + w * 0.53f;
                if (i == 1) polys.Add(Poly(V(x0, cy - h + 8, x1, cy - h + 8, x1, cy + h - 8, x0, cy + h - 8), Shade(plank, 1.22f)));
            }
            foreach (float x in new[] { -w * 0.27f, w * 0.27f })                                                         // plank seams
                polys.Add(Poly(Band(2.6f, cx + x, cy - h + 8, cx + x, cy + h - 8), O.LacquerFar));
            polys.Add(Poly(Band(3f, cx - w * 0.8f, cy - h + 20, cx - w * 0.45f, cy - h + 2, cx, cy - h - 2), O.Silver));      // lit top edge
            foreach (float y in new[] { -h * 0.62f, h * 0.62f })                                                         // iron bands with rivets
            {
                polys.Add(Poly(V(cx - w, cy + y - 11, cx + w, cy + y - 11, cx + w, cy + y + 11, cx - w, cy + y + 11), O.Lacquer));
                polys.Add(Poly(Band(3f, cx - w, cy + y - 9, cx + w, cy + y - 9), O.Silver));
                foreach (float rx in new[] { -w * 0.62f, 0f, w * 0.62f })
                    polys.Add(Poly(Ellipse(cx + rx, cy + y + 1, 4.2f, 4.2f), O.Silver));
            }
            polys.Add(Poly(Ellipse(cx, cy, 25, 25), O.Lacing));                  // the Order mark: a crimson barred circle on a dark boss
            polys.Add(Poly(Ellipse(cx, cy, 19, 19), O.Lacquer));
            polys.Add(Poly(Band(5.4f, cx - 18, cy, cx + 18, cy), O.Silver));
            polys.Add(Poly(Band(3.2f, cx - w * 0.7f, cy - h * 0.3f, cx, cy - h * 0.22f, cx + w * 0.7f, cy - h * 0.3f), O.Lacing));     // a crimson brush stroke
            AddMulti(d, "Tate", B.ArmNL, Slot.NearArm + 1, polys, 3, outline);
        }
    }
}
