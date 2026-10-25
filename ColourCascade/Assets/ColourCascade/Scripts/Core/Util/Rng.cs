using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Deterministic pseudo-random generator (xoshiro128**), seeded through SplitMix64.
    /// Uses integer arithmetic only, so the same seed produces the same sequence on every
    /// device and runtime. Daily and weekly challenges rely on this.
    /// </summary>
    public sealed class Rng
    {
        uint s0, s1, s2, s3;

        public Rng(ulong seed)
        {
            Reseed(seed);
        }

        public void Reseed(ulong seed)
        {
            ulong x = seed;
            ulong a = SplitMix(ref x);
            ulong b = SplitMix(ref x);
            s0 = (uint)a;
            s1 = (uint)(a >> 32);
            s2 = (uint)b;
            s3 = (uint)(b >> 32);
            if ((s0 | s1 | s2 | s3) == 0) s0 = 0x9E3779B9u;
        }

        static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static uint Rotl(uint v, int k)
        {
            return (v << k) | (v >> (32 - k));
        }

        public uint NextUInt()
        {
            uint result = Rotl(s1 * 5u, 7) * 9u;
            uint t = s1 << 9;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = Rotl(s3, 11);
            return result;
        }

        public ulong NextULong()
        {
            return ((ulong)NextUInt() << 32) | NextUInt();
        }

        /// <summary>Uniform integer in [0, maxExclusive). Unbiased (rejection sampling).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            uint bound = (uint)maxExclusive;
            uint threshold = (0u - bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>True with probability permille / 1000.</summary>
        public bool ChancePermille(int permille)
        {
            if (permille <= 0) return false;
            if (permille >= 1000) return true;
            return Next(1000) < permille;
        }

        /// <summary>Float in [0,1). Presentation use only: never feed into deterministic game state.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>Creates an independent generator derived from this one plus a salt.</summary>
        public Rng Fork(ulong salt)
        {
            return new Rng(NextULong() ^ StableHash.Mix(salt));
        }
    }

    /// <summary>Stable (platform independent) hashing used to derive seeds from keys such as dates.</summary>
    public static class StableHash
    {
        const ulong FnvOffset = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        public static ulong Of(string text)
        {
            ulong h = FnvOffset;
            if (text == null) return h;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                h ^= (byte)(c & 0xFF);
                h *= FnvPrime;
                h ^= (byte)(c >> 8);
                h *= FnvPrime;
            }
            return Mix(h);
        }

        public static ulong Of(string a, int b)
        {
            return Combine(Of(a), (ulong)(uint)b);
        }

        public static ulong Combine(ulong a, ulong b)
        {
            return Mix(a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2)));
        }

        public static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
