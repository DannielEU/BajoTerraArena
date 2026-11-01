package com.bajoterra.core.ammo;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.arena;
import static com.bajoterra.core.Fixtures.combatant;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.projectile.ProjectileSystem;
import com.bajoterra.core.world.World;
import org.junit.jupiter.api.Test;

/** Where slugs start and where they fly: muzzle position, aim angles, camera convergence and spread. */
class FireGeometryTest {

    private static final double SPEED = 60.5;     // flaringo

    private final Combatant shooter = combatant(0, 0, "eli", "flaringo");
    private final Combatant target = combatant(1, 1, "eli", "doc");
    private final CombatArena arena = arena(shooter, target);
    private final ProjectilePool pool = new ProjectilePool(8);
    private final FireController controller = new FireController(arena, new World(CONTENT.maps().require("test_open")),
            new ProjectileSystem(arena, new World(CONTENT.maps().require("test_open")), pool),
            SlotSelectionPolicy.MANUAL);

    GeometryProbe fireWith(double yaw, double pitch) {
        shooter.pos.set(-5, 0, 2);
        target.pos.set(15, 0, 9);
        shooter.yaw = yaw;
        shooter.input.aimYaw = yaw;
        shooter.input.aimPitch = pitch;
        shooter.input.fire = true;
        controller.update(shooter);
        return new GeometryProbe();
    }

    /** What the single projectile of the pool looks like right after it was launched. */
    final class GeometryProbe {
        final Projectile p;
        final Vec3 origin = new Vec3();

        GeometryProbe() {
            Projectile found = null;
            for (int i = 0; i < pool.capacity(); i++) {
                if (pool.at(i).active) {
                    found = pool.at(i);
                }
            }
            p = found;
            for (long seq = arena.events().oldestAvailable(); seq < arena.events().written(); seq++) {
                if (arena.events().at(seq).type == EventType.SHOT) {
                    origin.set(arena.events().at(seq).x, arena.events().at(seq).y, arena.events().at(seq).z);
                }
            }
        }

        Vec3 direction() {
            return new Vec3(p.vel.x, p.vel.y, p.vel.z).scale(1 / SPEED);
        }
    }

    @Test
    void theMuzzleSitsInFrontOfTheBodyAtBlasterHeight() {
        GeometryProbe probe = fireWith(Math.PI / 2, 0);

        assertThat(probe.origin.x).isCloseTo(-5 + 0.55, within(1e-9));
        assertThat(probe.origin.y).isCloseTo(1.8 * 0.72, within(1e-9));
        assertThat(probe.origin.z).isCloseTo(2, within(1e-9));
    }

    @Test
    void theMuzzleFollowsTheFacingDirection() {
        GeometryProbe north = fireWith(0, 0);

        assertThat(north.origin.x).isCloseTo(-5, within(1e-9));
        assertThat(north.origin.z).isCloseTo(2 + 0.55, within(1e-9));
    }

    @Test
    void aCrouchingShooterFiresFromALowerBlaster() {
        shooter.stats.hitboxHeightMultiplier = 0.5;

        GeometryProbe probe = fireWith(0, 0);

        assertThat(probe.origin.y).isCloseTo(1.8 * 0.5 * 0.72, within(1e-9));
    }

    @Test
    void flatShotsLeaveAlongTheYawDirectionAtSlugSpeed() {
        GeometryProbe probe = fireWith(Math.PI / 2, 0);

        assertThat(probe.p.vel.x).isCloseTo(SPEED, within(1e-9));
        assertThat(probe.p.vel.z).isCloseTo(0, within(1e-9));
        assertThat(probe.p.vel.y).as("launched flat").isCloseTo(0, within(1e-9));
    }

    @Test
    void yawAndPitchSelectTheLaunchDirection() {
        double yaw = Math.PI / 4;
        double pitch = 0.5;

        GeometryProbe probe = fireWith(yaw, pitch);

        assertThat(probe.p.vel.x).isCloseTo(SPEED * Math.sin(yaw) * Math.cos(pitch), within(1e-9));
        assertThat(probe.p.vel.y).isCloseTo(SPEED * Math.sin(pitch), within(1e-9));
        assertThat(probe.p.vel.z).isCloseTo(SPEED * Math.cos(yaw) * Math.cos(pitch), within(1e-9));
        assertThat(probe.direction().length()).isCloseTo(1.0, within(1e-9));
    }

    @Test
    void downwardPitchShootsDownward() {
        GeometryProbe probe = fireWith(0, -0.3);

        assertThat(probe.p.vel.y).isNegative();
    }

    @Test
    void thirdPersonShotsConvergeOnTheCrosshairPoint() {
        shooter.input.hasCamera = true;
        shooter.input.camX = -9;
        shooter.input.camY = 1.7;
        shooter.input.camZ = 2;

        GeometryProbe probe = fireWith(Math.PI / 2, 0);

        // The camera ray runs along +X at y = 1.7; the slug must head for the point 80 m along it.
        Vec3 aimPoint = new Vec3(-9 + 80, 1.7, 2);
        Vec3 expected = aimPoint.sub(probe.origin).normalize();
        assertThat(probe.direction().x).isCloseTo(expected.x, within(1e-6));
        assertThat(probe.direction().y).isCloseTo(expected.y, within(1e-6));
        assertThat(probe.direction().z).isCloseTo(expected.z, within(1e-6));
        assertThat(probe.direction().y).as("slightly upwards, from the low blaster to the high crosshair")
                .isPositive();
    }

    @Test
    void thirdPersonShotsAtTheGroundConvergeOnTheHitPoint() {
        shooter.input.hasCamera = true;
        shooter.input.camX = -9;
        shooter.input.camY = 5;
        shooter.input.camZ = 2;

        GeometryProbe probe = fireWith(Math.PI / 2, -0.2);

        double distance = 5 / Math.sin(0.2);   // where the descending camera ray reaches the ground
        Vec3 aimPoint = new Vec3(-9 + Math.cos(0.2) * distance, 0, 2);
        Vec3 expected = aimPoint.sub(probe.origin).normalize();
        assertThat(probe.direction().x).isCloseTo(expected.x, within(1e-6));
        assertThat(probe.direction().y).isCloseTo(expected.y, within(1e-6));
    }

    @Test
    void anAimPointBehindTheMuzzleFallsBackToTheCameraDirection() {
        // Standing next to the thin wall (x -0.2..0.2) with the camera low and almost touching it: the crosshair
        // point is nearly straight below the muzzle, so aiming at it would shoot the floor. The camera
        // direction is used instead.
        World walled = new World(CONTENT.maps().require("test_arena"));
        FireController closeQuarters = new FireController(arena, walled, new ProjectileSystem(arena, walled, pool),
                SlotSelectionPolicy.MANUAL);
        shooter.pos.set(-0.9, 0, 0);
        shooter.yaw = Math.PI / 2;
        shooter.input.aimYaw = Math.PI / 2;
        shooter.input.aimPitch = 0;
        shooter.input.hasCamera = true;
        shooter.input.camX = -0.3;
        shooter.input.camY = 0.1;
        shooter.input.camZ = 0;
        shooter.input.fire = true;

        closeQuarters.update(shooter);

        Projectile p = null;
        for (int i = 0; i < pool.capacity(); i++) {
            if (pool.at(i).active) {
                p = pool.at(i);
            }
        }
        assertThat(p).isNotNull();
        assertThat(p.vel.x).isCloseTo(SPEED, within(1e-9));
        assertThat(p.vel.y).isCloseTo(0, within(1e-9));
    }

    @Test
    void blindnessSpraysShotsAroundTheAimDirection() {
        shooter.stats.aimSpread = 0.2;
        double maxSideways = 0;
        double minSideways = 0;
        for (int i = 0; i < 40; i++) {
            pool.releaseAll();
            shooter.nextFireTick = 0;
            shooter.loadout.reset();
            GeometryProbe probe = fireWith(Math.PI / 2, 0);
            double sideways = probe.direction().z;
            maxSideways = Math.max(maxSideways, sideways);
            minSideways = Math.min(minSideways, sideways);
            assertThat(probe.direction().length()).isCloseTo(1.0, within(1e-9));
        }

        assertThat(maxSideways).isGreaterThan(0.03);
        assertThat(minSideways).isLessThan(-0.03);
        assertThat(maxSideways).isLessThan(0.25);
    }

    @Test
    void withoutBlindnessTheShotIsPerfectlyAccurate() {
        for (int i = 0; i < 10; i++) {
            pool.releaseAll();
            shooter.nextFireTick = 0;
            shooter.loadout.reset();
            assertThat(fireWith(Math.PI / 2, 0).direction().z).isCloseTo(0, within(1e-12));
        }
    }

    @Test
    void directionFromAnglesIsAUnitVectorWithTheDocumentedConvention() {
        Vec3 out = new Vec3();

        FireController.directionFromAngles(out, 0, 0);
        assertThat(out.z).isCloseTo(1, within(1e-9));
        FireController.directionFromAngles(out, Math.PI / 2, 0);
        assertThat(out.x).isCloseTo(1, within(1e-9));
        FireController.directionFromAngles(out, 0, Math.PI / 2);
        assertThat(out.y).isCloseTo(1, within(1e-9));
        FireController.directionFromAngles(out, 1.0, 0.6);
        assertThat(out.length()).isCloseTo(1, within(1e-9));
    }

    // ------------------------------------------------------------------------------------------ gating and input

    @Test
    void fireIsAllowedExactlyFromTheNextFireTick() {
        shooter.input.fire = true;
        shooter.nextFireTick = 5;

        assertThat(arena.tick()).isZero();
        assertThat(controller.canFire(shooter)).isFalse();
        for (int i = 0; i < 4; i++) {
            arena.advanceTick();
        }
        assertThat(controller.canFire(shooter)).as("tick 4 < 5").isFalse();
        arena.advanceTick();
        assertThat(controller.canFire(shooter)).as("tick 5 == 5").isTrue();
    }

    @Test
    void theBlasterIntervalIsScaledByTheSlugDelay() {
        shooter.input.fire = true;
        shooter.input.aimYaw = 0;
        controller.update(shooter);

        // flaringo: 42 ticks * 0.6 = 25.2 -> 25
        assertThat(shooter.nextFireTick).isEqualTo(25);
    }

    @Test
    void aDeadOrStunnedShooterCannotFire() {
        shooter.stats.stunned = true;
        assertThat(controller.canFire(shooter)).isFalse();
        shooter.stats.stunned = false;
        assertThat(controller.canFire(shooter)).isTrue();

        arena.damage(target, shooter, 10_000, com.bajoterra.core.slug.Element.NEUTRAL, 0);
        assertThat(controller.canFire(shooter)).isFalse();
    }

    @Test
    void aLockedSelectedSlotCannotFire() {
        shooter.loadout.fireSelected();   // selection still points at the locked slot

        assertThat(controller.canFire(shooter)).isFalse();
    }

    @Test
    void slotSelectionAndCyclingComeFromTheInputAndAreConsumed() {
        shooter.input.selectSlot = 3;
        controller.update(shooter);
        assertThat(shooter.loadout.selected()).isEqualTo(3);
        assertThat(shooter.input.selectSlot).isEqualTo(-1);

        shooter.input.cycleSlot = 1;
        controller.update(shooter);
        assertThat(shooter.loadout.selected()).isEqualTo(4);
        assertThat(shooter.input.cycleSlot).isZero();

        shooter.input.cycleSlot = -1;
        controller.update(shooter);
        assertThat(shooter.loadout.selected()).isEqualTo(3);
    }

    @Test
    void theDeadCannotChangeTheirSlot() {
        arena.damage(target, shooter, 10_000, com.bajoterra.core.slug.Element.NEUTRAL, 0);
        shooter.input.selectSlot = 7;
        shooter.input.cycleSlot = 1;

        controller.update(shooter);

        assertThat(shooter.loadout.selected()).isZero();
        assertThat(shooter.input.selectSlot).as("edges are still consumed").isEqualTo(-1);
    }

    @Test
    void selectionPoliciesDecideWhatIsSelectedAfterAShot() {
        Loadout loadout = shooter.loadout;
        loadout.select(2);
        loadout.fireSelected();
        SlotSelectionPolicy.MANUAL.afterFire(loadout, 2);
        assertThat(loadout.selected()).as("locked slot -> moves on").isEqualTo(3);

        Loadout other = combatant(5, 0, "eli", "flaringo").loadout;
        other.select(2);
        other.fireSelected();
        SlotSelectionPolicy.ROUND_ROBIN.afterFire(other, 2);
        assertThat(other.selected()).isEqualTo(3);
    }

    @Test
    void aSlugWithNoLockKeepsBeingSelectedByTheManualPolicy() {
        com.bajoterra.core.slug.SlugDefinition free = com.bajoterra.core.slug.SlugDefinition.builder("free")
                .reloadShotsOverride(0).build();
        Loadout loadout = new Loadout(java.util.Collections.nCopies(Loadout.SIZE, free), 3);
        loadout.fireSelected();

        SlotSelectionPolicy.MANUAL.afterFire(loadout, 0);

        assertThat(loadout.selected()).isZero();
    }

    @Test
    void loadoutQueriesAtTheEdges() {
        Loadout loadout = shooter.loadout;

        assertThat(loadout.select(-1)).isFalse();
        assertThat(loadout.select(5)).isFalse();
        assertThat(loadout.select(4)).isTrue();
        assertThat(loadout.select(0)).isTrue();
        assertThat(loadout.cycle(0)).isFalse();
        assertThat(loadout.hasAvailable()).isTrue();
        assertThat(loadout.cycle(1)).isTrue();
        assertThat(loadout.cycle(-1)).isTrue();
        assertThat(loadout.selected()).isZero();
    }

    @Test
    void cyclingWithEverythingElseLockedKeepsTheSelection() {
        // Reload 4: after four shots the four other slots are locked.
        java.util.List<com.bajoterra.core.slug.SlugDefinition> slugs = new java.util.ArrayList<>();
        for (int i = 0; i < Loadout.SIZE; i++) {
            slugs.add(com.bajoterra.core.slug.SlugDefinition.builder("s" + i).build());
        }
        Loadout loadout = new Loadout(slugs, 4);
        for (int slot = 0; slot < 4; slot++) {
            loadout.select(slot);
            loadout.fireSelected();
        }
        loadout.select(4);

        assertThat(loadout.cycle(1)).isFalse();
        assertThat(loadout.cycle(-1)).isFalse();
        assertThat(loadout.selected()).isEqualTo(4);
        assertThat(loadout.hasAvailable()).isTrue();
    }
}
