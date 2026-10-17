using System;
using NUnit.Framework;
using PiratesDice.Core;

namespace PiratesDice.Tests
{
    public class GameEngineTests
    {
        // Seat 0 holds 1 2 2 3 6 and seat 1 holds 2 4 5 5 6, so with wild 1s there are four 2s on the table.
        static GameEngine TwoSeatGame(ScriptedRandom rng = null, RuleSettings rules = null)
        {
            rng = rng ?? new ScriptedRandom(2, 2, 3, 1, 6, 2, 4, 5, 5, 6);
            var engine = new GameEngine(2, rules ?? new RuleSettings(), rng);
            engine.StartRound();
            return engine;
        }

        [Test]
        public void StartRoundRollsEveryCupAndOpensBidding()
        {
            GameEngine engine = TwoSeatGame();
            Assert.That(engine.Phase, Is.EqualTo(TablePhase.Bidding));
            Assert.That(engine.CurrentSeat, Is.EqualTo(0));
            Assert.That(engine.Seats[0].Dice, Is.EqualTo(new[] { 1, 2, 2, 3, 6 }));
            Assert.That(engine.Seats[1].Dice, Is.EqualTo(new[] { 2, 4, 5, 5, 6 }));
            Assert.That(engine.TotalDice, Is.EqualTo(10));
            Assert.That(engine.CountOnTable(2), Is.EqualTo(4));
        }

        [Test]
        public void BidsMustBeOnYourTurnAndBeatTheLastBid()
        {
            GameEngine engine = TwoSeatGame();
            Assert.That(engine.TryBid(1, new Bid(2, 3), out _), Is.False, "not seat 1's turn");
            Assert.That(engine.TryBid(0, new Bid(2, 3), out _), Is.True);
            Assert.That(engine.CurrentSeat, Is.EqualTo(1));
            Assert.That(engine.TryBid(1, new Bid(2, 2), out string error), Is.False);
            Assert.That(error, Is.Not.Null);
            Assert.That(engine.BidHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public void LiarCannotBeCalledBeforeAnyBid()
        {
            GameEngine engine = TwoSeatGame();
            Assert.That(engine.CanCallLiar(0), Is.False);
            Assert.Throws<InvalidOperationException>(() => engine.CallLiar(0));
        }

        [Test]
        public void CorrectLiarCallCostsTheBidderADie()
        {
            GameEngine engine = TwoSeatGame();
            engine.TryBid(0, new Bid(5, 2), out _);
            RoundResult result = engine.CallLiar(1);

            Assert.That(result.ActualCount, Is.EqualTo(4));
            Assert.That(result.CallerWasRight, Is.True);
            Assert.That(result.AffectedSeat, Is.EqualTo(0));
            Assert.That(engine.Seats[0].DiceCount, Is.EqualTo(4));
            Assert.That(engine.Seats[1].DiceCount, Is.EqualTo(5));
            Assert.That(engine.Phase, Is.EqualTo(TablePhase.RoundOver));
        }

        [Test]
        public void WrongLiarCallCostsTheChallengerADie()
        {
            GameEngine engine = TwoSeatGame();
            engine.TryBid(0, new Bid(4, 2), out _);
            RoundResult result = engine.CallLiar(1);

            Assert.That(result.CallerWasRight, Is.False);
            Assert.That(result.AffectedSeat, Is.EqualTo(1));
            Assert.That(engine.Seats[1].DiceCount, Is.EqualTo(4));
        }

        [Test]
        public void TheLoserOpensTheNextRound()
        {
            GameEngine engine = TwoSeatGame();
            engine.TryBid(0, new Bid(4, 2), out _);
            engine.CallLiar(1);
            engine.StartRound();
            Assert.That(engine.CurrentSeat, Is.EqualTo(1));
            Assert.That(engine.RoundNumber, Is.EqualTo(2));
            Assert.That(engine.Seats[1].Dice.Count, Is.EqualTo(4));
        }

        [Test]
        public void WrongSpotOnCostsTheCallerADie()
        {
            GameEngine engine = TwoSeatGame();
            engine.TryBid(0, new Bid(3, 2), out _);
            RoundResult result = engine.CallSpotOn(1);

            Assert.That(result.Kind, Is.EqualTo(ChallengeKind.SpotOn));
            Assert.That(result.CallerWasRight, Is.False);
            Assert.That(result.DiceChange, Is.EqualTo(-1));
            Assert.That(engine.Seats[1].DiceCount, Is.EqualTo(4));
        }

        [Test]
        public void CorrectSpotOnWithAFullCupChangesNothing()
        {
            GameEngine engine = TwoSeatGame();
            engine.TryBid(0, new Bid(4, 2), out _);
            RoundResult result = engine.CallSpotOn(1);

            Assert.That(result.CallerWasRight, Is.True);
            Assert.That(result.DiceChange, Is.EqualTo(0));
            Assert.That(engine.Seats[1].DiceCount, Is.EqualTo(5));
        }

        [Test]
        public void CorrectSpotOnWinsADieBack()
        {
            var rng = new ScriptedRandom(2, 2, 3, 1, 6, 2, 4, 5, 5, 6);
            GameEngine engine = TwoSeatGame(rng);
            engine.TryBid(0, new Bid(5, 2), out _);
            engine.CallLiar(1);                       // seat 0 drops to 4 dice
            Assert.That(engine.Seats[0].DiceCount, Is.EqualTo(4));

            rng.Add(3, 3, 4, 4, 3, 5, 5, 6, 6);       // three 3s on the table
            engine.StartRound();
            engine.TryBid(0, new Bid(2, 4), out _);
            engine.TryBid(1, new Bid(3, 3), out _);
            RoundResult result = engine.CallSpotOn(0);

            Assert.That(result.CallerWasRight, Is.True);
            Assert.That(result.DiceChange, Is.EqualTo(1));
            Assert.That(engine.Seats[0].DiceCount, Is.EqualTo(5));
        }

        [Test]
        public void SpotOnIsUnavailableWhenTheRuleIsOff()
        {
            GameEngine engine = TwoSeatGame(rules: new RuleSettings { SpotOn = false });
            engine.TryBid(0, new Bid(3, 2), out _);
            Assert.That(engine.CanCallSpotOn(1), Is.False);
            Assert.Throws<InvalidOperationException>(() => engine.CallSpotOn(1));
        }

        [Test]
        public void LastPirateWithDiceWins()
        {
            var engine = new GameEngine(2, new RuleSettings { StartingDice = 1 }, new ScriptedRandom(2, 3));
            engine.StartRound();
            engine.TryBid(0, new Bid(2, 2), out _);
            RoundResult result = engine.CallLiar(1);

            Assert.That(result.AffectedSeatEliminated, Is.True);
            Assert.That(result.Winner, Is.EqualTo(1));
            Assert.That(engine.Phase, Is.EqualTo(TablePhase.GameOver));
            Assert.Throws<InvalidOperationException>(() => engine.StartRound());
        }

        [Test]
        public void EliminatedSeatsAreSkipped()
        {
            var engine = new GameEngine(3, new RuleSettings { StartingDice = 1 }, new ScriptedRandom(2, 3, 4));
            engine.StartRound();
            engine.TryBid(0, new Bid(3, 2), out _);
            engine.CallLiar(1);                       // seat 0 is out

            Assert.That(engine.Seats[0].IsOut, Is.True);
            Assert.That(engine.ActiveSeatCount, Is.EqualTo(2));
            engine.StartRound();
            Assert.That(engine.CurrentSeat, Is.EqualTo(1), "next seat after the eliminated loser opens");
            engine.TryBid(1, new Bid(1, 3), out _);
            Assert.That(engine.CurrentSeat, Is.EqualTo(2));
            engine.TryBid(2, new Bid(1, 4), out _);
            Assert.That(engine.CurrentSeat, Is.EqualTo(1), "seat 0 is skipped");
        }

        [Test]
        public void DroppingToOneDieTriggersAPalificoRound()
        {
            var rules = new RuleSettings { StartingDice = 2, Palifico = true };
            var engine = new GameEngine(3, rules, new ScriptedRandom(2, 2, 3, 3, 4, 4));
            engine.StartRound();
            engine.TryBid(0, new Bid(5, 2), out _);
            RoundResult result = engine.CallLiar(1);

            Assert.That(result.NextRoundIsPalifico, Is.True);
            engine.StartRound();
            Assert.That(engine.IsPalificoRound, Is.True);
            Assert.That(engine.OnesWild, Is.False);
            Assert.That(engine.CurrentSeat, Is.EqualTo(0));
            Assert.That(engine.IsValidBid(0, new Bid(1, 1)), Is.True, "opening on 1s is allowed in Palifico");
        }

        [Test]
        public void AViewShowsOnlyYourOwnDice()
        {
            GameEngine engine = TwoSeatGame();
            PlayerView view = engine.ViewFor(1);
            Assert.That(view.MyDice, Is.EqualTo(new[] { 2, 4, 5, 5, 6 }));
            Assert.That(view.DiceCounts, Is.EqualTo(new[] { 5, 5 }));
            Assert.That(view.UnknownDice, Is.EqualTo(5));
        }

        [Test]
        public void TableSizeIsTwoToSixSeats()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameEngine(1, new RuleSettings(), new SeededRandom(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameEngine(7, new RuleSettings(), new SeededRandom(1)));
            Assert.DoesNotThrow(() => new GameEngine(6, new RuleSettings(), new SeededRandom(1)));
        }
    }
}
