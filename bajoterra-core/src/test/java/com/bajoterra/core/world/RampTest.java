package com.bajoterra.core.world;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.InputFrame;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import java.util.List;
import org.junit.jupiter.api.Test;

/** Ramps are walked up and down smoothly, and slugs and sight lines hit their sloped surface. */
class RampTest {

    private static final double QUARTER = Math.PI / 2;

    private static GameMap map() {
        return CONTENT.maps().require("test_ramps");
    }

    private static Obstacle ramp(String id) {
        for (Obstacle o : map().obstacleList()) {
            if (o.id().equals(id)) {
                return o;
            }
        }
        throw new AssertionError(id);
    }

    // ------------------------------------------------------------------------------------------ geometry

    @Test
    void theSurfaceClimbsLinearlyAlongTheRiseDirection() {
        Obstacle up = ramp("ramp_up");
        assertThat(up.isRamp()).isTrue();
        assertThat(up.slope()).isEqualTo(0.4, within(1e-12));
        assertThat(up.topAt(0, 0)).isEqualTo(0, within(1e-12));
        assertThat(up.topAt(2, 0)).isEqualTo(0.8, within(1e-12));
        assertThat(up.topAt(4, 1)).isEqualTo(1.6, within(1e-12));

        Obstacle down = ramp("ramp_down");
        assertThat(down.topAt(8, 0)).isEqualTo(1.6, within(1e-12));
        assertThat(down.topAt(10, 0)).isEqualTo(0.8, within(1e-12));
        assertThat(down.topAt(12, 0)).isEqualTo(0, within(1e-12));

        Obstacle side = ramp("side_ramp");
        assertThat(side.topAt(-10, 6)).isEqualTo(0, within(1e-12));
        assertThat(side.topAt(-10, 9)).isEqualTo(1.0, within(1e-12));
    }

    @Test
    void aPlainBoxIsFlatAndTheGradientIsZero() {
        Obstacle plateau = ramp("plateau");
        assertThat(plateau.isRamp()).isFalse();
        assertThat(plateau.topAt(5, 0)).isEqualTo(1.6);
        assertThat(plateau.slope()).isZero();
    }

    // ------------------------------------------------------------------------------------------ walking

    @Test
    void aCombatantWalksUpTheRampOntoThePlateauAndDownTheOtherSide() {
        GameSimulation sim = round();
        Combatant c = sim.combatants()[0];
        place(c, -3, 0, QUARTER);

        double maxY = 0;
        int airborneTicks = 0;
        for (int tick = 1; tick <= 240; tick++) {
            walk(sim, tick);
            maxY = Math.max(maxY, c.pos.y);
            boolean nearJoint = isNearJoint(c.pos.x);
            if (!c.grounded && c.pos.x > 0.3 && c.pos.x < 11.7) {
                airborneTicks++;
            }
            if (!nearJoint && c.pos.x > 0.3 && c.pos.x < 11.7) {
                assertThat(c.pos.y).as("feet follow the surface at x=%s", c.pos.x)
                        .isEqualTo(surfaceHeight(c.pos.x), within(0.03));
            }
        }

        assertThat(maxY).isEqualTo(1.6, within(0.01));
        assertThat(airborneTicks).as("never floats off the slope").isZero();
        assertThat(c.pos.x).isGreaterThan(12.0);
        assertThat(c.pos.y).isEqualTo(0, within(1e-9));
        assertThat(c.grounded).isTrue();
    }

    @Test
    void walkingOffAHighLedgeStillFalls() {
        GameSimulation sim = round();
        Combatant c = sim.combatants()[0];
        // Stand on the plateau and face its open side (north, +z): the edge drops 1.6 m, far more than a snap.
        place(c, 6, 1.7, 0);
        c.pos.y = 1.6;
        c.grounded = true;
        boolean airborne = false;
        for (int tick = 1; tick <= 30; tick++) {
            sim.submit(new InputFrame(0, tick, 0f, 1f, 0f, 0f, false, 0f, 0f, 0f, false, 0, -1, 0));
            sim.tick();
            airborne |= !c.grounded;
        }
        assertThat(airborne).isTrue();
        assertThat(c.pos.y).isEqualTo(0, within(1e-9));
    }

    @Test
    void theHighEndOfARampIsAWallFromTheGround() {
        GameSimulation sim = round();
        Combatant c = sim.combatants()[0];
        // Walk at the plateau's flank from the north side of the map: nothing climbs the 1.6 m box.
        place(c, 6, 5, 0);
        for (int tick = 1; tick <= 120; tick++) {
            sim.submit(new InputFrame(0, tick, 0f, -1f, 0f, 0f, false, 0f, 0f, 0f, false, 0, -1, 0));
            sim.tick();
        }
        assertThat(c.pos.z).isGreaterThan(2.0);
        assertThat(c.pos.y).isZero();
    }

    @Test
    void theLowEdgeOfARampIsWalkedOverFromTheSideButTheTallEndIsNot() {
        GameSimulation sim = round();
        Combatant c = sim.combatants()[0];
        // side_ramp rises towards +z: from the low (z = 6) side the body walks up it.
        place(c, -10, 4, 0);
        for (int tick = 1; tick <= 45; tick++) {
            sim.submit(new InputFrame(0, tick, 0f, 1f, 0f, 0f, false, 0f, 0f, 0f, false, 0, -1, 0));
            sim.tick();
        }
        assertThat(c.pos.y).as("standing near the top of the 1 m ramp").isGreaterThan(0.7);
        // Coming along its flank at the tall end is blocked by the side wall.
        place(c, -14, 8.5, QUARTER);
        for (int tick = 1; tick <= 60; tick++) {
            sim.submit(new InputFrame(0, 100 + tick, 0f, 1f, (float) QUARTER, 0f, false, 0f, 0f, 0f, false, 0, -1,
                    0));
            sim.tick();
        }
        assertThat(c.pos.x).as("stopped by the flank").isLessThan(-12.0);
    }

    // ------------------------------------------------------------------------------------------ projectiles

    @Test
    void aFlatSlugHitsTheSlopeWhereItsHeightIsReached() {
        World world = new World(map());
        // A slug at 0.8 m reaches the ramp surface (0.4 m per metre) 2 m in.
        double t = world.sweepProjectile(-2, 0.8, 0, 10, 0, 0, 0);
        assertThat(-2 + t * 10).isEqualTo(2.0, within(1e-9));
        // At 1.2 m it climbs to 3 m before meeting the surface.
        t = world.sweepProjectile(-2, 1.2, 0, 10, 0, 0, 0);
        assertThat(-2 + t * 10).isEqualTo(3.0, within(1e-9));
    }

    @Test
    void aSlugAboveThePlateauFliesOverTheWholeStructure() {
        World world = new World(map());
        assertThat(world.sweepProjectile(-2, 1.9, 0, 20, 0, 0, 0)).isLessThan(0);
    }

    @Test
    void aBiggerSlugTouchesTheSlopeSooner() {
        World world = new World(map());
        double thin = world.sweepProjectile(-2, 0.8, 0, 10, 0, 0, 0);
        double fat = world.sweepProjectile(-2, 0.8, 0, 10, 0, 0, 0.3);
        assertThat(fat).isLessThan(thin);
    }

    @Test
    void aSlugFallingOntoTheSlopeLandsOnIt() {
        World world = new World(map());
        // From 3 m above x = 2 straight down: the surface there is 0.8 m high.
        double t = world.sweepProjectile(2, 3, 0, 0, -3, 0, 0);
        assertThat(3 - t * 3).isEqualTo(0.8, within(1e-9));
    }

    @Test
    void theDownSlopeBlocksFromTheOtherDirectionToo() {
        World world = new World(map());
        // Coming from the east at 0.8 m: the down ramp's surface is 0.8 m high at x = 10.
        double t = world.sweepProjectile(14, 0.8, 0, -10, 0, 0, 0);
        assertThat(14 - t * 10).isEqualTo(10.0, within(1e-9));
    }

    @Test
    void sightIsBlockedByTheRampButNotByTheGroundBeside() {
        World world = new World(map());
        assertThat(world.hasLineOfSight(-2, 0.8, 0, 3, 0.8, 0)).isFalse();
        assertThat(world.hasLineOfSight(-2, 0.8, 5, 3, 0.8, 5)).isTrue();
        assertThat(world.hasLineOfSight(-2, 2.0, 0, 14, 2.0, 0)).isTrue();
    }

    // ------------------------------------------------------------------------------------------ helpers

    private static GameSimulation round() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("test_ramps").seed(7)
                .slot(SlotConfig.human(0, "eli", List.of())).build());
        sim.start();
        while (sim.match().phase() != MatchPhase.ROUND_ACTIVE) {
            sim.tick();
        }
        // Everybody else stands still and silent.
        for (Combatant c : sim.combatants()) {
            c.brain = null;
            c.input.reset();
        }
        sim.projectiles().releaseAll();
        return sim;
    }

    private static void place(Combatant c, double x, double z, double yaw) {
        c.pos.set(x, 0, z);
        c.vel.setZero();
        c.knock.setZero();
        c.yaw = yaw;
        c.grounded = true;
    }

    /** Walk forward along +x (yaw a quarter turn). */
    private static void walk(GameSimulation sim, int sequence) {
        sim.submit(new InputFrame(0, sequence, 0f, 1f, (float) QUARTER, 0f, false, 0f, 0f, 0f, false, 0, -1, 0));
        sim.tick();
    }

    /** Height of the surface on the centre line (z = 0) at x. */
    private static double surfaceHeight(double x) {
        double height = 0;
        for (Obstacle o : map().obstacleList()) {
            if (o.blocksMovement() && o.containsXZ(x, 0, 0)) {
                height = Math.max(height, o.topAt(x, 0));
            }
        }
        return height;
    }

    /** True within a body's reach of an edge between ramp, plateau and ground, where several surfaces overlap. */
    private static boolean isNearJoint(double x) {
        for (double joint : new double[] {0, 4, 8, 12}) {
            if (Math.abs(x - joint) < 0.3) {
                return true;
            }
        }
        return false;
    }
}
