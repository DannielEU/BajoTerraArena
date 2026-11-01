package com.bajoterra.core.rules;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;

/** State of a match: current phase, round counter and round wins per team. */
public final class Match {

    private final MatchRules rules;
    private final MatchListener listener;
    private final CombatArena arena;
    private final int[] wins = new int[2];
    private MatchPhase phase = MatchPhase.LOBBY;
    private int ticksLeft;
    private int roundNumber;
    private int lastRoundWinner = MatchRules.ONGOING;
    private int winner = MatchRules.ONGOING;

    public Match(MatchRules rules, MatchListener listener, CombatArena arena) {
        this.rules = rules;
        this.listener = listener;
        this.arena = arena;
    }

    public MatchRules rules() {
        return rules;
    }

    public MatchPhase phase() {
        return phase;
    }

    /** Ticks left in the current phase. */
    public int ticksLeft() {
        return ticksLeft;
    }

    public int roundNumber() {
        return roundNumber;
    }

    public int wins(int team) {
        return wins[team];
    }

    /** Winner of the last finished round: a team index, {@link MatchRules#DRAW}, or ONGOING before any round. */
    public int lastRoundWinner() {
        return lastRoundWinner;
    }

    /** Winner of the match once {@link MatchPhase#MATCH_END} is reached; a team index or DRAW. */
    public int winner() {
        return winner;
    }

    public boolean isOver() {
        return phase == MatchPhase.MATCH_END;
    }

    /** Leaves the lobby and starts the first round. */
    public void start() {
        if (phase == MatchPhase.LOBBY) {
            beginRound();
        }
    }

    /** Advances the match by one tick. */
    public void tick() {
        phase = phase.tick(this);
    }

    private void beginRound() {
        roundNumber++;
        ticksLeft = rules.countdownTicks();
        phase = MatchPhase.COUNTDOWN;
        listener.onRoundStart(roundNumber);
        GameEvent event = arena.events().begin(EventType.ROUND_STARTED, arena.tick());
        event.a = roundNumber;
    }

    /** Counts the countdown down; true when the round should become active. */
    boolean countdownTick() {
        if (ticksLeft > 0) {
            ticksLeft--;
        }
        if (ticksLeft <= 0) {
            ticksLeft = rules.roundTicks();
            return true;
        }
        return false;
    }

    /** Evaluates the running round; true when it just ended. */
    boolean roundTick() {
        ticksLeft--;
        int result = rules.evaluateRound(arena, ticksLeft);
        if (result == MatchRules.ONGOING) {
            return false;
        }
        lastRoundWinner = result;
        if (result >= 0) {
            wins[result]++;
        }
        ticksLeft = rules.roundEndTicks();
        GameEvent event = arena.events().begin(EventType.ROUND_ENDED, arena.tick());
        event.a = result;
        event.b = roundNumber;
        listener.onRoundEnd(result);
        return true;
    }

    /** Counts the round-end pause down and returns the phase that follows it. */
    MatchPhase roundEndTick() {
        if (ticksLeft > 0) {
            ticksLeft--;
            return MatchPhase.ROUND_END;
        }
        if (rules.isMatchOver(wins, roundNumber)) {
            winner = rules.matchWinner(wins);
            GameEvent event = arena.events().begin(EventType.MATCH_ENDED, arena.tick());
            event.a = winner;
            listener.onMatchEnd(winner);
            return MatchPhase.MATCH_END;
        }
        beginRound();
        return phase;
    }
}
