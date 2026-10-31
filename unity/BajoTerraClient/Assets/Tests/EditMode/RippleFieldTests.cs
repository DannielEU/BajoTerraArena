using Bajoterra.Client.Content;
using Bajoterra.Client.Effects;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class RippleFieldTests
    {
        private static int Cell(RippleField field, float metres)
        {
            return (int)System.Math.Round((metres + field.Radius) / field.CellSize);
        }

        [Test]
        public void StillWaterStaysStill()
        {
            var field = new RippleField(1.5f, 40);

            field.Update(2f);

            Assert.AreEqual(0f, field.Energy);
        }

        [Test]
        public void ASplashSpreadsOutAsARingAndDiesAway()
        {
            var field = new RippleField(1.6f, 48);
            int far = Cell(field, 0.8f);
            int middle = Cell(field, 0f);
            field.Disturb(0f, 0f, 0.2f, 0.05f);
            Assert.AreEqual(0f, field.Height(far, middle), "the far point is still untouched");
            float start = field.Energy;

            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                field.Update(1f / 60f);
                peak = System.Math.Max(peak, System.Math.Abs(field.Height(far, middle)));
            }
            Assert.Greater(peak, 1e-3f, "the ring reached 0.8 m within a second");

            for (int i = 0; i < 600; i++)
            {
                field.Update(1f / 60f);
            }
            Assert.Less(field.Energy, start * 0.02f, "ten seconds later the pool is calm again");
        }

        [Test]
        public void OnlyTheCircleIsWaterAndTheCornersNeverMove()
        {
            var field = new RippleField(1f, 32);
            Assert.IsTrue(field.IsWater(16, 16));
            Assert.IsFalse(field.IsWater(0, 0));

            field.Disturb(0.9f, 0.9f, 0.4f, 0.1f);   // half of it lands outside the circle
            for (int i = 0; i < 120; i++)
            {
                field.Update(1f / 60f);
                Assert.AreEqual(0f, field.Height(0, 0));
                Assert.AreEqual(0f, field.Height(31, 31));
            }
        }

        [Test]
        public void ALongFrameRunsAtMostAFewStepsAndStaysStable()
        {
            var field = new RippleField(2.5f, 56);
            field.Disturb(0.3f, -0.4f, 0.5f, 0.08f);
            for (int i = 0; i < 100; i++)
            {
                field.Update(0.5f);   // a hitch: 30 steps' worth, clamped
            }

            Assert.IsFalse(float.IsNaN(field.Energy));
            Assert.Less(field.Energy, 1f);
        }

        [Test]
        public void TheMapReadsItsPoolsOfWater()
        {
            var data = new MapData { Name = "test", MinX = -10, MaxX = 10, MinZ = -10, MaxZ = 10 };
            data.Water.Add(new WaterPool { X = 2f, Z = -3f, Radius = 1.5f });

            var map = new ArenaMap(data);

            Assert.AreEqual(1, map.Water.Length);
            Assert.IsTrue(map.Water[0].Contains(2.5f, -2f, 0f));
            Assert.IsFalse(map.Water[0].Contains(4f, -3f, 0f));
            Assert.IsTrue(map.Water[0].Contains(4f, -3f, 0.6f));
            Assert.AreEqual(0, new ArenaMap(new MapData { Name = "dry" }).Water.Length);
        }
    }
}
