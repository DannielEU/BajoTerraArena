package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.TornadoSpec;
import com.google.auto.service.AutoService;

/**
 * Turns the slug into a tornado where it lands ({@code "tornado"}, Tormato), see {@link TornadoSpec}. On a direct
 * hit it starts right on the victim, who is caught at once; against a wall it starts a little in front of it, and it
 * drifts on the way the slug was flying.
 */
public final class TornadoEffect implements Effect {

    /** How far back from a wall the slug struck the tornado starts. */
    private static final double OFF_WALL = 0.8;

    private final TornadoSpec spec;

    public TornadoEffect(TornadoSpec spec) {
        this.spec = spec;
    }

    @Override
    public void apply(EffectContext ctx) {
        double dirX = ctx.direction().x;
        double dirZ = ctx.direction().z;
        double flat = Math.sqrt(dirX * dirX + dirZ * dirZ);
        if (flat > 1e-3) {
            dirX /= flat;
            dirZ /= flat;
        } else {
            dirX = 0;
            dirZ = 0;
        }
        Combatant target = ctx.target();
        double x;
        double y;
        double z;
        if (target != null) {
            x = target.pos.x;
            y = target.pos.y;
            z = target.pos.z;
        } else {
            x = ctx.point().x - dirX * OFF_WALL;
            y = ctx.point().y;
            z = ctx.point().z - dirZ * OFF_WALL;
        }
        ctx.arena().structures().spawnTornado(ctx.source(), ctx.slug(), x, y, z, dirX, dirZ, spec);
    }

    @Override
    public double tornadoRadius() {
        return spec.radius();
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "tornado";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            double radius = spec.optDouble("radius", 3.5, 0.5, 15);
            return new TornadoEffect(new TornadoSpec(
                    radius,
                    spec.optDouble("eyeRadius", 1.2, 0.2, radius),
                    spec.optDouble("height", 4.5, 0.5, 20),
                    spec.requireDurationTicks(),
                    spec.optDouble("drift", 2.5, 0, 20),
                    spec.optDouble("pull", 24, 0, 200),
                    spec.optDouble("lift", 3, 0, 30),
                    spec.optDouble("allyLift", 12.5, 0, 40),
                    Math.max(1, (int) Math.round(spec.optDouble("periodSeconds", 0.5, 0.05, 10)
                            * SimulationConstants.TICKS_PER_SECOND)),
                    registry.createAll(spec.list("effects")),
                    registry.createAll(spec.list("frostEffects"))));
        }
    }
}
