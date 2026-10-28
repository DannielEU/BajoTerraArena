package com.bajoterra.core.ai;

import com.bajoterra.core.math.Trig;
/** Ballistic aiming: finds the yaw and pitch that make a slug with gravity hit a (possibly moving) point. */
final class AimSolver {

    /** Result holder; reused so aiming never allocates. */
    static final class Aim {
        double yaw;
        double pitch;
    }

    private AimSolver() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * @param out receives the solution
     * @param mx muzzle position
     * @param tx target position (aim point)
     * @param tvx target horizontal velocity, used when {@code lead} is true
     * @param speed launch speed of the slug in m/s
     * @param gravity downward acceleration of the slug in m/s^2
     */
    static void solve(Aim out, double mx, double my, double mz, double tx, double ty, double tz,
            double tvx, double tvz, double speed, double gravity, boolean lead) {
        double flightTime = 0;
        for (int i = 0; i < 3; i++) {
            double px = lead ? tx + tvx * flightTime : tx;
            double pz = lead ? tz + tvz * flightTime : tz;
            double dx = px - mx;
            double dz = pz - mz;
            double horizontal = Trig.hypot(dx, dz);
            double dy = ty - my + 0.5 * gravity * flightTime * flightTime;
            double pitch = Trig.atan2(dy, horizontal);
            out.yaw = Trig.atan2(dx, dz);
            out.pitch = pitch;
            flightTime = horizontal / Math.max(1e-3, speed * Trig.cos(pitch));
        }
    }

    /** Wraps an angle difference into (-pi, pi]. */
    static double wrap(double angle) {
        double a = angle % (2 * Math.PI);
        if (a > Math.PI) {
            a -= 2 * Math.PI;
        } else if (a <= -Math.PI) {
            a += 2 * Math.PI;
        }
        return a;
    }
}
