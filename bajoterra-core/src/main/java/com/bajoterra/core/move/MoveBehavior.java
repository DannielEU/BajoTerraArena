package com.bajoterra.core.move;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import java.util.Map;

/**
 * Optional code hook of a move (Strategy). Most moves are pure data (speed and hitbox modifiers, invulnerability
 * window); a behavior adds physical effects such as a vertical impulse or driven horizontal movement.
 * Implementations must be stateless: one instance is shared by every combatant using the move.
 */
public interface MoveBehavior {

    /** Called on the tick the move starts. */
    default void onStart(Combatant self, MoveRunner runner, CombatArena arena) {
        // no-op by default
    }

    /** Called every tick while the move is active, after its modifiers were applied. */
    default void onTick(Combatant self, MoveRunner runner, CombatArena arena) {
        // no-op by default
    }

    /** Called when the move ends or is cancelled. */
    default void onEnd(Combatant self, MoveRunner runner, CombatArena arena) {
        // no-op by default
    }

    /**
     * Name of the behavior type for clients that mirror the movement (client-side prediction); an empty string
     * means the behavior has no client-visible parameters and is not predicted.
     */
    default String type() {
        return "";
    }

    /** Numeric parameters that go with {@link #type()}. */
    default Map<String, Double> params() {
        return Map.of();
    }
}
