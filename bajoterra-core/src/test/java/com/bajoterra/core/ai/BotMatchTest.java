package com.bajoterra.core.ai;

import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import org.junit.jupiter.api.Test;

/** Whole matches between bots: they finish, stay within the rules and are balanced when mirrored. */
class BotMatchTest {

    @Test
    void mirroredMatchesFinishWithoutErrorsAndAreBalanced() {
        int matches = 100;
        int aWins = 0;
        int finished = 0;
        int teamZeroWins = 0;
        for (int i = 0; i < matches; i++) {
            BotArena.Result result = BotArena.play("caverna", 1000 + i, i % 2 == 0, 0.7, 0.7);
            finished += result.finished() ? 1 : 0;
            aWins += result.winnerIsA() == 1 ? 1 : 0;
            if (result.winnerIsA() >= 0 && (result.winnerIsA() == 1) == (i % 2 == 0)) {
                teamZeroWins++;
            }
            assertThat(result.roundsPlayed()).isBetween(3, 9);
            assertThat(result.shotsFired()).isPositive();
        }

        assertThat(finished).as("every match reaches its end").isEqualTo(matches);
        assertThat(aWins / (double) matches).as("mirrored teams win about half the time").isBetween(0.38, 0.62);
        assertThat(teamZeroWins / (double) matches).as("no spawn-side advantage").isBetween(0.38, 0.62);
    }

    @Test
    void aSkilledTeamBeatsANoviceTeam() {
        int strongWins = 0;
        int matches = 20;
        for (int i = 0; i < matches; i++) {
            strongWins += BotArena.play("caverna", 2000 + i, i % 2 == 0, 0.95, 0.2).winnerIsA() == 1 ? 1 : 0;
        }

        assertThat(strongWins).isGreaterThanOrEqualTo(18);
    }

    @Test
    void rulesHoldThroughoutAMatchBetweenBots() {
        GameSimulation sim = BotArena.build("caverna", 77, true, 0.7, 0.7);
        sim.start();

        while (sim.match().phase() != MatchPhase.MATCH_END && sim.tickCount() < 60 * 60 * 25) {
            sim.tick();
            for (Combatant c : sim.combatants()) {
                assertThat(c.vitals.hp()).isBetween(0.0, c.vitals.maxHp());
                assertThat(c.vitals.shield()).isBetween(0.0, c.vitals.maxShield());
                assertThat(c.vitals.isDefeated()).isEqualTo(!c.alive);
                int locked = 0;
                for (int slot = 0; slot < Loadout.SIZE; slot++) {
                    if (!c.loadout.isAvailable(slot)) {
                        locked++;
                    }
                }
                assertThat(locked).as("at most 3 locked slugs").isLessThanOrEqualTo(3);
                assertThat(c.pos.y).isBetween(-0.001, 5.0);
            }
            assertThat(sim.projectiles().activeCount()).isLessThan(sim.projectiles().capacity());
        }

        assertThat(sim.match().phase()).isEqualTo(MatchPhase.MATCH_END);
        assertThat(Math.max(sim.match().wins(0), sim.match().wins(1))).isEqualTo(3);
    }

    @Test
    void sameSeedSameMatch() {
        long first = hashAfter(5, 60 * 30);
        long second = hashAfter(5, 60 * 30);
        long other = hashAfter(6, 60 * 30);

        assertThat(second).isEqualTo(first);
        assertThat(other).isNotEqualTo(first);
    }

    private long hashAfter(long seed, int ticks) {
        GameSimulation sim = BotArena.build("caverna", seed, true, 0.7, 0.7);
        sim.start();
        for (int i = 0; i < ticks; i++) {
            sim.tick();
        }
        return sim.stateHash();
    }
}
