package com.bajoterra.core.slug;

import com.bajoterra.core.effect.Effect;

/**
 * The whirlwind a slug such as Tormato turns into where it lands: a column of wind {@code radius} wide and
 * {@code height} tall that lasts {@code durationTicks} and drifts on at {@code drift} m/s the way the slug flew.
 * Enemies inside are dragged towards its eye ({@code pull}, m/s^2) and spun round it; in the eye they are lifted at
 * {@code lift} m/s and suffer {@code effects} every {@code periodTicks}. It throws enemy slugs off course. Whoever
 * fired it, and their allies, are launched {@code allyLift} m/s upwards once when they step into its eye (a tornado
 * jump). Once an ice slug is fired into it, it turns into a freezing tornado and its eye also applies
 * {@code frostEffects} (typically a freeze) every period.
 */
public record TornadoSpec(double radius, double eyeRadius, double height, int durationTicks, double drift,
        double pull, double lift, double allyLift, int periodTicks, Effect effects, Effect frostEffects) {

    public TornadoSpec {
        if (!(radius > 0) || !(eyeRadius > 0) || eyeRadius > radius || !(height > 0) || durationTicks <= 0
                || !(drift >= 0) || !(pull >= 0) || !(lift >= 0) || !(allyLift >= 0) || periodTicks <= 0) {
            throw new IllegalArgumentException("Invalid tornado: radius=" + radius + " eyeRadius=" + eyeRadius
                    + " height=" + height + " durationTicks=" + durationTicks);
        }
    }

    /** A tornado whose freezing form only adds ice damage. */
    public TornadoSpec(double radius, double eyeRadius, double height, int durationTicks, double drift, double pull,
            double lift, double allyLift, int periodTicks, Effect effects) {
        this(radius, eyeRadius, height, durationTicks, drift, pull, lift, allyLift, periodTicks, effects, Effect.NONE);
    }
}
