using System.Collections.Generic;
using PiratesDice.Core;

namespace PiratesDice.Tests
{
    /// <summary>Returns the given rolls in order, so a test can set exactly what is under every cup.</summary>
    sealed class ScriptedRandom : IRandomSource
    {
        readonly Queue<int> _rolls;

        public ScriptedRandom(params int[] rolls) { _rolls = new Queue<int>(rolls); }

        public void Add(params int[] rolls)
        {
            foreach (int r in rolls) _rolls.Enqueue(r);
        }

        public int Next(int minInclusive, int maxExclusive) => _rolls.Count > 0 ? _rolls.Dequeue() : minInclusive;
        public double NextDouble() => 0.5;
    }
}
