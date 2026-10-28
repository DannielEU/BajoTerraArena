package com.bajoterra.core.math;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import net.jqwik.api.ForAll;
import net.jqwik.api.Property;
import net.jqwik.api.constraints.DoubleRange;
import org.junit.jupiter.api.Test;

/** The deterministic trigonometry must agree with the reference implementation to far below game precision. */
class TrigTest {

    private static final double TOLERANCE = 1e-9;

    @Test
    void sineAndCosineMatchTheReferenceAcrossManyTurns() {
        for (double angle = -40; angle <= 40; angle += 0.0137) {
            assertThat(Trig.sin(angle)).isCloseTo(StrictMath.sin(angle), within(TOLERANCE));
            assertThat(Trig.cos(angle)).isCloseTo(StrictMath.cos(angle), within(TOLERANCE));
        }
    }

    @Test
    void notableAnglesAreExactEnough() {
        assertThat(Trig.sin(0)).isZero();
        assertThat(Trig.cos(0)).isCloseTo(1, within(TOLERANCE));
        assertThat(Trig.sin(Math.PI / 2)).isCloseTo(1, within(TOLERANCE));
        assertThat(Trig.cos(Math.PI / 2)).isCloseTo(0, within(TOLERANCE));
        assertThat(Trig.sin(Math.PI)).isCloseTo(0, within(TOLERANCE));
        assertThat(Trig.cos(Math.PI)).isCloseTo(-1, within(TOLERANCE));
        assertThat(Trig.sin(-Math.PI / 2)).isCloseTo(-1, within(TOLERANCE));
        assertThat(Trig.sin(2 * Math.PI)).isCloseTo(0, within(TOLERANCE));
    }

    @Property
    void sineSquaredPlusCosineSquaredIsOne(@ForAll @DoubleRange(min = -1000, max = 1000) double angle) {
        double s = Trig.sin(angle);
        double c = Trig.cos(angle);

        assertThat(s * s + c * c).isCloseTo(1.0, within(1e-8));
    }

    @Test
    void arctangentMatchesTheReferenceInEveryQuadrant() {
        for (double y = -5; y <= 5; y += 0.173) {
            for (double x = -5; x <= 5; x += 0.211) {
                if (x == 0 && y == 0) {
                    continue;
                }
                assertThat(Trig.atan2(y, x)).as("atan2(%f, %f)", y, x)
                        .isCloseTo(StrictMath.atan2(y, x), within(TOLERANCE));
            }
        }
    }

    @Test
    void arctangentHandlesAxesAndTheOrigin() {
        assertThat(Trig.atan2(0, 0)).isZero();
        assertThat(Trig.atan2(0, 1)).isZero();
        assertThat(Trig.atan2(1, 0)).isCloseTo(Math.PI / 2, within(TOLERANCE));
        assertThat(Trig.atan2(-1, 0)).isCloseTo(-Math.PI / 2, within(TOLERANCE));
        assertThat(Trig.atan2(0, -1)).isCloseTo(Math.PI, within(TOLERANCE));
        assertThat(Trig.atan2(1e-300, 1)).isCloseTo(0, within(TOLERANCE));
    }

    @Property
    void arctangentInvertsTheDirection(@ForAll @DoubleRange(min = -3.1, max = 3.1) double angle) {
        double recovered = Trig.atan2(Trig.sin(angle), Trig.cos(angle));

        assertThat(recovered).isCloseTo(angle, within(1e-8));
    }

    @Test
    void hypotIsThePythagoreanLength() {
        assertThat(Trig.hypot(3, 4)).isEqualTo(5.0);
        assertThat(Trig.hypot(-3, 4)).isEqualTo(5.0);
        assertThat(Trig.hypot(0, 0)).isZero();
    }

    @Test
    void nonFiniteInputsDoNotPretendToBeNumbers() {
        assertThat(Trig.sin(Double.NaN)).isNaN();
        assertThat(Trig.cos(Double.POSITIVE_INFINITY)).isNaN();
        assertThat(Trig.atan2(Double.NaN, 1)).isNaN();
    }

    @Test
    void resultsAreIdenticalOnEveryCallBecauseNothingDependsOnTheMachine() {
        double first = 0;
        double second = 0;
        for (double angle = -7; angle < 7; angle += 0.01) {
            first += Trig.sin(angle) * 3 + Trig.cos(angle) * 5 + Trig.atan2(angle, 2);
        }
        for (double angle = -7; angle < 7; angle += 0.01) {
            second += Trig.sin(angle) * 3 + Trig.cos(angle) * 5 + Trig.atan2(angle, 2);
        }

        assertThat(Double.doubleToLongBits(second)).isEqualTo(Double.doubleToLongBits(first));
    }
}
