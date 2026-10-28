package com.bajoterra.core.sim;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.rules.MatchPhase;
import java.lang.management.ManagementFactory;
import org.junit.jupiter.api.Test;

/**
 * Performance budget: after warm-up, a tick of a full 3v3 bot match must not allocate (no garbage in the 60 Hz
 * loop means no GC pauses). The threshold leaves room only for rare, per-round bookkeeping.
 */
class AllocationRegressionTest {

    private static final int WARM_UP_TICKS = 20_000;
    private static final int MEASURED_TICKS = 40_000;
    private static final double MAX_BYTES_PER_TICK = 1.0;

    @Test
    void aSteadyStateTickAllocatesNothing() {
        com.sun.management.ThreadMXBean threads =
                (com.sun.management.ThreadMXBean) ManagementFactory.getThreadMXBean();
        // Rules that never end the match, so the whole measurement covers many rounds of real play.
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(21)
                .rules(new com.bajoterra.core.rules.EliminationRoundsRules(180, 5400, 180, 1000, 1000)).build());
        sim.start();
        run(sim, WARM_UP_TICKS);
        long before = threads.getCurrentThreadAllocatedBytes();

        run(sim, MEASURED_TICKS);

        double perTick = (threads.getCurrentThreadAllocatedBytes() - before) / (double) MEASURED_TICKS;
        assertThat(sim.match().phase()).isNotEqualTo(MatchPhase.MATCH_END);
        assertThat(perTick).as("bytes allocated per tick").isLessThanOrEqualTo(MAX_BYTES_PER_TICK);
    }

    private void run(GameSimulation sim, int ticks) {
        for (int i = 0; i < ticks; i++) {
            if (sim.match().phase() == MatchPhase.MATCH_END) {
                return;
            }
            sim.tick();
        }
    }
}
