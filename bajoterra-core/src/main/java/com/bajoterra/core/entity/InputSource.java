package com.bajoterra.core.entity;

import com.bajoterra.core.sim.GameSimulation;

/**
 * Something that produces input for a combatant on the simulation's own schedule. Bots implement it; humans do not
 * need one because their input arrives through commands.
 */
public interface InputSource {

    /** Called by the simulation every tick for each alive combatant that owns an input source. */
    void think(GameSimulation sim, Combatant self);

    /** Called when a new round starts so the source can drop stale memory. */
    default void onRoundStart(GameSimulation sim, Combatant self) {
        // nothing to reset by default
    }
}
