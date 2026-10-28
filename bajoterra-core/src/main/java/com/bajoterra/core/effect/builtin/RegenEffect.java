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
import com.google.auto.service.AutoService;

/** Heals over time ({@code "regen"}): {@code hps} health per second, scaled by the caster's heal potency. */
public final class RegenEffect extends AbstractStatusEffect {

    private static final int PERIOD_TICKS = 10;

    private final double hps;

    public RegenEffect(Params params, double hps) {
        super(params);
        this.hps = hps;
    }

    @Override
    public double strength() {
        return hps;
    }

    @Override
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        instance.setValue(hps * ctx.scale() * ctx.sourcePotency());
    }

    @Override
    protected void onPeriodic(StatusInstance instance, Combatant owner, CombatArena arena) {
        double amount = instance.value() * instance.stacks() * PERIOD_TICKS / SimulationConstants.TICKS_PER_SECOND;
        arena.heal(arena.byId(instance.sourceId()), owner, amount);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "regen";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new RegenEffect(
                    Params.parse(spec, "regen", PERIOD_TICKS, "regen", "buff"),
                    spec.requireDouble("hps", 0, 1000));
        }
    }
}
