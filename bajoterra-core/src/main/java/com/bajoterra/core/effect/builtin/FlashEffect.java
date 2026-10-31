package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Trig;
import com.bajoterra.core.math.Vec3;
import com.google.auto.service.AutoService;

/**
 * A blinding burst of light ({@code "flash"}). Its child effects (a {@code blind}) reach every combatant who
 * <em>sees</em> the point when it goes off, whatever their team, the thrower included; the only way to escape it is
 * not to look. Seeing it takes three things:
 * <ul>
 *   <li>being within {@code radius} metres of it,</li>
 *   <li>nothing that blocks sight between the eyes and the point (a wall or a crate hides it), and</li>
 *   <li>having it in view: the point lies within {@code viewAngle} degrees of where the combatant looks (55 by
 *       default, about the edge of the screen), seen from the over-the-shoulder camera players look through:
 *       the real one a client sends with its input, or the same camera placed behind a bot. Seen from there, a slug
 *       stuck on your own chest or landing at your feet is right in the middle of the picture.</li>
 * </ul>
 * With {@code beamAngle} below 180 the light shines forwards only, in a wide beam around the way the slug was facing
 * when it stopped (towards whoever its proximity fuse caught): whoever is behind it, like the slinger who fired it,
 * is out of the beam. Anyone in the beam, or within {@link #ALWAYS_IN_BEAM} metres of it, can be blinded.
 * The children are scaled by how squarely and how closely it was seen: 1 staring straight at it from up close, down
 * to {@code minScale} for one caught at the edge of the view at the far end of the radius.
 */
public final class FlashEffect implements Effect {

    /** Height of the eyes as a fraction of the current hitbox height. */
    public static final double EYE_FRACTION = 0.9;
    /** The players' camera (the client's ThirdPersonCamera): pivot height, distance behind, shoulder offset. */
    static final double CAMERA_PIVOT_FRACTION = 0.88;
    static final double CAMERA_DISTANCE = 3.4;
    static final double CAMERA_SHOULDER = 0.6;
    /** So close to the flash that its beam reaches you whatever way it faces. */
    static final double ALWAYS_IN_BEAM = 1.5;

    private final double radius;
    private final double viewCos;
    private final double beamCos;
    private final double minScale;
    private final Effect children;

    public FlashEffect(double radius, double viewAngleDegrees, double beamAngleDegrees, double minScale,
            Effect children) {
        this.radius = radius;
        this.viewCos = Trig.cos(Math.toRadians(viewAngleDegrees));
        this.beamCos = beamAngleDegrees >= 180 ? -1 : Trig.cos(Math.toRadians(beamAngleDegrees));
        this.minScale = minScale;
        this.children = children;
    }

    @Override
    public void apply(EffectContext ctx) {
        CombatArena arena = ctx.arena();
        Vec3 point = ctx.point();
        GameEvent event = arena.events().begin(EventType.FLASH, arena.tick());
        event.actor = ctx.source() == null ? -1 : ctx.source().id;
        event.a = ctx.slug() == null ? -1 : ctx.slug().index();
        event.x = point.x;
        event.y = point.y;
        event.z = point.z;
        event.value = radius;
        Combatant originalTarget = ctx.target();
        double originalScale = ctx.scale();
        for (Combatant viewer : arena.combatants()) {
            if (!viewer.alive) {
                continue;
            }
            double seen = seen(arena, viewer, point, ctx.direction());
            if (seen <= 0) {
                continue;
            }
            ctx.setTarget(viewer);
            ctx.setScale(originalScale * seen);
            children.apply(ctx);
        }
        ctx.setTarget(originalTarget);
        ctx.setScale(originalScale);
    }

    /**
     * How strongly {@code viewer} sees a flash at {@code point} shining along {@code shining}: 0 when it does not see
     * it at all (too far, out of its beam, behind a wall or looking away), otherwise between {@code minScale} and 1.
     */
    public double seen(CombatArena arena, Combatant viewer, Vec3 point, Vec3 shining) {
        double eyeY = viewer.pos.y + viewer.hitHeight() * EYE_FRACTION;
        double dx = point.x - viewer.pos.x;
        double dy = point.y - eyeY;
        double dz = point.z - viewer.pos.z;
        double distance = Math.sqrt(dx * dx + dy * dy + dz * dz);
        if (distance > radius || !inBeam(viewer, point, shining)) {
            return 0;
        }
        double cosPitch = Trig.cos(viewer.pitch);
        double forwardX = Trig.sin(viewer.yaw) * cosPitch;
        double forwardY = Trig.sin(viewer.pitch);
        double forwardZ = Trig.cos(viewer.yaw) * cosPitch;
        double camX;
        double camY;
        double camZ;
        if (viewer.input.hasCamera) {
            camX = viewer.input.camX;
            camY = viewer.input.camY;
            camZ = viewer.input.camZ;
        } else {
            double pivotY = viewer.pos.y + viewer.hitHeight() * CAMERA_PIVOT_FRACTION;
            camX = viewer.pos.x - forwardX * CAMERA_DISTANCE + Trig.cos(viewer.yaw) * CAMERA_SHOULDER;
            camY = pivotY - forwardY * CAMERA_DISTANCE;
            camZ = viewer.pos.z - forwardZ * CAMERA_DISTANCE - Trig.sin(viewer.yaw) * CAMERA_SHOULDER;
        }
        double vx = point.x - camX;
        double vy = point.y - camY;
        double vz = point.z - camZ;
        double viewDistance = Math.sqrt(vx * vx + vy * vy + vz * vz);
        double facing = viewDistance < 1e-6 ? 1.0 : (vx * forwardX + vy * forwardY + vz * forwardZ) / viewDistance;
        if (facing < viewCos) {
            return 0;
        }
        if (!arena.sight().clear(viewer.pos.x, eyeY, viewer.pos.z, point.x, point.y, point.z)) {
            return 0;
        }
        double squarely = viewCos >= 1 ? 1 : (facing - viewCos) / (1 - viewCos);
        double near = 1 - distance / radius;
        return Math.max(minScale, (0.4 + 0.6 * squarely) * (0.45 + 0.55 * near));
    }

    /** True when the viewer stands in the flash's beam (always for a flash that shines all around). */
    private boolean inBeam(Combatant viewer, Vec3 point, Vec3 facing) {
        double fx = facing.x;
        double fz = facing.z;
        double length = Math.sqrt(fx * fx + fz * fz);
        if (beamCos <= -1 || length < 1e-3) {
            return true;
        }
        double tx = viewer.pos.x - point.x;
        double tz = viewer.pos.z - point.z;
        double flat = Math.sqrt(tx * tx + tz * tz);
        return flat < ALWAYS_IN_BEAM || (tx * fx + tz * fz) / (flat * length) >= beamCos;
    }

    @Override
    public double flashRadius() {
        return radius;
    }

    @Override
    public double flashBeamCos() {
        return beamCos;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "flash";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            var childSpecs = spec.list("effects");
            if (childSpecs.isEmpty()) {
                throw spec.fail("effects", "a flash needs at least one child effect");
            }
            return new FlashEffect(
                    spec.requireDouble("radius", 1, 80),
                    spec.optDouble("viewAngle", 55, 5, 180),
                    spec.optDouble("beamAngle", 180, 10, 180),
                    spec.optDouble("minScale", 0.3, 0.05, 1),
                    registry.createAll(childSpecs));
        }
    }
}
