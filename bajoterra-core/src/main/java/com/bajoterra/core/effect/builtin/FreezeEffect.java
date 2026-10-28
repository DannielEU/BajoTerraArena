package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/**
 * Nearly immobilises the target and makes it vulnerable to one element ({@code "freeze"}): {@code speedFactor},
 * {@code vulnerableTo} (default FIRE) and {@code vulnerability} (damage multiplier, default 1.25).
 */
public final class FreezeEffect extends AbstractStatusEffect {

    private final double speedFactor;
    private final Element vulnerableTo;
    private final double vulnerability;

    public FreezeEffect(Params params, double speedFactor, Element vulnerableTo, double vulnerability) {
        super(params);
        this.speedFactor = speedFactor;
        this.vulnerableTo = vulnerableTo;
        this.vulnerability = vulnerability;
    }

    @Override
    public double strength() {
        return 1.0 - speedFactor;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.speedMultiplier *= speedFactor;
        stats.multiplyElementTaken(vulnerableTo, vulnerability);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "freeze";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new FreezeEffect(
                    Params.parse(spec, "freeze", 0, "freeze", "debuff", "cc"),
                    spec.optDouble("speedFactor", 0.15, 0, 1),
                    spec.optEnum("vulnerableTo", Element.class, Element.FIRE),
                    spec.optDouble("vulnerability", 1.25, 0, 10));
        }
    }
}
