using System;
using Bajoterra.Client.Content;
using Bajoterra.Client.Input;
using Bajoterra.Client.Prediction;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class RampTests
    {
        private const float Quarter = 1.5707964f;

        private static ObstacleInfo Ramp(string id, float minX, float minZ, float maxX, float maxZ, float height,
            uint rise)
        {
            return new ObstacleInfo
            {
                Id = id, MinX = minX, MinY = 0, MinZ = minZ, MaxX = maxX, MaxY = height, MaxZ = maxZ,
                BlocksProjectiles = true, BlocksMovement = true, BlocksLineOfSight = true, Rise = rise
            };
        }

        /// <summary>The same shape as the server's test map: ramp up (+x), plateau, ramp down (-x).</summary>
        private static ArenaMap RampMap()
        {
            var data = new MapData { Name = "Ramps", MinX = -20, MaxX = 20, MinZ = -10, MaxZ = 10, GroundY = 0 };
            data.Obstacles.Add(Ramp("up", 0, -2, 4, 2, 1.6f, 1));
            data.Obstacles.Add(Ramp("plateau", 4, -2, 8, 2, 1.6f, 0));
            data.Obstacles.Add(Ramp("down", 8, -2, 12, 2, 1.6f, 2));
            return new ArenaMap(data);
        }

        [TestCase(1u, 2f, 0f, 0.8)]
        [TestCase(1u, 4f, 0f, 1.6)]
        [TestCase(1u, -1f, 0f, 0.0)]
        [TestCase(2u, 0f, 0f, 1.6)]
        [TestCase(2u, 3f, 0f, 0.4)]
        [TestCase(3u, 0f, 1f, 1.2)]
        [TestCase(4u, 0f, 1f, 0.4)]
        public void TheSurfaceClimbsLinearlyInTheDirectionOfTheRise(uint rise, float x, float z, double expected)
        {
            // A 4 x 2 footprint centred on x 2, z 0 for the x ramps; the z ramps use a 2 x 4 one.
            bool alongX = rise <= 2;
            var info = alongX ? Ramp("r", 0, -1, 4, 1, 1.6f, rise) : Ramp("r", -1, -2, 1, 2, 1.6f, rise);
            ObstacleBox box = new ObstacleBox(info);

            Assert.IsTrue(box.IsRamp);
            Assert.AreEqual(expected, box.TopAt(x, z), 1e-5);
        }

        [Test]
        public void APlainBoxIsFlat()
        {
            ObstacleBox box = new ObstacleBox(Ramp("box", 0, 0, 4, 4, 1f, 0));

            Assert.IsFalse(box.IsRamp);
            Assert.AreEqual(1.0, box.TopAt(2, 2), 1e-9);
            Assert.AreEqual(0.0, box.Slope, 1e-9);
        }

        [Test]
        public void AFlatRayHitsTheSlopeWhereItsHeightIsReached()
        {
            ObstacleBox ramp = RampMap().Obstacles[0];

            float distance = ramp.RayDistance(new Vec3(-2f, 0.8f, 0f), new Vec3(1f, 0f, 0f), 20f, 0f);

            Assert.AreEqual(4.0f, distance, 1e-4f, "the surface is 0.8 m high 2 m into the ramp, 4 m from the origin");
        }

        [Test]
        public void ARayAboveTheWholeRampMissesIt()
        {
            ObstacleBox ramp = RampMap().Obstacles[0];

            Assert.Less(ramp.RayDistance(new Vec3(-2f, 1.9f, 0f), new Vec3(1f, 0f, 0f), 20f, 0f), 0f);
        }

        [Test]
        public void ARayFallingOntoTheSlopeLandsOnIt()
        {
            ObstacleBox ramp = RampMap().Obstacles[0];

            float distance = ramp.RayDistance(new Vec3(2f, 3f, 0f), new Vec3(0f, -1f, 0f), 10f, 0f);

            Assert.AreEqual(2.2f, distance, 1e-4f, "from 3 m down to the 0.8 m surface");
        }

        [Test]
        public void AWiderRadiusTouchesTheSlopeSooner()
        {
            ObstacleBox ramp = RampMap().Obstacles[0];
            var origin = new Vec3(-2f, 0.8f, 0f);
            var forward = new Vec3(1f, 0f, 0f);

            Assert.Less(ramp.RayDistance(origin, forward, 20f, 0.3f), ramp.RayDistance(origin, forward, 20f, 0f));
        }

        [Test]
        public void TheCameraStaysInsideTheArenaRectangle()
        {
            ArenaMap map = RampMap();
            var pivot = new Vec3(19f, 1.5f, 0f);

            float toTheEast = map.SightDistance(pivot, new Vec3(1f, 0f, 0f), 5f, 0.2f);
            float toTheWest = map.SightDistance(pivot, new Vec3(-1f, 0f, 0f), 5f, 0.2f);

            Assert.AreEqual(0.8f, toTheEast, 1e-4f, "stops 0.2 m before the east edge");
            Assert.GreaterOrEqual(toTheWest, 5f - 1e-4f, "free towards the middle (the ramp is further than 5 m away)");
        }

        [Test]
        public void ABodyWalksUpAndOverTheRampAndDownTheOtherSideWithoutLeavingTheSurface()
        {
            ArenaMap map = RampMap();
            ContentDetails details = TestContent.Details();
            var content = new ContentCatalog(TestContent.ManifestFor(details), details);
            CharacterInfo eli = content.FindCharacter("eli");
            MoveInfo[] infos = content.MoveSetOf(eli);
            var rules = new MoveRule[infos.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = new MoveRule(infos[i]);
            }
            var body = new PredictedBody(eli, rules) { X = -3, Z = 0, Yaw = Quarter };
            body.HitHeightNow = body.HitHeight;
            PlayerInput walk = PlayerInput.Idle;
            walk.MoveZ = 1f;
            walk.AimYaw = Quarter;

            double highest = 0;
            for (int tick = 0; tick < 240; tick++)
            {
                MovementModel.Step(body, walk, map);
                highest = Math.Max(highest, body.Y);
                if (body.X > 0.3 && body.X < 11.7)
                {
                    Assert.IsTrue(body.Grounded, "stays on the slope at x=" + body.X);
                }
            }

            Assert.AreEqual(1.6, highest, 0.01, "reaches the plateau");
            Assert.AreEqual(0.0, body.Y, 1e-9, "and is back on the ground after the ramp down");
            Assert.IsTrue(body.Grounded);
        }

        [Test]
        public void AJumpFromTheMiddleOfTheSlopeLeavesAndComesBackToIt()
        {
            ArenaMap map = RampMap();
            ContentDetails details = TestContent.Details();
            var content = new ContentCatalog(TestContent.ManifestFor(details), details);
            CharacterInfo eli = content.FindCharacter("eli");
            MoveInfo[] infos = content.MoveSetOf(eli);
            var rules = new MoveRule[infos.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = new MoveRule(infos[i]);
            }
            var body = new PredictedBody(eli, rules) { X = 1, Z = 0, Y = 0.4, Yaw = Quarter };
            body.HitHeightNow = body.HitHeight;
            body.Grounded = true;
            PlayerInput jump = PlayerInput.Idle;
            jump.AimYaw = Quarter;
            jump.MoveMask = 1;

            MovementModel.Step(body, jump, map);
            Assert.IsFalse(body.Grounded, "left the ground");
            Assert.Greater(body.VelY, 1.0);

            PlayerInput none = PlayerInput.Idle;
            none.AimYaw = Quarter;
            for (int tick = 0; tick < 120 && !body.Grounded; tick++)
            {
                MovementModel.Step(body, none, map);
            }

            Assert.IsTrue(body.Grounded, "landed");
            Assert.AreEqual(0.4, body.Y, 0.05, "on the same spot of the slope (standing still)");
        }
    }
}
