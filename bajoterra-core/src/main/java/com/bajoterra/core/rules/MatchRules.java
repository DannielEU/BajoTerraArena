package com.bajoterra.core.rules;

import com.bajoterra.core.combat.CombatArena;

/**
 * The pluggable rule set of a match (Strategy): how long each phase lasts, who wins a round and when the match is
 * over. {@link EliminationRoundsRules} is the default; a respawn-based deathmatch would be another implementation.
 */
public interface MatchRules {

    /** Round result: the round is still being played. */
    int ONGOING = -2;
    /** Round result: nobody won the round. */
    int DRAW = -1;

    int countdownTicks();

    int roundTicks();

    int roundEndTicks();

    /**
     * Evaluates the running round.
     *
     * @param ticksLeft ticks until the round timer runs out
     * @return the winning team index, {@link #DRAW} or {@link #ONGOING}
     */
    int evaluateRound(CombatArena arena, int ticksLeft);

    /** Whether the match is over after the round that just ended. */
    boolean isMatchOver(int[] wins, int roundsPlayed);

    /** The winning team of a finished match, or {@link #DRAW}. */
    int matchWinner(int[] wins);
}
