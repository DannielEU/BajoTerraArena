package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.slug.WebSpec;
import com.google.auto.service.AutoService;

/**
 * Spins a web trap on the floor where the slug lands ({@code "web"}, Arachnet), see {@link WebSpec}. Against a wall
 * the web is spun on the floor at its foot, on the side the slug came from.
 */
public final class WebEffect implements Effect {

    /** How far back from a wall the slug struck the web is spun. */
    private static final double OFF_WALL = 0.9;

    private final WebSpec spec;

    public WebEffect(WebSpec spec) {
        this.spec = spec;
    }

    @Override
    public void apply(EffectContext ctx) {
        double dirX = ctx.direction().x;
        double dirZ = ctx.direction().z;
        double flat = Math.sqrt(dirX * dirX + dirZ * dirZ);
        double back = flat > 1e-3 ? OFF_WALL / flat : 0;
        ctx.arena().structures().spinWeb(ctx.source(), ctx.slug(), ctx.point().x - dirX * back, ctx.point().y,
                ctx.point().z - dirZ * back, spec);
    }

    @Override
    public double webRadius() {
        return spec.radius();
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "web";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            var effects = spec.list("effects");
            if (effects.isEmpty()) {
                throw spec.fail("effects", "a web needs at least one effect");
            }
            return new WebEffect(new WebSpec(
                    spec.optDouble("radius", 1.5, 0.3, 6),
                    spec.requireDurationTicks(),
                    registry.createAll(effects)));
        }
    }
}
