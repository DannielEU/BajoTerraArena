package com.bajoterra.core.sim;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

class SimulationConstantsTest {

    @Test
    void snapshotRateDividesTickRateExactly() {
        assertThat(SimulationConstants.TICKS_PER_SECOND % SimulationConstants.SNAPSHOT_RATE_HZ).isZero();
        assertThat(SimulationConstants.TICKS_PER_SNAPSHOT).isEqualTo(3);
    }

    @Test
    void oneSecondOfTicksIsOneSecond() {
        assertThat(SimulationConstants.TICKS_PER_SECOND * SimulationConstants.TICK_SECONDS)
                .isCloseTo(1.0, org.assertj.core.data.Offset.offset(1e-9));
    }

    @Test
    void botsThinkAtTenHertz() {
        assertThat(SimulationConstants.TICKS_PER_SECOND / SimulationConstants.BOT_THINK_INTERVAL_TICKS)
                .isEqualTo(10);
    }
}
