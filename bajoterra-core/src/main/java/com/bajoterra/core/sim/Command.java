package com.bajoterra.core.sim;

import com.bajoterra.core.entity.InputState;

/**
 * A player action submitted to the simulation (Command pattern). Commands are queued and applied at the start of
 * the next tick, which makes a match fully reproducible from its seed plus its command log.
 */
public sealed interface Command permits InputFrame {

    /** Id of the combatant the command is for. */
    int combatantId();

    /** Writes the command into the combatant's input state. */
    void applyTo(InputState state);
}
