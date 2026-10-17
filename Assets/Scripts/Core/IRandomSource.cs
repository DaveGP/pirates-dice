using System;

namespace PiratesDice.Core
{
    /// <summary>Random numbers behind an interface so games and tests can be replayed from a seed.</summary>
    public interface IRandomSource
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
        int Next(int minInclusive, int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }

    public sealed class SeededRandom : IRandomSource
    {
        readonly Random _random;

        public SeededRandom(int seed) { _random = new Random(seed); }
        public SeededRandom() { _random = new Random(); }

        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
        public double NextDouble() => _random.NextDouble();
    }
}
