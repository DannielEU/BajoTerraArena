package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/** Prevents moving, firing and starting moves for the duration ({@code "stun"}). */
public final class StunEffect extends AbstractStatusEffect {

    public StunEffect(Params params) {
        super(params);
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.stunned = true;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "stun";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new StunEffect(Params.parse(spec, "stun", 0, "stun", "debuff", "cc"));
        }
    }
}
