using System.Collections.Generic;

namespace PiratesDice.Core
{
    /// <summary>One move in the tutorial, with what the coach says while it is that seat's turn.</summary>
    public sealed class TutorialMove
    {
        public readonly int Seat;
        public readonly SeatDecision Decision;
        public readonly string Coach;

        public TutorialMove(int seat, SeatDecision decision, string coach)
        {
            Seat = seat;
            Decision = decision;
            Coach = coach;
        }
    }

    public sealed class TutorialRound
    {
        /// <summary>Every die rolled this round, seat by seat in seat order.</summary>
        public int[] Rolls;
        public List<TutorialMove> Moves = new List<TutorialMove>();
        /// <summary>What the coach says once the cups are lifted.</summary>
        public string RevealCoach;
    }

    /// <summary>
    /// A fixed, three-round lesson against one AI pirate: calling Liar on a bluff, getting caught bluffing,
    /// and calling Spot on to win a die back. Dice and moves are scripted so every lesson lands.
    /// </summary>
    public sealed class TutorialScript
    {
        public const int Player = 0;
        public const int Opponent = 1;

        public string OpponentName = "Calico Jane";
        public int SeatCount = 2;
        public int FirstSeat = Player;
        public string[] Intro;
        public List<TutorialRound> Rounds = new List<TutorialRound>();
        public string Outro;

        public IRandomSource CreateDice()
        {
            var rolls = new List<int>();
            foreach (TutorialRound round in Rounds) rolls.AddRange(round.Rolls);
            return new FixedRolls(rolls);
        }

        public static TutorialScript Standard()
        {
            var script = new TutorialScript
            {
                Intro = new[]
                {
                    "Ahoy! Each pirate rolls five dice under a cup. You can see your own dice, but nobody else's.",
                    "Take turns bidding on how many of one face are under ALL the cups together, like \"three 4s\". 1s are wild: they count as any face.",
                    "Each bid must beat the last: more dice, or the same number of a higher face. Or call Liar! if you think the last bid is false.",
                },
                Outro = "That's Liar's Dice! Whoever is wrong loses a die, and the last pirate with dice wins the treasure.",
            };

            // Round 1: you open safely, Jane overbids, you call Liar.
            // You: 1 2 4 4 6. Jane: 2 3 3 5 6. Three 4s and three 6s on the table, counting your wild 1.
            var r1 = new TutorialRound { Rolls = new[] { 1, 2, 4, 4, 6, 2, 3, 3, 5, 6 } };
            r1.Moves.Add(new TutorialMove(Player, SeatDecision.Raise(new Bid(3, 4)),
                "You hold two 4s and a wild 1: that's three 4s already. Open with a safe bid: three 4s."));
            r1.Moves.Add(new TutorialMove(Opponent, SeatDecision.Raise(new Bid(4, 6)),
                "Calico Jane is thinking..."));
            r1.Moves.Add(new TutorialMove(Player, SeatDecision.Liar(),
                "Jane claims four 6s. You can see two of them: your 6 and your wild 1. She'd need two more under her cup. Smells like a bluff: call Liar!"));
            r1.RevealCoach = "Only three 6s! Jane was bluffing, so she loses a die. The loser opens the next round.";
            script.Rounds.Add(r1);

            // Round 2: Jane opens, you bluff, Jane catches you.
            // You: 2 3 4 5 6. Jane: 2 3 5 5.
            var r2 = new TutorialRound { Rolls = new[] { 6, 5, 4, 3, 2, 5, 5, 3, 2 } };
            r2.Moves.Add(new TutorialMove(Opponent, SeatDecision.Raise(new Bid(2, 5)),
                "Calico Jane opens the bidding..."));
            r2.Moves.Add(new TutorialMove(Player, SeatDecision.Raise(new Bid(3, 6)),
                "Pirates bluff all the time. You hold just one 6, but try bluffing three 6s and see if Jane believes you."));
            r2.Moves.Add(new TutorialMove(Opponent, SeatDecision.Liar(),
                "Calico Jane is weighing up your bid..."));
            r2.RevealCoach = "Caught! There was only one 6, so you lose a die. Bluff with care.";
            script.Rounds.Add(r2);

            // Round 3: you open, Jane bids exactly right, you call Spot on.
            // You: 1 3 3 4. Jane: 2 2 5 6. Exactly three 2s, counting your wild 1.
            var r3 = new TutorialRound { Rolls = new[] { 3, 1, 4, 3, 2, 6, 2, 5 } };
            r3.Moves.Add(new TutorialMove(Player, SeatDecision.Raise(new Bid(2, 3)),
                "You open this round. You hold two 3s and a wild 1, so bid two 3s."));
            r3.Moves.Add(new TutorialMove(Opponent, SeatDecision.Raise(new Bid(3, 2)),
                "Calico Jane is thinking..."));
            r3.Moves.Add(new TutorialMove(Player, SeatDecision.SpotOn(),
                "Jane bids three 2s. Your wild 1 makes one, and she's bid 2s with only four dice, so she likely holds two. If it's exactly three, call Spot on! to win back the die you lost."));
            r3.RevealCoach = "Exactly three 2s! Spot on wins you a die back. Get it wrong, though, and you lose one.";
            script.Rounds.Add(r3);

            return script;
        }

        sealed class FixedRolls : IRandomSource
        {
            readonly Queue<int> _rolls;
            public FixedRolls(IEnumerable<int> rolls) { _rolls = new Queue<int>(rolls); }
            public int Next(int minInclusive, int maxExclusive) => _rolls.Count > 0 ? _rolls.Dequeue() : minInclusive;
            public double NextDouble() => 0.5;
        }
    }
}
