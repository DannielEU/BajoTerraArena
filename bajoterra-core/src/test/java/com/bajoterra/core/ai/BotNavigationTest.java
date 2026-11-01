package com.bajoterra.core.ai;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.mapgen.ArenaGenerator;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import org.junit.jupiter.api.Test;

/**
 * Bots get about generated arenas without getting stuck: going somewhere (no fight, not hiding), a bot is almost
 * never still for a second, and seldom for three seconds in a row (before this was fixed, bots swung
 * between two path nodes at corners and jumped against walls: about one second in seven was spent stuck).
 */
class BotNavigationTest {

    @Test
    void botsGoingSomewhereAreAlmostNeverStuck() {
        long windows = 0;
        long still = 0;
        long longStill = 0;
        for (long seed = 0; seed < 8; seed++) {
            GameSimulation sim = new GameSimulation(CONTENT,
                    MatchConfig.builder(ArenaGenerator.MAP_ID).seed(seed).build());
            sim.start();
            int n = sim.combatants().length;
            double[] lastX = new double[n];
            double[] lastZ = new double[n];
            int[] seconds = new int[n];
            for (int t = 0; t < 60 * 60 * 6 && sim.match().phase() != MatchPhase.MATCH_END; t++) {
                sim.tick();
                if (!sim.match().phase().isGameplayActive() || t % 60 != 0) {
                    continue;
                }
                for (Combatant c : sim.combatants()) {
                    if (!c.alive || !(c.brain instanceof BotBrain brain)) {
                        continue;
                    }
                    BotContext ctx = brain.context();
                    double far = Math.hypot(ctx.waypointX - c.pos.x, ctx.waypointZ - c.pos.z);
                    boolean going = ctx.hasWaypoint && ctx.moveSpeedFactor > 0.1 && !ctx.inCover
                            && ctx.intent == BotContext.Intent.NONE && far > 1.5 && !c.stats.rooted
                            && !c.stats.stunned;
                    double moved = Math.hypot(c.pos.x - lastX[c.id], c.pos.z - lastZ[c.id]);
                    lastX[c.id] = c.pos.x;
                    lastZ[c.id] = c.pos.z;
                    if (!going) {
                        seconds[c.id] = 0;
                        continue;
                    }
                    windows++;
                    if (moved < 0.5) {
                        still++;
                        if (++seconds[c.id] == 3) {
                            longStill++;
                        }
                    } else {
                        seconds[c.id] = 0;
                    }
                }
            }
        }

        assertThat(windows).isGreaterThan(1000);
        assertThat((double) still / windows).as("share of seconds spent stuck").isLessThan(0.08);
        assertThat(longStill).as("times stuck for three seconds").isLessThanOrEqualTo(6);
    }
}
