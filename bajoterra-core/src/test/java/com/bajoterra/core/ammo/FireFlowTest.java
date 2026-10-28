package com.bajoterra.core.ammo;

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
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.SlotConfig;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.Test;

/** Firing end to end: input, interval between shots, slot locking, third-person aiming and damage. */
class FireFlowTest {

    private static final double FACING_PLUS_X = Math.PI / 2;

    private GameSimulation duel(String shooterSlug) {
        return scripted("test_open", 7,
                human(0, "eli", shooterSlug),
                human(1, "eli", "doc"));
    }

    private List<GameEvent> shots(GameSimulation sim) {
        List<GameEvent> shots = new ArrayList<>();
        for (long seq = sim.events().oldestAvailable(); seq < sim.events().written(); seq++) {
            GameEvent event = sim.events().at(seq);
            if (event.type == EventType.SHOT) {
                shots.add(event);
            }
        }
        return shots;
    }

    @Test
    void aShotFlightHitsTheTargetAndAppliesTheSlugEffect() {
        GameSimulation sim = duel("flaringo");
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        Combatant target = sim.combatants()[1];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(target, 5, 0, -FACING_PLUS_X);
        shooter.input.fire = true;
        shooter.input.aimYaw = FACING_PLUS_X;

        ticks(sim, 30);

        assertThat(count(sim, EventType.PROJECTILE_HIT)).isGreaterThanOrEqualTo(1);
        assertThat(target.vitals.shield()).isLessThan(target.vitals.maxShield());
        assertThat(shooter.shotsFired).isGreaterThanOrEqualTo(1);
        assertThat(shooter.shotsHit).isGreaterThanOrEqualTo(1);
    }

    @Test
    void timeBetweenShotsComesFromTheBlasterAndTheSlugDelay() {
        GameSimulation sim = duel("flaringo"); // delay multiplier 0.6 of a 42-tick interval -> 25 ticks
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 15, 8, 0);
        shooter.input.fire = true;
        shooter.input.aimYaw = FACING_PLUS_X;

        ticks(sim, 120);

        List<GameEvent> fired = shots(sim);
        assertThat(fired.size()).isBetween(4, 5);
        for (int i = 1; i < fired.size(); i++) {
            assertThat(fired.get(i).tick - fired.get(i - 1).tick).isEqualTo(25);
        }
    }

    @Test
    void heavySlugsWaitLongerBeforeTheNextShot() {
        GameSimulation sim = duel("rammstone"); // delay multiplier 1.5 -> 63 ticks
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 15, 8, 0);
        shooter.input.fire = true;
        shooter.input.aimYaw = FACING_PLUS_X;

        ticks(sim, 200);

        List<GameEvent> fired = shots(sim);
        assertThat(fired.size()).isGreaterThanOrEqualTo(3);
        for (int i = 1; i < fired.size(); i++) {
            assertThat(fired.get(i).tick - fired.get(i - 1).tick).isEqualTo(63);
        }
    }

    @Test
    void holdingFireNeverReusesASlugWithinThreeShots() {
        SlotConfig shooterSlot = SlotConfig.human(0, "eli",
                List.of("infurnus", "flaringo", "hop_rock", "thresher", "rammstone", "tazerling", "arachnet",
                        "frostfang", "tormato", "doc"));
        GameSimulation sim = scripted("test_open", 3, shooterSlot, human(1, "eli", "doc"));
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 15, 8, 0);
        shooter.input.fire = true;
        shooter.input.aimYaw = FACING_PLUS_X;

        ticks(sim, 60 * 12);

        List<GameEvent> fired = shots(sim);
        assertThat(fired.size()).isGreaterThan(12);
        for (int i = 0; i < fired.size(); i++) {
            for (int back = 1; back <= 3 && i - back >= 0; back++) {
                assertThat(fired.get(i).b).as("shot %d vs shot %d", i, i - back).isNotEqualTo(fired.get(i - back).b);
            }
        }
    }

    @Test
    void cannotFireWhileStunned() {
        GameSimulation sim = duel("flaringo");
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 15, 8, 0);
        shooter.input.fire = true;
        shooter.input.aimYaw = FACING_PLUS_X;
        com.bajoterra.core.Fixtures.apply(sim.arena(),
                com.bajoterra.core.Fixtures.effect("{\"type\":\"stun\",\"durationTicks\":60}"),
                sim.combatants()[1], shooter);

        ticks(sim, 30);

        assertThat(shooter.shotsFired).isZero();
        ticks(sim, 60);
        assertThat(shooter.shotsFired).isGreaterThan(0);
    }

    @Test
    void thirdPersonCameraAimingSendsTheSlugToTheCrosshair() {
        GameSimulation sim = duel("flaringo");
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        Combatant target = sim.combatants()[1];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(target, 5, 0, -FACING_PLUS_X);
        // The camera floats behind and above the shooter, looking along +X.
        shooter.input.hasCamera = true;
        shooter.input.camX = -9;
        shooter.input.camY = 1.7;
        shooter.input.camZ = 0;
        shooter.input.aimYaw = FACING_PLUS_X;
        shooter.input.aimPitch = 0;
        shooter.input.fire = true;

        ticks(sim, 30);

        assertThat(target.vitals.shield()).isLessThan(target.vitals.maxShield());
    }

    @Test
    void thereIsExactlyTenSlugsAndTheCharacterBlasterDefinesTheLock() {
        GameSimulation sim = duel("flaringo");
        Combatant shooter = sim.combatants()[0];

        assertThat(Loadout.SIZE).isEqualTo(10);
        assertThat(shooter.loadout.reloadShotsOf(0))
                .isEqualTo(CONTENT.characters().require("eli").blaster().reloadShots());
        assertThat(shooter.loadout.reloadShotsOf(0)).isEqualTo(3);
    }
}
