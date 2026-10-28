package com.bajoterra.core.rules;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.count;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

/** Rounds without respawn, best-of-five, and the 3v3 team setup. */
class MatchFlowTest {

    private static final EliminationRoundsRules QUICK = new EliminationRoundsRules(10, 300, 10, 3, 9);

    private GameSimulation duel(EliminationRoundsRules rules) {
        MatchConfig config = MatchConfig.builder("test_open").seed(2).fillWithBots(false)
                .maxDuplicateSlugs(10)
                .rules(rules)
                .slot(human(0, "eli", "flaringo"))
                .slot(human(1, "eli", "flaringo"))
                .build();
        return new GameSimulation(CONTENT, config);
    }

    private void runUntil(GameSimulation sim, MatchPhase phase) {
        for (int guard = 0; guard < 100_000 && sim.match().phase() != phase; guard++) {
            sim.tick();
        }
        assertThat(sim.match().phase()).isEqualTo(phase);
    }

    private void winRoundFor(GameSimulation sim, int team) {
        runUntil(sim, MatchPhase.ROUND_ACTIVE);
        Combatant loser = sim.combatants()[team == 0 ? 1 : 0];
        sim.arena().damage(sim.combatants()[team], loser, 10_000, Element.NEUTRAL, 0);
        sim.tick();
    }

    @Test
    void theMatchWaitsInTheLobbyUntilStarted() {
        GameSimulation sim = duel(QUICK);

        ticks(sim, 30);

        assertThat(sim.match().phase()).isEqualTo(MatchPhase.LOBBY);
        assertThat(sim.match().roundNumber()).isZero();
    }

    @Test
    void phasesRunCountdownThenRoundThenRoundEnd() {
        GameSimulation sim = duel(QUICK);
        sim.start();
        assertThat(sim.match().phase()).isEqualTo(MatchPhase.COUNTDOWN);
        assertThat(sim.match().roundNumber()).isEqualTo(1);

        runUntil(sim, MatchPhase.ROUND_ACTIVE);
        assertThat(count(sim, EventType.ROUND_STARTED)).isEqualTo(1);

        winRoundFor(sim, 0);
        assertThat(sim.match().phase()).isEqualTo(MatchPhase.ROUND_END);
        assertThat(sim.match().lastRoundWinner()).isZero();
        assertThat(sim.match().wins(0)).isEqualTo(1);
        assertThat(sim.match().wins(1)).isZero();
        assertThat(count(sim, EventType.ROUND_ENDED)).isEqualTo(1);
    }

    @Test
    void combatantsAreFrozenDuringTheCountdown() {
        GameSimulation sim = duel(new EliminationRoundsRules(60, 300, 10, 3, 9));
        sim.start();
        Combatant eli = sim.combatants()[0];
        double startX = eli.pos.x;
        eli.input.moveZ = 1;
        eli.input.aimYaw = Math.PI / 2;
        eli.input.fire = true;

        ticks(sim, 30);

        assertThat(sim.match().phase()).isEqualTo(MatchPhase.COUNTDOWN);
        assertThat(eli.pos.x).isEqualTo(startX);
        assertThat(eli.shotsFired).isZero();
    }

    @Test
    void defeatedCombatantsStayOutUntilTheNextRound() {
        GameSimulation sim = duel(QUICK);
        sim.start();
        winRoundFor(sim, 0);
        Combatant loser = sim.combatants()[1];
        assertThat(loser.alive).isFalse();

        ticks(sim, 5);
        assertThat(loser.alive).as("no respawn within the round end pause").isFalse();

        runUntil(sim, MatchPhase.COUNTDOWN);
        assertThat(sim.match().roundNumber()).isEqualTo(2);
        assertThat(loser.alive).isTrue();
        assertThat(loser.vitals.hp()).isEqualTo(loser.vitals.maxHp());
        assertThat(loser.vitals.shield()).isEqualTo(loser.vitals.maxShield());
    }

    @Test
    void everyRoundStartsFromTheSpawnsWithFullLoadouts() {
        GameSimulation sim = duel(QUICK);
        sim.start();
        runUntil(sim, MatchPhase.ROUND_ACTIVE);
        Combatant eli = sim.combatants()[0];
        eli.input.aimYaw = Math.PI / 2;
        eli.input.fire = true;
        ticks(sim, 60);
        eli.input.fire = false;
        assertThat(eli.loadout.shotCounter()).isPositive();

        sim.arena().damage(eli, sim.combatants()[1], 10_000, Element.NEUTRAL, 0);
        runUntil(sim, MatchPhase.COUNTDOWN);

        assertThat(eli.loadout.shotCounter()).isZero();
        assertThat(eli.pos.x).isEqualTo(-15);
        assertThat(sim.combatants()[1].pos.x).isEqualTo(15);
    }

    @Test
    void firstToThreeRoundsWinsAndTheMatchEnds() {
        GameSimulation sim = duel(QUICK);
        sim.start();

        winRoundFor(sim, 0);
        winRoundFor(sim, 1);
        winRoundFor(sim, 0);
        assertThat(sim.match().isOver()).isFalse();
        winRoundFor(sim, 0);
        runUntil(sim, MatchPhase.MATCH_END);

        assertThat(sim.match().winner()).isZero();
        assertThat(sim.match().wins(0)).isEqualTo(3);
        assertThat(sim.match().wins(1)).isEqualTo(1);
        assertThat(sim.match().roundNumber()).isEqualTo(4);
        assertThat(count(sim, EventType.MATCH_ENDED)).isEqualTo(1);
        ticks(sim, 100);
        assertThat(sim.match().phase()).isEqualTo(MatchPhase.MATCH_END);
    }

    @Test
    void bestOfFiveIsTheDefault() {
        EliminationRoundsRules rules = EliminationRoundsRules.bestOfFive();

        assertThat(rules.winsNeeded()).isEqualTo(3);
        assertThat(rules.isMatchOver(new int[] {2, 2}, 4)).isFalse();
        assertThat(rules.isMatchOver(new int[] {3, 2}, 5)).isTrue();
        assertThat(rules.roundTicks()).isEqualTo(90 * 60);
    }

    @Test
    void whenTimeRunsOutTheHealthierTeamWins() {
        GameSimulation sim = duel(QUICK);
        sim.start();
        runUntil(sim, MatchPhase.ROUND_ACTIVE);
        sim.arena().damage(null, sim.combatants()[1], 40, Element.NEUTRAL, 0);

        runUntil(sim, MatchPhase.ROUND_END);

        assertThat(sim.match().lastRoundWinner()).isZero();
    }

    @Test
    void aTimedOutRoundWithEqualHealthIsADraw() {
        GameSimulation sim = duel(QUICK);
        sim.start();
        runUntil(sim, MatchPhase.ROUND_END);

        assertThat(sim.match().lastRoundWinner()).isEqualTo(MatchRules.DRAW);
        assertThat(sim.match().wins(0) + sim.match().wins(1)).isZero();
    }

    @Test
    void eliminatingEveryoneAtTheSameTimeIsADraw() {
        GameSimulation sim = duel(QUICK);
        sim.start();
        runUntil(sim, MatchPhase.ROUND_ACTIVE);
        sim.arena().damage(null, sim.combatants()[0], 10_000, Element.NEUTRAL, 0);
        sim.arena().damage(null, sim.combatants()[1], 10_000, Element.NEUTRAL, 0);
        sim.tick();

        assertThat(sim.match().lastRoundWinner()).isEqualTo(MatchRules.DRAW);
    }

    @Test
    void endlessDrawsAreCappedByTheMaximumNumberOfRounds() {
        GameSimulation sim = duel(new EliminationRoundsRules(2, 5, 2, 3, 4));
        sim.start();

        runUntil(sim, MatchPhase.MATCH_END);

        assertThat(sim.match().roundNumber()).isEqualTo(4);
        assertThat(sim.match().winner()).isEqualTo(MatchRules.DRAW);
    }

    @Test
    void aFullMatchHasThreeCombatantsPerTeamFilledWithBots() {
        MatchConfig config = MatchConfig.builder("caverna").seed(1)
                .maxDuplicateSlugs(10)
                .slot(human(0, "eli", "doc", "flaringo"))
                .build();
        GameSimulation sim = new GameSimulation(CONTENT, config);

        assertThat(sim.combatants()).hasSize(6);
        int team0 = 0;
        int team1 = 0;
        int bots = 0;
        for (Combatant c : sim.combatants()) {
            if (c.team == 0) {
                team0++;
            } else {
                team1++;
            }
            if (c.brain != null) {
                bots++;
            }
        }
        assertThat(team0).isEqualTo(3);
        assertThat(team1).isEqualTo(3);
        assertThat(bots).isEqualTo(5);
        assertThat(sim.combatants()[0].brain).as("the human keeps manual control").isNull();
        assertThat(sim.slots().get(0).bot()).isFalse();
        assertThat(sim.combatants()[3].team).isEqualTo(1);
    }

    @Test
    void everyCombatantCarriesExactlyTenSlugs() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(4).build());

        for (Combatant c : sim.combatants()) {
            int slugs = 0;
            for (int slot = 0; slot < 10; slot++) {
                assertThat(c.loadout.slug(slot)).isNotNull();
                slugs++;
            }
            assertThat(slugs).isEqualTo(10);
        }
    }

    @Test
    void tooManyPlayersInATeamAreRejected() {
        MatchConfig.Builder builder = MatchConfig.builder("caverna");
        for (int i = 0; i < 4; i++) {
            builder.slot(SlotConfig.bot(0, "eli", 0.5));
        }

        org.assertj.core.api.Assertions.assertThatThrownBy(() -> new GameSimulation(CONTENT, builder.build()))
                .isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void invalidLoadoutsAreRejectedBeforeThePlayersJoin() {
        MatchConfig config = MatchConfig.builder("caverna")
                .slot(SlotConfig.human(0, "eli", java.util.List.of("doc", "doc", "doc")))
                .build();

        org.assertj.core.api.Assertions.assertThatThrownBy(() -> new GameSimulation(CONTENT, config))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessageContaining("exactly 10");
    }
}
