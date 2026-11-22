using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The JS game's brush banner as plain data (SAN-67; JS <c>Effects.banner</c> / <c>drawBanner</c>, 30-combat.js): an ink band painted
    /// from the left across the screen with ragged bristle edges, a coloured brush stroke along its top and a pale one along its bottom, the
    /// calligraphy glyph on the left, the title in the brush font and a small tracked caption under it. The stage ends (Stretch cleared,
    /// Fallen), a round, a wave and a rage art all use it. Everything is in the JS canvas's 1280 x 720 pixels, y down from the top-left
    /// corner; <see cref="HudInkBanner"/> draws it.
    /// </summary>
    public static class InkBanner
    {
        public const float Width = 1280f, Height = 720f;
        /// <summary>How long the banner stays (JS dur).</summary>
        public const float Duration = 1.55f;
        /// <summary>The band is painted over this many seconds (out cubic).</summary>
        public const float PaintTime = 0.32f;
        /// <summary>The fade starts at this share of the duration.</summary>
        public const float FadeFrom = 0.78f;
        /// <summary>The band's centre line (H * 0.4) and half height.</summary>
        public const float CenterY = Height * 0.4f, HalfHeight = 62f;
        public const float Step = 32f;

        /// <summary>The JS prng (30-combat.js): a small LCG seeded by <paramref name="seed"/>; the same seed gives the same ragged edges.</summary>
        public sealed class Prng
        {
            long _s;
            public Prng(int seed)
            {
                _s = ((long)seed * 9301L + 49297L) % 233280L;
                if (_s == 0) _s = 1;
            }
            public double Next() { _s = _s * 16807L % 2147483647L; return _s / 2147483647.0; }
        }

        /// <summary>How much of the band is painted, 0..1, <paramref name="t"/> seconds in (out cubic over <see cref="PaintTime"/>).</summary>
        public static float Painted(float t) => KMath.Apply(Ease.OutCubic, Mathf.Clamp01(t / PaintTime));

        /// <summary>0 until the fade starts, then 0..1 over the last 22 percent.</summary>
        public static float FadeOut(float t, float duration = Duration)
        {
            float k = t / Mathf.Max(0.001f, duration);
            return k > FadeFrom ? Mathf.Min(1f, (k - FadeFrom) / (1f - FadeFrom)) : 0f;
        }

        /// <summary>How far left the glyph and title still are while the band paints: they slide in 80 px (the title and caption at 60 and 40 percent of it).</summary>
        public static float Slide(float painted) => (1f - painted) * -80f;

        static readonly Dictionary<string, string> Glyphs = new Dictionary<string, string>
        {
            { "Round I", "壱" }, { "Round II", "弐" }, { "Round III", "参" }, { "Final Duel", "決" }, { "Sparring Match", "道" },
            { "Phase II", "弐" }, { "Phase III", "参" }, { "Raikon", "雷" }, { "Akari", "霧" }, { "Takashi", "父" }, { "Rem", "弟" },
            { "The Witness", "鏡" }, { "Hollow Raikon", "雷" }, { "Hollow Akari", "霧" }, { "Hollow Takashi", "父" }, { "Hollow Rem", "弟" },
            { "Shadow Palm", "影" }, { "Thousand Cuts", "千" }, { "Crescent Moon", "月" }, { "Mountain Split", "山" }, { "Dragon Lance", "龍" },
            { "Reaper's Harvest", "刈" }, { "Earthshatter", "震" }, { "Hollow Illusions", "幻" }, { "Stilled Time", "時" }, { "Ashen Wrath", "怒" },
        };

        /// <summary>The calligraphy for a title (JS bannerGlyph): the table's, 波 for a wave, else 闘 (fight).</summary>
        public static string GlyphFor(string title)
        {
            if (title != null && Glyphs.TryGetValue(title, out var g)) return g;
            if (title != null && title.StartsWith("Wave")) return "波";
            return "闘";
        }

        /// <summary>
        /// The ink band painted <paramref name="x1"/> far (JS drawBanner): the ragged top edge from <paramref name="left"/> to x1, the pointed
        /// leading end, the ragged bottom edge back. <paramref name="left"/> is where the band starts (0 on a 16:9 screen, further left on a
        /// wider one).
        /// </summary>
        public static Vector2[] Band(float x1, int seed, float left = 0f)
        {
            var r = new Prng(seed);
            float cy = CenterY, hh = HalfHeight;
            var pts = new List<Vector2> { new Vector2(left, cy - hh) };
            const int cap = 200;
            for (int i = 0; i < cap; i++)
            {
                float x = left + Step * i;
                if (x > x1) break;
                pts.Add(new Vector2(x, cy - hh + ((float)r.Next() - 0.5f) * 12f + Mathf.Sin(x * 0.01f) * 6f));
            }
            pts.Add(new Vector2(x1 + 30f, cy - hh * 0.2f));
            pts.Add(new Vector2(x1, cy + hh * 0.9f));
            for (int i = 0; i < cap; i++)
            {
                float x = x1 - Step * i;
                if (x < left) break;
                pts.Add(new Vector2(x, cy + hh + ((float)r.Next() - 0.5f) * 14f - Mathf.Sin(x * 0.012f) * 5f));
            }
            return pts.ToArray();
        }

        /// <summary>A tapered brush stroke from (x0, y0) to (x1, y1) of width <paramref name="w"/> (JS brushStroke, fully painted, no curve).</summary>
        public static Vector2[] Stroke(float x0, float y0, float x1, float y1, float w, int seed)
        {
            var r = new Prng(seed != 0 ? seed : 7);
            float dx = x1 - x0, dy = y1 - y0, len = Mathf.Max(0.0001f, Mathf.Sqrt(dx * dx + dy * dy)), nx = -dy / len, ny = dx / len;
            const int n = 16;
            var top = new Vector2[n + 1];
            var bot = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float px = x0 + dx * t, py = y0 + dy * t;
                // in double, as the JS: sin(pi) in float comes out a hair below zero and a negative base has no fractional power
                float ww = w * (float)System.Math.Pow(System.Math.Max(0.0, System.Math.Sin(System.Math.PI * System.Math.Min(1.0, t * 0.92 + 0.08))), 0.55) * (t > 0.85f ? 1f - (t - 0.85f) * 3f : 1f);
                float a = (float)r.Next(), b = (float)r.Next();
                top[i] = new Vector2(px + nx * ww * (0.5f + a * 0.08f), py + ny * ww * (0.5f + b * 0.08f));
                a = (float)r.Next(); b = (float)r.Next();
                bot[i] = new Vector2(px - nx * ww * (0.5f + a * 0.08f), py - ny * ww * (0.5f + b * 0.08f));
            }
            var pts = new Vector2[(n + 1) * 2];
            for (int i = 0; i <= n; i++) { pts[i] = top[i]; pts[n + 1 + i] = bot[n - i]; }
            return pts;
        }

        /// <summary>A seed from the words, so a banner is ragged the same way every time it is shown.</summary>
        public static int SeedOf(string title)
        {
            unchecked
            {
                int h = 17;
                if (title != null) foreach (char c in title) h = h * 31 + c;
                return (h & 0x7fffffff) % 1000;
            }
        }
    }
}
