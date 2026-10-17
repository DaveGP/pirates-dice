namespace PiratesDice.Core
{
    /// <summary>Table rules. Defaults are the classic Perudo-style game.</summary>
    public sealed class RuleSettings
    {
        public const int MinSeats = 2;
        public const int MaxSeats = 6;
        public const int Faces = 6;

        /// <summary>Dice each pirate starts with, and the most they can hold.</summary>
        public int StartingDice = 5;

        /// <summary>1s count as any face (except in a Palifico round, or when bidding on 1s).</summary>
        public bool WildOnes = true;

        /// <summary>Players may call "Spot on!" to claim the bid is exactly right.</summary>
        public bool SpotOn = true;

        /// <summary>When a pirate first drops to one die, the next round has no wild 1s and a locked face.</summary>
        public bool Palifico = false;

        public RuleSettings Clone() => (RuleSettings)MemberwiseClone();
    }
}
