package com.bajoterra.core.sim;

import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.entity.Combatant;
import org.junit.jupiter.api.Test;

/** Celebration dances: a player starts one, anything else stops it, and the winners of a round dance on their own. */
class DanceTest {

    private final GameSimulation sim = scripted("test_open", 3, human(0, "eli", "infurnus"),
            human(1, "kord", "infurnus"));
    private final Combatant dancer = sim.combatants()[0];
    private final Combatant other = sim.combatants()[1];

    DanceTest() {
        startRound(sim);
        place(dancer, -10, 0, 0);
        place(other, 10, 12, 0);
    }

    private void ask(int dance) {
        sim.submit(new InputFrame(dancer.id, 1, 0, 0, 0, 0, false, 0, 0, 0, false, 0, -1, 0, false, dance));
        sim.tick();
    }

    @Test
    void aDanceStartsOnRequestAndLastsWhileStandingStill() {
        ask(2);
        assertThat(dancer.dance).isEqualTo(2);
        ticks(sim, 120);
        assertThat(dancer.dance).as("still dancing two seconds later").isEqualTo(2);
    }

    @Test
    void movingStopsTheDance() {
        ask(1);
        sim.submit(new InputFrame(dancer.id, 2, 0, 1, 0, 0, false, 0, 0, 0, false, 0, -1, 0, false, 0));
        sim.tick();

        assertThat(dancer.dance).isZero();
    }

    @Test
    void unknownDancesAreIgnored() {
        ask(Combatant.DANCES + 1);

        assertThat(dancer.dance).isZero();
    }

    @Test
    void theSurvivorsOfTheWinningTeamDanceWhenTheRoundEnds() {
        sim.arena().damage(dancer, other, 10_000, com.bajoterra.core.slug.Element.NEUTRAL, 0);
        for (int i = 0; i < 30 && sim.match().phase().isGameplayActive(); i++) {
            sim.tick();
        }

        assertThat(dancer.dance).isBetween(1, Combatant.DANCES);
        assertThat(other.dance).isZero();
    }
}
