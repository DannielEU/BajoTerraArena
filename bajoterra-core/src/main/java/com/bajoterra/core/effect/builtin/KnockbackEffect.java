package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;

/** Pushes the target away from the impact point ({@code "knockback"}): {@code force} in m/s, optional {@code lift}. */
public final class KnockbackEffect implements Effect {

    private final double force;
    private final double lift;

    public KnockbackEffect(double force, double lift) {
        this.force = force;
        this.lift = lift;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target == null) {
            return;
        }
        double dx = target.pos.x - ctx.point().x;
        double dz = target.pos.z - ctx.point().z;
        if (dx * dx + dz * dz < 1e-6) {
            dx = ctx.direction().x;
            dz = ctx.direction().z;
        }
        ctx.arena().knockback(target, dx, dz, force * ctx.scale(), lift * ctx.scale());
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "knockback";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new KnockbackEffect(spec.requireDouble("force", 0, 100), spec.optDouble("lift", 0, 0, 30));
        }
    }
}
