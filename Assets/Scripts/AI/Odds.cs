using System;

namespace PiratesDice.AI
{
    /// <summary>Binomial odds for dice nobody has seen yet.</summary>
    public static class Odds
    {
        /// <summary>Chance that exactly k of n dice match, each matching with probability p.</summary>
        public static double Exactly(int n, int k, double p)
        {
            if (k < 0 || k > n) return 0;
            return Choose(n, k) * Math.Pow(p, k) * Math.Pow(1 - p, n - k);
        }

        /// <summary>Chance that at least k of n dice match, each matching with probability p.</summary>
        public static double AtLeast(int n, int k, double p)
        {
            if (k <= 0) return 1;
            if (k > n) return 0;
            double total = 0;
            for (int i = k; i <= n; i++) total += Exactly(n, i, p);
            return Math.Min(1, total);
        }

        static double Choose(int n, int k)
        {
            if (k > n - k) k = n - k;
            double result = 1;
            for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
            return result;
        }
    }
}
