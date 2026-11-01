package com.bajoterra.core.ammo;

import static com.bajoterra.core.Fixtures.count;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.sim.GameSimulation;
import org.junit.jupiter.api.Test;

/**
 * Fusion shots: dealing damage (and, slowly, time) fills the fusion energy; once full, the fusion button fires the
 * selected slug together with the next one as a single projectile carrying both slugs' effects.
 */
class FusionShotTest {

    private static final double EAST = Math.PI / 2;

    private final GameSimulation sim = scripted("test_open", 6,
            human(0, "eli", "arachnet", "tormato"), human(1, "kord", "infurnus"));
    private final Combatant slinger = sim.combatants()[0];
    private final Combatant enemy = sim.combatants()[1];

    FusionShotTest() {
        startRound(sim);
        place(slinger, -10, 0, EAST);
        place(enemy, 0, 0, -EAST);
    }

    @Test
    void theFusionButtonDoesNothingUntilTheEnergyIsFull() {
        slinger.fusion = 50;
        slinger.input.fusion = true;
        ticks(sim, 30);

        assertThat(slinger.shotsFired).isZero();
    }

    @Test
    void aFusionShotFiresTwoSlugsInOneAndSpendsTheEnergy() {
        slinger.fusion = Combatant.FUSION_FULL;
        slinger.input.aimPitch = 0.02;
        slinger.input.fusion = true;
        sim.tick();
        slinger.input.fusion = false;

        Projectile fused = null;
        for (int i = 0; i < sim.projectiles().capacity(); i++) {
            if (sim.projectiles().at(i).active) {
                fused = sim.projectiles().at(i);
            }
        }
        assertThat(fused).isNotNull();
        assertThat(fused.slug.id()).isEqualTo("arachnet");
        assertThat(fused.partner.id()).isEqualTo("tormato");
        assertThat(slinger.fusion).as("spent (it fills a little again every tick)").isLessThan(1);
        assertThat(slinger.loadout.isAvailable(0)).isFalse();
        assertThat(slinger.loadout.isAvailable(1)).as("the partner's slot is spent too").isFalse();
        assertThat(count(sim, EventType.FUSION)).isEqualTo(1);

        for (int i = 0; i < 60 && !enemy.statuses.has("ensnare"); i++) {
            sim.tick();
        }
        assertThat(enemy.statuses.has("ensnare")).as("the first slug's web").isTrue();
        assertThat(count(sim, EventType.TORNADO)).as("and the second slug's tornado").isEqualTo(1);
    }

    @Test
    void anAttackNeverFusesWithAHealingSlug() {
        GameSimulation mixed = com.bajoterra.core.Fixtures.scripted("test_open", 7,
                human(0, "eli", "infurnus", "doc", "doc", "tormato"), human(1, "kord", "infurnus"));
        Combatant c = mixed.combatants()[0];

        assertThat(c.loadout.fusionPartner()).as("skips the two Docs").isEqualTo(3);
    }

    @Test
    void damageDealtFillsTheEnergy() {
        sim.arena().damage(slinger, enemy, 20, com.bajoterra.core.slug.Element.FIRE, 0);

        assertThat(slinger.fusion).isGreaterThan(10);
    }

    @Test
    void theEnergyAlsoFillsSlowlyOnItsOwnAndStartsEmptyEachRound() {
        assertThat(slinger.fusion).isLessThan(5);
        ticks(sim, 600);

        assertThat(slinger.fusion).isBetween(10.0, 14.0);
    }
}
