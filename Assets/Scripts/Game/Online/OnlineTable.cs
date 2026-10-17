using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PiratesDice.Core;
using PiratesDice.Online;
using UnityEngine;

namespace PiratesDice.Game.Online
{
    /// <summary>
    /// One online table from this device's side, from gathering the crew to the last reveal. The host seats
    /// everyone, fills empty seats with AI pirates, runs the game, and tells each player only what they may
    /// know. A player mirrors the host's game and sends their own moves. If a player leaves mid-game, an AI
    /// pirate takes their seat.
    /// </summary>
    public sealed class OnlineTable
    {
        public enum Stage { Idle, Lobby, Playing }

        const string NameKey = "PiratesDice.PlayerName";
        static readonly string[] DefaultNames = { "Captain Gull", "Red Anne", "Peg-Leg Ned", "Bosun Kit", "Long Meg", "Scurvy Sam" };

        readonly OnlineSession _session;
        readonly TableController _table;
        readonly List<RemotePlayer> _crew = new List<RemotePlayer>();
        GameConfig _config;
        bool _tableEventsHooked;

        public Stage CurrentStage { get; private set; }
        public bool IsHost { get; private set; }
        public string MyName { get; private set; }
        public string Code => _session.Code;
        /// <summary>Everyone aboard, the captain first.</summary>
        public IReadOnlyList<string> CrewNames { get; private set; } = new string[0];
        /// <summary>Seats at the table; AI pirates fill the ones the crew doesn't.</summary>
        public int TableSize { get; private set; }

        /// <summary>Who's aboard or the table size changed.</summary>
        public event Action LobbyChanged;
        /// <summary>A game began (or began again); show the table.</summary>
        public event Action GameStarted;
        /// <summary>This device is no longer at the table, with a reason to show (null if it left on purpose).</summary>
        public event Action<string> Ended;

        public OnlineTable(OnlineSession session, TableController table)
        {
            _session = session;
            _table = table;
            _session.MessageReceived += OnMessage;
            _session.PlayerDisconnected += OnPlayerDisconnected;
            _session.ConnectedToHost += () => _session.SendToHost(new HelloMessage(Protocol.Version, MyName));
            _session.Closed += reason => Finish(reason);
            _table.Desynced += _ => Leave("Lost track of the table, sorry. Try joining again.");
        }

        public static string SavedName
        {
            get
            {
                string name = PlayerPrefs.GetString(NameKey, "");
                return name.Length > 0 ? name : DefaultNames[UnityEngine.Random.Range(0, DefaultNames.Length)];
            }
            set => PlayerPrefs.SetString(NameKey, Protocol.CleanName(value));
        }

        // ---------- getting to a table ----------

        public async Task HostAsync(GameConfig config, string name)
        {
            Begin(config, name, host: true);
            TableSize = Math.Max(2, config.SeatCount);
            try
            {
                await _session.HostAsync($"{MyName}'s table");
            }
            catch
            {
                CurrentStage = Stage.Idle;
                throw;
            }
            RefreshCrew();
        }

        public Task JoinByCodeAsync(GameConfig config, string name, string code) => Join(config, name, () => _session.JoinByCodeAsync(code));

        public Task JoinByIdAsync(GameConfig config, string name, string id) => Join(config, name, () => _session.JoinByIdAsync(id));

        async Task Join(GameConfig config, string name, Func<Task> join)
        {
            Begin(config, name, host: false);
            CrewNames = new string[0];
            try
            {
                await join();
            }
            catch
            {
                CurrentStage = Stage.Idle;
                throw;
            }
            LobbyChanged?.Invoke();
        }

        void Begin(GameConfig config, string name, bool host)
        {
            _config = config;
            MyName = Protocol.CleanName(name);
            SavedName = MyName;
            IsHost = host;
            _crew.Clear();
            CurrentStage = Stage.Lobby;
        }

        /// <summary>Leaves the table. A host closes it for everyone.</summary>
        public void Leave(string reason = null)
        {
            if (CurrentStage == Stage.Idle) return;
            if (IsHost) _session.SendToPlayers(new EndMessage("The captain has left the table."));
            _ = _session.LeaveAsync();
            Finish(reason);
        }

        void Finish(string reason)
        {
            if (CurrentStage == Stage.Idle) return;
            CurrentStage = Stage.Idle;
            _crew.Clear();
            _table.StopTurns();
            Ended?.Invoke(reason);
        }

        // ---------- the captain's controls ----------

        public int MinTableSize => Math.Max(RuleSettings.MinSeats, _crew.Count + 1);

        public void ChangeTableSize(int delta)
        {
            if (!IsHost || CurrentStage != Stage.Lobby) return;
            TableSize = Mathf.Clamp(TableSize + delta, MinTableSize, RuleSettings.MaxSeats);
            RefreshCrew();
        }

        /// <summary>Host: seats the crew and deals the first round. Also starts a fresh game after one ends.</summary>
        public void SetSail()
        {
            if (!IsHost || CurrentStage == Stage.Idle) return;
            CurrentStage = Stage.Playing;
            HookTableEvents();
            _ = _session.LockAsync(true);

            var config = new GameConfig
            {
                PlayerName = MyName,
                AiOpponents = Math.Max(TableSize, MinTableSize) - 1,
                Difficulty = _config.Difficulty,
                Rules = _config.Rules.Clone(),
            };
            _table.StartOnlineHost(config, MyName, _crew);
            for (int seat = 1; seat < _table.Seats.Count; seat++)
            {
                if (!(_table.Seats[seat] is NetworkSeatController remote)) continue;
                _session.Send(remote.ClientId, HostMessages.StartFor(seat, _table.Seats.Count, _table.FirstSeat,
                    _table.Engine.Rules, _table.PublicNames, _table.Humans));
            }
            GameStarted?.Invoke();
            _table.NextRound();
        }

        void HookTableEvents()
        {
            if (_tableEventsHooked) return;
            _tableEventsHooked = true;
            _table.RoundStarted += () =>
            {
                if (!IsHost || CurrentStage != Stage.Playing || _table.Mode != TableMode.OnlineHost) return;
                for (int seat = 1; seat < _table.Seats.Count; seat++)
                    if (_table.Seats[seat] is NetworkSeatController remote)
                        _session.Send(remote.ClientId, HostMessages.RoundFor(_table.Engine, seat));
            };
            _table.MoveApplied += (seat, decision, reveal) =>
            {
                if (!IsHost || CurrentStage != Stage.Playing || _table.Mode != TableMode.OnlineHost) return;
                _session.SendToPlayers(HostMessages.PlayFor(seat, decision, reveal));
            };
        }

        void RefreshCrew()
        {
            CrewNames = new[] { MyName }.Concat(_crew.Select(c => c.Name)).ToArray();
            TableSize = Mathf.Clamp(TableSize, MinTableSize, RuleSettings.MaxSeats);
            if (IsHost) _session.SendToPlayers(new LobbyMessage(CrewNames.ToArray(), TableSize));
            LobbyChanged?.Invoke();
        }

        // ---------- messages ----------

        void OnMessage(ulong sender, NetMessage message)
        {
            if (CurrentStage == Stage.Idle) return;
            if (IsHost) OnMessageAsHost(sender, message);
            else OnMessageAsPlayer(message);
        }

        void OnMessageAsHost(ulong sender, NetMessage message)
        {
            switch (message)
            {
                case HelloMessage hello:
                    if (hello.Version != Protocol.Version)
                        _session.Send(sender, new RejectMessage("This table runs a different version of the game. Update and try again."));
                    else if (CurrentStage == Stage.Playing)
                        _session.Send(sender, new RejectMessage("This table has already set sail."));
                    else if (_crew.Count + 1 >= RuleSettings.MaxSeats)
                        _session.Send(sender, new RejectMessage("This table is full."));
                    else if (_crew.All(c => c.ClientId != sender))
                    {
                        _crew.Add(new RemotePlayer(sender, UniqueName(hello.Name)));
                        RefreshCrew();
                    }
                    break;

                case MoveMessage move:
                    if (CurrentStage == Stage.Playing && !_table.ReceiveRemoteMove(sender, move.Decision))
                        _session.Send(sender, new NackMessage());
                    break;
            }
        }

        string UniqueName(string name)
        {
            // Names stay distinct from each other and from the AI captains, so the table reads clearly.
            var taken = new HashSet<string>(new[] { MyName }.Concat(_crew.Select(c => c.Name)).Concat(AI.Captains.All.Select(c => c.Name)));
            if (!taken.Contains(name)) return name;
            string stem = name.Length > Protocol.MaxNameLength - 2 ? name.Substring(0, Protocol.MaxNameLength - 2) : name;
            for (int i = 2; ; i++)
                if (!taken.Contains($"{stem} {i}")) return $"{stem} {i}";
        }

        void OnPlayerDisconnected(ulong clientId)
        {
            if (!IsHost || CurrentStage == Stage.Idle) return;
            int index = _crew.FindIndex(c => c.ClientId == clientId);
            if (index >= 0) _crew.RemoveAt(index);

            if (CurrentStage == Stage.Lobby)
            {
                RefreshCrew();
                return;
            }
            string name = _table.SeatOf(clientId) >= 0 ? _table.Seats[_table.SeatOf(clientId)].DisplayName : null;
            int seat = _table.ReplaceWithAi(clientId, _config.Difficulty);
            if (seat >= 0) _session.SendToPlayers(new SeatMessage(seat, false, name));
        }

        void OnMessageAsPlayer(NetMessage message)
        {
            switch (message)
            {
                case LobbyMessage lobby:
                    CrewNames = lobby.Names;
                    TableSize = lobby.TableSize;
                    LobbyChanged?.Invoke();
                    break;

                case RejectMessage reject:
                    Leave(reject.Reason);
                    break;

                case EndMessage end:
                    Leave(end.Reason);
                    break;

                case StartMessage start:
                    CurrentStage = Stage.Playing;
                    _table.StartMirror(new TableMirror(start), decision => _session.SendToHost(new MoveMessage(decision)));
                    GameStarted?.Invoke();
                    break;

                case RoundMessage round:
                    if (CurrentStage == Stage.Playing) _table.MirrorRound(round);
                    break;

                case PlayMessage play:
                    if (CurrentStage == Stage.Playing) _table.MirrorPlay(play);
                    break;

                case SeatMessage seat:
                    if (CurrentStage == Stage.Playing) _table.MirrorSeat(seat);
                    break;

                case NackMessage _:
                    _table.RetryLocalTurn();
                    break;
            }
        }

        /// <summary>A short reason a service call failed, fit to show a player.</summary>
        public static string Describe(Exception e)
        {
            Exception inner = e;
            while (inner is AggregateException && inner.InnerException != null) inner = inner.InnerException;
            string text = inner.Message;
            if (string.IsNullOrWhiteSpace(text)) text = inner.GetType().Name;
            return text.Length > 160 ? text.Substring(0, 160) + "..." : text;
        }
    }
}
