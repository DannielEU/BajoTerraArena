package com.bajoterra.core.combat;

/**
 * Whether anything that blocks sight stands between two points. The combat layer knows nothing about maps, so the
 * simulation hands its world's line-of-sight test to the {@link CombatArena}; effects that depend on what a combatant
 * can see (a blinding flash) ask through it.
 */
@FunctionalInterface
public interface SightLine {

    /** Open ground: nothing ever blocks sight (unit tests of effects without a map). */
    SightLine OPEN = (ax, ay, az, bx, by, bz) -> true;

    /** True when nothing blocks sight between (ax, ay, az) and (bx, by, bz). */
    boolean clear(double ax, double ay, double az, double bx, double by, double bz);
}
