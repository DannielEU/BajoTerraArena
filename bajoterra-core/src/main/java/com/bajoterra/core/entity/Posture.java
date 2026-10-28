package com.bajoterra.core.entity;

/**
 * Locomotion state of a combatant (State pattern: the posture decides which moves may start). It is derived each
 * tick from physics (grounded or not) and from the active move, so adding a new move never needs a new posture.
 */
public enum Posture {
    /** Standing on the ground with no move active. */
    GROUNDED(1),
    /** In the air with no move active. */
    AIRBORNE(2),
    /** On the ground holding a stance move such as crouch. */
    CROUCHING(4),
    /** Running a timed move such as a roll or a dash. */
    ACTION(8);

    private final int bit;

    Posture(int bit) {
        this.bit = bit;
    }

    /** Bit used in the {@code allowedFrom} mask of a move definition. */
    public int bit() {
        return bit;
    }
}
