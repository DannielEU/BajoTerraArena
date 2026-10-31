package com.bajoterra.core.effect;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static com.bajoterra.core.Fixtures.effect;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Trig;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.projectile.ProjectileSystem;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.World;
import org.junit.jupiter.api.Test;

/** Phosphoro's flash: it blinds whoever looks at it, and only them. */
class FlashEffectTest {

    private static final SlugDefinition PHOSPHORO = CONTENT.slugs().require("phosphoro");
    private static final Effect FLASH = effect("{\"type\":\"flash\",\"radius\":20,\"viewAngle\":55,\"minScale\":0.3,"
            + "\"effects\":[{\"type\":\"blind\",\"spread\":0.4,\"durationTicks\":240,\"scaleDuration\":true,"
            + "\"stacking\":\"LONGEST\"}]}");

    private final Combatant thrower = combatant(0, 0, "eli", "doc");
    private final Combatant enemy = combatant(1, 1, "eli", "doc");
    private final Combatant ally = combatant(2, 0, "eli", "doc");
    private final CombatArena arena = arena(thrower, enemy, ally);

    /** Turns {@code c} to look at (x, y, z) from its eyes. */
    private static void lookAt(Combatant c, double x, double y, double z) {
        double eye = c.pos.y + c.hitHeight() * 0.9;
        c.yaw = Trig.atan2(x - c.pos.x, z - c.pos.z);
        c.pitch = Trig.atan2(y - eye, Trig.hypot(x - c.pos.x, z - c.pos.z));
    }

    private void flashAt(double x, double y, double z) {
        FLASH.apply(new EffectContext().set(arena, thrower, null, PHOSPHORO, TargetingRule.ENEMIES,
                new Vec3(x, y, z), null));
    }

    private static int blindTicks(Combatant c) {
        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            StatusInstance status = c.statuses.at(i);
            if (status.active() && status.definition().statusId().equals("blind")) {
                return status.remainingTicks();
            }
        }
        return 0;
    }

    private static double blindness(Combatant c) {
        c.stats.reset();
        c.statuses.modify(c.stats);
        return c.stats.blindness;
    }

    @Test
    void lookingStraightAtItFromCloseByBlindsCompletely() {
        enemy.pos.set(3, 0, 0);
        lookAt(enemy, 0, 1, 0);

        flashAt(0, 1, 0);

        assertThat(blindness(enemy)).isGreaterThan(0.85);
        assertThat(blindTicks(enemy)).isGreaterThan(200);
        assertThat(enemy.stats.aimSpread).isGreaterThan(0.3);
    }

    @Test
    void turningYourBackIsTheWayNotToBeBlinded() {
        enemy.pos.set(3, 0, 0);
        lookAt(enemy, 10, 1, 0);

        flashAt(0, 1, 0);

        assertThat(blindTicks(enemy)).isZero();
        assertThat(blindness(enemy)).isZero();
    }

    @Test
    void aFlashOffTheSideOfTheViewDoesNotBlind() {
        enemy.pos.set(0, 0, 8);
        lookAt(enemy, 10, 1.6, 8);   // the flash is 8 m to its side, out of the picture

        flashAt(0, 1, 0);

        assertThat(blindTicks(enemy)).isZero();
    }

    @Test
    void aSlugStuckOnYourChestOrAtYourFeetIsInPlainViewOfTheCamera() {
        enemy.pos.set(0, 0, 0);
        enemy.yaw = 0;
        enemy.pitch = 0;          // looking straight ahead, along +z
        flashAt(0, 1.0, 0.45);    // on its chest
        assertThat(blindTicks(enemy)).as("on the chest").isPositive();

        enemy.statuses.clear();
        flashAt(0.3, 0.3, 0.9);   // at its feet
        assertThat(blindTicks(enemy)).as("at the feet").isPositive();
    }

    @Test
    void aPlayerIsJudgedFromTheCameraTheirClientSends() {
        enemy.pos.set(0, 0, 0);
        enemy.yaw = 0;
        enemy.pitch = 0;
        enemy.input.hasCamera = true;
        enemy.input.camX = 0;
        enemy.input.camY = 30;    // a camera high above, looking along +z: the flash below is out of its picture
        enemy.input.camZ = -3;

        flashAt(0, 1.0, 0.45);

        assertThat(blindTicks(enemy)).isZero();
    }

    @Test
    void somethingThatBlocksSightBetweenTheEyesAndTheFlashProtects() {
        enemy.pos.set(3, 0, 0);
        lookAt(enemy, 0, 1, 0);
        arena.setSight((ax, ay, az, bx, by, bz) -> false);

        flashAt(0, 1, 0);

        assertThat(blindTicks(enemy)).isZero();
    }

    @Test
    void beyondItsRadiusItIsTooFarToBlind() {
        enemy.pos.set(21, 0, 0);
        lookAt(enemy, 0, 1, 0);

        flashAt(0, 1, 0);

        assertThat(blindTicks(enemy)).isZero();
    }

    @Test
    void itBlindsEveryoneWhoLooksIncludingAlliesAndTheThrower() {
        thrower.pos.set(-4, 0, 0);
        enemy.pos.set(4, 0, 0);
        ally.pos.set(0, 0, 4);
        lookAt(thrower, 0, 1, 0);
        lookAt(enemy, 0, 1, 0);
        lookAt(ally, 0, 1, 14);

        flashAt(0, 1, 0);

        assertThat(blindTicks(thrower)).as("the thrower stared at it").isPositive();
        assertThat(blindTicks(enemy)).isPositive();
        assertThat(blindTicks(ally)).as("the ally looked away").isZero();
    }

    @Test
    void seenFromTheCornerOfTheEyeOrFromFarAwayItBlindsForLess() {
        enemy.pos.set(3, 0, 0);
        lookAt(enemy, 0, 1, 0);
        ally.pos.set(0, 0, 15);
        lookAt(ally, 0, 1, 0);
        thrower.pos.set(-3, 0, 0);
        lookAt(thrower, -3 + Trig.cos(0.8), 1.6, Trig.sin(0.8));   // about 45 degrees off

        flashAt(0, 1, 0);

        int straight = blindTicks(enemy);
        assertThat(blindTicks(ally)).as("far away").isPositive().isLessThan(straight);
        assertThat(blindTicks(thrower)).as("at the edge of the view").isPositive().isLessThan(straight);
    }

    @Test
    void aWeakFlashDoesNotCutAStrongBlindnessShort() {
        enemy.pos.set(3, 0, 0);
        lookAt(enemy, 0, 1, 0);
        flashAt(0, 1, 0);
        int strong = blindTicks(enemy);

        enemy.pos.set(18, 0, 0);
        lookAt(enemy, 0, 1, 0);
        flashAt(0, 1, 0);

        assertThat(blindTicks(enemy)).isEqualTo(strong);
    }

    @Test
    void theFlashIsAnnouncedWithItsRadius() {
        flashAt(2, 1, 3);

        GameEvent event = null;
        for (long seq = arena.events().oldestAvailable(); seq < arena.events().written(); seq++) {
            if (arena.events().at(seq).type == EventType.FLASH) {
                event = arena.events().at(seq);
            }
        }
        assertThat(event).isNotNull();
        assertThat(event.value).isEqualTo(20);
        assertThat(event.x).isEqualTo(2);
        assertThat(event.a).isEqualTo(PHOSPHORO.index());
    }

    @Test
    void blindnessFadesOutOverItsLastSecond() {
        enemy.pos.set(3, 0, 0);
        lookAt(enemy, 0, 1, 0);
        flashAt(0, 1, 0);
        double full = blindness(enemy);
        while (blindTicks(enemy) > 30) {
            enemy.statuses.tick(enemy, arena);
        }

        assertThat(blindness(enemy)).isCloseTo(full * 0.5, within(0.02));
    }

    // ------------------------------------------------------------------------------------------ the slug

    @Test
    void phosphoroIsASlowStickySlugThatFlashesWhenItsFuseBurnsOut() {
        assertThat(PHOSPHORO.projectile().sticky()).isTrue();
        assertThat(PHOSPHORO.projectile().fuseTicks()).isGreaterThan(CONTENT.slugs().require("grenuke")
                .projectile().fuseTicks() - 10);
        assertThat(PHOSPHORO.onExpire().flashRadius()).isPositive();
        for (SlugDefinition other : CONTENT.slugs().all()) {
            if (other != PHOSPHORO) {
                assertThat(PHOSPHORO.projectile().speed()).as("slower than " + other.id())
                        .isLessThan(other.projectile().speed());
            }
        }
    }

    @Test
    void aPhosphoroStuckBehindAWallBlindsOnlyThoseWhoCanSeeIt() {
        World world = new World(CONTENT.maps().require("test_arena"));   // a 4 m high wall along x = 0
        arena.setSight(world::hasLineOfSight);
        ProjectileSystem projectiles = new ProjectileSystem(arena, world, new ProjectilePool(4));
        thrower.pos.set(-12, 0, 8);
        enemy.pos.set(6, 0, 0);       // same side of the wall as the flash, looking at it
        ally.pos.set(-6, 0, 0);       // other side of the wall, also looking that way
        Projectile slug = projectiles.spawn(thrower, PHOSPHORO, new Vec3(2, 0.5, 0), new Vec3(0, -1, 0));
        while (slug.active && !slug.stuck) {
            arena.advanceTick();
            projectiles.step();
        }
        lookAt(enemy, slug.pos.x, slug.pos.y, slug.pos.z);
        lookAt(ally, slug.pos.x, slug.pos.y, slug.pos.z);
        lookAt(thrower, -30, 1.6, 8);

        for (int i = 0; i < PHOSPHORO.projectile().fuseTicks() + 1; i++) {
            arena.advanceTick();
            projectiles.step();
        }

        assertThat(slug.active).as("it went off").isFalse();
        assertThat(blindTicks(enemy)).isPositive();
        assertThat(blindTicks(ally)).as("the wall hid it").isZero();
        assertThat(blindTicks(thrower)).as("looking elsewhere").isZero();
    }
}
