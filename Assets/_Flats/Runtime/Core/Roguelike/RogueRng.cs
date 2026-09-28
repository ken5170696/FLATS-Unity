using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Deterministic xorshift64* generator for run seeding. The authority owns one root
    /// generator per run and derives named sub-streams so that shop, director and event
    /// sampling do not perturb each other. State round-trips through the save file.
    /// </summary>
    public sealed class RogueRng
    {
        private ulong state;
        public readonly ulong Seed;

        public RogueRng(ulong seed)
        {
            Seed = seed;
            state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public long State
        {
            get { return unchecked((long)state); }
            set { state = unchecked((ulong)value); if (state == 0) state = 1; }
        }

        public ulong NextULong()
        {
            ulong x = state;
            x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
            state = x;
            return unchecked(x * 0x2545F4914F6CDD1DUL);
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException("maxExclusive");
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        }

        public double Range(double min, double max)
        {
            return min + (max - min) * NextDouble();
        }

        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }

        /// <summary>Derives an independent stream from the root seed and a name; stable across sessions.</summary>
        public RogueRng Derive(string salt)
        {
            return new RogueRng(Hash(Seed, salt));
        }

        public RogueRng Derive(string salt, long index)
        {
            return new RogueRng(Hash(Hash(Seed, salt), index.ToString()));
        }

        public static ulong Hash(ulong seed, string salt)
        {
            ulong h = 14695981039346656037UL ^ seed;
            if (salt != null)
                foreach (char c in salt)
                {
                    h ^= c;
                    h = unchecked(h * 1099511628211UL);
                }
            h ^= h >> 33; h = unchecked(h * 0xff51afd7ed558ccdUL); h ^= h >> 33;
            return h == 0 ? 1 : h;
        }

        public T Pick<T>(IList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Cannot pick from an empty list");
            return items[Next(items.Count)];
        }

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = items[i]; items[i] = items[j]; items[j] = tmp;
            }
        }

        /// <summary>Index proportional to weight; non-positive weights are never chosen. Returns -1 when nothing is selectable.</summary>
        public int WeightedIndex(IList<double> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) if (weights[i] > 0) total += weights[i];
            if (total <= 0) return -1;
            double roll = NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                roll -= weights[i];
                if (roll < 0) return i;
            }
            for (int i = weights.Count - 1; i >= 0; i--) if (weights[i] > 0) return i;
            return -1;
        }
    }
}
