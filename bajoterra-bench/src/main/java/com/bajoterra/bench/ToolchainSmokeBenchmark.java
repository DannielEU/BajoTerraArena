package com.bajoterra.bench;

import com.bajoterra.core.sim.SimulationConstants;
import java.util.concurrent.TimeUnit;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.State;

/** Verifies that JMH generates and runs benchmarks on JDK 27; also used as a harness sanity check. */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
public class ToolchainSmokeBenchmark {

    private int ticks = 1_000;

    @Benchmark
    public double secondsForTicks() {
        return ticks * SimulationConstants.TICK_SECONDS;
    }
}
