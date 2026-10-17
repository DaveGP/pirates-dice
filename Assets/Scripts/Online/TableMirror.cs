using System;
using System.Collections.Generic;
using System.Linq;
using PiratesDice.Core;

namespace PiratesDice.Online
{
    /// <summary>
    /// A player's copy of the host's table. It runs the same rules engine, fed only what the host sends: its own
    /// dice each round, every move, and all the dice at a reveal. Seats are turned so this player always sits in
    /// seat 0, which is where the table screen expects the local player; turn order is unchanged.
    /// </summary>
    public sealed class TableMirror
    {
        readonly QueuedDice _dice = new QueuedDice();

        public readonly StartMessage Start;
        public int SeatCount => Start.SeatCount;
        /// <summary>This player's seat in the host's numbering.</summary>
        public int HostSeat => Start.YourSeat;

        public TableMirror(StartMessage start)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public int ToLocal(int hostSeat) => Seats.Rotate(hostSeat, HostSeat, SeatCount);
        public int ToHost(int localSeat) => Seats.Unrotate(localSeat, HostSeat, SeatCount);

        public string NameOf(int localSeat) => Start.Names[ToHost(localSeat)];
        public bool IsHuman(int localSeat) => Start.Humans[ToHost(localSeat)];

        /// <summary>The engine for this mirror. Its dice come from <see cref="PrepareRound"/> rather than a random roll.</summary>
        public GameEngine CreateEngine() => new GameEngine(SeatCount, Start.Rules, _dice, ToLocal(Start.FirstSeat));

        /// <summary>
        /// Loads this player's dice for the engine's next <see cref="GameEngine.StartRound"/>. Other cups stay
        /// hidden until a reveal. Throws if the host's dice counts disagree with this copy.
        /// </summary>
        public void PrepareRound(GameEngine engine, RoundMessage round)
        {
            if (round.DiceCounts.Length != SeatCount) throw new InvalidOperationException("The host sent dice counts for the wrong number of seats.");
            for (int local = 0; local < SeatCount; local++)
            {
                int expected = round.DiceCounts[ToHost(local)];
                if (engine.Seats[local].DiceCount != expected)
                    throw new InvalidOperationException($"Out of step with the host: seat {local} holds {engine.Seats[local].DiceCount} dice, host says {expected}.");
            }
            if (round.MyDice.Length != engine.Seats[0].DiceCount) throw new InvalidOperationException("The host sent the wrong number of dice.");

            _dice.Clear();
            for (int local = 0; local < SeatCount; local++)
            {
                if (local == 0) _dice.Enqueue(round.MyDice);
                else _dice.Enqueue(Enumerable.Repeat(QueuedDice.Hidden, engine.Seats[local].DiceCount));
            }
        }

        /// <summary>
        /// Turns a move from the host into a move on this copy. For a challenge it first lifts every cup so the
        /// reveal resolves exactly as it did on the host.
        /// </summary>
        public SeatDecision PrepareMove(GameEngine engine, PlayMessage play, out int localSeat)
        {
            if (play.Seat < 0 || play.Seat >= SeatCount) throw new InvalidOperationException("The host sent a move for a seat that doesn't exist.");
            localSeat = ToLocal(play.Seat);
            if (play.Decision.Kind != DecisionKind.Bid)
            {
                if (play.Dice == null || play.Dice.Length != SeatCount) throw new InvalidOperationException("The host sent a challenge without the dice.");
                var local = new int[SeatCount][];
                for (int hostSeat = 0; hostSeat < SeatCount; hostSeat++) local[ToLocal(hostSeat)] = play.Dice[hostSeat];
                engine.RevealDice(local);
            }
            return play.Decision;
        }

        /// <summary>Records a seat that changed hands, so names and AI flags stay current.</summary>
        public int ApplySeat(SeatMessage seat)
        {
            if (seat.Seat < 0 || seat.Seat >= SeatCount) throw new InvalidOperationException("The host changed a seat that doesn't exist.");
            Start.Names[seat.Seat] = seat.Name;
            Start.Humans[seat.Seat] = seat.Human;
            return ToLocal(seat.Seat);
        }

        /// <summary>Dice handed to the engine in order. Hidden dice are placeholders until a reveal replaces them.</summary>
        sealed class QueuedDice : IRandomSource
        {
            public const int Hidden = 0;
            readonly Queue<int> _queue = new Queue<int>();

            public void Clear() => _queue.Clear();

            public void Enqueue(IEnumerable<int> dice)
            {
                foreach (int d in dice) _queue.Enqueue(d);
            }

            public int Next(int minInclusive, int maxExclusive)
            {
                if (_queue.Count == 0) throw new InvalidOperationException("The host has not dealt this round's dice yet.");
                return _queue.Dequeue();
            }

            public double NextDouble() => 0;
        }
    }

    /// <summary>Seat numbering between the host's table and a player's turned view of it.</summary>
    public static class Seats
    {
        /// <summary>Where <paramref name="hostSeat"/> sits from the point of view of the player in <paramref name="viewer"/>.</summary>
        public static int Rotate(int hostSeat, int viewer, int count) => ((hostSeat - viewer) % count + count) % count;

        public static int Unrotate(int localSeat, int viewer, int count) => (localSeat + viewer) % count;
    }

    /// <summary>What the host sends, built from its engine.</summary>
    public static class HostMessages
    {
        public static StartMessage StartFor(int seat, int seatCount, int firstSeat, RuleSettings rules, string[] names, bool[] humans) =>
            new StartMessage(seatCount, seat, firstSeat, rules, names, humans);

        public static RoundMessage RoundFor(GameEngine engine, int seat) =>
            new RoundMessage(engine.Seats.Select(s => s.DiceCount).ToArray(), engine.Seats[seat].Dice.ToArray());

        /// <summary>A move as everyone hears it. Pass the reveal for a challenge.</summary>
        public static PlayMessage PlayFor(int seat, SeatDecision decision, RoundResult reveal)
        {
            if (decision.Kind != DecisionKind.Bid && reveal == null) throw new ArgumentException("A challenge needs its reveal.", nameof(reveal));
            return new PlayMessage(seat, decision, decision.Kind == DecisionKind.Bid ? null : reveal.RevealedDice);
        }
    }
}
