package com.bajoterra.bench;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.SimulationConstants;
import java.lang.management.GarbageCollectorMXBean;
import java.lang.management.ManagementFactory;
import java.util.Locale;
import org.HdrHistogram.Histogram;

/**
 * Long-running stability check: plays a bot match for a long stretch of simulated time and records the duration of
 * every tick in a high-dynamic-range histogram, plus how much the heap grew and how often the garbage collector ran.
 * A healthy simulation has a flat histogram far below the 16.7 ms frame budget, no heap growth and (almost) no GC.
 */
public final class SoakRunner {

    /** What a soak run measured. Times are in nanoseconds. */
    public record Result(long ticks, double meanNanos, long p50Nanos, long p99Nanos, long p999Nanos, long maxNanos,
            long heapGrowthBytes, long gcCollections) {
    }

    private static final int WARM_UP_TICKS = 6_000;

    private SoakRunner() {
        throw new AssertionError("Utility class, not instantiable");
    }

    public static void main(String[] args) {
        int minutes = args.length > 0 ? Integer.parseInt(args[0]) : 30;
        System.out.println("Soak: " + minutes + " simulated minutes of a 3v3 bot match");
        Result r = run(Content.load(), minutes * 60, 1234);
        System.out.printf(Locale.ROOT,
                "ticks %d | mean %.1f us | p50 %.1f us | p99 %.1f us | p99.9 %.1f us | max %.1f us%n",
                r.ticks(), r.meanNanos() / 1e3, r.p50Nanos() / 1e3, r.p99Nanos() / 1e3, r.p999Nanos() / 1e3,
                r.maxNanos() / 1e3);
        System.out.printf(Locale.ROOT, "heap growth %.2f MB | GC collections during the run: %d%n",
                r.heapGrowthBytes() / 1e6, r.gcCollections());
    }

    /** Runs {@code simulatedSeconds} of game time after a warm-up and returns the tick-time statistics. */
    public static Result run(Content content, int simulatedSeconds, long seed) {
        GameSimulation sim = BenchSupport.runningBotMatch(content, "caverna", seed);
        for (int i = 0; i < WARM_UP_TICKS; i++) {
            sim.tick();
        }
        long heapBefore = usedHeapAfterGc();
        long gcBefore = gcCollections();
        Histogram histogram = new Histogram(60_000_000_000L, 3);
        long ticks = (long) simulatedSeconds * SimulationConstants.TICKS_PER_SECOND;
        long total = 0;
        for (long i = 0; i < ticks; i++) {
            long begin = System.nanoTime();
            sim.tick();
            long elapsed = System.nanoTime() - begin;
            histogram.recordValue(elapsed);
            total += elapsed;
        }
        long gcDuring = gcCollections() - gcBefore;
        long heapGrowth = usedHeapAfterGc() - heapBefore;
        return new Result(ticks, total / (double) ticks, histogram.getValueAtPercentile(50),
                histogram.getValueAtPercentile(99), histogram.getValueAtPercentile(99.9), histogram.getMaxValue(),
                heapGrowth, gcDuring);
    }

    private static long usedHeapAfterGc() {
        System.gc();
        Runtime runtime = Runtime.getRuntime();
        return runtime.totalMemory() - runtime.freeMemory();
    }

    private static long gcCollections() {
        long total = 0;
        for (GarbageCollectorMXBean bean : ManagementFactory.getGarbageCollectorMXBeans()) {
            total += Math.max(0, bean.getCollectionCount());
        }
        return total;
    }
}
