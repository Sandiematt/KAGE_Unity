using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Runner's look (SAN-97), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the dagger runner of the
    /// JS Order skin (<c>order_runner</c>). A lean courier in a peaked shinobi hood with two long tails streaming behind it, a bare
    /// narrow face under a cloth veil, a light hara-ate breastplate with a silver chest strap, no sode, a short kusazuri of two
    /// plates, slim sleeves and legs, a sash with a long tail and a pair of dagger sheaths crossed on the small of the back. Same
    /// Order slate and silver as the Footman, but lean and low where he is blocky, with no fox ears and no saya. The weapon pair is
    /// the twin family's (<c>shadow_daggers</c>), drawn by <see cref="TwinVisual"/>.
    /// </summary>
    public sealed class OrderRunnerPartSet : IPartSet
    {
        public string Id => "order_runner";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER RUNNER");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            OrderBody.FarArm(d, 0.86f);
            OrderBody.FarLeg(d, 0.9f);
            OrderBody.Pelvis(d);
            Sheaths(d);
            OrderBody.Gi(d);
            Torso(d);
            OrderBody.NearLeg(d, 0.9f);
            Skirt(d);
            Head(d);
            OrderBody.NearArm(d, 0.86f);
            return d;
        }

        /// <summary>Two dagger sheaths crossed on the small of the back, their hilts up behind the hip.</summary>
        static void Sheaths(RigDef d)
        {
            Add(d, "Sheath_1", B.Hips, Slot.Pelvis, O.Lacquer, Tube(1000, 396, 930, 470, 22, 18), 1, PartRole.Detail);
            Add(d, "Sheath_2", B.Hips, Slot.Pelvis, O.LacquerFar, Tube(990, 470, 940, 410, 22, 18), 2, PartRole.Detail);
            Add(d, "Sheath_Mouth", B.Hips, Slot.Pelvis, O.SilverMid, Band(6f, 984, 392, 1010, 404), 3, PartRole.Metal, false);
            Add(d, "Sheath_Tip", B.Hips, Slot.Pelvis, O.SilverMid, Band(5f, 934, 466, 946, 478), 3, PartRole.Metal, false);
        }

        static void Torso(RigDef d)
        {
            // the hara-ate: a short breastplate laced in crimson, a lit top edge and a silver strap over the near shoulder
            var plate = V(820, 262, 866, 250, 916, 254, 948, 276, 954, 330, 954, 384, 806, 384, 806, 330, 812, 292);
            var polys = new List<(Vector2[], Color)> { Poly(plate, O.Lacquer) };
            polys.Add(Poly(Band(3f, 804, 345, 956, 345), O.SilverDark));
            for (float x = 826; x < 950; x += 24f) polys.Add(Poly(Band(3.6f, x, 270, x, 340), O.Lacing));
            for (float x = 826; x < 950; x += 24f) polys.Add(Poly(Band(3.6f, x, 350, x, 380), O.Lacing));
            polys.Add(Poly(Band(2.6f, 822, 266, 866, 254, 914, 258, 944, 280), O.LacquerLit));
            AddMulti(d, "Hara", B.Chest, Slot.TorsoTrim, polys, 1, plate);
            // the strap, from the far shoulder across to the near hip
            Add(d, "Chest_Strap", B.Chest, Slot.TorsoTrim, O.SilverMid, Band(7f, 828, 244, 880, 320, 936, 392), 2, PartRole.Metal, false);
            Add(d, "Chest_StrapEdge", B.Chest, Slot.TorsoTrim, O.Silver, Band(2f, 832, 242, 882, 316, 938, 388), 3, PartRole.Metal, false);

            Add(d, "Sash", B.Chest, Slot.Belt, O.ClothDeep, V(802, 384, 956, 388, 957, 410, 802, 406), 0, PartRole.Trim, false);
            // a long sash tail that streams with the run
            AddSwinging(d, "Sash_Tail", B.Chest, Slot.BeltTrim, O.Cloth, V(808, 402, 836, 402, 860, 500, 880, 580, 846, 560, 822, 490), 824, 404, 1.2f, 1, PartRole.Trim);
        }

        static void Skirt(RigDef d)
        {
            // short: two front plates and one behind
            AddSwinging(d, "Kusazuri_F1", B.Hips, Slot.FrontPlates, O.Lacquer, V(808, 404, 884, 404, 886, 456, 804, 456), 846, 404, 0.4f, 0);
            AddSwinging(d, "Kusazuri_F2", B.Hips, Slot.FrontPlates, Shade(O.Lacquer, 1.35f), V(804, 452, 884, 452, 876, 506, 800, 506), 842, 452, 0.6f, 1);
            AddSwinging(d, "Kusazuri_F2_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 842, 458, 840, 500), 842, 452, 0.6f, 2, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_B1", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 0.9f), V(930, 402, 982, 406, 996, 470, 926, 468), 956, 402, 0.5f, 1);
        }

        static void Head(RigDef d)
        {
            // two long hood tails streaming straight back
            AddSwinging(d, "Tail_Far", B.Head, Slot.Neck, O.ClothFar, V(896, 126, 934, 122, 1010, 148, 1100, 190, 1030, 176, 958, 166, 904, 162), 906, 140, 1.5f, 1);
            AddSwinging(d, "Tail_Near", B.Head, Slot.Neck, O.ClothDeep, V(896, 150, 936, 150, 1000, 182, 1070, 232, 1000, 214, 950, 196, 902, 188), 906, 160, 1.3f, 2);

            // the face first: a narrow bare profile with a long straight nose, under the hood
            var face = V(822, 144, 852, 140, 858, 176, 852, 214, 836, 232, 808, 234, 800, 220, 790, 208, 772, 202, 792, 192, 798, 172, 806, 154);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, O.Skin),
                Poly(V(792, 196, 772, 202, 790, 208, 798, 202), O.SkinShade),
                Poly(Band(2.2f, 800, 178, 792, 190), O.SkinShade),
            }, 0, face);
            // the veil over the mouth and chin, cloth tied behind the ear
            var veil = V(790, 206, 812, 202, 850, 196, 858, 214, 842, 236, 806, 238, 796, 224);
            Add(d, "Veil", B.Head, Slot.Face, O.ClothDeep, veil, 1, PartRole.Body, true);
            Add(d, "Veil_Fold", B.Head, Slot.Face, O.Cloth, Band(2.6f, 796, 212, 830, 208, 856, 204), 2, PartRole.Trim, false);

            // the peaked hood: pulled forward over the brow into a point, open on the face
            Add(d, "Hood", B.Head, Slot.Head, O.Cloth, V(808, 176, 806, 142, 786, 126, 774, 114, 806, 108, 832, 100, 866, 98, 896, 112, 912, 142, 912, 182, 902, 212, 876, 226, 856, 206, 852, 172, 828, 158), 3);
            Add(d, "Hood_Peak", B.Head, Slot.HeadTop, O.ClothDeep, V(774, 114, 806, 108, 812, 124, 792, 128), 0, PartRole.Trim, false);
            Add(d, "Hood_Band", B.Head, Slot.HeadTop, O.SilverMid, Band(5f, 800, 146, 830, 142, 860, 146), 1, PartRole.Metal, false);
            // a narrow slit of eye
            Add(d, "Eye_Shadow", B.Head, Slot.Face, O.Lacquer, V(804, 164, 836, 158, 852, 166, 836, 174, 810, 174), 3, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(820, 166, 836, 162, 848, 166, 836, 171, 823, 172), 4, PartRole.Eye, false, null, 2.8f);
        }
    }
}
