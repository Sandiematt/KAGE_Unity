using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The tutorial Hollow's look (SAN-76): the Shade's training husk. Same violet-black body, but dressed as a practice dummy: a
    /// stitched sack hood with straw bursting from its crown, a plank practice mask strapped over the eyes, rope bindings crossed
    /// over the chest, a rope sash, straw at the cuffs and a short rag skirt. Gentler and lower than the Shade, with no floating
    /// shards. Based on the JS Hollow skin (<c>hollow_tutorial</c>: sack hood, plank mask, rope, straw).
    /// </summary>
    public sealed class HollowTutorialPartSet : IPartSet
    {
        public string Id => "hollow_tutorial";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW (TUTORIAL)");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
            d.scarf = false;
            d.softCoat = false;

            HollowBody.FarArm(d, straw: true);
            HollowBody.FarLeg(d);
            HollowBody.Pelvis(d);
            Torso(d);
            HollowBody.NearLeg(d);
            Skirt(d);
            Head(d);
            HollowBody.NearArm(d, straw: true);
            HollowBody.Sword(d);
            return d;
        }

        static void Torso(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, H.Cloth, V(834, 226, 870, 214, 908, 220, 932, 240, 946, 268, 950, 310, 951, 355, 953, 425, 880, 428, 808, 425, 807, 360, 812, 322, 818, 290, 824, 260));
            // a patched, open gi with straw poking out of the tears
            var gi = V(814, 244, 862, 230, 908, 236, 944, 264, 954, 330, 956, 408, 806, 410, 802, 340, 806, 284);
            var polys = new List<(Vector2[], Color)> { Poly(gi, H.ClothDeep) };
            polys.Add(Poly(Band(2.4f, 818, 254, 850, 240, 876, 300, 864, 346), H.ClothLit));
            polys.Add(Poly(V(900, 296, 934, 292, 940, 326, 906, 332), H.Sack));          // a patch
            polys.Add(Poly(Band(1.8f, 904, 298, 936, 296, 938, 324, 906, 328, 900, 298), H.SackDark));
            polys.Add(Poly(Band(1.6f, 912, 292, 912, 300), H.Rope));
            polys.Add(Poly(V(826, 330, 846, 340, 840, 360, 822, 350), H.FaceDark));       // a tear
            polys.Add(Poly(V(830, 336, 846, 334, 850, 352, 836, 352), H.Straw));
            polys.Add(Poly(Band(2f, 830, 340, 842, 346), H.StrawDark));
            AddMulti(d, "Gi", B.Chest, Slot.TorsoTrim, polys, 1, gi);
            // rope crossed over the chest in two bands, knotted where they meet
            var rope = new List<(Vector2[], Color)>
            {
                Poly(Band(9f, 820, 250, 858, 308, 918, 392), H.Rope),
                Poly(Band(9f, 944, 266, 892, 318, 822, 392), H.Rope),
                Poly(Band(2.2f, 820, 252, 858, 308, 916, 390), H.RopeDark),
                Poly(Band(2.2f, 942, 268, 892, 318, 824, 390), H.RopeDark),
                Poly(Ellipse(872, 322, 13, 12), H.Rope),
                Poly(Ellipse(872, 322, 8, 7), H.RopeDark),
            };
            AddMulti(d, "Rope_Cross", B.Chest, Slot.TorsoTrim, rope, 2, null, PartRole.Detail);

            // rope sash, its ends hanging
            Add(d, "Sash", B.Chest, Slot.Belt, H.Rope, V(802, 384, 956, 388, 957, 410, 802, 406), 0, PartRole.Trim, false);
            Add(d, "Sash_Twist", B.Chest, Slot.Belt, H.RopeDark, Band(2.4f, 806, 392, 830, 400, 856, 392, 884, 400, 912, 392, 940, 400), 1, PartRole.Trim, false);
            AddSwinging(d, "Sash_Tail", B.Chest, Slot.BeltTrim, H.Rope, Band(7f, 822, 402, 832, 440, 826, 480), 822, 402, 0.9f, 1, PartRole.Trim);
            AddSwinging(d, "Sash_Tail2", B.Chest, Slot.BeltTrim, H.RopeDark, Band(6f, 836, 402, 850, 436, 856, 466), 836, 402, 0.9f, 0, PartRole.Trim);
            Add(d, "Neck", B.Neck, Slot.Neck, H.SackDark, V(838, 200, 868, 200, 872, 240, 842, 240), 0, PartRole.Body, false);
        }

        static void Skirt(RigDef d)
        {
            // a short rag skirt: two torn panels in front and one behind, ending at the knee
            AddSwinging(d, "Skirt_F1", B.Hips, Slot.FrontPlates, H.Cloth, HollowBody.Torn(796, 888, 402, 540, 4, 18, 5), 838, 404, 0.5f, 0);
            AddSwinging(d, "Skirt_F2", B.Hips, Slot.FrontPlates, H.ClothDeep, HollowBody.Torn(790, 856, 410, 580, 3, 22, 1, -3), 826, 408, 0.8f, 1);
            AddSwinging(d, "Skirt_B1", B.Hips, Slot.BackPlates, H.ClothFar, HollowBody.Torn(920, 1008, 402, 570, 4, 22, 3), 960, 402, 0.9f, 1);
        }

        static void Head(RigDef d)
        {
            // straw bursting from the crown of the sack: spikes that sway
            float[,] straw = { { 838, 106, 806, 36 }, { 856, 102, 856, 22 }, { 878, 104, 916, 34 }, { 822, 118, 770, 64 }, { 896, 118, 950, 70 } };
            for (int i = 0; i < 5; i++)
            {
                float x0 = straw[i, 0], y0 = straw[i, 1], tx = straw[i, 2], ty = straw[i, 3];
                AddSwinging(d, "Straw_" + i, B.Head, Slot.HeadTop, i % 2 == 0 ? H.Straw : H.StrawDark,
                    V(x0 - 7, y0 + 6, tx, ty, x0 + 7, y0 + 4), x0, y0 + 6, 0.8f, i % 3, PartRole.Detail);
            }
            // the sack hood's tail: the rope's loose ends at the nape
            AddSwinging(d, "Sack_Tail", B.Head, Slot.Neck, H.RopeDark, V(902, 190, 920, 196, 948, 250, 934, 256, 912, 224), 906, 192, 0.9f, 2, PartRole.Detail);

            // the stitched sack: a lumpy hood down to the throat
            var sack = V(804, 170, 806, 136, 828, 112, 860, 102, 892, 112, 912, 140, 914, 182, 906, 216, 880, 232, 846, 230, 818, 216);
            AddMulti(d, "Sack", B.Head, Slot.Head, new List<(Vector2[], Color)>
            {
                Poly(sack, H.Sack),
                Poly(Band(2f, 868, 104, 842, 140, 846, 190), H.SackDark),
                Poly(Band(1.6f, 856, 112, 864, 118, 850, 128, 858, 134, 846, 146, 854, 152, 842, 164), H.Rope),
                Poly(V(884, 150, 904, 146, 908, 168, 888, 172), H.SackDark),                 // a patch
                Poly(Band(1.4f, 886, 152, 902, 150, 906, 166, 888, 170, 884, 152), H.Rope),
                Poly(Band(2.2f, 806, 168, 810, 146, 830, 120), H.Violet),                    // light along a break
            }, 3, sack);
            // the rope at the throat
            Add(d, "Throat_Rope", B.Head, Slot.Face, H.Rope, Band(8f, 812, 212, 846, 224, 880, 222, 904, 210), 0, PartRole.Detail, false);

            // the gaunt face showing under the plank: snub nose, parted mouth, chin
            var face = V(806, 168, 840, 164, 852, 196, 846, 218, 824, 230, 802, 224, 796, 208, 788, 196, 800, 188);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, H.Face),
                Poly(V(798, 208, 812, 218, 830, 222, 846, 214, 826, 208), H.FaceDark),
                Poly(Band(2f, 788, 196, 798, 210), H.Violet),
            }, 0, face);
            // the plank across the eyes, nailed
            var plank = V(780, 150, 852, 142, 856, 182, 784, 188);
            AddMulti(d, "Plank", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(plank, H.Wood),
                Poly(Band(1.8f, 784, 160, 854, 154), H.WoodDark),
                Poly(Band(1.8f, 786, 174, 854, 168), H.WoodDark),
                Poly(Ellipse(795, 158, 3.4f, 3.4f), H.Rope),
                Poly(Ellipse(842, 154, 3.4f, 3.4f), H.Rope),
                Poly(Band(2.2f, 818, 146, 812, 166, 822, 184), H.Violet),
            }, 1, plank);
            // the eye glows through a slit in the plank
            Add(d, "Eye_Slit", B.Head, Slot.Face, H.WoodDark, V(804, 160, 840, 156, 842, 168, 806, 172), 2, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, H.Eye, V(818, 162, 834, 159, 840, 164, 830, 168, 820, 168), 3, PartRole.Eye, false, null, 3f);
        }
    }
}
