package com.bajoterra.core.world;

import com.bajoterra.core.entity.Combatant;

/**
 * Collision and visibility queries over a {@link GameMap} for one running simulation. The map and its grid are
 * immutable and shared; this class owns the only mutable scratch (the de-duplication stamps) and the few
 * {@link Barrier}s raised during a round (ice walls), which every query treats like the map's own solids. Every
 * simulation has its own {@code World} and queries never allocate.
 */
public final class World {

    /** Highest ledge a combatant walks onto without jumping. */
    public static final double STEP_HEIGHT = 0.35;

    /**
     * How far below its feet a combatant that was standing on the ground is pulled back down to it, so walking down
     * a ramp keeps the feet on the slope instead of floating off it.
     */
    public static final double SNAP_DOWN = 0.35;

    private static final int KIND_PROJECTILE = 0;
    private static final int KIND_SIGHT = 1;

    /** Most barriers standing at once; raising one more replaces the one closest to melting. */
    public static final int MAX_BARRIERS = 12;

    private final GameMap map;
    private final SpatialGrid grid;
    private final int[] stamps;
    private final Barrier[] barriers = new Barrier[MAX_BARRIERS];
    private int stamp;
    private int nextBarrierId = 1;
    private Barrier lastBarrierHit;

    public World(GameMap map) {
        this.map = map;
        this.grid = map.grid();
        this.stamps = new int[map.obstacleCount()];
        for (int i = 0; i < MAX_BARRIERS; i++) {
            barriers[i] = new Barrier();
        }
    }

    /** The barrier pool; check {@link Barrier#active} before using one. */
    public Barrier[] barriers() {
        return barriers;
    }

    /**
     * Raises a barrier over the given box, replacing the one closest to melting when every slot is taken.
     *
     * @return the barrier, already active
     */
    public Barrier raiseBarrier(double minX, double minY, double minZ, double maxX, double maxY, double maxZ,
            int hits, int ticks, int ownerId, int slugIndex) {
        Barrier slot = barriers[0];
        for (Barrier b : barriers) {
            if (!b.active) {
                slot = b;
                break;
            }
            if (b.ticksLeft < slot.ticksLeft) {
                slot = b;
            }
        }
        slot.active = true;
        slot.id = nextBarrierId++;
        slot.minX = minX;
        slot.minY = minY;
        slot.minZ = minZ;
        slot.maxX = maxX;
        slot.maxY = maxY;
        slot.maxZ = maxZ;
        slot.hitsLeft = hits;
        slot.ticksLeft = ticks;
        slot.lifeTicks = ticks;
        slot.ownerId = ownerId;
        slot.slugIndex = slugIndex;
        return slot;
    }

    /** Removes every barrier (a new round starts on a clean arena). */
    public void clearBarriers() {
        for (Barrier b : barriers) {
            b.active = false;
        }
    }

    /** The barrier that stopped the last {@link #sweepProjectile} call, or null when it was the map or nothing. */
    public Barrier lastBarrierHit() {
        return lastBarrierHit;
    }

    public GameMap map() {
        return map;
    }

    /**
     * Sweeps a sphere along a displacement and finds the first obstacle (or the ground) that stops projectiles.
     *
     * @return the fraction in [0, 1] of the displacement travelled before the hit, or -1 for no hit
     */
    public double sweepProjectile(double ox, double oy, double oz, double sx, double sy, double sz, double radius) {
        double best = sweepObstacles(ox, oy, oz, sx, sy, sz, radius, KIND_PROJECTILE);
        if (sy < -1e-12) {
            double t = (oy - radius - map.groundY()) / -sy;
            if (t < 0) {
                t = 0;
            }
            if (t <= 1 && (best < 0 || t < best)) {
                best = t;
                lastBarrierHit = null;
            }
        }
        return best;
    }

    /** Distance along a unit direction to the first projectile-blocking surface, capped at {@code maxDistance}. */
    public double projectileRayDistance(double ox, double oy, double oz, double dx, double dy, double dz,
            double maxDistance) {
        double t = sweepProjectile(ox, oy, oz, dx * maxDistance, dy * maxDistance, dz * maxDistance, 0);
        return t < 0 ? maxDistance : t * maxDistance;
    }

    /** True when no obstacle that blocks sight lies between the two points. */
    public boolean hasLineOfSight(double ax, double ay, double az, double bx, double by, double bz) {
        return sweepObstacles(ax, ay, az, bx - ax, by - ay, bz - az, 0, KIND_SIGHT) < 0;
    }

    /** True when a slug of the given radius can fly straight between the two points without hitting the map. */
    public boolean hasClearShot(double ax, double ay, double az, double bx, double by, double bz, double radius) {
        return sweepObstacles(ax, ay, az, bx - ax, by - ay, bz - az, radius, KIND_PROJECTILE) < 0;
    }

    /** Pushes a combatant out of solid obstacles, keeps it inside the map and settles it on the ground. */
    public void resolveCombatant(Combatant c) {
        double radius = c.hitRadius();
        double height = c.hitHeight();
        for (int pass = 0; pass < 2; pass++) {
            pushOutOfObstacles(c, radius, height);
        }
        c.pos.x = Math.min(Math.max(c.pos.x, map.minX() + radius), map.maxX() - radius);
        c.pos.z = Math.min(Math.max(c.pos.z, map.minZ() + radius), map.maxZ() - radius);
        double ground = groundHeightAt(c.pos.x, c.pos.z, c.pos.y, radius);
        if (c.pos.y <= ground) {
            c.pos.y = ground;
            if (c.vel.y < 0) {
                c.vel.y = 0;
            }
        } else if (c.grounded && c.vel.y <= 0.001 && c.pos.y - ground <= SNAP_DOWN) {
            // Was standing and the surface dropped a little (a ramp going down): follow it.
            c.pos.y = ground;
            c.vel.y = 0;
        }
        c.grounded = c.pos.y <= ground + 0.02 && c.vel.y <= 0.001;
    }

    /** Height of the surface a body standing at (x, z) with its feet at {@code feetY} would stand on. */
    public double groundHeightAt(double x, double z, double feetY, double radius) {
        double ground = map.groundY();
        stamp++;
        int c0 = grid.columnOf(x - radius);
        int c1 = grid.columnOf(x + radius);
        int r0 = grid.rowOf(z - radius);
        int r1 = grid.rowOf(z + radius);
        for (int row = r0; row <= r1; row++) {
            for (int col = c0; col <= c1; col++) {
                for (int index : grid.obstaclesIn(col, row)) {
                    if (stamps[index] == stamp) {
                        continue;
                    }
                    stamps[index] = stamp;
                    Obstacle o = map.obstacle(index);
                    if (o.blocksMovement() && o.containsXZ(x, z, radius * 0.5)) {
                        double top = o.topAt(x, z);
                        if (top <= feetY + STEP_HEIGHT && top > ground) {
                            ground = top;
                        }
                    }
                }
            }
        }
        return ground;
    }

    private void pushOutOfObstacles(Combatant c, double radius, double height) {
        stamp++;
        double feet = c.pos.y;
        double top = feet + height;
        for (Barrier b : barriers) {
            if (b.active && b.maxY - feet > STEP_HEIGHT && b.minY < top) {
                pushOut(c, b.minX, b.maxX, b.minZ, b.maxZ, radius);
            }
        }
        int c0 = grid.columnOf(c.pos.x - radius);
        int c1 = grid.columnOf(c.pos.x + radius);
        int r0 = grid.rowOf(c.pos.z - radius);
        int r1 = grid.rowOf(c.pos.z + radius);
        for (int row = r0; row <= r1; row++) {
            for (int col = c0; col <= c1; col++) {
                for (int index : grid.obstaclesIn(col, row)) {
                    if (stamps[index] == stamp) {
                        continue;
                    }
                    stamps[index] = stamp;
                    Obstacle o = map.obstacle(index);
                    if (!o.blocksMovement() || o.topAt(c.pos.x, c.pos.z) - feet <= STEP_HEIGHT || o.minY() >= top) {
                        continue;
                    }
                    pushOut(c, o.minX(), o.maxX(), o.minZ(), o.maxZ(), radius);
                }
            }
        }
    }

    private static void pushOut(Combatant c, double minX, double maxX, double minZ, double maxZ, double radius) {
        double nearestX = Math.min(Math.max(c.pos.x, minX), maxX);
        double nearestZ = Math.min(Math.max(c.pos.z, minZ), maxZ);
        double dx = c.pos.x - nearestX;
        double dz = c.pos.z - nearestZ;
        double distanceSq = dx * dx + dz * dz;
        if (distanceSq >= radius * radius) {
            return;
        }
        if (distanceSq > 1e-12) {
            double distance = Math.sqrt(distanceSq);
            double push = radius - distance;
            c.pos.x += dx / distance * push;
            c.pos.z += dz / distance * push;
            return;
        }
        // The centre is inside the box: leave through the closest face.
        double toMinX = c.pos.x - minX;
        double toMaxX = maxX - c.pos.x;
        double toMinZ = c.pos.z - minZ;
        double toMaxZ = maxZ - c.pos.z;
        double smallest = toMinX;
        int face = 0;
        if (toMaxX < smallest) {
            smallest = toMaxX;
            face = 1;
        }
        if (toMinZ < smallest) {
            smallest = toMinZ;
            face = 2;
        }
        if (toMaxZ < smallest) {
            face = 3;
        }
        switch (face) {
            case 0 -> c.pos.x = minX - radius;
            case 1 -> c.pos.x = maxX + radius;
            case 2 -> c.pos.z = minZ - radius;
            default -> c.pos.z = maxZ + radius;
        }
    }

    private double sweepObstacles(double ox, double oy, double oz, double sx, double sy, double sz, double radius,
            int kind) {
        stamp++;
        double best = -1;
        lastBarrierHit = null;
        for (Barrier b : barriers) {
            if (!b.active) {
                continue;
            }
            double t = boxHitTime(b.minX, b.minY, b.minZ, b.maxX, b.maxY, b.maxZ, ox, oy, oz, sx, sy, sz, radius);
            if (t >= 0 && (best < 0 || t < best)) {
                best = t;
                lastBarrierHit = b;
            }
        }
        int c0 = grid.columnOf(Math.min(ox, ox + sx) - radius);
        int c1 = grid.columnOf(Math.max(ox, ox + sx) + radius);
        int r0 = grid.rowOf(Math.min(oz, oz + sz) - radius);
        int r1 = grid.rowOf(Math.max(oz, oz + sz) + radius);
        for (int row = r0; row <= r1; row++) {
            for (int col = c0; col <= c1; col++) {
                for (int index : grid.obstaclesIn(col, row)) {
                    if (stamps[index] == stamp) {
                        continue;
                    }
                    stamps[index] = stamp;
                    Obstacle o = map.obstacle(index);
                    boolean blocks = kind == KIND_PROJECTILE ? o.blocksProjectiles() : o.blocksLineOfSight();
                    if (!blocks) {
                        continue;
                    }
                    double t = hitTime(o, ox, oy, oz, sx, sy, sz, radius);
                    if (t >= 0 && (best < 0 || t < best)) {
                        best = t;
                        lastBarrierHit = null;
                    }
                }
            }
        }
        return best;
    }

    /** Slab test of a segment against a plain box grown by {@code r}; returns the entry fraction or -1. */
    private static double boxHitTime(double minX, double minY, double minZ, double maxX, double maxY, double maxZ,
            double ox, double oy, double oz, double sx, double sy, double sz, double r) {
        double tMin = 0;
        double tMax = 1;
        for (int axis = 0; axis < 3; axis++) {
            double origin = axis == 0 ? ox : axis == 1 ? oy : oz;
            double delta = axis == 0 ? sx : axis == 1 ? sy : sz;
            double lo = (axis == 0 ? minX : axis == 1 ? minY : minZ) - r;
            double hi = (axis == 0 ? maxX : axis == 1 ? maxY : maxZ) + r;
            if (Math.abs(delta) < 1e-12) {
                if (origin < lo || origin > hi) {
                    return -1;
                }
                continue;
            }
            double t1 = (lo - origin) / delta;
            double t2 = (hi - origin) / delta;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
            if (tMin > tMax) {
                return -1;
            }
        }
        return tMin;
    }

    /**
     * Slab test of a segment against a box grown by {@code r}; returns the entry fraction or -1. A ramp is the same
     * box without its top face, cut by the half-space under its sloped surface (moved outwards by {@code r} along the
     * surface normal).
     */
    private static double hitTime(Obstacle o, double ox, double oy, double oz, double sx, double sy, double sz,
            double r) {
        double tMin = 0;
        double tMax = 1;
        boolean ramp = o.isRamp();
        for (int axis = 0; axis < 3; axis++) {
            double origin;
            double delta;
            double lo;
            double hi;
            if (axis == 0) {
                origin = ox;
                delta = sx;
                lo = o.minX() - r;
                hi = o.maxX() + r;
            } else if (axis == 1) {
                origin = oy;
                delta = sy;
                lo = o.minY() - r;
                hi = ramp ? Double.POSITIVE_INFINITY : o.maxY() + r;
            } else {
                origin = oz;
                delta = sz;
                lo = o.minZ() - r;
                hi = o.maxZ() + r;
            }
            if (Math.abs(delta) < 1e-12) {
                if (origin < lo || origin > hi) {
                    return -1;
                }
            } else {
                double t1 = (lo - origin) / delta;
                double t2 = (hi - origin) / delta;
                if (t1 > t2) {
                    double swap = t1;
                    t1 = t2;
                    t2 = swap;
                }
                tMin = Math.max(tMin, t1);
                tMax = Math.min(tMax, t2);
                if (tMin > tMax) {
                    return -1;
                }
            }
        }
        if (ramp) {
            double gx = o.gradientX();
            double gz = o.gradientZ();
            double slope = o.slope();
            // Height of the segment above the grown surface at t is f0 + t * fd; it is inside while that is <= 0.
            double f0 = oy - o.planeHeight(ox, oz) - r * Math.sqrt(1 + slope * slope);
            double fd = sy - (gx * sx + gz * sz);
            if (Math.abs(fd) < 1e-12) {
                if (f0 > 0) {
                    return -1;
                }
            } else {
                double boundary = -f0 / fd;
                if (fd > 0) {
                    tMax = Math.min(tMax, boundary);
                } else {
                    tMin = Math.max(tMin, boundary);
                }
                if (tMin > tMax) {
                    return -1;
                }
            }
        }
        return tMin;
    }
}
