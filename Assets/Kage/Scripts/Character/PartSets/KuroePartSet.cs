using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using K = Kage.KuroePalette;

namespace Kage
{
    /// <summary>
    /// Kuroe's look (SAN-103), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: a distinct palette and silhouette, no more
    /// (the spec: sub-bosses get a palette and a silhouette only). After the JS Ink Censor (<c>21-characters/sub-bosses/kuroe</c>): a tall black
    /// lacquered eboshi leaning back, slashed with a pale ink stroke and hung with a redaction slip; a monk's robe to the ankles with huge
    /// hanging sleeves; a gold-edged kesa across the chest. Her outline is tall and long, a column with a peaked top, where the Footman's is
    /// blocky and low and the Hollows' ragged. The temple staff is the spear family's (<see cref="SpearVisual"/>).
    /// </summary>
    public sealed class KuroePartSet : IPartSet
    {
        public string Id => Kuroe.Id;

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("KUROE");
            d.rimColor = K.Rim;
            d.bodyColor = K.Ink;
            d.scarf = false;
            d.softCoat = false;

            FarArm(d);
            FarLeg(d);
            Pelvis(d);
            Torso(d);
            NearLeg(d);
            RobeSkirt(d);
            Head(d);
            NearArm(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- far arm: a wide sleeve
        static void FarArm(RigDef d)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, K.InkFar, Tube(862, 272, 822, 385, 78, 74), swordLayer: true);
            var hang = AddSwinging(d, "ArmF_SleeveHang", B.ArmFL, Slot.FarArm, K.InkFar, V(800, 372, 848, 372, 872, 470, 858, 540, 812, 520, 792, 440), 826, 380, 0.5f, 1);
            hang.swordLayer = true;
            var hem = AddSwinging(d, "ArmF_SleeveHem", B.ArmFL, Slot.FarArm, K.GoldDark, Band(4f, 814, 518, 858, 538), 826, 380, 0.5f, 2, PartRole.Trim, false);
            hem.swordLayer = true;
            Add(d, "ArmF_Forearm", B.ArmFL, Slot.FarArm + 1, K.InkFar, Tube(822, 385, 797, 452, 50, 44), swordLayer: true);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, K.Face, V(780, 450, 810, 452, 814, 478, 802, 494, 780, 490, 772, 470), swordLayer: true);
        }

        // ---------------------------------------------------------------------------------------------- legs: the robe to the ankles
        static void FarLeg(RigDef d)
        {
            Add(d, "LegF_Robe", B.LegFU, Slot.FarLeg, K.InkFar, V(858, 530, 956, 530, 1004, 610, 1016, 700, 928, 704, 900, 640, 866, 590));
            Add(d, "LegF_RobeLow", B.LegFL, Slot.FarLeg + 1, K.InkFar, V(922, 668, 1004, 668, 1012, 744, 1004, 776, 928, 776, 916, 730));
            Add(d, "FootF_Tabi", B.FootF, Slot.FarFoot, K.PaperShade, V(934, 760, 978, 760, 988, 790, 994, 816, 904, 816, 900, 804, 916, 792, 934, 782));
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, K.InkDeep, V(900, 808, 996, 808, 996, 818, 904, 818), 1, PartRole.Detail, false);
        }

        static void NearLeg(RigDef d)
        {
            Add(d, "LegN_Robe", B.LegNU, Slot.NearLeg, K.Ink, V(796, 530, 896, 530, 900, 600, 892, 664, 884, 704, 790, 704, 774, 640, 782, 580));
            Add(d, "LegN_RobeLow", B.LegNL, Slot.NearLeg + 1, K.Ink, V(790, 668, 884, 668, 890, 744, 880, 776, 794, 776, 784, 730));
            Add(d, "LegN_RobeFold", B.LegNL, Slot.NearLeg + 1, K.InkLit, Band(3f, 838, 680, 832, 768), 1, PartRole.Trim, false);
            Add(d, "FootN_Tabi", B.FootN, Slot.NearFoot, K.Paper, V(814, 760, 858, 760, 862, 790, 865, 816, 774, 816, 770, 804, 788, 792, 812, 782));
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, K.InkDeep, V(768, 808, 866, 808, 866, 818, 772, 818), 1, PartRole.Detail, false);
            Add(d, "FootN_Thong", B.FootN, Slot.NearFoot, K.Gold, Band(3f, 800, 786, 820, 806), 2, PartRole.Trim, false);
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, K.Ink, V(806, 392, 958, 392, 960, 570, 804, 570), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- the robe's long skirt
        static void RobeSkirt(RigDef d)
        {
            // front and back panels of the robe from the obi to the ankles, swinging a little behind the walk: her long column
            AddSwinging(d, "Robe_Front", B.Hips, Slot.FrontPlates, K.Ink, V(796, 404, 884, 404, 892, 560, 884, 700, 864, 770, 784, 770, 774, 640, 782, 520), 840, 404, 0.3f, 0);
            AddSwinging(d, "Robe_FrontFold", B.Hips, Slot.FrontPlates, K.InkLit, Band(3.2f, 846, 420, 838, 600, 826, 760), 840, 404, 0.3f, 1, PartRole.Trim, false);
            AddSwinging(d, "Robe_FrontHem", B.Hips, Slot.FrontPlates, K.GoldDark, Band(4f, 786, 764, 864, 764), 840, 404, 0.3f, 2, PartRole.Trim, false);
            AddSwinging(d, "Robe_Back", B.Hips, Slot.BackPlates, K.InkFar, V(918, 404, 972, 404, 1000, 520, 1022, 660, 1030, 776, 934, 776, 922, 600), 944, 404, 0.4f, 0);
            AddSwinging(d, "Robe_BackHem", B.Hips, Slot.BackPlates, K.GoldDark, Band(4f, 936, 770, 1028, 770), 944, 404, 0.4f, 1, PartRole.Trim, false);
        }

        // ---------------------------------------------------------------------------------------------- torso: robe, kesa, obi
        static void Torso(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, K.Ink, V(830, 226, 870, 212, 912, 218, 938, 240, 952, 270, 956, 320, 958, 425, 880, 430, 804, 425, 804, 360, 810, 320, 816, 286, 822, 256));
            Add(d, "Robe_Collar", B.Chest, Slot.TorsoTrim, K.InkLit, V(826, 236, 846, 228, 878, 310, 862, 336, 844, 306), 0, PartRole.Trim, false);
            Add(d, "Robe_CollarEdge", B.Chest, Slot.TorsoTrim, K.Paper, Band(3f, 830, 236, 868, 322), 1, PartRole.Trim, false);

            // the kesa: a patchwork stole from the back shoulder across the chest to the front hip, gold-edged, a ring at the chest
            var kesa = V(916, 226, 950, 250, 846, 424, 806, 410);
            var polys = new List<(Vector2[], Color)> { Poly(kesa, K.Kesa) };
            foreach (float t in new[] { 0.2f, 0.38f, 0.56f, 0.74f, 0.9f })
            {
                Vector2 a = Vector2.Lerp(new Vector2(916, 226), new Vector2(806, 410), t), b = Vector2.Lerp(new Vector2(950, 250), new Vector2(846, 424), t);
                polys.Add(Poly(Band(2.4f, a.x, a.y, b.x, b.y), K.GoldDark));
            }
            polys.Add(Poly(Band(3.2f, 933, 238, 826, 417), K.KesaLit));
            polys.Add(Poly(Band(4f, 918, 228, 808, 412), K.Gold));
            polys.Add(Poly(Band(4f, 950, 252, 846, 424), K.Gold));
            polys.Add(Poly(Ellipse(880, 330, 13, 13), K.Gold));
            polys.Add(Poly(Ellipse(880, 330, 8.5f, 8.5f), K.Kesa));
            AddMulti(d, "Kesa", B.Chest, Slot.TorsoTrim, polys, 2, kesa);

            // the obi
            Add(d, "Obi", B.Chest, Slot.Belt, K.InkDeep, V(800, 384, 958, 388, 959, 414, 800, 410), 0, PartRole.Trim, false);
            Add(d, "Obi_Cord", B.Chest, Slot.BeltTrim, K.Gold, Band(3.4f, 802, 398, 958, 402), 0, PartRole.Trim, false);
            Add(d, "Neck", B.Neck, Slot.Neck, K.Face, V(838, 200, 868, 200, 872, 240, 842, 240), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- head: the eboshi
        static void Head(RigDef d)
        {
            // the redaction slip hanging from the cap's back edge: paper with black bars
            AddMulti(d, "Eboshi_Slip", B.Head, Slot.Neck, new List<(Vector2[], Color)>
            {
                Poly(V(902, 118, 936, 112, 958, 222, 922, 230), K.Paper),
                Poly(V(910, 140, 936, 136, 940, 154, 913, 158), K.InkDeep),
                Poly(V(914, 178, 945, 174, 948, 188, 917, 192), K.InkDeep),
                Poly(V(918, 206, 951, 202, 953, 212, 920, 216), K.InkDeep),
            }, 2, V(902, 118, 936, 112, 958, 222, 922, 230));
            d.parts[d.parts.Count - 1].jigglePivot = P(918, 116);
            d.parts[d.parts.Count - 1].jiggle = 0.7f;

            // the face: black, a long thin nose, a chin that slips back, the white eye
            var face = V(806, 150, 846, 146, 870, 168, 872, 210, 856, 232, 828, 236, 806, 222, 798, 206, 784, 196, 800, 186, 798, 166);
            Add(d, "Face", B.Head, Slot.Head, K.Face, face, 0);
            Add(d, "Eye", B.Head, Slot.Face, K.Eye, V(806, 176, 822, 170, 832, 174, 822, 180, 809, 181), 2, PartRole.Eye, false, null, 2.8f);
            Add(d, "Brow", B.Head, Slot.Face, K.InkDeep, Band(3.4f, 802, 168, 830, 164), 1, PartRole.Detail, false);
            Add(d, "Mouth", B.Head, Slot.Face, K.InkDeep, Band(2.2f, 800, 212, 816, 214), 1, PartRole.Detail, false);

            // the eboshi: a tall black lacquered court cap leaning back, its peak folded over, a pale dry-brush ink stroke across it
            var cap = V(800, 160, 808, 118, 822, 70, 846, 22, 878, -18, 912, -40, 938, -34, 934, 6, 922, 60, 916, 110, 912, 160, 880, 152, 840, 150);
            AddMulti(d, "Eboshi", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(cap, K.Lacquer),
                Poly(Band(2.6f, 864, 140, 870, 60, 900, -20), K.LacquerLit),
                Poly(Band(2.6f, 834, 146, 846, 70, 880, 0), K.InkDeep),
                Poly(Band(11f, 808, 108, 850, 88, 888, 78, 922, 82), K.Paper),
                Poly(V(830, 98, 840, 94, 842, 100, 832, 104), K.InkDeep),
                Poly(V(864, 84, 876, 82, 877, 88, 865, 90), K.InkDeep),
                Poly(V(910, -38, 950, -60, 930, -18), K.Lacquer),
            }, 0, cap);
            // gold chin cords
            Add(d, "Eboshi_Cord", B.Head, Slot.Face, K.Gold, Band(3f, 806, 158, 820, 210, 848, 236), 3, PartRole.Trim, false);
        }

        // ---------------------------------------------------------------------------------------------- near arm: the widest sleeve
        static void NearArm(RigDef d)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, K.Ink, Tube(905, 268, 926, 375, 88, 84));
            AddSwinging(d, "ArmN_SleeveHang", B.ArmNL, Slot.NearArm, K.Ink, V(898, 360, 966, 360, 996, 470, 984, 556, 926, 540, 900, 450), 930, 368, 0.55f, 1);
            AddSwinging(d, "ArmN_SleeveFold", B.ArmNL, Slot.NearArm, K.InkLit, Band(3f, 948, 380, 966, 530), 930, 368, 0.55f, 2, PartRole.Trim, false);
            AddSwinging(d, "ArmN_SleeveHem", B.ArmNL, Slot.NearArm, K.Gold, Band(4.4f, 928, 540, 986, 554), 930, 368, 0.55f, 3, PartRole.Trim, false);
            Add(d, "ArmN_Forearm", B.ArmNL, Slot.NearArm + 1, K.Ink, Tube(926, 375, 914, 444, 54, 46));
            Add(d, "HandN", B.HandN, Slot.NearHand, K.Face, V(888, 442, 926, 440, 932, 462, 924, 486, 898, 490, 880, 480, 877, 458));
        }
    }
}
