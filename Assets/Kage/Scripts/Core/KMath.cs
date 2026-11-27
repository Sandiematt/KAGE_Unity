using UnityEngine;

namespace Kage
{
    public enum Ease { Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, OutBack, OutExpo, InExpo, Hold }

    /// <summary>Small math helpers shared by animation, physics feel and VFX.</summary>
    public static class KMath
    {
        public static float Apply(Ease e, float t)
        {
            t = Mathf.Clamp01(t);
            switch (e)
            {
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                case Ease.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case Ease.InExpo: return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case Ease.Hold: return t >= 1f ? 1f : 0f;
                default: return t;
            }
        }

        public static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static float Ang(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        public static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        /// <summary>Frame-rate independent exponential smoothing.</summary>
        public static float Damp(float current, float target, float lambda, float dt) =>
            Mathf.Lerp(target, current, Mathf.Exp(-lambda * dt));

        public static Vector2 Damp(Vector2 current, Vector2 target, float lambda, float dt) =>
            Vector2.Lerp(target, current, Mathf.Exp(-lambda * dt));

        public static float Wrap(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithA(this Color c, float a) { c.a = a; return c; }

        public static Color Mul(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        public static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f * 0.5f;
        }
    }

    /// <summary>Palette sampled from the KAGE reference art (kage.svg fills).</summary>
    public static class Palette
    {
        public static readonly Color Body = KMath.Hex("#110D0F");
        public static readonly Color BodyFar = KMath.Hex("#070506");
        public static readonly Color Trim = KMath.Hex("#D5444B");
        public static readonly Color TrimBright = KMath.Hex("#F06F6E");
        public static readonly Color TrimDeep = KMath.Hex("#731F23");
        public static readonly Color Rim = KMath.Hex("#E65C5E");
        public static readonly Color Steel = KMath.Hex("#B9BDC7");
        public static readonly Color SteelDark = KMath.Hex("#4A4C55");
        public static readonly Color Silver = KMath.Hex("#C9C4C4");
        public static readonly Color BootRim = KMath.Hex("#9A9496");
        public static readonly Color Eye = KMath.Hex("#F4F1EE");
        public static readonly Color Background = KMath.Hex("#1D181B");

        // Opponent (Ronin) accents
        public static readonly Color Amber = KMath.Hex("#E2A24A");
        public static readonly Color AmberDeep = KMath.Hex("#6E4A1C");
        public static readonly Color Straw = KMath.Hex("#1E1610");
    }
}
