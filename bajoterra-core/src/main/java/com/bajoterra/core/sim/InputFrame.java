package com.bajoterra.core.sim;

import com.bajoterra.core.entity.InputState;

/**
 * One frame of player input, as sent by a client every tick.
 *
 * @param sequence client-side frame number, echoed back in snapshots for reconciliation
 * @param moveX strafe axis in [-1, 1]
 * @param moveZ forward axis in [-1, 1]
 * @param aimYaw camera yaw in radians (0 = +Z)
 * @param aimPitch camera pitch in radians (positive looks up)
 * @param hasCamera whether {@code camX/camY/camZ} carry the camera origin (third person aiming)
 * @param fire fire button held
 * @param moveMask bit i requests the i-th move of the character's move set
 * @param selectSlot slot to select (0-9) or -1
 * @param cycleSlot -1 / 0 / +1 to move the selection
 */
public record InputFrame(
        int combatantId,
        int sequence,
        float moveX,
        float moveZ,
        float aimYaw,
        float aimPitch,
        boolean hasCamera,
        float camX,
        float camY,
        float camZ,
        boolean fire,
        int moveMask,
        int selectSlot,
        int cycleSlot) implements Command {

    @Override
    public void applyTo(InputState state) {
        state.sequence = sequence;
        state.moveX = clamp(moveX);
        state.moveZ = clamp(moveZ);
        state.aimYaw = aimYaw;
        state.aimPitch = Math.max(-1.5f, Math.min(1.5f, aimPitch));
        state.hasCamera = hasCamera;
        state.camX = camX;
        state.camY = camY;
        state.camZ = camZ;
        state.fire = fire;
        state.moveMask = moveMask;
        if (selectSlot >= 0) {
            state.selectSlot = selectSlot;
        }
        if (cycleSlot != 0) {
            state.cycleSlot = cycleSlot;
        }
    }

    private static double clamp(float axis) {
        return Float.isNaN(axis) ? 0 : Math.max(-1f, Math.min(1f, axis));
    }
}
