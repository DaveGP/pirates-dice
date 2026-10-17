using NUnit.Framework;
using PiratesDice.Core;

namespace PiratesDice.Tests
{
    public class TutorialScriptTests
    {
        [Test]
        public void EveryScriptedMoveIsLegalAndEveryLessonLands()
        {
            TutorialScript script = TutorialScript.Standard();
            var engine = new GameEngine(script.SeatCount, new RuleSettings(), script.CreateDice(), script.FirstSeat);
            var outcomes = new RoundResult[script.Rounds.Count];

            for (int r = 0; r < script.Rounds.Count; r++)
            {
                engine.StartRound();
                TutorialRound round = script.Rounds[r];
                RoundResult result = null;
                foreach (TutorialMove move in round.Moves)
                {
                    Assert.That(engine.CurrentSeat, Is.EqualTo(move.Seat), $"round {r + 1}: wrong seat for {move.Decision}");
                    Assert.That(result, Is.Null, $"round {r + 1}: moves after the reveal");
                    result = engine.Apply(move.Seat, move.Decision);
                }
                Assert.That(result, Is.Not.Null, $"round {r + 1} never reached a reveal");
                outcomes[r] = result;
            }

            // Round 1: your Liar call is right and Jane loses a die.
            Assert.That(outcomes[0].Kind, Is.EqualTo(ChallengeKind.Liar));
            Assert.That(outcomes[0].ActualCount, Is.EqualTo(3));
            Assert.That(outcomes[0].AffectedSeat, Is.EqualTo(TutorialScript.Opponent));

            // Round 2: Jane catches your bluff and you lose a die.
            Assert.That(outcomes[1].ActualCount, Is.EqualTo(1));
            Assert.That(outcomes[1].AffectedSeat, Is.EqualTo(TutorialScript.Player));

            // Round 3: your Spot on is exact and wins the die back.
            Assert.That(outcomes[2].Kind, Is.EqualTo(ChallengeKind.SpotOn));
            Assert.That(outcomes[2].CallerWasRight, Is.True);
            Assert.That(outcomes[2].DiceChange, Is.EqualTo(1));

            Assert.That(engine.Seats[TutorialScript.Player].DiceCount, Is.EqualTo(5));
            Assert.That(engine.Seats[TutorialScript.Opponent].DiceCount, Is.EqualTo(4));
        }

        [Test]
        public void EveryPlayerMoveHasCoaching()
        {
            foreach (TutorialRound round in TutorialScript.Standard().Rounds)
            {
                Assert.That(round.RevealCoach, Is.Not.Empty);
                foreach (TutorialMove move in round.Moves)
                    if (move.Seat == TutorialScript.Player) Assert.That(move.Coach, Is.Not.Empty);
            }
        }
    }
}
