package com.bajoterra.core.move;

import static com.bajoterra.core.Fixtures.apply;
import static com.bajoterra.core.Fixtures.effect;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.entity.Posture;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

/** Jump, crouch, roll and dash, all produced by the same data-driven move machinery. */
class LocomotionTest {

    private static final double FACING_PLUS_X = Math.PI / 2;

    private final GameSimulation sim = scripted("test_open", 9,
            human(0, "eli", "flaringo"),
            human(1, "pronto", "flaringo"));
    private Combatant eli;
    private Combatant pronto;

    private void begin() {
        startRound(sim);
        eli = sim.combatants()[0];
        pronto = sim.combatants()[1];
        place(eli, -10, 0, FACING_PLUS_X);
        place(pronto, 10, 5, -FACING_PLUS_X);
        eli.input.aimYaw = FACING_PLUS_X;
        pronto.input.aimYaw = -FACING_PLUS_X;
    }

    private int slot(Combatant c, String moveId) {
        for (int i = 0; i < c.character.moves().size(); i++) {
            if (c.character.moves().get(i).id().equals(moveId)) {
                return i;
            }
        }
        throw new AssertionError("no move " + moveId);
    }

    private void press(Combatant c, String moveId) {
        c.input.moveMask = 1 << slot(c, moveId);
    }

    @Test
    void jumpLiftsTheCombatantAndLandsAgain() {
        begin();
        press(eli, "jump");
        double apex = 0;
        boolean leftTheGround = false;
        for (int tick = 0; tick < 90; tick++) {
            sim.tick();
            eli.input.moveMask = 0;
            apex = Math.max(apex, eli.pos.y);
            leftTheGround |= !eli.grounded;
        }

        assertThat(leftTheGround).isTrue();
        assertThat(apex).isBetween(1.2, 1.6);
        assertThat(eli.pos.y).isZero();
        assertThat(eli.grounded).isTrue();
        assertThat(eli.posture).isEqualTo(Posture.GROUNDED);
    }

    @Test
    void postureIsAirborneWhileJumping() {
        begin();
        press(eli, "jump");
        sim.tick();
        eli.input.moveMask = 0;
        ticks(sim, 10);

        assertThat(eli.posture).isEqualTo(Posture.AIRBORNE);
    }

    @Test
    void cannotJumpWhileAlreadyInTheAir() {
        begin();
        press(eli, "jump");
        sim.tick();
        eli.input.moveMask = 0;
        ticks(sim, 12);
        double heightMidAir = eli.pos.y;
        press(eli, "jump");
        ticks(sim, 3);

        // A second jump would have added 8.5 m/s of upward speed; the combatant keeps falling or rising normally.
        assertThat(eli.vel.y).isLessThan(8.0);
        assertThat(heightMidAir).isGreaterThan(0.5);
    }

    @Test
    void crouchShrinksTheHitboxAndSlowsWalkingWhileHeld() {
        begin();
        double standingHeight = eli.hitHeight();
        press(eli, "crouch");
        eli.input.moveZ = 1;
        double startX = eli.pos.x;
        ticks(sim, 60);

        assertThat(eli.posture).isEqualTo(Posture.CROUCHING);
        assertThat(eli.hitHeight()).isCloseTo(standingHeight * 0.6, within(1e-9));
        assertThat(eli.pos.x - startX).isCloseTo(3.0, within(0.3)); // 6 m/s * 0.5

        eli.input.moveMask = 0;
        ticks(sim, 2);
        assertThat(eli.posture).isEqualTo(Posture.GROUNDED);
        assertThat(eli.hitHeight()).isCloseTo(standingHeight, within(1e-9));
    }

    @Test
    void walkingSpeedMatchesTheCharacter() {
        begin();
        eli.input.moveZ = 1;
        double startX = eli.pos.x;

        ticks(sim, 60);

        assertThat(eli.pos.x - startX).isCloseTo(6.0, within(0.2));
    }

    @Test
    void strafingMovesSideways() {
        begin();
        eli.input.moveX = 1; // right of +X is -Z
        double startZ = eli.pos.z;

        ticks(sim, 30);

        assertThat(eli.pos.z - startZ).isCloseTo(-3.0, within(0.2));
        assertThat(eli.pos.x).isCloseTo(-10, within(0.01));
    }

    @Test
    void rollCoversDistanceGivesInvulnerabilityAndBlocksFiring() {
        begin();
        press(eli, "roll");
        eli.input.moveZ = 1;
        double startX = eli.pos.x;
        sim.tick();
        eli.input.moveMask = 0;
        eli.input.moveZ = 0; // the roll keeps its direction without further input
        eli.input.fire = true;

        // Early in the roll the combatant ignores damage.
        ticks(sim, 5);
        assertThat(eli.posture).isEqualTo(Posture.ACTION);
        assertThat(sim.arena().damage(pronto, eli, 50, Element.NEUTRAL, 0)).isZero();
        assertThat(eli.shotsFired).as("cannot fire while rolling").isZero();

        ticks(sim, 40); // the roll lasts 30 ticks
        assertThat(eli.pos.x - startX).isCloseTo(4.0, within(0.5));
        assertThat(eli.posture).isNotEqualTo(Posture.ACTION);
        assertThat(sim.arena().damage(pronto, eli, 10, Element.NEUTRAL, 0)).isEqualTo(10);
        assertThat(eli.shotsFired).as("firing resumes after the roll").isGreaterThan(0);
    }

    @Test
    void rollingIgnoresSteeringInputAndKeepsItsDirection() {
        begin();
        press(eli, "roll");
        eli.input.moveZ = 1;
        sim.tick();
        eli.input.moveMask = 0;
        eli.input.moveZ = 0;
        eli.input.moveX = 1; // tries to steer sideways mid-roll

        ticks(sim, 20);

        assertThat(eli.pos.z).isCloseTo(0, within(0.01));
        assertThat(eli.pos.x).isGreaterThan(-8);
    }

    @Test
    void rollHasACooldown() {
        begin();
        press(eli, "roll");
        sim.tick();
        eli.input.moveMask = 0;
        ticks(sim, 40); // roll finished, still cooling down (72 ticks)
        double afterFirst = eli.pos.x;

        press(eli, "roll");
        ticks(sim, 5);
        eli.input.moveMask = 0;
        assertThat(eli.posture).isNotEqualTo(Posture.ACTION);
        assertThat(eli.pos.x).isCloseTo(afterFirst, within(0.01));

        ticks(sim, 40);
        press(eli, "roll");
        ticks(sim, 3);
        assertThat(eli.posture).isEqualTo(Posture.ACTION);
    }

    @Test
    void rollCanStartInTheAir() {
        begin();
        press(eli, "jump");
        sim.tick();
        eli.input.moveMask = 0;
        ticks(sim, 10);
        press(eli, "roll");
        ticks(sim, 3);

        assertThat(eli.grounded).isFalse();
        assertThat(eli.posture).isEqualTo(Posture.ACTION);
        assertThat(eli.moves.active().id()).isEqualTo("roll");
    }

    /** The crouch key presses slide and crouch together, as the client does. */
    private void pressCrouchKey(Combatant c) {
        c.input.moveMask = (1 << slot(c, "slide")) | (1 << slot(c, "crouch"));
    }

    @Test
    void crouchingWhileRunningSlidesFasterThanRunningAndCanFire() {
        begin();
        eli.input.moveZ = 1;
        ticks(sim, 10);
        double startX = eli.pos.x;
        pressCrouchKey(eli);
        eli.input.fire = true;
        ticks(sim, 30);

        assertThat(eli.moves.active().id()).isEqualTo("slide");
        assertThat(eli.stats.hitboxHeightMultiplier).isCloseTo(0.6, within(1e-9));
        assertThat(eli.pos.x - startX).as("further than 30 ticks of running (3 m)").isGreaterThan(4.0);
        assertThat(eli.shotsFired).as("can fire while sliding").isPositive();

        ticks(sim, 15); // the slide lasts 36 ticks, then the held key keeps it crouched
        assertThat(eli.moves.active().id()).isEqualTo("crouch");
    }

    @Test
    void crouchingStandingStillDoesNotSlide() {
        begin();
        pressCrouchKey(eli);
        ticks(sim, 5);

        assertThat(eli.moves.active().id()).isEqualTo("crouch");
        assertThat(eli.pos.x).isCloseTo(-10, within(0.01));
    }

    @Test
    void startingToRunWhileCrouchedDoesNotSlide() {
        begin();
        pressCrouchKey(eli);
        ticks(sim, 5);
        eli.input.moveZ = 1;
        ticks(sim, 5);

        assertThat(eli.moves.active().id()).isEqualTo("crouch");
    }

    @Test
    void rollCanStartFromACrouch() {
        begin();
        press(eli, "crouch");
        ticks(sim, 3);
        assertThat(eli.posture).isEqualTo(Posture.CROUCHING);

        eli.input.moveMask = (1 << slot(eli, "crouch")) | (1 << slot(eli, "roll"));
        ticks(sim, 3);

        assertThat(eli.posture).isEqualTo(Posture.ACTION);
    }

    @Test
    void dashIsJustAnotherMoveInAnotherCharactersMoveSet() {
        begin();
        place(pronto, -10, 5, FACING_PLUS_X);
        pronto.input.aimYaw = FACING_PLUS_X;
        press(pronto, "dash");
        pronto.input.moveZ = 1;
        double startX = pronto.pos.x;
        sim.tick();
        pronto.input.moveMask = 0;

        ticks(sim, 20);

        assertThat(pronto.pos.x - startX).isCloseTo(3.2 + 0.0, within(1.5)); // 16 m/s for 0.2 s
        assertThat(eli.character.moves()).extracting(MoveDefinition::id).doesNotContain("dash");
        assertThat(pronto.character.moves()).extracting(MoveDefinition::id).contains("dash");
    }

    @Test
    void stunCancelsTheActiveMove() {
        begin();
        press(eli, "roll");
        sim.tick();
        eli.input.moveMask = 0;
        ticks(sim, 3);
        assertThat(eli.posture).isEqualTo(Posture.ACTION);

        apply(sim.arena(), effect("{\"type\":\"stun\",\"durationTicks\":60}"), pronto, eli);
        ticks(sim, 2);

        assertThat(eli.posture).isNotEqualTo(Posture.ACTION);
    }

    @Test
    void slowedCombatantsWalkSlower() {
        begin();
        apply(sim.arena(), effect("{\"type\":\"slow\",\"factor\":0.5,\"durationTicks\":120}"), pronto, eli);
        eli.input.moveZ = 1;
        double startX = eli.pos.x;

        ticks(sim, 60);

        assertThat(eli.pos.x - startX).isCloseTo(3.0, within(0.3));
    }

    @Test
    void knockbackPushesAndDecays() {
        begin();
        sim.arena().knockback(eli, 1, 0, 12, 0);
        double startX = eli.pos.x;

        ticks(sim, 60);

        assertThat(eli.pos.x - startX).isBetween(2.0, 3.0); // 12 m/s decaying by 8 % per tick
    }
}
