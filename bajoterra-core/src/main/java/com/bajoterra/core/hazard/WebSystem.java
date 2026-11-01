package com.bajoterra.core.hazard;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.WebSpec;
import com.bajoterra.core.world.World;

/**
 * The web traps slugs such as Arachnet spin on the floor ({@link WebSpec}): a fixed pool. A web waits until somebody
 * its slug could hit steps on it, catches them (its effects, an {@code ensnare}) and is spent; it fades when its time
 * is up, and a wall of fire burns it away. Each slinger keeps at most {@link #PER_OWNER} webs: a new one replaces
 * their oldest. Nothing here allocates during play.
 */
public final class WebSystem {

    /** Webs on the floor at once at most. */
    public static final int CAPACITY = 12;
    /** Webs a single slinger keeps at once. */
    public static final int PER_OWNER = 3;
    /** How far above or below a web's floor a pair of feet still steps on it. */
    private static final double STEP = 0.45;

    private final CombatArena arena;
    private final World world;
    private final HazardSystem fire;
    private final Web[] webs = new Web[CAPACITY];
    private final EffectContext context = new EffectContext();
    private final Vec3 point = new Vec3();
    private int nextId = 1;

    /** With {@code fire} (may be null), webs under a wall of fire burn away. */
    public WebSystem(CombatArena arena, World world, HazardSystem fire) {
        this.arena = arena;
        this.world = world;
        this.fire = fire;
        for (int i = 0; i < CAPACITY; i++) {
            webs[i] = new Web();
        }
    }

    /** The pool; check {@link Web#active} before using one. */
    public Web[] webs() {
        return webs;
    }

    /** Spins a web on the surface under (x, y, z). */
    public Web spin(Combatant owner, SlugDefinition slug, WebSpec spec, double x, double y, double z) {
        Web slot = null;
        Web oldestOwn = null;
        int own = 0;
        Web oldest = webs[0];
        for (Web w : webs) {
            if (!w.active) {
                if (slot == null) {
                    slot = w;
                }
                continue;
            }
            if (w.ticksLeft < oldest.ticksLeft) {
                oldest = w;
            }
            if (w.owner == owner) {
                own++;
                if (oldestOwn == null || w.ticksLeft < oldestOwn.ticksLeft) {
                    oldestOwn = w;
                }
            }
        }
        if (own >= PER_OWNER && oldestOwn != null) {
            gone(oldestOwn, 0, null);
            slot = oldestOwn;
        } else if (slot == null) {
            gone(oldest, 0, null);
            slot = oldest;
        }
        var map = world.map();
        double cx = Math.min(Math.max(x, map.minX() + spec.radius()), map.maxX() - spec.radius());
        double cz = Math.min(Math.max(z, map.minZ() + spec.radius()), map.maxZ() - spec.radius());
        slot.active = true;
        slot.id = nextId++;
        slot.owner = owner;
        slot.slug = slug;
        slot.spec = spec;
        slot.x = cx;
        slot.y = world.groundHeightAt(cx, cz, y + 0.05, 0.2);
        slot.z = cz;
        slot.ticksLeft = spec.durationTicks();
        GameEvent event = arena.events().begin(EventType.WEB_SPUN, arena.tick());
        event.actor = owner == null ? -1 : owner.id;
        event.a = slug == null ? -1 : slug.index();
        event.b = slot.id;
        event.x = slot.x;
        event.y = slot.y;
        event.z = slot.z;
        event.value = spec.radius();
        return slot;
    }

    /** Clears every web (a new round starts on a clean arena). */
    public void clear() {
        for (Web w : webs) {
            w.active = false;
            w.owner = null;
            w.slug = null;
        }
    }

    /** Ages the webs, burns the ones under fire and springs the ones somebody steps on. */
    public void step() {
        for (Web w : webs) {
            if (!w.active) {
                continue;
            }
            if (--w.ticksLeft <= 0) {
                gone(w, 0, null);
                continue;
            }
            if (fire != null && fire.burningNear(w.x, w.y, w.z, w.spec.radius() * 0.5)) {
                gone(w, 2, null);
                continue;
            }
            for (Combatant c : arena.combatants()) {
                if (catches(w, c, c.pos.x, c.pos.z, 0)) {
                    point.set(c.pos.x, w.y, c.pos.z);
                    context.set(arena, w.owner, c, w.slug, w.slug.targeting(), point, null);
                    w.spec.effects().apply(context);
                    gone(w, 1, c);
                    break;
                }
            }
        }
    }

    /** A web that would catch {@code c} if they stood at (x, z), {@code margin} metres early; null when none. */
    public Web trapAt(Combatant c, double x, double z, double margin) {
        for (Web w : webs) {
            if (w.active && catches(w, c, x, z, margin)) {
                return w;
            }
        }
        return null;
    }

    private static boolean catches(Web w, Combatant c, double x, double z, double margin) {
        if (!c.alive || w.owner == null || c == w.owner || !w.slug.targeting().accepts(w.owner, c)
                || Math.abs(c.pos.y - w.y) > STEP) {
            return false;
        }
        double reach = w.spec.radius() + c.hitRadius() * 0.5 + margin;
        double dx = x - w.x;
        double dz = z - w.z;
        return dx * dx + dz * dz <= reach * reach;
    }

    private void gone(Web w, int why, Combatant caught) {
        GameEvent event = arena.events().begin(EventType.WEB_GONE, arena.tick());
        event.actor = w.owner == null ? -1 : w.owner.id;
        event.subject = caught == null ? -1 : caught.id;
        event.a = why;
        event.b = w.id;
        event.x = w.x;
        event.y = w.y;
        event.z = w.z;
        w.active = false;
        w.owner = null;
        w.slug = null;
    }
}
