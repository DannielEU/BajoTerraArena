using System.Text;
using Bajoterra.Client.Input;
using Bajoterra.Client.Prediction;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    /// <summary>
    /// The client's movement model against movement recorded from the real server. If the Java rules change, the
    /// server-side fixture test fails first; once the fixture is regenerated these tests fail until the C# model
    /// follows.
    /// </summary>
    public class MovementParityTests
    {
        /// <summary>Allowed difference in metres; the server rounds positions to float32 and the models differ in trig.</summary>
        private const double Tolerance = 2e-3;

        private static ParityFixture Fixture
        {
            get { return ParityFixture.Load(); }
        }

        private static PredictedBody NewBody(RecordedScenario scenario, out CharacterInfo character)
        {
            character = Fixture.Content.FindCharacter(scenario.Character);
            MoveInfo[] infos = Fixture.Content.MoveSetOf(character);
            var rules = new MoveRule[infos.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = new MoveRule(infos[i]);
            }
            return new PredictedBody(character, rules);
        }

        private static PlayerInput InputOf(RecordedTick tick)
        {
            PlayerInput input = PlayerInput.Idle;
            input.MoveX = tick.MoveX;
            input.MoveZ = tick.MoveZ;
            input.AimYaw = tick.Yaw;
            input.MoveMask = tick.Mask;
            return input;
        }

        private static void ReplayAndCompare(RecordedScenario scenario, RecordedSnap from)
        {
            PredictedBody body = NewBody(scenario, out CharacterInfo character);
            body.LoadFrom(from.State, from.Local, from.ServerTick, character);
            foreach (RecordedTick tick in scenario.Ticks)
            {
                if (tick.Number <= from.Number)
                {
                    continue;
                }
                MovementModel.Step(body, InputOf(tick), Fixture.Map);
                string where = scenario.Name + " from snapshot " + from.Number + ", tick " + tick.Number;
                Assert.AreEqual(tick.X, body.X, Tolerance, where + ": x");
                Assert.AreEqual(tick.Y, body.Y, Tolerance, where + ": y");
                Assert.AreEqual(tick.Z, body.Z, Tolerance, where + ": z");
                Assert.AreEqual(tick.Posture, (int)body.Posture, where + ": posture");
                int activeMove = body.ActiveSlot >= 0 ? (int)character.MoveIndices[body.ActiveSlot] : -1;
                Assert.AreEqual(tick.ActiveMove, activeMove, where + ": active move");
                Assert.AreEqual(tick.Grounded, body.Grounded, where + ": grounded");
            }
        }

        [Test]
        public void TheFixtureHoldsTheScenariosThatExerciseEveryRule()
        {
            var names = new StringBuilder();
            foreach (RecordedScenario scenario in Fixture.Scenarios)
            {
                names.Append(scenario.Name).Append(' ');
                Assert.Greater(scenario.Ticks.Count, 80, scenario.Name);
                Assert.Greater(scenario.Snaps.Count, 5, scenario.Name);
            }
            StringAssert.Contains("jump_onto_low_cover", names.ToString());
            StringAssert.Contains("roll_cooldown_and_repeat", names.ToString());
            StringAssert.Contains("pronto_dash_ground_and_air", names.ToString());
            StringAssert.Contains("slide_along_wall", names.ToString());
        }

        [Test]
        public void EveryScenarioReplaysFromItsStartExactlyAsTheServerMoved()
        {
            foreach (RecordedScenario scenario in Fixture.Scenarios)
            {
                ReplayAndCompare(scenario, scenario.Snaps[0]);
            }
        }

        [Test]
        public void ReplayingFromAnyRecordedStateReachesTheSameFuture()
        {
            // Loading the state the server sends (mid-jump, mid-roll, in a cooldown...) and stepping on must agree
            // with the server too: this is what reconciliation does with every snapshot.
            foreach (RecordedScenario scenario in Fixture.Scenarios)
            {
                foreach (RecordedSnap snap in scenario.Snaps)
                {
                    ReplayAndCompare(scenario, snap);
                }
            }
        }

        [Test]
        public void TheRecordedMovesAreTheOnesTheRulesDependOn()
        {
            MoveInfo roll = Fixture.Content.Move(Fixture.Content.MoveIndexOf("roll"));
            MoveInfo jump = Fixture.Content.Move(Fixture.Content.MoveIndexOf("jump"));

            Assert.AreEqual("drive", roll.BehaviorType);
            Assert.AreEqual("impulse", jump.BehaviorType);
            Assert.AreEqual(8.5f, jump.BehaviorParams["vertical"]);
        }
    }
}
