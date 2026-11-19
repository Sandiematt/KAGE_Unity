using System.Collections.Generic;
using UnityEngine;
using static Kage.OpponentKit;
using H = Kage.HisamePalette;

namespace Kage
{
    /// <summary>
    /// Hisame's look (SAN-105), drawn in code on Kage's skeleton with <see cref="OpponentKit"/>: a palette and a silhouette, as the spec asks of
    /// a sub-boss. After the JS Rain Courier (<c>21-characters/sub-bosses/hisame</c>, <c>_shared/tones.js</c>): a close rain hood with a stiff
    /// black visor jutting over the eyes, its face cloth pulled down round the throat; a fringed rain capelet over the shoulders and a long
    /// fringed mino cape hanging from them behind her; a lacquered message tube slung across the back, poking up past the shoulder; a slim gi
    /// with kote on the forearms, slim legs bound in wraps, sandals. Her outline is slight and low-headed, a jutting visor at the front and a
    /// shaggy cape trailing at the back, where Kuroe is a tall peaked column and Garan a broad horned block. The Mist Fans are the twin family's
    /// (<see cref="TwinVisual"/>).
    /// </summary>
    public sealed class HisamePartSet : IPartSet
    {
        public string Id => Hisame.Id;

        public RigDef Build(CharacterDef c)
        {
            var d = Skeleton("HISAME");
            d.rimColor = H.Rim;
            d.bodyColor = H.Rain;
            d.scarf = false;
            d.softCoat = false;

            FarArm(d);
            FarLeg(d);
            Pelvis(d);
            Cape(d);
            Torso(d);
            NearLeg(d);
            Head(d);
            NearArm(d);
            return d;
        }

        // ---------------------------------------------------------------------------------------------- arms: a gi sleeve, a kote
        static void FarArm(RigDef d)
        {
            Add(d, "ArmF_Sleeve", B.ArmFU, Slot.FarArm, H.RainFar, Tube(862, 272, 826, 372, 60, 52), swordLayer: true);
            Add(d, "ArmF_Kote", B.ArmFL, Slot.FarArm + 1, Shade(H.Wrap, 0.75f), Tube(822, 385, 799, 450, 42, 36), swordLayer: true);
            Add(d, "ArmF_KoteBand", B.ArmFL, Slot.FarArm + 1, H.AccentDeep, Band(3f, 814, 400, 832, 408), 1, PartRole.Trim, false, swordLayer: true);
            Add(d, "HandF", B.HandF, Slot.FarArm + 2, H.Face, V(782, 450, 810, 452, 813, 476, 802, 490, 782, 486, 775, 468), swordLayer: true);
        }

        static void NearArm(RigDef d)
        {
            Add(d, "ArmN_Sleeve", B.ArmNU, Slot.NearArm, H.Rain, Tube(905, 268, 922, 366, 64, 56));
            Add(d, "ArmN_SleeveFold", B.ArmNU, Slot.NearArm, H.RainLit, Band(3f, 920, 290, 928, 352), 1, PartRole.Trim, false);
            Add(d, "ArmN_Kote", B.ArmNL, Slot.NearArm + 1, H.Wrap, Tube(926, 375, 915, 442, 46, 40));
            Add(d, "ArmN_KoteLit", B.ArmNL, Slot.NearArm + 1, H.WrapLit, Band(3f, 934, 388, 926, 432), 1, PartRole.Trim, false);
            Add(d, "ArmN_KoteBand", B.ArmNL, Slot.NearArm + 1, H.Accent, Band(3.2f, 908, 392, 940, 398), 2, PartRole.Trim, false);
            Add(d, "HandN", B.HandN, Slot.NearHand, H.Face, V(892, 442, 924, 440, 930, 460, 922, 484, 898, 488, 882, 478, 880, 458));
        }

        // ---------------------------------------------------------------------------------------------- legs: slim, bound shins, sandals
        static void FarLeg(RigDef d)
        {
            Add(d, "LegF_Thigh", B.LegFU, Slot.FarLeg, H.RainFar, Tube(905, 545, 952, 676, 62, 46));
            Add(d, "LegF_Wrap", B.LegFL, Slot.FarLeg + 1, Shade(H.Wrap, 0.75f), Tube(955, 680, 955, 760, 42, 34));
            for (int i = 0; i < 4; i++)
                Add(d, "LegF_WrapBand" + i, B.LegFL, Slot.FarLeg + 1, H.RainDeep, Band(2.4f, 936, 694 + i * 17, 974, 688 + i * 17), 1, PartRole.Detail, false);
            Add(d, "FootF_Sandal", B.FootF, Slot.FarFoot, H.RainDeep, V(940, 756, 976, 756, 986, 790, 992, 812, 906, 812, 904, 800, 920, 790, 938, 780));
            Add(d, "FootF_Sole", B.FootF, Slot.FarFoot, Shade(H.Wrap, 0.6f), V(902, 806, 994, 806, 994, 816, 906, 816), 1, PartRole.Detail, false);
        }

        static void NearLeg(RigDef d)
        {
            Add(d, "LegN_Thigh", B.LegNU, Slot.NearLeg, H.Rain, Tube(848, 545, 836, 676, 66, 50));
            Add(d, "LegN_ThighFold", B.LegNU, Slot.NearLeg, H.RainLit, Band(3f, 856, 566, 846, 656), 1, PartRole.Trim, false);
            Add(d, "LegN_Wrap", B.LegNL, Slot.NearLeg + 1, H.Wrap, Tube(835, 680, 835, 760, 46, 36));
            for (int i = 0; i < 4; i++)
                Add(d, "LegN_WrapBand" + i, B.LegNL, Slot.NearLeg + 1, H.RainDeep, Band(2.4f, 814, 694 + i * 17, 856, 688 + i * 17), 1, PartRole.Detail, false);
            Add(d, "LegN_WrapTie", B.LegNL, Slot.NearLeg + 1, H.Accent, Band(2.6f, 816, 752, 856, 748), 2, PartRole.Trim, false);
            Add(d, "FootN_Sandal", B.FootN, Slot.NearFoot, H.Rain, V(818, 756, 856, 756, 862, 790, 866, 812, 776, 812, 772, 800, 790, 790, 814, 780));
            Add(d, "FootN_Sole", B.FootN, Slot.NearFoot, Shade(H.Wrap, 0.6f), V(770, 806, 868, 806, 868, 816, 774, 816), 1, PartRole.Detail, false);
            Add(d, "FootN_Thong", B.FootN, Slot.NearFoot, H.Accent, Band(2.6f, 800, 786, 818, 804), 2, PartRole.Trim, false);
        }

        static void Pelvis(RigDef d)
        {
            Add(d, "Pelvis", B.Hips, Slot.Pelvis, H.Rain, V(826, 400, 936, 400, 944, 566, 818, 566), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- the mino and the message tube
        /// <summary>A zig-zag fringe along a hem from (ax, ay) to (bx, by), <paramref name="n"/> strands <paramref name="len"/> px long, hanging down.</summary>
        static float[] Fringe(float ax, float ay, float bx, float by, int n, float len)
        {
            var xy = new List<float>();
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                float x = Mathf.Lerp(ax, bx, t), y = Mathf.Lerp(ay, by, t);
                xy.Add(x); xy.Add(y);
                if (i < n)
                {
                    float tm = (i + 0.5f) / n;
                    xy.Add(Mathf.Lerp(ax, bx, tm) + 3f); xy.Add(Mathf.Lerp(ay, by, tm) + len * (i % 2 == 0 ? 1f : 0.7f));
                }
            }
            return xy.ToArray();
        }

        static float[] Concat(params float[][] parts)
        {
            var all = new List<float>();
            foreach (var p in parts) all.AddRange(p);
            return all.ToArray();
        }

        static void Cape(RigDef d)
        {
            // the long mino from the shoulders, flaring behind her to the shins, its hem a long fringe of oiled strands
            var upper = V(884, 236, 934, 226, 976, 246, 1004, 330, 1012, 430, 950, 430, 932, 330);
            AddMulti(d, "Mino_Upper", B.Chest, Slot.BackPlates, new List<(Vector2[], Color)>
            {
                Poly(upper, H.CapeDeep),
                Poly(Band(2.4f, 944, 262, 990, 300), H.Cape),
                Poly(Band(2.4f, 950, 300, 1000, 350), H.Cape),
                Poly(Band(2.4f, 952, 350, 1006, 400), H.Cape),
            }, 0, upper);
            var lowerPts = V(Concat(new float[] { 940, 404, 1012, 404, 1036, 520, 1062, 650 }, Fringe(1062, 650, 940, 700, 9, 52), new float[] { 930, 560 }));
            var lower = AddSwinging(d, "Mino_Lower", B.Hips, Slot.BackPlates, H.CapeDeep, lowerPts, 970, 404, 0.45f, 0);
            AddSwinging(d, "Mino_LowerStrands", B.Hips, Slot.BackPlates, H.Cape, Band(2.4f, 958, 470, 1040, 520), 970, 404, 0.45f, 1, PartRole.Trim, false);
            AddSwinging(d, "Mino_LowerStrands2", B.Hips, Slot.BackPlates, H.Cape, Band(2.4f, 952, 560, 1054, 600), 970, 404, 0.45f, 1, PartRole.Trim, false);
            AddSwinging(d, "Mino_Hem", B.Hips, Slot.BackPlates, H.AccentDeep, Band(3f, 1060, 646, 942, 694), 970, 404, 0.45f, 2, PartRole.Trim, false);

            // the message tube slung across the back, from the low back up past the far shoulder: black lacquer, rain-blue bands
            AddMulti(d, "Tube", B.Chest, Slot.BackPlates, new List<(Vector2[], Color)>
            {
                Poly(Band(22f, 992, 372, 900, 168), H.Lacquer),
                Poly(Band(5f, 988, 352, 904, 170), H.LacquerLit),
                Poly(Band(8f, 986, 360, 976, 338), H.Accent),
                Poly(Band(8f, 910, 192, 902, 172), H.Accent),
                Poly(Ellipse(898, 164, 12, 9, -64f), H.LacquerLit),
            }, 3, Band(24f, 992, 372, 900, 168));
        }

        // ---------------------------------------------------------------------------------------------- torso: a slim gi, the capelet, the obi
        static void Torso(RigDef d)
        {
            Add(d, "Torso", B.Chest, Slot.Torso, H.Rain, V(842, 228, 876, 216, 912, 222, 934, 244, 944, 280, 944, 330, 940, 420, 880, 426, 826, 420, 826, 360, 830, 310, 834, 270));
            Add(d, "Gi_Lapel", B.Chest, Slot.TorsoTrim, H.RainLit, V(844, 234, 862, 228, 892, 330, 880, 352, 860, 310), 0, PartRole.Trim, false);
            Add(d, "Gi_LapelEdge", B.Chest, Slot.TorsoTrim, H.AccentDeep, Band(2.6f, 848, 236, 886, 342), 1, PartRole.Trim, false);

            // the fringed rain capelet over the shoulders, its hem in the accent
            var capelet = V(Concat(new float[] { 846, 222, 896, 212, 946, 226, 966, 256 }, Fringe(966, 300, 840, 296, 8, 22), new float[] { 832, 260 }));
            AddMulti(d, "Capelet", B.Chest, Slot.TorsoTrim, new List<(Vector2[], Color)>
            {
                Poly(capelet, H.Cape),
                Poly(Band(2.2f, 852, 248, 958, 258), H.RainLit),
                Poly(Band(2.2f, 846, 272, 962, 280), H.RainLit),
                Poly(Band(2.6f, 840, 294, 966, 298), H.Accent),
            }, 2, capelet);

            // the obi and its cord
            Add(d, "Obi", B.Chest, Slot.Belt, H.RainDeep, V(824, 388, 942, 392, 943, 416, 824, 412), 0, PartRole.Trim, false);
            Add(d, "Obi_Cord", B.Chest, Slot.BeltTrim, H.Accent, Band(3f, 826, 402, 942, 404), 0, PartRole.Trim, false);
            Add(d, "Obi_Knot", B.Chest, Slot.BeltTrim, H.AccentDeep, V(828, 396, 842, 394, 838, 430, 826, 428), 1, PartRole.Trim, false);
            Add(d, "Neck", B.Neck, Slot.Neck, H.Face, V(840, 200, 866, 200, 870, 240, 844, 240), 0, PartRole.Body, false);
        }

        // ---------------------------------------------------------------------------------------------- head: the rain hood and visor
        static void Head(RigDef d)
        {
            // the hood's tail hanging from the back of the head
            AddSwinging(d, "Hood_Tail", B.Head, Slot.Neck, H.Hood, V(904, 150, 930, 160, 962, 214, 976, 262, 950, 270, 926, 236, 902, 206), 912, 156, 0.6f, 1);

            // the face: black, a fine straight nose, the chin set forward into the rain, the pale eye
            var face = V(806, 152, 846, 148, 868, 168, 870, 210, 856, 230, 830, 234, 812, 222, 800, 206, 790, 196, 802, 188, 800, 168);
            Add(d, "Face", B.Head, Slot.Head, H.Face, face, 0);
            Add(d, "Eye", B.Head, Slot.Face, H.Pale, V(808, 178, 824, 172, 834, 176, 824, 182, 811, 183), 2, PartRole.Eye, false, null, 2.8f);
            Add(d, "Mouth", B.Head, Slot.Face, H.RainDeep, Band(2f, 804, 212, 818, 213), 1, PartRole.Detail, false);

            // the close hood, open at the face, a blue cord round its opening
            var hood = V(818, 150, 826, 120, 852, 100, 886, 98, 912, 112, 926, 140, 930, 182, 924, 214, 902, 236, 872, 238, 866, 206, 858, 180, 846, 166, 826, 160);
            AddMulti(d, "Hood", B.Head, Slot.HeadTop, new List<(Vector2[], Color)>
            {
                Poly(hood, H.Hood),
                Poly(Band(2.6f, 880, 108, 914, 150, 920, 200), H.RainLit),
                Poly(Band(3f, 826, 152, 846, 168, 858, 184, 866, 210, 872, 236), H.Accent),
            }, 0, hood);

            // the stiff visor jutting over the eyes: black lacquer, a lit edge
            var visor = V(828, 146, 860, 138, 892, 136, 870, 152, 830, 160, 790, 164, 756, 164, 760, 156, 792, 150);
            AddMulti(d, "Visor", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(visor, H.Lacquer),
                Poly(Band(2f, 760, 158, 800, 154, 846, 146, 880, 140), H.LacquerLit),
            }, 3, visor);

            // the face cloth pulled down round the throat, its hem in the accent
            AddMulti(d, "FaceCloth", B.Head, Slot.Face, new List<(Vector2[], Color)>
            {
                Poly(V(808, 222, 836, 232, 868, 236, 884, 252, 848, 262, 814, 252, 802, 236), H.Hood),
                Poly(Band(2.6f, 808, 238, 846, 250, 882, 250), H.Accent),
            }, 1, V(808, 222, 836, 232, 868, 236, 884, 252, 848, 262, 814, 252, 802, 236));
        }
    }
}
