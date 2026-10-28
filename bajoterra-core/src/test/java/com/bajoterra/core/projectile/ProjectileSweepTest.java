package com.bajoterra.core.projectile;

import static com.bajoterra.core.Fixtures.combatant;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.math.Vec3;
import org.junit.jupiter.api.Test;

/** Geometry of the swept sphere against a combatant's capsule. */
class ProjectileSweepTest {

    private static final double SLUG_RADIUS = 0.25;

    private final Combatant target = combatant(0, 1, "eli", "doc"); // radius 0.4, height 1.8, at the origin

    @Test
    void aHeadOnShotHitsTheNearSurface() {
        double t = ProjectileSystem.sweepAgainst(target, new Vec3(-5, 1, 0), 10, 0, 0, SLUG_RADIUS);

        assertThat(t).isCloseTo((5 - 0.4 - SLUG_RADIUS) / 10, within(1e-9));
    }

    @Test
    void aSideStepMisses() {
        assertThat(ProjectileSystem.sweepAgainst(target, new Vec3(-5, 1, 2), 10, 0, 0, SLUG_RADIUS)).isNegative();
    }

    @Test
    void aShotAboveTheHeadMisses() {
        assertThat(ProjectileSystem.sweepAgainst(target, new Vec3(-5, 2.5, 0), 10, 0, 0, SLUG_RADIUS)).isNegative();
    }

    @Test
    void aShotThatFallsShortMisses() {
        assertThat(ProjectileSystem.sweepAgainst(target, new Vec3(-5, 1, 0), 3, 0, 0, SLUG_RADIUS)).isNegative();
    }

    @Test
    void startingInsideTheHitboxHitsImmediately() {
        assertThat(ProjectileSystem.sweepAgainst(target, new Vec3(0.1, 1, 0), 2, 0, 0, SLUG_RADIUS)).isZero();
    }

    @Test
    void aSlugFallingOntoTheHeadHitsTheTop() {
        double t = ProjectileSystem.sweepAgainst(target, new Vec3(0, 3, 0), 0, -2, 0, SLUG_RADIUS);

        assertThat(t).isCloseTo((3 - 1.8 - SLUG_RADIUS) / 2, within(1e-9));
    }

    @Test
    void aFastSlugCannotTunnelThroughTheBody() {
        // 200 m/s is 3.3 m per tick, far more than the body is wide.
        double t = ProjectileSystem.sweepAgainst(target, new Vec3(-1.5, 1, 0), 3.3, 0, 0, SLUG_RADIUS);

        assertThat(t).isBetween(0.0, 1.0);
    }

    @Test
    void crouchingShrinksTheTarget() {
        target.stats.hitboxHeightMultiplier = 0.5; // 0.9 m tall

        assertThat(ProjectileSystem.sweepAgainst(target, new Vec3(-5, 1.5, 0), 10, 0, 0, SLUG_RADIUS)).isNegative();
        assertThat(ProjectileSystem.sweepAgainst(target, new Vec3(-5, 0.5, 0), 10, 0, 0, SLUG_RADIUS))
                .isNotNegative();
    }
}
