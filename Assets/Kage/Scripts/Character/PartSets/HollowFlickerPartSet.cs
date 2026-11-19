using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The Hollow Flicker's look (SAN-101), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: the dagger Hollow of the
    /// JS Hollow skin (<c>hollow_flicker</c>). Slim and hunched, a hood peaked forward like a beak with two long torn tails, a thin
    /// cracked visor over the eyes, a torso bound in bandage wraps instead of a robe, and one long split tatter behind where the
    /// Shade has a skirt of panels. Same violet-black cloth, wraps and violet glow as every Hollow (<see cref="HollowPalette"/>,
    /// <see cref="HollowBody"/>), but no shard crown, no haori and no saya. The daggers are the twin family's
    /// (<c>shadow_daggers</c>), drawn by <see cref="TwinVisual"/>.
    /// </summary>
    public sealed class HollowFlickerPartSet : IPartSet
    {
        public string Id => "hollow_flicker";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW FLICKER");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
            d.scarf = false;
            d.softCoat = false;

            HollowBody.FarArm(d);
            HollowBody.FarLeg(d);
            Pelvis(d);
            Torso(d);
            HollowBody.NearLeg(d);
            Tatter(d);
            Head(d);
            HollowBody.NearArm(d);
            return d;
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, H.Cloth, V(812, 392, 952, 392, 946, 566, 816, 566), 0, PartRole.Body, false);
            Add(d, "Neck", B.Neck, Slot.Neck, H.ClothDeep, V(838, 200, 868, 200, 872, 240, 842, 240), 0, PartRole.Body, false);
        }

        static void Torso(RigDef d)
        {
            // a hunched, narrow chest: the shoulders pitched forward over the hips
            Add(d, "Torso", B.Chest, Slot.Torso, H.Cloth, V(812, 232, 848, 220, 890, 226, 922, 246, 940, 276, 948, 316, 951, 355, 953, 425, 880, 428, 808, 425, 807, 360, 806, 322, 800, 286, 802, 256));
            // bandage wraps bound in crossing turns round the chest and belly, one end trailing
            var polys = new List<(Vector2[], Color)>();
            var wrap = V(806, 262, 850, 250, 896, 258, 930, 280, 946, 320, 950, 392, 806, 396, 802, 330);
            polys.Add(Poly(wrap, H.WrapDark));
            for (int i = 0; i < 6; i++)
            {
                float y = 268 + i * 24;
                polys.Add(Poly(Band(7f, 802, y + 14, 878, y - 4, 952, y + 16), i % 2 == 0 ? H.Wrap : Shade(H.Wrap, 0.7f)));
            }
            polys.Add(Poly(Band(3f, 818, 276, 934, 392), H.Violet));          // a violet crack showing through the binding
            polys.Add(Poly(Band(2f, 904, 300, 918, 336, 906, 360), H.Violet));
            AddMulti(d, "Chest_Wraps", B.Chest, Slot.TorsoTrim, polys, 1, wrap);
            AddSwinging(d, "Wrap_End", B.Chest, Slot.TorsoTrim, H.Wrap, V(920, 270, 960, 280, 1010, 340, 1040, 400, 1000, 380, 966, 330, 930, 296), 924, 276, 1.3f, 3, PartRole.Trim);
            Add(d, "Sash", B.Chest, Slot.Belt, H.ClothDeep, V(802, 384, 956, 388, 957, 408, 802, 404), 0, PartRole.Trim, false);
        }

        /// <summary>One long split tatter hanging behind the hips, and a short torn flap in front: a ragged streak, not a skirt.</summary>
        static void Tatter(RigDef d)
        {
            AddSwinging(d, "Tatter_Long", B.Hips, Slot.BackPlates, H.ClothDeep, V(900, 402, 972, 404, 1000, 520, 1022, 640, 1050, 740, 1000, 700, 986, 590, 962, 690, 934, 600, 920, 500), 940, 404, 1.4f, 1);
            AddSwinging(d, "Tatter_Split", B.Hips, Slot.BackPlates, H.ClothFar, V(920, 404, 960, 406, 962, 480, 942, 560, 920, 500), 938, 404, 1.6f, 0);
            AddSwinging(d, "Tatter_Glow", B.Hips, Slot.BackPlates, H.Violet, V(982, 560, 1000, 540, 1016, 640, 1004, 650), 940, 404, 1.4f, 2, PartRole.Trim, false);
            AddSwinging(d, "Flap_Front", B.Hips, Slot.FrontPlates, H.Cloth, HollowBody.Torn(806, 872, 402, 500, 4, 20, 5), 840, 404, 0.8f, 0);
        }

        static void Head(RigDef d)
        {
            // two long torn hood tails, streaming back with a violet edge
            var t1 = AddSwinging(d, "Tail_Far", B.Head, Slot.Neck, H.ClothFar, V(896, 124, 940, 118, 1020, 150, 1110, 196, 1070, 206, 1040, 186, 990, 176, 940, 168, 902, 166), 906, 140, 1.6f, 1);
            t1.rimColor = H.Violet;
            var t2 = AddSwinging(d, "Tail_Near", B.Head, Slot.Neck, H.ClothDeep, V(896, 152, 940, 152, 1004, 190, 1066, 250, 1040, 252, 1020, 236, 976, 220, 940, 206, 902, 192), 906, 164, 1.3f, 2);
            t2.rimColor = H.Violet;

            // the gaunt face: a long hooked nose, a small chin, the mouth a little open
            var face = V(822, 148, 850, 144, 856, 178, 852, 214, 838, 232, 812, 234, 802, 222, 792, 214, 762, 212, 786, 198, 796, 176, 806, 156);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, H.Face),
                Poly(V(800, 214, 814, 224, 838, 230, 852, 214, 826, 214), H.FaceDark),
                Poly(Band(2f, 800, 214, 818, 216), H.FaceDark),
            }, 0, face);

            // the beak: a hood pulled forward into a long point over the brow
            Add(d, "Hood", B.Head, Slot.Head, H.Cloth, V(808, 182, 806, 146, 780, 128, 742, 118, 786, 106, 830, 98, 868, 100, 896, 116, 912, 146, 912, 184, 900, 214, 876, 228, 856, 208, 852, 176, 826, 164), 3);
            Add(d, "Hood_Edge", B.Head, Slot.HeadTop, H.Violet, Band(2.2f, 744, 118, 786, 110, 830, 102, 868, 104, 896, 120), 0, PartRole.Trim, false, null, 1.4f);
            Add(d, "Beak_Shade", B.Head, Slot.HeadTop, H.ClothDeep, V(744, 118, 786, 108, 800, 130, 774, 130), 1, PartRole.Detail, false);

            // a thin cracked visor across the eyes
            var visor = V(798, 160, 842, 154, 856, 164, 852, 174, 806, 178, 792, 170);
            AddMulti(d, "Visor", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(visor, H.Mask),
                Poly(Band(2f, 820, 156, 824, 176), H.Violet),
                Poly(Band(1.8f, 836, 156, 830, 172), H.Violet),
            }, 1, visor);
            Add(d, "Eye", B.Head, Slot.Face, H.Eye, V(822, 164, 838, 161, 849, 165, 838, 170, 825, 170), 3, PartRole.Eye, false, null, 3.2f);
        }
    }
}
