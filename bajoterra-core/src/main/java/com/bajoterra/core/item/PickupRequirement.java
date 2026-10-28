package com.bajoterra.core.item;

import com.bajoterra.core.entity.Combatant;

/** Condition a combatant must meet to consume a pickup (so a health pack is not wasted at full health). */
public enum PickupRequirement {
    /** Always collectable. */
    ANY,
    /** Only when health is not full. */
    NEEDS_HP,
    /** Only when shield is not full. */
    NEEDS_SHIELD,
    /** Only when health or shield is not full. */
    NEEDS_HP_OR_SHIELD;

    /** True when {@code c} can profit from the pickup. */
    public boolean satisfiedBy(Combatant c) {
        boolean hpMissing = c.vitals.hp() < c.vitals.maxHp();
        boolean shieldMissing = c.vitals.shield() < c.vitals.maxShield();
        return switch (this) {
            case ANY -> true;
            case NEEDS_HP -> hpMissing;
            case NEEDS_SHIELD -> shieldMissing;
            case NEEDS_HP_OR_SHIELD -> hpMissing || shieldMissing;
        };
    }
}
