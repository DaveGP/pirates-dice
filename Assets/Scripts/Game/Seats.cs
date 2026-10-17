using System;
using System.Collections;
using PiratesDice.AI;
using PiratesDice.Core;
using UnityEngine;

namespace PiratesDice.Game
{
    /// <summary>
    /// Whoever sits in a seat: the local player, an AI pirate, or an online player.
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
        /// <summary>Optional extra check on moves, used by the tutorial to steer the player.</summary>
        public Func<SeatDecision, bool> Allow;
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
            if (Allow != null && !Allow(decision)) return false;

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

        readonly string _name;

        /// <param name="name">Shown instead of the captain's name, such as when the AI stands in for a player who left.</param>
        public AiSeatController(AiPirate pirate, MonoBehaviour host, int seed, string name = null)
        {
            _pirate = pirate;
            _host = host;
            _timing = new System.Random(seed);
            _name = name;
        }

        public string DisplayName => _name ?? _pirate.Personality.Name;
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

    /// <summary>A seat that plays a fixed list of moves, for the tutorial.</summary>
    public sealed class ScriptedSeatController : ISeatController
    {
        readonly MonoBehaviour _host;
        readonly Func<SeatDecision> _next;
        Coroutine _thinking;

        public ScriptedSeatController(string name, MonoBehaviour host, Func<SeatDecision> next)
        {
            DisplayName = name;
            _host = host;
            _next = next;
        }

        public string DisplayName { get; }
        public bool IsLocalHuman => false;
        public float ThinkSeconds = 1.6f;

        public void BeginTurn(PlayerView view, Action<SeatDecision> decide)
        {
            CancelTurn();
            _thinking = _host.StartCoroutine(Think(decide));
        }

        IEnumerator Think(Action<SeatDecision> decide)
        {
            yield return new WaitForSeconds(ThinkSeconds);
            _thinking = null;
            decide(_next());
        }

        public void CancelTurn()
        {
            if (_thinking != null) _host.StopCoroutine(_thinking);
            _thinking = null;
        }
    }

    /// <summary>
    /// On the host: a seat played by someone on another device. Their moves arrive over the network. If they
    /// take too long, an AI pirate moves for them so the table never stalls.
    /// </summary>
    public sealed class NetworkSeatController : ISeatController
    {
        readonly MonoBehaviour _host;
        readonly AiPirate _standIn;
        Action<SeatDecision> _pending;
        PlayerView _view;
        Coroutine _timer;

        public NetworkSeatController(string name, ulong clientId, MonoBehaviour host, int seed)
        {
            DisplayName = name;
            ClientId = clientId;
            _host = host;
            _standIn = new AiPirate(Captains.All[0].WithDifficulty(AiDifficulty.Normal), new SeededRandom(seed));
        }

        public string DisplayName { get; }
        public ulong ClientId { get; }
        public bool IsLocalHuman => false;
        /// <summary>How long a player has to move before an AI pirate moves for them.</summary>
        public float TurnSeconds = 45f;

        public void BeginTurn(PlayerView view, Action<SeatDecision> decide)
        {
            CancelTurn();
            _view = view;
            _pending = decide;
            _timer = _host.StartCoroutine(Timeout());
        }

        IEnumerator Timeout()
        {
            yield return new WaitForSeconds(TurnSeconds);
            _timer = null;
            if (_pending != null) Decide(_standIn.Decide(_view));
        }

        /// <summary>A move from the player's device. False if it isn't their turn or the move is illegal.</summary>
        public bool Receive(SeatDecision decision)
        {
            if (_pending == null || _view == null) return false;
            if (decision.Kind == DecisionKind.Bid && !_view.IsValidBid(decision.Bid)) return false;
            if (decision.Kind == DecisionKind.Liar && !_view.CanCallLiar) return false;
            if (decision.Kind == DecisionKind.SpotOn && !_view.CanCallSpotOn) return false;
            Decide(decision);
            return true;
        }

        void Decide(SeatDecision decision)
        {
            Action<SeatDecision> decide = _pending;
            CancelTurn();
            decide(decision);
        }

        public void CancelTurn()
        {
            if (_timer != null) _host.StopCoroutine(_timer);
            _timer = null;
            _pending = null;
        }
    }

    /// <summary>
    /// On a player's device: someone else's seat at an online table. The host decides and announces their
    /// moves, so this seat never decides anything itself.
    /// </summary>
    public sealed class MirrorSeatController : ISeatController
    {
        public MirrorSeatController(string name) { DisplayName = name; }

        public string DisplayName { get; set; }
        public bool IsLocalHuman => false;
        public void BeginTurn(PlayerView view, Action<SeatDecision> decide) { }
        public void CancelTurn() { }
    }
}
