using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Client.Prediction;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class PredictionTests
    {
        private ContentCatalog _content;
        private ArenaMap _map;
        private CharacterInfo _eli;

        [SetUp]
        public void SetUp()
        {
            ContentDetails details = TestContent.Details();
            _content = new ContentCatalog(TestContent.ManifestFor(details), details);
            _map = new ArenaMap(TestContent.Map());
            _eli = _content.FindCharacter("eli");
        }

        private PredictionController Controller()
        {
            return new PredictionController(_content, _map, _eli, 0);
        }

        private PredictedBody Body(double x, double z)
        {
            MoveInfo[] infos = _content.MoveSetOf(_eli);
            var rules = new MoveRule[infos.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = new MoveRule(infos[i]);
            }
            var body = new PredictedBody(_eli, rules) { X = x, Z = z };
            body.HitHeightNow = body.HitHeight;
            return body;
        }

        private static PlayerInput Forward(float yaw = 0f)
        {
            PlayerInput input = PlayerInput.Idle;
            input.MoveZ = 1f;
            input.AimYaw = yaw;
            return input;
        }

        /// <summary>A snapshot of the body as the server would send it, addressed to combatant 0.</summary>
        private Snapshot Server(PredictedBody body, uint ack, MatchPhase phase = MatchPhase.RoundActive)
        {
            TestContent.DescribeBody(body, _eli, 0, out CombatantState state, out LocalState local);
            return TestContent.Snapshot((uint)body.Tick, new[] { state }, phase, ack, local);
        }

        /// <summary>Starts a controller in an active round at (x, z) by feeding it one authoritative snapshot.</summary>
        private PredictionController Active(double x, double z, out PredictedBody server)
        {
            PredictionController controller = Controller();
            server = Body(x, z);
            server.Tick = 1000;
            controller.Reconcile(Server(server, 0));
            Assert.IsTrue(controller.IsPredicting);
            return controller;
        }

        [Test]
        public void WalkingForwardCoversSpeedTimesTime()
        {
            PredictedBody body = Body(-10, 0);

            for (int i = 0; i < 60; i++)
            {
                MovementModel.Step(body, Forward(), _map);
            }

            Assert.AreEqual(6.0, body.Z, 1e-6, "Eli walks at 6 m/s along +Z for one second");
            Assert.AreEqual(-10.0, body.X, 1e-6);
        }

        [Test]
        public void YawTurnsTheWalkingDirection()
        {
            PredictedBody body = Body(-10, 0);

            for (int i = 0; i < 30; i++)
            {
                MovementModel.Step(body, Forward((float)System.Math.PI / 2f), _map);
            }

            Assert.AreEqual(-7.0, body.X, 1e-4, "yaw pi/2 faces +X");
            Assert.AreEqual(0.0, body.Z, 1e-4);
        }

        [Test]
        public void DiagonalInputIsNotFasterThanStraight()
        {
            PredictedBody body = Body(-10, -10);
            PlayerInput diagonal = PlayerInput.Idle;
            diagonal.MoveX = 1f;
            diagonal.MoveZ = 1f;

            for (int i = 0; i < 60; i++)
            {
                MovementModel.Step(body, diagonal, _map);
            }

            Assert.AreEqual(6.0, System.Math.Sqrt((body.X + 10) * (body.X + 10) + (body.Z + 10) * (body.Z + 10)), 1e-4);
        }

        [Test]
        public void AWallStopsTheBody()
        {
            PredictedBody body = Body(0, 0);
            PlayerInput right = PlayerInput.Idle;
            right.MoveZ = 1f;
            right.AimYaw = (float)System.Math.PI / 2f;

            for (int i = 0; i < 120; i++)
            {
                MovementModel.Step(body, right, _map);
            }

            // The wall's near face is at x = 5; the body is a 0.4 m radius disc.
            Assert.AreEqual(5.0 - 0.4, body.X, 1e-6);
        }

        [Test]
        public void ALowBoxBlocksWalkingButNotAJump()
        {
            PredictedBody walker = Body(0, 5);
            PlayerInput forward = Forward();
            for (int i = 0; i < 60; i++)
            {
                MovementModel.Step(walker, forward, _map);
            }
            Assert.AreEqual(8.0 - 0.4, walker.Z, 1e-6, "stopped at the 1 m high box");

            PredictedBody jumper = Body(0, 5);
            PlayerInput jump = Forward();
            jump.MoveMask = 1;
            for (int i = 0; i < 90; i++)
            {
                MovementModel.Step(jumper, jump, _map);
                jump.MoveMask = 0;
            }
            Assert.Greater(jumper.Z, 9.4, "cleared the box");
        }

        [Test]
        public void AJumpRisesAndComesBackDownWithinTheExpectedTime()
        {
            PredictedBody body = Body(-10, 0);
            PlayerInput jump = PlayerInput.Idle;
            jump.MoveMask = 1;
            MovementModel.Step(body, jump, _map);
            double apex = body.Y;
            int airborneTicks = 1;
            jump.MoveMask = 0;

            while (!body.Grounded && airborneTicks < 200)
            {
                MovementModel.Step(body, jump, _map);
                apex = System.Math.Max(apex, body.Y);
                airborneTicks++;
            }

            // 20 ticks of (8.5 - 25 k / 60) / 60: the discrete integration peaks a little below v^2 / 2g = 1.445.
            Assert.AreEqual(1.375, apex, 1e-6);
            Assert.That(airborneTicks, Is.InRange(38, 44), "about 0.68 s in the air");
            Assert.AreEqual(0.0, body.Y, 1e-9);
        }

        [Test]
        public void ARollMovesAtRollSpeedAndLocksFurtherRollsUntilTheCooldownEnds()
        {
            PredictedBody body = Body(-10, 0);
            PlayerInput roll = Forward();
            roll.MoveMask = 4;

            MovementModel.Step(body, roll, _map);
            Assert.AreEqual(Posture.Action, body.Posture);
            Assert.AreEqual(2, body.ActiveSlot);

            for (int i = 0; i < 29; i++)
            {
                MovementModel.Step(body, roll, _map);
            }
            Assert.AreEqual(8.0 * 0.5, body.Z, 1e-6, "30 ticks at 8 m/s");

            MovementModel.Step(body, roll, _map);
            Assert.AreEqual(-1, body.ActiveSlot, "the roll ended after its 30 ticks and the cooldown blocks a new one");
            Assert.AreEqual(Posture.Grounded, body.Posture);
        }

        [Test]
        public void CrouchingHalvesTheSpeedAndShrinksTheHitbox()
        {
            PredictedBody body = Body(-10, 0);
            PlayerInput crouch = Forward();
            crouch.MoveMask = 2;

            for (int i = 0; i < 60; i++)
            {
                MovementModel.Step(body, crouch, _map);
            }

            Assert.AreEqual(3.0, body.Z, 1e-6);
            Assert.AreEqual(Posture.Crouching, body.Posture);
            Assert.AreEqual(1.8 * 0.6, body.HitHeightNow, 1e-6);
        }

        [Test]
        public void ASlowedBodyMovesSlower()
        {
            PredictedBody body = Body(-10, 0);
            body.StatusSpeed = 0.4;

            for (int i = 0; i < 60; i++)
            {
                MovementModel.Step(body, Forward(), _map);
            }

            Assert.AreEqual(2.4, body.Z, 1e-6);
        }

        [Test]
        public void AStunnedBodyCannotMoveOrStartMoves()
        {
            PredictedBody body = Body(-10, 0);
            body.Stunned = true;
            PlayerInput input = Forward();
            input.MoveMask = 1;

            for (int i = 0; i < 30; i++)
            {
                MovementModel.Step(body, input, _map);
            }

            Assert.AreEqual(0.0, body.Z, 1e-9);
            Assert.AreEqual(0.0, body.Y, 1e-9);
        }

        [Test]
        public void PredictingMovesTheBodyAtOnce()
        {
            PredictionController controller = Active(-10, 0, out _);

            controller.Predict(1, Forward());

            Assert.Greater(controller.Current.Z, 0.0);
            Assert.AreEqual(1, controller.PendingInputs);
        }

        [Test]
        public void ReconcilingReplaysTheInputsTheServerHasNotSeen()
        {
            PredictionController controller = Active(-10, 0, out PredictedBody server);
            for (uint seq = 1; seq <= 10; seq++)
            {
                controller.Predict(seq, Forward());
            }
            double predictedZ = controller.Current.Z;

            // The server has applied the first 6 inputs.
            for (int i = 0; i < 6; i++)
            {
                MovementModel.Step(server, Forward(), _map);
            }
            controller.Reconcile(Server(server, 6));

            Assert.AreEqual(4, controller.PendingInputs);
            Assert.AreEqual(predictedZ, controller.Current.Z, 1e-4, "replaying 7..10 lands where prediction was");
            Assert.AreEqual(0.0, controller.LastError, 1e-4);
        }

        [Test]
        public void ACorrectionIsBlendedAwayInsteadOfTeleporting()
        {
            PredictionController controller = Active(-10, 0, out PredictedBody server);
            for (uint seq = 1; seq <= 6; seq++)
            {
                controller.Predict(seq, Forward());
            }
            Vec3 shown = controller.DisplayPosition;

            // The server disagrees: it ended 0.5 m to the side (a shove the client could not know about).
            for (int i = 0; i < 6; i++)
            {
                MovementModel.Step(server, Forward(), _map);
            }
            server.X += 0.5;
            controller.Reconcile(Server(server, 6));

            Assert.AreEqual(0.5, controller.LastError, 1e-4);
            Assert.AreEqual(shown.X, controller.DisplayPosition.X, 1e-4, "what is drawn does not jump");
            Assert.AreEqual(server.X, controller.Current.X, 1e-4, "but the body is now the server's");

            for (int frame = 0; frame < 60; frame++)
            {
                controller.Smooth(1.0 / 60.0);
            }
            Assert.AreEqual(controller.Current.X, controller.DisplayPosition.X, 0.02, "blended after a second");
        }

        [Test]
        public void AHugeErrorSnapsToTheServer()
        {
            PredictionController controller = Active(-10, 0, out PredictedBody server);
            for (uint seq = 1; seq <= 6; seq++)
            {
                controller.Predict(seq, Forward());
            }

            server.X += 8.0;
            server.Tick += 6;
            controller.Reconcile(Server(server, 6));

            Assert.AreEqual(1, controller.SnapCount);
            Assert.AreEqual(controller.Current.X, controller.DisplayPosition.X, 1e-6);
        }

        [Test]
        public void NothingIsPredictedOutsideAnActiveRound()
        {
            PredictionController controller = Controller();
            PredictedBody server = Body(-10, 0);
            controller.Reconcile(Server(server, 0, MatchPhase.Countdown));

            controller.Predict(1, Forward());

            Assert.IsFalse(controller.IsPredicting);
            Assert.AreEqual(0.0, controller.Current.Z, 1e-9, "frozen during the countdown");
            Assert.AreEqual(-10.0, controller.Current.X, 1e-6);
        }

        [Test]
        public void ADeadPlayerFollowsTheServer()
        {
            PredictionController controller = Active(-10, 0, out PredictedBody server);
            server.Alive = false;
            server.X = -3;

            controller.Reconcile(Server(server, 0));

            Assert.IsFalse(controller.IsPredicting);
            Assert.AreEqual(-3.0, controller.Current.X, 1e-6);
        }

        [Test]
        public void InputsSentBeforePredictionStartedAreReplayedOnceItDoes()
        {
            PredictionController controller = Controller();
            PredictedBody server = Body(-10, 0);
            controller.Reconcile(Server(server, 0, MatchPhase.Countdown));
            for (uint seq = 1; seq <= 5; seq++)
            {
                controller.Predict(seq, Forward()); // sent while the countdown was still on screen
            }

            server.Tick += 5;
            controller.Reconcile(Server(server, 2)); // round is active; the server has applied two of them

            Assert.IsTrue(controller.IsPredicting);
            Assert.AreEqual(3, controller.PendingInputs);
            Assert.AreEqual(3 * 0.1, controller.Current.Z, 1e-4);
        }

        [Test]
        public void DisablingPredictionMakesTheBodyFollowTheServer()
        {
            PredictionController controller = Active(-10, 0, out PredictedBody server);
            controller.Enabled = false;
            server.Z = 4;

            controller.Reconcile(Server(server, 0));
            controller.Predict(1, Forward());

            Assert.IsFalse(controller.IsPredicting);
            Assert.AreEqual(4.0, controller.Current.Z, 1e-6);
        }

        [Test]
        public void AcknowledgementsKeepWorkingAcrossTheSequenceWrapAround()
        {
            PredictionController controller = Active(-10, 0, out PredictedBody server);
            uint start = uint.MaxValue - 2;
            for (uint i = 0; i < 6; i++)
            {
                controller.Predict(unchecked(start + i), Forward());
            }

            server.Tick += 4;
            controller.Reconcile(Server(server, unchecked(start + 3)));

            Assert.AreEqual(2, controller.PendingInputs);
        }
    }
}
