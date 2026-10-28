package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/** Reduces movement speed to a {@code factor} of normal ({@code "slow"}; 0.4 means 60 % slower). */
public final class SlowEffect extends AbstractStatusEffect {

    private final double factor;

    public SlowEffect(Params params, double factor) {
        super(params);
        this.factor = factor;
    }

    @Override
    public double strength() {
        return 1.0 - factor;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.speedMultiplier *= factor;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "slow";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new SlowEffect(Params.parse(spec, "slow", 0, "slow", "debuff", "cc"),
                    spec.requireDouble("factor", 0, 1));
        }
    }
}
