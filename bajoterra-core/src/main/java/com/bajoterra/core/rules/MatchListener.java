package com.bajoterra.core.rules;

/** Receives the lifecycle callbacks of a {@link Match}; the simulation implements it to reset the world. */
public interface MatchListener {

    /** A round is about to start (countdown phase): place combatants, refill pickups, clear projectiles. */
    void onRoundStart(int roundNumber);

    /** A round just ended; {@code winnerTeam} is -1 for a draw. */
    default void onRoundEnd(int winnerTeam) {
        // nothing by default
    }

    /** The match finished. */
    void onMatchEnd(int winnerTeam);
}
