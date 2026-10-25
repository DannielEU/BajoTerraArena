package com.bajoterra.bench;

import com.bajoterra.core.sim.SimulationConstants;
import java.util.concurrent.TimeUnit;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.State;

/** Verifica que JMH genera y ejecuta benchmarks con JDK 27 (spike del hito M0). */
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
