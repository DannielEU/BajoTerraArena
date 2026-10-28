package com.bajoterra.bench;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.protocol.FrameCodec;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.Snapshot;
import com.bajoterra.server.SnapshotBuilder;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.util.concurrent.TimeUnit;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;

/** Cost of turning the world into a snapshot, serialising it and reading it back (20 times per second per room). */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.MICROSECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class NetworkBenchmark {

    private GameSimulation sim;
    private Snapshot snapshot;
    private ServerMessage message;
    private byte[] wire;
    private long eventSequence;

    @Setup
    public void setUp() {
        Content content = Content.load();
        sim = BenchSupport.runningBotMatch(content, "caverna", 23);
        for (int i = 0; i < 600; i++) {
            sim.tick();
        }
        snapshot = SnapshotBuilder.snapshot(sim);
        message = ServerMessage.newBuilder().setSnapshot(snapshot).build();
        wire = message.toByteArray();
    }

    @Benchmark
    public Snapshot buildSnapshot() {
        return SnapshotBuilder.snapshot(sim);
    }

    @Benchmark
    public ByteBuffer encodeFrame() {
        return FrameCodec.encode(message);
    }

    @Benchmark
    public ServerMessage decodeMessage() throws IOException {
        return ServerMessage.parseFrom(wire);
    }

    @Benchmark
    public int buildEventBatch() {
        SnapshotBuilder.Batch batch = SnapshotBuilder.events(sim, eventSequence);
        eventSequence = batch.nextSequence() >= sim.events().written() ? 0 : batch.nextSequence();
        return batch.batch().getEventsCount();
    }

    /** Size of one snapshot frame on the wire, reported as a benchmark result so regressions are visible. */
    @Benchmark
    public int snapshotBytes() {
        return wire.length;
    }
}
