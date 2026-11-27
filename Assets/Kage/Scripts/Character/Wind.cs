using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Wind for the soft parts (SAN-76): a steady world wind (m/s) with gusts on top, the same every run for the same seed.
    /// <see cref="SoftChains"/> add it to the air their particles are dragged towards (on top of each part's own breeze behind the
    /// fighter), so a part bends with it as strongly as its <see cref="ChainFeel.drag"/> lets it. <see cref="Calm"/> (the default) is
    /// no wind at all: a calm chain runs exactly the code it ran before wind existed, so no character changes unless something sets one
    /// (<see cref="FighterRig.wind"/>).
    /// <para>
    /// Gusts are smooth value noise over time (<see cref="gustRate"/> gusts a second, two octaves) that travel along the wind with the
    /// x position, so a long cape's chains are not all hit at once; they push along the wind (0 to <see cref="gust"/> m/s) with a little
    /// sideways wobble (a third of that either way). No random numbers are drawn: <see cref="At"/> is a pure function of the seed, the
    /// time and x.
    /// </para>
    /// </summary>
    [System.Serializable]
    public struct Wind
    {
        /// <summary>The steady wind, m/s in world space (+x right, +y up).</summary>
        public Vector2 steady;
        /// <summary>The strongest gust on top of the steady wind, m/s.</summary>
        public float gust;
        /// <summary>About how many gusts a second (default 0.6).</summary>
        public float gustRate;
        public int seed;

        public static Wind Calm => default;

        public bool IsCalm => steady.x == 0f && steady.y == 0f && gust == 0f;

        public static Wind Blowing(Vector2 steady, float gust = 0f, int seed = 1, float gustRate = 0.6f) =>
            new() { steady = steady, gust = gust, seed = seed, gustRate = gustRate };

        /// <summary>The wind at a time (seconds) and world x.</summary>
        public Vector2 At(float time, float x = 0f)
        {
            if (IsCalm) return Vector2.zero;
            Vector2 dir = steady.sqrMagnitude > 1e-8f ? steady.normalized : Vector2.right;
            if (gust == 0f) return steady;
            float rate = gustRate > 0f ? gustRate : 0.6f;
            float u = time * (rate * 0.42f) - x * 0.2f * Mathf.Sign(dir.x == 0f ? 1f : dir.x);
            float g = Noise(seed, u) * 0.82f + Noise(seed + 7919, u * 1.45f + 11.1f) * 0.18f;     // 0..1
            float side = Noise(seed + 104729, u * 0.65f + 3.3f) - 0.5f;                            // -0.5..0.5
            return steady + dir * (gust * g) + new Vector2(-dir.y, dir.x) * (gust * 0.18f * side);
        }

        /// <summary>C2-smooth value noise in 0..1 (quintic fade prevents sudden acceleration jerks between gusts).</summary>
        static float Noise(int seed, float u)
        {
            float fl = Mathf.Floor(u);
            int i = (int)fl;
            float f = u - fl;
            f = f * f * f * (f * (f * 6f - 15f) + 10f);
            float a = Hash(seed, i), b = Hash(seed, i + 1);
            return a + (b - a) * f;
        }

        static float Hash(int seed, int i)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)i * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du;
                h ^= h >> 12; h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
