package com.bajoterra.core.world;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Walkable waypoint graph generated from the map's movement-blocking obstacles. All-pairs shortest paths are
 * precomputed once (Floyd-Warshall), so asking for the next waypoint towards a destination is a table lookup and
 * bots never allocate while navigating.
 */
public final class NavGraph {

    private static final double NODE_CLEARANCE = 0.7;

    private final double[] nodeX;
    private final double[] nodeZ;
    private final int[] nextHop;
    private final float[] distance;
    private final int size;

    private NavGraph(double[] nodeX, double[] nodeZ, int[] nextHop, float[] distance) {
        this.nodeX = nodeX;
        this.nodeZ = nodeZ;
        this.nextHop = nextHop;
        this.distance = distance;
        this.size = nodeX.length;
    }

    /**
     * Builds the graph on a regular lattice.
     *
     * @param cellSize lattice spacing in metres
     */
    public static NavGraph generate(List<Obstacle> obstacles, double minX, double minZ, double maxX, double maxZ,
            double cellSize) {
        // The lattice is centred on the map, so a symmetric map gets a symmetric graph (no side is favoured).
        int columns = (int) Math.floor((maxX - minX - 2) / cellSize + 1e-9) + 1;
        int rows = (int) Math.floor((maxZ - minZ - 2) / cellSize + 1e-9) + 1;
        double startX = minX + (maxX - minX - (columns - 1) * cellSize) / 2;
        double startZ = minZ + (maxZ - minZ - (rows - 1) * cellSize) / 2;
        List<double[]> nodes = new ArrayList<>();
        for (int row = 0; row < rows; row++) {
            for (int column = 0; column < columns; column++) {
                double x = startX + column * cellSize;
                double z = startZ + row * cellSize;
                if (!blockedAt(obstacles, x, z, NODE_CLEARANCE)) {
                    nodes.add(new double[] {x, z});
                }
            }
        }
        int n = nodes.size();
        double[] xs = new double[n];
        double[] zs = new double[n];
        for (int i = 0; i < n; i++) {
            xs[i] = nodes.get(i)[0];
            zs[i] = nodes.get(i)[1];
        }
        float[] dist = new float[n * n];
        Arrays.fill(dist, Float.POSITIVE_INFINITY);
        List<List<Integer>> neighbours = new ArrayList<>();
        for (int i = 0; i < n; i++) {
            neighbours.add(new ArrayList<>());
        }
        double linkRange = cellSize * 1.5;
        for (int i = 0; i < n; i++) {
            dist[i * n + i] = 0;
            for (int j = i + 1; j < n; j++) {
                double d = StrictMath.hypot(xs[i] - xs[j], zs[i] - zs[j]);
                if (d <= linkRange && walkable(obstacles, xs[i], zs[i], xs[j], zs[j])) {
                    dist[i * n + j] = (float) d;
                    dist[j * n + i] = (float) d;
                    neighbours.get(i).add(j);
                    neighbours.get(j).add(i);
                }
            }
        }
        for (int k = 0; k < n; k++) {
            for (int i = 0; i < n; i++) {
                float dik = dist[i * n + k];
                if (dik == Float.POSITIVE_INFINITY) {
                    continue;
                }
                for (int j = 0; j < n; j++) {
                    float candidate = dik + dist[k * n + j];
                    if (candidate < dist[i * n + j]) {
                        dist[i * n + j] = candidate;
                    }
                }
            }
        }
        int[] next = new int[n * n];
        Arrays.fill(next, -1);
        for (int i = 0; i < n; i++) {
            next[i * n + i] = i;
            for (int j = 0; j < n; j++) {
                if (i == j || dist[i * n + j] == Float.POSITIVE_INFINITY) {
                    continue;
                }
                next[i * n + j] = bestHop(i, j, xs, zs, dist, neighbours.get(i), n);
            }
        }
        return new NavGraph(xs, zs, next, dist);
    }

    /**
     * Among the neighbours that lie on some shortest path, picks the one closest to the destination in a straight
     * line. Equal-cost routes are therefore resolved by geometry, never by node numbering, which keeps paths
     * mirror-symmetric on symmetric maps.
     */
    private static int bestHop(int from, int to, double[] xs, double[] zs, float[] dist, List<Integer> neighbours,
            int n) {
        int best = -1;
        double bestDistance = Double.MAX_VALUE;
        float target = dist[from * n + to];
        for (int hop : neighbours) {
            float viaHop = dist[from * n + hop] + dist[hop * n + to];
            if (viaHop <= target + 1e-3f) {
                double straight = StrictMath.hypot(xs[hop] - xs[to], zs[hop] - zs[to]);
                if (straight < bestDistance - 1e-9) {
                    bestDistance = straight;
                    best = hop;
                }
            }
        }
        return best;
    }

    public int size() {
        return size;
    }

    public double nodeX(int node) {
        return nodeX[node];
    }

    public double nodeZ(int node) {
        return nodeZ[node];
    }

    /** Index of the waypoint closest to the position, or -1 for an empty graph. */
    public int nearestNode(double x, double z) {
        int best = -1;
        double bestDistance = Double.MAX_VALUE;
        for (int i = 0; i < size; i++) {
            double dx = nodeX[i] - x;
            double dz = nodeZ[i] - z;
            double d = dx * dx + dz * dz;
            if (d < bestDistance) {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    /** Next waypoint on the shortest path from {@code from} to {@code to}, or -1 when unreachable. */
    public int nextNode(int from, int to) {
        return nextHop[from * size + to];
    }

    /** Length of the shortest path in metres, or infinity when unreachable. */
    public float pathLength(int from, int to) {
        return distance[from * size + to];
    }

    private static boolean blockedAt(List<Obstacle> obstacles, double x, double z, double clearance) {
        for (Obstacle o : obstacles) {
            if (o.blocksMovement() && o.height() > 0.35 && o.containsXZ(x, z, clearance)) {
                return true;
            }
        }
        return false;
    }

    /** True when a body of {@link #NODE_CLEARANCE} radius can walk the segment without touching obstacles. */
    private static boolean walkable(List<Obstacle> obstacles, double x0, double z0, double x1, double z1) {
        for (Obstacle o : obstacles) {
            if (!o.blocksMovement() || o.height() <= 0.35) {
                continue;
            }
            if (segmentHitsBox(x0, z0, x1, z1, o.minX() - NODE_CLEARANCE, o.minZ() - NODE_CLEARANCE,
                    o.maxX() + NODE_CLEARANCE, o.maxZ() + NODE_CLEARANCE)) {
                return false;
            }
        }
        return true;
    }

    private static boolean segmentHitsBox(double x0, double z0, double x1, double z1, double minX, double minZ,
            double maxX, double maxZ) {
        double tMin = 0;
        double tMax = 1;
        double dx = x1 - x0;
        double dz = z1 - z0;
        if (Math.abs(dx) < 1e-12) {
            if (x0 < minX || x0 > maxX) {
                return false;
            }
        } else {
            double t1 = (minX - x0) / dx;
            double t2 = (maxX - x0) / dx;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }
        if (Math.abs(dz) < 1e-12) {
            if (z0 < minZ || z0 > maxZ) {
                return false;
            }
        } else {
            double t1 = (minZ - z0) / dz;
            double t2 = (maxZ - z0) / dz;
            tMin = Math.max(tMin, Math.min(t1, t2));
            tMax = Math.min(tMax, Math.max(t1, t2));
        }
        return tMin <= tMax;
    }
}
