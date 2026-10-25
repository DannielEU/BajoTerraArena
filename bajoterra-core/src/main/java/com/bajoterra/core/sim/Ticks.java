package com.bajoterra.core.sim;

/** Conversion entre segundos de juego y ticks de simulacion. */
public final class Ticks {

    private Ticks() {
        throw new AssertionError("Clase de utilidades, no instanciable");
    }

    /**
     * Convierte una duracion en segundos al numero de ticks mas cercano.
     *
     * @throws IllegalArgumentException si la duracion es negativa o no es un numero finito
     */
    public static int fromSeconds(double seconds) {
        if (!Double.isFinite(seconds) || seconds < 0) {
            throw new IllegalArgumentException("Duracion invalida: " + seconds);
        }
        return (int) Math.round(seconds * SimulationConstants.TICKS_PER_SECOND);
    }

    /** Convierte un numero de ticks a segundos. */
    public static double toSeconds(int ticks) {
        return ticks * SimulationConstants.TICK_SECONDS;
    }
}
