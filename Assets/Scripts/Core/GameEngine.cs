using System;
using System.Collections.Generic;
using System.Linq;

namespace PiratesDice.Core
{
    public enum TablePhase { WaitingToRoll, Bidding, RoundOver, GameOver }

    public enum ChallengeKind { Liar, SpotOn }

    public sealed class SeatState
    {
        public readonly int Index;
        public int DiceCount { get; internal set; }
        internal int[] DiceInternal = new int[0];
        /// <summary>This pirate has already triggered their one Palifico round.</summary>
        public bool HasHadPalifico { get; internal set; }

        public bool IsOut => DiceCount == 0;
        public IReadOnlyList<int> Dice => DiceInternal;

        internal SeatState(int index, int diceCount)
        {
            Index = index;
            DiceCount = diceCount;
        }
    }

    public readonly struct BidRecord
    {
        public readonly int Seat;
        public readonly Bid Bid;

        public BidRecord(int seat, Bid bid)
        {
            Seat = seat;
            Bid = bid;
        }
    }

    /// <summary>What happened when the cups were lifted.</summary>
    public sealed class RoundResult
    {
        public ChallengeKind Kind;
        public int CallerSeat;
        public int BidderSeat;
        public Bid Bid;
        public int ActualCount;
        public bool OnesWereWild;
        /// <summary>For Liar: the bid was a lie. For Spot on: the count was exact.</summary>
        public bool CallerWasRight;
        /// <summary>The seat that lost (or, for a correct Spot on, gained) a die.</summary>
        public int AffectedSeat;
        /// <summary>-1 for a lost die, +1 for a regained die, 0 if Spot on was right but the caller already had a full cup.</summary>
        public int DiceChange;
        public bool AffectedSeatEliminated;
        public bool NextRoundIsPalifico;
        /// <summary>Every seat's dice as revealed, indexed by seat (empty for seats already out).</summary>
        public int[][] RevealedDice;
        /// <summary>The winning seat if this round ended the game, otherwise -1.</summary>
        public int Winner = -1;
    }

    /// <summary>
    /// Authoritative Liar's Dice rules engine. Pure C#, no Unity dependency, so the same code runs offline,
    /// on a network host, and in plain .NET unit tests.
    /// </summary>
    public sealed class GameEngine
    {
        readonly IRandomSource _rng;
        readonly SeatState[] _seats;
        readonly List<BidRecord> _history = new List<BidRecord>();
        int _nextStarter;
        bool _nextRoundPalifico;

        public RuleSettings Rules { get; }
        public TablePhase Phase { get; private set; } = TablePhase.WaitingToRoll;
        public int CurrentSeat { get; private set; } = -1;
        public Bid? CurrentBid { get; private set; }
        public int CurrentBidder { get; private set; } = -1;
        public int RoundNumber { get; private set; }
        public bool IsPalificoRound { get; private set; }
        public int PalificoSeat { get; private set; } = -1;
        public RoundResult LastResult { get; private set; }
        public int Winner { get; private set; } = -1;

        public IReadOnlyList<SeatState> Seats => _seats;
        public IReadOnlyList<BidRecord> BidHistory => _history;
        public int SeatCount => _seats.Length;
        public int TotalDice => _seats.Sum(s => s.DiceCount);
        public int ActiveSeatCount => _seats.Count(s => !s.IsOut);
        public bool OnesWild => Rules.WildOnes && !IsPalificoRound;

        public GameEngine(int seatCount, RuleSettings rules, IRandomSource rng, int firstSeat = 0)
        {
            if (seatCount < RuleSettings.MinSeats || seatCount > RuleSettings.MaxSeats)
                throw new ArgumentOutOfRangeException(nameof(seatCount), $"A table seats {RuleSettings.MinSeats} to {RuleSettings.MaxSeats} pirates.");
            if (firstSeat < 0 || firstSeat >= seatCount)
                throw new ArgumentOutOfRangeException(nameof(firstSeat));

            Rules = (rules ?? new RuleSettings()).Clone();
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _seats = new SeatState[seatCount];
            for (int i = 0; i < seatCount; i++) _seats[i] = new SeatState(i, Rules.StartingDice);
            _nextStarter = firstSeat;
        }

        /// <summary>Rolls every cup and opens bidding. Valid before the first round and after each reveal.</summary>
        public void StartRound()
        {
            if (Phase != TablePhase.WaitingToRoll && Phase != TablePhase.RoundOver)
                throw new InvalidOperationException($"Cannot start a round during {Phase}.");

            RoundNumber++;
            IsPalificoRound = _nextRoundPalifico;
            PalificoSeat = IsPalificoRound ? _nextStarter : -1;
            _nextRoundPalifico = false;

            foreach (SeatState seat in _seats)
            {
                seat.DiceInternal = new int[seat.DiceCount];
                for (int d = 0; d < seat.DiceCount; d++) seat.DiceInternal[d] = _rng.Next(1, RuleSettings.Faces + 1);
                Array.Sort(seat.DiceInternal);
            }

            _history.Clear();
            CurrentBid = null;
            CurrentBidder = -1;
            CurrentSeat = _nextStarter;
            Phase = TablePhase.Bidding;
        }

        public BidContext ContextFor(int seat)
        {
            bool mayChangeFace = !IsPalificoRound || _seats[seat].DiceCount == 1;
            return new BidContext(OnesWild, TotalDice, IsPalificoRound, mayChangeFace);
        }

        public bool IsValidBid(int seat, Bid bid) =>
            Phase == TablePhase.Bidding && seat == CurrentSeat && BidRules.IsValid(CurrentBid, bid, ContextFor(seat));

        public bool CanCallLiar(int seat) =>
            Phase == TablePhase.Bidding && seat == CurrentSeat && CurrentBid != null;

        public bool CanCallSpotOn(int seat) => Rules.SpotOn && CanCallLiar(seat);

        public bool TryBid(int seat, Bid bid, out string error)
        {
            if (Phase != TablePhase.Bidding) { error = "No round is in progress."; return false; }
            if (seat != CurrentSeat) { error = "It is not this pirate's turn."; return false; }
            if (!BidRules.IsValid(CurrentBid, bid, ContextFor(seat))) { error = $"{bid} does not beat the current bid."; return false; }

            CurrentBid = bid;
            CurrentBidder = seat;
            _history.Add(new BidRecord(seat, bid));
            CurrentSeat = NextActiveSeat(seat);
            error = null;
            return true;
        }

        public RoundResult CallLiar(int seat)
        {
            if (!CanCallLiar(seat)) throw new InvalidOperationException("Liar can only be called on your turn, against a standing bid.");

            Bid bid = CurrentBid.Value;
            int actual = CountOnTable(bid.Face);
            bool bidWasLie = actual < bid.Quantity;
            int loser = bidWasLie ? CurrentBidder : seat;
            return Resolve(ChallengeKind.Liar, seat, bid, actual, bidWasLie, loser, -1);
        }

        public RoundResult CallSpotOn(int seat)
        {
            if (!CanCallSpotOn(seat)) throw new InvalidOperationException("Spot on can only be called on your turn, against a standing bid.");

            Bid bid = CurrentBid.Value;
            int actual = CountOnTable(bid.Face);
            bool exact = actual == bid.Quantity;
            int change = exact ? (_seats[seat].DiceCount < Rules.StartingDice ? 1 : 0) : -1;
            return Resolve(ChallengeKind.SpotOn, seat, bid, actual, exact, seat, change);
        }

        /// <summary>The information one seat is allowed to see: its own dice plus everything public.</summary>
        public PlayerView ViewFor(int seat) => new PlayerView(this, seat);

        public int CountOnTable(int face)
        {
            bool wild = OnesWild;
            return _seats.Sum(s => BidRules.CountMatches(s.DiceInternal, face, wild));
        }

        public int NextActiveSeat(int from)
        {
            for (int i = 1; i <= _seats.Length; i++)
            {
                int candidate = (from + i) % _seats.Length;
                if (!_seats[candidate].IsOut) return candidate;
            }
            return -1;
        }

        RoundResult Resolve(ChallengeKind kind, int caller, Bid bid, int actual, bool callerRight, int affected, int change)
        {
            var result = new RoundResult
            {
                Kind = kind,
                CallerSeat = caller,
                BidderSeat = CurrentBidder,
                Bid = bid,
                ActualCount = actual,
                OnesWereWild = OnesWild,
                CallerWasRight = callerRight,
                AffectedSeat = affected,
                DiceChange = change,
                RevealedDice = _seats.Select(s => (int[])s.DiceInternal.Clone()).ToArray(),
            };

            SeatState hit = _seats[affected];
            hit.DiceCount += change;
            result.AffectedSeatEliminated = hit.IsOut;

            if (Rules.Palifico && change < 0 && hit.DiceCount == 1 && !hit.HasHadPalifico && ActiveSeatCount > 2)
            {
                hit.HasHadPalifico = true;
                _nextRoundPalifico = true;
            }
            result.NextRoundIsPalifico = _nextRoundPalifico;

            _nextStarter = hit.IsOut ? NextActiveSeat(affected) : affected;
            CurrentSeat = -1;
            LastResult = result;

            if (ActiveSeatCount == 1)
            {
                Winner = _seats.First(s => !s.IsOut).Index;
                result.Winner = Winner;
                Phase = TablePhase.GameOver;
            }
            else
            {
                Phase = TablePhase.RoundOver;
            }
            return result;
        }
    }
}
