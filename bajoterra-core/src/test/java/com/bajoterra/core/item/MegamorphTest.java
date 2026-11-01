package com.bajoterra.core.item;

import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.sim.GameSimulation;
import org.junit.jupiter.api.Test;

/** The megamorph crystal shows up some way into every round and megamorphs whoever takes it. */
class MegamorphTest {

    private final GameSimulation sim = scripted("caverna", 8, human(0, "eli", "infurnus"), human(1, "eli", "doc"));
    private final Combatant taker = sim.combatants()[0];

    private Pickup crystal() {
        return sim.pickups().stream().filter(p -> p.definition.id().equals("megamorph_crystal")).findFirst()
                .orElseThrow();
    }

    @Test
    void theCrystalIsNotThereAtTheStartOfTheRoundAndShowsUpLater() {
        startRound(sim);
        assertThat(crystal().available).isFalse();
        ticks(sim, crystal().definition.firstSpawnTicks());

        assertThat(crystal().available).isTrue();
    }

    @Test
    void takingItMakesSlugsGiantStrongerAndFaster() {
        startRound(sim);
        ticks(sim, crystal().definition.firstSpawnTicks());
        Pickup crystal = crystal();
        place(taker, crystal.pos.x, crystal.pos.z, Math.PI / 2);
        ticks(sim, 2);

        assertThat(crystal.available).isFalse();
        assertThat(taker.statuses.has("megamorph")).isTrue();
        assertThat(taker.stats.damageDealtMultiplier).isGreaterThan(1.3);
        taker.input.fire = true;
        for (int i = 0; i < 60 && taker.shotsFired == 0; i++) {
            sim.tick();
        }
        Projectile slug = null;
        for (int i = 0; i < sim.projectiles().capacity(); i++) {
            if (sim.projectiles().at(i).active) {
                slug = sim.projectiles().at(i);
            }
        }
        assertThat(slug).isNotNull();
        assertThat(slug.radius).isGreaterThan(slug.slug.projectile().radius() * 1.5);
    }
}
