package com.bajoterra.server;

import com.bajoterra.core.sim.SimulationConstants;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.locks.LockSupport;

/**
 * Runs a task at a fixed rate on a dedicated thread. In real-time mode it sleeps until the next deadline (spinning
 * for the last stretch for precision) and skips ahead instead of replaying ticks when it falls far behind; in
 * fast mode it never sleeps, which is how tests and simulations run whole matches in a blink.
 */
public final class TickScheduler implements AutoCloseable {

    private static final long PERIOD_NANOS = 1_000_000_000L / SimulationConstants.TICKS_PER_SECOND;
    private static final long SPIN_NANOS = 200_000L;

    private final Runnable task;
    private final boolean realTime;
    private final Thread thread;
    private volatile boolean running;
    private final AtomicLong ticks = new AtomicLong();
    private final AtomicLong slowestTickNanos = new AtomicLong();
    private final AtomicLong totalTickNanos = new AtomicLong();

    public TickScheduler(Runnable task, boolean realTime, String name) {
        this.task = task;
        this.realTime = realTime;
        this.thread = new Thread(this::loop, name);
        this.thread.setDaemon(true);
    }

    public void start() {
        running = true;
        thread.start();
    }

    @Override
    public void close() {
        running = false;
        LockSupport.unpark(thread);
        try {
            thread.join(2_000);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }

    /** Number of times the task has run. */
    public long ticks() {
        return ticks.get();
    }

    /** Longest single run of the task so far, in nanoseconds. */
    public long slowestTickNanos() {
        return slowestTickNanos.get();
    }

    /** Average duration of the task, in nanoseconds. */
    public double meanTickNanos() {
        long count = ticks.get();
        return count == 0 ? 0 : totalTickNanos.get() / (double) count;
    }

    private void loop() {
        long deadline = System.nanoTime();
        while (running) {
            long begin = System.nanoTime();
            try {
                task.run();
            } catch (RuntimeException e) {
                System.err.println("Tick failed: " + e);
            }
            long elapsed = System.nanoTime() - begin;
            totalTickNanos.addAndGet(elapsed);
            slowestTickNanos.accumulateAndGet(elapsed, Math::max);
            ticks.incrementAndGet();
            if (realTime) {
                deadline += PERIOD_NANOS;
                waitUntil(deadline);
            }
        }
    }

    private void waitUntil(long deadline) {
        long now = System.nanoTime();
        if (deadline - now < -PERIOD_NANOS * 5) {
            // Far behind (debugger, GC storm): resynchronise rather than fire a burst of catch-up ticks.
            deadline = now;
        }
        while (running) {
            long remaining = deadline - System.nanoTime();
            if (remaining <= 0) {
                return;
            }
            if (remaining > SPIN_NANOS) {
                LockSupport.parkNanos(remaining - SPIN_NANOS);
            } else {
                Thread.onSpinWait();
            }
        }
    }
}
