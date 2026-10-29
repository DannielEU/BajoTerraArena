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
}
