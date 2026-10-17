using System;
using System.Collections;
using PiratesDice.AI;
using PiratesDice.Core;
using UnityEngine;

namespace PiratesDice.Game
{
    /// <summary>
    /// Whoever sits in a seat: the local player, an AI pirate, or (from milestone 3) an online player.
    /// The table asks the seat for a decision on its turn and does not care which kind it is.
    /// </summary>
    public interface ISeatController
    {
        string DisplayName { get; }
        bool IsLocalHuman { get; }
        void BeginTurn(PlayerView view, Action<SeatDecision> decide);
        void CancelTurn();
    }

    /// <summary>A seat driven by this device's player through the table UI.</summary>
    public sealed class LocalSeatController : ISeatController
    {
        Action<SeatDecision> _pending;

        public LocalSeatController(string name) { DisplayName = name; }

        public string DisplayName { get; }
        public bool IsLocalHuman => true;
        public bool IsWaiting => _pending != null;
        public PlayerView CurrentView { get; private set; }

        public event Action TurnStarted;

        public void BeginTurn(PlayerView view, Action<SeatDecision> decide)
        {
            CurrentView = view;
            _pending = decide;
            TurnStarted?.Invoke();
        }

        /// <summary>Called by the UI. Ignores input when it is not this seat's turn or the move is illegal.</summary>
        public bool Submit(SeatDecision decision)
        {
            if (_pending == null || CurrentView == null) return false;
            if (decision.Kind == DecisionKind.Bid && !CurrentView.IsValidBid(decision.Bid)) return false;
            if (decision.Kind == DecisionKind.Liar && !CurrentView.CanCallLiar) return false;
            if (decision.Kind == DecisionKind.SpotOn && !CurrentView.CanCallSpotOn) return false;

            Action<SeatDecision> decide = _pending;
            _pending = null;
            decide(decision);
            return true;
        }

        public void CancelTurn() => _pending = null;
    }

    /// <summary>A seat played by an AI pirate, with a short pause so it feels like they're thinking.</summary>
    public sealed class AiSeatController : ISeatController
    {
        readonly AiPirate _pirate;
        readonly MonoBehaviour _host;
        readonly System.Random _timing;
        Coroutine _thinking;

        public AiSeatController(AiPirate pirate, MonoBehaviour host, int seed)
        {
            _pirate = pirate;
            _host = host;
            _timing = new System.Random(seed);
        }

        public string DisplayName => _pirate.Personality.Name;
        public bool IsLocalHuman => false;
        /// <summary>Seconds scale for thinking time; 0 makes AI moves instant.</summary>
        public float Pace { get; set; } = 1f;

        public void BeginTurn(PlayerView view, Action<SeatDecision> decide)
        {
            CancelTurn();
            _thinking = _host.StartCoroutine(Think(view, decide));
        }

        IEnumerator Think(PlayerView view, Action<SeatDecision> decide)
        {
            float delay = (0.7f + (float)_timing.NextDouble() * 1.1f) * Pace;
            if (delay > 0) yield return new WaitForSeconds(delay);
            _thinking = null;
            decide(_pirate.Decide(view));
        }

        public void CancelTurn()
        {
            if (_thinking != null) _host.StopCoroutine(_thinking);
            _thinking = null;
        }
    }
}
