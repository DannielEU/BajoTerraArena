package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.combat.CriticalHits;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.Element;
import com.google.auto.service.AutoService;

/**
 * Direct damage ({@code "damage"}): {@code amount}, optional {@code element} (defaults to the slug's) and
 * {@code shieldPierce}. Damage to the combatant a slug struck in the head is multiplied by
 * {@link CriticalHits#MULTIPLIER}.
 */
public final class DamageEffect implements Effect {

    private final double amount;
    private final Element element;
    private final double shieldPierce;

    /**
     * @param amount base damage, multiplied by the context scale
     * @param element element of the damage, or null to use the slug's element
     * @param shieldPierce fraction in [0, 1] that bypasses shield
     */
    public DamageEffect(double amount, Element element, double shieldPierce) {
        this.amount = amount;
        this.element = element;
        this.shieldPierce = shieldPierce;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target == null) {
            return;
        }
        Element used = element;
        if (used == null) {
            used = ctx.slug() != null ? ctx.slug().element() : Element.NEUTRAL;
        }
        boolean critical = target == ctx.criticalTarget();
        double dealt = amount * ctx.scale() * (critical ? CriticalHits.MULTIPLIER : 1.0);
        ctx.arena().damage(ctx.source(), target, dealt, used, shieldPierce, critical);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "damage";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new DamageEffect(
                    spec.requireDouble("amount", 0, 10_000),
                    spec.optEnum("element", Element.class, null),
                    spec.optDouble("shieldPierce", 0, 0, 1));
        }
    }
}
