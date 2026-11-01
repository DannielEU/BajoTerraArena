package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/**
 * Megamorphosis ({@code "megamorph"}): for a while every slug the target fires transforms into its giant form. They
 * deal {@code damage} times the damage, fly {@code size} times as big (easier to land) and the blaster fires
 * {@code fireRate} times as fast.
 */
public final class MegamorphEffect extends AbstractStatusEffect {

    private final double damage;
    private final double size;
    private final double fireRate;

    public MegamorphEffect(Params params, double damage, double size, double fireRate) {
        super(params);
        this.damage = damage;
        this.size = size;
        this.fireRate = fireRate;
    }

    @Override
    public double strength() {
        return damage;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.damageDealtMultiplier *= damage;
        stats.projectileScale *= size;
        stats.fireIntervalMultiplier /= fireRate;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "megamorph";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new MegamorphEffect(Params.parse(spec, "megamorph", 0, "megamorph", "buff"),
                    spec.optDouble("damage", 1.35, 1, 5),
                    spec.optDouble("size", 1.7, 1, 4),
                    spec.optDouble("fireRate", 1.2, 1, 4));
        }
    }
}
