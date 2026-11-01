package com.bajoterra.core.move;

import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.sim.GameSimulation;
import org.junit.jupiter.api.Test;

/**
 * The wall kick: in the air next to a wall, a fresh press of the button throws the body up and away from it; at most
 * two before landing, and never twice in a row off the same wall.
 */
class WallKickTest {

    private static final double WEST = -Math.PI / 2;
    // Eli's move set: jump, crouch, roll, wall_kick.
    private static final int JUMP = 1;
    private static final int KICK = 8;

    private final GameSimulation sim = scripted("test_open", 3, human(0, "eli", "infurnus"),
            human(1, "eli", "infurnus"));
    private final Combatant body = sim.combatants()[0];

    WallKickTest() {
        startRound(sim);
        place(sim.combatants()[1], 15, 5, 0);
    }

    private void press(int mask, int holdTicks) {
        body.input.moveMask = mask;
        ticks(sim, holdTicks);
        body.input.moveMask = 0;
    }

    private void jumpAgainstTheWestEdge(double z) {
        place(body, -19.45, z, WEST);
        ticks(sim, 2);
        press(JUMP, 1);
        ticks(sim, 14);
    }

    @Test
    void aFreshPressNextToAWallKicksTheBodyUpAndAwayFromIt() {
        jumpAgainstTheWestEdge(0);
        assertThat(body.grounded).isFalse();
        assertThat(body.wallNormalX).as("the west edge is within reach").isEqualTo(1.0);
        double fallingAt = body.vel.y;

        press(KICK, 1);

        assertThat(body.wallKicks).isEqualTo(1);
        assertThat(body.vel.y).as("thrown back up").isGreaterThan(fallingAt + 4);
        double highest = body.pos.y;
        for (int i = 0; i < 40; i++) {
            sim.tick();
            highest = Math.max(highest, body.pos.y);
        }
        assertThat(highest).as("higher than a plain jump reaches").isGreaterThan(2.0);
        assertThat(body.pos.x).as("pushed away from the wall").isGreaterThan(-18.2);
    }

    @Test
    void holdingTheButtonFromTheJumpNeverKicks() {
        place(body, -19.45, 0, WEST);
        ticks(sim, 2);
        press(JUMP | KICK, 40);

        assertThat(body.wallKicks).isZero();
    }

    @Test
    void theSameWallCannotBeKickedOffTwiceInARow() {
        jumpAgainstTheWestEdge(0);
        press(KICK, 1);
        ticks(sim, 12);
        // Drift back to the wall and try again.
        body.pos.x = -19.5;
        ticks(sim, 1);
        press(KICK, 1);

        assertThat(body.wallKicks).isEqualTo(1);
    }

    @Test
    void inACornerTwoKicksChainOffTwoWallsAndAThirdIsRefused() {
        jumpAgainstTheWestEdge(-9.1);
        press(KICK, 1);
        assertThat(body.wallKicks).isEqualTo(1);
        assertThat(body.lastKickX).as("off the west edge first").isEqualTo(1.0);
        ticks(sim, 12);
        body.pos.x = -18.0;
        body.pos.z = -9.5;
        ticks(sim, 1);
        assertThat(body.wallNormalZ).as("the south edge is within reach").isEqualTo(1.0);
        press(KICK, 1);
        assertThat(body.wallKicks).isEqualTo(2);
        ticks(sim, 12);
        body.pos.x = -19.5;
        ticks(sim, 1);
        press(KICK, 1);

        assertThat(body.wallKicks).as("two at most before landing").isEqualTo(2);
        for (int i = 0; i < 200 && !body.grounded; i++) {
            sim.tick();
        }
        assertThat(body.grounded).isTrue();
        ticks(sim, 1);
        assertThat(body.wallKicks).as("landing resets the count").isZero();
    }

    @Test
    void thereIsNothingToKickOffInTheOpen() {
        place(body, 0, 0, WEST);
        ticks(sim, 2);
        press(JUMP, 1);
        ticks(sim, 14);
        press(KICK, 1);

        assertThat(body.wallKicks).isZero();
    }
}
