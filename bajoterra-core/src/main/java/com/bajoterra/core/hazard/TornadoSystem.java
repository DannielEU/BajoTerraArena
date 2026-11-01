package com.bajoterra.core.hazard;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TornadoSpec;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.World;

/**
 * The tornadoes slugs such as Tormato turn into ({@link TornadoSpec}): a fixed pool of whirlwinds. Every tick a
 * tornado drifts on (until a solid stops it), drags the enemies of its owner towards its eye and spins them round it,
 * lifts the ones in the eye and hurts them every period, and launches its owner and their allies upwards once when
 * they step into the eye. It also throws enemy slugs off course ({@link #catching}). One that sweeps over a wall of
 * fire becomes a fire tornado and burns whoever it catches as well; one that swallows an ice slug ({@link #freeze})
 * becomes a freezing tornado that also deals ice damage and its {@link TornadoSpec#frostEffects()} (a
 * freeze) to whoever spins in its eye. Fire and ice put each other out: the last one
 * wins. Nothing here allocates during play.
 */
public final class TornadoSystem {

    /** Tornadoes spinning at once at most; a new one replaces the one closest to dying down. */
    public static final int CAPACITY = 8;
    /** Fastest the wind drags a body sideways, in m/s (a running hero can just about fight it). */
    private static final double MAX_DRAG = 8.0;
    /** How far below its foot a pair of feet still counts as inside it. */
    private static final double BELOW = 0.6;
    /** Extra fire damage a fire tornado deals every period to whoever spins in its eye. */
    private static final double FIRE_DAMAGE = 4.0;
    /** Extra ice damage a freezing tornado deals every period to whoever spins in its eye. */
    private static final double FROST_DAMAGE = 3.0;

    private final CombatArena arena;
    private final World world;
    private final HazardSystem fire;
    private final Tornado[] tornadoes = new Tornado[CAPACITY];
    private final EffectContext context = new EffectContext();
    private final Vec3 point = new Vec3();
    private int nextId = 1;

    /** With {@code fire} (may be null), tornadoes that sweep over a wall of fire catch fire. */
    public TornadoSystem(CombatArena arena, World world, HazardSystem fire) {
        this.arena = arena;
        this.world = world;
        this.fire = fire;
        for (int i = 0; i < CAPACITY; i++) {
            tornadoes[i] = new Tornado();
        }
    }

    /** The pool; check {@link Tornado#active} before using one. */
    public Tornado[] tornadoes() {
        return tornadoes;
    }

    /** Starts a tornado on the surface under (x, y, z), drifting along (dirX, dirZ) (a unit vector or zero). */
    public Tornado spawn(Combatant owner, SlugDefinition slug, TornadoSpec spec, double x, double y, double z,
            double dirX, double dirZ) {
        Tornado slot = tornadoes[0];
        for (Tornado t : tornadoes) {
            if (!t.active) {
                slot = t;
                break;
            }
            if (t.ticksLeft < slot.ticksLeft) {
                slot = t;
            }
        }
        GameMap map = world.map();
        double cx = clamp(x, map.minX() + 0.5, map.maxX() - 0.5);
        double cz = clamp(z, map.minZ() + 0.5, map.maxZ() - 0.5);
        slot.active = true;
        slot.id = nextId++;
        slot.owner = owner;
        slot.slug = slug;
        slot.spec = spec;
        slot.x = cx;
        slot.y = world.groundHeightAt(cx, cz, y + 0.05, 0.3);
        slot.z = cz;
        slot.dirX = dirX;
        slot.dirZ = dirZ;
        slot.ticksLeft = spec.durationTicks();
        slot.flaming = false;
        slot.freezing = false;
        slot.launchedMask = 0;
        GameEvent event = arena.events().begin(EventType.TORNADO, arena.tick());
        event.actor = owner == null ? -1 : owner.id;
        event.a = slug == null ? -1 : slug.index();
        event.b = slot.id;
        event.x = slot.x;
        event.y = slot.y;
        event.z = slot.z;
        event.value = spec.radius();
        return slot;
    }

    /** Calms every tornado (a new round starts on a clean arena). */
    public void clear() {
        for (Tornado t : tornadoes) {
            t.active = false;
            t.owner = null;
            t.slug = null;
        }
    }

    /** Moves the tornadoes on and lets them act on whoever is inside. */
    public void step() {
        for (Tornado t : tornadoes) {
            if (!t.active) {
                continue;
            }
            if (--t.ticksLeft <= 0) {
                t.active = false;
                t.owner = null;
                t.slug = null;
                continue;
            }
            drift(t);
            catchFire(t);
            for (Combatant c : arena.combatants()) {
                if (c.alive) {
                    blow(t, c);
                }
            }
        }
    }

    /**
     * The tornado whose wind would throw a slug at (x, y, z) fired by {@code shooter} off course: one of another
     * team that the point is inside of. Null when there is none.
     */
    public Tornado catching(double x, double y, double z, Combatant shooter) {
        for (Tornado t : tornadoes) {
            if (!t.active || t.owner == null || shooter == null || t.owner.team == shooter.team) {
                continue;
            }
            double dx = x - t.x;
            double dz = z - t.z;
            double r = t.spec.radius() * 0.8;
            if (dx * dx + dz * dz <= r * r && y >= t.y - BELOW && y <= t.y + t.spec.height()) {
                return t;
            }
        }
        return null;
    }

    /**
     * The tornado whose column (x, y, z) is inside of, whatever its team: the one an ice slug flying there would
     * turn into a freezing tornado. Null when there is none.
     */
    public Tornado around(double x, double y, double z) {
        for (Tornado t : tornadoes) {
            if (!t.active) {
                continue;
            }
            double dx = x - t.x;
            double dz = z - t.z;
            double r = t.spec.radius() * 0.8;
            if (dx * dx + dz * dz <= r * r && y >= t.y - BELOW && y <= t.y + t.spec.height()) {
                return t;
            }
        }
        return null;
    }

    /**
     * An ice slug flew into {@code t}: it becomes a freezing tornado (and stops burning, if it was). Returns false
     * when it already was one.
     */
    public boolean freeze(Tornado t, Combatant shooter) {
        if (t.freezing) {
            return false;
        }
        t.freezing = true;
        t.flaming = false;
        GameEvent event = arena.events().begin(EventType.TORNADO, arena.tick());
        event.actor = t.owner == null ? -1 : t.owner.id;
        event.subject = shooter == null ? -1 : shooter.id;
        event.a = t.slug == null ? -1 : t.slug.index();
        event.b = t.id;
        event.x = t.x;
        event.y = t.y;
        event.z = t.z;
        event.value = -2;
        return true;
    }

    /** The active tornado of another team than {@code c}'s whose wind reaches (x, z) on {@code c}'s floor, or null. */
    public Tornado enemyAt(Combatant c, double x, double z, double margin) {
        for (Tornado t : tornadoes) {
            if (!t.active || t.owner == null || t.owner.team == c.team) {
                continue;
            }
            double dx = x - t.x;
            double dz = z - t.z;
            double r = t.spec.radius() + margin;
            if (dx * dx + dz * dz <= r * r && Math.abs(c.pos.y - t.y) < t.spec.height()) {
                return t;
            }
        }
        return null;
    }

    private void drift(Tornado t) {
        double speed = t.spec.drift() * SimulationConstants.TICK_SECONDS;
        if (speed <= 0 || t.dirX == 0 && t.dirZ == 0) {
            return;
        }
        double nx = t.x + t.dirX * speed;
        double nz = t.z + t.dirZ * speed;
        GameMap map = world.map();
        boolean inside = nx > map.minX() + 0.5 && nx < map.maxX() - 0.5 && nz > map.minZ() + 0.5
                && nz < map.maxZ() - 0.5;
        double probe = 0.6;
        if (!inside || !world.hasClearShot(t.x, t.y + 0.6, t.z, nx + t.dirX * probe, t.y + 0.6,
                nz + t.dirZ * probe, 0.3)) {
            // Walls stop it: it stays where it is and spins out there.
            t.dirX = 0;
            t.dirZ = 0;
            return;
        }
        t.x = nx;
        t.z = nz;
        t.y = world.groundHeightAt(nx, nz, t.y + 0.05, 0.3);
    }

    private void catchFire(Tornado t) {
        if (t.flaming || fire == null || !fire.burningNear(t.x, t.y, t.z, t.spec.eyeRadius())) {
            return;
        }
        t.flaming = true;
        t.freezing = false;
        GameEvent event = arena.events().begin(EventType.TORNADO, arena.tick());
        event.actor = t.owner == null ? -1 : t.owner.id;
        event.a = t.slug == null ? -1 : t.slug.index();
        event.b = t.id;
        event.x = t.x;
        event.y = t.y;
        event.z = t.z;
        event.value = -1;
    }

    private void blow(Tornado t, Combatant c) {
        TornadoSpec spec = t.spec;
        double dx = t.x - c.pos.x;
        double dz = t.z - c.pos.z;
        double flatSq = dx * dx + dz * dz;
        double reach = spec.radius() + c.hitRadius() * 0.5;
        if (flatSq > reach * reach || c.pos.y < t.y - BELOW || c.pos.y > t.y + spec.height()) {
            return;
        }
        double flat = Math.sqrt(flatSq);
        Combatant owner = t.owner;
        boolean friend = owner != null && (c == owner || c.team == owner.team);
        if (friend) {
            launch(t, c, flat);
            return;
        }
        if (owner == null || !t.slug.targeting().accepts(owner, c)) {
            return;
        }
        double dt = SimulationConstants.TICK_SECONDS;
        if (flat > 1e-3) {
            // Stronger near the eye; part of the pull goes round it, so a caught body spins in.
            double strength = spec.pull() * dt * (0.5 + 0.5 * (1 - flat / reach));
            double inX = dx / flat;
            double inZ = dz / flat;
            c.knock.x += (inX - inZ * 0.7) * strength;
            c.knock.z += (inZ + inX * 0.7) * strength;
            double drag = Math.sqrt(c.knock.x * c.knock.x + c.knock.z * c.knock.z);
            if (drag > MAX_DRAG) {
                c.knock.x *= MAX_DRAG / drag;
                c.knock.z *= MAX_DRAG / drag;
            }
        }
        if (flat > spec.eyeRadius() + c.hitRadius()) {
            return;
        }
        if (spec.lift() > 0 && c.pos.y < t.y + spec.height() - c.hitHeight() * 0.5) {
            c.vel.y = Math.max(c.vel.y, spec.lift());
            c.grounded = false;
        }
        int tick = arena.tick();
        if (tick < c.windReadyTick) {
            return;
        }
        c.windReadyTick = tick + spec.periodTicks();
        point.set(c.pos.x, c.centerY(), c.pos.z);
        context.set(arena, owner, c, t.slug, t.slug.targeting(), point, null);
        spec.effects().apply(context);
        if (t.flaming && c.alive) {
            arena.damage(owner, c, FIRE_DAMAGE, Element.FIRE, 0);
        }
        if (t.freezing && c.alive) {
            arena.damage(owner, c, FROST_DAMAGE, Element.ICE, 0);
            spec.frostEffects().apply(context);
        }
    }

    /** A tornado jump: the owner's team is thrown upwards once when they step into the eye standing. */
    private void launch(Tornado t, Combatant c, double flat) {
        int bit = 1 << c.id;
        if ((t.launchedMask & bit) != 0 || !c.grounded || t.spec.allyLift() <= 0
                || flat > t.spec.eyeRadius() + c.hitRadius()) {
            return;
        }
        t.launchedMask |= bit;
        arena.knockback(c, 0, 0, 0, t.spec.allyLift());
    }

    private static double clamp(double v, double lo, double hi) {
        return Math.min(Math.max(v, lo), hi);
    }
}
