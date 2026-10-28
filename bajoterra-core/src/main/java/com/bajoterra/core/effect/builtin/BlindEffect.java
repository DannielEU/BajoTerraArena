package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/** Adds random aim error in radians to the target's shots ({@code "blind"}). */
public final class BlindEffect extends AbstractStatusEffect {

    private final double spread;

    public BlindEffect(Params params, double spread) {
        super(params);
        this.spread = spread;
    }

    @Override
    public double strength() {
        return spread;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.aimSpread += spread;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "blind";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new BlindEffect(Params.parse(spec, "blind", 0, "blind", "debuff"),
                    spec.optDouble("spread", 0.25, 0, 1.5));
        }
    }
}
