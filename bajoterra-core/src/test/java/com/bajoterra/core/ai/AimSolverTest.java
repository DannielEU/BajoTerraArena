package com.bajoterra.core.ai;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import org.junit.jupiter.api.Test;

class AimSolverTest {

    private final AimSolver.Aim aim = new AimSolver.Aim();

    @Test
    void aimsStraightAtAStationaryTargetWithoutGravity() {
        AimSolver.solve(aim, 0, 1.3, 0, 10, 1.3, 0, 0, 0, 45, 0, false);

        assertThat(aim.yaw).isCloseTo(Math.PI / 2, within(1e-9));
        assertThat(aim.pitch).isCloseTo(0, within(1e-9));
    }

    @Test
    void gravityMakesTheSolverAimAboveTheTarget() {
        AimSolver.solve(aim, 0, 1.3, 0, 20, 1.3, 0, 0, 0, 30, 12, false);

        assertThat(aim.pitch).isGreaterThan(0.05);
        // Check the physics: after the flight time the slug is back at the target height.
        double flightTime = 20 / (30 * Math.cos(aim.pitch));
        double height = 1.3 + 30 * Math.sin(aim.pitch) * flightTime - 0.5 * 12 * flightTime * flightTime;
        assertThat(height).isCloseTo(1.3, within(0.05));
    }

    @Test
    void leadsAMovingTarget() {
        AimSolver.solve(aim, 0, 1.3, 0, 20, 1.3, 0, 0, 6, 40, 0, true);
        double leadYaw = aim.yaw;
        AimSolver.solve(aim, 0, 1.3, 0, 20, 1.3, 0, 0, 6, 40, 0, false);

        assertThat(leadYaw).as("aims ahead of a target running towards +Z").isLessThan(aim.yaw);
    }

    @Test
    void aimsDownAtLowerTargets() {
        AimSolver.solve(aim, 0, 5, 0, 10, 0, 0, 0, 0, 40, 0, false);

        assertThat(aim.pitch).isLessThan(0);
    }

    @Test
    void wrapKeepsAnglesInTheShortestRange() {
        assertThat(AimSolver.wrap(3 * Math.PI)).isCloseTo(Math.PI, within(1e-9));
        assertThat(AimSolver.wrap(-3 * Math.PI)).isCloseTo(Math.PI, within(1e-9));
        assertThat(AimSolver.wrap(0.5)).isEqualTo(0.5);
        assertThat(AimSolver.wrap(2 * Math.PI + 0.1)).isCloseTo(0.1, within(1e-9));
    }
}
