using System.Collections.Generic;
using System.Linq;
using Bajoterra.Client.Input;
using Bajoterra.Client.Session;
using Bajoterra.Client.Transport;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class GameClientTests
    {
        private LoopbackClientTransport _transport;
        private GameClient _client;
        private readonly List<ClientPhase> _phases = new List<ClientPhase>();

        [SetUp]
        public void SetUp()
        {
            _transport = new LoopbackClientTransport();
            _client = new GameClient(_transport, "Eli");
            _phases.Clear();
            _client.PhaseChanged += _phases.Add;
        }

        private void Deliver(ServerMessage message)
        {
            _transport.Deliver(message);
        }

        private void EnterMatch(double now = 1.0)
        {
            _client.Connect("localhost", 7777);
            _client.Update(now);
            Deliver(TestContent.Wrap(TestContent.Welcome()));
            _client.Update(now);
            Deliver(TestContent.Wrap(new RoomState { RoomId = "arena" }));
            Deliver(TestContent.Wrap(TestContent.MatchStart()));
            _client.Update(now);
        }

        [Test]
        public void ConnectingSendsHelloWithTheProtocolVersionAndName()
        {
            _client.Connect("localhost", 7777);

            Assert.AreEqual(ClientPhase.Connecting, _client.Phase);
            ClientMessage hello = _transport.Sent.Single();
            Assert.AreEqual(ClientMessage.BodyOneofCase.Hello, hello.BodyCase);
            Assert.AreEqual(GameClient.ProtocolVersion, hello.Hello.ProtocolVersion);
            Assert.AreEqual("Eli", hello.Hello.PlayerName);
        }

        [Test]
        public void TheWelcomeUnlocksTheLobbyAndExposesTheContent()
        {
            _client.Connect("localhost", 7777);
            _client.Update(0.0);
            Welcome received = null;
            _client.Welcomed += w => received = w;

            Deliver(TestContent.Wrap(TestContent.Welcome()));
            _client.Update(0.1);

            Assert.AreEqual(ClientPhase.Lobby, _client.Phase);
            Assert.IsNotNull(received);
            Assert.AreEqual(2, _client.Content.SlugCount);
            Assert.AreEqual("Eli Shane", _client.Content.FindCharacter("eli").Name);
            CollectionAssert.AreEqual(
                new[] { ClientPhase.Connecting, ClientPhase.Greeting, ClientPhase.Lobby }, _phases);
        }

        [Test]
        public void ARoomStateMovesFromTheLobbyIntoTheRoom()
        {
            _client.Connect("localhost", 7777);
            _client.Update(0.0);
            Deliver(TestContent.Wrap(TestContent.Welcome()));
            _client.Update(0.1);
            RoomState seen = null;
            _client.RoomUpdated += r => seen = r;

            Deliver(TestContent.Wrap(new RoomState { RoomId = "arena", MatchRunning = false }));
            _client.Update(0.2);

            Assert.AreEqual(ClientPhase.InRoom, _client.Phase);
            Assert.AreEqual("arena", seen.RoomId);
        }

        [Test]
        public void AMismatchedProtocolVersionDisconnects()
        {
            string reason = null;
            _client.Disconnected += r => reason = r;
            _client.Connect("localhost", 7777);
            _client.Update(0.0);

            Deliver(TestContent.Wrap(TestContent.Welcome(version: 9)));
            _client.Update(0.1);

            Assert.AreEqual(ClientPhase.Disconnected, _client.Phase);
            StringAssert.Contains("protocol 9", reason);
            Assert.AreEqual(ConnectionState.Disconnected, _transport.State);
        }

        [Test]
        public void LobbyCommandsAreSentAsTheRightMessages()
        {
            _client.Connect("localhost", 7777);
            _transport.Sent.Clear();

            _client.JoinRoom("arena", 1);
            _client.SelectLoadout("eli", new[] { "doc", "infurnus" });
            _client.SetReady(true);

            Assert.AreEqual("arena", _transport.Sent[0].JoinRoom.RoomId);
            Assert.AreEqual(1, _transport.Sent[0].JoinRoom.Team);
            Assert.AreEqual("eli", _transport.Sent[1].SelectLoadout.CharacterId);
            CollectionAssert.AreEqual(new[] { "doc", "infurnus" }, _transport.Sent[1].SelectLoadout.SlugIds);
            Assert.IsTrue(_transport.Sent[2].Ready.Ready_);
        }

        [Test]
        public void MatchStartCreatesTheSessionWithTheMapAndTheCharacters()
        {
            MatchSession started = null;
            _client.MatchStarted += m => started = m;

            EnterMatch();

            Assert.AreEqual(ClientPhase.InMatch, _client.Phase);
            Assert.AreSame(_client.Match, started);
            Assert.AreEqual(0, started.YourId);
            Assert.AreEqual(2, started.Map.Obstacles.Length);
            Assert.AreEqual(6, started.Slots.Count);
            Assert.AreEqual(0, started.YourTeam);
        }

        [Test]
        public void InputIsSentWithIncreasingSequenceNumbersAndPredictedLocally()
        {
            EnterMatch();
            _client.Match.OnSnapshot(TestContent.Snapshot(100, new[] { TestContent.Combatant(0, -10f, 0f) },
                local: TestContent.GroundedLocal()), 1.0);
            _transport.Sent.Clear();
            PlayerInput input = PlayerInput.Idle;
            input.MoveZ = 1f;
            input.AimYaw = 0.5f;
            input.Fire = true;

            uint first = _client.SendInput(input);
            uint second = _client.SendInput(input);

            Assert.AreEqual(1u, first);
            Assert.AreEqual(2u, second);
            InputFrame frame = _transport.Sent[0].Input;
            Assert.AreEqual(1f, frame.MoveZ);
            Assert.AreEqual(0.5f, frame.AimYaw);
            Assert.IsTrue(frame.Fire);
            Assert.AreEqual(-1, frame.SelectSlot);
            Assert.AreEqual(2, _client.Match.Prediction.PendingInputs);
            Assert.Greater(_client.Match.Prediction.Current.Z, 0.0);
        }

        [Test]
        public void InputOutsideAMatchIsNotSent()
        {
            _client.Connect("localhost", 7777);
            _transport.Sent.Clear();

            Assert.AreEqual(0u, _client.SendInput(PlayerInput.Idle));
            Assert.IsEmpty(_transport.Sent);
        }

        [Test]
        public void SnapshotsEventsAndTheEndOfTheMatchAreRouted()
        {
            EnterMatch();
            var played = new List<EventKind>();
            _client.Match.EventPlayed += e => played.Add(e.Kind);
            MatchEnd end = null;
            _client.MatchEnded += e => end = e;

            Deliver(TestContent.Wrap(TestContent.Snapshot(120, new[] { TestContent.Combatant(0, 0f, 0f) },
                local: TestContent.GroundedLocal())));
            var batch = new EventBatch { Tick = 120 };
            batch.Events.Add(TestContent.Event(EventKind.Shot, 120, actor: 0));
            batch.Events.Add(TestContent.Event(EventKind.Damage, 120, actor: 3, subject: 1));
            Deliver(TestContent.Wrap(batch));
            _client.Update(2.0);

            Assert.AreEqual(1, _client.Match.Snapshots.Count);
            CollectionAssert.Contains(played, EventKind.Shot, "your own shot is shown at once");
            CollectionAssert.DoesNotContain(played, EventKind.Damage, "others wait for the render clock");

            _client.Update(2.2);
            CollectionAssert.Contains(played, EventKind.Damage);

            Deliver(new ServerMessage { MatchEnd = new MatchEnd { Winner = 0, WinsTeam0 = 3, WinsTeam1 = 1 } });
            _client.Update(3.0);

            Assert.AreEqual(ClientPhase.InRoom, _client.Phase);
            Assert.AreEqual(3u, end.WinsTeam0);
            Assert.IsTrue(_client.Match.Finished);
        }

        [Test]
        public void ServerErrorsAreExposed()
        {
            _client.Connect("localhost", 7777);
            ServerError seen = null;
            _client.ErrorReceived += e => seen = e;

            Deliver(new ServerMessage { Error = new ServerError { Code = 4, Message = "Invalid loadout" } });
            _client.Update(0.1);

            Assert.AreEqual(4u, seen.Code);
            Assert.AreSame(seen, _client.LastError);
        }

        [Test]
        public void ALostConnectionBecomesADisconnectedPhaseWithTheReason()
        {
            string reason = null;
            _client.Disconnected += r => reason = r;
            EnterMatch();

            _transport.Fail("connection reset");
            _client.Update(5.0);

            Assert.AreEqual(ClientPhase.Disconnected, _client.Phase);
            Assert.AreEqual("connection reset", reason);
            Assert.AreEqual("connection reset", _client.DisconnectReason);
        }

        [Test]
        public void PingsAreSentOncePerSecondAndTheRoundTripIsSmoothed()
        {
            _client.Connect("localhost", 7777);
            _client.Update(0.0);
            Deliver(TestContent.Wrap(TestContent.Welcome()));
            _transport.Sent.Clear();

            _client.Update(10.0);
            _client.Update(10.4);
            _client.Update(11.0);

            var pings = _transport.Sent.Where(m => m.BodyCase == ClientMessage.BodyOneofCase.Ping).ToList();
            Assert.AreEqual(2, pings.Count);
            Assert.AreEqual(10_000ul, pings[0].Ping.ClientTime);

            Deliver(new ServerMessage { Pong = new Pong { ClientTime = 10_000, ServerTick = 5 } });
            _client.Update(10.05);
            Assert.AreEqual(50, _client.PingMs);

            Deliver(new ServerMessage { Pong = new Pong { ClientTime = 11_000, ServerTick = 9 } });
            _client.Update(11.15);
            Assert.AreEqual(50 + (150 - 50) * 0.3, _client.PingMs, 1.0);
        }

        [Test]
        public void ReconnectingStartsFromACleanState()
        {
            EnterMatch();
            _client.Disconnect();

            _client.Connect("localhost", 7777);

            Assert.IsNull(_client.Match);
            Assert.IsNull(_client.Content);
            Assert.AreEqual(ClientPhase.Connecting, _client.Phase);
        }

        [Test]
        public void AMatchStartWithoutTheArenaIsRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new MatchSession(TestContent.Catalog(), new MatchStart { YourCombatantId = 0 }, 60));
        }
    }
}
