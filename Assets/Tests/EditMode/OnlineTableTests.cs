using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PiratesDice.AI;
using PiratesDice.Core;
using PiratesDice.Online;

namespace PiratesDice.Tests
{
    public class OnlineTableTests
    {
        static NetMessage RoundTrip(NetMessage message) => NetMessage.Decode(message.Encode());

        [Test]
        public void EveryMessageSurvivesEncoding()
        {
            var rules = new RuleSettings { StartingDice = 4, WildOnes = false, SpotOn = true, Palifico = true };
            var start = (StartMessage)RoundTrip(new StartMessage(3, 2, 1, rules, new[] { "Ann", "Calico Jane", "Bo|b,;" }, new[] { true, false, true }));
            Assert.That(start.SeatCount, Is.EqualTo(3));
            Assert.That(start.YourSeat, Is.EqualTo(2));
            Assert.That(start.FirstSeat, Is.EqualTo(1));
            Assert.That(start.Rules.StartingDice, Is.EqualTo(4));
            Assert.That(start.Rules.WildOnes, Is.False);
            Assert.That(start.Rules.Palifico, Is.True);
            Assert.That(start.Names, Is.EqualTo(new[] { "Ann", "Calico Jane", "Bob" }));
            Assert.That(start.Humans, Is.EqualTo(new[] { true, false, true }));

            var play = (PlayMessage)RoundTrip(new PlayMessage(1, SeatDecision.Liar(), new[] { new[] { 1, 2 }, new int[0], new[] { 6 } }));
            Assert.That(play.Decision.Kind, Is.EqualTo(DecisionKind.Liar));
            Assert.That(play.Dice[0], Is.EqualTo(new[] { 1, 2 }));
            Assert.That(play.Dice[1], Is.Empty);
            Assert.That(play.Dice[2], Is.EqualTo(new[] { 6 }));

            var bid = (PlayMessage)RoundTrip(new PlayMessage(0, SeatDecision.Raise(new Bid(3, 5)), null));
            Assert.That(bid.Decision.Bid, Is.EqualTo(new Bid(3, 5)));
            Assert.That(bid.Dice, Is.Null);

            var move = (MoveMessage)RoundTrip(new MoveMessage(SeatDecision.SpotOn()));
            Assert.That(move.Decision.Kind, Is.EqualTo(DecisionKind.SpotOn));

            var round = (RoundMessage)RoundTrip(new RoundMessage(new[] { 5, 0, 3 }, new[] { 1, 1, 4, 6, 6 }));
            Assert.That(round.DiceCounts, Is.EqualTo(new[] { 5, 0, 3 }));
            Assert.That(round.MyDice, Is.EqualTo(new[] { 1, 1, 4, 6, 6 }));

            var lobby = (LobbyMessage)RoundTrip(new LobbyMessage(new[] { "Ann", "Bob" }, 5));
            Assert.That(lobby.Names, Is.EqualTo(new[] { "Ann", "Bob" }));
            Assert.That(lobby.TableSize, Is.EqualTo(5));
            Assert.That(((LobbyMessage)RoundTrip(new LobbyMessage(new string[0], 2))).Names, Is.Empty);

            Assert.That(((HelloMessage)RoundTrip(new HelloMessage(Protocol.Version, "  Long John Silver the Third  "))).Name, Is.EqualTo("Long John Silver"));
            Assert.That(((SeatMessage)RoundTrip(new SeatMessage(4, false, "Salty Pete"))).Name, Is.EqualTo("Salty Pete"));
            Assert.That(((RejectMessage)RoundTrip(new RejectMessage("Table|full"))).Reason, Is.EqualTo("Table/full"));
            Assert.That(RoundTrip(new NackMessage()), Is.TypeOf<NackMessage>());
            Assert.That(((EndMessage)RoundTrip(new EndMessage("The captain left."))).Reason, Is.EqualTo("The captain left."));
        }

        [Test]
        public void MalformedMessagesAreRejected()
        {
            Assert.Throws<FormatException>(() => NetMessage.Decode(""));
            Assert.Throws<FormatException>(() => NetMessage.Decode("Mutiny|1"));
            Assert.Throws<FormatException>(() => NetMessage.Decode("Move|B|3"));
            Assert.Throws<FormatException>(() => NetMessage.Decode("Move|B|3|x"));
            Assert.Throws<FormatException>(() => NetMessage.Decode("Move|Q|3|4"));
            Assert.Throws<FormatException>(() => NetMessage.Decode("Nack|extra"));
            Assert.Throws<FormatException>(() => NetMessage.Decode("Start|9|0|0|5,1,1,0|a|1"));
        }

        [Test]
        public void SeatsTurnSoEachPlayerSitsFirst()
        {
            for (int viewer = 0; viewer < 5; viewer++)
            {
                Assert.That(Seats.Rotate(viewer, viewer, 5), Is.EqualTo(0));
                for (int seat = 0; seat < 5; seat++)
                {
                    Assert.That(Seats.Unrotate(Seats.Rotate(seat, viewer, 5), viewer, 5), Is.EqualTo(seat));
                    // The next seat round the table stays the next seat.
                    Assert.That(Seats.Rotate((seat + 1) % 5, viewer, 5), Is.EqualTo((Seats.Rotate(seat, viewer, 5) + 1) % 5));
                }
            }
        }

        [Test]
        public void ARevealNeedsMatchingDiceCounts()
        {
            var engine = new GameEngine(2, new RuleSettings(), new SeededRandom(3));
            engine.StartRound();
            Assert.Throws<ArgumentException>(() => engine.RevealDice(new[] { new[] { 1, 2, 3, 4, 5 }, new[] { 1 } }));
            Assert.Throws<ArgumentException>(() => engine.RevealDice(new[] { new[] { 1, 2, 3, 4, 7 }, new[] { 1, 2, 3, 4, 5 } }));
            engine.RevealDice(new[] { new[] { 6, 1, 6, 1, 6 }, new[] { 2, 2, 2, 2, 2 } });
            Assert.That(engine.Seats[0].Dice, Is.EqualTo(new[] { 1, 1, 6, 6, 6 }));
        }

        /// <summary>
        /// Plays whole games where every seat is a separate player with only a mirror of the table. Each one
        /// decides from its own mirror, sends its move to the host, and every message goes through the codec.
        /// The mirrors must agree with the host at every step and never see another cup before a reveal.
        /// </summary>
        [Test]
        public void MirroredTablesStayInStepWithTheHost([Values(2, 3, 6)] int seats, [Values(true, false)] bool palifico)
        {
            for (int game = 0; game < 25; game++)
            {
                int seed = 1000 * seats + game;
                var rules = new RuleSettings { Palifico = palifico };
                var rng = new SeededRandom(seed);
                int firstSeat = rng.Next(0, seats);
                var host = new GameEngine(seats, rules, rng, firstSeat);
                string[] names = Enumerable.Range(0, seats).Select(i => $"Pirate {i}").ToArray();
                bool[] humans = Enumerable.Repeat(true, seats).ToArray();

                var mirrors = new TableMirror[seats];
                var engines = new GameEngine[seats];
                var brains = new AiPirate[seats];
                for (int s = 0; s < seats; s++)
                {
                    var start = (StartMessage)RoundTrip(HostMessages.StartFor(s, seats, firstSeat, rules, names, humans));
                    mirrors[s] = new TableMirror(start);
                    engines[s] = mirrors[s].CreateEngine();
                    brains[s] = new AiPirate(Captains.All[s].WithDifficulty(AiDifficulty.Normal), new SeededRandom(seed + s));
                    Assert.That(mirrors[s].NameOf(0), Is.EqualTo(names[s]));
                }

                int moves = 0;
                while (host.Phase != TablePhase.GameOver)
                {
                    host.StartRound();
                    for (int s = 0; s < seats; s++)
                    {
                        var round = (RoundMessage)RoundTrip(HostMessages.RoundFor(host, s));
                        mirrors[s].PrepareRound(engines[s], round);
                        engines[s].StartRound();
                        for (int other = 1; other < seats; other++)
                            Assert.That(engines[s].Seats[other].Dice.All(d => d == 0), "A mirror saw another player's dice before the reveal.");
                    }

                    RoundResult hostResult = null;
                    while (hostResult == null)
                    {
                        int turn = host.CurrentSeat;
                        for (int s = 0; s < seats; s++) AssertInStep(host, engines[s], mirrors[s], s);

                        // The seat whose turn it is decides from its own mirror and sends the move to the host.
                        SeatDecision decided = brains[turn].Decide(engines[turn].ViewFor(0));
                        var move = (MoveMessage)RoundTrip(new MoveMessage(decided));
                        hostResult = host.Apply(turn, move.Decision);

                        PlayMessage play = HostMessages.PlayFor(turn, move.Decision, hostResult);
                        for (int s = 0; s < seats; s++)
                        {
                            var heard = (PlayMessage)RoundTrip(play);
                            SeatDecision decision = mirrors[s].PrepareMove(engines[s], heard, out int localSeat);
                            Assert.That(localSeat, Is.EqualTo(Seats.Rotate(turn, s, seats)));
                            RoundResult mirrored = engines[s].Apply(localSeat, decision);
                            Assert.That(mirrored == null, Is.EqualTo(hostResult == null));
                            if (mirrored != null) AssertSameResult(hostResult, mirrored, s, seats);
                        }
                        Assert.That(++moves, Is.LessThan(5000));
                    }
                }

                for (int s = 0; s < seats; s++)
                {
                    Assert.That(engines[s].Phase, Is.EqualTo(TablePhase.GameOver));
                    Assert.That(mirrors[s].ToHost(engines[s].Winner), Is.EqualTo(host.Winner));
                }
            }
        }

        static void AssertInStep(GameEngine host, GameEngine mirror, TableMirror map, int seat)
        {
            PlayerView truth = host.ViewFor(seat);
            PlayerView copy = mirror.ViewFor(0);
            Assert.That(copy.MyDice, Is.EqualTo(truth.MyDice));
            Assert.That(copy.TotalDice, Is.EqualTo(truth.TotalDice));
            Assert.That(copy.CurrentBid, Is.EqualTo(truth.CurrentBid));
            Assert.That(copy.CanCallLiar, Is.EqualTo(truth.CanCallLiar));
            Assert.That(copy.CanCallSpotOn, Is.EqualTo(truth.CanCallSpotOn));
            Assert.That(copy.IsPalificoRound, Is.EqualTo(truth.IsPalificoRound));
            Assert.That(copy.Context.BidderMayChangeFace, Is.EqualTo(truth.Context.BidderMayChangeFace));
            Assert.That(copy.RoundNumber, Is.EqualTo(truth.RoundNumber));
            if (truth.CurrentBidder >= 0) Assert.That(map.ToHost(copy.CurrentBidder), Is.EqualTo(truth.CurrentBidder));
            Assert.That(map.ToHost(mirror.CurrentSeat), Is.EqualTo(host.CurrentSeat));
            for (int s = 0; s < host.SeatCount; s++)
                Assert.That(mirror.Seats[map.ToLocal(s)].DiceCount, Is.EqualTo(host.Seats[s].DiceCount));
        }

        static void AssertSameResult(RoundResult host, RoundResult mirror, int viewer, int seats)
        {
            int Local(int s) => s < 0 ? s : Seats.Rotate(s, viewer, seats);
            Assert.That(mirror.Kind, Is.EqualTo(host.Kind));
            Assert.That(mirror.ActualCount, Is.EqualTo(host.ActualCount));
            Assert.That(mirror.CallerWasRight, Is.EqualTo(host.CallerWasRight));
            Assert.That(mirror.DiceChange, Is.EqualTo(host.DiceChange));
            Assert.That(mirror.AffectedSeat, Is.EqualTo(Local(host.AffectedSeat)));
            Assert.That(mirror.CallerSeat, Is.EqualTo(Local(host.CallerSeat)));
            Assert.That(mirror.BidderSeat, Is.EqualTo(Local(host.BidderSeat)));
            Assert.That(mirror.Winner, Is.EqualTo(Local(host.Winner)));
            Assert.That(mirror.NextRoundIsPalifico, Is.EqualTo(host.NextRoundIsPalifico));
            for (int s = 0; s < seats; s++)
                Assert.That(mirror.RevealedDice[Local(s)], Is.EqualTo(host.RevealedDice[s]));
        }
    }
}
