package com.bajoterra.core.math;

/**
 * Deterministic, allocation-free trigonometry for the simulation hot path.
 *
 * <p>Why not {@code Math}? Its sine, cosine and arctangent may differ in the last bit between CPUs (intrinsics),
 * which would make a replay diverge on another machine. Why not {@code StrictMath}? Its fdlibm port allocates a small
 * array during argument reduction, which breaks the zero-garbage tick. These functions use only IEEE-754 additions,
 * multiplications, divisions, comparisons and {@code Math.floor}/{@code Math.sqrt} (all exactly specified), so every
 * platform produces identical bits. Absolute error is below 1e-10, far finer than anything the game can show.
 */
public final class Trig {

    private static final double HALF_PI = Math.PI / 2;
    private static final double QUARTER_PI = Math.PI / 4;
    private static final double TWO_PI = Math.PI * 2;
    /** tan(pi / 8): above it the arctangent argument is folded towards zero for faster convergence. */
    private static final double TAN_EIGHTH_PI = 0.41421356237309503;

    // Taylor coefficients of sin(x): x - x^3/3! + x^5/5! - ...
    private static final double S3 = -1.0 / 6.0;
    private static final double S5 = 1.0 / 120.0;
    private static final double S7 = -1.0 / 5040.0;
    private static final double S9 = 1.0 / 362880.0;
    private static final double S11 = -1.0 / 39916800.0;
    private static final double S13 = 1.0 / 6227020800.0;
    private static final double S15 = -1.0 / 1307674368000.0;

    private Trig() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Sine of an angle in radians. */
    public static double sin(double angle) {
        double r = angle - TWO_PI * Math.floor(angle / TWO_PI + 0.5);   // [-pi, pi]
        if (r > HALF_PI) {
            r = Math.PI - r;
        } else if (r < -HALF_PI) {
            r = -Math.PI - r;
        }
        double r2 = r * r;
        return r * (1.0 + r2 * (S3 + r2 * (S5 + r2 * (S7 + r2 * (S9 + r2 * (S11 + r2 * (S13 + r2 * S15)))))));
    }

    /** Cosine of an angle in radians. */
    public static double cos(double angle) {
        return sin(angle + HALF_PI);
    }

    /**
     * Angle of the point (x, y) in radians in (-pi, pi], like {@link Math#atan2}. Both zero gives zero.
     *
     * @param y vertical component (the sine side)
     * @param x horizontal component (the cosine side)
     */
    public static double atan2(double y, double x) {
        double ax = Math.abs(x);
        double ay = Math.abs(y);
        if (ax + ay == 0.0) {
            return 0.0;
        }
        double ratio = ay <= ax ? ay / ax : ax / ay;   // in [0, 1]
        double angle = arctan(ratio);
        if (ay > ax) {
            angle = HALF_PI - angle;
        }
        if (x < 0) {
            angle = Math.PI - angle;
        }
        return y < 0 ? -angle : angle;
    }

    /** Length of the vector (x, y). */
    public static double hypot(double x, double y) {
        return Math.sqrt(x * x + y * y);
    }

    /** Arctangent of a value in [0, 1]. */
    private static double arctan(double value) {
        if (value > TAN_EIGHTH_PI) {
            return QUARTER_PI + arctanSmall((value - 1.0) / (value + 1.0));
        }
        return arctanSmall(value);
    }

    /** Arctangent series, accurate to about 1e-12 for |u| up to tan(pi / 8). Horner form, highest term first. */
    private static double arctanSmall(double u) {
        double u2 = u * u;
        double p = 1.0 / 25.0;
        p = -1.0 / 23.0 + u2 * p;
        p = 1.0 / 21.0 + u2 * p;
        p = -1.0 / 19.0 + u2 * p;
        p = 1.0 / 17.0 + u2 * p;
        p = -1.0 / 15.0 + u2 * p;
        p = 1.0 / 13.0 + u2 * p;
        p = -1.0 / 11.0 + u2 * p;
        p = 1.0 / 9.0 + u2 * p;
        p = -1.0 / 7.0 + u2 * p;
        p = 1.0 / 5.0 + u2 * p;
        p = -1.0 / 3.0 + u2 * p;
        return u * (1.0 + u2 * p);
    }
}
