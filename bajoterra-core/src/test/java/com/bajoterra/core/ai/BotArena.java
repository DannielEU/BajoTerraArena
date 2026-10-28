package com.bajoterra.core.ai;

import static com.bajoterra.core.Fixtures.CONTENT;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.LoadoutBuilder;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import java.util.List;
import java.util.SplittableRandom;

/** Runs complete bot-versus-bot matches with mirrored teams, for behaviour and balance tests. */
public final class BotArena {

    /** Outcome of one match from the point of view of team "A" (the team that may start on either side). */
    public record Result(int winnerIsA, int roundsPlayed, int ticks, int aRoundWins, int bRoundWins, int shotsFired,
            int shotsHit, boolean finished) {
    }

    private static final List<String> TEAM = List.of("eli", "kord", "trixie");

    private BotArena() {
        throw new AssertionError("Utility class, not instantiable");
    }

    public static GameSimulation build(String mapId, long seed, boolean aOnTeamZero, double skillA, double skillB) {
        SplittableRandom random = new SplittableRandom(seed);
        MatchConfig.Builder builder = MatchConfig.builder(mapId).seed(seed).fillWithBots(false);
        List<List<String>> loadouts = TEAM.stream()
                .map(c -> LoadoutBuilder.balanced(CONTENT.slugs(), random, 2)).toList();
        for (int side = 0; side < 2; side++) {
            boolean isA = (side == 0) == aOnTeamZero;
            double skill = isA ? skillA : skillB;
            for (int i = 0; i < TEAM.size(); i++) {
                builder.slot(new SlotConfig(side, TEAM.get(i), loadouts.get(i), true, skill));
            }
        }
        return new GameSimulation(CONTENT, builder.build());
    }

    public static Result play(String mapId, long seed, boolean aOnTeamZero, double skillA, double skillB) {
        GameSimulation sim = build(mapId, seed, aOnTeamZero, skillA, skillB);
        sim.start();
        int limit = 60 * 60 * 25;
        while (sim.match().phase() != MatchPhase.MATCH_END && sim.tickCount() < limit) {
            sim.tick();
        }
        int teamA = aOnTeamZero ? 0 : 1;
        int winner = sim.match().winner();
        int winnerIsA = winner < 0 ? -1 : (winner == teamA ? 1 : 0);
        int shots = 0;
        int hits = 0;
        for (Combatant c : sim.combatants()) {
            shots += c.shotsFired;
            hits += c.shotsHit;
        }
        return new Result(winnerIsA, sim.match().roundNumber(), sim.tickCount(),
                sim.match().wins(teamA), sim.match().wins(1 - teamA), shots, hits,
                sim.match().phase() == MatchPhase.MATCH_END);
    }
}
