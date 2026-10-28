package com.bajoterra.core.effect;

import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static com.bajoterra.core.Fixtures.effect;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.TargetingRule;
import org.junit.jupiter.api.Test;

/** Exact numbers of the spatial effects: area falloff, chains and knockback directions. */
class GeometryEffectsTest {

    private final Combatant caster = combatant(0, 0, "eli", "doc");
    private final Combatant e1 = combatant(1, 1, "eli", "doc");
    private final Combatant e2 = combatant(2, 1, "eli", "doc");
    private final Combatant e3 = combatant(3, 1, "eli", "doc");
    private final Combatant ally = combatant(4, 0, "eli", "doc");
    private final CombatArena arena = arena(caster, e1, e2, e3, ally);

    private EffectContext context(Combatant target, TargetingRule rule, Vec3 point, Vec3 direction) {
        return new EffectContext().set(arena, caster, target, null, rule, point, direction);
    }

    private double lost(Combatant c) {
        return c.vitals.maxShield() - c.vitals.shield();
    }

    private GameEvent lastEvent(EventType type) {
        EventBuffer buffer = arena.events();
        GameEvent found = null;
        for (long seq = buffer.oldestAvailable(); seq < buffer.written(); seq++) {
            if (buffer.at(seq).type == type) {
                found = buffer.at(seq);
            }
        }
        return found;
    }

    // ------------------------------------------------------------------------------------------ area

    @Test
    void linearFalloffScalesDamageByDistanceFromTheHitbox() {
        // Centre at the origin, 1.4 m above the ground; the radius is 4. e1 stands 1.4 m away (0.4 m body radius).
        e1.pos.set(1.4, 0, 0);
        e2.pos.set(2.4, 0, 0);
        e3.pos.set(3.4, 0, 0);
        Effect area = effect("{\"type\":\"area\",\"radius\":4,\"falloff\":\"LINEAR\",\"minScale\":0,"
                + "\"effects\":[{\"type\":\"damage\",\"amount\":40}]}");

        area.apply(context(null, TargetingRule.ENEMIES, new Vec3(0, 0.5, 0), null));

        // distance to the hitbox = centre distance - 0.4 -> 1.0, 2.0, 3.0 m: scales 0.75, 0.5, 0.25
        assertThat(lost(e1)).isCloseTo(40 * 0.75, within(1e-9));
        assertThat(lost(e2)).isCloseTo(40 * 0.5, within(1e-9));
        assertThat(lost(e3)).isCloseTo(40 * 0.25, within(1e-9));
    }

    @Test
    void noFalloffAppliesFullMagnitudeUpToTheRadiusAndNotBeyond() {
        e1.pos.set(3.9, 0, 0);     // hitbox edge at 3.5 m: inside
        e2.pos.set(4.5, 0, 0);     // hitbox edge at 4.1 m: outside
        Effect area = effect("{\"type\":\"area\",\"radius\":4,\"falloff\":\"NONE\","
                + "\"effects\":[{\"type\":\"damage\",\"amount\":10}]}");

        area.apply(context(null, TargetingRule.ENEMIES, new Vec3(0, 0.5, 0), null));

        assertThat(lost(e1)).isEqualTo(10);
        assertThat(lost(e2)).isZero();
    }

    @Test
    void theMinimumScaleBoundsTheFalloff() {
        e1.pos.set(3.9, 0, 0);     // right at the edge
        Effect area = effect("{\"type\":\"area\",\"radius\":4,\"falloff\":\"LINEAR\",\"minScale\":0.4,"
                + "\"effects\":[{\"type\":\"damage\",\"amount\":10}]}");

        area.apply(context(null, TargetingRule.ENEMIES, new Vec3(0, 0.5, 0), null));

        assertThat(lost(e1)).isGreaterThanOrEqualTo(4.0 - 1e-9);
        assertThat(lost(e1)).isLessThan(5.0);
    }

    @Test
    void areaAnnouncesItselfAndRestoresTheContext() {
        e1.pos.set(1, 0, 0);
        Effect area = effect("{\"type\":\"area\",\"radius\":3,\"effects\":[{\"type\":\"damage\",\"amount\":1}]}");
        EffectContext ctx = context(e2, TargetingRule.ENEMIES, new Vec3(2, 0.5, 5), null);
        ctx.setScale(0.5);

        area.apply(ctx);

        GameEvent explosion = lastEvent(EventType.EXPLOSION);
        assertThat(explosion.actor).isEqualTo(0);
        assertThat(explosion.x).isEqualTo(2);
        assertThat(explosion.y).isEqualTo(0.5);
        assertThat(explosion.z).isEqualTo(5);
        assertThat(explosion.value).isEqualTo(3);
        assertThat(ctx.target()).isSameAs(e2);
        assertThat(ctx.scale()).isEqualTo(0.5);
    }

    @Test
    void theContextScaleMultipliesTheAreaMagnitude() {
        e1.pos.set(0, 0, 0);
        Effect area = effect("{\"type\":\"area\",\"radius\":3,\"falloff\":\"NONE\","
                + "\"effects\":[{\"type\":\"damage\",\"amount\":20}]}");
        EffectContext ctx = context(null, TargetingRule.ENEMIES, new Vec3(0, 0.5, 0), null);
        ctx.setScale(0.5);

        area.apply(ctx);

        assertThat(lost(e1)).isEqualTo(10);
    }

    @Test
    void areaSkipsTheDeadAndHonoursTheTargetingRule() {
        e1.pos.set(0, 0, 0);
        ally.pos.set(0.5, 0, 0);
        caster.pos.set(-0.5, 0, 0);
        arena.damage(caster, e2, 10_000, com.bajoterra.core.slug.Element.NEUTRAL, 0);
        e2.pos.set(1, 0, 0);
        Effect area = effect("{\"type\":\"area\",\"radius\":3,\"falloff\":\"NONE\","
                + "\"effects\":[{\"type\":\"heal\",\"amount\":1},{\"type\":\"damage\",\"amount\":7}]}");

        area.apply(context(null, TargetingRule.ENEMIES, new Vec3(0, 0.5, 0), null));
        assertThat(lost(e1)).isEqualTo(7);
        assertThat(lost(ally)).isZero();
        assertThat(lost(caster)).isZero();

        area.apply(context(null, TargetingRule.ALLIES_AND_SELF, new Vec3(0, 0.5, 0), null));
        assertThat(lost(ally)).as("the rule picks who is reached; friendly fire is off").isZero();
        assertThat(lost(caster)).as("self damage is never blocked").isEqualTo(7);
    }

    @Test
    void anAreaWithoutASourceAffectsEveryone() {
        e1.pos.set(0, 0, 0);
        ally.pos.set(0.5, 0, 0);
        Effect area = effect("{\"type\":\"area\",\"radius\":3,\"falloff\":\"NONE\","
                + "\"effects\":[{\"type\":\"damage\",\"amount\":5}]}");
        EffectContext ctx = new EffectContext().set(arena, null, null, null, TargetingRule.ENEMIES,
                new Vec3(0, 0.5, 0), null);

        area.apply(ctx);

        assertThat(lost(e1)).isEqualTo(5);
        assertThat(lost(ally)).isEqualTo(5);
    }

    // ------------------------------------------------------------------------------------------ chain

    @Test
    void aChainWithSeveralJumpsDecaysMultiplicativelyAndNeverRevisits() {
        e1.pos.set(0, 0, 0);
        e2.pos.set(2, 0, 0);
        e3.pos.set(4, 0, 0);
        Effect chain = effect("{\"type\":\"chain\",\"radius\":3,\"jumps\":3,\"decay\":0.5,"
                + "\"effects\":[{\"type\":\"damage\",\"amount\":40}]}");

        chain.apply(context(e1, TargetingRule.ENEMIES, e1.pos, null));

        assertThat(lost(e1)).as("the original target is hit by the slug itself, not the chain").isZero();
        assertThat(lost(e2)).isEqualTo(20);      // 40 * 0.5
        assertThat(lost(e3)).isEqualTo(10);      // 40 * 0.5 * 0.5
        assertThat(lost(ally)).isZero();
    }

    @Test
    void aChainStopsWhenNothingIsInRange() {
        e1.pos.set(0, 0, 0);
        e2.pos.set(3.01, 0, 0);
        Effect chain = effect("{\"type\":\"chain\",\"radius\":3,\"jumps\":2,\"decay\":1,"
                + "\"effects\":[{\"type\":\"damage\",\"amount\":10}]}");

        chain.apply(context(e1, TargetingRule.ENEMIES, e1.pos, null));

        assertThat(lost(e2)).isZero();
    }

    @Test
    void aChainHopsToTheNearestValidTargetNotTheFirstInTheList() {
        e1.pos.set(0, 0, 0);
        e2.pos.set(2.5, 0, 0);
        e3.pos.set(1.0, 0, 0);
        Effect chain = effect("{\"type\":\"chain\",\"radius\":3,\"jumps\":1,\"decay\":1,"
                + "\"effects\":[{\"type\":\"damage\",\"amount\":10}]}");

        chain.apply(context(e1, TargetingRule.ENEMIES, e1.pos, null));

        assertThat(lost(e3)).isEqualTo(10);
        assertThat(lost(e2)).isZero();
    }

    @Test
    void aChainWithoutATargetDoesNothing() {
        Effect chain = effect("{\"type\":\"chain\",\"radius\":3,\"effects\":[{\"type\":\"damage\",\"amount\":10}]}");

        chain.apply(context(null, TargetingRule.ENEMIES, new Vec3(), null));

        assertThat(lost(e1) + lost(e2) + lost(e3)).isZero();
    }

    // ------------------------------------------------------------------------------------------ knockback

    @Test
    void knockbackPushesAwayFromTheImpactPoint() {
        e1.pos.set(2, 0, 0);
        Effect knockback = effect("{\"type\":\"knockback\",\"force\":10}");

        knockback.apply(context(e1, TargetingRule.ENEMIES, new Vec3(0, 1, 0), null));

        assertThat(e1.knock.x).isCloseTo(10, within(1e-9));
        assertThat(e1.knock.z).isCloseTo(0, within(1e-9));
    }

    @Test
    void knockbackFallsBackToTheTravelDirectionWhenTheImpactIsRightOnTheTarget() {
        e1.pos.set(5, 0, 5);
        Effect knockback = effect("{\"type\":\"knockback\",\"force\":8}");

        knockback.apply(context(e1, TargetingRule.ENEMIES, new Vec3(5, 1, 5), new Vec3(0, 0, 1)));

        assertThat(e1.knock.z).isCloseTo(8, within(1e-9));
        assertThat(e1.knock.x).isCloseTo(0, within(1e-9));
    }

    @Test
    void knockbackScalesWithTheContextScaleAndLiftsWhenAsked() {
        e1.pos.set(0, 0, 3);
        Effect knockback = effect("{\"type\":\"knockback\",\"force\":10,\"lift\":6}");
        EffectContext ctx = context(e1, TargetingRule.ENEMIES, new Vec3(0, 1, 0), null);
        ctx.setScale(0.5);

        knockback.apply(ctx);

        assertThat(e1.knock.z).isCloseTo(5, within(1e-9));
        assertThat(e1.vel.y).isCloseTo(3, within(1e-9));
        assertThat(e1.grounded).isFalse();
    }

    @Test
    void knockbackIgnoresMissingTargets() {
        Effect knockback = effect("{\"type\":\"knockback\",\"force\":10}");

        knockback.apply(context(null, TargetingRule.ENEMIES, new Vec3(), null));

        assertThat(lastEvent(EventType.KNOCKBACK)).isNull();
    }

    // ------------------------------------------------------------------------------------------ small effects

    @Test
    void conditionalNeedsAtLeastOneConditionAndHonoursBothWhenGiven() {
        Effect both = effect("{\"type\":\"conditional\",\"ifTargetHasStatus\":\"stun\",\"ifTargetHpBelow\":0.9,"
                + "\"then\":[{\"type\":\"damage\",\"amount\":10}]}");
        arena.damage(null, e1, 20, com.bajoterra.core.slug.Element.NEUTRAL, 1.0);   // 80 % health

        both.apply(context(e1, TargetingRule.ENEMIES, e1.pos, null));
        assertThat(lost(e1)).as("hp condition holds but there is no stun").isZero();

        effect("{\"type\":\"stun\",\"durationTicks\":30}").apply(context(e1, TargetingRule.ENEMIES, e1.pos, null));
        both.apply(context(e1, TargetingRule.ENEMIES, e1.pos, null));
        assertThat(lost(e1)).isEqualTo(10);
    }

    @Test
    void compositesAndRegistriesReportTheirShape() {
        Effect two = CONTENT_EFFECTS.createAll(java.util.List.of(
                com.bajoterra.core.Fixtures.spec("{\"type\":\"damage\",\"amount\":1}"),
                com.bajoterra.core.Fixtures.spec("{\"type\":\"heal\",\"amount\":1}")));
        Effect one = CONTENT_EFFECTS.createAll(java.util.List.of(
                com.bajoterra.core.Fixtures.spec("{\"type\":\"damage\",\"amount\":1}")));

        assertThat(two).isInstanceOf(CompositeEffect.class);
        assertThat(((CompositeEffect) two).size()).isEqualTo(2);
        assertThat(one).isNotInstanceOf(CompositeEffect.class);
        assertThat(CONTENT_EFFECTS.createAll(java.util.List.of())).isSameAs(Effect.NONE);
        assertThat(CONTENT_EFFECTS.knows("damage")).isTrue();
        assertThat(CONTENT_EFFECTS.knows("nonsense")).isFalse();
    }

    @Test
    void theContextExposesWhatItWasGiven() {
        Vec3 direction = new Vec3(0, 0, 1);
        EffectContext ctx = context(e1, TargetingRule.ALLIES, new Vec3(1, 2, 3), direction);

        assertThat(ctx.direction().z).isEqualTo(1);
        assertThat(ctx.point().y).isEqualTo(2);
        assertThat(ctx.targeting()).isEqualTo(TargetingRule.ALLIES);
        assertThat(ctx.source()).isSameAs(caster);
        assertThat(ctx.slug()).isNull();
        assertThat(ctx.sourcePotency()).isEqualTo(1.0);
        assertThat(new EffectContext().set(arena, null, null, null, TargetingRule.ANY, new Vec3(), null)
                .sourcePotency()).isEqualTo(1.0);
        assertThat(new EffectContext().set(arena, null, null, null, TargetingRule.ANY, new Vec3(), null)
                .direction().z).as("default direction is +Z").isEqualTo(1.0);
    }

    private static final EffectRegistry CONTENT_EFFECTS = com.bajoterra.core.Fixtures.CONTENT.effects();
}
