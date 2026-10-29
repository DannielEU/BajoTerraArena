package com.bajoterra.core.combat;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.combatant;
import static com.bajoterra.core.Fixtures.effect;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.Fixtures;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.projectile.ProjectileSystem;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.World;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

/** Headshots: a direct hit on the top of an enemy's hitbox deals {@link CriticalHits#MULTIPLIER} times the damage. */
class CriticalHitTest {

    private static final SlugDefinition PROBE = SlugDefinition.builder("probe")
            .projectile(new ProjectileSpec(45, 0, 0.1, 600, 0))
            .onHit(effect("{\"type\":\"damage\",\"amount\":20}"))
            .build();

    private record Shot(double damage, GameEvent hit, GameEvent damageEvent) {
    }

    private static Shot shootAt(double height, int targetTeam, SlugDefinition slug, boolean crouched) {
        Combatant shooter = combatant(0, 0, "eli", "doc");
        Combatant target = combatant(1, targetTeam, "eli", "doc");
        shooter.pos.set(-6, 0, 0);
        target.pos.set(0, 0, 0);
        if (crouched) {
            target.stats.hitboxHeightMultiplier = 0.6;
        }
        CombatArena arena = Fixtures.arena(shooter, target);
        ProjectileSystem projectiles = new ProjectileSystem(arena, new World(CONTENT.maps().require("test_open")),
                new ProjectilePool(4));
        projectiles.spawn(shooter, slug, new Vec3(-6, height, 0), new Vec3(1, 0, 0));
        for (int i = 0; i < 120 && projectiles.pool().activeCount() > 0; i++) {
            arena.advanceTick();
            projectiles.step();
        }
        GameEvent hit = null;
        GameEvent damage = null;
        for (long seq = arena.events().oldestAvailable(); seq < arena.events().written(); seq++) {
            GameEvent event = arena.events().at(seq);
            if (event.type == EventType.PROJECTILE_HIT && hit == null) {
                hit = event;
            } else if (event.type == EventType.DAMAGE && damage == null) {
                damage = event;
            }
        }
        return new Shot(target.damageTaken, hit, damage);
    }

    @ParameterizedTest(name = "a slug at {0} m on an 1.8 m hero: critical = {1}")
    @CsvSource({"1.0, false", "1.4, false", "1.5, true", "1.75, true"})
    void onlyTheTopFifthOfTheHitboxIsTheHead(double height, boolean critical) {
        Shot shot = shootAt(height, 1, PROBE, false);

        assertThat(shot.hit()).isNotNull();
        assertThat(shot.hit().b).isEqualTo(critical ? 1 : 0);
        assertThat(shot.damageEvent().b).isEqualTo(critical ? 1 : 0);
        assertThat(shot.damage()).isCloseTo(critical ? 30.0 : 20.0, within(1e-9));
    }

    @Test
    void theHeadGoesDownWithACrouch() {
        // Crouched, Eli is 1.08 m tall: 1.0 m is now his head, 1.5 m flies over him.
        assertThat(shootAt(1.0, 1, PROBE, true).hit().b).isEqualTo(1);
        assertThat(shootAt(1.5, 1, PROBE, true).hit()).isNull();
    }

    @Test
    void slugsThatHelpAlliesNeverCrit() {
        SlugDefinition friendly = SlugDefinition.builder("friendly")
                .targeting(TargetingRule.ANY)
                .projectile(new ProjectileSpec(45, 0, 0.1, 600, 0))
                .onHit(effect("{\"type\":\"damage\",\"amount\":20}"))
                .build();

        Shot shot = shootAt(1.7, 0, friendly, false);

        assertThat(shot.hit().b).as("a teammate's head is not a weak spot").isZero();
    }

    @Test
    void onlyTheDirectHitIsCriticalNotTheRestOfAnArea() {
        Combatant shooter = combatant(0, 0, "eli", "doc");
        Combatant victim = combatant(1, 1, "eli", "doc");
        Combatant bystander = combatant(2, 1, "eli", "doc");
        victim.pos.set(0, 0, 0);
        bystander.pos.set(1.5, 0, 0);
        CombatArena arena = Fixtures.arena(shooter, victim, bystander);
        var blast = effect("{\"type\":\"area\",\"radius\":3,\"falloff\":\"NONE\",\"effects\":"
                + "[{\"type\":\"damage\",\"amount\":10}]}");
        var ctx = new EffectContext().set(arena, shooter, victim, null,
                TargetingRule.ENEMIES, new Vec3(0, 1.7, 0), null);
        ctx.setCriticalTarget(victim);

        blast.apply(ctx);

        assertThat(victim.damageTaken).isCloseTo(15, within(1e-9));
        assertThat(bystander.damageTaken).isCloseTo(10, within(1e-9));
    }
}
