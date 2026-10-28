package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.google.auto.service.AutoService;

/**
 * Applies child effects to every valid combatant within a radius of the impact point ({@code "area"}). Valid targets
 * follow the slug's targeting rule, so an area heal helps only allies and an explosion only hurts enemies. The
 * children's magnitude is scaled by distance when {@code falloff} is {@code LINEAR}.
 */
public final class AreaEffect implements Effect {

    /** How the magnitude fades with distance from the centre. */
    public enum Falloff {
        /** Full magnitude everywhere inside the radius. */
        NONE,
        /** Linear fade from full magnitude at the centre down to {@code minScale} at the edge. */
        LINEAR
    }

    private final double radius;
    private final Falloff falloff;
    private final double minScale;
    private final Effect children;

    public AreaEffect(double radius, Falloff falloff, double minScale, Effect children) {
        this.radius = radius;
        this.falloff = falloff;
        this.minScale = minScale;
        this.children = children;
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant originalTarget = ctx.target();
        double originalScale = ctx.scale();
        GameEvent event = ctx.arena().events().begin(EventType.EXPLOSION, ctx.arena().tick());
        event.actor = ctx.source() == null ? -1 : ctx.source().id;
        event.x = ctx.point().x;
        event.y = ctx.point().y;
        event.z = ctx.point().z;
        event.value = radius;
        for (Combatant candidate : ctx.arena().combatants()) {
            if (!candidate.alive) {
                continue;
            }
            if (ctx.source() != null && !ctx.targeting().accepts(ctx.source(), candidate)) {
                continue;
            }
            double distance = distanceToHitbox(ctx, candidate);
            if (distance > radius) {
                continue;
            }
            double fade = falloff == Falloff.LINEAR ? Math.max(minScale, 1.0 - distance / radius) : 1.0;
            ctx.setTarget(candidate);
            ctx.setScale(originalScale * fade);
            children.apply(ctx);
        }
        ctx.setTarget(originalTarget);
        ctx.setScale(originalScale);
    }

    private static double distanceToHitbox(EffectContext ctx, Combatant candidate) {
        double dx = candidate.pos.x - ctx.point().x;
        double dz = candidate.pos.z - ctx.point().z;
        double clampedY = Math.min(Math.max(ctx.point().y, candidate.pos.y), candidate.pos.y + candidate.hitHeight());
        double dy = clampedY - ctx.point().y;
        double axisDistance = Math.sqrt(dx * dx + dy * dy + dz * dz);
        return Math.max(0, axisDistance - candidate.hitRadius());
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "area";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            var childSpecs = spec.list("effects");
            if (childSpecs.isEmpty()) {
                throw spec.fail("effects", "an area effect needs at least one child effect");
            }
            return new AreaEffect(
                    spec.requireDouble("radius", 0.1, 50),
                    spec.optEnum("falloff", Falloff.class, Falloff.LINEAR),
                    spec.optDouble("minScale", 0.2, 0, 1),
                    registry.createAll(childSpecs));
        }
    }
}
