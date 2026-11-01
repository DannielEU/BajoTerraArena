package com.bajoterra.core.ammo;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.entity.InputState;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Trig;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.move.LocomotionStateMachine;
import com.bajoterra.core.projectile.ProjectileSystem;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.World;

/**
 * Turns fire input into projectiles: validates the shot (alive, not stunned, interval elapsed, selected slug
 * unlocked), works out where the slug starts and where it flies, launches it and starts the lock on the slot.
 *
 * <p>With full fusion energy, the fusion button fires a fusion shot: the selected slug and the next available one
 * ({@link Loadout#fusionPartner()}) leave the blaster together as one projectile, both slots are locked and the energy
 * is spent.
 */
public final class FireController {

    /** How far the aiming ray of a third-person camera reaches, in metres. */
    private static final double MAX_AIM_DISTANCE = 80.0;
    /** Distance in front of the body at which slugs leave the blaster. */
    private static final double MUZZLE_FORWARD = 0.55;
    /** Fraction of the hitbox height at which the blaster sits. */
    private static final double MUZZLE_HEIGHT_FRACTION = 0.72;
    /** A fusion shot takes the blaster this much longer to recover from than its slower slug alone. */
    private static final double FUSION_DELAY = 1.25;

    private final CombatArena arena;
    private final World world;
    private final ProjectileSystem projectiles;
    private final SlotSelectionPolicy selectionPolicy;
    private final Vec3 origin = new Vec3();
    private final Vec3 direction = new Vec3();
    private final Vec3 cameraDirection = new Vec3();
    private final Vec3 aimPoint = new Vec3();

    public FireController(CombatArena arena, World world, ProjectileSystem projectiles,
            SlotSelectionPolicy selectionPolicy) {
        this.arena = arena;
        this.world = world;
        this.projectiles = projectiles;
        this.selectionPolicy = selectionPolicy;
    }

    /** Processes slot selection and fire input for one combatant on this tick. */
    public void update(Combatant c) {
        InputState input = c.input;
        if (c.alive) {
            if (input.selectSlot >= 0) {
                c.loadout.select(input.selectSlot);
            }
            if (input.cycleSlot != 0) {
                c.loadout.cycle(input.cycleSlot);
            }
        }
        input.consumeEdges();
        boolean fusion = input.fusion && c.fusionReady();
        if ((input.fire || fusion) && canFire(c)) {
            fire(c, fusion);
        }
    }

    /** True when the combatant may fire right now. */
    public boolean canFire(Combatant c) {
        return c.alive
                && !c.stats.stunned
                && arena.tick() >= c.nextFireTick
                && c.loadout.isAvailable(c.loadout.selected())
                && !LocomotionStateMachine.blocksFiring(c);
    }

    private void fire(Combatant c, boolean fusion) {
        computeShot(c);
        int slot = c.loadout.selected();
        int partnerSlot = fusion ? c.loadout.fusionPartner() : -1;
        SlugDefinition slug = c.loadout.fireSelected();
        SlugDefinition partner = partnerSlot >= 0 ? c.loadout.fireAlongside(partnerSlot) : null;
        projectiles.spawn(c, slug, partner, origin, direction);
        c.shotsFired++;
        double delay = slug.fireDelayMultiplier();
        if (partner != null) {
            c.fusion = 0;
            delay = Math.max(delay, partner.fireDelayMultiplier()) * FUSION_DELAY;
            GameEvent fused = arena.events().begin(EventType.FUSION, arena.tick());
            fused.actor = c.id;
            fused.a = slug.index();
            fused.b = partner.index();
            fused.x = origin.x;
            fused.y = origin.y;
            fused.z = origin.z;
        }
        int interval = Math.max(1, (int) Math.round(c.character.blaster().fireIntervalTicks()
                * delay * c.stats.fireIntervalMultiplier));
        c.nextFireTick = arena.tick() + interval;
        selectionPolicy.afterFire(c.loadout, slot);

        GameEvent event = arena.events().begin(EventType.SHOT, arena.tick());
        event.actor = c.id;
        event.a = slug.index();
        event.b = slot;
        event.x = origin.x;
        event.y = origin.y;
        event.z = origin.z;
        event.value = interval;
    }

    /** Fills {@link #origin} and {@link #direction} for the combatant's current input. */
    private void computeShot(Combatant c) {
        InputState input = c.input;
        double forwardX = Trig.sin(c.yaw);
        double forwardZ = Trig.cos(c.yaw);
        origin.set(c.pos.x + forwardX * MUZZLE_FORWARD,
                c.pos.y + c.hitHeight() * MUZZLE_HEIGHT_FRACTION,
                c.pos.z + forwardZ * MUZZLE_FORWARD);
        directionFromAngles(direction, input.aimYaw, input.aimPitch);
        if (input.hasCamera) {
            // Third person: the crosshair is on the camera ray; the slug must reach that point from the blaster.
            cameraDirection.set(direction);
            double distance = world.projectileRayDistance(input.camX, input.camY, input.camZ,
                    cameraDirection.x, cameraDirection.y, cameraDirection.z, MAX_AIM_DISTANCE);
            aimPoint.set(input.camX + cameraDirection.x * distance,
                    input.camY + cameraDirection.y * distance,
                    input.camZ + cameraDirection.z * distance);
            direction.set(aimPoint).sub(origin).normalize();
            if (direction.dot(cameraDirection) < 0.2) {
                direction.set(cameraDirection);
            }
        }
        double spread = c.stats.aimSpread;
        if (spread > 0) {
            direction.x += (arena.random().nextDouble() - 0.5) * 2 * spread;
            direction.y += (arena.random().nextDouble() - 0.5) * 2 * spread;
            direction.z += (arena.random().nextDouble() - 0.5) * 2 * spread;
            direction.normalize();
        }
    }

    /** Writes the unit direction for a yaw/pitch pair (yaw 0 = +Z, positive pitch looks up) into {@code out}. */
    public static void directionFromAngles(Vec3 out, double yaw, double pitch) {
        double cosPitch = Trig.cos(pitch);
        out.set(Trig.sin(yaw) * cosPitch, Trig.sin(pitch), Trig.cos(yaw) * cosPitch);
    }
}
