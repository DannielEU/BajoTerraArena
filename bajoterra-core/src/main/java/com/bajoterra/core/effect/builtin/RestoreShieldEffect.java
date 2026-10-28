package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;

/** Restores shield ({@code "restore_shield"}): {@code amount}, scaled by the caster's heal potency. */
public final class RestoreShieldEffect implements Effect {

    private final double amount;

    public RestoreShieldEffect(double amount) {
        this.amount = amount;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target != null) {
            ctx.arena().restoreShield(ctx.source(), target, amount * ctx.scale() * ctx.sourcePotency());
        }
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "restore_shield";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new RestoreShieldEffect(spec.requireDouble("amount", 0, 10_000));
        }
    }
}
