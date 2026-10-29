package com.bajoterra.core.sim;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.ai.BotBrain;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.slug.Element;
import java.util.List;
import org.junit.jupiter.api.Test;

/** Dynamic difficulty: in matches against people, the bots of whoever wins a round get worse and the others better. */
class BotDirectorTest {

    /** One person on team 0, filled with bots. */
    private static GameSimulation personVersusBots() {
        MatchConfig config = MatchConfig.builder("caverna").seed(5)
                .slot(SlotConfig.human(0, "eli", List.of())).build();
        GameSimulation sim = new GameSimulation(CONTENT, config);
        sim.start();
        return sim;
    }

    private static double skill(GameSimulation sim, int id) {
        return ((BotBrain) sim.combatants()[id].brain).skill();
    }

    /** Defeats every member of {@code loser} and plays on until the next round starts. */
    private static void loseRound(GameSimulation sim, int loser) {
        while (!sim.match().phase().isGameplayActive()) {
            sim.tick();
        }
        int round = sim.match().roundNumber();
        for (Combatant c : sim.combatants()) {
            if (c.team == loser) {
                sim.arena().damage(null, c, 10_000, Element.NEUTRAL, 0);
            }
        }
        while (sim.match().roundNumber() == round && sim.match().phase() != MatchPhase.MATCH_END) {
            sim.tick();
        }
    }

    @Test
    void peopleGetStrongAlliesAndSomewhatWeakerRivals() {
        GameSimulation sim = personVersusBots();

        assertThat(skill(sim, 1)).isEqualTo(BotFiller.ALLY_SKILL);
        assertThat(skill(sim, 2)).isEqualTo(BotFiller.ALLY_SKILL);
        assertThat(skill(sim, 3)).isEqualTo(BotFiller.RIVAL_SKILL);
        assertThat(BotFiller.RIVAL_SKILL).isLessThan(BotFiller.ALLY_SKILL);
    }

    @Test
    void losingARoundMakesTheRivalBotsWeakerAndTheAlliesStronger() {
        GameSimulation sim = personVersusBots();
        double rival = skill(sim, 4);
        double ally = skill(sim, 1);

        loseRound(sim, 0);

        assertThat(skill(sim, 4)).isLessThan(rival);
        assertThat(skill(sim, 1)).isGreaterThan(ally);
    }

    @Test
    void winningARoundMakesTheRivalBotsStronger() {
        GameSimulation sim = personVersusBots();
        double rival = skill(sim, 4);

        loseRound(sim, 1);

        assertThat(skill(sim, 4)).isGreaterThan(rival);
        assertThat(sim.fillerBotSkill(1)).isGreaterThan(rival);
        assertThat(sim.fillerBotSkill(0)).isLessThan(BotFiller.ALLY_SKILL);
    }

    @Test
    void skillsStayWithinTheirLimitsWhateverHappens() {
        GameSimulation sim = personVersusBots();
        for (int i = 0; i < 3; i++) {
            loseRound(sim, 0);
        }

        for (int id = 3; id < 6; id++) {
            assertThat(skill(sim, id)).isGreaterThanOrEqualTo(BotDirector.MIN_SKILL);
        }
        for (int id = 1; id < 3; id++) {
            assertThat(skill(sim, id)).isLessThanOrEqualTo(BotDirector.MAX_SKILL);
        }
    }

    @Test
    void aRoomCanStartTheNextMatchWhereTheDifficultyWas() {
        MatchConfig config = MatchConfig.builder("caverna").seed(5)
                .slot(SlotConfig.human(1, "eli", List.of()))
                .fillerSkills(0.8, 0.3).build();
        GameSimulation sim = new GameSimulation(CONTENT, config);

        assertThat(sim.fillerBotSkill(1)).isEqualTo(0.8);
        assertThat(sim.fillerBotSkill(0)).isEqualTo(0.3);
    }

    @Test
    void matchesBetweenBotsAreNeverAdjusted() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(5).build());
        sim.start();

        loseRound(sim, 0);

        for (int id = 0; id < 6; id++) {
            assertThat(skill(sim, id)).isEqualTo(BotFiller.DEFAULT_SKILL);
        }
    }
}
