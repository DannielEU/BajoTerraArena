package com.bajoterra.bench;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.rules.EliminationRoundsRules;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;

/** Shared setup code for the benchmarks. */
public final class BenchSupport {

    private BenchSupport() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Rules that never end a match, so a benchmark can run for as long as it likes. */
    public static EliminationRoundsRules endlessRules() {
        return new EliminationRoundsRules(60, 5400, 60, 1_000_000, 1_000_000);
    }

    /** A 3v3 bot match on the given map with endless rules, started and past its first countdown. */
    public static GameSimulation runningBotMatch(Content content, String mapId, long seed) {
        GameSimulation sim = new GameSimulation(content,
                MatchConfig.builder(mapId).seed(seed).rules(endlessRules()).build());
        sim.start();
        for (int i = 0; i < 120; i++) {
            sim.tick();
        }
        return sim;
    }
}
