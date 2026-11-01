package com.bajoterra.core.effect;

/**
 * Something that happens when a slug (or a pickup) takes effect (Strategy / Composite). Implementations are
 * immutable and shared, so they must keep all per-use state in the {@link EffectContext} or in the combatants.
 */
@FunctionalInterface
public interface Effect {

    /** An effect that does nothing. */
    Effect NONE = ctx -> {
        // intentionally empty
    };

    void apply(EffectContext ctx);

    /** Radius in metres of the largest area this effect reaches around its point (0 when it has no area). */
    default double areaRadius() {
        return 0;
    }

    /** Radius in metres within which this effect's blinding flash can be seen (0 when it has none). */
    default double flashRadius() {
        return 0;
    }

    /**
     * Cosine of the half-angle of the beam a flash shines in, around the way its slug faced (-1 = all around). Only
     * meaningful when {@link #flashRadius()} is positive.
     */
    default double flashBeamCos() {
        return -1;
    }

    /** True when this effect raises a wall (an ice wall), a slug used for cover rather than to hit somebody. */
    default boolean raisesWall() {
        return false;
    }

    /** Radius in metres of the tornado this effect starts (0 when it starts none). */
    default double tornadoRadius() {
        return 0;
    }

    /** Radius in metres of the web trap this effect spins (0 when it spins none). */
    default double webRadius() {
        return 0;
    }
}
