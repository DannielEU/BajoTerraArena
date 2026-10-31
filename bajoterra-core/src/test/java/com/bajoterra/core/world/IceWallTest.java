package com.bajoterra.core.world;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.SlugDefinition;
import org.junit.jupiter.api.Test;

/** Frostcrawler's ice wall: cover that stops slugs, bodies and sight until it takes two hits or melts. */
class IceWallTest {

    private static final double EAST = Math.PI / 2;
    private static final SlugDefinition FROSTCRAWLER = CONTENT.slugs().require("frostcrawler");

    private final GameSimulation sim = scripted("test_open", 3,
            human(0, "eli", "frostcrawler"), human(1, "eli", "flaringo"));
    private final Combatant builder = sim.combatants()[0];
    private final Combatant enemy = sim.combatants()[1];

    IceWallTest() {
        startRound(sim);
        place(builder, -10, 0, EAST);
        place(enemy, 12, 0, -EAST);
    }

    /** Fires the selected slug of {@code c} once at the given pitch. */
    private void fire(Combatant c, double pitch) {
        c.input.aimPitch = pitch;
        c.input.fire = true;
        int shots = c.shotsFired;
        for (int i = 0; i < 120 && c.shotsFired == shots; i++) {
            sim.tick();
        }
        c.input.fire = false;
    }

    private Barrier wall() {
        for (Barrier b : sim.world().barriers()) {
            if (b.active) {
                return b;
            }
        }
        return null;
    }

    /** Throws an ice wall at the floor a few metres in front of the builder and waits for it to go up. */
    private Barrier raise() {
        fire(builder, -0.35);
        for (int i = 0; i < 120 && wall() == null; i++) {
            sim.tick();
        }
        return wall();
    }

    private int count(EventType type, int a) {
        int n = 0;
        for (long seq = sim.events().oldestAvailable(); seq < sim.events().written(); seq++) {
            GameEvent e = sim.events().at(seq);
            if (e.type == type && e.a == a) {
                n++;
            }
        }
        return n;
    }

    @Test
    void frostcrawlerIsAnIceSlugThatRaisesAWall() {
        assertThat(FROSTCRAWLER.onWorldHit().raisesWall()).isTrue();
        assertThat(FROSTCRAWLER.onHit().raisesWall()).isTrue();
        assertThat(CONTENT.slugs().require("flaringo").onWorldHit().raisesWall()).isFalse();
    }

    @Test
    void theWallGoesUpOnTheFloorAcrossTheThrowAndStandsBetweenTheTwo() {
        Barrier wall = raise();

        assertThat(wall).isNotNull();
        assertThat(wall.minY).isCloseTo(0, within(1e-9));
        assertThat(wall.maxY).isCloseTo(2.2, within(1e-9));
        assertThat(wall.maxX - wall.minX).as("thin along the throw").isCloseTo(0.5, within(1e-9));
        assertThat(wall.maxZ - wall.minZ).as("wide across it").isCloseTo(3.2, within(1e-9));
        assertThat(wall.minX).isGreaterThan(builder.pos.x).isLessThan(enemy.pos.x);
        assertThat(wall.hitsLeft).isEqualTo(2);
        assertThat(count(EventType.BARRIER_RAISED, FROSTCRAWLER.index())).isEqualTo(1);
    }

    @Test
    void itStopsSlugsAndSightAndBodies() {
        Barrier wall = raise();
        World world = sim.world();
        double midX = (wall.minX + wall.maxX) / 2;

        assertThat(world.hasLineOfSight(builder.pos.x, 1.5, 0, enemy.pos.x, 1.5, 0)).isFalse();
        assertThat(world.hasClearShot(builder.pos.x, 1.5, 0, enemy.pos.x, 1.5, 0, 0.25)).isFalse();
        assertThat(world.hasLineOfSight(builder.pos.x, 3, 0, enemy.pos.x, 3, 0)).as("over the top").isTrue();

        // Walking into it from the enemy's side: the body stops at its face.
        place(enemy, wall.maxX + 2, 0, -EAST);
        enemy.input.moveZ = 1;
        ticks(sim, 90);
        enemy.input.moveZ = 0;
        assertThat(enemy.pos.x).isGreaterThanOrEqualTo(wall.maxX + enemy.hitRadius() - 1e-6);
        assertThat(midX).isLessThan(enemy.pos.x);
    }

    @Test
    void itTakesTwoHitsAndBreaksOnTheSecond() {
        Barrier wall = raise();
        int id = wall.id;
        place(enemy, wall.maxX + 6, 0, -EAST);

        fire(enemy, 0.02);
        ticks(sim, 30);
        assertThat(wall.active).isTrue();
        assertThat(wall.hitsLeft).isEqualTo(1);

        ticks(sim, 40);   // the blaster's wait between shots
        fire(enemy, 0.02);
        ticks(sim, 30);

        assertThat(wall.active && wall.id == id).as("broken").isFalse();
        assertThat(count(EventType.BARRIER_HIT, 0)).isEqualTo(1);
        assertThat(count(EventType.BARRIER_GONE, 1)).as("broken, not melted").isEqualTo(1);
    }

    @Test
    void itMeltsAwayAfterEightSeconds() {
        Barrier wall = raise();
        int left = wall.ticksLeft;
        assertThat(left).isLessThanOrEqualTo(8 * SimulationConstants.TICKS_PER_SECOND);

        ticks(sim, left - 1);
        assertThat(wall.active).isTrue();
        ticks(sim, 2);

        assertThat(wall.active).isFalse();
        assertThat(count(EventType.BARRIER_GONE, 0)).as("melted").isEqualTo(1);
    }

    @Test
    void hittingSomebodyRaisesTheWallInFrontOfThemNotOnThem() {
        place(enemy, -4, 0, -EAST);

        fire(builder, 0.08);
        for (int i = 0; i < 60 && wall() == null; i++) {
            sim.tick();
        }

        Barrier wall = wall();
        assertThat(wall).isNotNull();
        assertThat(wall.maxX).isLessThanOrEqualTo(enemy.pos.x - enemy.hitRadius() + 1e-6);
        assertThat(wall.containsXZ(enemy.pos.x, enemy.pos.z, 0)).isFalse();
    }

    @Test
    void aNewRoundStartsWithoutWalls() {
        raise();
        sim.onRoundStart(2);

        assertThat(wall()).isNull();
    }
}
