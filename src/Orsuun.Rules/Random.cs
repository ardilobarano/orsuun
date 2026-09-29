#nullable enable
using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Source of randomness for every roll. The server plugs in a cryptographic generator,
    /// simulations and replays plug in a seeded one. Rules never touch System.Random or floats.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        int NextInt(int maxExclusive);
    }

    /// <summary>Deterministic xorshift64* generator: same seed, same sequence, on every platform.</summary>
    public sealed class XorShiftRandom : IRandom
    {
        private ulong _state;

        public XorShiftRandom(ulong seed)
        {
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>A generator in the same state: it draws what this one would draw next, without moving this one.</summary>
        public XorShiftRandom Copy() => new XorShiftRandom(1UL) { _state = _state };

        private ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return unchecked(_state * 0x2545F4914F6CDD1DUL);
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            // Rejection sampling removes modulo bias.
            ulong bound = (ulong)maxExclusive;
            ulong threshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                ulong r = NextULong();
                if (r >= threshold) return (int)(r % bound);
            }
        }
    }

    public static class RandomExtensions
    {
        /// <summary>All chances are expressed in basis points: 10000 = 100%.</summary>
        public const int FullBp = 10000;

        public static bool RollBp(this IRandom rng, int chanceBp) => rng.NextInt(FullBp) < chanceBp;
    }
}
