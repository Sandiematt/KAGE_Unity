using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The other eight trophy armours (SAN-90 rework, "each armor should have different design like first 2 ones"). Each is drawn from
    /// its JS look on Kage, the same way <see cref="HalfDo"/> and <see cref="VanguardPlate"/> are: plates and trim over his chest, a
    /// shoulder piece on the near arm, hanging pieces on the coat, tones and signature details from the JS drawing. Shapes are in
    /// reference pixels against his body (x right, y down, Kage facing left). His near arm covers most of the chest from x 884 on, so
    /// the designs read in the front half of the torso (x 808..884) and the shoulder piece, as the first two do.
    /// <list type="bullet">
    /// <item>Shirabe-do (<c>operative_vest</c>, sub-bosses/sayo): lacquered strips ticked in lilac, a strap of pale message slips, a small sode.</item>
    /// <item>Kasane Haori (<c>ledger_coat</c>, sub-bosses/kasane): a slate haori shingled with ledger pages, a violet ruled collar band, a pasted skirt panel.</item>
    /// <item>Tetsumon-do (<c>gate_plate</c>, sub-bosses/gonryu): iron boards, two straps of gold gate studs, the kannuki bar, a studded square sode.</item>
    /// <item>Kudamaki-do (<c>conduit_harness</c>, sub-bosses/moro): a leather do crossed by two glass tubes of violet light round a ring, a canister on the back.</item>
    /// <item>Mei-En Garb (<c>mei_en</c>, bosses/akari): two overlapping dark plates edged sakura pink, a pink sash with blossoms hung from the belt.</item>
    /// <item>Kysmei Robe (<c>kysmei</c>, bosses/witness): a violet-edged crossed robe with a gem, crimson veins, a rune sigil, a standing collar and drifting shards.</item>
    /// <item>Kurokumo Yoroi (<c>tyrant</c>, sub-bosses/echo_warden): heavy black iron laced in ember red with burnt veins, spiked gorget and sode, a black cape.</item>
    /// <item>Shinsei Yoroi (<c>shinsei</c>, bosses/takashi): a plum capelet edged gold over the shoulders, a gold clasp with a tassel, swagged cords.</item>
    /// </list>
    /// The JS glow, cloth physics and animation are flattened to one tone per shape (a thin rim light on the main plates), as for the
    /// other armours; the glowing tubes, gems and veins are HDR-lit parts.
    /// </summary>
    static partial class ArmorLooks
    {
        const int ChestGlowOrder = ChestOrder + 1, ChestTopOrder = ChestOrder + 2;

        static readonly Color Black = new Color32(0x05, 0x04, 0x06, 255);
        static readonly Color GoldBase = new Color32(0xc9, 0xa4, 0x5c, 255), GoldLo = new Color32(0x8a, 0x74, 0x40, 255), GoldDk = new Color32(0x4a, 0x3c, 0x1e, 255);
        static readonly Color LeatherBase = new Color32(0x2b, 0x20, 0x18, 255), LeatherHi = new Color32(0x4a, 0x37, 0x2a, 255);

        // ---------------------------------------------------------------- shapes

        static List<Vector2> Box(float x0, float y0, float x1, float y1) => ArmorArt.Pts(x0, y0, x1, y0, x1, y1, x0, y1);

        /// <summary>A rectangle of w by h turned by <paramref name="rot"/> radians about its centre.</summary>
        static List<Vector2> TurnedBox(float cx, float cy, float w, float h, float rot)
        {
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            Vector2 R(float x, float y) => new(cx + x * c - y * s, cy + x * s + y * c);
            return new List<Vector2> { R(-w / 2, -h / 2), R(w / 2, -h / 2), R(w / 2, h / 2), R(-w / 2, h / 2) };
        }

        /// <summary>A line from a to b wavering sideways by <paramref name="amp"/> over <paramref name="cycles"/> sine cycles.</summary>
        static List<Vector2> Wave(Vector2 a, Vector2 b, float amp, float cycles, int n = 18)
        {
            Vector2 d = (b - a), nrm = new Vector2(-d.y, d.x).normalized;
            var o = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++) { float q = i / (float)n; o.Add(Vector2.Lerp(a, b, q) + nrm * Mathf.Sin(q * Mathf.PI * 2f * cycles) * amp); }
            return o;
        }

        /// <summary>A lightning-like zigzag through the points.</summary>
        static List<Vector2> Bolt(params float[] xy) => ArmorArt.Pts(xy);

        // ---------------------------------------------------------------- Shirabe-do

        /// <summary>Shirabe-do (the operative vest): a close vest of lacquered strips, a strap of pale message slips across the chest, a small sode.</summary>
        public static void OperativeVest(ArmorArt a)
        {
            Color lilac = C("#9b8cc9"), paper = C("#c4bba5"), paperHi = C("#e4dcc8"), strap = C("#2a2a34");
            a.Fill(B.Chest, ChestOrder, LacqBase, true, 822, 258, 811, 300, 809, 352, 846, 360, 900, 360, 944, 354, 946, 298, 936, 262, 890, 250);
            // the strips: a dark seam, a lit edge, and a lilac stitch between the rows
            for (int i = 0; i < 6; i++)
            {
                float x = 826f + i * 22f;
                a.Line(B.Chest, ChestOrder, Black, 2f, x, 256, x - 2, 358);
                a.Line(B.Chest, ChestOrder, LacqHi, 1.8f, x + 3, 258, x + 1, 356);
                for (int r = 0; r < 4; r++) a.Line(B.Chest, ChestOrder, lilac, 1.8f, x - 8, 274 + r * 24, x - 2, 275 + r * 24);
            }
            // the strap of message slips, from the far shoulder down across the chest to the near hip
            a.Line(B.Chest, ChestOrder, Black, 15f, 938, 262, 822, 352);
            a.Line(B.Chest, ChestOrder, strap, 10f, 938, 262, 822, 352);
            for (int i = 0; i < 3; i++)
            {
                float k = 0.5f + i * 0.18f, x = Mathf.Lerp(938f, 822f, k), y = Mathf.Lerp(262f, 352f, k);
                a.Fill(B.Chest, ChestTopOrder, paper, false, Box(x - 7, y - 9, x + 7, y + 7));
                a.Fill(B.Chest, ChestTopOrder, paperHi, false, Box(x - 7, y + 2, x + 7, y + 5));
            }
            // the high collar's wrap band at the base of the neck
            a.Line(B.Chest, ChestOrder, LacqHi, 5f, 828, 262, 872, 250);
            // the small guard on the near shoulder
            a.Fill(B.ArmNU, SodeOrder, LacqBase, true, 884, 254, 926, 244, 948, 262, 944, 304, 892, 300);
            for (int i = 0; i < 2; i++) a.Line(B.ArmNU, SodeOrder, lilac, 1.8f, 892, 272 + i * 18, 942, 270 + i * 18);
            a.Line(B.ArmNU, SodeOrder, LacqHi, 2f, 888, 258, 926, 247, 946, 262);
        }

        // ---------------------------------------------------------------- Kasane Haori

        static void LedgerPage(ArmorArt a, float cx, float cy, float rot, Color paper, Color ink, Color? strike)
        {
            var box = TurnedBox(cx, cy, 30f, 21f, rot);
            a.Fill(B.Chest, ChestTopOrder, paper, false, box);
            a.Outline(B.Chest, ChestTopOrder, ink, 1.8f, box);
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            Vector2 R(float x, float y) => new(cx + x * c - y * s, cy + x * s + y * c);
            a.Line(B.Chest, ChestTopOrder, ink, 1.3f, R(-11, -3).x, R(-11, -3).y, R(11, -3).x, R(11, -3).y);
            a.Line(B.Chest, ChestTopOrder, ink, 1.3f, R(-11, 3).x, R(-11, 3).y, R(5, 3).x, R(5, 3).y);
            if (strike.HasValue) a.Line(B.Chest, ChestTopOrder, strike.Value, 2.4f, R(-12, 7).x, R(-12, 7).y, R(12, -7).x, R(12, -7).y);
        }

        /// <summary>Kasane Haori (the ledger coat): a slate haori open at the front, shingled with pasted ledger pages, one struck through, a violet ruled band, a pasted skirt panel.</summary>
        public static void LedgerCoat(ArmorArt a)
        {
            Color coat = C("#1d1d27"), coatLo = C("#0c0c11"), coatHi = C("#34344a"), violet = C("#b48ae6"), violetDk = C("#5d4688"), ink = C("#09080b");
            Color pg0 = C("#4a463c"), pg1 = C("#6b6555");
            a.Fill(B.Chest, ChestOrder, coat, true, 820, 256, 806, 300, 805, 356, 846, 366, 900, 366, 950, 358, 950, 296, 938, 260, 890, 248);
            a.Fill(B.Chest, ChestOrder, coatLo, false, 846, 262, 866, 304, 862, 362, 818, 362, 830, 304);       // the gi between the lapels
            a.Line(B.Chest, ChestOrder, coatHi, 2.2f, 812, 270, 808, 346);
            // the violet collar band down the lapel, ruled like a ledger column
            a.Line(B.Chest, ChestOrder, violetDk, 13f, 842, 258, 856, 298, 864, 344);
            for (int k = 0; k < 7; k++)
            {
                float y = 268f + k * 11.5f, x = 846f + k * 2.6f;
                a.Line(B.Chest, ChestTopOrder, violet, 1.8f, x - 7, y - 1, x + 6, y + 2);
            }
            // pasted pages over the chest and back, one struck through in violet
            LedgerPage(a, 826, 296, 0.08f, pg0, ink, null);
            LedgerPage(a, 828, 332, -0.06f, pg1, ink, violet);
            LedgerPage(a, 884, 292, 0.1f, pg1, ink, null);
            LedgerPage(a, 906, 332, 0.04f, pg0, ink, null);
            LedgerPage(a, 924, 286, -0.05f, pg0, ink, null);
            // the skirt panel pasted with pages, hung from the belt on the coat's front flap
            a.CoatFill("Coat", coat, true, new List<Vector2> { new(802, 398), new(896, 398), new(892, 508), new(852, 524), new(808, 506) });
            a.CoatLine("Coat", violetDk, 4f, new List<Vector2> { new(806, 504), new(852, 522), new(892, 506) });
            a.CoatFill("Coat", pg0, false, Box(812, 412, 846, 436));
            a.CoatFill("Coat", pg1, false, Box(852, 438, 886, 462));
            a.CoatFill("Coat", pg0, false, Box(814, 462, 846, 486));
            a.CoatLine("Coat", violet, 2.4f, new List<Vector2> { new(816, 484), new(844, 464) });
            foreach (var (x0, y0) in new[] { (812f, 412f), (852f, 438f), (814f, 462f) })
            {
                a.CoatLine("Coat", ink, 1.4f, new List<Vector2> { new(x0 + 4, y0 + 8), new(x0 + 30, y0 + 8) });
                a.CoatLine("Coat", ink, 1.4f, new List<Vector2> { new(x0 + 4, y0 + 15), new(x0 + 22, y0 + 15) });
            }
        }

        // ---------------------------------------------------------------- Tetsumon-do

        static void Stud(ArmorArt a, B bone, int order, float x, float y, float r)
        {
            a.Disc(bone, order, GoldDk, x, y, r);
            a.Disc(bone, order, GoldLo, x - 0.6f, y - 0.6f, r * 0.75f);
            a.Disc(bone, order, GoldBase, x - 1.2f, y - 1.2f, r * 0.35f);
        }

        /// <summary>Tetsumon-do (the gate plate): a flat-fronted iron do built like the Fortress door: boards, two straps of gold studs, the kannuki bar, a studded square sode.</summary>
        public static void GatePlate(ArmorArt a)
        {
            Color barDk = C("#101114"), bar = C("#3d424a");
            a.Fill(B.Chest, ChestOrder, IronBase, true, 818, 254, 805, 300, 805, 358, 846, 366, 900, 366, 950, 358, 950, 296, 940, 258, 890, 246);
            foreach (float x in new[] { 830f, 856f, 882f, 908f, 934f })
            {
                a.Line(B.Chest, ChestOrder, Black, 2.2f, x, 258, x, 362);
                a.Line(B.Chest, ChestOrder, IronHi, 1.8f, x + 3, 258, x + 3, 362);
            }
            // the two straps, studded in gold
            foreach (float y in new[] { 278f, 342f })
            {
                a.Fill(B.Chest, ChestTopOrder, barDk, false, Box(805, y - 10, 950, y + 10));
                a.Line(B.Chest, ChestTopOrder, IronHi, 1.5f, 806, y - 9, 950, y - 9);
                for (float x = 817f; x < 948f; x += 19f) Stud(a, B.Chest, ChestTopOrder, x, y, 6f);
            }
            // the kannuki: a heavy iron bar across the middle held in two gold brackets
            a.Fill(B.Chest, ChestTopOrder, barDk, false, Box(800, 306, 954, 320));
            a.Line(B.Chest, ChestTopOrder, bar, 3f, 801, 309, 953, 309);
            foreach (float x in new[] { 846f, 914f })
            {
                a.Fill(B.Chest, ChestTopOrder, GoldDk, false, Box(x - 10, 298, x + 10, 328));
                a.Fill(B.Chest, ChestTopOrder, GoldLo, false, Box(x - 7, 301, x + 7, 325));
                a.Line(B.Chest, ChestTopOrder, GoldBase, 2f, x - 6, 303, x + 6, 303);
            }
            // the great square sode with gate studs
            a.Fill(B.ArmNU, SodeOrder, IronBase, true, 866, 250, 962, 244, 970, 340, 872, 346);
            for (int i = 0; i < 4; i++) a.Line(B.ArmNU, SodeOrder, GoldLo, 1.8f, 872, 270 + i * 22, 966, 266 + i * 22);
            foreach (float yy in new[] { 276f, 300f, 324f })
                foreach (float xx in new[] { 890f, 918f, 946f }) Stud(a, B.ArmNU, SodeOrder, xx, yy + (xx - 890f) * -0.06f, 5.4f);
            a.Line(B.ArmNU, SodeOrder, IronHi, 2.2f, 870, 254, 962, 248);
            Kusazuri(a, IronBase, IronHi, GoldLo, 3, 72f);
        }

        // ---------------------------------------------------------------- Kudamaki-do

        /// <summary>Kudamaki-do (the conduit harness): a leather do under two straps crossed over the chest, each coiled with a glass tube of violet light, a ring where they cross, a canister on the back.</summary>
        public static void ConduitHarness(ArmorArt a)
        {
            Color violet = C("#a587ff"), violetLit = C("#c9bdff"), glass = C("#2c2640"), canVio = C("#141020");
            // the canister strapped high on his back, beside the torso
            a.Fill(B.Chest, ChestOrder - 6, canVio, true, 954, 244, 992, 244, 998, 330, 988, 338, 958, 338, 952, 330);
            a.Line(B.Chest, ChestOrder - 6, IronHi, 3f, 954, 262, 996, 262);
            a.Line(B.Chest, ChestOrder - 6, IronHi, 3f, 954, 322, 996, 322);
            a.Fill(B.Chest, ChestOrder - 5, violet, false, 966, 274, 986, 274, 986, 312, 966, 312);
            // the leather do
            a.Fill(B.Chest, ChestOrder, LeatherBase, true, 822, 258, 811, 300, 809, 352, 846, 362, 900, 362, 944, 356, 946, 298, 936, 262, 890, 250);
            a.Line(B.Chest, ChestOrder, Black, 2f, 850, 258, 852, 360);
            a.Line(B.Chest, ChestOrder, LeatherHi, 2.2f, 818, 268, 813, 342);
            // two straps crossed, each coiled with a glass tube
            var strapA = (from: new Vector2(930, 258), to: new Vector2(820, 352));
            var strapB = (from: new Vector2(822, 258), to: new Vector2(932, 352));
            foreach (var s in new[] { strapA, strapB })
            {
                a.Line(B.Chest, ChestOrder, Black, 15f, s.from.x, s.from.y, s.to.x, s.to.y);
                a.Line(B.Chest, ChestOrder, LeatherHi, 10.5f, s.from.x, s.from.y, s.to.x, s.to.y);
                a.Line(B.Chest, ChestTopOrder, glass, 4.4f, Wave(s.from, s.to, 0f, 1f));
                a.Line(B.Chest, ChestGlowOrder, violet, 3f, Wave(s.from, s.to, 3.6f, 3.4f), 1.7f);
            }
            // the ring where they cross, with a lit core
            Vector2 c = new(876, 305);
            a.Outline(B.Chest, ChestTopOrder, Black, 7f, ArmorArt.Circle(c.x, c.y, 17f, 14));
            a.Outline(B.Chest, ChestTopOrder, IronHi, 3.4f, ArmorArt.Circle(c.x, c.y, 17f, 14));
            a.Disc(B.Chest, ChestGlowOrder, violetLit, c.x, c.y, 8.5f, 1.9f);
        }

        // ---------------------------------------------------------------- Mei-En Garb

        static void Sakura(ArmorArt a, float cx, float cy, float r, Color petal, Color heart)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = i / 5f * Mathf.PI * 2f - Mathf.PI / 2f;
                a.CoatFill("Coat", petal, false, ArmorArt.Circle(cx + Mathf.Cos(t) * r * 0.62f, cy + Mathf.Sin(t) * r * 0.62f, r * 0.5f, 8));
            }
            a.CoatFill("Coat", heart, false, ArmorArt.Circle(cx, cy, r * 0.32f, 8));
        }

        /// <summary>Mei-En Garb (Akari's): two overlapping lacquered chest plates edged sakura pink, a pink sash hung from the belt with blossoms along it.</summary>
        public static void MeiEn(ArmorArt a)
        {
            Color guard = C("#2a1a21"), guardHi = C("#4b3441"), pink = C("#e0408e"), pinkHi = C("#ff8cc6"), pinkDk = C("#5a0a32"), blossom = C("#fff0f6");
            // the upper plate, then the lower one overlapping it
            a.Fill(B.Chest, ChestOrder, guard, true, 818, 262, 812, 306, 826, 316, 900, 322, 944, 316, 944, 270, 920, 254, 880, 252);
            a.Line(B.Chest, ChestTopOrder, guardHi, 2.2f, 822, 268, 816, 304);
            a.Line(B.Chest, ChestTopOrder, pink, 5.5f, 826, 316, 900, 322, 942, 316);
            a.Line(B.Chest, ChestTopOrder, pinkHi, 2.4f, 816, 270, 812, 306);
            a.Fill(B.Chest, ChestGlowOrder, guard, true, 820, 306, 814, 346, 830, 356, 900, 360, 940, 352, 940, 312, 900, 316, 850, 310);
            a.Line(B.Chest, ChestTopOrder, guardHi, 2.2f, 824, 312, 818, 342);
            a.Line(B.Chest, ChestTopOrder, pink, 5.5f, 830, 356, 900, 360, 938, 352);
            a.Line(B.Chest, ChestTopOrder, pinkHi, 2.4f, 818, 312, 815, 344);
            // the lacing between the lacquered lames
            foreach (float x in new[] { 852f, 884f, 916f })
            {
                a.Line(B.Chest, ChestTopOrder, pinkDk, 1.8f, x, 258, x - 2, 312);
                a.Line(B.Chest, ChestTopOrder, pinkDk, 1.8f, x, 318, x - 2, 354);
            }
            // the sash: pink, darker at the knot, a fold down the middle, blossoms along it, on the coat's front flap
            a.CoatFill("Coat", pink, true, new List<Vector2> { new(836, 398), new(868, 398), new(872, 468), new(870, 540), new(852, 566), new(834, 540), new(832, 468) });
            a.CoatFill("Coat", pinkDk, false, Box(835, 398, 869, 414));
            a.CoatLine("Coat", pinkDk, 2f, new List<Vector2> { new(852, 416), new(853, 470), new(851, 540) });
            a.CoatLine("Coat", pinkHi, 1.8f, new List<Vector2> { new(836, 400), new(833, 468), new(835, 540) });
            Sakura(a, 852, 462, 11f, blossom, pink);
            Sakura(a, 853, 498, 13f, blossom, pink);
            Sakura(a, 851, 534, 11f, blossom, pink);
        }

        // ---------------------------------------------------------------- Kysmei Robe

        static void Shard(ArmorArt a, B bone, int order, float cx, float cy, float rot, float z, int kind, bool front)
        {
            Color fill = front ? C("#0f0a1a") : C("#150f22"), facet = front ? C("#261b40") : C("#1f1634");
            Color edge = front ? C("#c4a6ff") : C("#6f58a8");
            Vector2[] raw = kind switch
            {
                0 => new[] { new Vector2(0, -z), new Vector2(z * 0.55f, z * 0.7f), new Vector2(-z * 0.5f, z * 0.5f) },
                1 => new[] { new Vector2(-z * 0.4f, -z * 0.8f), new Vector2(z * 0.45f, -z * 0.6f), new Vector2(z * 0.3f, z * 0.9f), new Vector2(-z * 0.5f, z * 0.6f) },
                2 => new[] { new Vector2(0, -z * 1.1f), new Vector2(z * 0.3f, 0), new Vector2(0, z * 1.1f), new Vector2(-z * 0.3f, 0) },
                _ => new[] { new Vector2(-z * 0.6f, -z * 0.3f), new Vector2(z * 0.7f, -z * 0.5f), new Vector2(z * 0.2f, z * 0.7f) },
            };
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            var pts = new List<Vector2>();
            foreach (var p in raw) pts.Add(new Vector2(cx + p.x * c - p.y * s, cy + p.x * s + p.y * c));
            a.Fill(bone, order, fill, false, pts);
            a.Fill(bone, order, facet, false, new List<Vector2> { pts[0], pts[1], Vector2.Lerp(pts[0], pts[pts.Count - 1], 0.3f) });
            a.Outline(bone, order, edge, 2f, pts);
        }

        /// <summary>Kysmei Robe (the Witness's, on Kage): a standing collar, a violet-edged robe with a crossed collar, a gem, crimson veins and a rune sigil, and drifting shards.</summary>
        public static void KysmeiRobe(ArmorArt a)
        {
            Color robe = C("#14101f"), robeHi = C("#2c2144"), robeLo = C("#050309"), edge = C("#c4a6ff"), seam = C("#6f58a8"), crim = C("#e03a52"), violet = C("#a77bff");
            // the standing collar behind his head, edged violet along the front
            a.Fill(B.Head, 80, robe, true, 884, 246, 912, 160, 936, 150, 936, 200, 906, 250);
            a.Line(B.Head, 80, edge, 2.4f, 890, 238, 912, 168, 930, 152);
            // the robe over the coat's upper body, the hem broken into points above the belt
            a.Fill(B.Chest, ChestOrder, robe, true, 826, 256, 810, 300, 806, 344, 818, 362, 830, 346, 846, 366, 858, 344, 878, 364, 892, 342, 912, 366, 926, 346, 944, 362, 950, 322, 948, 290, 938, 262, 890, 248);
            a.Line(B.Chest, ChestOrder, seam, 1.8f, 936, 270, 926, 346);
            a.Line(B.Chest, ChestOrder, robeHi, 2.4f, 814, 286, 810, 340);
            // the crossed collar: dark, edged violet, running down the front
            a.Line(B.Chest, ChestTopOrder, robeLo, 13f, 852, 254, 836, 300, 826, 346);
            a.Line(B.Chest, ChestTopOrder, edge, 3.2f, 846, 254, 830, 300, 820, 346);
            a.Line(B.Chest, ChestTopOrder, robeHi, 2f, 862, 262, 846, 310);
            // crimson veins running down the back and a rune sigil
            a.Line(B.Chest, ChestGlowOrder, crim, 2.6f, Bolt(900, 340, 910, 322, 902, 308, 920, 292, 914, 280, 934, 264), 1.6f);
            a.Line(B.Chest, ChestGlowOrder, crim, 2.2f, Bolt(906, 300, 918, 296, 914, 284, 930, 280), 1.6f);
            var sig = ArmorArt.Circle(924, 322, 17f, 14);
            a.Outline(B.Chest, ChestTopOrder, seam, 2.4f, sig);
            a.Outline(B.Chest, ChestTopOrder, seam, 2.2f, ArmorArt.Circle(924, 322, 10f, 12));
            a.Outline(B.Chest, ChestTopOrder, seam, 2f, ArmorArt.Pts(924, 315, 930, 326, 918, 326));
            // the violet gem where the collar crosses, with its glow
            a.Fill(B.Chest, ChestTopOrder, C("#07040c"), false, 832, 290, 842, 306, 832, 322, 822, 306);
            a.Disc(B.Chest, ChestGlowOrder, violet, 832, 306, 13f, 1.3f);
            a.Disc(B.Chest, ChestGlowOrder, C("#ffffff"), 832, 306, 4f, 1.7f);
            // the shards: behind and beside the body, and a broken skirt toward the knees
            Shard(a, B.Chest, ChestOrder - 6, 972, 262, 0.5f, 30f, 0, false);
            Shard(a, B.Chest, ChestOrder - 6, 986, 340, 2.1f, 26f, 1, false);
            Shard(a, B.Chest, ChestOrder - 6, 962, 408, 1.1f, 28f, 3, false);
            Shard(a, B.Chest, ChestTopOrder, 796, 266, -0.4f, 24f, 0, true);
            Shard(a, B.Chest, ChestTopOrder, 794, 346, 1.2f, 22f, 2, true);
            Shard(a, B.Hips, 66, 788, 452, 0.3f, 30f, 3, true);
            Shard(a, B.Hips, 66, 846, 540, 1.7f, 32f, 1, true);
            Shard(a, B.Hips, 66, 800, 600, -0.8f, 34f, 0, true);
            Shard(a, B.Hips, 30, 930, 520, 2.6f, 30f, 2, false);
            Shard(a, B.Hips, 30, 900, 604, 0.9f, 32f, 3, false);
        }

        // ---------------------------------------------------------------- Kurokumo Yoroi

        /// <summary>Kurokumo Yoroi (the tyrant plate): heavy black iron laced in ember red, burnt veins, a spiked gorget, stacked spiked sode, kusazuri, a black cape behind.</summary>
        public static void Tyrant(ArmorArt a)
        {
            Color ember = C("#d24a3c"), lace = C("#7a2e26"), cape = C("#14110f"), capeHi = C("#2a2523");
            // the cape, behind and beyond the back, its hem torn
            a.Fill(B.Chest, ChestOrder - 6, cape, true, 936, 262, 952, 248, 1004, 318, 1022, 420, 1004, 470, 1000, 526, 984, 494, 970, 556, 956, 484, 944, 520, 946, 380);
            a.Line(B.Chest, ChestOrder - 6, capeHi, 3f, 960, 262, 1004, 330, 1016, 416);
            // heavy iron over the chest, laced, a burnt vein up from the waist
            a.Fill(B.Chest, ChestOrder, IronBase, true, 818, 254, 805, 300, 805, 358, 846, 368, 900, 368, 952, 360, 952, 296, 940, 258, 890, 246);
            for (int i = 0; i < 3; i++) a.Line(B.Chest, ChestOrder, lace, 2.2f, 812, 280 + i * 28, 944, 276 + i * 28);
            a.Line(B.Chest, ChestOrder, Black, 2.2f, 856, 252, 856, 362);
            a.Line(B.Chest, ChestOrder, IronHi, 2.4f, 814, 266, 808, 346);
            a.Line(B.Chest, ChestGlowOrder, ember, 2.6f, Bolt(822, 352, 838, 330, 846, 340, 862, 316, 858, 300), 1.5f);
            a.Line(B.Chest, ChestGlowOrder, ember, 2.2f, Bolt(826, 302, 840, 292, 852, 304, 864, 286), 1.5f);
            // the gorget's spikes rising behind the head
            a.Fill(B.Chest, ChestOrder, IronBase, true, 898, 240, 918, 186, 928, 244);
            a.Fill(B.Chest, ChestOrder, IronBase, true, 922, 242, 950, 196, 950, 250);
            a.Fill(B.Chest, ChestOrder, IronBase, true, 874, 244, 886, 198, 902, 242);
            // the stacked sode, spiked along the top
            a.Fill(B.ArmNU, SodeOrder, IronBase, true, 878, 256, 950, 240, 962, 262, 958, 296, 886, 292);
            a.Fill(B.ArmNU, SodeOrder, IronBase, true, 886, 288, 960, 288, 966, 340, 894, 346);
            for (int i = 0; i < 3; i++) a.Line(B.ArmNU, SodeOrder, lace, 1.8f, 890, 310 + i * 12, 962, 308 + i * 12);
            foreach (float x in new[] { 896f, 920f, 944f })
                a.Fill(B.ArmNU, SodeOrder, IronBase, true, x - 9, 252 - (x - 896f) * 0.1f, x, 226 - (x - 896f) * 0.1f, x + 9, 248 - (x - 896f) * 0.1f);
            a.Line(B.ArmNU, SodeOrder, ember, 2.2f, 906, 262, 922, 280, 914, 300, 936, 330);
            Kusazuri(a, IronBase, IronHi, lace, 3, 76f);
        }

        // ---------------------------------------------------------------- Shinsei Yoroi

        /// <summary>Shinsei Yoroi (Takashi's): a plum capelet edged gold over the shoulders and the near arm, a gold clasp with a tassel, cords swagged across it.</summary>
        public static void Shinsei(ArmorArt a)
        {
            Color plum = C("#1d1119"), plumHi = C("#3a2030"), gold = C("#d0a443"), goldLo = C("#6e4f1c"), goldHi = C("#f4da8e");
            a.Fill(B.Chest, ChestOrder, plum, true, 824, 254, 806, 286, 808, 332, 826, 358, 850, 338, 884, 330, 930, 336, 952, 304, 946, 262, 890, 246);
            // two folds falling from the shoulder
            a.Line(B.Chest, ChestOrder, plumHi, 2.4f, 940, 268, 900, 290, 866, 296);
            a.Line(B.Chest, ChestOrder, plumHi, 2.4f, 836, 270, 822, 300);
            // the gold trim along the hem, with a fine second line inside
            var hem = new List<Vector2> { new(808, 330), new(826, 358), new(850, 338), new(884, 330), new(930, 336), new(952, 304) };
            a.Line(B.Chest, ChestTopOrder, gold, 4.4f, hem);
            var inner = new List<Vector2>();
            foreach (var p in hem) inner.Add(p + new Vector2(0, -9f));
            a.Line(B.Chest, ChestTopOrder, goldLo, 1.8f, inner);
            // the cords swagged from the clasp across the capelet
            a.Line(B.Chest, ChestTopOrder, goldLo, 4.2f, ArmorArt.Quad(834, 298, 872, 336, 912, 322));
            a.Line(B.Chest, ChestTopOrder, gold, 2.2f, ArmorArt.Quad(834, 298, 872, 336, 912, 322));
            a.Line(B.Chest, ChestTopOrder, goldLo, 4.2f, ArmorArt.Quad(834, 298, 814, 338, 822, 352));
            a.Line(B.Chest, ChestTopOrder, gold, 2.2f, ArmorArt.Quad(834, 298, 814, 338, 822, 352));
            // the clasp: a gold disc, a dark ring and dot, a tassel under it
            a.Line(B.Chest, ChestTopOrder, goldLo, 3.4f, 834, 310, 834, 346);
            a.Disc(B.Chest, ChestTopOrder, gold, 834, 350, 5f);
            a.Disc(B.Chest, ChestTopOrder, goldLo, 834, 298, 15f);
            a.Disc(B.Chest, ChestTopOrder, gold, 833, 297, 13f);
            a.Outline(B.Chest, ChestTopOrder, goldLo, 2.2f, ArmorArt.Circle(834, 298, 8f, 10));
            a.Disc(B.Chest, ChestTopOrder, goldLo, 834, 298, 3.2f);
            a.Disc(B.Chest, ChestTopOrder, goldHi, 829, 292, 2.4f);
            // the capelet over the near shoulder, gold along its edge
            a.Fill(B.ArmNU, SodeOrder, plum, true, 880, 250, 930, 234, 964, 256, 966, 304, 940, 322, 890, 312);
            a.Line(B.ArmNU, SodeOrder, plumHi, 2.4f, 892, 258, 934, 244, 958, 262);
            a.Line(B.ArmNU, SodeOrder, gold, 4.2f, 890, 312, 940, 322, 966, 304);
            a.Line(B.ArmNU, SodeOrder, goldLo, 1.8f, 892, 303, 938, 313, 960, 297);
        }
    }
}
