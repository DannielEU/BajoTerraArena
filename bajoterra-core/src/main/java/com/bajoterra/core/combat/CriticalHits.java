package com.bajoterra.core.combat;

import com.bajoterra.core.entity.Combatant;

/**
 * Headshots: a slug whose direct hit lands on the top of an enemy's hitbox makes that hit's damage critical. Only the
 * direct hit counts (not burns, chains or the rest of an explosion), so aiming at the head is what pays off.
 */
public final class CriticalHits {

    /** Fraction of the current hitbox height above which a hit counts as a headshot. */
    public static final double HEAD_FRACTION = 0.8;
    /** Damage multiplier of a critical hit. */
    public static final double MULTIPLIER = 1.5;

    private CriticalHits() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * Whether a slug centred at height {@code impactY} when it touched {@code victim} struck the head.
     * The hitbox shrinks while crouching, and the head goes down with it.
     */
    public static boolean isHeadshot(Combatant victim, double impactY) {
        return impactY >= victim.pos.y + victim.hitHeight() * HEAD_FRACTION;
    }
}
