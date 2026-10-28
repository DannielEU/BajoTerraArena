package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class BotMatchRunnerTest {

    @Test
    void mirroredBotMatchesFinishAndAreBalanced() throws Exception {
        BotMatchRunner.Summary summary = BotMatchRunner.run(CONTENT,
                new BotMatchRunner.Options(60, 7, "caverna", 0.7, 0.7, 4));

        assertThat(summary.finished()).isEqualTo(60);
        assertThat(summary.aWins() + summary.bWins() + summary.draws()).isEqualTo(60);
        assertThat(summary.aWinRate()).isBetween(0.3, 0.7);
        assertThat(summary.teamZeroWinRate()).isBetween(0.3, 0.7);
        assertThat(summary.averageRounds()).isBetween(3.0, 6.0);
        assertThat(summary.accuracy()).isBetween(0.05, 0.6);
    }

    @Test
    void theRunnerSimulatesMuchFasterThanRealTime() throws Exception {
        BotMatchRunner.Summary summary = BotMatchRunner.run(CONTENT,
                new BotMatchRunner.Options(8, 3, "caverna", 0.7, 0.7, 1));

        // A single thread must still simulate at least 100x real time (60 ticks/s) on any development machine.
        assertThat(summary.ticksPerSecond()).isGreaterThan(6_000);
    }

    @Test
    void aStrongerAiWinsMoreOften() throws Exception {
        BotMatchRunner.Summary summary = BotMatchRunner.run(CONTENT,
                new BotMatchRunner.Options(20, 100, "caverna", 0.95, 0.2, 4));

        assertThat(summary.aWinRate()).isGreaterThan(0.85);
    }
}
