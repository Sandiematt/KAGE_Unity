using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The 13 helmet drawings (SAN-72), each authored in head-frame numbers copied from its JS look: the shop's hoods and kabuto
    /// (<c>21-characters/kage/gear/head.js</c>), the Act I sub-boss heads that are also gear (<c>sub-bosses/*/head.js</c>) and
    /// Takashi's straw kasa. JS cel shading (shadow strip, highlight strip) is flattened to one tone per shape; the thin rim
    /// light and a few lit details carry the form. Colours are the JS tones lifted a little so they read against Kage's
    /// near-black body. Layers: 0 behind, 1 main shell, 2 details, 3 front, 4 top.
    /// </summary>
    static class HelmetLooks
    {
        static Color C(string hex) => KMath.Hex(hex);

        // tones (JS base tone, a touch lighter), the highlight tone, and the line colour
        static readonly Color Hood = C("#1b1818"), HoodHi = C("#352e2e");
        static readonly Color Lacq = C("#1e1e26"), LacqHi = C("#34343e");
        static readonly Color Iron = C("#262a31"), IronHi = C("#40454e");
        static readonly Color Horn = C("#24222b"), HornHi = C("#3d3a46");
        static readonly Color Line = C("#050404");
        static readonly Color Silver = Palette.Silver, SilverDark = C("#6f737a");
        static readonly Color Paper = C("#c4bba5"), PaperDark = C("#857d6c");
        static readonly Color Face = Palette.Body;
        static readonly Color GoldDark = C("#8a7440"), GoldMid = C("#c9a45c"), Crown = C("#a8893f"), CrownHi = C("#e0c27a");

        /// <summary>The JS <c>subTone</c>: an accent in three tones, dark to light.</summary>
        static (Color dark, Color mid, Color light) Acc(string hex)
        {
            var c = C(hex);
            return (new Color(c.r * 0.34f, c.g * 0.34f, c.b * 0.34f, 1f), new Color(c.r * 0.64f, c.g * 0.64f, c.b * 0.64f, 1f), c);
        }

        // the face-hugging pieces, in the Kage head frame: from the cheek under the eye, round the nose and mouth, to the chin
        const string ChinGuard = "[-0.55,0.1,1],[0.24,0.24,1],[0.6,0.74,1],[1.03,0.76,1],[1.01,0.98,1],[0.8,1.16,1],[0.3,1.26,1],[-0.3,1.22,1],[-0.58,1.0,1]";
        const string VeilCut = "[-0.5,0.34,1],[0.3,0.24,1],[0.8,0.17,1],[1.06,0.15,1],[1.04,0.92,1],[0.8,1.14,1],[0.3,1.24,1],[-0.3,1.2,1],[-0.54,0.96,1]";
        const string KabutoBowl = "[1.0,-0.36,1],[0.86,-0.92],[0.14,-1.24],[-0.7,-1.06],[-1.08,-0.4,1]";
        const string ShikoroStd = "[0.1,-0.52],[-0.88,-0.82],[-1.56,0.0],[-1.86,0.74,1],[-0.92,0.94],[-0.42,0.46],[-0.2,-0.2]";

        // ---------------------------------------------------------------- shared pieces

        /// <summary>A lacquered neck guard whose lames are laced in the accent (dashed) over a dark edge.</summary>
        static void Shikoro(HelmetArt a, int layer, string shape, Color tone, Color lace, float k0, float k1)
        {
            a.Fill(layer, shape, tone);
            for (int i = 1; i <= 3; i++)
            {
                float k = i / 4f;
                a.Stroke(layer + 1, Line, 0.06f, HelmetArt.QuadPath(-0.3f - k * 0.2f, -0.4f + k * k0, -0.9f - k * 0.3f, -0.62f + k * k0, -1.2f - k * k1, -0.5f + k * (k0 + 0.3f), 8));
                a.Dashed(layer + 1, lace, 0.07f, HelmetArt.QuadPath(-0.3f - k * 0.2f, -0.3f + k * k0, -0.9f - k * 0.3f, -0.52f + k * k0, -1.2f - k * k1, -0.4f + k * (k0 + 0.3f), 14));
            }
        }

        /// <summary>A riveted kabuto bowl with a band at the brow and ribs from the crown.</summary>
        static void Bowl(HelmetArt a, int layer, Color tone, Color band, float[] ribs, string shape = "[1.08,-0.34,1],[0.96,-0.98],[0.2,-1.4],[-0.7,-1.22],[-1.16,-0.4,1]")
        {
            a.Fill(layer, shape, tone);
            foreach (float x in ribs)
            {
                a.Stroke(layer + 1, Line, 0.045f, HelmetArt.QuadPath(0.1f, -1.36f, x * 1.1f, -1.08f, x * 1.24f, -0.42f, 6));
                a.Disc(layer + 1, Silver, x * 1.2f, -0.62f, 0.035f);
            }
            a.Stroke(layer + 1, band, 0.13f, HelmetArt.QuadPath(-1.14f, -0.48f, 0f, -0.64f, 1.08f, -0.44f, 12));
        }

        /// <summary>The iron hōate on the jaw (nose and lips bare) with two laced throat lames under it.</summary>
        static void ChinAndThroat(HelmetArt a, int layer, Color tone, Color lace, Color? edge = null)
        {
            a.Fill(layer, "[-0.12,0.9],[0.34,1.02,1],[0.66,1.04,1],[0.7,1.42,1],[0.02,1.48,1],[-0.24,1.16]", tone);
            foreach (float y in new[] { 1.17f, 1.31f })
            {
                a.Stroke(layer + 1, Line, 0.06f, -0.3f, y - 0.05f, 0.8f, y);
                a.Dashed(layer + 1, lace, 0.05f, HelmetArt.QuadPath(-0.3f, y + 0.01f, 0.25f, y + 0.035f, 0.8f, y + 0.06f, 8));
            }
            a.Fill(layer + 1, ChinGuard, tone);
            if (edge.HasValue) a.Stroke(layer + 2, edge.Value, 0.06f, 0.2f, 0.3f, 0.58f, 0.8f, 1.0f, 0.82f);
        }

        static void Visor(HelmetArt a, int layer, string shape, Color tone, Color? line = null)
        {
            a.Fill(layer, shape, tone);
            if (line.HasValue) a.Stroke(layer + 1, line.Value, 0.03f, 0.1f, -0.36f, 1.24f, -0.27f);
        }

        // ---------------------------------------------------------------- 1. Kuro Zukin (ronin_hood)

        public static void RoninHood(HelmetArt a)
        {
            var acc = Acc("#b02a31");
            // the drape over the nape, then the hood wrapping the head to the jaw
            a.Fill(0, "[-0.36,0.42],[-1.08,0.34],[-1.56,1.2,1],[-1.02,1.5],[-0.24,1.36],[0.24,1.1]", Hood);
            a.Stroke(0, HoodHi, 0.04f, HelmetArt.QuadPath(-0.8f, 0.6f, -1.0f, 1.0f, -0.9f, 1.38f, 6));
            a.Fill(1, "[1.0,-0.34,1],[0.86,-0.86],[0.2,-1.24],[-0.62,-1.16],[-1.16,-0.52],[-1.18,0.36],[-0.82,1.02],[-0.1,1.24],[0.52,1.12],[0.98,0.76],[1.06,0.36,1]", Hood);
            a.Stroke(2, HoodHi, 0.04f, HelmetArt.QuadPath(0.3f, -1.14f, -0.4f, -0.9f, -0.86f, -0.2f, 8));
            a.Stroke(2, HoodHi, 0.04f, HelmetArt.QuadPath(-0.3f, 0.5f, -0.2f, 0.86f, -0.5f, 1.1f, 8));
            a.Stroke(2, HoodHi, 0.04f, HelmetArt.QuadPath(0.06f, 0.74f, 0.12f, 0.98f, 0.02f, 1.16f, 8));   // the face cloth gathered under the jaw
            // the eye slit: Kage's black face showing through, so his eye sits in it
            a.Fill(2, "[0.06,-0.2],[0.6,-0.24],[1.06,-0.22,1],[1.08,0.3,1],[0.6,0.32],[0.06,0.28]", Face, false);
            a.Stroke(3, HoodHi, 0.04f, HelmetArt.QuadPath(0.1f, -0.23f, 0.6f, -0.29f, 1.04f, -0.27f, 8));
            // the crimson cord round the brow, knotted at the back
            a.Stroke(3, acc.mid, 0.09f, HelmetArt.QuadPath(0.98f, -0.4f, 0f, -0.62f, -1.12f, -0.4f, 14));
            a.Stroke(3, acc.light, 0.03f, HelmetArt.QuadPath(0.96f, -0.44f, 0f, -0.66f, -1.1f, -0.44f, 14));
            a.Fill(3, "[-1.0,-0.56],[-1.36,-0.72,1],[-1.34,-0.24,1],[-1.02,-0.3]", acc.mid, false);
            a.Tail("Tail", acc.mid, -1.2f, -0.46f, 1.5f, 0.28f, 0.12f, 1.3f);
        }

        // ---------------------------------------------------------------- 2. Sugegasa (straw_kasa)

        public static void StrawKasa(HelmetArt a)
        {
            var straw = C("#5a4a30");
            var gold = new[] { C("#3a2f1f"), C("#6e5c3e"), C("#8f7a54") };
            const float k = 1.2f;
            const string hat = "[-2.32,-0.5,1],[-0.04,-1.36,1],[2.36,-0.1,1],[1.1,-0.17],[0,-0.28],[-1.2,-0.4]";
            const string brim = "[-2.36,-0.52,1],[-1.2,-0.44],[0,-0.32],[1.1,-0.21],[2.42,-0.13,1],[2.38,0.0,1],[1.1,-0.08],[0,-0.18],[-1.2,-0.3],[-2.3,-0.4,1]";
            a.Fill(2, hat, straw, true, k);
            // the ribs from the apex, the lit ones brighter
            float ax = -0.04f * k, ay = -1.36f * k;
            foreach (var (rib, lit) in new[] { (-2.0f, false), (-1.3f, false), (-0.62f, true), (0.1f, true), (0.84f, true), (1.6f, false) })
            {
                float x1 = rib * k, y1 = (-0.47f + (rib + 2.32f) / 4.68f * 0.36f) * k;
                a.Stroke(3, Line, 0.05f, ax, ay, x1, y1);
                a.Stroke(3, lit ? gold[2] : gold[1], 0.028f, ax + 0.03f, ay + 0.02f, x1 + 0.03f, y1);
            }
            // the rim along the brim
            a.Fill(3, brim, gold[1], false, k);
            a.Stroke(4, gold[2], 0.035f, -2.32f * k, -0.52f * k, 0f, -0.33f * k, 2.4f * k, -0.14f * k);
        }

        // ---------------------------------------------------------------- 3. Hachigane (storm_band)

        public static void StormBand(HelmetArt a)
        {
            var acc = Acc("#7fb8ff");
            // the band round the brow, its edges in the accent
            a.Fill(1, "[-1.02,-0.66,1],[0,-0.76],[1.0,-0.6,1],[1.0,-0.32,1],[0,-0.46],[-1.02,-0.38,1]", C("#1c1a24"));
            a.Stroke(2, acc.mid, 0.04f, HelmetArt.QuadPath(-1f, -0.62f, 0f, -0.72f, 1f, -0.57f, 10));
            a.Stroke(2, acc.mid, 0.04f, HelmetArt.QuadPath(-1f, -0.42f, 0f, -0.5f, 1f, -0.35f, 10));
            // the iron plate on the front: riveted, engraved with a ledger entry struck through
            a.Fill(2, "[0.06,-0.8,1],[0.98,-0.68,1],[1.04,-0.26,1],[0.08,-0.32,1]", Iron);
            foreach (var (u, v) in new[] { (0.16f, -0.72f), (0.9f, -0.62f), (0.92f, -0.33f), (0.18f, -0.38f) }) a.Disc(3, Silver, u, v, 0.035f);
            foreach (var (y, w) in new[] { (-0.62f, 0.42f), (-0.52f, 0.3f), (-0.42f, 0.38f) }) a.Stroke(3, acc.mid, 0.035f, 0.36f, y, 0.36f + w, y + 0.01f);
            a.Stroke(3, acc.light, 0.04f, 0.32f, -0.36f, 0.84f, -0.68f);
            // the knot at the back and the two band tails
            a.Fill(3, "[-0.92,-0.62],[-1.24,-0.7,1],[-1.2,-0.3,1],[-0.92,-0.38]", acc.dark, false);
            a.Tail("TailA", acc.mid, -1.1f, -0.5f, 2.1f, 0.5f, 0.2f, 1.2f);
            a.Tail("TailB", acc.dark, -1.1f, -0.44f, 1.7f, 0.42f, 0.16f, 1.5f);
        }

        // ---------------------------------------------------------------- 4. Kasumi Veil (veil)

        public static void Veil(HelmetArt a)
        {
            var acc = Acc("#9b8cc9");
            var hood = C("#16151d");
            a.Fill(1, "[0.6,-0.84],[0,-1.12],[-0.62,-1.06],[-1.06,-0.5],[-1.1,0.4],[-0.82,1.1],[-0.2,1.24],[0.26,1.06],[0.02,0.56],[-0.2,-0.06],[0.1,-0.54]", hood);
            a.Stroke(2, HoodHi, 0.05f, HelmetArt.QuadPath(-0.4f, -1.0f, -0.9f, -0.2f, -0.62f, 0.9f, 8));
            // the grey mourning gauze drawn tight from under the eyes, a mist band across it
            a.Fill(2, VeilCut, C("#3a3746"));
            a.Stroke(3, acc.mid, 0.07f, HelmetArt.QuadPath(0f, 0.78f, 0.4f, 0.66f, 0.62f, 0.78f, 6));
            a.Stroke(3, acc.mid, 0.07f, 0.62f, 0.78f, 1.0f, 0.75f);
            // the silver brow band and a hairpin crossing the back of the hood
            a.Stroke(3, Silver, 0.07f, HelmetArt.QuadPath(0.88f, -0.38f, 0f, -0.62f, -1.06f, -0.3f, 12));
            a.Stroke(3, Silver, 0.06f, -0.2f, -1.02f, -1.2f, -0.2f);
            a.Disc(3, acc.light, -0.22f, -1.02f, 0.09f);
            // the long drape from the back of the hood
            a.Tail("Drape", C("#121118"), -1.0f, -0.6f, 2.5f, 0.9f, 0.36f, 1.0f, edge: acc.mid);
        }

        // ---------------------------------------------------------------- 5. Tetsu Kabuto (iron_kabuto)

        public static void IronKabuto(HelmetArt a)
        {
            var acc = Acc("#c9a45c");
            Shikoro(a, 0, "[0.12,-0.56],[-0.9,-0.84],[-1.62,0.0],[-2.0,0.82,1],[-0.96,1.02],[-0.42,0.5],[-0.2,-0.2]", Iron, acc.dark, 1.0f, 0.72f);
            ChinAndThroat(a, 0, Iron, acc.dark, acc.mid);
            a.Fill(2, "[1.1,-0.34,1],[0.98,-1.02],[0.2,-1.46],[-0.72,-1.26],[-1.18,-0.4,1]", Iron);
            foreach (float x in new[] { -0.7f, -0.35f, 0f, 0.35f, 0.7f })
            {
                a.Stroke(3, Line, 0.045f, HelmetArt.QuadPath(0.1f, -1.4f, x * 1.1f, -1.1f, x * 1.25f, -0.42f, 6));
                a.Disc(3, GoldMid, x * 1.2f, -0.58f, 0.05f);
            }
            foreach (float x in new[] { -0.32f, 0.03f, 0.38f }) a.Stroke(3, IronHi, 0.025f, HelmetArt.QuadPath(0.12f, -1.36f, x * 1.1f + 0.03f, -1.1f, x * 1.25f + 0.04f, -0.44f, 6));
            a.Stroke(3, acc.dark, 0.16f, HelmetArt.QuadPath(-1.14f, -0.5f, 0f, -0.66f, 1.08f, -0.46f, 12));
            a.Stroke(3, acc.mid, 0.04f, HelmetArt.QuadPath(-1.12f, -0.56f, 0f, -0.72f, 1.06f, -0.52f, 12));
            Visor(a, 3, "[0.36,-0.52],[1.46,-0.46,1],[1.36,-0.28,1],[0.36,-0.34]", Iron);
            a.Fill(3, "[0.34,-0.52],[0.18,0.1,1],[-0.32,0.0],[-0.16,-0.54]", Iron);
            a.Stroke(4, acc.mid, 0.05f, 0.3f, -0.48f, 0.16f, 0.04f);
            // the great gold kuwagata rising off the brow round a gate stud
            a.Fill(4, "[0.5,-1.0,1],[0.92,-1.6],[1.36,-2.7,1],[0.82,-1.86],[0.4,-1.2,1]", Crown);
            a.Fill(4, "[0.34,-1.04,1],[0.14,-1.9],[-0.22,-2.84,1],[0.3,-1.96],[0.56,-1.12,1]", C("#7a6330"));
            a.Disc(4, GoldMid, 0.5f, -1.02f, 0.17f);
            a.Disc(4, CrownHi, 0.45f, -1.07f, 0.07f);
        }

        // ---------------------------------------------------------------- 6. Oni Menpo (oni_mask)

        public static void OniMask(HelmetArt a)
        {
            var acc = Acc("#d24a3c");
            // the brow plate and its two short iron horns
            a.Fill(0, "[-0.36,-0.92],[-0.52,-1.5],[-0.7,-1.96,1],[-0.26,-1.42],[-0.06,-0.98]", Horn);
            a.Fill(1, "[0.2,-0.9],[0.48,-1.5],[0.36,-2.12,1],[0.74,-1.5],[0.68,-0.9]", HornHi);
            a.Fill(2, "[-0.92,-0.72,1],[-0.2,-0.98],[0.6,-0.94],[1.0,-0.62],[1.04,-0.34,1],[0.3,-0.5],[-0.5,-0.46],[-0.94,-0.4,1]", Iron);
            foreach (float x in new[] { -0.6f, 0f, 0.62f }) a.Disc(3, Silver, x, -0.66f, 0.045f);
            a.Stroke(3, acc.mid, 0.04f, HelmetArt.QuadPath(-0.9f, -0.44f, 0f, -0.56f, 1f, -0.38f, 10));
            // the jaw guard below the lips, two bone tusks rising beside the mouth
            a.Fill(2, ChinGuard, Iron);
            a.Stroke(3, acc.dark, 0.05f, 0.5f, 0.76f, 0.9f, 0.8f);
            a.Fill(3, "[0.6,0.8,1],[0.72,0.8,1],[0.66,0.54,1]", Paper, false);
            a.Fill(3, "[0.45,0.78,1],[0.55,0.78,1],[0.5,0.6,1]", Paper, false);
        }

        // ---------------------------------------------------------------- 7. Hoshi Kabuto (sentinel_helm)

        public static void SentinelHelm(HelmetArt a)
        {
            var acc = Acc("#b07cff");
            Shikoro(a, 0, "[0.12,-0.56],[-0.86,-0.82],[-1.5,0.0],[-1.8,0.7,1],[-0.9,0.9],[-0.42,0.46],[-0.2,-0.2]", Lacq, acc.mid, 0.95f, 0.6f);
            ChinAndThroat(a, 0, Lacq, acc.mid);
            a.Fill(2, KabutoBowl, Lacq);
            foreach (var (x, y0) in new[] { (-0.62f, -0.98f), (-0.2f, -1.18f), (0.24f, -1.16f), (0.62f, -0.96f) })
                for (int j = 0; j < 3; j++) a.Disc(3, Silver, x * (1 + j * 0.12f), y0 + j * 0.2f, 0.04f);
            a.Stroke(3, acc.mid, 0.1f, HelmetArt.QuadPath(-1.1f, -0.47f, 0f, -0.6f, 1.05f, -0.43f, 12));
            a.Fill(3, "[0.42,-0.5],[1.32,-0.44,1],[1.24,-0.3,1],[0.4,-0.34]", Lacq);
            a.Fill(3, "[0.34,-0.5],[0.24,0.04,1],[-0.22,-0.04],[-0.1,-0.52]", acc.mid, false);
            // the forked lightning maedate in silver
            a.Fill(4, "[0.4,-1.06,1],[0.12,-1.66,1],[0.3,-1.66,1],[-0.06,-2.2,1],[0.44,-1.6,1],[0.28,-1.58,1],[0.58,-1.08,1]", SilverDark);
            a.Fill(4, "[0.52,-1.02,1],[0.86,-1.6,1],[0.66,-1.62,1],[1.12,-2.36,1],[0.92,-1.84,1],[1.08,-1.82,1],[0.7,-1.2,1],[0.66,-1.04,1]", Silver);
            // the agemaki cords at the back of the kabuto: two violet tassels
            a.Tail("TasselA", acc.mid, -1.3f, -0.1f, 1.1f, 0.28f, 0.34f, 1.3f);
            a.Tail("TasselB", acc.dark, -1.25f, 0.12f, 0.9f, 0.24f, 0.3f, 1.6f);
        }

        // ---------------------------------------------------------------- 8. Shika Kabuto (sage_antlers)

        static void Antler(HelmetArt a, int layer, float ox, float oy, Color tone, Color tip, bool far)
        {
            a.Tine(layer, tone, 0.3f, 0.08f, !far, 0f + ox, -1.1f + oy, 0.3f + ox, -1.72f + oy, 0.18f + ox, -2.36f + oy, 0.44f + ox, -3.0f + oy);
            a.Tine(layer, tone, 0.18f, 0.06f, !far, 0.26f + ox, -1.66f + oy, 0.74f + ox, -1.98f + oy, 0.98f + ox, -1.92f + oy);
            a.Tine(layer, tone, 0.16f, 0.06f, !far, 0.2f + ox, -2.3f + oy, -0.16f + ox, -2.7f + oy, -0.3f + ox, -2.98f + oy);
            a.Tine(layer, tone, 0.12f, 0.05f, !far, 0.36f + ox, -2.72f + oy, 0.74f + ox, -2.96f + oy);
            foreach (var (u, v) in new[] { (0.44f, -3.0f), (0.98f, -1.92f), (-0.3f, -2.98f), (0.74f, -2.96f) }) a.Disc(layer, tip, u + ox, v + oy, 0.06f);
        }

        public static void SageAntlers(HelmetArt a)
        {
            var acc = Acc("#7fd0c8");
            Antler(a, 0, -0.62f, 0.16f, C("#1c1610"), acc.mid, true);
            Shikoro(a, 0, ShikoroStd, Lacq, acc.mid, 1.0f, 0.6f);
            Bowl(a, 2, Iron, acc.dark, new[] { -0.7f, -0.35f, 0f, 0.35f, 0.7f });
            Visor(a, 3, "[0.04,-0.5],[0.9,-0.52],[1.46,-0.36,1],[1.38,-0.22,1],[0.8,-0.3],[0.04,-0.34]", Lacq);
            Antler(a, 4, 0.06f, 0f, C("#3a2d20"), acc.light, false);
        }

        // ---------------------------------------------------------------- 9. Utsuro Kabuto (hollow_crest)

        public static void HollowCrest(HelmetArt a)
        {
            var acc = Acc("#9b8cff");
            float pi = Mathf.PI;
            // a great hollow crescent swept back off the crown; its inner edge glows violet
            var crescent = HelmetArt.ArcPath(-0.3f, -0.2f, 2.0f, pi * 0.8f, pi * 1.7f, 22);
            crescent.AddRange(HelmetArt.ArcPath(-0.05f, -0.35f, 1.55f, pi * 1.68f, pi * 0.82f, 22));
            a.Fill(0, crescent, Iron);
            foreach (float k in new[] { 0.95f, 1.15f, 1.35f, 1.55f })
                a.Stroke(1, Line, 0.05f, -0.3f + Mathf.Cos(pi * k) * 2.0f, -0.2f + Mathf.Sin(pi * k) * 2.0f, -0.05f + Mathf.Cos(pi * k) * 1.55f, -0.35f + Mathf.Sin(pi * k) * 1.55f);
            a.Stroke(1, acc.mid, 0.12f, HelmetArt.ArcPath(-0.05f, -0.35f, 1.6f, pi * 0.86f, pi * 1.66f, 20));
            a.Stroke(1, new Color(0.61f, 0.55f, 1f, 0.35f), 0.16f, HelmetArt.ArcPath(-0.05f, -0.35f, 1.5f, pi * 0.9f, pi * 1.64f, 20));
            a.Stroke(1, acc.light, 0.035f, HelmetArt.ArcPath(-0.05f, -0.35f, 1.56f, pi * 0.9f, pi * 1.64f, 20));
            Shikoro(a, 1, ShikoroStd, Lacq, acc.dark, 1.0f, 0.6f);
            Bowl(a, 2, Lacq, acc.dark, new[] { -0.6f, -0.2f, 0.2f, 0.6f });
            Visor(a, 3, "[0.04,-0.5],[0.86,-0.5],[1.34,-0.34,1],[1.26,-0.22,1],[0.76,-0.3],[0.04,-0.34]", Iron, acc.light);
        }

        // ---------------------------------------------------------------- 10. Shikko Zukin (executioner)

        public static void Executioner(HelmetArt a)
        {
            var acc = Acc("#b02a31");
            // a tall peaked hood of black cloth falling to the shoulders
            a.Fill(1, "[-1.2,1.0],[-1.18,-0.5],[-0.62,-1.7],[-0.22,-2.9,1],[0.32,-1.62],[1.02,-0.6],[1.12,0.4,1],[1.02,1.0],[0.4,1.3],[-0.5,1.28]", Hood);
            a.Stroke(2, HoodHi, 0.04f, HelmetArt.QuadPath(-0.4f, -2.2f, -0.86f, -1.0f, -0.9f, 0.2f, 10));
            a.Stroke(2, HoodHi, 0.04f, HelmetArt.QuadPath(-0.66f, 0.5f, -0.6f, 0.9f, -0.84f, 1.2f, 8));
            a.Stroke(2, HoodHi, 0.04f, HelmetArt.QuadPath(0f, 0.7f, 0.08f, 0.96f, -0.04f, 1.2f, 8));
            // the seam up the peak stitched in crimson, the eye slit, the cord at the throat
            a.Dashed(3, acc.mid, 0.05f, HelmetArt.QuadPath(-0.24f, -2.8f, 0.1f, -1.6f, 0.5f, -1.24f, 18));
            a.Fill(2, "[0.06,-0.18],[0.6,-0.22],[1.06,-0.2,1],[1.08,0.28,1],[0.6,0.3],[0.06,0.26]", Face, false);
            a.Stroke(3, HoodHi, 0.04f, HelmetArt.QuadPath(0.1f, -0.21f, 0.6f, -0.27f, 1.04f, -0.25f, 8));
            a.Fill(3, "[-0.9,1.0,1],[0.9,0.96,1],[0.88,1.12,1],[-0.9,1.16,1]", acc.mid, false);
            a.Tail("Cord", acc.mid, -0.85f, 1.1f, 0.9f, 0.16f, 0.1f, 1.4f, -0.4f, 1f);
        }

        // ---------------------------------------------------------------- 11. Raijin Kabuto (raijin_kabuto)

        public static void RaijinKabuto(HelmetArt a)
        {
            var red = Acc("#d24a3c");
            // the stepped neck guard swept back, the angular faceted bowl, the peak jutting over the brow, a short red crest
            a.Fill(0, "[-0.4,-0.55,1],[-1.7,-0.05,1],[-1.55,0.28,1],[-1.95,0.42,1],[-1.75,0.72,1],[-0.85,0.78,1],[-0.3,0.15,1]", Iron);
            a.Stroke(1, red.mid, 0.08f, -1.62f, 0.3f, -0.62f, 0.28f);
            a.Fill(1, "[-1.15,-0.1,1],[-0.98,-0.92,1],[-0.3,-1.34,1],[0.5,-1.24,1],[1.1,-0.66,1],[1.2,-0.18,1]", Iron);
            a.Stroke(2, red.mid, 0.08f, -0.98f, -0.92f, 1.1f, -0.66f);
            a.Stroke(2, IronHi, 0.04f, -0.3f, -1.3f, 0.5f, -1.2f);
            a.Fill(3, "[0.1,-0.42,1],[1.62,-0.34,1],[1.15,-0.08,1],[0.1,-0.1,1]", Iron);
            a.Fill(4, "[0.12,-1.24,1],[0.5,-1.86,1],[0.36,-1.78,1],[0.82,-2.36,1],[0.62,-1.62,1],[0.8,-1.68,1],[0.56,-1.2,1]", red.mid);
        }

        // ---------------------------------------------------------------- 12. Sumi Eboshi (sumi_eboshi)

        public static void SumiEboshi(HelmetArt a)
        {
            var acc = Acc("#c9a45c");
            // the censor's slip with black bars, hanging from the back edge of the cap
            a.Fill(0, "[-0.62,-0.5],[-1.22,-0.2],[-1.36,0.82,1],[-1.0,0.86,1],[-0.86,-0.1]", Paper);
            a.Fill(1, "[-1.18,0.12,1],[-0.9,0.08,1],[-0.9,0.24,1],[-1.2,0.28,1]", C("#060505"), false);
            a.Fill(1, "[-1.28,0.5,1],[-0.94,0.46,1],[-0.95,0.58,1],[-1.3,0.62,1]", C("#060505"), false);
            // a tall black lacquered court cap leaning back, its peak folded over
            a.Fill(1, "[0.92,-0.3,1],[0.8,-0.95],[0.52,-1.8],[0.2,-2.5],[-0.24,-2.84,1],[-0.66,-2.62],[-1.0,-1.7],[-1.06,-0.72],[-0.98,-0.08,1],[-0.4,-0.38],[0.4,-0.48]", Lacq);
            a.Stroke(2, Line, 0.05f, HelmetArt.QuadPath(0.2f, -0.5f, 0.1f, -1.6f, -0.18f, -2.6f, 10));
            a.Stroke(2, Line, 0.05f, HelmetArt.QuadPath(-0.5f, -0.42f, -0.62f, -1.4f, -0.6f, -2.4f, 10));
            // the pale dry-brush stroke of ink across it
            var ink = HelmetArt.QuadPath(-0.86f, -1.2f, -0.2f, -1.72f, 0.62f, -1.42f, 10);
            ink.AddRange(HelmetArt.QuadPath(0.5f, -1.3f, -0.15f, -1.48f, -0.84f, -1.06f, 10));
            a.Fill(3, ink, Paper, false);
            a.Stroke(3, acc.mid, 0.07f, HelmetArt.QuadPath(-0.98f, -0.16f, 0f, -0.5f, 0.9f, -0.36f, 12));
            a.Fill(3, "[-0.24,-2.82],[-1.02,-3.02,1],[-0.66,-2.44]", Lacq);
            // the gold chin cord along the jaw, its loose end behind the cheek
            a.Stroke(3, acc.mid, 0.06f, HelmetArt.QuadPath(-0.8f, -0.12f, -0.3f, 1.12f, 0.5f, 0.92f, 14));
            a.Tail("Cord", acc.mid, -0.4f, 1.05f, 0.9f, 0.14f, 0.08f, 1.5f, -0.35f, 1f);
        }

        // ---------------------------------------------------------------- 13. Hisame Zukin (rain_hood)

        public static void RainHood(HelmetArt a)
        {
            var acc = Acc("#7fb8ff");
            var rain = C("#18202b");
            // a close oiled hood open at the face, a blue cord round it
            a.Fill(1, "[0.75,-0.72],[0.2,-1.12],[-0.56,-1.1],[-1.08,-0.56],[-1.12,0.36],[-0.86,1.16],[-0.2,1.32],[0.3,1.12],[0.04,0.56],[-0.18,-0.1],[0.12,-0.46]", rain);
            a.Stroke(2, Line, 0.05f, HelmetArt.QuadPath(-0.3f, -1.0f, -0.86f, -0.3f, -0.6f, 0.9f, 10));
            a.Stroke(2, acc.mid, 0.07f, HelmetArt.QuadPath(0.7f, -0.62f, -0.3f, -0.82f, -1.06f, -0.3f, 12));
            // the face cloth pulled down round the throat, its hem in the accent
            a.Fill(2, "[-0.26,0.74],[0.16,0.96],[0.5,1.06],[0.74,1.1,1],[0.7,1.3],[0.2,1.42],[-0.3,1.24]", rain);
            a.Stroke(3, acc.mid, 0.06f, HelmetArt.QuadPath(-0.3f, 0.8f, 0.24f, 1.04f, 0.74f, 1.12f, 10));
            // the stiff visor jutting over the eyes
            a.Fill(3, "[-0.1,-0.64],[0.7,-0.68],[1.78,-0.5,1],[1.72,-0.34,1],[0.76,-0.32],[0,-0.38]", Lacq);
            a.Stroke(4, acc.light, 0.04f, 0.1f, -0.4f, 0.76f, -0.36f, 1.7f, -0.37f);
            // the rain cape tail and the thin cord end
            a.Tail("Cape", C("#202a36"), -1.0f, 0.1f, 2.4f, 0.9f, 0.3f, 1.0f, edge: acc.mid);
            a.Tail("Cord", acc.mid, -1.05f, -0.4f, 1.6f, 0.12f, 0.08f, 1.4f);
        }
    }
}
