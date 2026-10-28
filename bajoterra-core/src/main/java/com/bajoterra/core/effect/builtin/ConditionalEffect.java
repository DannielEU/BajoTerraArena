package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;

/**
 * Chooses between two effect lists depending on the target ({@code "conditional"}). Conditions (all that are present
 * must hold): {@code ifTargetHasStatus} (status id) and {@code ifTargetHpBelow} (fraction of max health, 0-1).
 * Children go in {@code then} and, optionally, {@code else}.
 */
public final class ConditionalEffect implements Effect {

    private final String requiredStatus;
    private final double hpBelowFraction;
    private final Effect thenEffect;
    private final Effect elseEffect;

    /**
     * @param requiredStatus status the target must have, or null for no requirement
     * @param hpBelowFraction the target's health fraction must be below this value; 1 or more for no requirement
     */
    public ConditionalEffect(String requiredStatus, double hpBelowFraction, Effect thenEffect, Effect elseEffect) {
        this.requiredStatus = requiredStatus;
        this.hpBelowFraction = hpBelowFraction;
        this.thenEffect = thenEffect;
        this.elseEffect = elseEffect;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        boolean holds = target != null
                && (requiredStatus == null || target.statuses.has(requiredStatus))
                && (hpBelowFraction >= 1.0 || target.vitals.hpFraction() < hpBelowFraction);
        (holds ? thenEffect : elseEffect).apply(ctx);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "conditional";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            String status = spec.optString("ifTargetHasStatus", null);
            double hpBelow = spec.optDouble("ifTargetHpBelow", 1.0, 0, 1);
            if (status == null && hpBelow >= 1.0) {
                throw spec.fail("", "needs ifTargetHasStatus and/or ifTargetHpBelow");
            }
            return new ConditionalEffect(status, hpBelow,
                    registry.createAll(spec.list("then")), registry.createAll(spec.list("else")));
        }
    }
}
