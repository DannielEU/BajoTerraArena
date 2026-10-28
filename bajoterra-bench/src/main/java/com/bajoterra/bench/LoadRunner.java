package com.bajoterra.bench;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.SimulationConstants;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicLong;

/**
 * Scaling check: runs many independent matches at once on a number of threads and reports how many ticks per second
 * the machine sustains. Divided by 60 this is the number of matches that could run in real time at the same moment.
 */
public final class LoadRunner {

    /** Outcome of a load run. */
    public record Result(int matches, int threads, long ticks, double seconds) {

        public double ticksPerSecond() {
            return ticks / seconds;
        }

        /** How many matches this configuration could simulate in real time (60 ticks per second each). */
        public double realTimeMatches() {
            return ticksPerSecond() / SimulationConstants.TICKS_PER_SECOND;
        }
    }

    private LoadRunner() {
        throw new AssertionError("Utility class, not instantiable");
    }

    public static void main(String[] args) throws InterruptedException {
        int matches = args.length > 0 ? Integer.parseInt(args[0]) : 200;
        int maxThreads = Runtime.getRuntime().availableProcessors();
        Content content = Content.load();
        System.out.println("Load: " + matches + " concurrent 3v3 bot matches");
        for (int threads = 1; threads <= maxThreads; threads *= 2) {
            Result r = run(content, matches, threads, 3.0);
            System.out.printf(Locale.ROOT, "%2d thread(s): %,.0f ticks/s -> %,.0f real-time matches%n", threads,
                    r.ticksPerSecond(), r.realTimeMatches());
        }
    }

    /** Runs {@code matches} simulations spread over {@code threads} for about {@code seconds} of wall time. */
    public static Result run(Content content, int matches, int threads, double seconds) throws InterruptedException {
        List<List<GameSimulation>> perThread = new ArrayList<>();
        for (int t = 0; t < threads; t++) {
            perThread.add(new ArrayList<>());
        }
        for (int m = 0; m < matches; m++) {
            perThread.get(m % threads).add(BenchSupport.runningBotMatch(content, "caverna", 1000L + m));
        }
        AtomicLong totalTicks = new AtomicLong();
        CountDownLatch done = new CountDownLatch(threads);
        long deadline = System.nanoTime() + (long) (seconds * 1e9);
        long begin = System.nanoTime();
        for (List<GameSimulation> sims : perThread) {
            Thread worker = new Thread(() -> {
                long ticks = 0;
                while (System.nanoTime() < deadline) {
                    for (GameSimulation sim : sims) {
                        sim.tick();
                    }
                    ticks += sims.size();
                }
                totalTicks.addAndGet(ticks);
                done.countDown();
            }, "load-worker");
            worker.setDaemon(true);
            worker.start();
        }
        done.await();
        double elapsed = (System.nanoTime() - begin) / 1e9;
        return new Result(matches, threads, totalTicks.get(), elapsed);
    }
}
