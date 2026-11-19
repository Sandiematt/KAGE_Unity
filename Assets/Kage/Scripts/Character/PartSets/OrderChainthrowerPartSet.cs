using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using O = Kage.OrderPalette;

namespace Kage
{
    /// <summary>
    /// The Order Chainthrower's look (SAN-99), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the chain hook of the
    /// JS Order skin (<c>order_chainthrower</c>). A wide flat jingasa over a dark hood with crimson hat cords, a pale fox menpo over
    /// the lower face, a light hara-ate breastplate with a chain bandolier across the chest and a coil of chain at the hip, no sode,
    /// a three-plate kusazuri. Order slate and silver like the Footman, but the wide hat is the silhouette (no fox ears, no saya, no
    /// big sode). The sickle and its chain are the chain family's (<c>chain_hook</c>), drawn by <see cref="ChainVisual"/>.
    /// </summary>
    public sealed class OrderChainthrowerPartSet : IPartSet
    {
        public string Id => "order_chainthrower";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("ORDER CHAINTHROWER");
            d.rimColor = O.Rim;
            d.bodyColor = O.Cloth;
            d.scarf = false;
            d.softCoat = false;

            OrderBody.FarArm(d, 0.95f);
            OrderBody.FarLeg(d);
            OrderBody.Pelvis(d);
            Coil(d);
            OrderBody.Gi(d);
            Torso(d);
            OrderBody.NearLeg(d);
            Skirt(d);
            Head(d);
            OrderBody.NearArm(d, 0.95f);
            return d;
        }

        /// <summary>A coil of spare chain hung at the back of the belt.</summary>
        static void Coil(RigDef d)
        {
            var polys = new List<(Vector2[], Color)>();
            for (int i = 0; i < 3; i++)
            {
                float y = 440 + i * 16;
                polys.Add(Poly(Ellipse(962, y, 40, 12), O.SilverDark));
                polys.Add(Poly(Ellipse(962, y, 30, 6), O.Cloth));
            }
            polys.Add(Poly(Band(5f, 940, 420, 944, 500, 930, 540), O.SilverMid));
            AddMulti(d, "Chain_Coil", B.Hips, Slot.Pelvis, polys, 1, null, PartRole.Metal);
        }

        static void Torso(RigDef d)
        {
            var plate = V(820, 258, 866, 246, 916, 250, 948, 274, 954, 330, 954, 392, 806, 392, 806, 330, 812, 288);
            var polys = new List<(Vector2[], Color)> { Poly(plate, O.Lacquer) };
            polys.Add(Poly(Band(3f, 804, 352, 956, 352), O.SilverDark));
            for (float x = 826; x < 950; x += 24f) polys.Add(Poly(Band(3.6f, x, 268, x, 346), O.Lacing));
            for (float x = 826; x < 950; x += 24f) polys.Add(Poly(Band(3.6f, x, 358, x, 386), O.Lacing));
            polys.Add(Poly(Band(2.6f, 822, 262, 866, 250, 914, 254, 944, 278), O.LacquerLit));
            AddMulti(d, "Hara", B.Chest, Slot.TorsoTrim, polys, 1, plate);

            // the chain bandolier: a band of linked rings from the far shoulder across to the near hip
            var links = new List<(Vector2[], Color)>();
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                float x = Mathf.Lerp(826, 940, t), y = Mathf.Lerp(250, 398, t);
                links.Add(Poly(Ellipse(x, y, 15, 9, 55f * (i % 2 == 0 ? 1 : -1) + 0f), O.SilverMid));
                links.Add(Poly(Ellipse(x, y, 9, 4, 55f * (i % 2 == 0 ? 1 : -1)), O.Lacquer));
            }
            AddMulti(d, "Bandolier", B.Chest, Slot.TorsoTrim, links, 2, null, PartRole.Metal);
            Add(d, "Bandolier_Lit", B.Chest, Slot.TorsoTrim, O.Silver, Band(2f, 826, 244, 940, 392), 3, PartRole.Metal, false);

            Add(d, "Sash", B.Chest, Slot.Belt, O.ClothDeep, V(802, 384, 956, 388, 957, 410, 802, 406), 0, PartRole.Trim, false);
            Add(d, "Sash_Knot", B.Chest, Slot.BeltTrim, O.Cloth, Ellipse(822, 400, 17, 13), 0, PartRole.Trim);
        }

        static void Skirt(RigDef d)
        {
            AddSwinging(d, "Kusazuri_F1", B.Hips, Slot.FrontPlates, O.Lacquer, V(808, 404, 884, 404, 888, 462, 804, 462), 846, 404, 0.3f, 0);
            AddSwinging(d, "Kusazuri_F2", B.Hips, Slot.FrontPlates, Shade(O.Lacquer, 1.35f), V(802, 458, 886, 458, 880, 520, 796, 520), 842, 458, 0.45f, 1);
            AddSwinging(d, "Kusazuri_F3", B.Hips, Slot.FrontPlates, O.Lacquer, V(796, 516, 880, 516, 870, 572, 790, 572), 838, 516, 0.6f, 2);
            AddSwinging(d, "Kusazuri_F2_Lace", B.Hips, Slot.FrontPlates, O.Lacing, Band(4f, 842, 464, 840, 514), 842, 458, 0.45f, 3, PartRole.Trim, false);
            AddSwinging(d, "Kusazuri_B1", B.Hips, Slot.BackPlates, Shade(O.Lacquer, 0.9f), V(930, 402, 982, 406, 1000, 476, 924, 472), 956, 402, 0.4f, 1);
        }

        static void Head(RigDef d)
        {
            // the hood and a shadowed upper face
            Add(d, "Hood", B.Head, Slot.Head, O.ClothDeep, V(800, 178, 802, 146, 826, 122, 860, 112, 892, 122, 910, 148, 910, 186, 900, 214, 876, 228, 844, 228, 816, 214), 2);
            var eyes = V(802, 156, 840, 150, 856, 166, 856, 190, 806, 190, 796, 172);
            Add(d, "Face_Upper", B.Head, Slot.Face, O.Skin, eyes, 0, PartRole.Body, false);
            Add(d, "Eye_Shadow", B.Head, Slot.Face, O.Lacquer, V(804, 164, 838, 158, 854, 166, 838, 176, 810, 176), 1, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, O.Eye, V(820, 166, 838, 162, 850, 166, 838, 172, 823, 173), 2, PartRole.Eye, false, null, 2.8f);

            // the fox menpo: a pale muzzle over the nose and mouth
            var mask = V(812, 176, 848, 174, 858, 204, 850, 230, 826, 240, 794, 234, 774, 220, 756, 206, 782, 196, 800, 186);
            AddMulti(d, "Menpo", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(mask, O.Mask),
                Poly(V(792, 220, 826, 238, 850, 228, 856, 206, 826, 214), O.MaskShade),
                Poly(Ellipse(759, 205, 7, 5.5f), O.Lacquer),
                Poly(Band(2.6f, 800, 214, 826, 214), O.SilverDark),
            }, 3, mask);

            // the jingasa: a wide flat cone of lacquer, silver rim, crimson cords tied under the chin
            var hat = V(722, 130, 790, 100, 858, 66, 928, 100, 996, 132, 940, 142, 858, 124, 776, 142);
            AddMulti(d, "Jingasa", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(hat, O.Lacquer),
                Poly(V(790, 100, 858, 66, 928, 100, 858, 90), O.LacquerLit),
                Poly(Band(3.4f, 724, 132, 776, 142, 858, 125, 940, 143, 994, 133), O.SilverMid),
                Poly(Band(2.4f, 858, 72, 858, 122), O.SilverDark),
            }, 0, hat);
            Add(d, "Hat_Cord", B.Head, Slot.HeadTop, O.Lacing, Band(3.4f, 800, 138, 790, 190, 812, 232), 1, PartRole.Trim, false);
            AddSwinging(d, "Hat_Cord_Tail", B.Head, Slot.HeadTop, O.Lacing, V(806, 226, 816, 228, 824, 280, 812, 296, 808, 262), 810, 228, 0.9f, 2, PartRole.Trim, false);
        }
    }
}
