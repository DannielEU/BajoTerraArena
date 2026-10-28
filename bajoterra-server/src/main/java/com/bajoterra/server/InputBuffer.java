package com.bajoterra.server;

import com.bajoterra.core.sim.InputFrame;

/**
 * Collects the input frames a client sent between two simulation ticks. Held values (movement, aim, fire) come from
 * the newest frame; one-shot values (slot selection and cycling) are kept until a tick consumes them, so a frame
 * that arrives together with a newer one cannot lose a slot change.
 */
final class InputBuffer {

    private com.bajoterra.protocol.generated.InputFrame latest;
    private int pendingSelect = -1;
    private int pendingCycle;

    /** Records a frame received from the network. */
    void accept(com.bajoterra.protocol.generated.InputFrame frame) {
        latest = frame;
        if (frame.getSelectSlot() >= 0) {
            pendingSelect = frame.getSelectSlot();
        }
        if (frame.getCycleSlot() != 0) {
            pendingCycle = frame.getCycleSlot();
        }
    }

    /**
     * Turns what has been collected into a core input frame for the combatant, consuming the one-shot values.
     * Returns null when the client has not sent anything yet.
     */
    InputFrame drain(int combatantId) {
        if (latest == null) {
            return null;
        }
        InputFrame frame = new InputFrame(combatantId, latest.getSequence(), latest.getMoveX(), latest.getMoveZ(),
                latest.getAimYaw(), latest.getAimPitch(), latest.getHasCamera(), latest.getCamX(), latest.getCamY(),
                latest.getCamZ(), latest.getFire(), latest.getMoveMask(), pendingSelect, pendingCycle);
        pendingSelect = -1;
        pendingCycle = 0;
        return frame;
    }
}
