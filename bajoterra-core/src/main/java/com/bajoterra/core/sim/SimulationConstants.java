package com.bajoterra.core.sim;

/**
 * Constantes de tiempo de la simulacion. El tiempo del juego se mide siempre en ticks;
 * nunca se consulta el reloj de pared dentro del core (determinismo y replay).
 */
public final class SimulationConstants {

    /** Frecuencia fija de simulacion. */
    public static final int TICKS_PER_SECOND = 60;

    /** Duracion de un tick en segundos. */
    public static final double TICK_SECONDS = 1.0 / TICKS_PER_SECOND;

    /** Frecuencia a la que el servidor emite snapshots a los clientes. */
    public static final int SNAPSHOT_RATE_HZ = 20;

    /** Cada cuantos ticks se emite un snapshot. */
    public static final int TICKS_PER_SNAPSHOT = TICKS_PER_SECOND / SNAPSHOT_RATE_HZ;

    /** Cada cuantos ticks piensa un bot (10 Hz). */
    public static final int BOT_THINK_INTERVAL_TICKS = 6;

    private SimulationConstants() {
        throw new AssertionError("Clase de constantes, no instanciable");
    }
}
