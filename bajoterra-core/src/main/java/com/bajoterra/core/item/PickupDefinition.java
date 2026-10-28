package com.bajoterra.core.item;

import com.bajoterra.core.content.Identified;
import com.bajoterra.core.effect.Effect;

/**
 * Immutable definition of a collectable item. It reuses the same {@link Effect}s as slugs, so restoring health
 * through a pickup and through an ally's healing slug is the same code path.
 *
 * @param respawnTicks ticks until the pickup becomes available again after being taken
 * @param radius horizontal distance at which a combatant collects it
 */
public record PickupDefinition(
        String id,
        int index,
        String name,
        Effect effect,
        int respawnTicks,
        double radius,
        PickupRequirement requirement) implements Identified {

    public PickupDefinition {
        if (id == null || id.isBlank() || respawnTicks <= 0 || !(radius > 0)) {
            throw new IllegalArgumentException("Invalid pickup '" + id + "'");
        }
    }

    public PickupDefinition withIndex(int newIndex) {
        return new PickupDefinition(id, newIndex, name, effect, respawnTicks, radius, requirement);
    }
}
