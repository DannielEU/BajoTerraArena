package com.bajoterra.core.effect;

import com.bajoterra.core.content.Spec;

/**
 * Builds an {@link Effect} from its JSON spec. Implementations register themselves with
 * {@code @AutoService(EffectFactory.class)}, which generates the {@code META-INF/services} entry at compile time,
 * so adding an effect never requires editing an existing class.
 */
public interface EffectFactory {

    /** Value of the {@code type} field this factory handles, e.g. {@code "damage"}. */
    String type();

    /**
     * Creates the effect.
     *
     * @param spec the effect object (validate every field; errors must be {@code ContentException}s)
     * @param registry lets compound effects build their children
     */
    Effect create(Spec spec, EffectRegistry registry);
}
