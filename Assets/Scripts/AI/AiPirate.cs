using System;
using System.Collections.Generic;
using PiratesDice.Core;

namespace PiratesDice.AI
{
    /// <summary>
    /// Decides a move from a <see cref="PlayerView"/>: estimates how likely each bid is from its own dice plus
    /// binomial odds for the hidden ones, then weighs raising, Liar and Spot on by the risk of losing a die.
    /// </summary>
    public sealed class AiPirate
    {
        /// <summary>Expected matching dice we credit to another pirate for each face they have bid.</summary>
        const double TableReadBonus = 0.6;

        public AiPersonality Personality { get; }
        readonly IRandomSource _rng;

        public AiPirate(AiPersonality personality, IRandomSource rng)
        {
            Personality = personality ?? throw new ArgumentNullException(nameof(personality));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public SeatDecision Decide(PlayerView view)
        {
            if (view.CurrentBid == null) return SeatDecision.Raise(ChooseOpening(view));

            double pTrue = ChanceTrue(view, view.CurrentBid.Value);
            double liarRisk = pTrue + Jitter();

            Bid? raise = ChooseRaise(view, out double raiseChance);
            double raiseRisk = raise == null
                ? double.MaxValue
                : (1 - raiseChance) * (1 - 0.4 * Personality.Boldness) + Jitter();

            double spotRisk = double.MaxValue;
            if (view.CanCallSpotOn)
            {
                double pExact = ChanceExact(view, view.CurrentBid.Value);
                bool canGain = view.MyDice.Length < view.Rules.StartingDice;
                double threshold = 0.25 + 0.3 * Personality.Caution;
                if (pExact >= threshold)
                    spotRisk = (canGain ? 1 - 2 * pExact : 1 - pExact) + Jitter();
            }

            if (spotRisk < liarRisk && spotRisk < raiseRisk) return SeatDecision.SpotOn();
            if (raise != null && raiseRisk <= liarRisk) return SeatDecision.Raise(raise.Value);
            return SeatDecision.Liar();
        }

        /// <summary>Probability that the bid is true given this seat's knowledge.</summary>
        public double ChanceTrue(PlayerView view, Bid bid)
        {
            int need = NeededFromHidden(view, bid);
            return Odds.AtLeast(view.UnknownDice, need, MatchChance(view, bid.Face));
        }

        public double ChanceExact(PlayerView view, Bid bid)
        {
            int known = BidRules.CountMatches(view.MyDice, bid.Face, view.OnesWild);
            return Odds.Exactly(view.UnknownDice, bid.Quantity - known, MatchChance(view, bid.Face));
        }

        Bid ChooseOpening(PlayerView view)
        {
            int face = BestFace(view);
            if (_rng.NextDouble() < Personality.BluffRate)
                face = RandomFace(view);

            double threshold = 0.7 - 0.3 * Personality.Boldness;
            int min = Math.Max(1, view.MinimumQuantity(face));
            int quantity = min;
            for (int q = min; q <= view.TotalDice; q++)
            {
                if (ChanceTrue(view, new Bid(q, face)) + Jitter() < threshold) break;
                quantity = q;
            }
            return new Bid(quantity, face);
        }

        Bid? ChooseRaise(PlayerView view, out double chance)
        {
            var options = new List<(Bid bid, double chance)>();
            for (int face = 1; face <= RuleSettings.Faces; face++)
            {
                int q = view.MinimumQuantity(face);
                if (q == 0) continue;
                var bid = new Bid(q, face);
                options.Add((bid, ChanceTrue(view, bid)));
            }

            if (options.Count == 0)
            {
                chance = 0;
                return null;
            }

            (Bid bid, double chance) best = options[0];
            double bestScore = double.MinValue;
            foreach (var option in options)
            {
                double score = option.chance + Jitter();
                if (score > bestScore)
                {
                    bestScore = score;
                    best = option;
                }
            }

            if (_rng.NextDouble() < Personality.BluffRate)
            {
                var bluffs = options.FindAll(o => o.chance >= 0.25 && o.bid != best.bid);
                if (bluffs.Count > 0) best = bluffs[_rng.Next(0, bluffs.Count)];
            }

            chance = best.chance;
            return best.bid;
        }

        int BestFace(PlayerView view)
        {
            int bestFace = view.OnesWild ? 2 : 1;
            double bestScore = double.MinValue;
            for (int face = 1; face <= RuleSettings.Faces; face++)
            {
                if (view.MinimumQuantity(face) == 0) continue;
                double score = BidRules.CountMatches(view.MyDice, face, view.OnesWild) + face * 0.01 + Jitter();
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFace = face;
                }
            }
            return bestFace;
        }

        int RandomFace(PlayerView view)
        {
            var faces = new List<int>();
            for (int face = 1; face <= RuleSettings.Faces; face++)
                if (view.MinimumQuantity(face) > 0) faces.Add(face);
            return faces.Count == 0 ? BestFace(view) : faces[_rng.Next(0, faces.Count)];
        }

        int NeededFromHidden(PlayerView view, Bid bid)
        {
            int known = BidRules.CountMatches(view.MyDice, bid.Face, view.OnesWild);
            double reads = Personality.ReadsTheTable ? TableReads(view, bid.Face) : 0;
            return (int)Math.Ceiling(bid.Quantity - known - reads);
        }

        double TableReads(PlayerView view, int face)
        {
            var seats = new HashSet<int>();
            foreach (BidRecord record in view.BidHistory)
            {
                if (record.Seat == view.Seat) continue;
                bool hints = record.Bid.Face == face || (view.OnesWild && record.Bid.Face == 1);
                if (hints) seats.Add(record.Seat);
            }
            return Math.Min(seats.Count * TableReadBonus, view.UnknownDice);
        }

        static double MatchChance(PlayerView view, int face) =>
            view.OnesWild && face != 1 ? 2.0 / RuleSettings.Faces : 1.0 / RuleSettings.Faces;

        double Jitter() => (_rng.NextDouble() - 0.5) * 2 * Personality.Noise;
    }
}
