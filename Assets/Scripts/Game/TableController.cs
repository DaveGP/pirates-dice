using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PiratesDice.AI;
using PiratesDice.Core;
using PiratesDice.Online;
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

    public enum TableMode { Offline, Tutorial, OnlineHost, OnlinePlayer }

    /// <summary>A person at an online table, as the host knows them.</summary>
    public readonly struct RemotePlayer
    {
        public readonly ulong ClientId;
        public readonly string Name;
        public RemotePlayer(ulong clientId, string name) { ClientId = clientId; Name = name; }
    }

    /// <summary>
    /// Runs a table: owns the rules engine, asks each seat for its move in turn, and tells the view what
    /// happened. The view only displays; it never decides an outcome.
    /// Offline and when hosting online, this is the real game. On an online player's device it is a mirror of
    /// the host's game: its engine is fed the host's messages, and the local player's moves go to the host.
    /// The local player is always seat 0.
    /// </summary>
    public sealed class TableController : MonoBehaviour
    {
        public const int LocalSeat = 0;
        /// <summary>Online, how long the reveal stays up before the host deals the next round.</summary>
        public const float RevealSeconds = 8f;

        GameEngine _engine;
        ISeatController[] _seats;
        int _turnToken;
        TableMirror _mirror;
        Action<SeatDecision> _sendMove;
        Coroutine _nextRound;
        int _seed;

        public GameEngine Engine => _engine;
        public IReadOnlyList<ISeatController> Seats => _seats;
        public LocalSeatController LocalPlayer => (LocalSeatController)_seats[LocalSeat];
        public string LastAction { get; private set; }
        public string[] SeatLastActions { get; private set; }
        public TableMode Mode { get; private set; }
        public bool IsOnline => Mode == TableMode.OnlineHost || Mode == TableMode.OnlinePlayer;
        /// <summary>This device mirrors someone else's table.</summary>
        public bool IsMirror => Mode == TableMode.OnlinePlayer;
        /// <summary>Hosting: each seat's name as the other players see it.</summary>
        public string[] PublicNames { get; private set; }
        /// <summary>Hosting: true where a person sits.</summary>
        public bool[] Humans { get; private set; }
        /// <summary>Hosting: the seat that opened the first round.</summary>
        public int FirstSeat { get; private set; }

        /// <summary>Something visible changed: a bid, a new turn, or a new round.</summary>
        public event Action Changed;
        /// <summary>Cups were lifted. The view shows the reveal, then calls <see cref="NextRound"/>.</summary>
        public event Action<RoundResult> Revealed;
        /// <summary>Fresh dice were rolled.</summary>
        public event Action RoundStarted;
        /// <summary>A seat bid (challenges arrive through <see cref="Revealed"/>).</summary>
        public event Action<int, Bid> BidPlaced;
        /// <summary>Any move was applied, with the reveal if it was a challenge. Raised before the other events.</summary>
        public event Action<int, SeatDecision, RoundResult> MoveApplied;
        /// <summary>A seat changed hands, such as an AI pirate taking over for a player who left.</summary>
        public event Action<int> SeatChanged;
        /// <summary>Mirroring: this copy no longer matches the host's table and can't continue.</summary>
        public event Action<string> Desynced;

        /// <summary>The lesson being played, or null in a normal game.</summary>
        public TutorialScript Tutorial { get; private set; }
        /// <summary>While true the tutorial is showing its intro and the player cannot move yet.</summary>
        public bool TutorialPaused { get; set; }
        int _tutorialRound = -1, _tutorialMove;

        public TutorialRound CurrentTutorialRound =>
            Tutorial != null && _tutorialRound >= 0 && _tutorialRound < Tutorial.Rounds.Count ? Tutorial.Rounds[_tutorialRound] : null;

        public TutorialMove CurrentTutorialMove
        {
            get
            {
                TutorialRound round = CurrentTutorialRound;
                return round != null && _tutorialMove < round.Moves.Count ? round.Moves[_tutorialMove] : null;
            }
        }

        public bool TutorialFinished => Tutorial != null && _tutorialRound >= Tutorial.Rounds.Count - 1 && CurrentTutorialMove == null;

        public void StartGame(GameConfig config)
        {
            StopTurns();
            Reset(TableMode.Offline);
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

        /// <summary>Starts the scripted lesson: you against one pirate, with fixed dice and moves.</summary>
        public void StartTutorial(TutorialScript script, string playerName)
        {
            StopTurns();
            Reset(TableMode.Tutorial);
            Tutorial = script;
            TutorialPaused = true;
            _tutorialRound = -1;
            _tutorialMove = 0;

            var local = new LocalSeatController(playerName);
            local.Allow = decision =>
            {
                TutorialMove move = CurrentTutorialMove;
                return !TutorialPaused && move != null && move.Seat == LocalSeat && Same(move.Decision, decision);
            };
            _seats = new ISeatController[script.SeatCount];
            _seats[LocalSeat] = local;
            for (int i = 1; i < script.SeatCount; i++)
                _seats[i] = new ScriptedSeatController(script.OpponentName, this, () => CurrentTutorialMove.Decision);

            _engine = new GameEngine(script.SeatCount, new RuleSettings(), script.CreateDice(), script.FirstSeat);
            SeatLastActions = new string[script.SeatCount];
            NextRound();
        }

        static bool Same(SeatDecision a, SeatDecision b) => a.Kind == b.Kind && (a.Kind != DecisionKind.Bid || a.Bid == b.Bid);

        void Reset(TableMode mode)
        {
            Mode = mode;
            Tutorial = null;
            TutorialPaused = false;
            _mirror = null;
            _sendMove = null;
            PublicNames = null;
            Humans = null;
        }

        // ---------- hosting online ----------

        /// <summary>
        /// Seats the host, then each remote player, then AI pirates up to the table size. Call
        /// <see cref="NextRound"/> to deal once the players have been told the seating.
        /// </summary>
        public void StartOnlineHost(GameConfig config, string hostName, IReadOnlyList<RemotePlayer> players)
        {
            StopTurns();
            Reset(TableMode.OnlineHost);
            _seed = config.Seed != 0 ? config.Seed : Environment.TickCount;
            var rng = new SeededRandom(_seed);

            int seatCount = Mathf.Clamp(Math.Max(players.Count + 1, config.SeatCount), RuleSettings.MinSeats, RuleSettings.MaxSeats);
            _seats = new ISeatController[seatCount];
            PublicNames = new string[seatCount];
            Humans = new bool[seatCount];

            _seats[LocalSeat] = new LocalSeatController("You");
            PublicNames[LocalSeat] = Protocol.CleanName(hostName);
            Humans[LocalSeat] = true;
            for (int i = 0; i < players.Count && i + 1 < seatCount; i++)
            {
                int seat = i + 1;
                _seats[seat] = new NetworkSeatController(Protocol.CleanName(players[i].Name), players[i].ClientId, this, _seed + seat * 31);
                PublicNames[seat] = _seats[seat].DisplayName;
                Humans[seat] = true;
            }

            var crew = Captains.All.Where(c => !PublicNames.Contains(c.Name)).ToList();
            for (int seat = 1; seat < seatCount; seat++)
            {
                if (_seats[seat] != null) continue;
                AiPersonality captain = crew[rng.Next(0, crew.Count)];
                crew.Remove(captain);
                var pirate = new AiPirate(captain.WithDifficulty(config.Difficulty), new SeededRandom(_seed + seat * 7919));
                _seats[seat] = new AiSeatController(pirate, this, _seed + seat);
                PublicNames[seat] = captain.Name;
            }

            FirstSeat = rng.Next(0, seatCount);
            _engine = new GameEngine(seatCount, config.Rules, rng, FirstSeat);
            SeatLastActions = new string[seatCount];
            LastAction = "Setting sail...";
        }

        /// <summary>The seat a remote player sits in, or -1.</summary>
        public int SeatOf(ulong clientId)
        {
            if (Mode != TableMode.OnlineHost || _seats == null) return -1;
            for (int i = 0; i < _seats.Length; i++)
                if (_seats[i] is NetworkSeatController remote && remote.ClientId == clientId) return i;
            return -1;
        }

        /// <summary>Hosting: a remote player's move. False if it isn't their turn or the move is illegal.</summary>
        public bool ReceiveRemoteMove(ulong clientId, SeatDecision decision)
        {
            int seat = SeatOf(clientId);
            return seat >= 0 && ((NetworkSeatController)_seats[seat]).Receive(decision);
        }

        /// <summary>Hosting: an AI pirate takes over the seat of a player who left. Returns the seat, or -1.</summary>
        public int ReplaceWithAi(ulong clientId, AiDifficulty difficulty)
        {
            int seat = SeatOf(clientId);
            if (seat < 0) return -1;
            string name = _seats[seat].DisplayName;
            var personality = Captains.All[seat % Captains.All.Length].WithDifficulty(difficulty);
            var pirate = new AiPirate(personality, new SeededRandom(_seed + seat * 104729));
            ReplaceSeat(seat, new AiSeatController(pirate, this, _seed + seat * 13, AiName(name)));
            Humans[seat] = false;
            SeatLastActions[seat] = "Left the table";
            SeatChanged?.Invoke(seat);
            return seat;
        }

        public static string AiName(string name) => $"{name} (AI)";

        void ReplaceSeat(int seat, ISeatController controller)
        {
            _seats[seat].CancelTurn();
            _seats[seat] = controller;
            if (_engine.Phase == TablePhase.Bidding && _engine.CurrentSeat == seat) RequestTurn();
            else Changed?.Invoke();
        }

        IEnumerator NextRoundSoon()
        {
            yield return new WaitForSeconds(RevealSeconds);
            _nextRound = null;
            BeginRound();
        }

        // ---------- mirroring the host's table ----------

        /// <summary>Joins the host's game as seen from this device. Moves the local player makes go to <paramref name="sendMove"/>.</summary>
        public void StartMirror(TableMirror mirror, Action<SeatDecision> sendMove)
        {
            StopTurns();
            Reset(TableMode.OnlinePlayer);
            _mirror = mirror;
            _sendMove = sendMove;
            _seats = new ISeatController[mirror.SeatCount];
            _seats[LocalSeat] = new LocalSeatController("You");
            for (int seat = 1; seat < mirror.SeatCount; seat++)
                _seats[seat] = new MirrorSeatController(mirror.NameOf(seat));
            _engine = mirror.CreateEngine();
            SeatLastActions = new string[mirror.SeatCount];
            LastAction = "The captain is dealing...";
            Changed?.Invoke();
        }

        public void MirrorRound(RoundMessage round)
        {
            if (!IsMirror) return;
            try
            {
                _mirror.PrepareRound(_engine, round);
            }
            catch (Exception e)
            {
                Desync(e.Message);
                return;
            }
            BeginRound();
        }

        public void MirrorPlay(PlayMessage play)
        {
            if (!IsMirror) return;
            SeatDecision decision;
            int seat;
            try
            {
                if (_engine.Phase != TablePhase.Bidding) throw new InvalidOperationException("A move arrived between rounds.");
                decision = _mirror.PrepareMove(_engine, play, out seat);
                if (seat != _engine.CurrentSeat) throw new InvalidOperationException("A move arrived for a seat whose turn it isn't.");
            }
            catch (Exception e)
            {
                Desync(e.Message);
                return;
            }
            OnDecision(_turnToken, seat, decision);
        }

        public void MirrorSeat(SeatMessage message)
        {
            if (!IsMirror) return;
            int seat;
            try
            {
                seat = _mirror.ApplySeat(message);
            }
            catch (Exception e)
            {
                Desync(e.Message);
                return;
            }
            if (_seats[seat] is MirrorSeatController mirrorSeat)
                mirrorSeat.DisplayName = message.Human ? message.Name : AiName(message.Name);
            if (!message.Human) SeatLastActions[seat] = "Left the table";
            SeatChanged?.Invoke(seat);
            Changed?.Invoke();
        }

        /// <summary>Mirroring: the host refused our move, so ask the local player again.</summary>
        public void RetryLocalTurn()
        {
            if (IsMirror && _engine.Phase == TablePhase.Bidding && _engine.CurrentSeat == LocalSeat) RequestTurn();
        }

        void Desync(string why)
        {
            Debug.LogError($"Online table out of step with the host: {why}");
            StopTurns();
            Desynced?.Invoke(why);
        }

        // ---------- rounds and turns ----------

        /// <summary>
        /// Deals the next round. Online, only the host deals the first round this way; after that it deals by
        /// itself once each reveal has been shown, and players are dealt by the host's messages.
        /// </summary>
        public void NextRound()
        {
            if (IsMirror || (IsOnline && (_engine == null || _engine.RoundNumber > 0))) return;
            BeginRound();
        }

        void BeginRound()
        {
            if (_engine == null || _engine.Phase == TablePhase.GameOver) return;
            if (Tutorial != null)
            {
                if (_tutorialRound >= Tutorial.Rounds.Count - 1) return;
                _tutorialRound++;
                _tutorialMove = 0;
            }
            _engine.StartRound();
            for (int i = 0; i < SeatLastActions.Length; i++) SeatLastActions[i] = null;
            ISeatController opener = _seats[_engine.CurrentSeat];
            LastAction = _engine.IsPalificoRound
                ? $"Palifico round! No wild 1s. {opener.DisplayName} opens."
                : $"Round {_engine.RoundNumber}. {opener.DisplayName} opens the bidding.";
            RoundStarted?.Invoke();
            RequestTurn();
        }

        public void StopTurns()
        {
            _turnToken++;
            if (_nextRound != null) StopCoroutine(_nextRound);
            _nextRound = null;
            if (_seats == null) return;
            foreach (ISeatController seat in _seats) seat.CancelTurn();
        }

        void RequestTurn()
        {
            Changed?.Invoke();
            int seat = _engine.CurrentSeat;
            int token = ++_turnToken;
            Action<SeatDecision> decide;
            if (!IsMirror) decide = decision => OnDecision(token, seat, decision);
            else if (seat == LocalSeat) decide = decision => _sendMove?.Invoke(decision);
            else decide = _ => { };
            _seats[seat].BeginTurn(_engine.ViewFor(seat), decide);
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
                if (IsMirror)
                {
                    Desync($"{name}'s move ({decision}) was illegal here: {e.Message}");
                    return;
                }
                Debug.LogError($"{name} made an illegal move ({decision}): {e.Message}");
                RequestTurn();
                return;
            }

            // Online, the host may have moved for this seat (a stand-in after a timeout), so close its turn.
            _seats[seat].CancelTurn();
            if (Tutorial != null) _tutorialMove++;
            MoveApplied?.Invoke(seat, decision, result);

            if (result == null)
            {
                SeatLastActions[seat] = $"Bid {decision.Bid}";
                LastAction = $"{name} bids {decision.Bid}.";
                BidPlaced?.Invoke(seat, decision.Bid);
                RequestTurn();
                return;
            }

            SeatLastActions[seat] = decision.Kind == DecisionKind.Liar ? "Liar!" : "Spot on!";
            LastAction = $"{name} calls {SeatLastActions[seat]}";
            if (Mode == TableMode.OnlineHost && _engine.Phase == TablePhase.RoundOver) _nextRound = StartCoroutine(NextRoundSoon());
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
