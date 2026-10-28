package com.bajoterra.core.world;

import java.util.ArrayList;
import java.util.List;

/** Immutable uniform grid over the XZ plane that maps cells to the obstacles overlapping them. */
public final class SpatialGrid {

    private final double minX;
    private final double minZ;
    private final double cellSize;
    private final int columns;
    private final int rows;
    private final int[][] cells;

    public SpatialGrid(List<Obstacle> obstacles, double minX, double minZ, double maxX, double maxZ,
            double cellSize) {
        this.minX = minX;
        this.minZ = minZ;
        this.cellSize = cellSize;
        this.columns = Math.max(1, (int) Math.ceil((maxX - minX) / cellSize));
        this.rows = Math.max(1, (int) Math.ceil((maxZ - minZ) / cellSize));
        List<List<Integer>> lists = new ArrayList<>(columns * rows);
        for (int i = 0; i < columns * rows; i++) {
            lists.add(new ArrayList<>());
        }
        for (int index = 0; index < obstacles.size(); index++) {
            Obstacle o = obstacles.get(index);
            int c0 = clampColumn(column(o.minX()));
            int c1 = clampColumn(column(o.maxX()));
            int r0 = clampRow(row(o.minZ()));
            int r1 = clampRow(row(o.maxZ()));
            for (int r = r0; r <= r1; r++) {
                for (int c = c0; c <= c1; c++) {
                    lists.get(r * columns + c).add(index);
                }
            }
        }
        this.cells = new int[columns * rows][];
        for (int i = 0; i < cells.length; i++) {
            cells[i] = lists.get(i).stream().mapToInt(Integer::intValue).toArray();
        }
    }

    public int columns() {
        return columns;
    }

    public int rows() {
        return rows;
    }

    /** Column containing world x, clamped into the grid. */
    public int columnOf(double x) {
        return clampColumn(column(x));
    }

    /** Row containing world z, clamped into the grid. */
    public int rowOf(double z) {
        return clampRow(row(z));
    }

    /** Obstacle indices overlapping a cell; callers must not modify the returned array. */
    public int[] obstaclesIn(int column, int row) {
        return cells[row * columns + column];
    }

    private int column(double x) {
        return (int) Math.floor((x - minX) / cellSize);
    }

    private int row(double z) {
        return (int) Math.floor((z - minZ) / cellSize);
    }

    private int clampColumn(int c) {
        return Math.max(0, Math.min(columns - 1, c));
    }

    private int clampRow(int r) {
        return Math.max(0, Math.min(rows - 1, r));
    }
}
