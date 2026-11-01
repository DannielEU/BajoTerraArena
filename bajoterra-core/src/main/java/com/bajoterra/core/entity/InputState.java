package com.bajoterra.core.entity;

/**
 * The latest input of one combatant. Humans fill it from network frames, bots fill it from their brain, so the
 * simulation treats both identically. Aim convention: yaw 0 faces +Z, positive pitch looks up.
 */
public final class InputState {

    /** Strafe axis in [-1, 1]; positive moves right. */
    public double moveX;
    /** Forward axis in [-1, 1]; positive moves forward. */
    public double moveZ;
    public double aimYaw;
    public double aimPitch;
    /** True when the client supplied a camera origin (third person aiming); bots aim from the muzzle instead. */
    public boolean hasCamera;
    public double camX;
    public double camY;
    public double camZ;
    public boolean fire;
    /** Fusion button held: fire a fusion shot (the selected slug and the next one together) once energy is full. */
    public boolean fusion;
    /** Bit i requests the i-th move of the character's move set (held for stance moves). */
    public int moveMask;
    /** Slot to select (0-9), or -1 for no change. Consumed by the fire system. */
    public int selectSlot = -1;
    /** -1 selects the previous available slot, +1 the next one, 0 no change. Consumed by the fire system. */
    public int cycleSlot;
    /** Celebration dance to start (1..{@link Combatant#DANCES}), or 0 for none. Consumed by the simulation. */
    public int dance;
    /** Sequence number of the last client frame applied (echoed back for client-side reconciliation). */
    public int sequence;

    public void reset() {
        moveX = 0;
        moveZ = 0;
        aimYaw = 0;
        aimPitch = 0;
        hasCamera = false;
        camX = 0;
        camY = 0;
        camZ = 0;
        fire = false;
        fusion = false;
        moveMask = 0;
        selectSlot = -1;
        cycleSlot = 0;
        dance = 0;
    }

    /** Clears the one-shot fields once the simulation has consumed them. */
    public void consumeEdges() {
        selectSlot = -1;
        cycleSlot = 0;
        dance = 0;
    }
}
