package com.bajoterra.core.testext;

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
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.sim.GameSimulation;
import com.tngtech.archunit.core.importer.ClassFileImporter;
import com.tngtech.archunit.core.importer.ImportOption;
import org.junit.jupiter.api.Test;

/**
 * Proves the "easy to extend" requirement: a new effect, slug, move behavior, move and character exist in this
 * package and in test resources only, yet are loaded and played by the unmodified game. Adding them touched zero
 * existing classes.
 */
class ExtensibilityTest {

    private static final double FACING_PLUS_X = Math.PI / 2;

    @Test
    void theNewEffectIsDiscoveredWithoutAnyRegistration() {
        assertThat(CONTENT.effects().knows("poison")).isTrue();
        assertThat(CONTENT.effects().types()).contains("poison", "damage");
    }

    @Test
    void theNewSlugIsAJsonFileThatUsesTheNewEffect() {
        var slug = CONTENT.slugs().find("test_toxic");

        assertThat(slug).isNotNull();
        assertThat(slug.onHit()).isNotNull();
    }

    @Test
    void thePoisonSlugPlaysLikeAnyOther() {
        GameSimulation sim = scripted("test_open", 3,
                human(0, "test_hopper", "test_toxic"),
                human(1, "eli", "doc"));
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        Combatant target = sim.combatants()[1];
        place(shooter, -5, 0, FACING_PLUS_X);
        place(target, 5, 0, -FACING_PLUS_X);
        shooter.input.aimYaw = FACING_PLUS_X;
        shooter.input.fire = true;

        ticks(sim, 30);
        shooter.input.fire = false;
        double hpRightAfterHit = target.vitals.hp();
        ticks(sim, 90);

        assertThat(count(sim, EventType.PROJECTILE_HIT)).isGreaterThanOrEqualTo(1);
        assertThat(target.statuses.has("poison") || target.vitals.hp() < hpRightAfterHit).isTrue();
        assertThat(target.vitals.hp()).as("poison bypasses the shield").isLessThan(target.vitals.maxHp());
    }

    @Test
    void theNewMovesBelongToTheNewCharacterOnly() {
        assertThat(CONTENT.characters().require("test_hopper").moves()).extracting(MoveDefinition::id)
                .containsExactly("jump", "crouch", "roll", "test_leap", "test_slide");
        assertThat(CONTENT.characters().require("eli").moves()).extracting(MoveDefinition::id)
                .doesNotContain("test_leap", "test_slide");
    }

    @Test
    void theNewMoveBehaviorMovesTheCharacterFartherThanAJump() {
        GameSimulation sim = scripted("test_open", 3, human(0, "test_hopper", "doc"), human(1, "eli", "doc"));
        startRound(sim);
        Combatant hopper = sim.combatants()[0];
        place(hopper, -10, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 15, 9, 0);
        hopper.input.aimYaw = FACING_PLUS_X;
        hopper.input.moveMask = 1 << 3; // test_leap
        sim.tick();
        hopper.input.moveMask = 0;
        double apex = 0;
        for (int i = 0; i < 90; i++) {
            sim.tick();
            apex = Math.max(apex, hopper.pos.y);
        }

        assertThat(apex).isGreaterThan(0.5);
        assertThat(hopper.pos.x).isGreaterThan(-10 + 2.5); // 14 m/s of decaying push is about 2.9 m
    }

    @Test
    void aMoveWrittenOnlyInJsonReusesAnExistingBehavior() {
        GameSimulation sim = scripted("test_open", 3, human(0, "test_hopper", "doc"), human(1, "eli", "doc"));
        startRound(sim);
        Combatant hopper = sim.combatants()[0];
        place(hopper, -10, 0, FACING_PLUS_X);
        place(sim.combatants()[1], 15, 9, 0);
        hopper.input.aimYaw = FACING_PLUS_X;
        hopper.input.moveMask = 1 << 4; // test_slide: drive at 12 m/s for 0.4 s
        sim.tick();
        hopper.input.moveMask = 0;
        ticks(sim, 40);

        assertThat(hopper.pos.x - (-10)).isBetween(4.0, 5.2);
    }

    @Test
    void noMainCodeKnowsAboutTheExtensions() {
        var mainClasses = new ClassFileImporter()
                .withImportOption(ImportOption.Predefined.DO_NOT_INCLUDE_TESTS)
                .importPackages("com.bajoterra.core");

        com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses()
                .should().dependOnClassesThat().resideInAPackage("..testext..")
                .allowEmptyShould(true)
                .check(mainClasses);
    }
}
