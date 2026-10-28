package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;

/** Restores health ({@code "heal"}): {@code amount}, scaled by the caster's heal potency. */
public final class HealEffect implements Effect {

    private final double amount;

    public HealEffect(double amount) {
        this.amount = amount;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target != null) {
            ctx.arena().heal(ctx.source(), target, amount * ctx.scale() * ctx.sourcePotency());
        }
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "heal";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new HealEffect(spec.requireDouble("amount", 0, 10_000));
        }
    }
}
