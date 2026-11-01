package com.bajoterra.core.hazard;

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
 * Tormato turns into a tornado where it lands: it drags enemies in and lifts them, throws enemy slugs off course and
 * launches its slinger's team into the air (a tornado jump).
 */
class TornadoTest {

    private static final double EAST = Math.PI / 2;
    private static final double WEST = -Math.PI / 2;

    // Team order: the slinger and its ally on team 0, the enemy on team 1.
    private final GameSimulation sim = scripted("test_open", 3,
            human(0, "eli", "tormato"), human(0, "trixie", "doc"), human(1, "eli", "infurnus"));
    private final GameSimulation icy = scripted("test_open", 3,
            human(0, "eli", "tormato"), human(0, "trixie", "frostfang"), human(1, "eli", "frostfang"));
    private final Combatant slinger = sim.combatants()[0];
    private final Combatant ally = sim.combatants()[1];
    private final Combatant enemy = sim.combatants()[2];

    TornadoTest() {
        startRound(sim);
        place(slinger, -12, 0, EAST);
        place(ally, -12, 6, EAST);
        place(enemy, 0, 0, WEST);
    }

    private static void shoot(GameSimulation sim, Combatant c, double pitch) {
        c.input.aimPitch = pitch;
        c.input.fire = true;
        int shots = c.shotsFired;
        for (int i = 0; i < 120 && c.shotsFired == shots; i++) {
            sim.tick();
        }
        c.input.fire = false;
    }

    private Tornado spinning() {
        for (Tornado t : sim.tornadoes().tornadoes()) {
            if (t.active) {
                return t;
            }
        }
        return null;
    }

    @Test
    void aDirectHitCatchesTheVictimInATornadoThatLiftsAndHurtsThem() {
        shoot(sim, slinger, 0.02);
        for (int i = 0; i < 60 && spinning() == null; i++) {
            sim.tick();
        }
        assertThat(spinning()).as("a tornado started").isNotNull();
        assertThat(count(sim, EventType.TORNADO)).isEqualTo(1);
        double hurt = enemy.damageTaken;
        double highest = 0;
        for (int i = 0; i < 90; i++) {
            sim.tick();
            highest = Math.max(highest, enemy.pos.y);
        }

        assertThat(highest).as("lifted off the floor").isGreaterThan(1.0);
        assertThat(enemy.damageTaken).as("hurt while spinning").isGreaterThan(hurt + 6);
    }

    @Test
    void theWindDragsAnEnemyStandingNearbyIntoTheEye() {
        Tornado t = sim.tornadoes().spawn(slinger, slinger.loadout.slug(0), tornadoSpec(), 0, 0, 0, 0, 0);
        place(enemy, 2.8, 0, WEST);
        ticks(sim, 30);

        assertThat(Math.hypot(enemy.pos.x - t.x, enemy.pos.z - t.z)).as("pulled in").isLessThan(2.0);
    }

    @Test
    void anEnemySlugFlyingThroughTheTornadoIsThrownOffCourse() {
        sim.tornadoes().spawn(slinger, slinger.loadout.slug(0), tornadoSpec(), -6, 0, 0, 0, 0);
        place(enemy, 4, 0, WEST);
        double before = slinger.damageTaken;
        shoot(sim, enemy, 0.0);
        ticks(sim, 90);

        assertThat(count(sim, EventType.DEFLECTED)).isEqualTo(1);
        assertThat(slinger.damageTaken).as("the slug behind the tornado never arrives").isEqualTo(before);
    }

    @Test
    void ownSlugsFlyThroughTheirOwnTornado() {
        sim.tornadoes().spawn(slinger, slinger.loadout.slug(0), tornadoSpec(), -6, 0, 0, 0, 0);
        shoot(sim, slinger, 0.02);
        ticks(sim, 40);

        assertThat(count(sim, EventType.DEFLECTED)).isZero();
    }

    @Test
    void steppingIntoAnAlliedTornadoLaunchesYouOnceIntoTheAir() {
        sim.tornadoes().spawn(slinger, slinger.loadout.slug(0), tornadoSpec(), -12, 0, 6, 0, 0);
        double highest = 0;
        for (int i = 0; i < 60; i++) {
            sim.tick();
            highest = Math.max(highest, ally.pos.y);
        }

        assertThat(highest).as("a tornado jump clears a balcony").isGreaterThan(2.5);
        assertThat(ally.damageTaken).isZero();
        ticks(sim, 30);
        assertThat(ally.pos.y).as("only once: back on the floor").isLessThan(0.1);
    }

    @Test
    void aTornadoThatSweepsOverAWallOfFireCatchesFire() {
        Tornado t = sim.tornadoes().spawn(slinger, slinger.loadout.slug(0), tornadoSpec(), 0, 0, -6, 0, 0);
        sim.hazards().lay(enemy, enemy.loadout.slug(0), 0, 0, -6);
        sim.tick();

        assertThat(t.flaming).isTrue();
    }

    @Test
    void anIceSlugFiredIntoATornadoIsSwallowedAndTurnsItIntoAFreezingTornado() {
        startRound(icy);
        Combatant owner = icy.combatants()[0];
        Combatant rival = icy.combatants()[2];
        place(owner, -12, 0, EAST);
        place(icy.combatants()[1], -12, 6, EAST);
        place(rival, 4, 0, WEST);
        Tornado t = icy.tornadoes().spawn(owner, owner.loadout.slug(0), tornadoSpec(), -6, 0, 0, 0, 0);
        shoot(icy, rival, 0.0);
        ticks(icy, 60);

        assertThat(t.freezing).isTrue();
        assertThat(owner.damageTaken).as("the ice slug never comes out of it").isZero();
        assertThat(count(icy, EventType.DEFLECTED)).as("swallowed, not thrown off course").isZero();
    }

    @Test
    void anAlliedIceSlugTurnsTheTornadoFreezingAndItFreezesTheEnemyInsideIt() {
        startRound(icy);
        Combatant owner = icy.combatants()[0];
        Combatant friend = icy.combatants()[1];
        Combatant rival = icy.combatants()[2];
        place(owner, -12, 0, EAST);
        place(friend, -12, 0.5, EAST);
        place(rival, 0, 0, WEST);
        shoot(icy, owner, 0.02);
        Tornado t = null;
        for (int i = 0; i < 60 && t == null; i++) {
            icy.tick();
            for (Tornado candidate : icy.tornadoes().tornadoes()) {
                t = candidate.active ? candidate : t;
            }
        }
        assertThat(t).as("the tornado caught the rival").isNotNull();
        friend.yaw = EAST;
        shoot(icy, friend, 0.06);
        boolean frozen = false;
        for (int i = 0; i < 90 && !frozen; i++) {
            icy.tick();
            frozen = rival.statuses.has("freeze");
        }

        assertThat(t.freezing).isTrue();
        assertThat(frozen).as("frozen in the eye").isTrue();
    }

    @Test
    void fireMeltsAFreezingTornadoIntoAFireTornado() {
        Tornado t = sim.tornadoes().spawn(slinger, slinger.loadout.slug(0), tornadoSpec(), 0, 0, -6, 0, 0);
        sim.tornadoes().freeze(t, slinger);
        sim.hazards().lay(enemy, enemy.loadout.slug(0), 0, 0, -6);
        sim.tick();

        assertThat(t.flaming).isTrue();
        assertThat(t.freezing).isFalse();
    }

    private com.bajoterra.core.slug.TornadoSpec tornadoSpec() {
        return new com.bajoterra.core.slug.TornadoSpec(3.5, 1.2, 4.5, 180, 0, 24, 3, 12.5, 30,
                com.bajoterra.core.effect.Effect.NONE);
    }
}
