package com.bajoterra.core.effect;

import static com.bajoterra.core.Fixtures.apply;
import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static com.bajoterra.core.Fixtures.effect;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.ContentException;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.Element;
import org.junit.jupiter.api.Test;

class EffectsTest {

    private final Combatant caster = combatant(0, 0, "eli", "doc");
    private final Combatant ally = combatant(1, 0, "eli", "doc");
    private final Combatant enemy = combatant(2, 1, "eli", "doc");
    private final Combatant enemyTwo = combatant(3, 1, "eli", "doc");
    private final CombatArena arena = arena(caster, ally, enemy, enemyTwo);

    private void tickStatuses(Combatant c, int ticks) {
        for (int i = 0; i < ticks; i++) {
            c.stats.reset();
            c.statuses.modify(c.stats);
            c.statuses.tick(c, arena);
            arena.advanceTick();
        }
    }

    @Test
    void damageUsesTheSlugElementWhenNoneIsGiven() {
        apply(arena, effect("{\"type\":\"damage\",\"amount\":10,\"element\":\"FIRE\"}"), caster, enemy);

        assertThat(enemy.vitals.shield()).isEqualTo(40);
    }

    @Test
    void healScalesWithTheCastersPotency() {
        Combatant trixie = combatant(4, 0, "trixie", "doc"); // potency 1.25
        CombatArena local = arena(trixie, ally);
        local.damage(null, ally, 100, Element.NEUTRAL, 1.0);

        apply(local, effect("{\"type\":\"heal\",\"amount\":20}"), trixie, ally);

        assertThat(ally.vitals.hp()).isCloseTo(25, within(1e-9));
    }

    @Test
    void restoreShieldFillsTheShield() {
        arena.damage(null, ally, 40, Element.NEUTRAL, 0);

        apply(arena, effect("{\"type\":\"restore_shield\",\"amount\":25}"), caster, ally);

        assertThat(ally.vitals.shield()).isEqualTo(35);
    }

    @Test
    void overshieldAbsorbsDamageAndFades() {
        apply(arena, effect("{\"type\":\"overshield\",\"amount\":40,\"durationTicks\":10}"), caster, ally);
        assertThat(ally.vitals.overshield()).isEqualTo(40);

        arena.damage(enemy, ally, 30, Element.NEUTRAL, 0);

        assertThat(ally.vitals.shield()).isEqualTo(50);
        assertThat(ally.vitals.overshield()).isEqualTo(10);
    }

    @Test
    void burnDealsDamageOverTimeAndExpires() {
        apply(arena, effect("{\"type\":\"burn\",\"dps\":6,\"durationSeconds\":2}"), caster, enemy);
        assertThat(enemy.statuses.has("burn")).isTrue();

        tickStatuses(enemy, 60);
        double afterOneSecond = 50 - enemy.vitals.shield();
        tickStatuses(enemy, 80);

        assertThat(afterOneSecond).isCloseTo(6, within(0.5));
        assertThat(enemy.statuses.has("burn")).isFalse();
        assertThat(50 - enemy.vitals.shield()).isCloseTo(12, within(0.5));
    }

    @Test
    void slowReducesSpeedWhileActive() {
        apply(arena, effect("{\"type\":\"slow\",\"factor\":0.4,\"durationTicks\":30}"), caster, enemy);

        tickStatuses(enemy, 1);
        enemy.stats.reset();
        enemy.statuses.modify(enemy.stats);
        assertThat(enemy.stats.speedMultiplier).isEqualTo(0.4);

        tickStatuses(enemy, 40);
        enemy.stats.reset();
        enemy.statuses.modify(enemy.stats);
        assertThat(enemy.stats.speedMultiplier).isEqualTo(1.0);
    }

    @Test
    void freezeSlowsAndMakesTheTargetVulnerableToFire() {
        apply(arena, effect("{\"type\":\"freeze\",\"durationTicks\":60}"), caster, enemy);
        enemy.stats.reset();
        enemy.statuses.modify(enemy.stats);

        assertThat(enemy.stats.speedMultiplier).isCloseTo(0.15, within(1e-9));
        assertThat(enemy.stats.elementTakenMultiplier(Element.FIRE)).isCloseTo(1.25, within(1e-9));
        assertThat(enemy.stats.elementTakenMultiplier(Element.WATER)).isEqualTo(1.0);
    }

    @Test
    void stunSetsTheStunnedFlag() {
        apply(arena, effect("{\"type\":\"stun\",\"durationTicks\":30}"), caster, enemy);
        enemy.stats.reset();
        enemy.statuses.modify(enemy.stats);

        assertThat(enemy.stats.stunned).isTrue();
    }

    @Test
    void regenHealsGraduallyAndIsScaledByPotency() {
        arena.damage(null, ally, 100, Element.NEUTRAL, 1.0);
        double before = ally.vitals.hp();

        apply(arena, effect("{\"type\":\"regen\",\"hps\":12,\"durationSeconds\":3}"), caster, ally);
        tickStatuses(ally, 180);

        assertThat(ally.vitals.hp() - before).isCloseTo(36, within(1.0));
    }

    @Test
    void blindAddsAimSpread() {
        apply(arena, effect("{\"type\":\"blind\",\"spread\":0.3,\"durationTicks\":30}"), caster, enemy);
        enemy.stats.reset();
        enemy.statuses.modify(enemy.stats);

        assertThat(enemy.stats.aimSpread).isCloseTo(0.3, within(1e-9));
    }

    @Test
    void refreshStackingRestartsTheDurationWithoutDuplicating() {
        Effect burn = effect("{\"type\":\"burn\",\"dps\":6,\"durationTicks\":60,\"stacking\":\"REFRESH\"}");
        apply(arena, burn, caster, enemy);
        tickStatuses(enemy, 40);

        apply(arena, burn, caster, enemy);

        int active = 0;
        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            if (enemy.statuses.at(i).active()) {
                active++;
                assertThat(enemy.statuses.at(i).remainingTicks()).isEqualTo(60);
            }
        }
        assertThat(active).isEqualTo(1);
    }

    @Test
    void stackingPolicyAddsStacksUpToTheLimit() {
        Effect burn = effect("{\"type\":\"burn\",\"dps\":6,\"durationTicks\":60,"
                + "\"stacking\":\"STACK\",\"maxStacks\":2}");

        apply(arena, burn, caster, enemy);
        apply(arena, burn, caster, enemy);
        apply(arena, burn, caster, enemy);

        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            if (enemy.statuses.at(i).active()) {
                assertThat(enemy.statuses.at(i).stacks()).isEqualTo(2);
            }
        }
    }

    @Test
    void ignoreStackingKeepsTheRunningInstance() {
        Effect burn = effect("{\"type\":\"burn\",\"dps\":6,\"durationTicks\":60,\"stacking\":\"IGNORE\"}");
        apply(arena, burn, caster, enemy);
        tickStatuses(enemy, 30);

        apply(arena, burn, caster, enemy);

        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            if (enemy.statuses.at(i).active()) {
                assertThat(enemy.statuses.at(i).remainingTicks()).isEqualTo(30);
            }
        }
    }

    @Test
    void replaceIfStrongerOnlyReplacesWithAStrongerStatus() {
        Effect weak = effect("{\"type\":\"slow\",\"factor\":0.8,\"durationTicks\":60,"
                + "\"stacking\":\"REPLACE_IF_STRONGER\"}");
        Effect strong = effect("{\"type\":\"slow\",\"factor\":0.3,\"durationTicks\":60,"
                + "\"stacking\":\"REPLACE_IF_STRONGER\"}");

        apply(arena, strong, caster, enemy);
        apply(arena, weak, caster, enemy);
        enemy.stats.reset();
        enemy.statuses.modify(enemy.stats);

        assertThat(enemy.stats.speedMultiplier).isEqualTo(0.3);
    }

    @Test
    void cleanseRemovesStatusesWithTheTag() {
        apply(arena, effect("{\"type\":\"burn\",\"dps\":6,\"durationTicks\":60}"), enemy, ally);
        apply(arena, effect("{\"type\":\"slow\",\"factor\":0.5,\"durationTicks\":60}"), enemy, ally);

        apply(arena, effect("{\"type\":\"cleanse\",\"tags\":[\"burn\"]}"), caster, ally);

        assertThat(ally.statuses.has("burn")).isFalse();
        assertThat(ally.statuses.has("slow")).isTrue();
    }

    @Test
    void conditionalPicksTheBranchFromTheTargetState() {
        Effect conditional = effect("{\"type\":\"conditional\",\"ifTargetHasStatus\":\"burn\","
                + "\"then\":[{\"type\":\"damage\",\"amount\":30}],\"else\":[{\"type\":\"damage\",\"amount\":5}]}");

        apply(arena, conditional, caster, enemy);
        assertThat(enemy.vitals.shield()).isEqualTo(45);

        apply(arena, effect("{\"type\":\"burn\",\"dps\":1,\"durationTicks\":60}"), caster, enemy);
        apply(arena, conditional, caster, enemy);
        assertThat(enemy.vitals.shield()).isEqualTo(15);
    }

    @Test
    void conditionalCanCheckRemainingHealth() {
        Effect finisher = effect("{\"type\":\"conditional\",\"ifTargetHpBelow\":0.5,"
                + "\"then\":[{\"type\":\"damage\",\"amount\":99}]}");
        apply(arena, finisher, caster, enemy);
        assertThat(enemy.vitals.hp()).isEqualTo(100);

        arena.damage(null, enemy, 90, Element.NEUTRAL, 1.0);
        apply(arena, finisher, caster, enemy);
        assertThat(enemy.alive).isFalse();
    }

    @Test
    void areaAffectsOnlyValidTargetsInsideTheRadiusWithFalloff() {
        enemy.pos.set(0, 0, 0);
        enemyTwo.pos.set(2, 0, 0);
        ally.pos.set(1, 0, 0);
        caster.pos.set(-1, 0, 0);
        Effect explosion = effect("{\"type\":\"area\",\"radius\":3,\"falloff\":\"LINEAR\","
                + "\"effects\":[{\"type\":\"damage\",\"amount\":20}]}");
        com.bajoterra.core.effect.EffectContext ctx = new com.bajoterra.core.effect.EffectContext().set(arena,
                caster, null, null, com.bajoterra.core.slug.TargetingRule.ENEMIES,
                new com.bajoterra.core.math.Vec3(0, 0.5, 0), null);

        explosion.apply(ctx);

        double nearDamage = 50 - enemy.vitals.shield();
        double farDamage = 50 - enemyTwo.vitals.shield();
        assertThat(nearDamage).isGreaterThan(farDamage);
        assertThat(farDamage).isGreaterThan(0);
        assertThat(ally.vitals.shield()).isEqualTo(50); // allies untouched by an enemy-targeting area
    }

    @Test
    void chainJumpsToTheNearestOtherEnemyWithDecay() {
        enemy.pos.set(0, 0, 0);
        enemyTwo.pos.set(3, 0, 0);
        Effect chain = effect("{\"type\":\"chain\",\"radius\":5,\"jumps\":1,\"decay\":0.5,"
                + "\"effects\":[{\"type\":\"damage\",\"amount\":20}]}");
        com.bajoterra.core.effect.EffectContext ctx = new com.bajoterra.core.effect.EffectContext().set(arena,
                caster, enemy, null, com.bajoterra.core.slug.TargetingRule.ENEMIES, enemy.pos, null);

        chain.apply(ctx);

        assertThat(ctx.target()).isSameAs(enemy);
        assertThat(ctx.scale()).isEqualTo(1.0);
        assertThat(enemy.vitals.shield()).isEqualTo(50); // the chain does not hit the first target again
        assertThat(enemyTwo.vitals.shield()).isEqualTo(40); // 20 * 0.5
    }

    @Test
    void unknownEffectTypesAndBadParametersAreReported() {
        assertThatThrownBy(() -> effect("{\"type\":\"explode_everything\"}"))
                .isInstanceOf(ContentException.class)
                .hasMessageContaining("unknown effect type");
        assertThatThrownBy(() -> effect("{\"type\":\"damage\",\"amount\":-5}"))
                .isInstanceOf(ContentException.class)
                .hasMessageContaining("amount");
        assertThatThrownBy(() -> effect("{\"type\":\"burn\",\"dps\":3}"))
                .isInstanceOf(ContentException.class)
                .hasMessageContaining("duration");
    }
}
