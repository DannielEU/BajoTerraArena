package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;

/** Grants a temporary shield that decays to zero ({@code "overshield"}): {@code amount} and a duration. */
public final class OvershieldEffect implements Effect {

    private final double amount;
    private final int durationTicks;

    public OvershieldEffect(double amount, int durationTicks) {
        this.amount = amount;
        this.durationTicks = durationTicks;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target != null) {
            ctx.arena().grantOvershield(ctx.source(), target, amount * ctx.scale() * ctx.sourcePotency(),
                    durationTicks);
        }
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "overshield";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new OvershieldEffect(spec.requireDouble("amount", 0, 10_000), spec.requireDurationTicks());
        }
    }
}
