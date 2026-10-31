package com.bajoterra.core.world;

/**
 * A solid raised during a round (an ice wall): an axis-aligned box that stops slugs, bodies and sight like any
 * obstacle of the map until it has taken {@link #hitsLeft} hits or its time runs out. Instances live in a fixed pool
 * of the {@link World} and are reused, never allocated during play.
 */
public final class Barrier {

    public boolean active;
    /** Unique, increasing id (snapshots match barriers between frames by it). */
    public int id;
    /** Combatant who raised it, or -1. */
    public int ownerId = -1;
    /** Index of the slug that raised it, or -1. */
    public int slugIndex = -1;
    public double minX;
    public double minY;
    public double minZ;
    public double maxX;
    public double maxY;
    public double maxZ;
    /** Hits it can still take; the last one breaks it. */
    public int hitsLeft;
    /** Ticks before it melts away. */
    public int ticksLeft;
    /** Ticks it was raised for (to tell how far it has melted). */
    public int lifeTicks;

    /** True when (x, z) lies within the barrier's footprint grown by {@code margin}. */
    public boolean containsXZ(double x, double z, double margin) {
        return x >= minX - margin && x <= maxX + margin && z >= minZ - margin && z <= maxZ + margin;
    }
}
