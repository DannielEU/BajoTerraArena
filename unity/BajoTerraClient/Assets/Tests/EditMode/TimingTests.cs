using Bajoterra.Client.Interpolation;
using Bajoterra.Client.Session;
using Bajoterra.Protocol;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class TimingTests
    {
        [Test]
        public void TheClockStartsFromTheFirstSample()
        {
            var clock = new ServerClock();
            Assert.IsFalse(clock.IsSynchronized);

            clock.Observe(10.0, 60); // server time 1.0

            Assert.IsTrue(clock.IsSynchronized);
            Assert.AreEqual(1.0, clock.ServerTimeAt(10.0), 1e-9);
            Assert.AreEqual(1.5, clock.ServerTimeAt(10.5), 1e-9);
        }

        [Test]
        public void AFasterPacketLowersTheOffsetAtOnceAndASlowerOneRaisesItGently()
        {
            var clock = new ServerClock();
            clock.Observe(10.00, 60); // local 10.00, server 1.00: offset 9.00

            clock.Observe(10.60, 90); // server 1.50 -> sample 9.10, a slower packet
            double creeping = 10.60 - clock.ServerTimeAt(10.60);
            Assert.Greater(creeping, 9.0);
            Assert.Less(creeping, 9.01, "only 2 % of the 0.1 s difference");

            clock.Observe(10.55, 96); // server 1.60 -> sample 8.95, a faster packet
            Assert.AreEqual(8.95, 10.55 - clock.ServerTimeAt(10.55), 1e-9);
        }

        [Test]
        public void AJitteryPacketDoesNotMoveTheClockMuch()
        {
            var clock = new ServerClock();
            clock.Observe(0.0, 0);
            double before = clock.ServerTimeAt(5.0);

            clock.Observe(1.5, 60); // a packet 0.5 s late

            Assert.AreEqual(before, clock.ServerTimeAt(5.0), 0.011);
        }

        [Test]
        public void FixedStepsAreProducedAtTheConfiguredRate()
        {
            var clock = new FixedStepClock(1.0 / 60.0, 5);
            int steps = 0;

            for (int frame = 0; frame < 120; frame++)
            {
                steps += clock.Advance(1.0 / 120.0); // a 120 fps renderer for one second
            }

            Assert.That(steps, Is.InRange(59, 60));
        }

        [Test]
        public void ASlowFrameRunsSeveralStepsButNeverTooMany()
        {
            var clock = new FixedStepClock(1.0 / 60.0, 5);

            Assert.AreEqual(3, clock.Advance(3.5 / 60.0));
            Assert.AreEqual(5, clock.Advance(2.0), "a long stall is capped");
            Assert.AreEqual(0, clock.Advance(0.001), "and the backlog is dropped");
        }

        [Test]
        public void NegativeDeltasAreIgnored()
        {
            var clock = new FixedStepClock();

            Assert.AreEqual(0, clock.Advance(-1.0));
        }

        private static Snapshot Snap(uint tick, float x)
        {
            return TestContent.Snapshot(tick, new[] { TestContent.Combatant(0, x, 0f) });
        }

        [Test]
        public void TheBufferKeepsSnapshotsInTimeOrderAndRejectsLateOnes()
        {
            var buffer = new SnapshotBuffer(4);

            Assert.IsTrue(buffer.Add(1.0, Snap(60, 0f)));
            Assert.IsTrue(buffer.Add(1.05, Snap(63, 1f)));
            Assert.IsFalse(buffer.Add(1.02, Snap(61, 0.5f)), "older than the newest");
            Assert.IsFalse(buffer.Add(1.05, Snap(63, 1f)), "duplicate");

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(63u, buffer.Latest.Tick);
        }

        [Test]
        public void TheBufferDropsTheOldestWhenFull()
        {
            var buffer = new SnapshotBuffer(3);
            for (uint i = 0; i < 5; i++)
            {
                buffer.Add(i, Snap(i * 3, i));
            }

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(2.0, buffer[0].ServerTime);
            Assert.AreEqual(4.0, buffer[2].ServerTime);
        }

        [Test]
        public void FindReturnsTheSnapshotsAroundTheRenderTime()
        {
            var buffer = new SnapshotBuffer();
            buffer.Add(1.00, Snap(60, 0f));
            buffer.Add(1.05, Snap(63, 1f));
            buffer.Add(1.10, Snap(66, 2f));

            Assert.IsTrue(buffer.Find(1.07, out TimedSnapshot from, out TimedSnapshot to, out double alpha,
                out double overshoot));

            Assert.AreEqual(63u, from.Snapshot.Tick);
            Assert.AreEqual(66u, to.Snapshot.Tick);
            Assert.AreEqual(0.4, alpha, 1e-9);
            Assert.AreEqual(0.0, overshoot);
        }

        [Test]
        public void FindReportsTheOvershootBeyondTheNewestAndClampsBeforeTheOldest()
        {
            var buffer = new SnapshotBuffer();
            buffer.Add(1.00, Snap(60, 0f));
            buffer.Add(1.05, Snap(63, 1f));

            buffer.Find(1.20, out TimedSnapshot from, out TimedSnapshot to, out double alpha, out double overshoot);
            Assert.AreEqual(63u, from.Snapshot.Tick);
            Assert.AreEqual(63u, to.Snapshot.Tick);
            Assert.AreEqual(0.15, overshoot, 1e-9);

            buffer.Find(0.5, out from, out to, out alpha, out overshoot);
            Assert.AreEqual(60u, from.Snapshot.Tick);
            Assert.AreEqual(0.0, alpha);
            Assert.AreEqual(0.0, overshoot);
        }

        [Test]
        public void AnEmptyBufferFindsNothing()
        {
            Assert.IsFalse(new SnapshotBuffer().Find(1.0, out _, out _, out _, out _));
        }

        [Test]
        public void EventsWaitUntilTheRenderClockReachesThem()
        {
            var scheduler = new EventScheduler(60);
            scheduler.Enqueue(TestContent.Event(EventKind.Shot, 60));
            scheduler.Enqueue(TestContent.Event(EventKind.Damage, 66));
            var played = new System.Collections.Generic.List<uint>();

            scheduler.Release(0.99, e => played.Add(e.Tick));
            Assert.AreEqual(0, played.Count);

            scheduler.Release(1.0, e => played.Add(e.Tick));
            Assert.AreEqual(new uint[] { 60 }, played.ToArray());

            scheduler.Release(1.2, e => played.Add(e.Tick));
            Assert.AreEqual(new uint[] { 60, 66 }, played.ToArray());
            Assert.AreEqual(0, scheduler.Pending);
        }
    }
}
