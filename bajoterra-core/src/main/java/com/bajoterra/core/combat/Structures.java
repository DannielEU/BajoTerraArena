package com.bajoterra.core.combat;

import com.bajoterra.core.entity.Combatant;

/**
 * Raises solids in the arena during a round (an ice wall). The combat layer knows nothing about maps, so the
 * simulation hands its implementation to the {@link CombatArena}; effects that build something ask through it.
 */
@FunctionalInterface
public interface Structures {

    /** Nothing can be built (unit tests of effects without a map). */
    Structures NONE = (owner, slugIndex, x, y, z, dirX, dirZ, width, height, thickness, hits, ticks) -> false;

    /**
     * Raises a wall standing on the surface under (x, y, z), across the horizontal direction (dirX, dirZ) so it
     * blocks whatever comes along it.
     *
     * @param owner who raised it (may be null)
     * @param hits hits it takes before it breaks
     * @param ticks ticks before it melts
     * @return true when the wall was raised
     */
    boolean raiseWall(Combatant owner, int slugIndex, double x, double y, double z, double dirX, double dirZ,
            double width, double height, double thickness, int hits, int ticks);
}
