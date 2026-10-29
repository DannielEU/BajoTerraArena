package com.bajoterra.core.stats;

import com.bajoterra.core.slug.Element;
import java.util.Arrays;

/**
 * Per-tick derived stats of a combatant. It is reset every tick and then every active status effect and active
 * move applies its modifiers to it (a stack of {@code StatModifier}s without allocating objects).
 */
public final class StatBlock {

    /** Multiplies movement speed. */
    public double speedMultiplier = 1.0;
    /** Multiplies the hitbox height (crouching shrinks it). */
    public double hitboxHeightMultiplier = 1.0;
    /** Multiplies healing and shield restoration received. */
    public double healReceivedMultiplier = 1.0;
    /** Extra random aim error in radians (blinded combatants). */
    public double aimSpread;
    /** Multiplies the damage this combatant deals (courage). */
    public double damageDealtMultiplier = 1.0;
    /** Multiplies the blaster's interval between shots (below 1 fires faster). */
    public double fireIntervalMultiplier = 1.0;
    /** Multiplies all the damage this combatant takes (courage); elemental resistances stack on top. */
    public double damageTakenMultiplier = 1.0;
    /** Ignores all damage while true (roll invulnerability frames). */
    public boolean invulnerable;
    /** Cannot move, fire or start moves while true. */
    public boolean stunned;

    private final double[] elementTaken = new double[Element.COUNT];

    public StatBlock() {
        reset();
    }

    /** Restores the neutral values. */
    public void reset() {
        speedMultiplier = 1.0;
        hitboxHeightMultiplier = 1.0;
        healReceivedMultiplier = 1.0;
        damageDealtMultiplier = 1.0;
        fireIntervalMultiplier = 1.0;
        damageTakenMultiplier = 1.0;
        aimSpread = 0;
        invulnerable = false;
        stunned = false;
        Arrays.fill(elementTaken, 1.0);
    }

    /** Damage multiplier applied when the combatant is hit by the given element. */
    public double elementTakenMultiplier(Element element) {
        return elementTaken[element.ordinal()];
    }

    /** Multiplies the damage taken from an element (vulnerability above 1, resistance below 1). */
    public void multiplyElementTaken(Element element, double factor) {
        elementTaken[element.ordinal()] *= factor;
    }
}
