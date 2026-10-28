package com.bajoterra.core.move;

import com.bajoterra.core.content.Identified;

/**
 * Immutable, data-driven definition of a move (jump, crouch, roll, dash, ...).
 *
 * @param durationTicks length of a timed move; ignored for holdable moves
 * @param cooldownTicks ticks before the move can start again, counted from its start
 * @param holdable stays active while its input bit is held (stances such as crouch)
 * @param stance a holdable move that puts the combatant in the CROUCHING posture
 * @param interruptible another move may replace this one while it is active
 * @param blocksFiring the combatant cannot fire while the move is active
 * @param allowedFromMask bit mask of the postures the move may start from
 * @param speedMultiplier walking speed multiplier while active
 * @param hitboxHeightMultiplier hitbox height multiplier while active
 * @param invulnerableFromTick first elapsed tick of the invulnerability window (inclusive)
 * @param invulnerableToTick end of the invulnerability window (exclusive); equal to the start for none
 * @param behavior optional code hook (impulses, driven movement)
 */
public record MoveDefinition(
        String id,
        int index,
        int durationTicks,
        int cooldownTicks,
        boolean holdable,
        boolean stance,
        boolean interruptible,
        boolean blocksFiring,
        int allowedFromMask,
        double speedMultiplier,
        double hitboxHeightMultiplier,
        int invulnerableFromTick,
        int invulnerableToTick,
        MoveBehavior behavior) implements Identified {

    public MoveDefinition {
        if (id == null || id.isBlank()) {
            throw new IllegalArgumentException("Move id is required");
        }
        if (durationTicks < 0 || cooldownTicks < 0 || allowedFromMask == 0 || !(speedMultiplier >= 0)
                || !(hitboxHeightMultiplier > 0) || invulnerableToTick < invulnerableFromTick) {
            throw new IllegalArgumentException("Invalid move '" + id + "'");
        }
        if (!holdable && durationTicks == 0) {
            throw new IllegalArgumentException("Timed move '" + id + "' needs durationTicks > 0");
        }
    }

    public MoveDefinition withIndex(int newIndex) {
        return new MoveDefinition(id, newIndex, durationTicks, cooldownTicks, holdable, stance, interruptible,
                blocksFiring, allowedFromMask, speedMultiplier, hitboxHeightMultiplier, invulnerableFromTick,
                invulnerableToTick, behavior);
    }
}
