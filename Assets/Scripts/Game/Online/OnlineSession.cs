using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PiratesDice.Online;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace PiratesDice.Game.Online
{
    /// <summary>An open table someone can join from the list.</summary>
    public sealed class OpenTable
    {
        public string Id;
        public string Name;
        public int Players;
        public int MaxPlayers;
    }

    /// <summary>
    /// The connection to Unity's online services: signing in, hosting or joining a table, and carrying table
    /// messages between the host and players. Connections go through Unity's relay servers (WebSockets on the
    /// web), so nobody needs to open ports. Everything above this class speaks <see cref="NetMessage"/>.
    /// </summary>
    public sealed class OnlineSession : MonoBehaviour
    {
        const string SessionType = "pirates-dice-table";

        NetworkManager _network;
        ISession _session;
        bool _handlersRegistered;
        bool _leaving;

        public static OnlineSession Instance { get; private set; }

        public bool InTable => _session != null;
        public bool IsHost => _network != null && _network.IsHost;
        public string Code => _session?.Code ?? "";
        public ulong LocalClientId => _network != null ? _network.LocalClientId : 0;
        public const ulong HostClientId = NetworkManager.ServerClientId;

        /// <summary>A message arrived from <c>sender</c> (the host is <see cref="HostClientId"/>).</summary>
        public event Action<ulong, NetMessage> MessageReceived;
        /// <summary>Host only: a player's connection opened.</summary>
        public event Action<ulong> PlayerConnected;
        /// <summary>Host only: a player's connection closed.</summary>
        public event Action<ulong> PlayerDisconnected;
        /// <summary>Player only: this device is connected to the host and can send.</summary>
        public event Action ConnectedToHost;
        /// <summary>This device lost its table, with a reason to show (null when it left on purpose).</summary>
        public event Action<string> Closed;

        public static OnlineSession Ensure(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Online");
            go.transform.SetParent(parent, false);
            return go.AddComponent<OnlineSession>();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- signing in ----------

        static Task _signIn;

        /// <summary>Starts Unity Services and signs in anonymously. Safe to call repeatedly.</summary>
        public static Task SignInAsync() => _signIn != null && !_signIn.IsFaulted ? _signIn : _signIn = SignIn();

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
#if UNITY_EDITOR
                // Each Editor or clone gets its own player, so one machine can test a whole table.
                options.SetProfile("editor" + UnityEngine.Random.Range(0, 1_000_000));
#endif
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // ---------- hosting and joining ----------

        public async Task HostAsync(string tableName)
        {
            await SignInAsync();
            PrepareNetwork();
            var options = new SessionOptions
            {
                Name = tableName,
                MaxPlayers = Core.RuleSettings.MaxSeats,
                Type = SessionType,
            }.WithRelayNetwork();
            _session = await MultiplayerService.Instance.CreateSessionAsync(options);
            RegisterHandlers();
        }

        public async Task JoinByCodeAsync(string code)
        {
            await SignInAsync();
            PrepareNetwork();
            _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), new JoinSessionOptions { Type = SessionType });
            RegisterHandlers();
        }

        public async Task JoinByIdAsync(string id)
        {
            await SignInAsync();
            PrepareNetwork();
            _session = await MultiplayerService.Instance.JoinSessionByIdAsync(id, new JoinSessionOptions { Type = SessionType });
            RegisterHandlers();
        }

        /// <summary>Tables that have a free seat and haven't set sail yet.</summary>
        public static async Task<List<OpenTable>> ListOpenTablesAsync()
        {
            await SignInAsync();
            var query = new QuerySessionsOptions
            {
                Count = 10,
                FilterOptions = new List<FilterOption>
                {
                    new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
                },
                SortOptions = new List<SortOption> { new SortOption(SortOrder.Descending, SortField.CreationTime) },
            };
            QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(query);
            var tables = new List<OpenTable>();
            foreach (ISessionInfo info in results.Sessions)
                tables.Add(new OpenTable { Id = info.Id, Name = info.Name, MaxPlayers = info.MaxPlayers, Players = info.MaxPlayers - info.AvailableSlots });
            return tables;
        }

        /// <summary>Host only: hides the table from the list once it sets sail, so nobody joins mid-game.</summary>
        public async Task LockAsync(bool locked)
        {
            if (!(_session is IHostSession host)) return;
            try
            {
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't {(locked ? "lock" : "unlock")} the table: {e.Message}");
            }
        }

        /// <summary>Leaves the table. When the host leaves, the table closes for everyone.</summary>
        public async Task LeaveAsync()
        {
            if (_leaving) return;
            _leaving = true;
            ISession session = _session;
            _session = null;
            UnregisterHandlers();
            try
            {
                if (session != null) await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leaving the table: {e.Message}");
            }
            finally
            {
                if (_network != null && _network.IsListening && !_network.ShutdownInProgress) _network.Shutdown();
                _leaving = false;
            }
        }

        // ---------- messages ----------

        /// <summary>Player to host.</summary>
        public void SendToHost(NetMessage message) => Send(HostClientId, message);

        /// <summary>Host to one player.</summary>
        public void Send(ulong clientId, NetMessage message)
        {
            if (_network == null || !_network.IsListening || _network.CustomMessagingManager == null) return;
            string line = message.Encode();
            using (var writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(line) + 16, Allocator.Temp))
            {
                writer.WriteValueSafe(line);
                _network.CustomMessagingManager.SendNamedMessage(Protocol.Channel, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        /// <summary>Host to every connected player except itself.</summary>
        public void SendToPlayers(NetMessage message)
        {
            foreach (ulong id in ConnectedPlayers()) Send(id, message);
        }

        public List<ulong> ConnectedPlayers()
        {
            var ids = new List<ulong>();
            if (_network == null || !_network.IsServer) return ids;
            foreach (ulong id in _network.ConnectedClientsIds)
                if (id != _network.LocalClientId) ids.Add(id);
            return ids;
        }

        void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            NetMessage message;
            try
            {
                reader.ReadValueSafe(out string line);
                message = NetMessage.Decode(line);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Ignored a garbled table message from {sender}: {e.Message}");
                return;
            }
            // Only the host speaks to players; ignore anything else that claims to.
            if (!IsHost && sender != HostClientId) return;
            MessageReceived?.Invoke(sender, message);
        }

        // ---------- network plumbing ----------

        void PrepareNetwork()
        {
            if (_network != null) return;
            var go = new GameObject("Network");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
#if UNITY_WEBGL && !UNITY_EDITOR
            transport.UseWebSockets = true;
#endif
            _network = go.AddComponent<NetworkManager>();
            if (_network.NetworkConfig == null) _network.NetworkConfig = new NetworkConfig();
            _network.NetworkConfig.NetworkTransport = transport;
            _network.NetworkConfig.EnableSceneManagement = false;
            _network.NetworkConfig.ConnectionApproval = false;
            go.SetActive(true);

            _network.OnClientStarted += RegisterHandlers;
            _network.OnClientConnectedCallback += OnClientConnected;
            _network.OnClientDisconnectCallback += OnClientDisconnected;
            _network.OnClientStopped += OnStopped;
            _network.OnTransportFailure += OnTransportFailure;
        }

        void RegisterHandlers()
        {
            if (_handlersRegistered || _network == null || _network.CustomMessagingManager == null) return;
            _network.CustomMessagingManager.RegisterNamedMessageHandler(Protocol.Channel, OnNamedMessage);
            _handlersRegistered = true;
        }

        void UnregisterHandlers()
        {
            if (!_handlersRegistered) return;
            _handlersRegistered = false;
            if (_network != null && _network.CustomMessagingManager != null)
                _network.CustomMessagingManager.UnregisterNamedMessageHandler(Protocol.Channel);
        }

        void OnClientConnected(ulong clientId)
        {
            RegisterHandlers();
            if (_network.IsServer)
            {
                if (clientId != _network.LocalClientId) PlayerConnected?.Invoke(clientId);
            }
            else if (clientId == _network.LocalClientId)
            {
                ConnectedToHost?.Invoke();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (_network.IsServer)
            {
                if (clientId != _network.LocalClientId) PlayerDisconnected?.Invoke(clientId);
            }
            else if (clientId == _network.LocalClientId || clientId == HostClientId)
            {
                LostTable("Lost the connection to the captain's table.");
            }
        }

        void OnStopped(bool wasHost)
        {
            _handlersRegistered = false;
            if (_session != null && !_leaving) LostTable("The connection to the table closed.");
        }

        void OnTransportFailure() => LostTable("The connection failed.");

        void LostTable(string reason)
        {
            if (_session == null || _leaving) return;
            _ = LeaveAsync();
            Closed?.Invoke(reason);
        }
    }
}
