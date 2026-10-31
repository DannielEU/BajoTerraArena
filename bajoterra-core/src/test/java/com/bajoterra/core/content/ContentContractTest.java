package com.bajoterra.core.content;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.count;
import static com.bajoterra.core.Fixtures.human;
import static com.bajoterra.core.Fixtures.place;
import static com.bajoterra.core.Fixtures.scripted;
import static com.bajoterra.core.Fixtures.startRound;
import static com.bajoterra.core.Fixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.SlugRole;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.GameMap;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.MethodSource;

/**
 * Contract every piece of content must honour. New slugs, moves, characters and maps are picked up by these
 * parameterised tests automatically, so adding content cannot silently break the game.
 */
class ContentContractTest {

    static Stream<SlugDefinition> slugs() {
        return CONTENT.slugs().all().stream();
    }

    static Stream<CharacterDefinition> characters() {
        return CONTENT.characters().all().stream();
    }

    static Stream<GameMap> maps() {
        return CONTENT.maps().all().stream();
    }

    @Test
    void theCatalogHasTheDocumentedStarterContent() {
        assertThat(CONTENT.slugs().all()).extracting(SlugDefinition::id).contains(
                "infurnus", "flaringo", "hop_rock", "thresher", "rammstone", "tazerling", "arachnet", "frostfang",
                "tormato", "phosphoro", "doc", "boon_doc", "aquabeek", "armashelt");
        assertThat(CONTENT.characters().all()).extracting(CharacterDefinition::id)
                .contains("eli", "kord", "trixie", "pronto");
        assertThat(CONTENT.moves().all()).extracting(MoveDefinition::id).contains("jump", "crouch", "roll", "dash");
        assertThat(CONTENT.effects().types()).contains("damage", "heal", "restore_shield", "overshield",
                "knockback", "area", "chain", "conditional", "cleanse", "burn", "slow", "freeze", "stun", "regen",
                "blind");
    }

    @Test
    void indicesAreDenseAndFollowIdOrder() {
        for (var registry : java.util.List.of(CONTENT.slugs(), CONTENT.moves(), CONTENT.characters(),
                CONTENT.pickups(), CONTENT.maps())) {
            String previous = "";
            for (int i = 0; i < registry.size(); i++) {
                assertThat(registry.get(i).index()).isEqualTo(i);
                assertThat(registry.get(i).id().compareTo(previous)).isPositive();
                previous = registry.get(i).id();
            }
        }
    }

    @ParameterizedTest(name = "slug {0}")
    @MethodSource("slugs")
    void everySlugCanBeFiredAndTakesEffect(SlugDefinition slug) {
        boolean support = slug.role() == SlugRole.SUPPORT;
        // Team 1 always has a bystander so the round does not end the moment it starts.
        GameSimulation sim = scripted("test_open", 11,
                human(0, "eli", slug.id()),
                human(support ? 0 : 1, support ? "kord" : "eli", "doc"),
                human(1, "pronto", "doc"));
        startRound(sim);
        Combatant shooter = sim.combatants()[0];
        Combatant target = sim.combatants()[1];
        place(sim.combatants()[2], 15, 9, 0);
        // Support slugs aim at a wounded ally; the others at an enemy standing in the open.
        place(shooter, -4, 0, Math.PI / 2);
        place(target, 4, 0, -Math.PI / 2);
        if (support) {
            sim.arena().damage(null, target, 100, com.bajoterra.core.slug.Element.NEUTRAL, 0);
        }
        double hpBefore = target.vitals.hp();
        double shieldBefore = target.vitals.shield();
        shooter.input.aimYaw = Math.PI / 2;
        shooter.input.fire = true;

        // Long enough for a slug with a fuse (a grenade, a hovering flash) to go off too.
        ticks(sim, 150);

        assertThat(count(sim, EventType.SHOT)).as("fires").isGreaterThanOrEqualTo(1);
        assertThat(count(sim, EventType.PROJECTILE_HIT) + count(sim, EventType.PROJECTILE_WORLD_HIT)
                + count(sim, EventType.PROJECTILE_EXPIRED)).as("the slug lands somewhere").isGreaterThanOrEqualTo(1);
        if (slug.onHit() != com.bajoterra.core.effect.Effect.NONE) {
            assertThat(count(sim, EventType.PROJECTILE_HIT)).as("hits the target").isGreaterThanOrEqualTo(1);
            boolean changed = target.vitals.hp() != hpBefore || target.vitals.shield() != shieldBefore
                    || target.vitals.overshield() > 0
                    || count(sim, EventType.STATUS_APPLIED) > 0 || count(sim, EventType.KNOCKBACK) > 0;
            assertThat(changed).as("the effect is visible").isTrue();
        }
        if (support) {
            assertThat(target.vitals.hp() + target.vitals.shield() + target.vitals.overshield())
                    .as("support slugs only help").isGreaterThanOrEqualTo(hpBefore + shieldBefore);
        }
    }

    @ParameterizedTest(name = "slug {0}")
    @MethodSource("slugs")
    void slugRoleAgreesWithItsTargeting(SlugDefinition slug) {
        boolean helpsAllies = slug.targeting() == TargetingRule.ALLIES
                || slug.targeting() == TargetingRule.ALLIES_AND_SELF;
        assertThat(slug.role() == SlugRole.SUPPORT).isEqualTo(helpsAllies);
        assertThat(slug.projectile().speed()).isBetween(1.0, 200.0);
        assertThat(slug.fireDelayMultiplier()).isPositive();
    }

    @ParameterizedTest(name = "character {0}")
    @MethodSource("characters")
    void everyCharacterIsPlayable(CharacterDefinition character) {
        assertThat(character.moves()).as("move set").isNotEmpty();
        assertThat(character.blaster().reloadShots()).isLessThan(Loadout.SIZE);
        GameSimulation sim = scripted("test_open", 5, human(0, character.id(), "doc"), human(1, "eli", "doc"));
        startRound(sim);
        Combatant c = sim.combatants()[0];
        c.input.moveZ = 1;
        c.input.aimYaw = Math.PI / 2;
        for (int slot = 0; slot < character.moves().size(); slot++) {
            c.input.moveMask = 1 << slot;
            ticks(sim, 90);
            c.input.moveMask = 0;
            ticks(sim, 30);
        }
        assertThat(c.alive).isTrue();
        assertThat(c.pos.x).isGreaterThan(-20).isLessThan(20);
    }

    @ParameterizedTest(name = "map {0}")
    @MethodSource("maps")
    void everyMapIsPlayable(GameMap map) {
        assertThat(map.spawns()).hasSize(2);
        assertThat(map.spawns().get(0)).hasSizeGreaterThanOrEqualTo(3);
        assertThat(map.spawns().get(1)).hasSizeGreaterThanOrEqualTo(3);
        assertThat(map.nav().size()).isPositive();
        GameSimulation sim = new GameSimulation(CONTENT,
                com.bajoterra.core.sim.MatchConfig.builder(map.id()).seed(3).build());
        sim.start();
        ticks(sim, 600);
        assertThat(sim.tickCount()).isEqualTo(600);
    }
}
