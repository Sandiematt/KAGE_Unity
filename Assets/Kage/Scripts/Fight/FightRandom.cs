using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// The fight's seeded random source (ADR-0002). Our own generator (SplitMix64), so a seed gives
    /// the same numbers on every platform and Unity version. Every gameplay roll goes through a named
    /// stream (<c>"crit"</c>, <c>"ai/&lt;FighterId&gt;"</c>, <c>"spawn"</c>, <c>"boss/&lt;FighterId&gt;"</c>), so adding a
    /// roll to one stream never shifts another. Cosmetic randomness stays on UnityEngine.Random.
    /// </summary>
    public sealed class FightRandom
    {
        readonly ulong _seed;
        ulong _state;
        Dictionary<string, FightRandom> _streams;

        public FightRandom(int seed) : this((ulong)(uint)seed) { }

        FightRandom(ulong seed)
        {
            _seed = seed;
            _state = seed;
        }

        /// <summary>
        /// The independent generator for (seed, name). The same name always returns the same
        /// instance, which keeps its position; it doesn't depend on how many values were drawn elsewhere.
        /// </summary>
        public FightRandom Stream(string name)
        {
            _streams ??= new Dictionary<string, FightRandom>();
            if (_streams.TryGetValue(name, out var s)) return s;
            s = new FightRandom(Mix(_seed ^ Fnv1a(name)));
            _streams.Add(name, s);
            return s;
        }

        /// <summary>A float in [0, 1).</summary>
        public float Value() => (Next() >> 40) * (1f / 16777216f);

        /// <summary>An int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + (long)(Next() % span));
        }

        /// <summary>A float in [min, max].</summary>
        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>True with probability p.</summary>
        public bool Chance(float p) => Value() < p;

        ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Mix(_state);
        }

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static ulong Fnv1a(string s)
        {
            ulong h = 0xCBF29CE484222325UL;
            foreach (char c in s)
            {
                h ^= c;
                h *= 0x100000001B3UL;
            }
            return h;
        }
    }
}
