package com.bajoterra.core.projectile;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.World;

/**
 * Moves projectiles and resolves their impacts. Each tick a projectile travels along a swept sphere, so even the
 * fastest slugs cannot tunnel through thin walls or combatants; the earliest impact along the path wins, which
 * means an obstacle in front of a combatant shields that combatant.
 */
public final class ProjectileSystem {

    private static final int MAX_IMPACTS_PER_TICK = 4;

    private final CombatArena arena;
    private final World world;
    private final ProjectilePool pool;
    private final EffectContext context = new EffectContext();
    private final Vec3 impactPoint = new Vec3();
    private final Vec3 travelDirection = new Vec3();

    public ProjectileSystem(CombatArena arena, World world, ProjectilePool pool) {
        this.arena = arena;
        this.world = world;
        this.pool = pool;
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
        SlugDefinition slug = p.slug;
        ProjectileSpec spec = slug.projectile();
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
                    : world.sweepProjectile(p.pos.x, p.pos.y, p.pos.z, sx, sy, sz, spec.radius());
            Combatant hit = null;
            for (Combatant candidate : arena.combatants()) {
                if (!candidate.alive || candidate == p.owner || (p.hitMask & (1 << candidate.id)) != 0
                        || !slug.targeting().accepts(p.owner, candidate)) {
                    continue;
                }
                double t = sweepAgainst(candidate, p.pos, sx, sy, sz, spec.radius());
                if (t >= 0 && (bestTime < 0 || t < bestTime)) {
                    bestTime = t;
                    hit = candidate;
                }
            }
            if (bestTime < 0) {
                p.pos.add(sx, sy, sz);
                remaining = 0;
                break;
            }
            p.pos.add(sx * bestTime, sy * bestTime, sz * bestTime);
            remaining *= 1.0 - bestTime;
            impactPoint.set(p.pos);
            if (hit != null) {
                p.hitMask |= 1 << hit.id;
                hitCombatant(p, hit);
                if (p.pierceLeft-- <= 0) {
                    pool.release(p);
                    return;
                }
            } else {
                hitWorld(p);
                pool.release(p);
                return;
            }
        }
        if (--p.ticksLeft <= 0) {
            expire(p);
        }
    }

    private void hitCombatant(Projectile p, Combatant victim) {
        p.owner.shotsHit++;
        GameEvent event = arena.events().begin(EventType.PROJECTILE_HIT, arena.tick());
        event.actor = p.owner.id;
        event.subject = victim.id;
        event.a = p.slug.index();
        event.x = impactPoint.x;
        event.y = impactPoint.y;
        event.z = impactPoint.z;
        context.set(arena, p.owner, victim, p.slug, p.slug.targeting(), impactPoint, travelDirection);
        p.slug.onHit().apply(context);
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
