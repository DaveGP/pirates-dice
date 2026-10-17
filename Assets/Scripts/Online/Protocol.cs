using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PiratesDice.Core;

namespace PiratesDice.Online
{
    public enum MessageKind { Hello, Lobby, Reject, Start, Round, Move, Play, Seat, Nack, End }

    /// <summary>
    /// The messages an online table sends. The host runs the only real game; players get their own dice,
    /// every move, and everyone's dice only when the cups are lifted. Each message is one short line of text.
    /// </summary>
    public abstract class NetMessage
    {
        public abstract MessageKind Kind { get; }

        public string Encode() => string.Join("|", Fields().Prepend(Kind.ToString()));

        protected abstract IEnumerable<string> Fields();

        /// <summary>Parses a line from <see cref="Encode"/>. Throws <see cref="FormatException"/> on anything malformed.</summary>
        public static NetMessage Decode(string line)
        {
            if (string.IsNullOrEmpty(line)) throw new FormatException("Empty message.");
            string[] f = line.Split('|');
            if (!Enum.TryParse(f[0], out MessageKind kind)) throw new FormatException($"Unknown message '{f[0]}'.");
            var r = new FieldReader(f);
            NetMessage message;
            switch (kind)
            {
                case MessageKind.Hello: message = new HelloMessage(r.Int(), r.Text()); break;
                case MessageKind.Lobby: message = new LobbyMessage(r.Names(), r.Int()); break;
                case MessageKind.Reject: message = new RejectMessage(r.Text()); break;
                case MessageKind.Start: message = new StartMessage(r.Int(), r.Int(), r.Int(), r.Rules(), r.Names(), r.Flags()); break;
                case MessageKind.Round: message = new RoundMessage(r.Ints(), r.Ints()); break;
                case MessageKind.Move: message = new MoveMessage(r.Decision()); break;
                case MessageKind.Play: message = new PlayMessage(r.Int(), r.Decision(), r.DiceOrNull()); break;
                case MessageKind.Seat: message = new SeatMessage(r.Int(), r.Bool(), r.Text()); break;
                case MessageKind.Nack: message = new NackMessage(); break;
                case MessageKind.End: message = new EndMessage(r.Text()); break;
                default: throw new FormatException($"Unhandled message '{kind}'.");
            }
            r.ExpectEnd();
            return message;
        }

        // ---------- field encoding ----------

        protected static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
        protected static string Bool(bool value) => value ? "1" : "0";
        protected static string Ints(IEnumerable<int> values) => string.Join(",", values.Select(Int));
        protected static string EncNames(IEnumerable<string> names) => string.Join(",", names.Select(Protocol.CleanName));
        protected static string EncFlags(IEnumerable<bool> flags) => string.Concat(flags.Select(b => b ? "1" : "0"));

        protected static string EncRules(RuleSettings rules) =>
            $"{Int(rules.StartingDice)},{Bool(rules.WildOnes)},{Bool(rules.SpotOn)},{Bool(rules.Palifico)}";

        protected static IEnumerable<string> EncDecision(SeatDecision d)
        {
            switch (d.Kind)
            {
                case DecisionKind.Liar: return new[] { "L", "0", "0" };
                case DecisionKind.SpotOn: return new[] { "S", "0", "0" };
                default: return new[] { "B", Int(d.Bid.Quantity), Int(d.Bid.Face) };
            }
        }

        protected static string EncDice(int[][] dice) => dice == null ? "-" : string.Join(";", dice.Select(Ints));

        sealed class FieldReader
        {
            readonly string[] _f;
            int _i = 1;

            public FieldReader(string[] fields) { _f = fields; }

            string Next()
            {
                if (_i >= _f.Length) throw new FormatException($"Message '{_f[0]}' is missing fields.");
                return _f[_i++];
            }

            public void ExpectEnd()
            {
                if (_i != _f.Length) throw new FormatException($"Message '{_f[0]}' has extra fields.");
            }

            public string Text() => Next();

            public int Int() => ParseInt(Next());

            public bool Bool()
            {
                string s = Next();
                if (s == "1") return true;
                if (s == "0") return false;
                throw new FormatException($"'{s}' is not a flag.");
            }

            public int[] Ints() => ParseInts(Next());

            public string[] Names()
            {
                string s = Next();
                return s.Length == 0 ? new string[0] : s.Split(',');
            }

            public bool[] Flags()
            {
                string s = Next();
                if (s.Any(c => c != '0' && c != '1')) throw new FormatException($"'{s}' is not a list of flags.");
                return s.Select(c => c == '1').ToArray();
            }

            public RuleSettings Rules()
            {
                string[] p = Next().Split(',');
                if (p.Length != 4) throw new FormatException("Rules need four values.");
                return new RuleSettings
                {
                    StartingDice = ParseInt(p[0]),
                    WildOnes = p[1] == "1",
                    SpotOn = p[2] == "1",
                    Palifico = p[3] == "1",
                };
            }

            public SeatDecision Decision()
            {
                string kind = Next();
                int quantity = Int(), face = Int();
                switch (kind)
                {
                    case "L": return SeatDecision.Liar();
                    case "S": return SeatDecision.SpotOn();
                    case "B": return SeatDecision.Raise(new Bid(quantity, face));
                    default: throw new FormatException($"'{kind}' is not a move.");
                }
            }

            public int[][] DiceOrNull()
            {
                string s = Next();
                return s == "-" ? null : s.Split(';').Select(ParseInts).ToArray();
            }

            static int ParseInt(string s)
            {
                if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                    throw new FormatException($"'{s}' is not a number.");
                return value;
            }

            static int[] ParseInts(string s) => s.Length == 0 ? new int[0] : s.Split(',').Select(ParseInt).ToArray();
        }
    }

    public static class Protocol
    {
        /// <summary>Bumped whenever messages change, so mismatched builds refuse each other politely.</summary>
        public const int Version = 1;
        /// <summary>The named network message every table line travels on.</summary>
        public const string Channel = "PiratesDice";
        public const int MaxNameLength = 16;

        /// <summary>A pirate name that is safe to put in a message: no separators, trimmed, never empty.</summary>
        public static string CleanName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
            {
                if (c == '|' || c == ',' || c == ';' || char.IsControl(c)) continue;
                sb.Append(c);
            }
            string clean = sb.ToString().Trim();
            if (clean.Length > MaxNameLength) clean = clean.Substring(0, MaxNameLength).Trim();
            return clean.Length == 0 ? "Deckhand" : clean;
        }
    }

    /// <summary>Player to host, once connected: who I am.</summary>
    public sealed class HelloMessage : NetMessage
    {
        public readonly int Version;
        public readonly string Name;
        public HelloMessage(int version, string name) { Version = version; Name = Protocol.CleanName(name); }
        public override MessageKind Kind => MessageKind.Hello;
        protected override IEnumerable<string> Fields() => new[] { Int(Version), Name };
    }

    /// <summary>Host to players while gathering the crew: who's aboard and how many seats the table will have.</summary>
    public sealed class LobbyMessage : NetMessage
    {
        public readonly string[] Names;
        public readonly int TableSize;
        public LobbyMessage(string[] names, int tableSize) { Names = names.Select(Protocol.CleanName).ToArray(); TableSize = tableSize; }
        public override MessageKind Kind => MessageKind.Lobby;
        protected override IEnumerable<string> Fields() => new[] { EncNames(Names), Int(TableSize) };
    }

    /// <summary>Host to a player it will not seat. The player leaves after showing the reason.</summary>
    public sealed class RejectMessage : NetMessage
    {
        public readonly string Reason;
        public RejectMessage(string reason) { Reason = Clean(reason); }
        public override MessageKind Kind => MessageKind.Reject;
        protected override IEnumerable<string> Fields() => new[] { Reason };
        internal static string Clean(string s) => (s ?? "").Replace('|', '/');
    }

    /// <summary>Host to each player: the game begins. Seats are in the host's order.</summary>
    public sealed class StartMessage : NetMessage
    {
        public readonly int SeatCount;
        public readonly int YourSeat;
        public readonly int FirstSeat;
        public readonly RuleSettings Rules;
        public readonly string[] Names;
        /// <summary>True where a person sits, false for an AI pirate.</summary>
        public readonly bool[] Humans;

        public StartMessage(int seatCount, int yourSeat, int firstSeat, RuleSettings rules, string[] names, bool[] humans)
        {
            if (seatCount < RuleSettings.MinSeats || seatCount > RuleSettings.MaxSeats) throw new FormatException("Bad seat count.");
            if (yourSeat < 0 || yourSeat >= seatCount || firstSeat < 0 || firstSeat >= seatCount) throw new FormatException("Bad seat.");
            if (names.Length != seatCount || humans.Length != seatCount) throw new FormatException("One name and one flag per seat.");
            SeatCount = seatCount;
            YourSeat = yourSeat;
            FirstSeat = firstSeat;
            Rules = rules.Clone();
            Names = names.Select(Protocol.CleanName).ToArray();
            Humans = (bool[])humans.Clone();
        }

        public override MessageKind Kind => MessageKind.Start;
        protected override IEnumerable<string> Fields() =>
            new[] { Int(SeatCount), Int(YourSeat), Int(FirstSeat), EncRules(Rules), EncNames(Names), EncFlags(Humans) };
    }

    /// <summary>Host to one player: a new round, with every seat's dice count and only that player's own dice.</summary>
    public sealed class RoundMessage : NetMessage
    {
        public readonly int[] DiceCounts;
        public readonly int[] MyDice;
        public RoundMessage(int[] diceCounts, int[] myDice) { DiceCounts = diceCounts; MyDice = myDice; }
        public override MessageKind Kind => MessageKind.Round;
        protected override IEnumerable<string> Fields() => new[] { Ints(DiceCounts), Ints(MyDice) };
    }

    /// <summary>Player to host: my move. The host checks it is legal and that it is my turn.</summary>
    public sealed class MoveMessage : NetMessage
    {
        public readonly SeatDecision Decision;
        public MoveMessage(SeatDecision decision) { Decision = decision; }
        public override MessageKind Kind => MessageKind.Move;
        protected override IEnumerable<string> Fields() => EncDecision(Decision);
    }

    /// <summary>Host to every player: a seat made a move. A challenge carries every cup's dice for the reveal.</summary>
    public sealed class PlayMessage : NetMessage
    {
        public readonly int Seat;
        public readonly SeatDecision Decision;
        /// <summary>Every seat's dice, in the host's seat order, or null for a bid.</summary>
        public readonly int[][] Dice;

        public PlayMessage(int seat, SeatDecision decision, int[][] dice)
        {
            Seat = seat;
            Decision = decision;
            Dice = dice;
        }

        public override MessageKind Kind => MessageKind.Play;
        protected override IEnumerable<string> Fields() => new[] { Int(Seat) }.Concat(EncDecision(Decision)).Append(EncDice(Dice));
    }

    /// <summary>Host to every player: someone now sits in a seat, such as an AI pirate taking over for a player who left.</summary>
    public sealed class SeatMessage : NetMessage
    {
        public readonly int Seat;
        public readonly bool Human;
        public readonly string Name;
        public SeatMessage(int seat, bool human, string name) { Seat = seat; Human = human; Name = Protocol.CleanName(name); }
        public override MessageKind Kind => MessageKind.Seat;
        protected override IEnumerable<string> Fields() => new[] { Int(Seat), Bool(Human), Name };
    }

    /// <summary>Host to a player whose move was refused. The player is asked for a move again.</summary>
    public sealed class NackMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.Nack;
        protected override IEnumerable<string> Fields() => Enumerable.Empty<string>();
    }

    /// <summary>Host to every player: the table is closing.</summary>
    public sealed class EndMessage : NetMessage
    {
        public readonly string Reason;
        public EndMessage(string reason) { Reason = RejectMessage.Clean(reason); }
        public override MessageKind Kind => MessageKind.End;
        protected override IEnumerable<string> Fields() => new[] { Reason };
    }
}
