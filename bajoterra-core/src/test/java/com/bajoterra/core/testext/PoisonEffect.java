package com.bajoterra.core.testext;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.Element;
import com.google.auto.service.AutoService;

/**
 * A brand-new effect added entirely outside the core, to prove the game is open for extension: one class, one
 * JSON file, no edit to any existing source file.
 */
public final class PoisonEffect extends AbstractStatusEffect {

    private static final int PERIOD_TICKS = 15;

    private final double dps;

    public PoisonEffect(Params params, double dps) {
        super(params);
        this.dps = dps;
    }

    @Override
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        instance.setValue(dps);
    }

    @Override
    protected void onPeriodic(StatusInstance instance, Combatant owner, CombatArena arena) {
        // Poison ignores the shield: it goes straight into the health pool.
        arena.damage(arena.byId(instance.sourceId()), owner, instance.value() * PERIOD_TICKS / 60.0,
                Element.NEUTRAL, 1.0);
    }

    /** Registered through the service loader like any built-in effect. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "poison";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new PoisonEffect(Params.parse(spec, "poison", PERIOD_TICKS, "poison", "debuff"),
                    spec.requireDouble("dps", 0, 1000));
        }
    }
}
