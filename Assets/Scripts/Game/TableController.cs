using System;
using System.Collections.Generic;
using PiratesDice.AI;
using PiratesDice.Core;
using UnityEngine;

namespace PiratesDice.Game
{
    /// <summary>What the main menu hands the table.</summary>
    public sealed class GameConfig
    {
        public string PlayerName = "You";
        public int AiOpponents = 3;
        public AiDifficulty Difficulty = AiDifficulty.Normal;
        public RuleSettings Rules = new RuleSettings();
        /// <summary>0 picks a random seed.</summary>
        public int Seed;

        public int SeatCount => AiOpponents + 1;
    }

    /// <summary>
    /// Runs an offline table: owns the rules engine, asks each seat for its move in turn, and tells the view
    /// what happened. The view only displays; it never decides an outcome.
    /// </summary>
    public sealed class TableController : MonoBehaviour
    {
        public const int LocalSeat = 0;

        GameEngine _engine;
        ISeatController[] _seats;
        int _turnToken;

        public GameEngine Engine => _engine;
        public IReadOnlyList<ISeatController> Seats => _seats;
        public LocalSeatController LocalPlayer => (LocalSeatController)_seats[LocalSeat];
        public string LastAction { get; private set; }
        public string[] SeatLastActions { get; private set; }

        /// <summary>Something visible changed: a bid, a new turn, or a new round.</summary>
        public event Action Changed;
        /// <summary>Cups were lifted. The view shows the reveal, then calls <see cref="NextRound"/>.</summary>
        public event Action<RoundResult> Revealed;

        public void StartGame(GameConfig config)
        {
            StopTurns();
            int seed = config.Seed != 0 ? config.Seed : Environment.TickCount;
            var rng = new SeededRandom(seed);

            _seats = new ISeatController[config.SeatCount];
            _seats[LocalSeat] = new LocalSeatController(config.PlayerName);

            var crew = new List<AiPersonality>(Captains.All);
            for (int i = 1; i < config.SeatCount; i++)
            {
                AiPersonality captain = crew[rng.Next(0, crew.Count)];
                crew.Remove(captain);
                var pirate = new AiPirate(captain.WithDifficulty(config.Difficulty), new SeededRandom(seed + i * 7919));
                _seats[i] = new AiSeatController(pirate, this, seed + i);
            }

            _engine = new GameEngine(config.SeatCount, config.Rules, rng, firstSeat: rng.Next(0, config.SeatCount));
            SeatLastActions = new string[config.SeatCount];
            NextRound();
        }

        public void NextRound()
        {
            if (_engine == null || _engine.Phase == TablePhase.GameOver) return;
            _engine.StartRound();
            for (int i = 0; i < SeatLastActions.Length; i++) SeatLastActions[i] = null;
            ISeatController opener = _seats[_engine.CurrentSeat];
            LastAction = _engine.IsPalificoRound
                ? $"Palifico round! No wild 1s. {opener.DisplayName} opens."
                : $"Round {_engine.RoundNumber}. {opener.DisplayName} opens the bidding.";
            RequestTurn();
        }

        public void StopTurns()
        {
            _turnToken++;
            if (_seats == null) return;
            foreach (ISeatController seat in _seats) seat.CancelTurn();
        }

        void RequestTurn()
        {
            Changed?.Invoke();
            int seat = _engine.CurrentSeat;
            int token = ++_turnToken;
            _seats[seat].BeginTurn(_engine.ViewFor(seat), decision => OnDecision(token, seat, decision));
        }

        void OnDecision(int token, int seat, SeatDecision decision)
        {
            if (token != _turnToken || _engine.Phase != TablePhase.Bidding || seat != _engine.CurrentSeat) return;

            string name = _seats[seat].DisplayName;
            RoundResult result;
            try
            {
                result = _engine.Apply(seat, decision);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{name} made an illegal move ({decision}): {e.Message}");
                RequestTurn();
                return;
            }

            if (result == null)
            {
                SeatLastActions[seat] = $"Bid {decision.Bid}";
                LastAction = $"{name} bids {decision.Bid}.";
                RequestTurn();
                return;
            }

            SeatLastActions[seat] = decision.Kind == DecisionKind.Liar ? "Liar!" : "Spot on!";
            LastAction = $"{name} calls {SeatLastActions[seat]}";
            Changed?.Invoke();
            Revealed?.Invoke(result);
        }

        public string NameOf(int seat) => _seats[seat].DisplayName;

        /// <summary>One plain sentence saying what a reveal meant.</summary>
        public string Describe(RoundResult r)
        {
            string caller = NameOf(r.CallerSeat);
            string bidder = NameOf(r.BidderSeat);
            string count = r.ActualCount == 1 ? $"one {r.Bid.Face}" : $"{r.ActualCount} {r.Bid.Face}s";
            if (r.ActualCount == 0) count = $"no {r.Bid.Face}s";
            string affected = NameOf(r.AffectedSeat);

            string verdict;
            if (r.Kind == ChallengeKind.Liar)
                verdict = r.CallerWasRight ? $"{bidder} was lying!" : $"{bidder} told the truth.";
            else
                verdict = r.CallerWasRight ? $"{caller} was spot on!" : $"{caller} missed the mark.";

            string consequence;
            if (r.DiceChange > 0) consequence = $"{affected} wins a die back.";
            else if (r.DiceChange == 0) consequence = $"{affected}'s cup is already full.";
            else if (r.AffectedSeatEliminated) consequence = $"{affected} loses their last die and walks the plank!";
            else consequence = $"{affected} loses a die overboard.";

            return $"There were {count} for a bid of {r.Bid}. {verdict} {consequence}";
        }
    }
}
