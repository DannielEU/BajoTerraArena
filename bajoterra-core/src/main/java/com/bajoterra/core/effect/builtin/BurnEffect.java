package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.Element;
import com.google.auto.service.AutoService;

/** Damage over time ({@code "burn"}): {@code dps} fire damage per second plus the common status fields. */
public final class BurnEffect extends AbstractStatusEffect {

    private static final int PERIOD_TICKS = 10;

    private final double dps;

    public BurnEffect(Params params, double dps) {
        super(params);
        this.dps = dps;
    }

    @Override
    public double strength() {
        return dps;
    }

    @Override
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        instance.setValue(dps * ctx.scale());
    }

    @Override
    protected void onPeriodic(StatusInstance instance, Combatant owner, CombatArena arena) {
        double damage = instance.value() * instance.stacks() * PERIOD_TICKS / SimulationConstants.TICKS_PER_SECOND;
        arena.damage(arena.byId(instance.sourceId()), owner, damage, Element.FIRE, 0);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "burn";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new BurnEffect(
                    Params.parse(spec, "burn", PERIOD_TICKS, "burn", "debuff", "dot"),
                    spec.requireDouble("dps", 0, 1000));
        }
    }
}
