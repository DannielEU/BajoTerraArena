package com.bajoterra.core.world;

import com.bajoterra.core.content.Identified;
import java.util.ArrayList;
import java.util.List;

/**
 * Immutable arena description shared by every simulation that plays on it: bounds, a flat ground at y = 0,
 * obstacles (boxes and ramps), team spawn points, pickup placements, a navigation graph and derived cover points.
 */
public final class GameMap implements Identified {

    /** Cell size of the obstacle lookup grid, in metres. */
    private static final double GRID_CELL = 4.0;

    private final String id;
    private final int index;
    private final String name;
    private final double minX;
    private final double maxX;
    private final double minZ;
    private final double maxZ;
    private final Obstacle[] obstacles;
    private final List<Obstacle> obstacleList;
    private final List<List<SpawnPoint>> spawns;
    private final List<PickupSpawn> pickups;
    private final double navCellSize;
    private final SpatialGrid grid;
    private final NavGraph nav;
    private final double[] coverX;
    private final double[] coverZ;
    private final double[] coverHeight;

    /**
     * @param spawns one list of spawn points per team
     * @param navCellSize spacing of the generated navigation lattice
     */
    public GameMap(String id, int index, String name, double minX, double maxX, double minZ, double maxZ,
            List<Obstacle> obstacles, List<List<SpawnPoint>> spawns, List<PickupSpawn> pickups,
            double navCellSize) {
        this.id = id;
        this.index = index;
        this.name = name;
        this.minX = minX;
        this.maxX = maxX;
        this.minZ = minZ;
        this.maxZ = maxZ;
        this.obstacleList = List.copyOf(obstacles);
        this.obstacles = obstacleList.toArray(new Obstacle[0]);
        List<List<SpawnPoint>> copy = new ArrayList<>();
        for (List<SpawnPoint> team : spawns) {
            copy.add(List.copyOf(team));
        }
        this.spawns = List.copyOf(copy);
        this.pickups = List.copyOf(pickups);
        this.navCellSize = navCellSize;
        this.grid = new SpatialGrid(obstacleList, minX, minZ, maxX, maxZ, GRID_CELL);
        this.nav = NavGraph.generate(obstacleList, minX, minZ, maxX, maxZ, navCellSize);
        List<double[]> cover = computeCover();
        this.coverX = new double[cover.size()];
        this.coverZ = new double[cover.size()];
        this.coverHeight = new double[cover.size()];
        for (int i = 0; i < cover.size(); i++) {
            coverX[i] = cover.get(i)[0];
            coverZ[i] = cover.get(i)[1];
            coverHeight[i] = cover.get(i)[2];
        }
    }

    public GameMap withIndex(int newIndex) {
        return new GameMap(id, newIndex, name, minX, maxX, minZ, maxZ, obstacleList, spawns, pickups, navCellSize);
    }

    @Override
    public String id() {
        return id;
    }

    @Override
    public int index() {
        return index;
    }

    public String name() {
        return name;
    }

    public double minX() {
        return minX;
    }

    public double maxX() {
        return maxX;
    }

    public double minZ() {
        return minZ;
    }

    public double maxZ() {
        return maxZ;
    }

    /** Height of the flat ground plane. */
    public double groundY() {
        return 0;
    }

    public List<Obstacle> obstacleList() {
        return obstacleList;
    }

    public Obstacle obstacle(int i) {
        return obstacles[i];
    }

    public int obstacleCount() {
        return obstacles.length;
    }

    public SpatialGrid grid() {
        return grid;
    }

    public List<List<SpawnPoint>> spawns() {
        return spawns;
    }

    public List<PickupSpawn> pickups() {
        return pickups;
    }

    public NavGraph nav() {
        return nav;
    }

    public double navCellSize() {
        return navCellSize;
    }

    public int coverCount() {
        return coverX.length;
    }

    public double coverX(int i) {
        return coverX[i];
    }

    public double coverZ(int i) {
        return coverZ[i];
    }

    /** Height of the obstacle that provides the cover point (low cover only protects a crouching body). */
    public double coverHeight(int i) {
        return coverHeight[i];
    }

    private List<double[]> computeCover() {
        List<double[]> points = new ArrayList<>();
        for (Obstacle o : obstacleList) {
            if (!o.blocksProjectiles() || !o.blocksMovement() || o.isRamp() || o.height() < 0.8) {
                continue;
            }
            double offset = 1.0;
            double width = o.maxX() - o.minX();
            double depth = o.maxZ() - o.minZ();
            int alongX = Math.max(1, (int) Math.floor(width / 3.0));
            int alongZ = Math.max(1, (int) Math.floor(depth / 3.0));
            for (int i = 0; i < alongX; i++) {
                double x = o.minX() + width * (i + 0.5) / alongX;
                addCover(points, x, o.minZ() - offset, o.height());
                addCover(points, x, o.maxZ() + offset, o.height());
            }
            for (int i = 0; i < alongZ; i++) {
                double z = o.minZ() + depth * (i + 0.5) / alongZ;
                addCover(points, o.minX() - offset, z, o.height());
                addCover(points, o.maxX() + offset, z, o.height());
            }
        }
        return points;
    }

    private void addCover(List<double[]> points, double x, double z, double height) {
        if (x < minX + 1 || x > maxX - 1 || z < minZ + 1 || z > maxZ - 1) {
            return;
        }
        for (Obstacle o : obstacleList) {
            if (o.blocksMovement() && o.height() > 0.35 && o.containsXZ(x, z, 0.5)) {
                return;
            }
        }
        points.add(new double[] {x, z, height});
    }
}
