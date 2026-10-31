package com.bajoterra.core.world;

/**
 * A small round pool of shallow water on the floor, centred on (x, z). Purely decorative: the simulation ignores it
 * (it neither slows nor hides anyone); clients draw it and splash it when combatants or slugs go through.
 */
public record WaterPool(double x, double z, double radius) {

    /** Smallest pool worth drawing. */
    public static final double MIN_RADIUS = 0.6;
    /** A pool stays a small corner of the arena, never a lake or a river. */
    public static final double MAX_RADIUS = 2.5;
}
