using System;
using NUnit.Framework;
using PiratesDice.AI;
using PiratesDice.Core;

namespace PiratesDice.Tests
{
    public class AiPirateTests
    {
        static AiPirate HardPirate(int seed = 1) =>
            new AiPirate(Captains.All[2].WithDifficulty(AiDifficulty.Hard), new SeededRandom(seed));

        [Test]
        public void OddsAreSaneBinomials()
        {
            Assert.That(Odds.AtLeast(10, 0, 1.0 / 3), Is.EqualTo(1));
            Assert.That(Odds.AtLeast(3, 4, 0.5), Is.EqualTo(0));
            Assert.That(Odds.Exactly(2, 1, 0.5), Is.EqualTo(0.5).Within(1e-9));
            double sum = 0;
            for (int k = 0; k <= 12; k++) sum += Odds.Exactly(12, k, 1.0 / 6);
            Assert.That(sum, Is.EqualTo(1).Within(1e-9));
            Assert.That(Odds.AtLeast(12, 4, 1.0 / 3), Is.EqualTo(0.6069).Within(1e-3));
        }

        [Test]
        public void CallsLiarOnAnAlmostImpossibleBid()
        {
            // Seat 1 holds no 6s and no 1s, and seat 0 claims eight 6s out of ten dice.
            var engine = new GameEngine(2, new RuleSettings(), new ScriptedRandom(2, 2, 3, 3, 4, 2, 3, 4, 5, 5));
            engine.StartRound();
            engine.TryBid(0, new Bid(8, 6), out _);

            SeatDecision decision = HardPirate().Decide(engine.ViewFor(1));
            Assert.That(decision.Kind, Is.EqualTo(DecisionKind.Liar));
        }

        [Test]
        public void RaisesWhenItsOwnDiceProveTheBid()
        {
            // Seat 1 holds five 2s, so "two 2s" is certainly true.
            var engine = new GameEngine(2, new RuleSettings(), new ScriptedRandom(3, 4, 5, 6, 6, 2, 2, 2, 2, 2));
            engine.StartRound();
            engine.TryBid(0, new Bid(2, 2), out _);

            SeatDecision decision = HardPirate().Decide(engine.ViewFor(1));
            Assert.That(decision.Kind, Is.EqualTo(DecisionKind.Bid));
            Assert.That(engine.IsValidBid(1, decision.Bid), Is.True);
        }

        [Test]
        public void OpensWithItsStrongestFace()
        {
            var engine = new GameEngine(2, new RuleSettings(), new ScriptedRandom(5, 5, 5, 2, 3, 2, 3, 4, 6, 6));
            engine.StartRound();
            var pirate = new AiPirate(new AiPersonality { BluffRate = 0, Noise = 0 }, new SeededRandom(3));

            SeatDecision decision = pirate.Decide(engine.ViewFor(0));
            Assert.That(decision.Kind, Is.EqualTo(DecisionKind.Bid));
            Assert.That(decision.Bid.Face, Is.EqualTo(5));
            Assert.That(decision.Bid.Quantity, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ChanceTrueCountsWildOnesInItsOwnHand()
        {
            var engine = new GameEngine(2, new RuleSettings(), new ScriptedRandom(1, 1, 4, 4, 6, 2, 2, 2, 2, 2));
            engine.StartRound();
            PlayerView view = engine.ViewFor(0);
            var pirate = new AiPirate(new AiPersonality { ReadsTheTable = false }, new SeededRandom(1));
            Assert.That(pirate.ChanceTrue(view, new Bid(4, 4)), Is.EqualTo(1));
            Assert.That(pirate.ChanceTrue(view, new Bid(5, 4)), Is.EqualTo(Odds.AtLeast(5, 1, 1.0 / 3)).Within(1e-9));
        }

        [TestCase(AiDifficulty.Easy)]
        [TestCase(AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard)]
        public void AllAiTablesPlayToAWinnerWithOnlyLegalMoves(AiDifficulty difficulty)
        {
            for (int game = 0; game < 40; game++)
            {
                int seats = 2 + game % 5;
                var rules = new RuleSettings { WildOnes = game % 3 != 0, SpotOn = game % 2 == 0, Palifico = game % 4 == 1 };
                PlayFullGame(seats, rules, difficulty, seed: 1000 + game);
            }
        }

        static void PlayFullGame(int seats, RuleSettings rules, AiDifficulty difficulty, int seed)
        {
            var rng = new SeededRandom(seed);
            var engine = new GameEngine(seats, rules, rng, firstSeat: seed % seats);
            var pirates = new AiPirate[seats];
            for (int i = 0; i < seats; i++)
                pirates[i] = new AiPirate(Captains.All[i].WithDifficulty(difficulty), new SeededRandom(seed * 31 + i));

            int moves = 0;
            while (engine.Phase != TablePhase.GameOver)
            {
                engine.StartRound();
                while (engine.Phase == TablePhase.Bidding)
                {
                    int seat = engine.CurrentSeat;
                    SeatDecision decision = pirates[seat].Decide(engine.ViewFor(seat));
                    Assert.DoesNotThrow(() => engine.Apply(seat, decision), $"seed {seed}: seat {seat} chose {decision}");
                    if (++moves > 5000) Assert.Fail($"seed {seed}: game did not finish");
                }
            }

            Assert.That(engine.Winner, Is.InRange(0, seats - 1));
            Assert.That(engine.ActiveSeatCount, Is.EqualTo(1));
        }
    }
}
