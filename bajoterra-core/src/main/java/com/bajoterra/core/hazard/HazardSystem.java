package com.bajoterra.core.hazard;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TrailSpec;

/**
 * The walls of fire left on the floor by slugs such as Infurnus ({@link TrailSpec}): a fixed pool of burning
 * patches. Every tick the patches burn down, and a combatant the slug could hit who stands in one suffers the trail's
 * effects, at most once per the trail's period however many patches overlap. Nothing here allocates during play.
 */
public final class HazardSystem {

    /** Patches burning at once at most; a new one replaces the one closest to going out. */
    public static final int CAPACITY = 160;
    /** How far below a patch's floor a pair of feet still counts as standing in it. */
    private static final double BELOW = 0.3;

    private final CombatArena arena;
    private final Hazard[] hazards = new Hazard[CAPACITY];
    private final EffectContext context = new EffectContext();
    private final Vec3 point = new Vec3();
    private int nextId = 1;

    public HazardSystem(CombatArena arena) {
        this.arena = arena;
        for (int i = 0; i < CAPACITY; i++) {
            hazards[i] = new Hazard();
        }
    }

    /** The pool; check {@link Hazard#active} before using one. */
    public Hazard[] hazards() {
        return hazards;
    }

    /** Lights a patch of the slug's trail on the floor at (x, y, z). */
    public Hazard lay(Combatant owner, SlugDefinition slug, double x, double y, double z) {
        Hazard slot = hazards[0];
        for (Hazard h : hazards) {
            if (!h.active) {
                slot = h;
                break;
            }
            if (h.ticksLeft < slot.ticksLeft) {
                slot = h;
            }
        }
        slot.active = true;
        slot.id = nextId++;
        slot.owner = owner;
        slot.slug = slug;
        slot.x = x;
        slot.y = y;
        slot.z = z;
        slot.ticksLeft = slug.trail().durationTicks();
        return slot;
    }

    /** Puts every patch out (a new round starts on a clean arena). */
    public void clear() {
        for (Hazard h : hazards) {
            h.active = false;
            h.owner = null;
            h.slug = null;
        }
    }

    /** Burns the patches down and lets them hurt whoever stands in them. */
    public void step() {
        for (Hazard h : hazards) {
            if (h.active && --h.ticksLeft <= 0) {
                h.active = false;
                h.owner = null;
                h.slug = null;
            }
        }
        int tick = arena.tick();
        for (Combatant c : arena.combatants()) {
            if (!c.alive || tick < c.hazardReadyTick) {
                continue;
            }
            Hazard burning = touching(c);
            if (burning == null) {
                continue;
            }
            TrailSpec trail = burning.slug.trail();
            c.hazardReadyTick = tick + trail.periodTicks();
            point.set(c.pos.x, burning.y, c.pos.z);
            context.set(arena, burning.owner, c, burning.slug, burning.slug.targeting(), point, null);
            trail.effects().apply(context);
        }
    }

    /** True when a patch of fire burns within {@code reach} of (x, z), on about the same floor as {@code y}. */
    public boolean burningNear(double x, double y, double z, double reach) {
        for (Hazard h : hazards) {
            if (!h.active || Math.abs(h.y - y) > 1.0) {
                continue;
            }
            double dx = h.x - x;
            double dz = h.z - z;
            double r = reach + h.slug.trail().radius();
            if (dx * dx + dz * dz <= r * r) {
                return true;
            }
        }
        return false;
    }

    /** A patch that can hurt {@code c} and that {@code c} stands in, or null. */
    public Hazard touching(Combatant c) {
        for (Hazard h : hazards) {
            if (!h.active || h.owner == null || h.owner == c || !h.slug.targeting().accepts(h.owner, c)) {
                continue;
            }
            TrailSpec trail = h.slug.trail();
            double reach = trail.radius() + c.hitRadius() * 0.5;
            double dx = c.pos.x - h.x;
            double dz = c.pos.z - h.z;
            if (dx * dx + dz * dz <= reach * reach && c.pos.y >= h.y - BELOW && c.pos.y <= h.y + trail.height()) {
                return h;
            }
        }
        return null;
    }
}
