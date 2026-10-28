package com.bajoterra.core.item;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.count;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

/** Restoring health and shield: allies' slugs and map pickups share the same effects. */
class HealAndPickupTest {

    private static final double FACING_PLUS_X = Math.PI / 2;

    /** Medic (team 0) at x=-8, wounded ally (team 0) at x=8 and an enemy standing between them on the line of fire. */
    private GameSimulation medicScenario(String medicCharacter, String slug) {
        GameSimulation sim = scripted("test_open", 5,
                human(0, medicCharacter, slug),
                human(0, "kord", "flaringo"),
                human(1, "eli", "flaringo"));
        startRound(sim);
        Combatant medic = sim.combatants()[0];
        Combatant ally = sim.combatants()[1];
        Combatant enemy = sim.combatants()[2];
        place(medic, -8, 0, FACING_PLUS_X);
        place(ally, 8, 0, -FACING_PLUS_X);
        place(enemy, 0, 0, 0);
        sim.arena().damage(null, ally, 130, Element.NEUTRAL, 0); // kord: shield 70 -> 0, hp 140 -> 80
        medic.input.aimYaw = FACING_PLUS_X;
        medic.input.fire = true;
        return sim;
    }

    @Test
    void anAllyCanHealYouByShootingAHealingSlug() {
        GameSimulation sim = medicScenario("eli", "doc");
        Combatant ally = sim.combatants()[1];
        double before = ally.vitals.hp();

        ticks(sim, 40);

        assertThat(ally.vitals.hp()).isCloseTo(before + 35, within(1e-6));
        assertThat(count(sim, EventType.HEAL)).isGreaterThanOrEqualTo(1);
    }

    @Test
    void healingSlugsFlyThroughEnemiesWithoutTouchingThem() {
        GameSimulation sim = medicScenario("eli", "doc");
        Combatant enemy = sim.combatants()[2];

        ticks(sim, 40);

        assertThat(enemy.vitals.hp()).isEqualTo(enemy.vitals.maxHp());
        assertThat(enemy.vitals.shield()).isEqualTo(enemy.vitals.maxShield());
    }

    @Test
    void damagingSlugsFlyThroughAlliesAndHitTheEnemyBehind() {
        GameSimulation sim = scripted("test_open", 5,
                human(0, "eli", "flaringo"),
                human(0, "kord", "flaringo"),
                human(1, "eli", "flaringo"));
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        Combatant ally = sim.combatants()[1];
        Combatant enemy = sim.combatants()[2];
        place(shooter, -8, 0, FACING_PLUS_X);
        place(ally, 0, 0, 0);
        place(enemy, 8, 0, -FACING_PLUS_X);
        shooter.input.aimYaw = FACING_PLUS_X;
        shooter.input.fire = true;

        ticks(sim, 40);

        assertThat(ally.vitals.shield()).isEqualTo(ally.vitals.maxShield());
        assertThat(enemy.vitals.shield()).isLessThan(enemy.vitals.maxShield());
    }

    @Test
    void healingPotencyOfTheCasterScalesTheHeal() {
        GameSimulation sim = medicScenario("trixie", "doc"); // potency 1.25
        Combatant ally = sim.combatants()[1];
        double before = ally.vitals.hp();

        ticks(sim, 40);

        assertThat(ally.vitals.hp()).isCloseTo(before + 35 * 1.25, within(1e-6));
    }

    @Test
    void aProtectionSlugGivesTheAllyAnOvershield() {
        GameSimulation sim = medicScenario("eli", "armashelt");
        Combatant ally = sim.combatants()[1];

        ticks(sim, 40);

        assertThat(ally.vitals.overshield()).isGreaterThan(0);
        double hpBefore = ally.vitals.hp();
        sim.arena().damage(sim.combatants()[2], ally, 20, Element.NEUTRAL, 0);
        assertThat(ally.vitals.hp()).as("the overshield took the hit").isEqualTo(hpBefore);
    }

    @Test
    void aShieldSlugRestoresShieldAndPutsOutFires() {
        GameSimulation sim = medicScenario("eli", "aquabeek");
        Combatant ally = sim.combatants()[1];
        com.bajoterra.core.Fixtures.apply(sim.arena(),
                com.bajoterra.core.Fixtures.effect("{\"type\":\"burn\",\"dps\":3,\"durationTicks\":600}"),
                sim.combatants()[2], ally);
        assertThat(ally.statuses.has("burn")).isTrue();

        ticks(sim, 40);

        assertThat(ally.vitals.shield()).isGreaterThan(0);
        assertThat(ally.statuses.has("burn")).isFalse();
    }

    @Test
    void aRegenerationSlugHealsOverTime() {
        GameSimulation sim = medicScenario("eli", "boon_doc");
        Combatant ally = sim.combatants()[1];
        double before = ally.vitals.hp();

        ticks(sim, 30);
        medicStopsFiring(sim);
        double soon = ally.vitals.hp();
        ticks(sim, 300);

        assertThat(ally.vitals.hp()).isGreaterThan(soon);
        assertThat(ally.vitals.hp()).isGreaterThan(before + 20);
    }

    private void medicStopsFiring(GameSimulation sim) {
        sim.combatants()[0].input.fire = false;
    }

    @Test
    void healingNeverExceedsTheMaximum() {
        GameSimulation sim = medicScenario("eli", "doc");
        Combatant ally = sim.combatants()[1];
        sim.arena().heal(null, ally, 1000);

        ticks(sim, 40);

        assertThat(ally.vitals.hp()).isEqualTo(ally.vitals.maxHp());
    }

    // ------------------------------------------------------------------------------------------ pickups

    private GameSimulation pickupScenario() {
        GameSimulation sim = scripted("test_open", 5, human(0, "eli", "flaringo"), human(1, "eli", "flaringo"));
        startRound(sim);
        place(sim.combatants()[0], -10, 0, 0);
        place(sim.combatants()[1], 15, 9, 0);
        return sim;
    }

    @Test
    void theMapPlacesItsPickups() {
        GameSimulation sim = pickupScenario();

        assertThat(sim.pickups()).extracting(p -> p.definition.id()).containsExactly("health_pack", "shield_cell");
        assertThat(sim.pickups()).allMatch(p -> p.available);
    }

    @Test
    void aWoundedCombatantRestoresHealthByWalkingOverAPack() {
        GameSimulation sim = pickupScenario();
        Combatant eli = sim.combatants()[0];
        sim.arena().damage(null, eli, 50, Element.NEUTRAL, 1.0); // piercing: health only
        place(eli, 0, 6, 0);

        ticks(sim, 2);

        assertThat(eli.vitals.hp()).isEqualTo(90);
        assertThat(sim.pickups().get(0).available).isFalse();
        assertThat(count(sim, EventType.PICKUP_TAKEN)).isEqualTo(1);
    }

    @Test
    void aFullHealthCombatantLeavesTheHealthPackAlone() {
        GameSimulation sim = pickupScenario();
        place(sim.combatants()[0], 0, 6, 0);

        ticks(sim, 10);

        assertThat(sim.pickups().get(0).available).isTrue();
    }

    @Test
    void shieldCellsRestoreShield() {
        GameSimulation sim = pickupScenario();
        Combatant eli = sim.combatants()[0];
        sim.arena().damage(null, eli, 30, Element.NEUTRAL, 0);
        place(eli, 0, -6, 0);

        ticks(sim, 2);

        assertThat(eli.vitals.shield()).isEqualTo(50);
        assertThat(sim.pickups().get(1).available).isFalse();
    }

    @Test
    void pickupsComeBackAfterTheirRespawnTime() {
        GameSimulation sim = pickupScenario();
        Combatant eli = sim.combatants()[0];
        sim.arena().damage(null, eli, 50, Element.NEUTRAL, 1.0);
        place(eli, 0, 6, 0);
        ticks(sim, 2);
        place(eli, -10, 0, 0);
        int respawnTicks = CONTENT.pickups().require("health_pack").respawnTicks();

        ticks(sim, respawnTicks - 10);
        assertThat(sim.pickups().get(0).available).isFalse();
        ticks(sim, 20);

        assertThat(sim.pickups().get(0).available).isTrue();
        assertThat(count(sim, EventType.PICKUP_RESPAWNED)).isEqualTo(1);
    }

    @Test
    void deadCombatantsCannotCollectPickups() {
        GameSimulation sim = scripted("test_open", 5, human(0, "eli", "flaringo"), human(0, "kord", "flaringo"),
                human(1, "eli", "flaringo"));
        startRound(sim);
        Combatant dead = sim.combatants()[0];
        place(sim.combatants()[1], -10, 0, 0);
        place(sim.combatants()[2], 15, 9, 0);
        sim.arena().damage(null, dead, 1000, Element.NEUTRAL, 0);
        place(dead, 0, 6, 0);

        ticks(sim, 10);

        assertThat(sim.match().phase().isGameplayActive()).isTrue();
        assertThat(sim.pickups().get(0).available).isTrue();
    }
}
