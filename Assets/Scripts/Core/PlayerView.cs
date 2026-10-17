using System.Collections.Generic;
using System.Linq;

namespace PiratesDice.Core
{
    /// <summary>
    /// A snapshot of the table from one seat: its own dice and everything public, never anyone else's dice.
    /// This is all an AI pirate (or a remote client) gets to decide with.
    /// </summary>
    public sealed class PlayerView
    {
        public readonly int Seat;
        public readonly int SeatCount;
        public readonly int[] MyDice;
        /// <summary>Dice still held by each seat, indexed by seat.</summary>
        public readonly int[] DiceCounts;
        public readonly int TotalDice;
        public readonly Bid? CurrentBid;
        public readonly int CurrentBidder;
        public readonly IReadOnlyList<BidRecord> BidHistory;
        public readonly RuleSettings Rules;
        public readonly BidContext Context;
        public readonly bool IsPalificoRound;
        public readonly bool CanCallLiar;
        public readonly bool CanCallSpotOn;
        public readonly int RoundNumber;

        public int ActiveSeatCount => DiceCounts.Count(c => c > 0);
        public int UnknownDice => TotalDice - MyDice.Length;
        public bool OnesWild => Context.OnesWild;

        internal PlayerView(GameEngine engine, int seat)
        {
            Seat = seat;
            SeatCount = engine.SeatCount;
            MyDice = engine.Seats[seat].Dice.ToArray();
            DiceCounts = engine.Seats.Select(s => s.DiceCount).ToArray();
            TotalDice = engine.TotalDice;
            CurrentBid = engine.CurrentBid;
            CurrentBidder = engine.CurrentBidder;
            BidHistory = engine.BidHistory.ToArray();
            Rules = engine.Rules.Clone();
            Context = engine.ContextFor(seat);
            IsPalificoRound = engine.IsPalificoRound;
            CanCallLiar = engine.CanCallLiar(seat);
            CanCallSpotOn = engine.CanCallSpotOn(seat);
            RoundNumber = engine.RoundNumber;
        }

        public bool IsValidBid(Bid bid) => BidRules.IsValid(CurrentBid, bid, Context);

        public int MinimumQuantity(int face) => BidRules.MinimumQuantity(CurrentBid, face, Context);
    }
}
