package com.bajoterra.core.mapgen;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.content.ContentValidator;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import com.bajoterra.core.world.World;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;

/**
 * Makes a new arena for every match: different every time, but always one that plays well. A candidate is laid out
 * by {@link ArenaLayout} (designed pieces, mirrored so both teams get the same arena) and kept only if it passes
 * {@link #problems}: the usual map checks, no unreachable corners, spawns that cannot see each other, cover all
 * along the way and no long open shooting lanes. Generation is deterministic for a seed, so replays still work.
 */
public final class ArenaGenerator {

    /** Map id that asks for a generated arena instead of a map from the content. */
    public static final String MAP_ID = "aleatoria";
    /** Candidates tried before falling back to the hand-made {@link #FALLBACK_MAP}. */
    static final int MAX_ATTEMPTS = 60;
    /** The hand-made map used if no candidate is good enough (never needed in practice, see the tests). */
    static final String FALLBACK_MAP = "caverna";
    /** Size of the floor grid used to check that every patch can be walked to. */
    static final double CELL = 0.5;
    /** Radius of the widest hero (Kord), so the walking check is right for everybody. */
    static final double HERO_RADIUS = 0.65;
    /** Height of the sight lines checked between spawns and along the lanes (a standing chest). */
    static final double CHEST = 1.2;
    /** Minimum cover points in each third of the arena (west, middle, east). */
    static final int MIN_COVER_PER_THIRD = 4;
    /**
     * Longest stretch of a lane in the middle half of the arena (where the fighting is), as a fraction of the
     * arena's length, with nothing to hide behind. The hand-made Caverna is at 0.48; lanes along the long walls may
     * stay open, as they do there.
     */
    static final double MAX_OPEN_LANE = 0.65;
    /** Share of the floor taken by solids: below it the arena is a bare field, above it a maze. */
    static final double MIN_OCCUPANCY = 0.08;
    static final double MAX_OCCUPANCY = 0.30;

    private ArenaGenerator() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** A playable arena for this seed (the same seed always gives the same arena). */
    public static GameMap generate(long seed, Content content) {
        for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
            GameMap candidate = candidate(seed, attempt);
            if (problems(candidate, content).isEmpty()) {
                return candidate;
            }
        }
        GameMap fallback = content.maps().find(FALLBACK_MAP);
        return fallback != null ? fallback : content.maps().get(0);
    }

    /** The {@code attempt}-th candidate for a seed, before any checks. */
    static GameMap candidate(long seed, int attempt) {
        return new ArenaLayout(new SplittableRandom(seed ^ (attempt * 0x9E3779B97F4A7C15L))).build(MAP_ID);
    }

    /** Everything that keeps a map from playing well; empty when it is good. Hand-made maps pass it too. */
    public static List<String> problems(GameMap map, Content content) {
        // The quick checks first: most rejected candidates fail one of them, before the costly ones run.
        List<String> problems = new ArrayList<>();
        double occupancy = occupancy(map);
        if (occupancy < MIN_OCCUPANCY || occupancy > MAX_OCCUPANCY) {
            problems.add("solids cover " + Math.round(occupancy * 100) + " % of the floor");
        }
        double open = longestOpenLane(map);
        if (open > MAX_OPEN_LANE * (map.maxX() - map.minX())) {
            problems.add("a lane is open for " + Math.round(open) + " m with nothing to hide behind");
        }
        checkCover(map, problems);
        checkSpawnsHidden(map, problems);
        if (!problems.isEmpty()) {
            return problems;
        }
        problems.addAll(ContentValidator.mapProblems(map, content));
        if (!problems.isEmpty()) {
            return problems;
        }
        checkReachability(map, problems);
        return problems;
    }

    /**
     * Every patch of floor a hero fits on, and every pickup on the floor, can be reached on foot from the spawns:
     * a flood fill over a fine grid of the floor, with the solids grown by a hero's radius.
     */
    private static void checkReachability(GameMap map, List<String> problems) {
        int columns = (int) Math.ceil((map.maxX() - map.minX()) / CELL);
        int rows = (int) Math.ceil((map.maxZ() - map.minZ()) / CELL);
        boolean[] open = new boolean[columns * rows];
        for (int row = 0; row < rows; row++) {
            for (int column = 0; column < columns; column++) {
                double x = map.minX() + (column + 0.5) * CELL;
                double z = map.minZ() + (row + 0.5) * CELL;
                open[row * columns + column] = standable(map, x, z);
            }
        }
        boolean[] reached = new boolean[open.length];
        SpawnPoint spawn = map.spawns().get(0).get(0);
        int start = cell(map, columns, rows, spawn.x(), spawn.z());
        int[] queue = new int[open.length];
        int head = 0;
        int tail = 0;
        if (start >= 0 && open[start]) {
            reached[start] = true;
            queue[tail++] = start;
        }
        while (head < tail) {
            int current = queue[head++];
            int row = current / columns;
            int column = current % columns;
            int[] next = {column > 0 ? current - 1 : -1, column < columns - 1 ? current + 1 : -1,
                row > 0 ? current - columns : -1, row < rows - 1 ? current + columns : -1};
            for (int n : next) {
                if (n >= 0 && open[n] && !reached[n]) {
                    reached[n] = true;
                    queue[tail++] = n;
                }
            }
        }
        int cut = 0;
        for (int i = 0; i < open.length; i++) {
            if (open[i] && !reached[i]) {
                cut++;
            }
        }
        if (cut > 0) {
            problems.add(cut + " patches of floor are cut off from the spawns");
        }
        for (PickupSpawn p : map.pickups()) {
            if (p.y() > 0.1) {
                continue;
            }
            int c = cell(map, columns, rows, p.x(), p.z());
            if (c < 0 || !reached[c]) {
                problems.add("pickup '" + p.pickupId() + "' at (" + p.x() + ", " + p.z() + ") cannot be reached");
            }
        }
    }

    /** True when a hero standing on the floor at (x, z) does not overlap a solid. */
    private static boolean standable(GameMap map, double x, double z) {
        if (x < map.minX() + HERO_RADIUS || x > map.maxX() - HERO_RADIUS
                || z < map.minZ() + HERO_RADIUS || z > map.maxZ() - HERO_RADIUS) {
            return false;
        }
        for (Obstacle o : map.obstacleList()) {
            if (o.blocksMovement() && o.height() > 0.35 && o.containsXZ(x, z, HERO_RADIUS)) {
                return false;
            }
        }
        return true;
    }

    private static int cell(GameMap map, int columns, int rows, double x, double z) {
        int column = (int) Math.floor((x - map.minX()) / CELL);
        int row = (int) Math.floor((z - map.minZ()) / CELL);
        return column < 0 || row < 0 || column >= columns || row >= rows ? -1 : row * columns + column;
    }

    /** No spawn point sees an enemy spawn point: a round never starts with a free shot. */
    private static void checkSpawnsHidden(GameMap map, List<String> problems) {
        World world = new World(map);
        for (SpawnPoint a : map.spawns().get(0)) {
            for (SpawnPoint b : map.spawns().get(1)) {
                if (world.hasLineOfSight(a.x(), CHEST, a.z(), b.x(), CHEST, b.z())) {
                    problems.add("spawns (" + a.x() + ", " + a.z() + ") and (" + b.x() + ", " + b.z()
                            + ") see each other");
                    return;
                }
            }
        }
    }

    /** Some cover in each third of the arena, so no stretch of the way across is a bare field. */
    private static void checkCover(GameMap map, List<String> problems) {
        double third = (map.maxX() - map.minX()) / 3;
        int[] counts = new int[3];
        for (int i = 0; i < map.coverCount(); i++) {
            int slot = (int) Math.floor((map.coverX(i) - map.minX()) / third);
            counts[Math.max(0, Math.min(2, slot))]++;
        }
        for (int slot = 0; slot < 3; slot++) {
            if (counts[slot] < MIN_COVER_PER_THIRD) {
                problems.add("third " + slot + " has only " + counts[slot] + " cover points");
            }
        }
    }

    /**
     * Longest straight run, at chest height and along the length of the arena, with nothing that blocks sight:
     * sampled on lines every 2 m across the middle half of the arena.
     */
    static double longestOpenLane(GameMap map) {
        double longest = 0;
        double band = (map.maxZ() - map.minZ()) / 4;
        double middle = (map.maxZ() + map.minZ()) / 2;
        for (double z = middle - band; z <= middle + band + 1e-9; z += 2) {
            double run = 0;
            for (double x = map.minX(); x <= map.maxX(); x += 0.5) {
                if (blocksSight(map, x, z)) {
                    run = 0;
                } else {
                    run += 0.5;
                    longest = Math.max(longest, run);
                }
            }
        }
        return longest;
    }

    private static boolean blocksSight(GameMap map, double x, double z) {
        return blocksSight(map.obstacleList(), x, z);
    }

    /** True when something at (x, z) blocks sight at chest height. */
    static boolean blocksSight(List<Obstacle> obstacles, double x, double z) {
        for (Obstacle o : obstacles) {
            if (o.blocksLineOfSight() && o.containsXZ(x, z, 0) && o.topAt(x, z) > CHEST) {
                return true;
            }
        }
        return false;
    }

    /** Share of the floor covered by solids (ramps included). */
    static double occupancy(GameMap map) {
        double area = 0;
        for (Obstacle o : map.obstacleList()) {
            area += (o.maxX() - o.minX()) * (o.maxZ() - o.minZ());
        }
        return area / ((map.maxX() - map.minX()) * (map.maxZ() - map.minZ()));
    }
}
