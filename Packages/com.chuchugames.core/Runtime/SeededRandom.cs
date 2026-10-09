using System;
using System.Collections.Generic;

namespace ChuchuGames.Core
{
    /// <summary>
    /// Deterministic, engine-free random numbers (xorshift128+ seeded through SplitMix64). The same seed
    /// gives the same sequence on every platform and .NET runtime, unlike System.Random or
    /// UnityEngine.Random, so generated levels, daily challenges and bug reports are reproducible.
    /// </summary>
    public sealed class SeededRandom
    {
        ulong _s0, _s1;

        public SeededRandom(ulong seed)
        {
            ulong x = seed;
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        public SeededRandom(int seed) : this((ulong)(uint)seed) { }

        /// <summary>A seed derived from text, e.g. a date for a daily challenge ("2026-10-09").</summary>
        public static ulong SeedFrom(string text)
        {
            // FNV-1a, 64-bit: stable across runs, unlike string.GetHashCode.
            ulong h = 14695981039346656037UL;
            foreach (char c in text ?? "")
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>A child generator whose sequence depends only on this one's seed and <paramref name="salt"/>.
        /// Use it to keep sub-systems independent (adding a call in one doesn't shift the other).</summary>
        public SeededRandom Fork(ulong salt) => new SeededRandom(NextULong() ^ (salt * 0x9E3779B97F4A7C15UL));

        public ulong NextULong()
        {
            ulong s1 = _s0, s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _s1 + s0;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Value() => (float)((NextULong() >> 40) * (1.0 / (1UL << 24)));

        /// <summary>Uniform integer in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value();

        public bool Chance(float probability) => Value() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Nothing to pick from", nameof(items));
            return items[Range(0, items.Count)];
        }

        /// <summary>Index chosen with probability proportional to its weight; -1 if all weights are 0.</summary>
        public int PickWeighted(IReadOnlyList<float> weights)
        {
            float total = 0;
            foreach (var w in weights) total += Math.Max(0, w);
            if (total <= 0) return -1;
            float r = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0, weights[i]);
                if (r < 0) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>Fisher–Yates shuffle in place.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        static ulong SplitMix(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
