package com.bajoterra.core.world;

import static com.bajoterra.core.Fixtures.CONTENT;
import static com.bajoterra.core.Fixtures.combatant;
import static com.bajoterra.core.Fixtures.effect;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.projectile.ProjectileSystem;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/**
 * Obstacles in the map stop the slugs that are fired at them, whatever the slug's speed, and only the obstacle
 * flags decide what is blocked.
 */
class ObstacleBlockingTest {

    /** A shooter, a target and the machinery to fly slugs between them on a given map. */
    private static final class Rig {
        final Combatant shooter;
        final Combatant target;
        final CombatArena arena;
        final ProjectileSystem projectiles;

        Rig(String mapId, double shooterX, double shooterZ, double targetX, double targetZ, int targetTeam) {
            shooter = combatant(0, 0, "eli", "doc");
            target = combatant(1, targetTeam, "eli", "doc");
            shooter.pos.set(shooterX, 0, shooterZ);
            target.pos.set(targetX, 0, targetZ);
            arena = com.bajoterra.core.Fixtures.arena(shooter, target);
            projectiles = new ProjectileSystem(arena, new World(CONTENT.maps().require(mapId)),
                    new ProjectilePool(16));
        }

        void fire(SlugDefinition slug, double y, double dx, double dy, double dz) {
            Vec3 direction = new Vec3(dx, dy, dz).normalize();
            projectiles.spawn(shooter, slug, new Vec3(shooter.pos.x, y, shooter.pos.z), direction);
            for (int i = 0; i < 600 && projectiles.pool().activeCount() > 0; i++) {
                arena.advanceTick();
                projectiles.step();
            }
        }

        GameEvent firstEvent(EventType type) {
            for (long seq = arena.events().oldestAvailable(); seq < arena.events().written(); seq++) {
                if (arena.events().at(seq).type == type) {
                    return arena.events().at(seq);
                }
            }
            return null;
        }
    }

    private static SlugDefinition probe(double speed, double gravity) {
        return SlugDefinition.builder("probe")
                .projectile(new ProjectileSpec(speed, gravity, 0.25, 600, 0))
                .onHit(effect("{\"type\":\"damage\",\"amount\":10}"))
                .build();
    }

    @ParameterizedTest(name = "speed {0} m/s")
    @ValueSource(doubles = {20, 45, 90, 200})
    void aWallStopsAnySlugBeforeItReachesTheTargetBehindIt(double speed) {
        Rig rig = new Rig("test_arena", -5, 0, 5, 0, 1);

        rig.fire(probe(speed, 0), 1.3, 1, 0, 0);

        assertThat(rig.target.vitals.shield()).isEqualTo(rig.target.vitals.maxShield());
        GameEvent wallHit = rig.firstEvent(EventType.PROJECTILE_WORLD_HIT);
        assertThat(wallHit).as("stopped by the wall").isNotNull();
        assertThat(wallHit.x).isLessThan(-0.2);
        assertThat(rig.firstEvent(EventType.PROJECTILE_HIT)).isNull();
    }

    @Test
    void theSameShotHitsWhenThereIsNoWall() {
        Rig rig = new Rig("test_open", -5, 0, 5, 0, 1);

        rig.fire(probe(45, 0), 1.3, 1, 0, 0);

        assertThat(rig.target.vitals.shield()).isEqualTo(rig.target.vitals.maxShield() - 10);
        assertThat(rig.firstEvent(EventType.PROJECTILE_WORLD_HIT)).isNull();
    }

    @Test
    void goingAroundTheWallWorks() {
        Rig rig = new Rig("test_arena", -5, 6, 5, 6, 1);

        rig.fire(probe(45, 0), 1.3, 1, 0, 0);

        assertThat(rig.target.vitals.shield()).isEqualTo(rig.target.vitals.maxShield() - 10);
    }

    @Test
    void healingSlugsAreBlockedByTheWallToo() {
        Rig rig = new Rig("test_arena", -5, 0, 5, 0, 0);
        rig.target.vitals.applyDamage(60, 1.0);
        double before = rig.target.vitals.hp();

        rig.fire(CONTENT.slugs().require("doc"), 1.3, 1, 0, 0);

        assertThat(rig.target.vitals.hp()).isEqualTo(before);
        assertThat(rig.firstEvent(EventType.PROJECTILE_WORLD_HIT)).isNotNull();
    }

    @Test
    void aFlatShotIsStoppedByLowCoverButALobbedShotClearsIt() {
        // Low cover: x 7.5..8.5, z -2..2, 1 m high. Shoot from x = 3 at a target 5 m behind it.
        Rig flat = new Rig("test_arena", 3, 0, 12, 0, 1);
        flat.fire(probe(30, 0), 0.7, 1, 0, 0);
        assertThat(flat.target.vitals.shield()).as("flat shot blocked").isEqualTo(50);
        assertThat(flat.firstEvent(EventType.PROJECTILE_WORLD_HIT)).isNotNull();

        boolean lobbedShotLanded = false;
        for (double pitch = 0.2; pitch <= 1.0 && !lobbedShotLanded; pitch += 0.05) {
            Rig lobbed = new Rig("test_arena", 3, 0, 12, 0, 1);
            lobbed.fire(probe(20, 12), 0.7, Math.cos(pitch), Math.sin(pitch), 0);
            lobbedShotLanded = lobbed.target.vitals.shield() < 50;
        }
        assertThat(lobbedShotLanded).as("an arc over the cover reaches the target").isTrue();
    }

    @Test
    void obstaclesThatDoNotBlockProjectilesLetSlugsThrough() {
        // The fence at x -8..-7.7, z -6..-3 blocks movement only.
        Rig rig = new Rig("test_arena", -12, -4.5, -4, -4.5, 1);

        rig.fire(probe(45, 0), 1.3, 1, 0, 0);

        assertThat(rig.target.vitals.shield()).isEqualTo(rig.target.vitals.maxShield() - 10);
    }

    @Test
    void slugsThatPierceTheWorldFlyThroughWalls() {
        SlugDefinition ghost = SlugDefinition.builder("ghost")
                .projectile(new ProjectileSpec(45, 0, 0.25, 600, 0))
                .pierceWorld(true)
                .onHit(effect("{\"type\":\"damage\",\"amount\":10}"))
                .build();
        Rig rig = new Rig("test_arena", -5, 0, 5, 0, 1);

        rig.fire(ghost, 1.3, 1, 0, 0);

        assertThat(rig.target.vitals.shield()).isEqualTo(rig.target.vitals.maxShield() - 10);
    }

    @Test
    void worldHitEffectsRunWhereTheSlugStops() {
        SlugDefinition bomb = SlugDefinition.builder("bomb")
                .projectile(new ProjectileSpec(30, 0, 0.25, 600, 0))
                .onWorldHit(effect("{\"type\":\"area\",\"radius\":3,\"falloff\":\"NONE\","
                        + "\"effects\":[{\"type\":\"damage\",\"amount\":20}]}"))
                .build();
        Rig rig = new Rig("test_arena", -6, 0, 5, 0, 1);
        Combatant nearWall = combatant(2, 1, "eli", "doc");
        nearWall.pos.set(-1.2, 0, 1.5);
        CombatArena arena = com.bajoterra.core.Fixtures.arena(rig.shooter, rig.target, nearWall);
        ProjectileSystem system = new ProjectileSystem(arena, new World(CONTENT.maps().require("test_arena")),
                new ProjectilePool(16));

        system.spawn(rig.shooter, bomb, new Vec3(-6, 1.3, 0), new Vec3(1, 0, 0));
        for (int i = 0; i < 100 && system.pool().activeCount() > 0; i++) {
            arena.advanceTick();
            system.step();
        }

        assertThat(nearWall.vitals.shield()).as("caught in the blast against the wall").isLessThan(50);
        assertThat(rig.target.vitals.shield()).as("far side of the wall is safe").isEqualTo(50);
    }

    @Test
    void theGroundStopsSlugsToo() {
        Rig rig = new Rig("test_open", -5, 0, 15, 8, 1);

        rig.fire(probe(30, 0), 1.3, 1, -0.5, 0);

        assertThat(rig.firstEvent(EventType.PROJECTILE_WORLD_HIT)).isNotNull();
        assertThat(rig.firstEvent(EventType.PROJECTILE_WORLD_HIT).y).isLessThan(0.5);
    }

    @Test
    void anObstacleInFrontOfACombatantShieldsThemEvenWhenTheSlugWouldHitThem() {
        // The wall sits between shooter and target; the slug is aimed straight at the target.
        Rig rig = new Rig("test_arena", -3, 0, 3, 0, 1);

        rig.fire(probe(90, 0), 1.0, 6, 0, 0);

        assertThat(rig.firstEvent(EventType.PROJECTILE_HIT)).isNull();
        assertThat(rig.target.alive).isTrue();
    }

    @Test
    void lineOfSightAndMovementHonourTheSeparateFlags() {
        World world = new World(CONTENT.maps().require("test_arena"));

        assertThat(world.hasLineOfSight(-5, 1.3, 0, 5, 1.3, 0)).as("wall blocks sight").isFalse();
        assertThat(world.hasLineOfSight(-12, 1.0, -4.5, -4, 1.0, -4.5)).as("fence does not").isTrue();
        assertThat(world.hasClearShot(-12, 1.0, -4.5, -4, 1.0, -4.5, 0.25)).as("nor stops slugs").isTrue();

        Combatant walker = combatant(0, 0, "eli", "doc");
        walker.pos.set(-10, 0, -4.5);
        for (int i = 0; i < 120; i++) {
            walker.pos.x += 0.1;
            world.resolveCombatant(walker);
        }
        assertThat(walker.pos.x).as("the fence blocks walking").isLessThan(-8 + 0.01);
    }

    @Test
    void obstaclesDefineWhichSurfaceACombatantStandsOn() {
        World world = new World(CONTENT.maps().require("test_arena"));

        assertThat(world.groundHeightAt(8, 0, 0, 0.4)).as("cover is too tall to step on").isEqualTo(0);
        assertThat(world.groundHeightAt(8, 0, 0.8, 0.4)).as("but can be landed on after a jump").isEqualTo(1.0);
        assertThat(world.groundHeightAt(14, 0, 0, 0.4)).isEqualTo(0);
    }
}
