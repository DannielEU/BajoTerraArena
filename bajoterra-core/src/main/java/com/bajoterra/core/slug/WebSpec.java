package com.bajoterra.core.slug;

import com.bajoterra.core.effect.Effect;

/**
 * The web trap a slug such as Arachnet spins on the floor where it lands: a round web {@code radius} wide that waits
 * up to {@code durationTicks}. The first combatant the slug could hit who steps on it is caught ({@code effects},
 * typically an {@code ensnare}) and the web is spent. Fire burns it away.
 */
public record WebSpec(double radius, int durationTicks, Effect effects) {

    public WebSpec {
        if (!(radius > 0) || durationTicks <= 0) {
            throw new IllegalArgumentException("Invalid web: radius=" + radius + " durationTicks=" + durationTicks);
        }
    }
}
