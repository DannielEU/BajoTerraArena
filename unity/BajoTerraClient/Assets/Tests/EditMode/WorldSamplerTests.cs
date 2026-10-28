using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class WorldSamplerTests
    {
        private static SnapshotBuffer Buffer(params Snapshot[] snapshots)
        {
            var buffer = new SnapshotBuffer();
            foreach (Snapshot snapshot in snapshots)
            {
                buffer.Add(snapshot.Tick / 60.0, snapshot);
            }
            return buffer;
        }

        private static ProjectileState Slug(uint id, float x, float vx)
        {
            return new ProjectileState { Id = id, SlugIndex = 1, Owner = 2, X = x, Y = 1f, Z = 0f, Vx = vx };
        }

        [Test]
        public void CombatantsAreInterpolatedBetweenTheSurroundingSnapshots()
        {
            var buffer = Buffer(
                TestContent.Snapshot(60, new[] { TestContent.Combatant(0, 0f, 0f) }),
                TestContent.Snapshot(63, new[] { TestContent.Combatant(0, 1f, 2f) }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 61.5 / 60.0, frame);

            Assert.IsTrue(frame.Valid);
            Assert.AreEqual(1, frame.CombatantCount);
            Assert.AreEqual(0.5f, frame.Combatants[0].Position.X, 1e-5f);
            Assert.AreEqual(1.0f, frame.Combatants[0].Position.Z, 1e-5f);
        }

        [Test]
        public void YawTurnsAlongTheShortestArc()
        {
            var a = TestContent.Combatant(0, 0f, 0f, 3.0f);
            var b = TestContent.Combatant(0, 0f, 0f, -3.0f);
            var buffer = Buffer(TestContent.Snapshot(60, new[] { a }), TestContent.Snapshot(63, new[] { b }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 61.5 / 60.0, frame);

            // Halfway between 3.0 and -3.0 the short way round is pi (about 3.14), not 0.
            Assert.Greater(System.Math.Abs(frame.Combatants[0].Yaw), 3.0f);
        }

        [Test]
        public void ATeleportIsNotDraggedAcrossTheMap()
        {
            var buffer = Buffer(
                TestContent.Snapshot(60, new[] { TestContent.Combatant(0, -20f, 0f) }),
                TestContent.Snapshot(63, new[] { TestContent.Combatant(0, 20f, 0f) }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 61.5 / 60.0, frame);

            Assert.AreEqual(20f, frame.Combatants[0].Position.X, 1e-4f);
        }

        [Test]
        public void SlugsPresentInBothSnapshotsAreInterpolated()
        {
            var buffer = Buffer(
                TestContent.Snapshot(60, new[] { TestContent.Combatant(0, 0f, 0f) }, projectiles: new[] { Slug(5, 0f, 45f) }),
                TestContent.Snapshot(63, new[] { TestContent.Combatant(0, 0f, 0f) }, projectiles: new[] { Slug(5, 2.25f, 45f) }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 61.5 / 60.0, frame);

            Assert.AreEqual(1, frame.ProjectileCount);
            Assert.AreEqual(5u, frame.Projectiles[0].Id);
            Assert.AreEqual(1.125f, frame.Projectiles[0].Position.X, 1e-4f);
        }

        [Test]
        public void ASlugBornInTheLastIntervalIsTracedBackAlongItsVelocity()
        {
            var buffer = Buffer(
                TestContent.Snapshot(60, new[] { TestContent.Combatant(0, 0f, 0f) }),
                TestContent.Snapshot(63, new[] { TestContent.Combatant(0, 0f, 0f) }, projectiles: new[] { Slug(9, 2.25f, 45f) }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 61.5 / 60.0, frame);

            // Half an interval before the newest snapshot: 2.25 - 45 * 0.025 = 1.125.
            Assert.AreEqual(1.125f, frame.Projectiles[0].Position.X, 1e-3f);
        }

        [Test]
        public void SlugsAreExtrapolatedWhenTheRenderTimeIsPastTheNewestSnapshot()
        {
            var buffer = Buffer(
                TestContent.Snapshot(60, new[] { TestContent.Combatant(0, 0f, 0f) }, projectiles: new[] { Slug(5, 0f, 45f) }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 1.1, frame);

            Assert.AreEqual(45f * 0.1f, frame.Projectiles[0].Position.X, 1e-3f);
        }

        [Test]
        public void ExtrapolationIsCappedSoALaggingConnectionDoesNotFlingSlugsAway()
        {
            var buffer = Buffer(
                TestContent.Snapshot(60, new[] { TestContent.Combatant(0, 0f, 0f) }, projectiles: new[] { Slug(5, 0f, 45f) }));
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 5.0, frame);

            Assert.AreEqual(45f * (float)WorldSampler.MaxExtrapolation, frame.Projectiles[0].Position.X, 1e-3f);
        }

        [Test]
        public void VitalsComeFromTheNewestSnapshotAndPickupsAreCopied()
        {
            var older = TestContent.Combatant(0, 0f, 0f);
            var newer = TestContent.Combatant(0, 0f, 0f);
            newer.Hp = 40f;
            var snapshot = TestContent.Snapshot(63, new[] { newer });
            snapshot.Pickups.Add(new PickupState { Index = 0, Available = false });
            var buffer = Buffer(TestContent.Snapshot(60, new[] { older }), snapshot);
            var frame = new WorldFrame();

            WorldSampler.Sample(buffer, 61.5 / 60.0, frame);

            Assert.AreEqual(40f, frame.Combatants[0].State.Hp);
            Assert.AreEqual(1, frame.PickupCount);
            Assert.IsFalse(frame.Pickups[0].Available);
            Assert.AreEqual(1u, frame.Match.Round);
        }

        [Test]
        public void SamplingAnEmptyBufferLeavesAnInvalidFrame()
        {
            var frame = new WorldFrame { Valid = true, CombatantCount = 3 };

            WorldSampler.Sample(new SnapshotBuffer(), 1.0, frame);

            Assert.IsFalse(frame.Valid);
            Assert.AreEqual(0, frame.CombatantCount);
        }

        [Test]
        public void IndexOfFindsACombatantById()
        {
            var buffer = Buffer(TestContent.Snapshot(60, new[]
            {
                TestContent.Combatant(4, 0f, 0f), TestContent.Combatant(2, 1f, 0f)
            }));
            var frame = new WorldFrame();
            WorldSampler.Sample(buffer, 1.0, frame);

            Assert.AreEqual(1, frame.IndexOf(2));
            Assert.AreEqual(-1, frame.IndexOf(9));
        }
    }
}
