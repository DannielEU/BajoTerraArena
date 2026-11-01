package com.bajoterra.core.effect;

import static com.bajoterra.core.Fixtures.count;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.sim.GameSimulation;
import org.junit.jupiter.api.Test;

/**
 * Bubbaleone traps its victim in a bubble that floats them up until a hit pops it; Lariat's lasso hauls its victim
 * towards whoever threw it.
 */
class BubbleAndLassoTest {

    private static final double EAST = Math.PI / 2;
    private static final double WEST = -Math.PI / 2;

    private static void shoot(GameSimulation sim, Combatant c) {
        c.input.aimPitch = 0.02;
        c.input.fire = true;
        int shots = c.shotsFired;
        for (int i = 0; i < 120 && c.shotsFired == shots; i++) {
            sim.tick();
        }
        c.input.fire = false;
    }

    private static boolean waitFor(GameSimulation sim, Combatant c, String status) {
        for (int i = 0; i < 90; i++) {
            if (c.statuses.has(status)) {
                return true;
            }
            sim.tick();
        }
        return false;
    }

    @Test
    void aBubbleFloatsItsVictimUpAndHoldsThemThere() {
        GameSimulation sim = scripted("test_open", 3, human(0, "eli", "bubbaleone"), human(1, "eli", "infurnus"));
        startRound(sim);
        Combatant slinger = sim.combatants()[0];
        Combatant victim = sim.combatants()[1];
        place(slinger, -8, 0, EAST);
        place(victim, 4, 0, WEST);
        shoot(sim, slinger);

        assertThat(waitFor(sim, victim, "bubble")).as("trapped").isTrue();
        ticks(sim, 60);
        assertThat(victim.pos.y).as("floating").isBetween(1.2, 2.0);
        assertThat(victim.stats.stunned).as("cannot move or fire").isTrue();
        double held = victim.pos.y;
        ticks(sim, 30);
        assertThat(victim.pos.y).as("hovering, not still climbing")
                .isCloseTo(held, org.assertj.core.data.Offset.offset(0.05));
        assertThat(count(sim, EventType.BUBBLE)).isEqualTo(1);
    }

    @Test
    void aHitPopsTheBubbleAndTheVictimDropsBackDown() {
        GameSimulation sim = scripted("test_open", 3, human(0, "eli", "bubbaleone"), human(0, "trixie", "infurnus"),
                human(1, "eli", "infurnus"));
        startRound(sim);
        Combatant slinger = sim.combatants()[0];
        Combatant friend = sim.combatants()[1];
        Combatant victim = sim.combatants()[2];
        place(slinger, -8, 0, EAST);
        place(friend, -8, 4, EAST);
        place(victim, 4, 0, WEST);
        shoot(sim, slinger);
        assertThat(waitFor(sim, victim, "bubble")).isTrue();
        ticks(sim, 20);

        friend.input.aimYaw = Math.atan2(victim.pos.x - friend.pos.x, victim.pos.z - friend.pos.z);
        friend.input.aimPitch = Math.atan2(victim.centerY() - 1.3, 12.6);
        friend.input.fire = true;
        for (int i = 0; i < 60 && victim.statuses.has("bubble"); i++) {
            sim.tick();
        }
        friend.input.fire = false;

        assertThat(victim.statuses.has("bubble")).as("popped").isFalse();
        ticks(sim, 60);
        assertThat(victim.pos.y).as("back on the floor").isLessThan(0.05);
    }

    @Test
    void theLassoHaulsItsVictimToTheThrower() {
        GameSimulation sim = scripted("test_open", 3, human(0, "eli", "lariat"), human(1, "eli", "infurnus"));
        startRound(sim);
        Combatant thrower = sim.combatants()[0];
        Combatant victim = sim.combatants()[1];
        place(thrower, -10, 0, EAST);
        place(victim, 4, 0, WEST);
        shoot(sim, thrower);

        assertThat(waitFor(sim, victim, "lasso")).as("caught").isTrue();
        assertThat(count(sim, EventType.LASSO)).isEqualTo(1);
        ticks(sim, 60);
        double gap = Math.hypot(victim.pos.x - thrower.pos.x, victim.pos.z - thrower.pos.z);
        assertThat(gap).as("pulled in from 14 m").isBetween(1.0, 5.0);
    }

    @Test
    void theLassoIsShortRanged() {
        GameSimulation sim = scripted("test_open", 3, human(0, "eli", "lariat"), human(1, "eli", "infurnus"));
        startRound(sim);
        Combatant thrower = sim.combatants()[0];
        Combatant victim = sim.combatants()[1];
        place(thrower, -20, 0, EAST);
        place(victim, 20, 0, WEST);
        shoot(sim, thrower);
        ticks(sim, 90);

        assertThat(victim.statuses.has("lasso")).isFalse();
        assertThat(victim.damageTaken).as("it fades out before 40 m").isZero();
    }
}
