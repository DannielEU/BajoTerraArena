package com.bajoterra.core.slug;

/**
 * Flight parameters of a fired slug.
 *
 * @param speed launch speed in m/s (the series transforms slugs at ~100 mph, i.e. ~45 m/s)
 * @param gravity downward acceleration in m/s^2 (0 = straight shot, higher = lobbed arc)
 * @param radius collision radius in metres
 * @param lifetimeTicks ticks before the slug expires in flight
 * @param pierce number of additional valid combatants the slug passes through after the first hit
 */
public record ProjectileSpec(double speed, double gravity, double radius, int lifetimeTicks, int pierce) {

    public ProjectileSpec {
        if (!(speed > 0) || !(gravity >= 0) || !(radius > 0) || lifetimeTicks <= 0 || pierce < 0) {
            throw new IllegalArgumentException("Invalid projectile spec: speed=" + speed + " gravity=" + gravity
                    + " radius=" + radius + " lifetimeTicks=" + lifetimeTicks + " pierce=" + pierce);
        }
    }

    /** A sensible default: 45 m/s, mild arc, 0.25 m radius, 2 seconds of flight, no piercing. */
    public static ProjectileSpec standard() {
        return new ProjectileSpec(45, 4, 0.25, 120, 0);
    }
}
