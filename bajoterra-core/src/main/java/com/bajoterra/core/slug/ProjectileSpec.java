package com.bajoterra.core.slug;

/**
 * Flight parameters of a fired slug.
 *
 * @param speed launch speed in m/s (the series transforms slugs at ~100 mph, i.e. ~45 m/s)
 * @param gravity downward acceleration in m/s^2 (0 = straight shot, higher = lobbed arc)
 * @param radius collision radius in metres
 * @param lifetimeTicks ticks before the slug expires in flight
 * @param pierce number of additional valid combatants the slug passes through after the first hit
 * @param fuseTicks 0 for a normal slug; otherwise the slug is sticky: it stops and sticks to whatever it touches
 *     first (an obstacle, the ground or a combatant it can affect) and expires this many ticks later, which runs
 *     its {@code onExpire} effects (a grenade's explosion)
 */
public record ProjectileSpec(double speed, double gravity, double radius, int lifetimeTicks, int pierce,
        int fuseTicks) {

    public ProjectileSpec {
        if (!(speed > 0) || !(gravity >= 0) || !(radius > 0) || lifetimeTicks <= 0 || pierce < 0 || fuseTicks < 0) {
            throw new IllegalArgumentException("Invalid projectile spec: speed=" + speed + " gravity=" + gravity
                    + " radius=" + radius + " lifetimeTicks=" + lifetimeTicks + " pierce=" + pierce
                    + " fuseTicks=" + fuseTicks);
        }
    }

    /** A slug that does not stick. */
    public ProjectileSpec(double speed, double gravity, double radius, int lifetimeTicks, int pierce) {
        this(speed, gravity, radius, lifetimeTicks, pierce, 0);
    }

    /** True when the slug sticks where it lands and detonates once its fuse burns out. */
    public boolean sticky() {
        return fuseTicks > 0;
    }

    /** A sensible default: 45 m/s, mild arc, 0.25 m radius, 2 seconds of flight, no piercing. */
    public static ProjectileSpec standard() {
        return new ProjectileSpec(45, 4, 0.25, 120, 0);
    }
}
