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
 * Wraps the target in Arachnet's web ({@code "ensnare"}): they cannot walk, jump or roll until it wears off, but
 * they can still turn, aim and fire. Webs burn, so fire hurts them more meanwhile ({@code fireVulnerability},
 * default 1.3).
 */
public final class EnsnareEffect extends AbstractStatusEffect {

    private final double fireVulnerability;

    public EnsnareEffect(Params params, double fireVulnerability) {
        super(params);
        this.fireVulnerability = fireVulnerability;
    }

    @Override
    public double strength() {
        return 1.0;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.rooted = true;
        stats.multiplyElementTaken(Element.FIRE, fireVulnerability);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "ensnare";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new EnsnareEffect(Params.parse(spec, "ensnare", 0, "ensnare", "debuff", "cc"),
                    spec.optDouble("fireVulnerability", 1.3, 0, 10));
        }
    }
}
