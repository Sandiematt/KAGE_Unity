using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HollowPalette;

namespace Kage
{
    /// <summary>
    /// The Hollow Shade's look (SAN-76), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: a memory of a samurai
    /// coming apart. A ragged cowl with a crown of floating mask shards, a gaunt face under a cracked half-mask, a cold white eye,
    /// a torn haori with a rip across the chest that glows violet, frayed sleeves, wrapped forearms and shins, a torn skirt of
    /// swinging panels to the calf and a saya. Violet-black cloth and a violet light along every break, where the Order is slate
    /// and silver and Kage crimson. Based on the JS Hollow skin (<c>hollow_shade</c>: cowl, shards, half-mask, rip, saya).
    /// </summary>
    public sealed class HollowShadePartSet : IPartSet
    {
        public string Id => "hollow_shade";

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HOLLOW SHADE");
            d.rimColor = H.Rim;
            d.bodyColor = H.Cloth;
            d.scarf = false;
            d.softCoat = false;

            HollowBody.FarArm(d);
            HollowBody.FarLeg(d);
            HollowBody.Pelvis(d);
            Torso(d);
            HollowBody.NearLeg(d);
            Skirt(d);
            Head(d);
            HollowBody.NearArm(d);
            HollowBody.Sword(d);
            return d;
        }

        static void Torso(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, H.Cloth, V(834, 226, 870, 214, 908, 220, 932, 240, 946, 268, 950, 310, 951, 355, 953, 425, 880, 428, 808, 425, 807, 360, 812, 322, 818, 290, 824, 260));
            // the haori: a torn robe over it, lapels falling open, the hem ragged
            var haori = V(812, 240, 862, 226, 904, 232, 944, 262, 954, 330, 958, 410, 940, 424, 928, 404, 910, 428, 892, 404, 872, 424, 850, 402, 826, 424, 806, 404, 802, 340, 806, 284);
            var polys = new List<(Vector2[], Color)> { Poly(haori, H.ClothDeep) };
            // the rip across the chest: a dark gash lined with violet light
            polys.Add(Poly(V(812, 296, 846, 300, 880, 336, 924, 350, 948, 360, 934, 372, 878, 362, 838, 340, 806, 316), H.Violet));
            polys.Add(Poly(V(818, 302, 846, 306, 880, 343, 920, 357, 940, 364, 930, 366, 878, 354, 840, 334, 812, 314), H.FaceDark));
            polys.Add(Poly(Band(2.2f, 828, 308, 860, 322, 884, 346), H.Eye));
            // cracks fanning from the rip
            polys.Add(Poly(Band(1.8f, 892, 340, 900, 312, 914, 300), H.Violet));
            polys.Add(Poly(Band(1.8f, 906, 356, 922, 384, 918, 404), H.Violet));
            // the lapel edge, lit
            polys.Add(Poly(Band(2.6f, 818, 252, 848, 238, 874, 300, 862, 346), H.ClothLit));
            AddMulti(d, "Haori", B.Chest, Slot.TorsoTrim, polys, 1, haori);
            // wraps bound across the shoulder
            Add(d, "Shoulder_Wrap", B.Chest, Slot.TorsoTrim, H.Wrap, Band(7f, 822, 252, 868, 270, 910, 276, 946, 296), 2, PartRole.Trim, false);

            // ragged sash, its ends hanging in tatters
            Add(d, "Sash", B.Chest, Slot.Belt, H.ClothDeep, V(802, 384, 956, 388, 957, 410, 802, 406), 0, PartRole.Trim, false);
            AddSwinging(d, "Sash_Tail", B.Chest, Slot.BeltTrim, H.Cloth, V(810, 404, 840, 404, 852, 456, 842, 448, 838, 492, 824, 462, 814, 480, 818, 440), 826, 404, 0.9f, 1, PartRole.Trim);
            Add(d, "Neck", B.Neck, Slot.Neck, H.ClothDeep, V(838, 200, 868, 200, 872, 240, 842, 240), 0, PartRole.Body, false);
        }

        static void Skirt(RigDef d)
        {
            // front: three torn haori panels, each hanging from its own point
            void Front(string n, float sw, int sub, float px, float py, Vector2[] pts, Color col) => AddSwinging(d, n, B.Hips, Slot.FrontPlates, col, pts, px, py, sw, sub);
            Front("Skirt_F1", 0.5f, 0, 838, 404, HollowBody.Torn(796, 892, 402, 560, 6, 26, 1), H.Cloth);
            Front("Skirt_F2", 0.8f, 1, 830, 404, HollowBody.Torn(786, 862, 410, 640, 5, 30, 3, -4), H.ClothDeep);
            Front("Skirt_F2_Glow", 0.8f, 2, 830, 404, V(800, 470, 806, 474, 830, 566, 822, 570), H.Violet);
            // back: two long panels
            AddSwinging(d, "Skirt_B1", B.Hips, Slot.BackPlates, H.ClothDeep, HollowBody.Torn(922, 996, 402, 590, 4, 28, 2), 960, 402, 0.8f, 1);
            AddSwinging(d, "Skirt_B2", B.Hips, Slot.BackPlates, H.ClothFar, HollowBody.Torn(938, 1030, 410, 650, 5, 32, 4, 8), 980, 410, 1.1f, 0);
        }

        static void Head(RigDef d)
        {
            // the crown: four shards of its broken mask hang in the air above the head and drift
            void Shard(string n, float swing, int sub, Color c, float cx, float cy, float w, float h, float lean)
            {
                var s = AddSwinging(d, n, B.Head, Slot.HeadTop, c, V(cx - w, cy + h * 0.4f, cx + lean, cy - h, cx + w, cy + h * 0.5f, cx + w * 0.2f, cy + h * 0.2f), cx, cy + h, swing, sub, PartRole.Metal);
                s.rimColor = H.Violet;
            }
            Shard("Shard_1", 1.1f, 0, H.MaskDark, 822, 64, 11, 30, -6);
            Shard("Shard_2", 1.4f, 1, H.Mask, 858, 46, 9, 34, 4);
            Shard("Shard_3", 1.2f, 2, H.MaskDark, 892, 70, 10, 26, 6);
            Shard("Shard_4", 1.6f, 0, H.Mask, 928, 100, 8, 22, 8);

            // the ragged cowl, its tail torn into points
            AddSwinging(d, "Cowl_Tail", B.Head, Slot.Neck, H.ClothDeep, V(898, 140, 930, 156, 944, 206, 962, 244, 934, 232, 928, 262, 908, 226, 892, 232, 902, 190), 912, 150, 1.0f, 2);
            Add(d, "Cowl", B.Head, Slot.Head, H.Cloth, V(802, 168, 804, 136, 826, 112, 860, 100, 892, 108, 912, 138, 916, 176, 910, 214, 920, 238, 896, 226, 884, 244, 862, 224, 836, 226, 818, 212), 3);
            Add(d, "Cowl_Edge", B.Head, Slot.HeadTop, H.Violet, Band(2.2f, 808, 144, 828, 118, 862, 108, 890, 116, 908, 142), 0, PartRole.Trim, false, null, 1.4f);

            // the gaunt face: a pale ash-violet profile with a long nose, the mouth a little parted
            var face = V(820, 142, 850, 138, 856, 172, 852, 208, 834, 228, 806, 228, 800, 212, 790, 192, 802, 186, 799, 168, 806, 150);
            AddMulti(d, "Face", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(face, H.Face),
                Poly(V(800, 210, 812, 220, 834, 226, 852, 208, 830, 212), H.FaceDark),
                Poly(Band(2.4f, 799, 168, 802, 186, 790, 192, 800, 212), H.Violet),
                Poly(Band(2f, 802, 207, 818, 210), H.FaceDark),
            }, 0, face);

            // the cracked half-mask: over the brow and the eye, broken off before the mouth
            var mask = V(800, 148, 846, 140, 854, 172, 838, 186, 818, 182, 800, 196, 794, 176, 800, 162);
            AddMulti(d, "Mask", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(mask, H.Mask),
                Poly(Band(2.6f, 802, 152, 846, 144), H.MaskLit),
                Poly(Band(2f, 826, 146, 822, 164, 832, 178), H.Violet),
                Poly(Band(2f, 806, 170, 812, 182, 802, 192), H.Violet),
            }, 1, mask);
            Add(d, "Eye_Slit", B.Head, Slot.Face, H.FaceDark, V(806, 160, 836, 156, 850, 164, 834, 174, 810, 172), 2, PartRole.Detail, false);
            Add(d, "Eye", B.Head, Slot.Face, H.Eye, V(820, 163, 836, 159, 848, 164, 836, 170, 823, 170), 3, PartRole.Eye, false, null, 3.2f);
        }
    }
}
