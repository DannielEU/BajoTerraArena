package com.bajoterra.core.combat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TornadoSpec;
import com.bajoterra.core.slug.WebSpec;

/**
 * Builds things in the arena during a round: an ice wall, a tornado, a web trap. The combat layer knows nothing about
 * maps, so the simulation hands its implementation to the {@link CombatArena}; effects that build something ask
 * through it. Every method does nothing by default (unit tests of effects without a map).
 */
public interface Structures {

    /** Nothing can be built. */
    Structures NONE = new Structures() {
    };

    /**
     * Raises a wall standing on the surface under (x, y, z), across the horizontal direction (dirX, dirZ) so it
     * blocks whatever comes along it.
     *
     * @param owner who raised it (may be null)
     * @param hits hits it takes before it breaks
     * @param ticks ticks before it melts
     * @return true when the wall was raised
     */
    default boolean raiseWall(Combatant owner, int slugIndex, double x, double y, double z, double dirX, double dirZ,
            double width, double height, double thickness, int hits, int ticks) {
        return false;
    }

    /**
     * Starts a tornado on the surface under (x, y, z), drifting along the horizontal direction (dirX, dirZ).
     *
     * @return true when the tornado was started
     */
    default boolean spawnTornado(Combatant owner, SlugDefinition slug, double x, double y, double z, double dirX,
            double dirZ, TornadoSpec spec) {
        return false;
    }

    /**
     * Spins a web trap on the surface under (x, y, z).
     *
     * @return true when the web was spun
     */
    default boolean spinWeb(Combatant owner, SlugDefinition slug, double x, double y, double z, WebSpec spec) {
        return false;
    }
}
