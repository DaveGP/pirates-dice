using System.Collections.Generic;

namespace PiratesDice.Core
{
    /// <summary>Everything that decides whether a bid is legal at this moment.</summary>
    public readonly struct BidContext
    {
        /// <summary>1s count as any face right now (wild 1s on and not a Palifico round).</summary>
        public readonly bool OnesWild;
        public readonly int TotalDice;
        public readonly bool PalificoRound;
        /// <summary>In a Palifico round, a pirate with one die may change the face.</summary>
        public readonly bool BidderMayChangeFace;

        public BidContext(bool onesWild, int totalDice, bool palificoRound = false, bool bidderMayChangeFace = true)
        {
            OnesWild = onesWild;
            TotalDice = totalDice;
            PalificoRound = palificoRound;
            BidderMayChangeFace = bidderMayChangeFace;
        }
    }

    public static class BidRules
    {
        public static bool IsValid(Bid? current, Bid next, BidContext ctx)
        {
            if (next.Face < 1 || next.Face > RuleSettings.Faces) return false;
            if (next.Quantity < 1 || next.Quantity > ctx.TotalDice) return false;

            if (current == null)
                return !(ctx.OnesWild && next.Face == 1);

            Bid cur = current.Value;

            if (ctx.PalificoRound && !ctx.BidderMayChangeFace)
                return next.Face == cur.Face && next.Quantity > cur.Quantity;

            if (!ctx.OnesWild)
                return next.Quantity > cur.Quantity || (next.Quantity == cur.Quantity && next.Face > cur.Face);

            bool curOnes = cur.Face == 1;
            bool nextOnes = next.Face == 1;

            if (!curOnes && !nextOnes)
                return next.Quantity > cur.Quantity || (next.Quantity == cur.Quantity && next.Face > cur.Face);
            if (!curOnes && nextOnes)
                return next.Quantity >= (cur.Quantity + 1) / 2;
            if (curOnes && nextOnes)
                return next.Quantity > cur.Quantity;
            return next.Quantity >= cur.Quantity * 2 + 1;
        }

        /// <summary>The smallest legal quantity for a bid on this face, or 0 if none fits under the dice on the table.</summary>
        public static int MinimumQuantity(Bid? current, int face, BidContext ctx)
        {
            for (int q = 1; q <= ctx.TotalDice; q++)
                if (IsValid(current, new Bid(q, face), ctx)) return q;
            return 0;
        }

        /// <summary>How many of these dice count toward a bid on this face.</summary>
        public static int CountMatches(IEnumerable<int> dice, int face, bool onesWild)
        {
            int count = 0;
            foreach (int d in dice)
                if (d == face || (onesWild && d == 1 && face != 1)) count++;
            return count;
        }
    }
}
