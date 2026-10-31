package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/**
 * Blinds the target ({@code "blind"}): random aim error in radians on its shots and a {@link StatBlock#blindness}
 * that bots treat as not seeing anything (players see a white screen). Both follow the context's scale, so a flash
 * seen squarely blinds harder than one caught from the corner of the eye; with {@code scaleDuration} the scale also
 * shortens the blindness. It wears off over its last {@link #FADE_TICKS} ticks.
 */
public final class BlindEffect extends AbstractStatusEffect {

    /** Ticks over which the blindness fades out at the end. */
    public static final int FADE_TICKS = SimulationConstants.TICKS_PER_SECOND;

    private final double spread;
    private final boolean scaleDuration;

    public BlindEffect(Params params, double spread, boolean scaleDuration) {
        super(params);
        this.spread = spread;
        this.scaleDuration = scaleDuration;
    }

    @Override
    public double strength() {
        return spread;
    }

    @Override
    protected int durationFor(EffectContext ctx) {
        if (!scaleDuration) {
            return durationTicks();
        }
        return Math.max(1, (int) Math.round(durationTicks() * Math.min(1.0, ctx.scale())));
    }

    @Override
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        instance.setValue(Math.min(1.0, Math.max(0.0, ctx.scale())));
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        double fade = Math.min(1.0, instance.remainingTicks() / (double) FADE_TICKS);
        double level = instance.value() * fade;
        stats.aimSpread += spread * level;
        stats.blindness = Math.max(stats.blindness, level);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "blind";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new BlindEffect(Params.parse(spec, "blind", 0, "blind", "debuff"),
                    spec.optDouble("spread", 0.25, 0, 1.5), spec.optBoolean("scaleDuration", false));
        }
    }
}
