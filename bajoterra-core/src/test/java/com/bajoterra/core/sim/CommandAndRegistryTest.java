package com.bajoterra.core.sim;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.bajoterra.core.content.ContentException;
import com.bajoterra.core.content.Registry;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;
import java.util.List;
import org.junit.jupiter.api.Test;

class CommandAndRegistryTest {

    @Test
    void inputFramesAreAppliedAtTheStartOfTheNextTick() {
        GameSimulation sim = scripted("test_open", 1, human(0, "eli", "doc"), human(1, "eli", "doc"));
        startRound(sim);
        Combatant eli = sim.combatants()[0];

        sim.submit(new InputFrame(0, 42, 0.5f, 1f, 1.2f, 0.1f, false, 0, 0, 0, true, 0b101, 3, 1));
        assertThat(eli.input.sequence).isZero();
        sim.tick();

        assertThat(eli.input.sequence).isEqualTo(42);
        assertThat(eli.input.moveX).isEqualTo(0.5);
        assertThat(eli.input.aimYaw).isCloseTo(1.2, org.assertj.core.data.Offset.offset(1e-6));
        assertThat(eli.input.moveMask).isEqualTo(0b101);
    }

    @Test
    void inputFramesAreSanitised() {
        GameSimulation sim = scripted("test_open", 1, human(0, "eli", "doc"), human(1, "eli", "doc"));
        startRound(sim);

        sim.submit(new InputFrame(0, 1, 7f, Float.NaN, 0f, 9f, false, 0, 0, 0, false, 0, -1, 0));
        sim.tick();

        Combatant eli = sim.combatants()[0];
        assertThat(eli.input.moveX).isEqualTo(1.0);
        assertThat(eli.input.moveZ).isZero();
        assertThat(eli.input.aimPitch).isLessThanOrEqualTo(1.5);
    }

    @Test
    void commandsForUnknownCombatantsAreIgnored() {
        GameSimulation sim = scripted("test_open", 1, human(0, "eli", "doc"), human(1, "eli", "doc"));

        sim.submit(new InputFrame(99, 1, 0, 0, 0, 0, false, 0, 0, 0, false, 0, -1, 0));
        sim.submit(new InputFrame(-1, 1, 0, 0, 0, 0, false, 0, 0, 0, false, 0, -1, 0));
        sim.tick();

        assertThat(sim.tickCount()).isEqualTo(1);
    }

    @Test
    void registriesLookUpByIdAndByIndex() {
        Registry<SlugDefinition> slugs = CONTENT.slugs();

        assertThat(slugs.require("doc").id()).isEqualTo("doc");
        assertThat(slugs.get(slugs.require("doc").index()).id()).isEqualTo("doc");
        assertThat(slugs.find("nope")).isNull();
        assertThat(slugs.contains("infurnus")).isTrue();
        assertThatThrownBy(() -> slugs.require("nope")).isInstanceOf(ContentException.class)
                .hasMessageContaining("Unknown slug 'nope'");
    }

    @Test
    void registriesRejectDuplicatesAndWrongIndices() {
        SlugDefinition a = SlugDefinition.builder("a").build();
        SlugDefinition b = SlugDefinition.builder("a").build().withIndex(1);

        assertThatThrownBy(() -> new Registry<>("slug", List.of(a, b))).isInstanceOf(ContentException.class)
                .hasMessageContaining("Duplicate");
        assertThatThrownBy(() -> new Registry<>("slug", List.of(a.withIndex(3))))
                .isInstanceOf(IllegalArgumentException.class);
    }
}
