package com.bajoterra.core.projectile;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.combatant;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.Fixtures;
import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.World;
import org.junit.jupiter.api.Test;

/** Grenuke, the sticky grenade slug: it clings to what it touches and explodes when its fuse burns out. */
class StickySlugTest {

    private static final SlugDefinition GRENUKE = CONTENT.slugs().require("grenuke");

    /** A thrower, an enemy and an ally of the thrower on the open test field. */
    private static final class Rig {
        final Combatant thrower = combatant(0, 0, "eli", "doc");
        final Combatant enemy = combatant(1, 1, "eli", "doc");
        final Combatant ally = combatant(2, 0, "eli", "doc");
        final CombatArena arena = Fixtures.arena(thrower, enemy, ally);
        final ProjectileSystem projectiles = new ProjectileSystem(arena,
                new World(CONTENT.maps().require("test_open")), new ProjectilePool(8));

        Projectile throwAt(double dx, double dy, double dz) {
            return projectiles.spawn(thrower, GRENUKE,
                    new Vec3(thrower.pos.x, 1.3, thrower.pos.z), new Vec3(dx, dy, dz).normalize());
        }

        void step(int ticks) {
            for (int i = 0; i < ticks; i++) {
                arena.advanceTick();
                projectiles.step();
            }
        }

        int stepUntilStuck(Projectile p) {
            int ticks = 0;
            while (p.active && !p.stuck && ticks < 600) {
                step(1);
                ticks++;
            }
            return ticks;
        }

        GameEvent first(EventType type) {
            for (long seq = arena.events().oldestAvailable(); seq < arena.events().written(); seq++) {
                if (arena.events().at(seq).type == type) {
                    return arena.events().at(seq);
                }
            }
            return null;
        }
    }

    @Test
    void grenukeIsLoadedAsAStickyGrenadeWithABlast() {
        assertThat(GRENUKE.projectile().sticky()).isTrue();
        assertThat(GRENUKE.projectile().fuseTicks()).isEqualTo(96);
        assertThat(GRENUKE.onExpire().areaRadius()).isEqualTo(3.5);
        assertThat(CONTENT.slugs().require("flaringo").projectile().sticky()).isFalse();
    }

    @Test
    void aGrenadeThatLandsOnTheFloorStaysThereAndExplodesWhenTheFuseRunsOut() {
        Rig rig = new Rig();
        rig.thrower.pos.set(-10, 0, 5);
        rig.enemy.pos.set(15, 0, -8);
        rig.ally.pos.set(-12, 0, 8);
        Projectile grenade = rig.throwAt(1, 0.2, 0);

        rig.stepUntilStuck(grenade);
        assertThat(grenade.stuck).as("it stuck instead of vanishing").isTrue();
        assertThat(grenade.active).isTrue();
        assertThat(grenade.stuckTo).isNull();
        assertThat(rig.first(EventType.PROJECTILE_WORLD_HIT)).isNotNull();
        double landedX = grenade.pos.x;
        double landedZ = grenade.pos.z;
        // The enemy walks right up to it while the fuse burns.
        rig.enemy.pos.set(landedX + 1, 0, landedZ);

        rig.step(GRENUKE.projectile().fuseTicks() - 1);
        assertThat(grenade.active).as("still burning one tick before the end").isTrue();
        assertThat(grenade.pos.x).isEqualTo(landedX);
        assertThat(rig.first(EventType.EXPLOSION)).isNull();

        rig.step(1);
        assertThat(grenade.active).isFalse();
        GameEvent blast = rig.first(EventType.EXPLOSION);
        assertThat(blast).isNotNull();
        assertThat(blast.a).as("the explosion names the slug").isEqualTo(GRENUKE.index());
        assertThat(blast.x).isCloseTo(landedX, within(1e-9));
        assertThat(rig.enemy.damageTaken).isGreaterThan(20);
        assertThat(rig.thrower.damageTaken).isZero();
    }

    @Test
    void aGrenadeThatHitsAnEnemySticksToThemFollowsThemAndBlowsUpOnThem() {
        Rig rig = new Rig();
        rig.thrower.pos.set(-4, 0, 0);
        rig.enemy.pos.set(0, 0, 0);
        rig.ally.pos.set(-15, 0, 9);
        Projectile grenade = rig.throwAt(1, 0.05, 0);

        rig.stepUntilStuck(grenade);
        assertThat(grenade.stuckTo).isSameAs(rig.enemy);
        GameEvent hit = rig.first(EventType.PROJECTILE_HIT);
        assertThat(hit.subject).isEqualTo(rig.enemy.id);
        double offsetX = grenade.pos.x - rig.enemy.pos.x;

        // The enemy runs away; the grenade goes with them.
        rig.enemy.pos.set(8, 0, 3);
        rig.step(1);
        assertThat(grenade.pos.x - rig.enemy.pos.x).isCloseTo(offsetX, within(1e-9));
        assertThat(grenade.pos.z).isCloseTo(3, within(0.5));

        rig.step(GRENUKE.projectile().fuseTicks());
        assertThat(grenade.active).isFalse();
        // Point blank: shield 50 + hp 100 take nearly the whole 34.
        assertThat(rig.enemy.damageTaken).isGreaterThan(30);
    }

    @Test
    void aGrenadeOnACombatantWhoFallsStaysWhereTheyFell() {
        Rig rig = new Rig();
        rig.thrower.pos.set(-4, 0, 0);
        rig.enemy.pos.set(0, 0, 0);
        rig.ally.pos.set(-15, 0, 9);
        Projectile grenade = rig.throwAt(1, 0.05, 0);
        rig.stepUntilStuck(grenade);

        rig.arena.damage(null, rig.enemy, 1000, Element.NEUTRAL, 0);
        rig.step(1);
        double x = grenade.pos.x;
        rig.enemy.pos.set(9, 0, 9);
        rig.step(5);

        assertThat(grenade.stuckTo).isNull();
        assertThat(grenade.pos.x).isEqualTo(x);
    }

    @Test
    void aStuckGrenadeIsNotSweptAgainAndCannotHitAnybodyElse() {
        Rig rig = new Rig();
        rig.thrower.pos.set(-10, 0, 5);
        rig.enemy.pos.set(15, 0, -8);
        rig.ally.pos.set(-12, 0, 8);
        Projectile grenade = rig.throwAt(1, 0.2, 0);
        rig.stepUntilStuck(grenade);
        // An enemy standing on top of a stuck grenade is not "hit" by it, only caught in the blast later.
        rig.enemy.pos.set(grenade.pos.x, 0, grenade.pos.z);

        rig.step(10);

        assertThat(rig.first(EventType.PROJECTILE_HIT)).isNull();
        assertThat(grenade.vel.lengthSq()).isZero();
    }
}
