package com.bajoterra.core.character;

/**
 * Blaster parameters of a character.
 *
 * @param fireIntervalTicks minimum ticks between two shots (multiplied by the fired slug's delay multiplier)
 * @param reloadShots number of further shots a fired slug stays locked
 */
public record BlasterSpec(int fireIntervalTicks, int reloadShots) {

    public BlasterSpec {
        if (fireIntervalTicks <= 0 || reloadShots < 0) {
            throw new IllegalArgumentException("Invalid blaster: interval=" + fireIntervalTicks
                    + " reload=" + reloadShots);
        }
    }

    /** About 0.7 s between shots and a 3-shot lock, the game's baseline. */
    public static BlasterSpec standard() {
        return new BlasterSpec(42, 3);
    }
}
