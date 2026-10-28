package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;

/**
 * After the first target, jumps to the nearest other valid combatant within {@code radius}, up to {@code jumps}
 * times, applying the child effects each time with the magnitude multiplied by {@code decay} per jump
 * ({@code "chain"}; the Tazerling's arc lightning).
 */
public final class ChainEffect implements Effect {

    private final double radius;
    private final int jumps;
    private final double decay;
    private final Effect children;

    public ChainEffect(double radius, int jumps, double decay, Effect children) {
        this.radius = radius;
        this.jumps = jumps;
        this.decay = decay;
        this.children = children;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant original = ctx.target();
        Combatant current = original;
        if (current == null) {
            return;
        }
        double originalScale = ctx.scale();
        double scale = originalScale;
        int visited = 1 << current.id;
        for (int jump = 0; jump < jumps; jump++) {
            Combatant best = null;
            double bestDistance = radius * radius;
            for (Combatant candidate : ctx.arena().combatants()) {
                if (!candidate.alive || (visited & (1 << candidate.id)) != 0) {
                    continue;
                }
                if (ctx.source() != null && !ctx.targeting().accepts(ctx.source(), candidate)) {
                    continue;
                }
                double distance = current.pos.distanceSq(candidate.pos);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (best == null) {
                break;
            }
            visited |= 1 << best.id;
            scale *= decay;
            ctx.setTarget(best);
            ctx.setScale(scale);
            children.apply(ctx);
            current = best;
        }
        ctx.setTarget(original);
        ctx.setScale(originalScale);
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "chain";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            var childSpecs = spec.list("effects");
            if (childSpecs.isEmpty()) {
                throw spec.fail("effects", "a chain effect needs at least one child effect");
            }
            return new ChainEffect(
                    spec.requireDouble("radius", 0.5, 50),
                    spec.optInt("jumps", 1, 1, 5),
                    spec.optDouble("decay", 0.6, 0, 1),
                    registry.createAll(childSpecs));
        }
    }
}
