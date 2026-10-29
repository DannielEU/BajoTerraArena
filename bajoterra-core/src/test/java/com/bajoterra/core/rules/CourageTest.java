package com.bajoterra.core.rules;

import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

/** Courage: survivors of an outnumbered team hit harder, fire faster, take less damage and get a shield burst. */
class CourageTest {

    /** Two against two on the open field, everybody far apart and idle. */
    private static GameSimulation twoOnTwo() {
        GameSimulation sim = scripted("test_open", 3,
                human(0, "eli", "flaringo"), human(0, "kord", "flaringo"),
                human(1, "eli", "flaringo"), human(1, "kord", "flaringo"));
        startRound(sim);
        place(sim.combatants()[0], -15, -8, 0);
        place(sim.combatants()[1], -15, 8, 0);
        place(sim.combatants()[2], 15, -8, 0);
        place(sim.combatants()[3], 15, 8, 0);
        return sim;
    }

    private static GameEvent last(GameSimulation sim, EventType type) {
        GameEvent found = null;
        for (long seq = sim.events().oldestAvailable(); seq < sim.events().written(); seq++) {
            if (sim.events().at(seq).type == type) {
                found = sim.events().at(seq);
            }
        }
        return found;
    }

    @Test
    void evenTeamsHaveNoCourage() {
        GameSimulation sim = twoOnTwo();
        sim.tick();

        for (Combatant c : sim.combatants()) {
            assertThat(c.courage).isZero();
        }
    }

    @Test
    void whenATeammateFallsTheSurvivorGetsCourageAndAShieldBurst() {
        GameSimulation sim = twoOnTwo();
        Combatant survivor = sim.combatants()[0];
        sim.arena().damage(null, survivor, 40, Element.NEUTRAL, 0); // shield 50 -> 10
        sim.arena().damage(null, sim.combatants()[1], 10_000, Element.NEUTRAL, 0);

        sim.tick();

        assertThat(survivor.courage).isEqualTo(1);
        assertThat(sim.combatants()[2].courage).as("the team ahead gets nothing").isZero();
        assertThat(survivor.vitals.shield()).isCloseTo(10 + 25, within(1e-9));
        GameEvent event = last(sim, EventType.COURAGE);
        assertThat(event.subject).isEqualTo(survivor.id);
        assertThat(event.a).isEqualTo(1);
        assertThat(event.b).isZero();
        assertThat(event.value).isCloseTo(25, within(1e-9));
    }

    @Test
    void courageHitsHarderAndSoftensTheBlowsTaken() {
        GameSimulation sim = twoOnTwo();
        Combatant survivor = sim.combatants()[0];
        Combatant enemy = sim.combatants()[2];
        sim.arena().damage(null, sim.combatants()[1], 10_000, Element.NEUTRAL, 0);
        sim.tick();

        double dealt = sim.arena().damage(survivor, enemy, 20, Element.NEUTRAL, 0);
        double taken = sim.arena().damage(enemy, survivor, 20, Element.NEUTRAL, 0);

        assertThat(dealt).isCloseTo(20 * (1 + Courage.DAMAGE_BONUS_PER_LEVEL), within(1e-9));
        assertThat(taken).isCloseTo(20 * (1 - Courage.DAMAGE_REDUCTION_PER_LEVEL), within(1e-9));
    }

    @Test
    void courageMakesTheBlasterRechargeFasterAndTheShotSaysHowLong() {
        GameSimulation sim = twoOnTwo();
        Combatant survivor = sim.combatants()[0];
        sim.arena().damage(null, sim.combatants()[1], 10_000, Element.NEUTRAL, 0);
        survivor.input.aimYaw = Math.PI;
        survivor.input.fire = true;

        sim.tick();
        survivor.input.fire = false;

        GameEvent shot = last(sim, EventType.SHOT);
        int normal = survivor.character.blaster().fireIntervalTicks();
        int expected = (int) Math.round(normal * 0.6 / (1 + Courage.FIRE_RATE_BONUS_PER_LEVEL));
        assertThat(shot.value).isEqualTo(expected);
        assertThat(survivor.nextFireTick - sim.tickCount()).isEqualTo(expected);
    }

    @Test
    void courageGoesAwayWhenTheNumbersEvenUpAgainAndIsCappedAtTwo() {
        GameSimulation sim = scripted("test_open", 4,
                human(0, "eli", "flaringo"),
                human(1, "eli", "flaringo"), human(1, "kord", "flaringo"), human(1, "pronto", "flaringo"));
        startRound(sim);
        sim.tick();
        Combatant alone = sim.combatants()[0];
        assertThat(alone.courage).as("one against three").isEqualTo(Courage.MAX_LEVEL);

        sim.arena().damage(null, sim.combatants()[1], 10_000, Element.NEUTRAL, 0);
        sim.arena().damage(null, sim.combatants()[2], 10_000, Element.NEUTRAL, 0);
        sim.tick();

        assertThat(alone.courage).isZero();
        assertThat(last(sim, EventType.COURAGE).b).isEqualTo(Courage.MAX_LEVEL);
    }

    @Test
    void aRuleSetCanTurnCourageOff() {
        MatchRules standard = EliminationRoundsRules.bestOfFive();
        MatchRules noComebacks = new MatchRules() {
            @Override
            public int countdownTicks() {
                return standard.countdownTicks();
            }

            @Override
            public int roundTicks() {
                return standard.roundTicks();
            }

            @Override
            public int roundEndTicks() {
                return standard.roundEndTicks();
            }

            @Override
            public int evaluateRound(com.bajoterra.core.combat.CombatArena arena, int ticksLeft) {
                return standard.evaluateRound(arena, ticksLeft);
            }

            @Override
            public boolean isMatchOver(int[] wins, int roundsPlayed) {
                return standard.isMatchOver(wins, roundsPlayed);
            }

            @Override
            public int matchWinner(int[] wins) {
                return standard.matchWinner(wins);
            }

            @Override
            public int courageLevel(int ownAlive, int enemyAlive) {
                return 0;
            }
        };

        assertThat(standard.courageLevel(1, 3)).isEqualTo(2);
        assertThat(standard.courageLevel(2, 3)).isEqualTo(1);
        assertThat(standard.courageLevel(3, 1)).isZero();
        assertThat(noComebacks.courageLevel(1, 3)).isZero();
    }
}
