using System;
using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Client.Transport;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Session
{
    /// <summary>Where the client is in the connection and room flow.</summary>
    public enum ClientPhase
    {
        Disconnected,
        Connecting,

        /// <summary>Connected, <c>Hello</c> sent, waiting for the server's <c>Welcome</c>.</summary>
        Greeting,

        /// <summary>Welcomed; not in a room yet.</summary>
        Lobby,

        /// <summary>In a room, choosing a loadout and getting ready.</summary>
        InRoom,

        InMatch
    }

    /// <summary>
    /// The client's single entry point to the server (Facade over transport, protocol flow and match state).
    /// It is driven from the game thread: call <see cref="Update"/> every frame with a monotonic time in seconds,
    /// <see cref="SendInput"/> once per fixed tick, and react to the events. Nothing in here reads a clock.
    /// </summary>
    public sealed class GameClient : IDisposable
    {
        /// <summary>The protocol version this build speaks; must equal the server's.</summary>
        public const uint ProtocolVersion = 1;

        private const double PingIntervalSeconds = 1.0;

        private readonly IClientTransport _transport;
        private readonly string _playerName;
        private ClientPhase _phase = ClientPhase.Disconnected;
        private uint _sequence;
        private double _nextPing;
        private double _rttMs;

        public GameClient(IClientTransport transport, string playerName)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _playerName = string.IsNullOrWhiteSpace(playerName) ? "player" : playerName;
        }

        public ClientPhase Phase
        {
            get { return _phase; }
        }

        /// <summary>The server's greeting, or null before it arrives.</summary>
        public Welcome Welcome { get; private set; }

        /// <summary>The content announced by the server, or null before the welcome.</summary>
        public ContentCatalog Content { get; private set; }

        /// <summary>The newest lobby state, or null.</summary>
        public RoomState Room { get; private set; }

        /// <summary>The running match, or the one that just ended until the next starts.</summary>
        public MatchSession Match { get; private set; }

        /// <summary>The newest error the server reported, or null.</summary>
        public ServerError LastError { get; private set; }

        /// <summary>Why the connection ended, or empty.</summary>
        public string DisconnectReason { get; private set; } = string.Empty;

        /// <summary>Smoothed round-trip time in milliseconds; 0 until the first reply.</summary>
        public int PingMs
        {
            get { return (int)Math.Round(_rttMs); }
        }

        public event Action<ClientPhase> PhaseChanged;

        public event Action<Welcome> Welcomed;

        public event Action<RoomState> RoomUpdated;

        public event Action<MatchSession> MatchStarted;

        public event Action<MatchEnd> MatchEnded;

        public event Action<ServerError> ErrorReceived;

        public event Action<string> Disconnected;

        // ------------------------------------------------------------------------------------------ commands

        public void Connect(string host, int port)
        {
            DisconnectReason = string.Empty;
            LastError = null;
            Welcome = null;
            Content = null;
            Room = null;
            Match = null;
            _sequence = 0;
            _rttMs = 0.0;
            _nextPing = 0.0;
            SetPhase(ClientPhase.Connecting);
            _transport.Connect(host, port);
            // Queued by the transport until the socket is up.
            _transport.Send(new ClientMessage
            {
                Hello = new Hello { ProtocolVersion = ProtocolVersion, PlayerName = _playerName }
            });
        }

        /// <summary>Enters a room (created when it does not exist). Team 0 or 1, or -1 to be balanced.</summary>
        public void JoinRoom(string roomId, int team = -1)
        {
            _transport.Send(new ClientMessage { JoinRoom = new JoinRoom { RoomId = roomId, Team = team } });
        }

        /// <summary>Picks the character; with no slugs the server draws a balanced random loadout.</summary>
        public void SelectLoadout(string characterId, string[] slugIds = null)
        {
            var message = new SelectLoadout { CharacterId = characterId };
            if (slugIds != null)
            {
                message.SlugIds.AddRange(slugIds);
            }
            _transport.Send(new ClientMessage { SelectLoadout = message });
        }

        public void SetReady(bool ready)
        {
            _transport.Send(new ClientMessage { Ready = new Ready { Ready_ = ready } });
        }

        /// <summary>
        /// Sends one tick of input and applies it to the local prediction. Returns the sequence number it was
        /// sent under, or 0 when no match is running.
        /// </summary>
        public uint SendInput(in PlayerInput input)
        {
            if (_phase != ClientPhase.InMatch || Match == null || Match.Finished)
            {
                return 0;
            }
            uint sequence = ++_sequence;
            _transport.Send(new ClientMessage { Input = input.ToFrame(sequence) });
            Match.Prediction.Predict(sequence, input);
            return sequence;
        }

        public void Disconnect()
        {
            _transport.Close();
            if (_phase != ClientPhase.Disconnected)
            {
                SetPhase(ClientPhase.Disconnected);
            }
        }

        public void Dispose()
        {
            _transport.Dispose();
        }

        // ------------------------------------------------------------------------------------------ pump

        /// <summary>
        /// Processes everything the server sent since the last call and advances the match clocks.
        /// </summary>
        /// <param name="now">Monotonic time in seconds (for example Time.realtimeSinceStartupAsDouble).</param>
        public void Update(double now)
        {
            while (_transport.TryReceive(out ServerMessage message))
            {
                Handle(message, now);
            }
            CheckConnection();
            if (_phase == ClientPhase.Greeting || _phase == ClientPhase.Lobby || _phase == ClientPhase.InRoom
                || _phase == ClientPhase.InMatch)
            {
                if (now >= _nextPing)
                {
                    _nextPing = now + PingIntervalSeconds;
                    _transport.Send(new ClientMessage { Ping = new Ping { ClientTime = (ulong)(now * 1000.0) } });
                }
            }
            if (Match != null && !Match.Finished)
            {
                Match.Update(now);
            }
        }

        private void CheckConnection()
        {
            if (_phase == ClientPhase.Connecting && _transport.State == ConnectionState.Connected)
            {
                SetPhase(ClientPhase.Greeting);
            }
            if (_transport.State == ConnectionState.Failed && _phase != ClientPhase.Disconnected)
            {
                DisconnectReason = _transport.FailureReason;
                SetPhase(ClientPhase.Disconnected);
                Disconnected?.Invoke(DisconnectReason);
            }
        }

        private void Handle(ServerMessage message, double now)
        {
            switch (message.BodyCase)
            {
                case ServerMessage.BodyOneofCase.Welcome:
                    OnWelcome(message.Welcome);
                    break;
                case ServerMessage.BodyOneofCase.RoomState:
                    Room = message.RoomState;
                    if (_phase == ClientPhase.Lobby)
                    {
                        SetPhase(ClientPhase.InRoom);
                    }
                    RoomUpdated?.Invoke(Room);
                    break;
                case ServerMessage.BodyOneofCase.MatchStart:
                    OnMatchStart(message.MatchStart);
                    break;
                case ServerMessage.BodyOneofCase.Snapshot:
                    Match?.OnSnapshot(message.Snapshot, now);
                    break;
                case ServerMessage.BodyOneofCase.Events:
                    Match?.OnEvents(message.Events);
                    break;
                case ServerMessage.BodyOneofCase.MatchEnd:
                    Match?.OnMatchEnd();
                    SetPhase(ClientPhase.InRoom);
                    MatchEnded?.Invoke(message.MatchEnd);
                    break;
                case ServerMessage.BodyOneofCase.Error:
                    LastError = message.Error;
                    ErrorReceived?.Invoke(message.Error);
                    break;
                case ServerMessage.BodyOneofCase.Pong:
                    OnPong(message.Pong, now);
                    break;
            }
        }

        private void OnWelcome(Welcome welcome)
        {
            if (welcome.ProtocolVersion != ProtocolVersion)
            {
                DisconnectReason = "The server speaks protocol " + welcome.ProtocolVersion + ", this client "
                                   + ProtocolVersion;
                _transport.Close();
                SetPhase(ClientPhase.Disconnected);
                Disconnected?.Invoke(DisconnectReason);
                return;
            }
            Welcome = welcome;
            Content = new ContentCatalog(welcome.Content, welcome.Details);
            SetPhase(ClientPhase.Lobby);
            Welcomed?.Invoke(welcome);
        }

        private void OnMatchStart(MatchStart start)
        {
            if (Content == null)
            {
                return;
            }
            _sequence = 0;
            Match = new MatchSession(Content, start, (int)Welcome.TickRate);
            SetPhase(ClientPhase.InMatch);
            MatchStarted?.Invoke(Match);
        }

        private void OnPong(Pong pong, double now)
        {
            double rtt = Math.Max(0.0, now * 1000.0 - pong.ClientTime);
            _rttMs = _rttMs <= 0.0 ? rtt : _rttMs + (rtt - _rttMs) * 0.3;
        }

        private void SetPhase(ClientPhase phase)
        {
            if (_phase == phase)
            {
                return;
            }
            _phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
