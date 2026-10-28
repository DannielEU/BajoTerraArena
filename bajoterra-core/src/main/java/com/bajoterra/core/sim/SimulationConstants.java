package com.bajoterra.core.sim;

/**
 * Time constants of the simulation. Game time is always measured in ticks; the wall clock is never read
 * inside the core (determinism and replay).
 */
public final class SimulationConstants {

    /** Fixed simulation frequency. */
    public static final int TICKS_PER_SECOND = 60;

    /** Duration of one tick in seconds. */
    public static final double TICK_SECONDS = 1.0 / TICKS_PER_SECOND;

    /** Rate at which the server sends snapshots to clients. */
    public static final int SNAPSHOT_RATE_HZ = 20;

    /** Number of ticks between two snapshots. */
    public static final int TICKS_PER_SNAPSHOT = TICKS_PER_SECOND / SNAPSHOT_RATE_HZ;

    /** Number of ticks between two bot decisions (10 Hz). */
    public static final int BOT_THINK_INTERVAL_TICKS = 6;

    private SimulationConstants() {
        throw new AssertionError("Constants class, not instantiable");
    }
}
