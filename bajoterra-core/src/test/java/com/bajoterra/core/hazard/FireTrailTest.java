package com.bajoterra.core.hazard;

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
import com.bajoterra.core.slug.TrailSpec;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.Test;

/** Infurnus leaves a wall of fire on the floor along its flight that burns whoever walks into it. */
class FireTrailTest {

    private static final double EAST = Math.PI / 2;
    private static final TrailSpec TRAIL = CONTENT.slugs().require("infurnus").trail();

    // Combatants are in team order: the slinger and its ally on team 0, the enemy on team 1.
    private final GameSimulation sim = scripted("test_open", 5,
            human(0, "eli", "infurnus"), human(0, "trixie", "doc"), human(1, "eli", "doc"));
    private final Combatant slinger = sim.combatants()[0];
    private final Combatant ally = sim.combatants()[1];
    private final Combatant enemy = sim.combatants()[2];

    FireTrailTest() {
        startRound(sim);
        place(slinger, -15, 0, EAST);
        place(enemy, 0, 0, 8);
        place(ally, 0, 0, -8);
    }

    private void fire(double pitch) {
        slinger.input.aimPitch = pitch;
        slinger.input.fire = true;
        int shots = slinger.shotsFired;
        for (int i = 0; i < 120 && slinger.shotsFired == shots; i++) {
            sim.tick();
        }
        slinger.input.fire = false;
    }

    private List<Hazard> burning() {
        List<Hazard> list = new ArrayList<>();
        for (Hazard h : sim.hazards().hazards()) {
            if (h.active) {
                list.add(h);
            }
        }
        return list;
    }

    @Test
    void infurnusLeavesAWallOfFireOnTheFloorAlongItsFlight() {
        fire(0);
        ticks(sim, 30);

        List<Hazard> fire = burning();
        assertThat(fire).hasSizeBetween((int) (TRAIL.maxLength() / TRAIL.spacing()) - 1,
                (int) (TRAIL.maxLength() / TRAIL.spacing()) + 1);
        for (Hazard h : fire) {
            assertThat(h.y).as("on the floor").isCloseTo(0, within(1e-6));
            assertThat(h.z).as("along the line of flight").isCloseTo(slinger.pos.z, within(0.3));
            assertThat(h.x).as("not at the slinger's feet").isGreaterThan(slinger.pos.x + TRAIL.startAfter() - 0.1);
        }
    }

    @Test
    void aSlugFlyingHighLeavesNoFire() {
        fire(1.0);
        ticks(sim, 20);

        assertThat(burning()).isEmpty();
    }

    @Test
    void anEnemyWalkingIntoTheFireBurnsAndAnAllyDoesNot() {
        fire(0);
        ticks(sim, 25);
        Hazard patch = burning().get(burning().size() / 2);
        place(enemy, patch.x, patch.z, 0);
        place(ally, patch.x + 0.2, patch.z + 0.1, 0);
        double enemyBefore = enemy.vitals.totalFraction();
        double allyBefore = ally.vitals.totalFraction();

        ticks(sim, 60);

        assertThat(enemy.vitals.totalFraction()).isLessThan(enemyBefore);
        assertThat(enemy.statuses.has("burn")).isTrue();
        assertThat(ally.vitals.totalFraction()).isEqualTo(allyBefore);
    }

    @Test
    void theFireBurnsOutAfterItsDuration() {
        fire(0);
        ticks(sim, TRAIL.durationTicks() + 60);

        assertThat(burning()).isEmpty();
    }

    @Test
    void standingInSeveralPatchesHurtsOncePerPeriod() {
        fire(0);
        ticks(sim, 25);
        Hazard patch = burning().get(3);
        place(enemy, patch.x + TRAIL.spacing() * 0.5, patch.z, 0);   // between two patches, in both
        long from = sim.events().written();
        ticks(sim, TRAIL.periodTicks() - 1);
        int bites = 0;
        for (long seq = from; seq < sim.events().written(); seq++) {
            GameEvent e = sim.events().at(seq);
            if (e.type == EventType.STATUS_APPLIED && e.subject == enemy.id) {
                bites++;
            }
        }

        assertThat(bites).as("one bite per period, not one per patch").isEqualTo(1);
    }
}
