package com.bajoterra.core.hazard;

import static com.bajoterra.core.Fixtures.CONTENT;
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
 * Arachnet wraps whoever it hits in a web (they cannot walk, jump or roll, but can still shoot) and, when it lands on
 * the floor or a wall, spins a web trap that catches the first enemy to step on it. Fire burns webs away.
 */
class WebTest {

    private static final double EAST = Math.PI / 2;
    private static final double WEST = -Math.PI / 2;

    private final GameSimulation sim = scripted("test_open", 4,
            human(0, "eli", "arachnet"), human(1, "eli", "infurnus"));
    private final Combatant slinger = sim.combatants()[0];
    private final Combatant enemy = sim.combatants()[1];

    WebTest() {
        startRound(sim);
        place(slinger, -12, 0, EAST);
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

    private Web spun() {
        for (Web w : sim.webs().webs()) {
            if (w.active) {
                return w;
            }
        }
        return null;
    }

    @Test
    void aDirectHitWrapsTheVictimWhoCannotMoveButCanStillShoot() {
        shoot(sim, slinger, 0.03);
        for (int i = 0; i < 40 && !enemy.statuses.has("ensnare"); i++) {
            sim.tick();
        }
        assertThat(enemy.statuses.has("ensnare")).isTrue();
        double x = enemy.pos.x;
        enemy.input.moveZ = 1;
        enemy.input.moveMask = 1;
        ticks(sim, 30);
        enemy.input.moveZ = 0;
        enemy.input.moveMask = 0;

        assertThat(enemy.pos.x).as("rooted to the spot").isEqualTo(x);
        int shots = enemy.shotsFired;
        shoot(sim, enemy, 0);
        assertThat(enemy.shotsFired).as("still shoots").isEqualTo(shots + 1);
    }

    @Test
    void aSlugThatMissesSpinsAWebOnTheFloorThatCatchesTheFirstEnemyToStepOnIt() {
        place(enemy, 10, 6, WEST);
        shoot(sim, slinger, -0.25);
        ticks(sim, 60);
        Web web = spun();
        assertThat(web).as("a web on the floor").isNotNull();
        assertThat(web.y).isEqualTo(0);
        assertThat(count(sim, EventType.WEB_SPUN)).isEqualTo(1);

        place(enemy, web.x + 4, web.z, WEST);
        enemy.input.moveZ = 1;
        for (int i = 0; i < 120 && spun() != null; i++) {
            sim.tick();
        }

        assertThat(spun()).as("spent").isNull();
        assertThat(enemy.statuses.has("ensnare")).isTrue();
    }

    @Test
    void theSlingerWalksOverTheirOwnWeb() {
        Web web = sim.webs().spin(slinger, CONTENT.slugs().require("arachnet"), webSpec(), -12, 0, 0);
        ticks(sim, 10);

        assertThat(web.active).isTrue();
        assertThat(slinger.statuses.has("ensnare")).isFalse();
    }

    @Test
    void fireBurnsAWebAway() {
        Web web = sim.webs().spin(slinger, CONTENT.slugs().require("arachnet"), webSpec(), 5, 0, 5);
        sim.hazards().lay(enemy, enemy.loadout.slug(0), 5.3, 0, 5);
        sim.tick();

        assertThat(web.active).isFalse();
        assertThat(count(sim, EventType.WEB_GONE)).isEqualTo(1);
    }

    @Test
    void aSlingerKeepsAtMostThreeWebs() {
        for (int i = 0; i < 5; i++) {
            sim.webs().spin(slinger, CONTENT.slugs().require("arachnet"), webSpec(), 4 * i - 8, 0, 6);
        }
        int active = 0;
        for (Web w : sim.webs().webs()) {
            active += w.active ? 1 : 0;
        }

        assertThat(active).isEqualTo(WebSystem.PER_OWNER);
    }

    private static com.bajoterra.core.slug.WebSpec webSpec() {
        return new com.bajoterra.core.slug.WebSpec(1.5, 600, com.bajoterra.core.Fixtures.effect(
                "{\"type\": \"ensnare\", \"durationSeconds\": 2}"));
    }
}
