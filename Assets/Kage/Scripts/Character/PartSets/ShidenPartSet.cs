using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using S = Kage.ShidenPalette;

namespace Kage
{
    /// <summary>
    /// Shiden's look (SAN-106), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: a distinct palette and silhouette, no more (the
    /// spec: sub-bosses get a palette and a silhouette only). After the JS Last Lieutenant (<c>21-characters/sub-bosses/shiden</c>): a
    /// star-riveted hoshi kabuto with a flared shikoro laced violet and turned-back wings, a forked silver lightning maedate rising from its
    /// brow, an iron hōate on the chin and cheeks; a full black-lacquered dō laced violet with a silver lightning mark, stacked sode on both
    /// shoulders, kusazuri front and back; and a sashimono on his back, a lacquered pole leaning back with a violet-black banner and a
    /// silver lightning mark. A general's outline: a crest and a banner standing above a square armoured body, where Kuroe (the other spear
    /// user) is a long robed column under a leaning cap, Garan a broad horned block and Hisame a low hooded shape. The yari is the spear
    /// family's (<see cref="SpearVisual"/>, the Vanguard Yari look).
    /// </summary>
    public sealed class ShidenPartSet : IPartSet
    {
        public string Id => Shiden.Id;

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("SHIDEN");
            d.rimColor = S.Rim;
            d.bodyColor = S.Cloth;
            d.scarf = false;
            d.softCoat = false;

            Banner(d);
            FarArm(d);
            FarLeg(d);
            Pelvis(d);
            Torso(d);
            NearLeg(d);
            Kusazuri(d);
            Head(d);
            NearArm(d);
            return d;
        }

        /// <summary>A stacked sode: lacquer plates in four rows, each row laced violet, a lit top edge.</summary>
        static List<(Vector2[], Color)> Sode(float x0, float y0, float w, float h, bool far)
        {
            Color plate = far ? S.LacquerFar : S.Lacquer, lace = far ? S.AccentDeep : S.AccentMid, edge = far ? S.Lacquer : S.LacquerLit;
            var outline = V(x0, y0, x0 + w, y0 - 6, x0 + w + 10, y0 + h, x0 + 12, y0 + h + 6);
            var polys = new List<(Vector2[], Color)> { Poly(outline, plate) };
            for (int i = 1; i <= 3; i++)
            {
                float y = y0 + h * i / 4f;
                polys.Add(Poly(Band(3.2f, x0 + 3 * i, y, x0 + w + 2.5f * i, y - 6 + 1.5f * i), S.LacquerDeep));
                polys.Add(Poly(Band(2.2f, x0 + 3 * i, y + 3, x0 + w + 2.5f * i, y - 3 + 1.5f * i), edge));
            }
            polys.Add(Poly(Band(3f, x0, y0, x0 + w, y0 - 6), edge));
            // the violet lacing running down each plate (two cords)
            polys.Add(Poly(Band(3.2f, x0 + w * 0.3f, y0 + 2, x0 + w * 0.3f + 8, y0 + h + 2), lace));
            polys.Add(Poly(Band(3.2f, x0 + w * 0.7f, y0 - 2, x0 + w * 0.7f + 8, y0 + h), lace));
            return polys;
        }

        // ---------------------------------------------------------------------------------------------- the sashimono
        static void Banner(RigDef d)
        {
            // a lacquered pole from the back of the waist up past his head, leaning back (kept short, as the JS)
            Add(d, "BannerPole", B.Chest, 0, S.Lacquer, Band(9f, 950, 430, 972, 220, 1004, 6), 0, PartRole.Body);
            Add(d, "BannerPoleCap", B.Chest, 0, S.Silver, Ellipse(1006, 0, 7, 7), 2, PartRole.Metal, false);
            // the banner: violet-black cloth hung along the pole, stirring, its edges in his violet and a silver lightning mark
            var banner = V(1002, 12, 1078, 22, 1070, 196, 984, 182);
            var b = AddMulti(d, "Banner", B.Chest, 0, new List<(Vector2[], Color)>
            {
                Poly(banner, S.Cloth),
                Poly(Band(4f, 1002, 12, 1078, 22), S.Accent),
                Poly(Band(3f, 1070, 196, 984, 182), S.AccentMid),
                Poly(Band(2.4f, 1076, 26, 1068, 192), S.ClothLit),
                Poly(V(1048, 52, 1022, 102, 1040, 100, 1016, 156, 1056, 92, 1036, 94, 1060, 52), S.Silver),
            }, 1, banner);
            b.jigglePivot = P(992, 100);
            b.jiggle = 0.5f;
        }

        // ---------------------------------------------------------------------------------------------- far arm: a sode, the kote
        static void FarArm(RigDef d)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, S.ClothFar, Tube(862, 270, 822, 385, 80, 66), swordLayer: true);
            AddMulti(d, "SodeF", B.ArmFU, Slot.FarArm, Sode(820, 250, 84, 92, true), 1, V(820, 250, 904, 244, 914, 342, 832, 348), swordLayer: true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, S.IronFar, Tube(822, 385, 797, 452, 58, 50), 0, PartRole.Metal, swordLayer: true);
            Add(d, "ArmF_KoteBand", B.ArmFL, Slot.FarArm + 1, S.AccentDeep, Band(3.4f, 786, 422, 828, 428), 1, PartRole.Trim, false, swordLayer: true);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, S.Face, V(780, 450, 810, 452, 814, 478, 802, 494, 780, 490, 772, 470), swordLayer: true);
        }

        // ---------------------------------------------------------------------------------------------- legs: gi, suneate laced violet, sandals
        static void FarLeg(RigDef d)
        {
            Add(d, "LegF_Gi", B.LegFU, Slot.FarLeg, S.ClothFar, V(858, 530, 958, 530, 1002, 612, 1016, 700, 930, 704, 900, 640, 866, 592));
            Add(d, "LegF_Shin", B.LegFL, Slot.FarLeg + 1, S.ClothFar, Tube(955, 672, 957, 764, 70, 58));
            Add(d, "LegF_Suneate", B.LegFL, Slot.FarLeg + 1, S.IronFar, V(930, 690, 982, 690, 980, 756, 932, 756), 1, PartRole.Metal);
            Add(d, "FootF_Foot", B.FootF, Slot.FarFoot, S.Face, V(932, 752, 980, 752, 990, 792, 998, 814, 900, 814, 896, 802, 914, 790, 932, 780));
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, S.LacquerDeep, V(896, 808, 1000, 808, 1000, 818, 900, 818), 1, PartRole.Detail, false);
        }

        static void NearLeg(RigDef d)
        {
            Add(d, "LegN_Gi", B.LegNU, Slot.NearLeg, S.Cloth, V(792, 530, 900, 530, 906, 600, 896, 664, 886, 704, 786, 704, 770, 640, 778, 580));
            Add(d, "LegN_Shin", B.LegNL, Slot.NearLeg + 1, S.Cloth, Tube(835, 672, 835, 764, 74, 60));
            var sune = V(808, 688, 864, 688, 862, 758, 810, 758);
            AddMulti(d, "LegN_Suneate", B.LegNL, Slot.NearLeg + 1, new List<(Vector2[], Color)>
            {
                Poly(sune, S.Iron),
                Poly(Band(3f, 822, 692, 822, 754), S.IronLit),
                Poly(Band(3f, 848, 692, 848, 754), S.IronLit),
                Poly(Band(3.4f, 806, 704, 866, 704), S.AccentMid),
                Poly(Band(3.4f, 806, 742, 866, 742), S.AccentMid),
            }, 1, sune, PartRole.Metal);
            Add(d, "FootN_Foot", B.FootN, Slot.NearFoot, S.Face, V(812, 752, 860, 752, 866, 792, 870, 814, 772, 814, 768, 802, 786, 790, 810, 780));
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, S.LacquerDeep, V(766, 808, 872, 808, 872, 818, 770, 818), 1, PartRole.Detail, false);
            Add(d, "FootN_Thong", B.FootN, Slot.NearFoot, S.AccentMid, Band(3f, 796, 784, 818, 806), 2, PartRole.Trim, false);
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, S.Cloth, V(800, 392, 962, 392, 962, 572, 800, 572), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- kusazuri: four laced rows, front and back
        static List<(Vector2[], Color)> Tassets(float x0, float x1, float y0, float y1, bool far)
        {
            Color plate = far ? S.LacquerFar : S.Lacquer, lace = far ? S.AccentDeep : S.AccentMid;
            var shape = V(x0, y0, x1, y0, x1 + 8, y1, x0 - 8, y1);
            var polys = new List<(Vector2[], Color)> { Poly(shape, plate) };
            for (int i = 1; i <= 3; i++)
            {
                float y = Mathf.Lerp(y0, y1, i / 4f), dx = 8f * i / 4f;
                polys.Add(Poly(Band(3f, x0 - dx, y, x1 + dx, y), S.LacquerDeep));
                polys.Add(Poly(Band(2f, x0 - dx, y + 3, x1 + dx, y + 3), far ? S.Lacquer : S.LacquerLit));
            }
            float mid = (x0 + x1) / 2f;
            polys.Add(Poly(Band(3f, x0 + (mid - x0) * 0.5f, y0, x0 + (mid - x0) * 0.5f - 4, y1), lace));
            polys.Add(Poly(Band(3f, mid + (x1 - mid) * 0.5f, y0, mid + (x1 - mid) * 0.5f + 4, y1), lace));
            polys.Add(Poly(Band(3.6f, x0 - 8, y1, x1 + 8, y1), lace));
            return polys;
        }

        static void Kusazuri(RigDef d)
        {
            var front = AddMulti(d, "Kusazuri_Front", B.Hips, Slot.FrontPlates, Tassets(792, 878, 418, 560, false), 0, V(792, 418, 878, 418, 886, 560, 784, 560));
            front.jigglePivot = P(836, 418);
            front.jiggle = 0.25f;
            var back = AddMulti(d, "Kusazuri_Back", B.Hips, Slot.BackPlates, Tassets(912, 978, 418, 556, true), 0, V(912, 418, 978, 418, 986, 556, 904, 556));
            back.jigglePivot = P(944, 418);
            back.jiggle = 0.3f;
        }

        // ---------------------------------------------------------------------------------------------- torso: the full dō
        static void Torso(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, S.Cloth, V(826, 222, 866, 208, 914, 214, 944, 236, 960, 270, 964, 330, 964, 428, 880, 432, 800, 428, 798, 360, 806, 312, 812, 272, 818, 244));
            // the dō: black lacquer from the gorget to the waist, laced violet in rows, a silver lightning mark on the chest plate
            var dou = V(798, 300, 806, 258, 828, 240, 880, 234, 930, 240, 958, 266, 964, 330, 962, 426, 880, 432, 800, 426, 796, 360);
            var polys = new List<(Vector2[], Color)> { Poly(dou, S.Lacquer) };
            foreach (float y in new[] { 330f, 362f, 394f })
            {
                polys.Add(Poly(Band(3f, 798, y, 962, y + 2), S.LacquerDeep));
                polys.Add(Poly(Band(2f, 798, y + 3, 962, y + 5), S.LacquerLit));
                for (float x = 812; x < 960; x += 22) polys.Add(Poly(Band(2.6f, x, y - 12, x + 6, y + 14), S.AccentMid));
            }
            polys.Add(Poly(Band(3.4f, 806, 262, 830, 244, 880, 238, 930, 244, 954, 268), S.LacquerLit));
            polys.Add(Poly(Band(2.6f, 810, 296, 940, 292), S.Accent));
            polys.Add(Poly(V(866, 252, 846, 282, 860, 282, 842, 316, 874, 276, 860, 276, 878, 252), S.Silver));
            AddMulti(d, "Do", B.Chest, Slot.TorsoTrim, polys, 0, dou, PartRole.Metal);
            // the gorget at the throat
            Add(d, "Gorget", B.Chest, Slot.TorsoTrim, S.Iron, V(828, 220, 880, 214, 900, 236, 870, 246, 834, 244), 1, PartRole.Metal);
            Add(d, "GorgetLace", B.Chest, Slot.TorsoTrim, S.AccentMid, Band(2.6f, 832, 238, 896, 232), 2, PartRole.Trim, false);
            // the obi under the dō's skirt line
            Add(d, "Obi", B.Chest, Slot.Belt, S.LacquerDeep, V(798, 410, 964, 412, 964, 434, 798, 432), 0, PartRole.Trim, false);
            Add(d, "Obi_Cord", B.Chest, Slot.BeltTrim, S.Accent, Band(3f, 800, 422, 962, 424), 0, PartRole.Trim, false);
            Add(d, "Neck", B.Neck, Slot.Neck, S.Face, V(836, 198, 870, 198, 874, 240, 840, 240), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- head: the hoshi kabuto
        static void Head(RigDef d)
        {
            // the shikoro: the flared neck guard, laced violet in three rows, sweeping back and down
            var shikoro = V(886, 150, 920, 140, 960, 176, 986, 226, 948, 238, 902, 226, 878, 206);
            var sp = new List<(Vector2[], Color)> { Poly(shikoro, S.Lacquer) };
            for (int i = 1; i <= 3; i++)
            {
                float k = i / 4f;
                sp.Add(Poly(Band(3f, 884 + k * 6, 160 + k * 60, 922 + k * 30, 150 + k * 66, 960 + k * 26, 180 + k * 52), S.LacquerDeep));
                sp.Add(Poly(Band(2.4f, 890 + k * 6, 156 + k * 60, 926 + k * 30, 146 + k * 66, 962 + k * 26, 176 + k * 52), S.AccentMid));
            }
            AddMulti(d, "Shikoro", B.Head, Slot.Neck, sp, 1, shikoro);

            // the face: black, a hawk nose, a hard brow, the pale violet eye
            var face = V(806, 150, 846, 144, 872, 166, 874, 208, 858, 232, 828, 236, 806, 224, 798, 206, 782, 196, 796, 184, 798, 166);
            Add(d, "Face", B.Head, Slot.Head, S.Face, face, 0);
            Add(d, "Eye", B.Head, Slot.Face, S.Eye, V(806, 176, 822, 170, 832, 174, 822, 180, 809, 181), 2, PartRole.Eye, false, null, 3.0f);
            Add(d, "Brow", B.Head, Slot.Face, S.LacquerDeep, Band(4.4f, 800, 168, 834, 162), 1, PartRole.Detail, false);

            // the hōate: an iron guard on the chin and cheeks (the nose and lips bare), laced throat lames under it
            var hoate = V(792, 210, 806, 206, 828, 216, 852, 210, 876, 214, 878, 236, 862, 250, 830, 252, 806, 244, 794, 228);
            AddMulti(d, "Hoate", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(hoate, S.Iron),
                Poly(Band(2.6f, 798, 214, 826, 222, 852, 216, 874, 220), S.IronLit),
                Poly(Band(3f, 806, 252, 864, 250), S.AccentMid),
                Poly(Band(3f, 810, 262, 862, 260), S.AccentMid),
            }, 1, hoate, PartRole.Metal);

            // the bowl: black lacquer, rows of silver star rivets, a violet band at the brim, the turned-back wings (fukigaeshi)
            var bowl = V(794, 166, 796, 136, 812, 112, 846, 96, 884, 100, 912, 120, 922, 156, 884, 152, 840, 152);
            var bp = new List<(Vector2[], Color)> { Poly(bowl, S.Lacquer) };
            foreach (var (x, y0) in new[] { (812f, 134f), (834f, 116f), (860f, 108f), (886f, 114f), (906f, 132f) })
                for (int j = 0; j < 3; j++) bp.Add(Poly(Ellipse(x + j * 1.5f, y0 + j * 12f, 2.6f, 2.6f), S.Silver));
            bp.Add(Poly(Band(5f, 794, 154, 840, 150, 884, 150, 922, 154), S.Accent));
            bp.Add(Poly(Band(2.4f, 812, 118, 846, 100, 884, 104), S.LacquerLit));
            AddMulti(d, "Kabuto", B.Head, Slot.HeadTop, bp, 0, bowl, PartRole.Metal);
            Add(d, "Fukigaeshi", B.Head, Slot.HeadTop, S.Lacquer, V(806, 150, 780, 130, 772, 148, 790, 176), 1, PartRole.Metal);
            Add(d, "FukigaeshiLace", B.Head, Slot.HeadTop, S.AccentMid, Band(2.6f, 782, 140, 798, 164), 2, PartRole.Trim, false);

            // the maedate: a forked lightning crest in silver rising from the brow, two prongs
            var crest = V(822, 110, 806, 66, 818, 66, 796, 14, 828, 60, 816, 62, 838, 106,
                          852, 104, 852, 58, 840, 58, 862, -4, 866, 52, 878, 52, 860, 106);
            AddMulti(d, "Maedate", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(V(822, 110, 806, 66, 818, 66, 796, 14, 828, 60, 816, 62, 838, 106), S.Silver),
                Poly(V(852, 104, 852, 58, 840, 58, 862, -4, 866, 52, 878, 52, 860, 106), S.Silver),
                Poly(Band(1.6f, 818, 100, 812, 70, 802, 30), S.SilverHi),
                Poly(Band(1.6f, 856, 98, 856, 60, 860, 12), S.SilverHi),
                Poly(Ellipse(840, 112, 8, 6), S.SilverDark),
            }, 3, crest, PartRole.Metal);
        }

        // ---------------------------------------------------------------------------------------------- near arm: the stacked sode, kote
        static void NearArm(RigDef d)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, S.Cloth, Tube(905, 266, 926, 375, 86, 72));
            AddMulti(d, "SodeN", B.ArmNU, Slot.NearArm, Sode(862, 244, 104, 104, false), 1, V(862, 244, 966, 238, 976, 348, 874, 354));
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, S.Iron, Tube(926, 375, 914, 444, 60, 50), 0, PartRole.Metal);
            Add(d, "ArmN_KoteBand", B.ArmNL, Slot.NearArm + 1, S.AccentMid, Band(3.4f, 896, 414, 942, 418), 1, PartRole.Trim, false);
            Add(d, "HandN", B.HandN, Slot.NearHand, S.Face, V(888, 442, 926, 440, 932, 462, 924, 486, 898, 490, 880, 480, 877, 458));
        }
    }
}
