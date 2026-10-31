package com.bajoterra.core.mapgen;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.content.ContentValidator;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.Obstacle.Rise;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import com.bajoterra.core.world.WaterPool;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import org.junit.jupiter.api.Test;

/** Generated arenas: a new one for every seed, always fair, walkable and with cover, never a bare field or a maze. */
class MapGeneratorTest {

    private static final int SEEDS = 300;

    @Test
    void theSameSeedAlwaysGivesTheSameArena() {
        GameMap a = ArenaGenerator.generate(42, CONTENT);
        GameMap b = ArenaGenerator.generate(42, CONTENT);

        assertThat(b.obstacleList()).isEqualTo(a.obstacleList());
        assertThat(b.pickups()).isEqualTo(a.pickups());
        assertThat(b.spawns()).isEqualTo(a.spawns());
        assertThat(b.name()).isEqualTo(a.name());
    }

    @Test
    void differentSeedsGiveDifferentArenas() {
        Set<List<Obstacle>> layouts = new HashSet<>();
        Set<String> names = new HashSet<>();
        for (long seed = 0; seed < 50; seed++) {
            GameMap map = ArenaGenerator.generate(seed, CONTENT);
            layouts.add(map.obstacleList());
            names.add(map.name());
        }

        assertThat(layouts).hasSize(50);
        assertThat(names.size()).as("names vary too").isGreaterThan(25);
    }

    @Test
    void everyArenaIsGeneratedAndPassesEveryCheck() {
        for (long seed = 0; seed < SEEDS; seed++) {
            GameMap map = ArenaGenerator.generate(seed, CONTENT);

            assertThat(map.id()).as("seed %d is generated, not the fallback", seed).isEqualTo(ArenaGenerator.MAP_ID);
            assertThat(ArenaGenerator.problems(map, CONTENT)).as("seed %d", seed).isEmpty();
            assertThat(map.pickups()).as("seed %d has pickups", seed).hasSizeGreaterThanOrEqualTo(5);
        }
    }

    @Test
    void everyArenaIsFairBothHalvesAreMirrorImages() {
        for (long seed = 0; seed < SEEDS; seed++) {
            GameMap map = ArenaGenerator.generate(seed, CONTENT);
            boolean mirror = symmetric(map, false);
            boolean halfTurn = symmetric(map, true);

            assertThat(mirror || halfTurn).as("seed %d is symmetric", seed).isTrue();
        }
    }

    @Test
    void arenasVaryInSizeShapeAndPieces() {
        Set<Double> widths = new HashSet<>();
        Set<Double> depths = new HashSet<>();
        int halfTurns = 0;
        int withBalconies = 0;
        int withHills = 0;
        for (long seed = 0; seed < 100; seed++) {
            GameMap map = ArenaGenerator.generate(seed, CONTENT);
            widths.add(map.maxX() - map.minX());
            depths.add(map.maxZ() - map.minZ());
            halfTurns += !symmetric(map, false) ? 1 : 0;
            withBalconies += map.obstacleList().stream().anyMatch(o -> o.id().startsWith("balcony")) ? 1 : 0;
            withHills += map.obstacleList().stream().anyMatch(o -> o.id().contains("hill")) ? 1 : 0;
        }

        assertThat(widths).hasSizeGreaterThanOrEqualTo(4);
        assertThat(depths).hasSizeGreaterThanOrEqualTo(4);
        assertThat(halfTurns).as("both kinds of symmetry appear").isBetween(15, 85);
        assertThat(withBalconies).isBetween(15, 90);
        assertThat(withHills).isBetween(15, 95);
    }

    @Test
    void theHandMadeCavernaPassesTheSameChecks() {
        assertThat(ArenaGenerator.problems(CONTENT.maps().require("caverna"), CONTENT)).isEmpty();
    }

    @Test
    void theChecksRejectABareFieldAndACutOffCorner() {
        GameMap caverna = CONTENT.maps().require("caverna");
        GameMap bare = new GameMap("bare", 0, "bare", -30, 30, -20, 20, List.of(), caverna.spawns(),
                caverna.pickups(), 3);
        List<Obstacle> walls = new java.util.ArrayList<>(caverna.obstacleList());
        // Two walls that close the north-west corner off.
        walls.add(new Obstacle("cut_a", -30, 0, 10, -25, 3, 11, true, true, true));
        walls.add(new Obstacle("cut_b", -26, 0, 11, -25, 3, 20, true, true, true));
        GameMap closed = new GameMap("closed", 0, "closed", -30, 30, -20, 20, walls, caverna.spawns(),
                caverna.pickups(), 3);

        assertThat(ArenaGenerator.problems(bare, CONTENT)).anyMatch(p -> p.contains("see each other"))
                .anyMatch(p -> p.contains("open for")).anyMatch(p -> p.contains("cover"));
        assertThat(ArenaGenerator.problems(closed, CONTENT)).anyMatch(p -> p.contains("cut off"));
    }

    @Test
    void everyArenaHasOneSmallPoolOfWaterOnOpenFloorOutsideTheSpawnAreas() {
        for (long seed = 0; seed < SEEDS; seed++) {
            GameMap map = ArenaGenerator.generate(seed, CONTENT);
            assertThat(map.water()).as("seed " + seed).hasSize(1);
            WaterPool pool = map.water().get(0);
            assertThat(pool.radius()).isBetween(ArenaLayout.MIN_POOL, ArenaLayout.MAX_POOL);
            assertThat(Math.abs(pool.x()) + pool.radius())
                    .as("seed " + seed + " keeps out of the spawn areas")
                    .isLessThanOrEqualTo(map.maxX() - ArenaLayout.SPAWN_DEPTH + 1e-9);
            assertThat(ContentValidator.mapProblems(map, CONTENT)).isEmpty();
        }
    }

    @Test
    void aPoolOfWaterUnderASolidOrAtTheEdgeIsRejected() {
        GameMap caverna = CONTENT.maps().require("caverna");
        assertThat(caverna.water()).hasSize(1);
        Obstacle pillar = caverna.obstacleList().get(3);
        GameMap underPillar = caverna.withWater(List.of(new WaterPool((pillar.minX() + pillar.maxX()) / 2,
                (pillar.minZ() + pillar.maxZ()) / 2, 1.5)));
        GameMap atTheEdge = caverna.withWater(List.of(new WaterPool(caverna.maxX() - 1, 0, 1.5)));

        assertThat(ContentValidator.mapProblems(underPillar, CONTENT)).anyMatch(p -> p.contains("runs under"));
        assertThat(ContentValidator.mapProblems(atTheEdge, CONTENT)).anyMatch(p -> p.contains("edge"));
    }

    @Test
    void generatingAnArenaIsQuick() {
        long start = System.nanoTime();
        for (long seed = 1000; seed < 1050; seed++) {
            ArenaGenerator.generate(seed, CONTENT);
        }
        double average = (System.nanoTime() - start) / 50 / 1e6;

        assertThat(average).as("milliseconds per arena").isLessThan(250);
    }

    @Test
    void botsPlayWholeMatchesOnGeneratedArenas() {
        for (long seed = 0; seed < 4; seed++) {
            GameSimulation sim = new GameSimulation(CONTENT,
                    MatchConfig.builder(ArenaGenerator.MAP_ID).seed(seed).build());
            sim.start();
            while (sim.match().phase() != MatchPhase.MATCH_END && sim.tickCount() < 60 * 60 * 20) {
                sim.tick();
            }

            assertThat(sim.map().id()).isEqualTo(ArenaGenerator.MAP_ID);
            assertThat(sim.match().phase()).as("seed %d finishes", seed).isEqualTo(MatchPhase.MATCH_END);
        }
    }

    @Test
    void aMatchOnTheGeneratedMapUsesItsSeed() {
        GameMap a = GameSimulation.resolveMap(CONTENT, MatchConfig.builder(ArenaGenerator.MAP_ID).seed(7).build());
        GameMap b = GameSimulation.resolveMap(CONTENT, MatchConfig.builder(ArenaGenerator.MAP_ID).seed(8).build());

        assertThat(a.obstacleList()).isNotEqualTo(b.obstacleList());
        assertThat(GameSimulation.resolveMap(CONTENT, MatchConfig.builder("caverna").build()))
                .isSameAs(CONTENT.maps().require("caverna"));
        assertThat(GameSimulation.isPlayableMap(CONTENT, ArenaGenerator.MAP_ID)).isTrue();
        assertThat(GameSimulation.isPlayableMap(CONTENT, "nowhere")).isFalse();
    }

    /** Every obstacle, pickup and spawn has its image on the other half (ramps climbing the mirrored way). */
    private static boolean symmetric(GameMap map, boolean halfTurn) {
        for (Obstacle o : map.obstacleList()) {
            Obstacle image = new Obstacle("", -o.maxX(), o.minY(), halfTurn ? -o.maxZ() : o.minZ(), -o.minX(),
                    o.maxY(), halfTurn ? -o.minZ() : o.maxZ(), o.blocksProjectiles(), o.blocksMovement(),
                    o.blocksLineOfSight(), mirrored(o.rise(), halfTurn));
            boolean found = map.obstacleList().stream().anyMatch(other -> same(other, image));
            if (!found) {
                return false;
            }
        }
        for (PickupSpawn p : map.pickups()) {
            boolean found = map.pickups().stream().anyMatch(q -> q.pickupId().equals(p.pickupId())
                    && close(q.x(), -p.x()) && close(q.y(), p.y()) && close(q.z(), halfTurn ? -p.z() : p.z()));
            if (!found) {
                return false;
            }
        }
        for (SpawnPoint s : map.spawns().get(0)) {
            boolean found = map.spawns().get(1).stream()
                    .anyMatch(t -> close(t.x(), -s.x()) && close(t.z(), halfTurn ? -s.z() : s.z()));
            if (!found) {
                return false;
            }
        }
        return true;
    }

    private static Rise mirrored(Rise rise, boolean halfTurn) {
        return switch (rise) {
            case POS_X -> Rise.NEG_X;
            case NEG_X -> Rise.POS_X;
            case POS_Z -> halfTurn ? Rise.NEG_Z : Rise.POS_Z;
            case NEG_Z -> halfTurn ? Rise.POS_Z : Rise.NEG_Z;
            case NONE -> Rise.NONE;
        };
    }

    private static boolean same(Obstacle a, Obstacle b) {
        return close(a.minX(), b.minX()) && close(a.maxX(), b.maxX()) && close(a.minZ(), b.minZ())
                && close(a.maxZ(), b.maxZ()) && close(a.minY(), b.minY()) && close(a.maxY(), b.maxY())
                && a.rise() == b.rise();
    }

    private static boolean close(double a, double b) {
        return Math.abs(a - b) < 1e-9;
    }
}
