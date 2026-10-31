package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.math.Vec3;
import com.google.auto.service.AutoService;

/**
 * Raises a wall of ice where the slug lands ({@code "ice_wall"}), across the way it was flying, to hide behind: it
 * stops slugs, bodies and sight like any solid of the map until it has taken {@code hits} hits (two by default) or
 * melts after its duration. A slug that hits somebody raises it just in front of them, never on top of them.
 */
public final class IceWallEffect implements Effect {

    private final double width;
    private final double height;
    private final double thickness;
    private final int hits;
    private final int durationTicks;

    public IceWallEffect(double width, double height, double thickness, int hits, int durationTicks) {
        this.width = width;
        this.height = height;
        this.thickness = thickness;
        this.hits = hits;
        this.durationTicks = durationTicks;
    }

    @Override
    public void apply(EffectContext ctx) {
        Vec3 direction = ctx.direction();
        double dirX = direction.x;
        double dirZ = direction.z;
        double flat = Math.sqrt(dirX * dirX + dirZ * dirZ);
        Combatant source = ctx.source();
        if (flat < 1e-3 && source != null) {
            dirX = ctx.point().x - source.pos.x;
            dirZ = ctx.point().z - source.pos.z;
            flat = Math.sqrt(dirX * dirX + dirZ * dirZ);
        }
        if (flat < 1e-3) {
            dirX = 0;
            dirZ = 1;
            flat = 1;
        }
        dirX /= flat;
        dirZ /= flat;
        double x = ctx.point().x;
        double z = ctx.point().z;
        Combatant target = ctx.target();
        if (target != null) {
            // Never on top of the one it hit: just in front of them, towards whoever fired it.
            double back = target.hitRadius() + thickness * 0.5 + 0.15;
            x = target.pos.x - dirX * back;
            z = target.pos.z - dirZ * back;
        }
        ctx.arena().structures().raiseWall(source, ctx.slug() == null ? -1 : ctx.slug().index(), x,
                ctx.point().y, z, dirX, dirZ, width, height, thickness, hits, durationTicks);
    }

    @Override
    public boolean raisesWall() {
        return true;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "ice_wall";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new IceWallEffect(
                    spec.optDouble("width", 3.2, 0.5, 10),
                    spec.optDouble("height", 2.2, 0.5, 6),
                    spec.optDouble("thickness", 0.5, 0.2, 2),
                    spec.optInt("hits", 2, 1, 20),
                    spec.requireDurationTicks());
        }
    }
}
