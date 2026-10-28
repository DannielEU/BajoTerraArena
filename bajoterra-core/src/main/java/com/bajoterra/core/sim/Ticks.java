package com.bajoterra.core.sim;

/** Conversion between game seconds and simulation ticks. */
public final class Ticks {

    private Ticks() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * Converts a duration in seconds to the nearest number of ticks.
     *
     * @throws IllegalArgumentException if the duration is negative or not a finite number
     */
    public static int fromSeconds(double seconds) {
        if (!Double.isFinite(seconds) || seconds < 0) {
            throw new IllegalArgumentException("Invalid duration: " + seconds);
        }
        return (int) Math.round(seconds * SimulationConstants.TICKS_PER_SECOND);
    }

    /** Converts a number of ticks to seconds. */
    public static double toSeconds(int ticks) {
        return ticks * SimulationConstants.TICK_SECONDS;
    }
}
