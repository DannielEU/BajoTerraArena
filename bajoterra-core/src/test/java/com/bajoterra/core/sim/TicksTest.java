package com.bajoterra.core.sim;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import net.jqwik.api.ForAll;
import net.jqwik.api.Property;
import net.jqwik.api.constraints.IntRange;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class TicksTest {

    @ParameterizedTest
    @CsvSource({"0.0,0", "1.0,60", "0.35,21", "0.5,30", "3.0,180"})
    void convertsSecondsToNearestTick(double seconds, int expectedTicks) {
        assertThat(Ticks.fromSeconds(seconds)).isEqualTo(expectedTicks);
    }

    @Test
    void rejectsNegativeDuration() {
        assertThatThrownBy(() -> Ticks.fromSeconds(-0.1)).isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void rejectsNonFiniteDuration() {
        assertThatThrownBy(() -> Ticks.fromSeconds(Double.NaN)).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> Ticks.fromSeconds(Double.POSITIVE_INFINITY))
                .isInstanceOf(IllegalArgumentException.class);
    }

    @Property
    void ticksSurviveARoundTripThroughSeconds(@ForAll @IntRange(min = 0, max = 1_000_000) int ticks) {
        assertThat(Ticks.fromSeconds(Ticks.toSeconds(ticks))).isEqualTo(ticks);
    }
}
