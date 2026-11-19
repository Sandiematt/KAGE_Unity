using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using G = Kage.GaranPalette;

namespace Kage
{
    /// <summary>
    /// Garan's look (SAN-104), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: a distinct palette and silhouette, no more (the
    /// spec: sub-bosses get a palette and a silhouette only). After the JS Stormbroken (<c>21-characters/sub-bosses/garan</c>): a wild black
    /// mane bound under a horned iron brow band, a snarling red oni menpō over the jaw, a broad gi under half a dō burnt ragged down the
    /// middle with a glowing storm scar, one huge laced sode on the near shoulder, a thick rope belt, iron suneate, and the iron tekko
    /// gauntlets (SAN-80: the fists family draws nothing, a fists foe's gauntlets are its part set's). He is also drawn wider than Kage's
    /// frame (JS build w 14), and the 1.18 build scale makes him the biggest of the sub-bosses: a broad block with horns and a mane, where
    /// Kuroe is a tall column and the Order blocky and low.
    /// </summary>
    public sealed class GaranPartSet : IPartSet
    {
        public string Id => Garan.Id;

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("GARAN");
            d.rimColor = G.Rim;
            d.bodyColor = G.Gi;
            d.scarf = false;
            d.softCoat = false;

            FarArm(d);
            FarLeg(d);
            Pelvis(d);
            Torso(d);
            NearLeg(d);
            Hakama(d);
            Head(d);
            NearArm(d);
            return d;
        }

        /// <summary>An iron tekko gauntlet on a hand bone: the flared cuff over the wrist, the fist, the knuckle plate with three studs, a storm crackle.</summary>
        static void Tekko(RigDef d, string name, B bone, int slot, float hx, float hy, bool far)
        {
            Color iron = far ? G.IronFar : G.Iron, lit = far ? G.Iron : G.IronLit;
            var fist = Ellipse(hx - 4, hy + 14, 27, 24, 10);
            var polys = new List<(Vector2[], Color)>
            {
                Poly(Tube(hx + 18, hy - 22, hx + 6, hy - 4, 40, 46), iron),                 // the cuff over the wrist
                Poly(Band(3f, hx - 6, hy - 16, hx + 26, hy - 10), lit),
                Poly(fist, iron),
                Poly(Band(5f, hx - 26, hy - 2, hx - 22, hy + 30), lit),                       // the knuckle plate across the front
                Poly(Ellipse(hx - 24, hy + 2, 3, 3), G.IronEdge),
                Poly(Ellipse(hx - 25, hy + 14, 3, 3), G.IronEdge),
                Poly(Ellipse(hx - 23, hy + 26, 3, 3), G.IronEdge),
            };
            if (!far) polys.Add(Poly(Band(2.2f, hx + 4, hy + 2, hx - 6, hy + 12, hx + 2, hy + 18, hx - 10, hy + 28), G.Storm));   // the storm still in it
            AddMulti(d, name, bone, slot, polys, 0, fist, PartRole.Metal, swordLayer: far);
        }

        // ---------------------------------------------------------------------------------------------- far arm
        static void FarArm(RigDef d)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, G.GiFar, Tube(862, 268, 822, 385, 82, 70), swordLayer: true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, G.IronFar, Tube(822, 385, 797, 452, 62, 54), 0, PartRole.Metal, swordLayer: true);
            Add(d, "ArmF_KoteBand", B.ArmFL, Slot.FarArm + 1, G.Iron, Band(4f, 784, 420, 828, 428), 1, PartRole.Metal, false, swordLayer: true);
            Tekko(d, "HandF_Tekko", B.HandF, Slot.FarArm + 2, 797, 455, true);
        }

        // ---------------------------------------------------------------------------------------------- legs: wide hakama, iron suneate, sandals
        static void FarLeg(RigDef d)
        {
            Add(d, "LegF_Hakama", B.LegFU, Slot.FarLeg, G.GiFar, V(856, 530, 962, 530, 1010, 612, 1026, 700, 928, 706, 898, 640, 864, 592));
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, G.GiFar, Tube(955, 672, 957, 764, 76, 60));
            Add(d, "LegF_Suneate", B.LegFL, Slot.FarLeg + 1, G.IronFar, V(930, 694, 984, 694, 982, 756, 932, 756), 1, PartRole.Metal);
            Add(d, "FootF_Foot", B.FootF, Slot.FarFoot, G.Skin, V(932, 752, 980, 752, 990, 792, 998, 814, 900, 814, 896, 802, 914, 790, 932, 780));
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, G.IronFar, V(896, 808, 1000, 808, 1000, 818, 900, 818), 1, PartRole.Detail, false);
        }

        static void NearLeg(RigDef d)
        {
            Add(d, "LegN_Hakama", B.LegNU, Slot.NearLeg, G.Gi, V(790, 530, 902, 530, 908, 600, 898, 664, 888, 706, 784, 706, 768, 640, 776, 580));
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, G.Gi, Tube(835, 672, 835, 764, 80, 62));
            AddMulti(d, "LegN_Suneate", B.LegNL, Slot.NearLeg + 1, new List<(Vector2[], Color)>
            {
                Poly(V(808, 690, 864, 690, 862, 758, 810, 758), G.Iron),
                Poly(Band(3f, 836, 694, 836, 754), G.IronLit),
                Poly(Band(3.4f, 806, 724, 866, 724), G.StormDark),
            }, 1, V(808, 690, 864, 690, 862, 758, 810, 758), PartRole.Metal);
            Add(d, "FootN_Foot", B.FootN, Slot.NearFoot, G.Skin, V(812, 752, 860, 752, 866, 792, 870, 814, 772, 814, 768, 802, 786, 790, 810, 780));
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, G.IronFar, V(766, 808, 872, 808, 872, 818, 770, 818), 1, PartRole.Detail, false);
            Add(d, "FootN_Thong", B.FootN, Slot.NearFoot, G.StormDark, Band(3f, 796, 784, 818, 806), 2, PartRole.Trim, false);
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, G.Gi, V(800, 392, 966, 392, 966, 572, 800, 572), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- the hakama's front and back panels
        static void Hakama(RigDef d)
        {
            AddSwinging(d, "Hakama_Front", B.Hips, Slot.FrontPlates, G.Gi, V(790, 410, 878, 410, 888, 520, 880, 610, 790, 612, 780, 520), 834, 410, 0.25f, 0);
            AddSwinging(d, "Hakama_Fold", B.Hips, Slot.FrontPlates, G.GiLit, Band(3f, 834, 424, 828, 600), 834, 410, 0.25f, 1, PartRole.Trim, false);
            AddSwinging(d, "Hakama_Back", B.Hips, Slot.BackPlates, G.GiFar, V(920, 410, 978, 410, 1004, 520, 1012, 616, 930, 616, 920, 520), 948, 410, 0.3f, 0);
        }

        // ---------------------------------------------------------------------------------------------- torso: gi, half dō with the scar, rope belt
        static void Torso(RigDef d)
        {
            // a broad chest and belly, wider than Kage's frame front and back (JS build w 14)
            Add(d, "Torso", B.Chest, Slot.Torso, G.Gi, V(822, 222, 864, 206, 916, 212, 948, 236, 966, 272, 972, 330, 972, 430, 880, 436, 796, 430, 792, 360, 800, 310, 808, 270, 814, 240));
            AddMulti(d, "Gi_Lapels", B.Chest, Slot.TorsoTrim, new List<(Vector2[], Color)>
            {
                Poly(V(830, 228, 852, 220, 900, 330, 884, 352), G.GiLit),
                Poly(V(878, 222, 900, 226, 862, 326, 846, 316), G.GiLit),
            }, 0, null, PartRole.Trim);

            // the half dō: iron over the front of the chest and belly, burnt ragged down the middle, the storm scar glowing along the tear
            var dou = V(796, 300, 806, 262, 830, 248, 870, 244, 884, 268, 870, 296, 892, 322, 874, 352, 896, 380, 878, 410, 892, 432, 796, 430, 792, 360);
            AddMulti(d, "HalfDo", B.Chest, Slot.TorsoTrim, new List<(Vector2[], Color)>
            {
                Poly(dou, G.Iron),
                Poly(Band(3f, 800, 300, 810, 268, 832, 254, 866, 250), G.IronLit),
                Poly(Band(3f, 798, 340, 870, 340), G.IronFar),
                Poly(Band(3f, 798, 380, 878, 380), G.IronFar),
                Poly(Band(2.6f, 798, 342, 868, 342), G.IronEdge),
                Poly(Band(2.6f, 798, 382, 876, 382), G.IronEdge),
                Poly(Band(3.2f, 876, 262, 862, 294, 884, 322, 866, 352, 888, 380, 872, 408, 884, 430), G.Storm),
            }, 1, dou, PartRole.Metal);
            d.parts[d.parts.Count - 1].glow = 1f;
            Add(d, "Do_Scar", B.Chest, Slot.TorsoTrim, G.Storm, Band(2.2f, 870, 270, 856, 296, 876, 322, 860, 350, 880, 378), 2, PartRole.Eye, false, null, 2.2f);

            // a thick twisted rope belt and its knot
            Add(d, "Belt", B.Chest, Slot.Belt, G.Rope, V(792, 400, 972, 404, 973, 440, 792, 436), 0, PartRole.Trim, false);
            AddMulti(d, "Belt_Twist", B.Chest, Slot.BeltTrim, new List<(Vector2[], Color)>
            {
                Poly(Band(3f, 804, 404, 816, 436), G.RopeLit), Poly(Band(3f, 830, 405, 842, 437), G.RopeLit),
                Poly(Band(3f, 856, 405, 868, 438), G.RopeLit), Poly(Band(3f, 882, 406, 894, 438), G.RopeLit),
                Poly(Band(3f, 908, 406, 920, 439), G.RopeLit), Poly(Band(3f, 934, 407, 946, 439), G.RopeLit),
                Poly(Ellipse(820, 446, 16, 12), G.Rope),
                Poly(V(812, 452, 828, 452, 834, 492, 806, 488), G.Rope),
            }, 0, null, PartRole.Trim);
            Add(d, "Neck", B.Neck, Slot.Neck, G.Skin, V(832, 196, 874, 196, 880, 242, 836, 242), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- head: mane, horned brow band, oni menpō
        static void Head(RigDef d)
        {
            // the wild mane behind the head, flaring back and up in ragged locks
            var mane = V(842, 118, 870, 92, 900, 70, 912, 96, 950, 70, 948, 112, 990, 104, 966, 146, 1004, 158, 962, 182, 986, 214, 942, 206, 946, 240, 908, 222, 880, 230, 860, 196);
            AddMulti(d, "Mane", B.Head, Slot.Neck, new List<(Vector2[], Color)>
            {
                Poly(mane, G.Mane),
                Poly(Band(3f, 900, 100, 940, 136, 976, 160), G.ManeLit),
                Poly(Band(3f, 910, 150, 952, 196), G.ManeLit),
            }, 1, mane, PartRole.Hair);

            // the face: black, a shelf of a brow, the broken nose, a block of a jaw (covered by the menpō)
            var face = V(806, 146, 846, 136, 878, 152, 886, 196, 870, 230, 834, 236, 806, 226, 796, 206, 780, 194, 794, 180, 796, 162);
            Add(d, "Face", B.Head, Slot.Head, G.Skin, face, 0);
            Add(d, "Eye", B.Head, Slot.Face, G.Eye, V(806, 168, 822, 162, 834, 166, 822, 173, 808, 174), 2, PartRole.Eye, false, null, 3.2f);
            Add(d, "Brow", B.Head, Slot.Face, G.Mane, Band(5f, 800, 160, 836, 154), 1, PartRole.Detail, false);

            // the red oni menpō over the jaw: from under the nose to the chin, the snarl cut at the mouth, teeth bared
            var menpo = V(784, 192, 806, 186, 846, 192, 878, 206, 880, 226, 862, 242, 828, 246, 800, 238, 788, 222);
            AddMulti(d, "Menpo", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(menpo, G.Lacquer),
                Poly(Band(3f, 790, 196, 846, 196, 876, 210), G.LacquerLit),
                Poly(V(792, 212, 846, 210, 846, 222, 794, 224), G.LacquerDeep),
                Poly(V(800, 212, 806, 220, 812, 212, 818, 220, 824, 212, 830, 220, 836, 212), G.Horn),
                Poly(Band(2.2f, 784, 202, 776, 196), G.LacquerLit),
                Poly(Band(2.4f, 870, 214, 900, 220), G.StormDark),
            }, 1, menpo);

            // the horned iron brow band: three studs, a crackle, two bone horns sweeping up and back
            var band = V(796, 160, 820, 138, 866, 130, 900, 140, 904, 158, 862, 150, 820, 156, 798, 168);
            AddMulti(d, "BrowBand", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(band, G.Iron),
                Poly(Band(2.4f, 802, 156, 822, 142, 866, 136, 898, 146), G.IronLit),
                Poly(Ellipse(820, 150, 3.4f, 3.4f), G.IronEdge),
                Poly(Ellipse(848, 144, 3.4f, 3.4f), G.IronEdge),
                Poly(Ellipse(876, 144, 3.4f, 3.4f), G.IronEdge),
                Poly(Band(2f, 830, 140, 838, 150, 834, 156), G.Storm),
            }, 0, band, PartRole.Metal);
            AddMulti(d, "Horns", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(V(818, 142, 834, 136, 830, 106, 842, 74, 852, 50, 832, 66, 816, 98), G.Horn),
                Poly(Band(2.6f, 826, 132, 828, 102, 846, 60), G.HornShade),
                Poly(V(870, 134, 888, 138, 900, 112, 924, 84, 936, 56, 912, 76, 884, 104), G.HornShade),
            }, 1, V(818, 142, 834, 136, 830, 106, 842, 74, 852, 50, 832, 66, 816, 98));
        }

        // ---------------------------------------------------------------------------------------------- near arm: the huge sode, kote, tekko
        static void NearArm(RigDef d)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, G.Gi, Tube(905, 264, 926, 375, 88, 76));
            // one huge iron sode, laced in storm red: the widest, squarest thing on him
            var sode = V(842, 236, 980, 226, 1012, 290, 1008, 392, 874, 404, 852, 320);
            AddMulti(d, "ArmN_Sode", B.ArmNU, Slot.NearArm, new List<(Vector2[], Color)>
            {
                Poly(sode, G.Iron),
                Poly(Band(3f, 850, 244, 980, 234, 1008, 288), G.IronLit),
                Poly(Band(3.4f, 852, 290, 1010, 288), G.IronFar),
                Poly(Band(3.4f, 858, 332, 1010, 330), G.IronFar),
                Poly(Band(3.4f, 866, 368, 1008, 364), G.IronFar),
                Poly(Band(2.6f, 852, 292, 1010, 290), G.IronEdge),
                Poly(Band(2.6f, 858, 334, 1010, 332), G.IronEdge),
                Poly(Band(2.6f, 866, 370, 1008, 366), G.IronEdge),
                Poly(Band(3.8f, 870, 250, 866, 286), G.StormDark),
                Poly(Band(3.8f, 994, 244, 1000, 284), G.StormDark),
                Poly(Band(3.8f, 870, 296, 868, 326), G.StormDark),
                Poly(Band(3.8f, 998, 296, 1002, 326), G.StormDark),
                Poly(Band(3.8f, 876, 338, 878, 362), G.StormDark),
                Poly(Band(3.8f, 998, 338, 1002, 360), G.StormDark),
                Poly(Band(4.6f, 878, 398, 1006, 386), G.IronEdge),
                Poly(Band(2.4f, 960, 252, 944, 274, 958, 284, 940, 306), G.Storm),
            }, 2, sode, PartRole.Metal);
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, G.Iron, Tube(926, 375, 914, 444, 64, 56), 0, PartRole.Metal);
            Add(d, "ArmN_KoteBand", B.ArmNL, Slot.NearArm + 1, G.IronLit, Band(4f, 884, 410, 950, 410), 1, PartRole.Metal, false);
            Tekko(d, "HandN_Tekko", B.HandN, Slot.NearHand, 914, 442, false);
        }
    }
}
