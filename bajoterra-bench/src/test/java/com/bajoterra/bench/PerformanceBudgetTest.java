package com.bajoterra.bench;

import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.content.Content;
import org.junit.jupiter.api.Test;

/**
 * The performance budget as executable tests. The limits are the targets of the plan, deliberately loose enough for
 * a busy CI machine; the real numbers are tracked with JMH and documented in docs/performance.md.
 */
class PerformanceBudgetTest {

    private static final Content CONTENT = Content.load();

    @Test
    void aThirtyMinuteSoakStaysFarBelowTheFrameBudget() {
        SoakRunner.Result result = SoakRunner.run(CONTENT, 30 * 60, 4321);

        // Budget (plan): mean <= 0.25 ms, p99 <= 1 ms, out of 16.67 ms per tick.
        assertThat(result.meanNanos() / 1e6).as("mean tick (ms)").isLessThan(0.25);
        assertThat(result.p99Nanos() / 1e6).as("p99 tick (ms)").isLessThan(1.0);
        assertThat(result.heapGrowthBytes() / 1e6).as("heap growth over 30 simulated minutes (MB)").isLessThan(16.0);
    }

    @Test
    void manyConcurrentMatchesStillRunMuchFasterThanRealTime() throws InterruptedException {
        LoadRunner.Result result = LoadRunner.run(CONTENT, 24, 2, 1.0);

        // 24 matches on two threads must each be simulated well above real time (60 ticks/s each).
        assertThat(result.ticksPerSecond() / result.matches()).as("ticks per second per match")
                .isGreaterThan(60 * 20);
        assertThat(result.realTimeMatches()).isGreaterThan(24);
    }
}
