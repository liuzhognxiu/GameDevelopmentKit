using System;

namespace Game.Hot.Buqi.Run.Core
{
    public static class BuqiRunRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplier2 = 0x94D049BB133111EBUL;
        private const ulong AttemptSalt = 0xD1342543DE82EF95UL;

        public static int Next(long seed, ref int cursor, int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive,
                    "maxExclusive must be positive.");
            }

            uint range = (uint)maxExclusive;
            uint originalCursor = unchecked((uint)cursor);
            uint threshold = unchecked(0u - range) % range;
            uint attempt = 0u;

            while (true)
            {
                uint sample = NextUInt32(seed, originalCursor, attempt);
                ulong product = (ulong)sample * range;
                uint low = (uint)product;
                if (low < threshold)
                {
                    attempt++;
                    continue;
                }

                cursor = unchecked((int)(originalCursor + 1u));
                return (int)(product >> 32);
            }
        }

        private static uint NextUInt32(long seed, uint callIndex, uint attempt)
        {
            ulong state = unchecked((ulong)seed + ((ulong)callIndex * Gamma) + ((ulong)attempt * AttemptSalt));
            ulong mixed = Mix(state);
            return (uint)(mixed >> 32);
        }

        private static ulong Mix(ulong state)
        {
            ulong value = unchecked(state + Gamma);
            value = unchecked((value ^ (value >> 30)) * MixMultiplier1);
            value = unchecked((value ^ (value >> 27)) * MixMultiplier2);
            return value ^ (value >> 31);
        }
    }
}
