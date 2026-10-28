package com.bajoterra.core.rules;

/** Phases of a match (State pattern: each phase decides how the match advances). */
public enum MatchPhase {

    /** Waiting for {@link Match#start()}. */
    LOBBY {
        @Override
        MatchPhase tick(Match match) {
            return this;
        }
    },

    /** Combatants are placed and frozen while the countdown runs. */
    COUNTDOWN {
        @Override
        MatchPhase tick(Match match) {
            return match.countdownTick() ? ROUND_ACTIVE : this;
        }
    },

    /** The round is being fought. */
    ROUND_ACTIVE {
        @Override
        MatchPhase tick(Match match) {
            return match.roundTick() ? ROUND_END : this;
        }
    },

    /** The round result is displayed before the next round or the end of the match. */
    ROUND_END {
        @Override
        MatchPhase tick(Match match) {
            return match.roundEndTick();
        }
    },

    /** Terminal phase. */
    MATCH_END {
        @Override
        MatchPhase tick(Match match) {
            return this;
        }
    };

    /** Advances the match by one tick and returns the phase for the next tick. */
    abstract MatchPhase tick(Match match);

    /** True when combatants may move and fire. */
    public boolean isGameplayActive() {
        return this == ROUND_ACTIVE;
    }
}
