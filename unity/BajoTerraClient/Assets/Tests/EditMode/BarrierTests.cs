using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Client.Prediction;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    /// <summary>Walls raised during a round (ice walls) are as solid for the client's prediction as for the server.</summary>
    public class BarrierTests
    {
        private const float Quarter = 1.5707964f;

        private static ArenaMap OpenMap()
        {
            return new ArenaMap(new MapData { Name = "Open", MinX = -20, MaxX = 20, MinZ = -10, MaxZ = 10, GroundY = 0 });
        }

        private static Google.Protobuf.Collections.RepeatedField<BarrierState> Wall(float x)
        {
            return new Google.Protobuf.Collections.RepeatedField<BarrierState>
            {
                new BarrierState { Id = 1, MinX = x - 0.25f, MinY = 0, MinZ = -1.6f, MaxX = x + 0.25f, MaxY = 2.2f, MaxZ = 1.6f, HitsLeft = 2 }
            };
        }

        private static PredictedBody Walker(double x)
        {
            ContentDetails details = TestContent.Details();
            var content = new ContentCatalog(TestContent.ManifestFor(details), details);
            CharacterInfo eli = content.FindCharacter("eli");
            MoveInfo[] infos = content.MoveSetOf(eli);
            var rules = new MoveRule[infos.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = new MoveRule(infos[i]);
            }
            var body = new PredictedBody(eli, rules) { X = x, Z = 0, Yaw = Quarter };
            body.HitHeightNow = body.HitHeight;
            return body;
        }

        [Test]
        public void AWalkerStopsAtAnIceWallAndWalksOnOnceItIsGone()
        {
            ArenaMap map = OpenMap();
            map.SetBarriers(Wall(2f));
            PredictedBody body = Walker(-3);
            PlayerInput walk = PlayerInput.Idle;
            walk.MoveZ = 1f;
            walk.AimYaw = Quarter;

            for (int tick = 0; tick < 120; tick++)
            {
                MovementModel.Step(body, walk, map);
            }
            Assert.AreEqual(2f - 0.25f - 0.4f, body.X, 1e-3, "stopped at the wall's face");

            map.SetBarriers(new Google.Protobuf.Collections.RepeatedField<BarrierState>());
            for (int tick = 0; tick < 60; tick++)
            {
                MovementModel.Step(body, walk, map);
            }
            Assert.Greater(body.X, 3.0, "the wall melted: the way is open");
        }

        [Test]
        public void TheCameraStaysInFrontOfAnIceWall()
        {
            ArenaMap map = OpenMap();
            Assert.AreEqual(10f, map.SightDistance(new Vec3(-3f, 1.5f, 0f), new Vec3(1f, 0f, 0f), 10f, 0f), 1e-4f);

            map.SetBarriers(Wall(2f));

            Assert.AreEqual(4.75f, map.SightDistance(new Vec3(-3f, 1.5f, 0f), new Vec3(1f, 0f, 0f), 10f, 0f), 1e-3f);
            Assert.AreEqual(1, map.BarrierCount);
        }
    }
}
