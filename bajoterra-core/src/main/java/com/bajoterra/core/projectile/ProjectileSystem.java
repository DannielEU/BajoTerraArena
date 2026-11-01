package com.bajoterra.core.projectile;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.combat.CriticalHits;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.hazard.HazardSystem;
import com.bajoterra.core.hazard.Tornado;
import com.bajoterra.core.hazard.TornadoSystem;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TrailSpec;
import com.bajoterra.core.world.Barrier;
import com.bajoterra.core.world.World;

/**
 * Moves projectiles and resolves their impacts. Each tick a projectile travels along a swept sphere, so even the
 * fastest slugs cannot tunnel through thin walls or combatants; the earliest impact along the path wins, which
 * means an obstacle in front of a combatant shields that combatant.
 *
 * <p>Sticky slugs (grenades) stop at their first impact instead: they cling to the obstacle or to the combatant
 * they touched, follow that combatant around, and run their expiry effects when the fuse burns out. A direct hit on
 * the top of an enemy's hitbox is a headshot ({@link CriticalHits}). A sticky slug with a proximity fuse
 * (Phosphoro) also stops in mid-air, hovering, as soon as it comes that close to a combatant it can affect, facing
 * them.
 *
 * <p>A tornado of another team throws a slug that flies into it off course, once, sideways and up; an ice slug that
 * flies into any tornado is swallowed instead and turns it into a freezing tornado. A fusion shot
 * carries a second slug ({@link Projectile#partner}): wherever the first one takes effect, the second one's effects go
 * off there too.
 */
public final class ProjectileSystem {

    private static final int MAX_IMPACTS_PER_TICK = 4;

    private final CombatArena arena;
    private final World world;
    private final ProjectilePool pool;
    private final HazardSystem hazards;
    private TornadoSystem tornadoes;
    private final EffectContext context = new EffectContext();
    private final Vec3 impactPoint = new Vec3();
    private final Vec3 travelDirection = new Vec3();

    public ProjectileSystem(CombatArena arena, World world, ProjectilePool pool) {
        this(arena, world, pool, null);
    }

    /** With {@code hazards}, slugs that leave a trail ({@link TrailSpec}) light it as they fly. */
    public ProjectileSystem(CombatArena arena, World world, ProjectilePool pool, HazardSystem hazards) {
        this.arena = arena;
        this.world = world;
        this.pool = pool;
        this.hazards = hazards;
    }

    /** Lets the tornadoes of the arena throw slugs off course. */
    public void setTornadoes(TornadoSystem newTornadoes) {
        this.tornadoes = newTornadoes;
    }

    public ProjectilePool pool() {
        return pool;
    }

    /**
     * Launches a slug.
     *
     * @param direction unit direction of the launch
     * @return the projectile, or null when the pool is exhausted
     */
    public Projectile spawn(Combatant owner, SlugDefinition slug, Vec3 origin, Vec3 direction) {
        return spawn(owner, slug, null, origin, direction);
    }

    /**
     * Launches a slug, fused with {@code partner} (may be null) and as big as its slinger's
     * {@link com.bajoterra.core.stats.StatBlock#projectileScale} makes it.
     *
     * @param direction unit direction of the launch
     * @return the projectile, or null when the pool is exhausted
     */
    public Projectile spawn(Combatant owner, SlugDefinition slug, SlugDefinition partner, Vec3 origin,
            Vec3 direction) {
        Projectile p = pool.acquire();
        if (p == null) {
            return null;
        }
        ProjectileSpec spec = slug.projectile();
        p.owner = owner;
        p.slug = slug;
        p.pos.set(origin);
        p.vel.set(direction).scale(spec.speed());
        p.ticksLeft = spec.lifetimeTicks();
        p.pierceLeft = spec.pierce();
        p.trailGap = -slug.trail().startAfter();
        p.trailLaid = 0;
        p.radius = spec.radius() * (owner == null ? 1.0 : owner.stats.projectileScale);
        p.partner = partner;
        p.deflectedBy = 0;
        return p;
    }

    /** Advances every active projectile by one tick. */
    public void step() {
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (p.active) {
                step(p);
            }
        }
    }

    private void step(Projectile p) {
        if (p.stuck) {
            burnFuse(p);
            return;
        }
        SlugDefinition slug = p.slug;
        ProjectileSpec spec = slug.projectile();
        if (spec.proximity() > 0 && stopNearTarget(p, spec.proximity())) {
            return;
        }
        if (tornadoes != null && carriesIce(p)) {
            // An ice slug that flies into a tornado (anybody's) is swallowed and turns it into a freezing tornado.
            Tornado swirl = tornadoes.around(p.pos.x, p.pos.y, p.pos.z);
            if (swirl != null && tornadoes.freeze(swirl, p.owner)) {
                pool.release(p);
                return;
            }
        }
        if (tornadoes != null && p.deflectedBy == 0) {
            Tornado wind = tornadoes.catching(p.pos.x, p.pos.y, p.pos.z, p.owner);
            if (wind != null) {
                deflect(p, wind);
            }
        }
        double dt = SimulationConstants.TICK_SECONDS;
        p.vel.y -= spec.gravity() * dt;
        travelDirection.set(p.vel).normalize();
        applyTickEffect(p);

        double dx = p.vel.x * dt;
        double dy = p.vel.y * dt;
        double dz = p.vel.z * dt;
        double remaining = 1.0;
        for (int impact = 0; impact < MAX_IMPACTS_PER_TICK && remaining > 1e-6; impact++) {
            double sx = dx * remaining;
            double sy = dy * remaining;
            double sz = dz * remaining;
            double bestTime = slug.pierceWorld() ? -1
                    : world.sweepProjectile(p.pos.x, p.pos.y, p.pos.z, sx, sy, sz, p.radius);
            Combatant hit = null;
            for (Combatant candidate : arena.combatants()) {
                if (!candidate.alive || candidate == p.owner || (p.hitMask & (1 << candidate.id)) != 0
                        || !slug.targeting().accepts(p.owner, candidate)) {
                    continue;
                }
                double t = sweepAgainst(candidate, p.pos, sx, sy, sz, p.radius);
                if (t >= 0 && (bestTime < 0 || t < bestTime)) {
                    bestTime = t;
                    hit = candidate;
                }
            }
            if (bestTime < 0) {
                p.pos.add(sx, sy, sz);
                layTrail(p, Math.sqrt(sx * sx + sz * sz));
                remaining = 0;
                break;
            }
            Barrier barrier = hit == null ? world.lastBarrierHit() : null;
            p.pos.add(sx * bestTime, sy * bestTime, sz * bestTime);
            layTrail(p, Math.sqrt(sx * sx + sz * sz) * bestTime);
            remaining *= 1.0 - bestTime;
            impactPoint.set(p.pos);
            if (hit != null) {
                p.hitMask |= 1 << hit.id;
                hitCombatant(p, hit);
                if (spec.sticky()) {
                    stick(p, hit);
                    return;
                }
                if (p.pierceLeft-- <= 0) {
                    pool.release(p);
                    return;
                }
            } else {
                if (barrier != null) {
                    strike(p, barrier);
                }
                hitWorld(p);
                if (spec.sticky()) {
                    stick(p, null);
                    return;
                }
                pool.release(p);
                return;
            }
        }
        if (--p.ticksLeft <= 0) {
            // A blast has no travel direction: someone standing right on it is thrown straight up.
            travelDirection.set(0, 1, 0);
            expire(p);
        }
    }

    private static boolean carriesIce(Projectile p) {
        return p.slug.element() == Element.ICE || p.partner != null && p.partner.element() == Element.ICE;
    }

    private void hitCombatant(Projectile p, Combatant victim) {
        p.owner.shotsHit++;
        boolean critical = victim.team != p.owner.team && CriticalHits.isHeadshot(victim, impactPoint.y);
        GameEvent event = arena.events().begin(EventType.PROJECTILE_HIT, arena.tick());
        event.actor = p.owner.id;
        event.subject = victim.id;
        event.a = p.slug.index();
        event.b = critical ? 1 : 0;
        event.x = impactPoint.x;
        event.y = impactPoint.y;
        event.z = impactPoint.z;
        context.set(arena, p.owner, victim, p.slug, p.slug.targeting(), impactPoint, travelDirection);
        if (critical) {
            context.setCriticalTarget(victim);
        }
        p.slug.onHit().apply(context);
        SlugDefinition partner = p.partner;
        if (partner != null) {
            // Only once: a piercing first slug carries its partner to the first body only.
            p.partner = null;
            boolean direct = partner.onHit() != Effect.NONE && partner.targeting().accepts(p.owner, victim);
            context.set(arena, p.owner, direct ? victim : null, partner, partner.targeting(), impactPoint,
                    travelDirection);
            (direct ? partner.onHit() : partner.onExpire()).apply(context);
        }
    }

    /**
     * A tornado's wind catches the slug: it is flung sideways round the tornado and up, losing most of its speed, and
     * misses whatever it was flying at.
     */
    private void deflect(Projectile p, Tornado wind) {
        p.deflectedBy = wind.id;
        double speed = p.vel.length();
        double rx = p.pos.x - wind.x;
        double rz = p.pos.z - wind.z;
        double flat = Math.sqrt(rx * rx + rz * rz);
        if (flat < 1e-3) {
            rx = p.vel.x;
            rz = p.vel.z;
            flat = Math.max(1e-3, Math.sqrt(rx * rx + rz * rz));
        }
        // Round the eye the way the wind spins (see TornadoSystem), and outwards a little.
        double tx = (-rz + rx * 0.5) / flat;
        double tz = (rx + rz * 0.5) / flat;
        double along = speed * 0.45;
        p.vel.set(tx * along, 7 + speed * 0.1, tz * along);
        GameEvent event = arena.events().begin(EventType.DEFLECTED, arena.tick());
        event.actor = wind.owner == null ? -1 : wind.owner.id;
        event.subject = p.owner.id;
        event.a = p.slug.index();
        event.b = wind.id;
        event.x = p.pos.x;
        event.y = p.pos.y;
        event.z = p.pos.z;
    }

    /**
     * A proximity fuse: once a combatant the slug can affect is within {@code reach} of it (centre to the middle of
     * their body), the slug stops where it is, turns to face them and lights its fuse.
     */
    private boolean stopNearTarget(Projectile p, double reach) {
        for (Combatant candidate : arena.combatants()) {
            if (!candidate.alive || candidate == p.owner || !p.slug.targeting().accepts(p.owner, candidate)) {
                continue;
            }
            double dx = candidate.pos.x - p.pos.x;
            double dy = candidate.centerY() - p.pos.y;
            double dz = candidate.pos.z - p.pos.z;
            if (dx * dx + dy * dy + dz * dz <= reach * reach) {
                stick(p, null);
                double flat = Math.sqrt(dx * dx + dz * dz);
                if (flat > 1e-6) {
                    p.stuckDirX = dx / flat;
                    p.stuckDirZ = dz / flat;
                }
                return true;
            }
        }
        return false;
    }

    /** Stops a sticky slug where it touched and lights its fuse; it clings to {@code victim} when there is one. */
    private void stick(Projectile p, Combatant victim) {
        double flat = Math.sqrt(p.vel.x * p.vel.x + p.vel.z * p.vel.z);
        p.stuckDirX = flat > 1e-6 ? p.vel.x / flat : 0;
        p.stuckDirZ = flat > 1e-6 ? p.vel.z / flat : 0;
        p.stuck = true;
        p.vel.setZero();
        p.ticksLeft = p.slug.projectile().fuseTicks();
        p.stuckTo = victim;
        if (victim != null) {
            p.stuckOffset.set(p.pos).sub(victim.pos);
        }
    }

    /** A stuck slug rides along with the combatant it clings to and detonates when the fuse runs out. */
    private void burnFuse(Projectile p) {
        Combatant carrier = p.stuckTo;
        if (carrier != null) {
            if (carrier.alive) {
                // Crouching lowers the head, and a grenade on the head comes down with it.
                p.pos.set(carrier.pos.x + p.stuckOffset.x,
                        carrier.pos.y + Math.min(p.stuckOffset.y, carrier.hitHeight()),
                        carrier.pos.z + p.stuckOffset.z);
            } else {
                p.stuckTo = null;
            }
        }
        if (--p.ticksLeft <= 0) {
            // The blast (or the flash) faces the way the slug was facing when it stopped.
            travelDirection.set(p.stuckDirX, 0, p.stuckDirZ);
            expire(p);
        }
    }

    /** A slug struck a barrier (an ice wall): it takes one hit, and the last one breaks it. */
    private void strike(Projectile p, Barrier barrier) {
        barrier.hitsLeft--;
        GameEvent event = arena.events().begin(EventType.BARRIER_HIT, arena.tick());
        event.actor = p.owner.id;
        event.a = Math.max(0, barrier.hitsLeft);
        event.b = barrier.id;
        event.x = impactPoint.x;
        event.y = impactPoint.y;
        event.z = impactPoint.z;
        if (barrier.hitsLeft <= 0) {
            barrier.active = false;
            GameEvent gone = arena.events().begin(EventType.BARRIER_GONE, arena.tick());
            gone.actor = p.owner.id;
            gone.a = 1;
            gone.b = barrier.id;
            gone.x = (barrier.minX + barrier.maxX) * 0.5;
            gone.y = barrier.minY;
            gone.z = (barrier.minZ + barrier.maxZ) * 0.5;
        }
    }

    /**
     * A slug with a trail lights a patch of fire on the floor under it every {@code spacing} metres it flies, as long
     * as it flies low enough and has trail left.
     */
    private void layTrail(Projectile p, double flown) {
        TrailSpec trail = p.slug.trail();
        if (hazards == null || !trail.exists() || p.trailLaid >= trail.maxLength()) {
            return;
        }
        p.trailGap += flown;
        while (p.trailGap >= 0 && p.trailLaid < trail.maxLength()) {
            p.trailGap -= trail.spacing();
            p.trailLaid += trail.spacing();
            double below = world.projectileRayDistance(p.pos.x, p.pos.y, p.pos.z, 0, -1, 0, trail.maxHeight());
            if (below < trail.maxHeight()) {
                hazards.lay(p.owner, p.slug, p.pos.x, p.pos.y - below, p.pos.z);
            }
        }
    }

    private void hitWorld(Projectile p) {
        GameEvent event = arena.events().begin(EventType.PROJECTILE_WORLD_HIT, arena.tick());
        event.actor = p.owner.id;
        event.a = p.slug.index();
        event.x = impactPoint.x;
        event.y = impactPoint.y;
        event.z = impactPoint.z;
        context.set(arena, p.owner, null, p.slug, p.slug.targeting(), impactPoint, travelDirection);
        p.slug.onWorldHit().apply(context);
        SlugDefinition partner = p.partner;
        if (partner != null) {
            p.partner = null;
            context.set(arena, p.owner, null, partner, partner.targeting(), impactPoint, travelDirection);
            (partner.onWorldHit() != Effect.NONE ? partner.onWorldHit() : partner.onExpire()).apply(context);
        }
    }

    private void applyTickEffect(Projectile p) {
        Effect onTick = p.slug.onTick();
        if (onTick != Effect.NONE) {
            context.set(arena, p.owner, null, p.slug, p.slug.targeting(), p.pos, travelDirection);
            onTick.apply(context);
        }
    }

    private void expire(Projectile p) {
        GameEvent event = arena.events().begin(EventType.PROJECTILE_EXPIRED, arena.tick());
        event.actor = p.owner.id;
        event.a = p.slug.index();
        event.x = p.pos.x;
        event.y = p.pos.y;
        event.z = p.pos.z;
        context.set(arena, p.owner, null, p.slug, p.slug.targeting(), p.pos, travelDirection);
        p.slug.onExpire().apply(context);
        SlugDefinition partner = p.partner;
        if (partner != null) {
            p.partner = null;
            // A fused grenade that went off on somebody hits them with its partner too.
            Combatant carrier = p.stuckTo != null && p.stuckTo.alive ? p.stuckTo : null;
            boolean direct = carrier != null && partner.onHit() != Effect.NONE
                    && partner.targeting().accepts(p.owner, carrier);
            context.set(arena, p.owner, direct ? carrier : null, partner, partner.targeting(), p.pos,
                    travelDirection);
            (direct ? partner.onHit() : partner.onExpire()).apply(context);
        }
        pool.release(p);
    }

    /**
     * Sweeps a sphere along a displacement against a combatant's vertical capsule (approximated as a cylinder grown
     * by the sphere radius).
     *
     * @return fraction in [0, 1] of the displacement at first contact, or -1 for a miss
     */
    static double sweepAgainst(Combatant c, Vec3 origin, double sx, double sy, double sz, double radius) {
        double grown = c.hitRadius() + radius;
        double yLow = c.pos.y - radius;
        double yHigh = c.pos.y + c.hitHeight() + radius;
        double ax = origin.x - c.pos.x;
        double az = origin.z - c.pos.z;
        double a = sx * sx + sz * sz;
        double c0 = ax * ax + az * az - grown * grown;
        double tEnter;
        double tExit;
        if (a < 1e-12) {
            if (c0 > 0) {
                return -1;
            }
            tEnter = 0;
            tExit = 1;
        } else {
            double b = 2 * (ax * sx + az * sz);
            double discriminant = b * b - 4 * a * c0;
            if (discriminant < 0) {
                return -1;
            }
            double root = Math.sqrt(discriminant);
            tEnter = (-b - root) / (2 * a);
            tExit = (-b + root) / (2 * a);
            if (tExit < 0 || tEnter > 1) {
                return -1;
            }
            tEnter = Math.max(tEnter, 0);
            tExit = Math.min(tExit, 1);
        }
        if (Math.abs(sy) < 1e-12) {
            if (origin.y < yLow || origin.y > yHigh) {
                return -1;
            }
        } else {
            double t1 = (yLow - origin.y) / sy;
            double t2 = (yHigh - origin.y) / sy;
            tEnter = Math.max(tEnter, Math.min(t1, t2));
            tExit = Math.min(tExit, Math.max(t1, t2));
            if (tEnter > tExit) {
                return -1;
            }
        }
        return tEnter;
    }
}
