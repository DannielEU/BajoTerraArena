using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Client.Prediction;
using Bajoterra.Client.Session;
using Bajoterra.Client.Transport;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Integration
{
    /// <summary>
    /// The real C# client, over a real TCP socket, against the real Java server with its bots. These tests are the
    /// proof that the two halves of the game speak the same protocol and agree on movement.
    /// </summary>
    [Category("Integration")]
    [NonParallelizable]
    public class ServerIntegrationTests
    {
        private JavaServer _server;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private GameClient _client;

        [OneTimeSetUp]
        public void StartServer()
        {
            _server = JavaServer.Start();
        }

        [OneTimeTearDown]
        public void StopServer()
        {
            _server?.Dispose();
        }

        [TearDown]
        public void Disconnect()
        {
            _client?.Dispose();
            _client = null;
        }

        private double Now
        {
            get { return _clock.Elapsed.TotalSeconds; }
        }

        private GameClient NewClient(string name)
        {
            _client = new GameClient(new TcpClientTransport(), name);
            return _client;
        }

        /// <summary>Pumps the client until the condition holds; fails after the timeout.</summary>
        private bool WaitFor(Func<bool> condition, double timeoutSeconds, Action<double> perFrame = null)
        {
            double deadline = Now + timeoutSeconds;
            while (Now < deadline)
            {
                _client.Update(Now);
                perFrame?.Invoke(Now);
                if (condition())
                {
                    return true;
                }
                Thread.Sleep(2);
            }
            return false;
        }

        private void Join(string name, string room)
        {
            NewClient(name).Connect("127.0.0.1", _server.Port);
            Assert.IsTrue(WaitFor(() => _client.Phase == ClientPhase.Lobby, 5), "welcomed");
            _client.JoinRoom(room);
            _client.SelectLoadout("eli");
            _client.SetReady(true);
            Assert.IsTrue(WaitFor(() => _client.Phase == ClientPhase.InMatch, 5), "match started");
        }

        /// <summary>Sends input at 60 Hz while pumping the client for the given time.</summary>
        private void Play(double seconds, Func<double, PlayerInput> script, Action afterFrame = null)
        {
            var fixedClock = new FixedStepClock();
            double previous = Now;
            double end = previous + seconds;
            while (Now < end)
            {
                double now = Now;
                int steps = fixedClock.Advance(now - previous);
                previous = now;
                for (int i = 0; i < steps; i++)
                {
                    _client.SendInput(script(now));
                }
                _client.Update(now);
                afterFrame?.Invoke();
                Thread.Sleep(1);
            }
        }

        [Test, Timeout(30000)]
        public void TheHandshakeDeliversTheContentAndTheArena()
        {
            Join("Eli", "handshake");

            ContentCatalogCheck(_client);
            MatchSession match = _client.Match;
            Assert.AreEqual(6, match.Slots.Count);
            Assert.AreEqual("Eli", match.You.Name);
            Assert.AreEqual("eli", match.You.CharacterId);
            Assert.GreaterOrEqual(match.Map.Obstacles.Length, 15);
            Assert.AreEqual(8, match.Map.Obstacles.Count(o => o.IsRamp), "the hills' and balconies' ramps arrive with their direction");
            Assert.AreEqual(RampDirection.PosX, match.Map.Obstacles.First(o => o.Id == "ramp_n_w").Rise);
            Assert.AreEqual(RampDirection.NegZ, match.Map.Obstacles.First(o => o.Id == "ramp_w_n").Rise);
            Assert.AreEqual(8, match.Map.Pickups.Length, "7 pickups and the megamorph crystal");
            Assert.AreEqual(6, match.Map.Spawns.Length);
            Assert.AreEqual(-30f, match.Map.MinX);
            Assert.IsTrue(match.Map.Obstacles.Any(o => o.Id == "center_wall_n" && o.BlocksProjectiles));
            Assert.AreEqual(60, _client.Welcome.TickRate);
            Assert.AreEqual(20, _client.Welcome.SnapshotRate);
        }

        private static void ContentCatalogCheck(GameClient client)
        {
            Assert.GreaterOrEqual(client.Content.SlugCount, 14);
            Assert.AreEqual(5, client.Content.MoveSetOf(client.Content.FindCharacter("eli")).Length);
            Assert.AreEqual(6, client.Content.MoveSetOf(client.Content.FindCharacter("pronto")).Length);
            Assert.AreEqual("roll", client.Content.Move(client.Content.MoveIndexOf("roll")).Id);
            Assert.AreEqual("drive", client.Content.Move(client.Content.MoveIndexOf("roll")).BehaviorType);
            Assert.AreEqual("slide", client.Content.Move(client.Content.MoveIndexOf("slide")).BehaviorType);
        }

        [Test, Timeout(30000)]
        public void ARefusedProtocolVersionIsReportedByTheServer()
        {
            var transport = new TcpClientTransport();
            transport.Connect("127.0.0.1", _server.Port);
            transport.Send(new ClientMessage { Hello = new Hello { ProtocolVersion = 99, PlayerName = "old" } });

            ServerMessage reply = null;
            double deadline = Now + 5;
            while (Now < deadline && !transport.TryReceive(out reply))
            {
                Thread.Sleep(5);
            }

            Assert.IsNotNull(reply);
            Assert.AreEqual(ServerMessage.BodyOneofCase.Error, reply.BodyCase);
            Assert.AreEqual(1u, reply.Error.Code);
            transport.Dispose();
        }

        [Test, Timeout(30000)]
        public void TheClientCannotConnectToAClosedPort()
        {
            NewClient("Nobody").Connect("127.0.0.1", 1);

            Assert.IsTrue(WaitFor(() => _client.Phase == ClientPhase.Disconnected, 8));
            Assert.IsNotEmpty(_client.DisconnectReason);
        }

        [Test, Timeout(60000)]
        public void SnapshotsArriveAtTwentyHertzAndTheCountdownLeadsIntoAnActiveRound()
        {
            Join("Eli", "snapshots");
            int snapshots = 0;
            uint lastTick = 0;
            var phases = new HashSet<MatchPhase>();

            bool active = WaitFor(() =>
            {
                Snapshot latest = _client.Match.Latest;
                if (latest != null && latest.Tick != lastTick)
                {
                    snapshots++;
                    lastTick = latest.Tick;
                    phases.Add(latest.Match.Phase);
                }
                return latest != null && latest.Match.Phase == MatchPhase.RoundActive;
            }, 15);

            Assert.IsTrue(active, "the round became active after the countdown");
            Assert.Greater(snapshots, 30, "about 3 s of countdown at 20 snapshots per second");
            CollectionAssert.Contains(phases, MatchPhase.Countdown);
            Assert.IsTrue(_client.Match.Clock.IsSynchronized);
            Assert.AreEqual(6, _client.Match.Latest.Combatants.Count);
            Assert.IsNotNull(_client.Match.Latest.Local, "the owner receives its movement state");
            Assert.AreEqual(5, _client.Match.YourState.Slots.Count);
        }

        [Test, Timeout(90000)]
        public void PredictionStaysInStepWithTheServerWhileMovingJumpingAndRolling()
        {
            Join("Eli", "prediction");
            Assert.IsTrue(WaitFor(() => _client.Match.Latest != null
                                        && _client.Match.Latest.Match.Phase == MatchPhase.RoundActive, 15));
            MatchSession match = _client.Match;
            var mask = new MoveMask(_client.Content, _client.Content.FindCharacter("eli"));
            var errors = new List<double>();
            int reconciles = match.Prediction.ReconcileCount;
            int maxPending = 0;
            float yaw = 1.5707964f; // face the centre of the map (+X) from the left spawn

            Play(6.0, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                double t = now % 4.0;
                input.AimYaw = yaw + (float)Math.Sin(now) * 0.4f;
                input.MoveZ = t < 2.5 ? 1f : 0f;
                input.MoveX = t >= 1.0 && t < 3.0 ? 0.6f : 0f;
                if (t > 0.8 && t < 0.85)
                {
                    input.MoveMask |= mask.BitFor("jump");
                }
                if (t > 2.0 && t < 2.1)
                {
                    input.MoveMask |= mask.BitFor("roll");
                }
                if (t > 3.2 && t < 3.9)
                {
                    input.MoveMask |= mask.BitFor("crouch");
                }
                return input;
            }, () =>
            {
                if (match.Prediction.ReconcileCount != reconciles)
                {
                    reconciles = match.Prediction.ReconcileCount;
                    errors.Add(match.Prediction.LastError);
                }
                maxPending = Math.Max(maxPending, match.Prediction.PendingInputs);
            });

            Assert.Greater(errors.Count, 60, "about 20 reconciliations per second for 6 s");
            errors.Sort();
            double median = errors[errors.Count / 2];
            double p90 = errors[(int)(errors.Count * 0.9)];
            TestContext.WriteLine("reconciles=" + errors.Count + " median=" + median.ToString("0.0000")
                                  + " p90=" + p90.ToString("0.000") + " max=" + errors[errors.Count - 1].ToString("0.000")
                                  + " snaps=" + match.Prediction.SnapCount + " pending<=" + maxPending
                                  + " rtt=" + _client.PingMs + "ms");
            Assert.Less(median, 0.05, "the predicted body usually matches the server within 5 cm");
            Assert.Less(p90, 0.5, "and corrections stay small");
            Assert.LessOrEqual(match.Prediction.SnapCount, 1, "no teleports");
            Assert.Less(maxPending, 40, "the server acknowledges inputs promptly");
            Assert.IsTrue(match.Prediction.IsPredicting);
        }

        [Test, Timeout(90000)]
        public void FiringProducesShotEventsAndSlugsInFlightAndTheSlotLocksForThreeShots()
        {
            Join("Eli", "firing");
            Assert.IsTrue(WaitFor(() => _client.Match.Latest != null
                                        && _client.Match.Latest.Match.Phase == MatchPhase.RoundActive, 15));
            MatchSession match = _client.Match;
            int shots = 0;
            int slugsSeen = 0;
            int maxLocked = 0;
            match.EventPlayed += e =>
            {
                if (e.Kind == EventKind.Shot && e.Actor == match.YourId)
                {
                    shots++;
                }
            };

            Play(3.0, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                input.Fire = true;
                input.AimYaw = 1.5707964f;
                return input;
            }, () =>
            {
                if (match.Frame.Valid)
                {
                    slugsSeen = Math.Max(slugsSeen, match.Frame.ProjectileCount);
                }
                CombatantState you = match.YourState;
                if (you != null)
                {
                    maxLocked = Math.Max(maxLocked, you.Slots.Count(s => s.ShotsUntilReady > 0));
                }
            });

            Assert.GreaterOrEqual(shots, 4, "0.7 s between shots for 3 s");
            Assert.LessOrEqual(shots, 6, "and not faster than that");
            Assert.Greater(slugsSeen, 0, "slugs were drawn in flight");
            Assert.AreEqual(2, maxLocked, "a fired slug stays locked while two more shots are fired");
        }

        [Test, Timeout(90000)]
        public void SelectingASlotIsReflectedByTheServer()
        {
            Join("Eli", "slots");
            Assert.IsTrue(WaitFor(() => _client.Match.Latest != null, 10));
            MatchSession match = _client.Match;

            Play(1.0, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                input.SelectSlot = 3;
                return input;
            });

            Assert.IsTrue(WaitFor(() => match.YourState != null && match.YourState.SelectedSlot == 3, 3),
                "slot 3 is selected");
        }

        [Test, Timeout(60000)]
        public void AskingForADanceMakesYourHeroDanceUntilYouMove()
        {
            Join("Eli", "dance");
            Assert.IsTrue(WaitFor(() => _client.Match.Latest != null
                                        && _client.Match.Latest.Match.Phase == MatchPhase.RoundActive, 15));
            MatchSession match = _client.Match;
            bool asked = false;

            Play(0.6, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                input.Dance = asked ? 0u : 3u;
                asked = true;
                return input;
            });
            Assert.IsTrue(WaitFor(() => match.YourState != null && match.YourState.Dance == 3, 3), "dancing the third dance");

            Play(0.5, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                input.MoveZ = 1f;
                return input;
            });
            Assert.IsTrue(WaitFor(() => match.YourState != null && match.YourState.Dance == 0, 3), "moving stops it");
        }

        [Test, Timeout(60000)]
        public void WalkingForwardIntoTheArenaEndsWhereTheServerHasTheBody()
        {
            // Walk straight ahead for three seconds: the predicted body and the server's body must end up together, so
            // the arena and the movement rules the client works with really are the ones being simulated.
            Join("Eli", "walls");
            Assert.IsTrue(WaitFor(() => _client.Match.Latest != null
                                        && _client.Match.Latest.Match.Phase == MatchPhase.RoundActive, 15));
            MatchSession match = _client.Match;

            Play(3.0, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                input.MoveZ = 1f;
                input.AimYaw = 1.5707964f;
                return input;
            });

            CombatantState you = match.YourState;
            Assert.AreEqual(you.X, match.Prediction.Current.X, 0.6);
            Assert.AreEqual(you.Z, match.Prediction.Current.Z, 0.6);
        }

        [Test, Timeout(60000)]
        public void ClimbingTheWestHillByItsRampAgreesWithTheServer()
        {
            Join("Eli", "ramps");
            Assert.IsTrue(WaitFor(() => _client.Match.Latest != null
                                        && _client.Match.Latest.Match.Phase == MatchPhase.RoundActive, 15));
            MatchSession match = _client.Match;
            // The route: down to the low end of the south ramp (z = -9.5), across to x = -15.5, then straight
            // north over the ramp (z -8 to -3), the plateau (-3 to 3) and the north ramp, back to the ground.
            const float south = 3.1415927f;
            const float east = 1.5707964f;
            const float north = 0f;
            double maxHeight = 0.0;
            double maxPredictionGap = 0.0;
            var errors = new List<double>();
            int reconciles = match.Prediction.ReconcileCount;
            bool onTheGroundAgain = false;
            int stage = 0;

            Play(14.0, now =>
            {
                PlayerInput input = PlayerInput.Idle;
                PredictedBody body = match.Prediction.Current;
                if (stage == 0 && body.Z <= -9.4)
                {
                    stage = 1;
                }
                if (stage == 1 && body.X >= -15.6)
                {
                    stage = 2;
                }
                if (stage == 2 && body.Z > 9.0)
                {
                    stage = 3;
                }
                switch (stage)
                {
                    case 0:
                        input.AimYaw = south;
                        input.MoveZ = 1f;
                        break;
                    case 1:
                        input.AimYaw = east;
                        input.MoveZ = 1f;
                        break;
                    case 2:
                        input.AimYaw = north;
                        input.MoveZ = 1f;
                        break;
                    default:
                        input.AimYaw = north;
                        break;
                }
                return input;
            }, () =>
            {
                PredictedBody body = match.Prediction.Current;
                maxHeight = Math.Max(maxHeight, body.Y);
                if (stage == 3 && body.Grounded && body.Y < 0.05)
                {
                    onTheGroundAgain = true;
                }
                if (match.Prediction.ReconcileCount != reconciles)
                {
                    reconciles = match.Prediction.ReconcileCount;
                    errors.Add(match.Prediction.LastError);
                }
                CombatantState you = match.YourState;
                if (you != null)
                {
                    maxPredictionGap = Math.Max(maxPredictionGap, Math.Abs(you.Y - body.Y));
                }
            });

            errors.Sort();
            TestContext.WriteLine("stage=" + stage + " maxHeight=" + maxHeight.ToString("0.00") + " reconciles="
                                  + errors.Count + " median=" + (errors.Count > 0 ? errors[errors.Count / 2] : 0.0).ToString("0.0000")
                                  + " max=" + (errors.Count > 0 ? errors[errors.Count - 1] : 0.0).ToString("0.000")
                                  + " snaps=" + match.Prediction.SnapCount);
            Assert.AreEqual(3, stage, "the route was completed");
            Assert.GreaterOrEqual(maxHeight, 1.9, "stood on the 2 m plateau");
            Assert.IsTrue(onTheGroundAgain, "came back down to the ground by the far ramp");
            Assert.Less(errors[errors.Count / 2], 0.05, "the predicted body follows the slope like the server does");
            Assert.LessOrEqual(match.Prediction.SnapCount, 1, "no teleports while climbing");
            Assert.Less(maxPredictionGap, 0.6, "the predicted height stays within a latency's worth of slope of the server's");
        }
    }
}
