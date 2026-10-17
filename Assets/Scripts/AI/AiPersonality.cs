using System;

namespace PiratesDice.AI
{
    public enum AiDifficulty { Easy, Normal, Hard }

    /// <summary>How an AI pirate plays. All values are 0 to 1.</summary>
    public sealed class AiPersonality
    {
        public string Name = "Pirate";
        /// <summary>Willingness to make risky raises instead of calling.</summary>
        public double Boldness = 0.5;
        /// <summary>Chance to bid a face it doesn't hold, to mislead the table.</summary>
        public double BluffRate = 0.15;
        /// <summary>Higher means Spot on is called less often.</summary>
        public double Caution = 0.5;
        /// <summary>Randomness added to every judgement; easy pirates make more mistakes.</summary>
        public double Noise = 0.1;
        /// <summary>Treat other pirates' bids as hints about what is under their cups.</summary>
        public bool ReadsTheTable = true;

        public AiPersonality Clone() => (AiPersonality)MemberwiseClone();

        /// <summary>Applies a difficulty on top of a captain's character.</summary>
        public AiPersonality WithDifficulty(AiDifficulty difficulty)
        {
            AiPersonality p = Clone();
            switch (difficulty)
            {
                case AiDifficulty.Easy:
                    p.Noise = 0.35;
                    p.ReadsTheTable = false;
                    p.BluffRate = Math.Min(1, p.BluffRate + 0.1);
                    break;
                case AiDifficulty.Normal:
                    p.Noise = 0.12;
                    p.ReadsTheTable = true;
                    break;
                case AiDifficulty.Hard:
                    p.Noise = 0.03;
                    p.ReadsTheTable = true;
                    break;
            }
            return p;
        }
    }

    /// <summary>The named crew who fill empty seats.</summary>
    public static class Captains
    {
        public static readonly AiPersonality[] All =
        {
            new AiPersonality { Name = "Calico Jane", Boldness = 0.7, BluffRate = 0.25, Caution = 0.4 },
            new AiPersonality { Name = "One-Eyed Morgan", Boldness = 0.35, BluffRate = 0.08, Caution = 0.7 },
            new AiPersonality { Name = "Barnacle Bess", Boldness = 0.55, BluffRate = 0.15, Caution = 0.5 },
            new AiPersonality { Name = "Salty Pete", Boldness = 0.85, BluffRate = 0.3, Caution = 0.3 },
            new AiPersonality { Name = "Mad Maggie Flint", Boldness = 0.6, BluffRate = 0.2, Caution = 0.2 },
            new AiPersonality { Name = "Old Tom Rumbelly", Boldness = 0.25, BluffRate = 0.05, Caution = 0.8 },
        };
    }
}
