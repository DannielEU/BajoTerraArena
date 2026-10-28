package com.bajoterra.core.rules;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;

/**
 * Default rules: a round is won by the team that eliminates every opponent; defeated combatants stay out until the
 * next round (no respawn). When the timer runs out the team with more remaining health and shield wins. The first
 * team to {@code winsNeeded} rounds wins the match (best of five by default).
 *
 * @param maxRounds hard cap on the number of rounds so drawn rounds cannot make a match endless
 */
public record EliminationRoundsRules(
        int countdownTicks,
        int roundTicks,
        int roundEndTicks,
        int winsNeeded,
        int maxRounds) implements MatchRules {

    public EliminationRoundsRules {
        if (countdownTicks < 0 || roundTicks <= 0 || roundEndTicks < 0 || winsNeeded <= 0
                || maxRounds < winsNeeded) {
            throw new IllegalArgumentException("Invalid elimination rules");
        }
    }

    /** Best of five: 3 s countdown, 90 s rounds, 3 s between rounds, at most 9 rounds. */
    public static EliminationRoundsRules bestOfFive() {
        return new EliminationRoundsRules(180, 5400, 180, 3, 9);
    }

    @Override
    public int evaluateRound(CombatArena arena, int ticksLeft) {
        int alive0 = arena.aliveCount(0);
        int alive1 = arena.aliveCount(1);
        if (alive0 == 0 && alive1 == 0) {
            return DRAW;
        }
        if (alive0 == 0) {
            return 1;
        }
        if (alive1 == 0) {
            return 0;
        }
        if (ticksLeft <= 0) {
            double health0 = teamHealth(arena, 0);
            double health1 = teamHealth(arena, 1);
            if (Math.abs(health0 - health1) < 1e-9) {
                return DRAW;
            }
            return health0 > health1 ? 0 : 1;
        }
        return ONGOING;
    }

    @Override
    public boolean isMatchOver(int[] wins, int roundsPlayed) {
        return wins[0] >= winsNeeded || wins[1] >= winsNeeded || roundsPlayed >= maxRounds;
    }

    @Override
    public int matchWinner(int[] wins) {
        if (wins[0] == wins[1]) {
            return DRAW;
        }
        return wins[0] > wins[1] ? 0 : 1;
    }

    private static double teamHealth(CombatArena arena, int team) {
        double total = 0;
        for (Combatant c : arena.combatants()) {
            if (c.team == team && c.alive) {
                total += c.vitals.totalFraction();
            }
        }
        return total;
    }
}
