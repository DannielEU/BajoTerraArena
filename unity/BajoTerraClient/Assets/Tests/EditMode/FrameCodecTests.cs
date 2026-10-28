using System;
using Bajoterra.Client.Wire;
using Bajoterra.Protocol;
using Google.Protobuf;
using NUnit.Framework;

namespace Bajoterra.Client.Tests
{
    public class FrameCodecTests
    {
        // Golden bytes pinned by the Java ProtocolContractTest: if the two sides ever disagree, both suites fail.
        private const string GoldenInputHex =
            "084d150000003f1d0000803f250000c03f2d000080be30013d0000803f45000000404d000040405001580560066801";

        // Golden bytes of the data added for the client (also pinned by the Java ProtocolContractTest).
        private const string GoldenLocalHex = "0d00000841150000c03f20012d0000003f38034d0000803f5203001548";
        private const string GoldenMoveHex =
            "0a046a756d7010011815300140054d0000803f550000803f6a07696d70756c7365720f0a08766572746963616c1500000841";

        private static byte[] FromHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        [Test]
        public void TheLocalStateTheJavaServerPinnedDecodesAndReEncodesIdentically()
        {
            LocalState local = LocalState.Parser.ParseFrom(FromHex(GoldenLocalHex));

            Assert.AreEqual(8.5f, local.VelY);
            Assert.AreEqual(1.5f, local.KnockX);
            Assert.IsTrue(local.Grounded);
            Assert.AreEqual(0.5f, local.StatusSpeedMultiplier);
            Assert.AreEqual(3u, local.MoveElapsedTicks);
            Assert.AreEqual(1f, local.MoveDirZ);
            CollectionAssert.AreEqual(new uint[] { 0, 21, 72 }, local.MoveCooldownTicks);
            CollectionAssert.AreEqual(FromHex(GoldenLocalHex), local.ToByteArray());
        }

        [Test]
        public void TheMoveInfoTheJavaServerPinnedDecodesAndReEncodesIdentically()
        {
            MoveInfo move = MoveInfo.Parser.ParseFrom(FromHex(GoldenMoveHex));

            Assert.AreEqual("jump", move.Id);
            Assert.AreEqual(21u, move.CooldownTicks);
            Assert.IsTrue(move.Interruptible);
            Assert.AreEqual(5u, move.AllowedFromMask);
            Assert.AreEqual("impulse", move.BehaviorType);
            Assert.AreEqual(8.5f, move.BehaviorParams["vertical"]);
            CollectionAssert.AreEqual(FromHex(GoldenMoveHex), move.ToByteArray());
        }

        private static InputFrame SampleInput()
        {
            return new InputFrame
            {
                Sequence = 77, MoveX = 0.5f, MoveZ = 1f, AimYaw = 1.5f, AimPitch = -0.25f, HasCamera = true,
                CamX = 1f, CamY = 2f, CamZ = 3f, Fire = true, MoveMask = 5, SelectSlot = 3, CycleSlot = -1
            };
        }

        private static string Hex(byte[] bytes, int offset, int count)
        {
            return BitConverter.ToString(bytes, offset, count).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static byte[] Frame(ServerMessage message)
        {
            return FrameCodec.Encode(message);
        }

        [Test]
        public void TheInputFrameEncodesToTheSameBytesAsTheJavaServerPinned()
        {
            byte[] frame = FrameCodec.Encode(new ClientMessage { Input = SampleInput() });

            // The Java golden is the InputFrame alone; the ClientMessage wraps it in field 5.
            string hex = Hex(frame, FrameCodec.HeaderBytes, frame.Length - FrameCodec.HeaderBytes);
            Assert.AreEqual("2a" + (GoldenInputHex.Length / 2).ToString("x2") + GoldenInputHex, hex);
        }

        [Test]
        public void TheFrameStartsWithABigEndianLength()
        {
            byte[] frame = FrameCodec.Encode(new ClientMessage { Ping = new Ping { ClientTime = 1 } });

            Assert.AreEqual(0, frame[0]);
            Assert.AreEqual(0, frame[1]);
            Assert.AreEqual(0, frame[2]);
            Assert.AreEqual(frame.Length - FrameCodec.HeaderBytes, frame[3]);
        }

        [Test]
        public void AMessageOverTheFrameLimitIsRefused()
        {
            var huge = new ClientMessage { SelectLoadout = new SelectLoadout { CharacterId = new string('x', 70_000) } };

            Assert.Throws<ProtocolViolationException>(() => FrameCodec.Encode(huge));
        }

        [Test]
        public void AMessageSurvivesAnEncodeAndDecodeRoundTrip()
        {
            var message = TestContent.Wrap(TestContent.Welcome());
            var reader = new FrameReader();
            byte[] frame = Frame(message);

            reader.Feed(frame, 0, frame.Length);

            Assert.IsTrue(reader.TryRead(out ServerMessage decoded));
            Assert.AreEqual(message, decoded);
            Assert.AreEqual(0, reader.Pending);
        }

        [Test]
        public void AFrameSplitAcrossReadsIsReassembled()
        {
            var message = TestContent.Wrap(TestContent.MatchStart());
            byte[] frame = Frame(message);
            var reader = new FrameReader();

            for (int i = 0; i < frame.Length; i++)
            {
                Assert.IsFalse(reader.TryRead(out _), "incomplete after " + i + " bytes");
                reader.Feed(frame, i, 1);
            }

            Assert.IsTrue(reader.TryRead(out ServerMessage decoded));
            Assert.AreEqual(message, decoded);
        }

        [Test]
        public void SeveralFramesInOneReadComeOutInOrder()
        {
            var first = TestContent.Wrap(TestContent.Snapshot(10, new[] { TestContent.Combatant(0, 1, 2) }));
            var second = TestContent.Wrap(TestContent.Snapshot(13, new[] { TestContent.Combatant(0, 2, 2) }));
            byte[] a = Frame(first);
            byte[] b = Frame(second);
            var both = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, both, 0, a.Length);
            Buffer.BlockCopy(b, 0, both, a.Length, b.Length);
            var reader = new FrameReader();

            reader.Feed(both, 0, both.Length);

            Assert.IsTrue(reader.TryRead(out ServerMessage one));
            Assert.IsTrue(reader.TryRead(out ServerMessage two));
            Assert.IsFalse(reader.TryRead(out _));
            Assert.AreEqual(10u, one.Snapshot.Tick);
            Assert.AreEqual(13u, two.Snapshot.Tick);
        }

        [Test]
        public void AHugeBacklogGrowsTheBufferInsteadOfLosingBytes()
        {
            var reader = new FrameReader();
            var frames = new System.Collections.Generic.List<byte[]>();
            int total = 0;
            for (uint tick = 0; tick < 80; tick++)
            {
                var combatants = new System.Collections.Generic.List<CombatantState>();
                for (int i = 0; i < 6; i++)
                {
                    combatants.Add(TestContent.Combatant(i, tick, i));
                }
                byte[] frame = Frame(TestContent.Wrap(TestContent.Snapshot(tick, combatants)));
                frames.Add(frame);
                total += frame.Length;
            }
            Assert.Greater(total, 16 * 1024, "the test must exceed the initial buffer");

            foreach (byte[] frame in frames)
            {
                reader.Feed(frame, 0, frame.Length);
            }

            for (uint tick = 0; tick < 80; tick++)
            {
                Assert.IsTrue(reader.TryRead(out ServerMessage message));
                Assert.AreEqual(tick, message.Snapshot.Tick);
            }
        }

        [Test]
        public void ANegativeOrOversizedLengthIsAProtocolViolation()
        {
            var reader = new FrameReader();
            var bad = new byte[] { 0x7f, 0xff, 0xff, 0xff, 1, 2, 3 };
            reader.Feed(bad, 0, bad.Length);

            Assert.Throws<ProtocolViolationException>(() => reader.TryRead(out _));

            var negative = new FrameReader();
            var negativeBytes = new byte[] { 0xff, 0xff, 0xff, 0xfe };
            negative.Feed(negativeBytes, 0, negativeBytes.Length);
            Assert.Throws<ProtocolViolationException>(() => negative.TryRead(out _));
        }

        [Test]
        public void AnUndecodablePayloadIsAProtocolViolation()
        {
            var reader = new FrameReader();
            // Length 3, field 1 with wire type 7 (invalid).
            var garbage = new byte[] { 0, 0, 0, 3, 0x0f, 0xff, 0xff };
            reader.Feed(garbage, 0, garbage.Length);

            Assert.Throws<ProtocolViolationException>(() => reader.TryRead(out _));
        }
    }
}
