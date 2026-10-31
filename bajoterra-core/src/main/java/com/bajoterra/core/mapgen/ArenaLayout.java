package com.bajoterra.core.mapgen;

import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.Obstacle.Rise;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import com.bajoterra.core.world.WaterPool;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;

/**
 * Lays out one candidate arena from a random source. The west half (team 0's side) is built from a small kit of
 * designed pieces (pillars, blocks, crates, walls, hills with ramps) placed in lanes with room to walk around them,
 * and is then mirrored onto the east half, so both teams always get the same arena. Pieces that straddle the middle
 * (the centre piece and the balconies) are built already symmetric. Whether the result is good enough to play is
 * decided afterwards by {@link ArenaGenerator}.
 */
final class ArenaLayout {

    /** How the west half is copied onto the east half. Both keep the arena fair; they just look different. */
    enum Symmetry {
        /** (x, z) to (-x, z): the north side stays north. */
        MIRROR,
        /** (x, z) to (-x, -z): a half turn, what is north on one side is south on the other. */
        HALF_TURN
    }

    /** Free space kept between separate structures, so two combatants can pass side by side. */
    static final double GAP = 2.5;
    /** The smaller gap allowed for the pieces that break up open lanes (still a comfortable corridor). */
    static final double BREAKER_GAP = 2.0;
    /** Free floor kept beyond the foot of every ramp. */
    static final double RUN_OUT = 1.8;
    /** Range of floor occupancy the layout aims for (the hand-made Caverna has 0.165). */
    static final double MIN_FILL = 0.12;
    static final double MAX_FILL = 0.18;
    /** Distance from the outer walls that free-standing pieces keep. */
    static final double WALL_MARGIN = 2.0;
    /** Depth of each team's spawn area, measured from its end wall. */
    static final double SPAWN_DEPTH = 10.0;
    /** Spawn points sit this far from the end wall. */
    static final double SPAWN_OFFSET = 4.0;
    /** Height of crates and low walls: they stop flat shots at a crouching body, arcs fly over them. */
    static final double LOW = 1.0;
    /** Height of the crates in front of the spawns: just over chest height, still drawn as crates. */
    static final double SPAWN_COVER = 1.3;
    /** Height of hills (the plank decks reached by ramps). */
    static final double HILL = 2.0;
    /** Height of the balconies along the long walls. */
    static final double BALCONY = 2.4;
    /** Radius range of the decorative pool of water: a puddle in a corner, never a lake or a river. */
    static final double MIN_POOL = 1.3;
    static final double MAX_POOL = 1.9;
    /** Free floor between the pool and any solid or pickup. */
    static final double POOL_CLEARANCE = 1.0;

    private static final String[] PLACES = {
        "Gruta", "Caverna", "Sima", "Galería", "Hondonada", "Cripta", "Bóveda", "Cueva", "Grieta", "Madriguera"
    };
    private static final String[] TRAITS = {
        "del Eco", "de los Hongos", "Cristalina", "del Musgo", "de Brasa", "Profunda", "de las Babosas",
        "del Trueno", "Escondida", "de la Niebla", "del Río Seco", "de Ámbar", "Resonante", "de las Raíces"
    };

    /** An axis-aligned rectangle on the floor. */
    record Rect(double minX, double minZ, double maxX, double maxZ) {

        boolean overlaps(Rect other, double gap) {
            return minX < other.maxX + gap && maxX > other.minX - gap
                    && minZ < other.maxZ + gap && maxZ > other.minZ - gap;
        }

        boolean contains(double x, double z, double margin) {
            return x >= minX - margin && x <= maxX + margin && z >= minZ - margin && z <= maxZ + margin;
        }

        double area() {
            return (maxX - minX) * (maxZ - minZ);
        }
    }

    /** A structure ready to be placed: its solids, the floor it covers and the free run-outs its ramps need. */
    private static final class Piece {
        final List<Obstacle> solids = new ArrayList<>();
        final List<Rect> runOuts = new ArrayList<>();
        final List<PickupSpawn> pickups = new ArrayList<>();
        Rect footprint;

        void add(Obstacle o) {
            solids.add(o);
            Rect r = new Rect(o.minX(), o.minZ(), o.maxX(), o.maxZ());
            footprint = footprint == null ? r : new Rect(Math.min(footprint.minX(), r.minX()),
                    Math.min(footprint.minZ(), r.minZ()), Math.max(footprint.maxX(), r.maxX()),
                    Math.max(footprint.maxZ(), r.maxZ()));
        }
    }

    private final SplittableRandom random;
    private final double halfW;
    private final double halfD;
    private final Symmetry symmetry;
    private final List<Obstacle> west = new ArrayList<>();
    private final List<Obstacle> shared = new ArrayList<>();
    private final List<PickupSpawn> westPickups = new ArrayList<>();
    private final List<PickupSpawn> sharedPickups = new ArrayList<>();
    /** Everything already placed, in whole-map coordinates (a west piece is recorded together with its mirror). */
    private final List<Rect> taken = new ArrayList<>();
    private final List<Rect> solidsFloor = new ArrayList<>();
    private int counter;

    ArenaLayout(SplittableRandom random) {
        this.random = random;
        this.halfW = (56 + 2 * random.nextInt(5)) / 2.0;
        this.halfD = (36 + 2 * random.nextInt(5)) / 2.0;
        this.symmetry = random.nextBoolean() ? Symmetry.MIRROR : Symmetry.HALF_TURN;
    }

    Symmetry symmetry() {
        return symmetry;
    }

    /** Builds the whole arena. */
    GameMap build(String id) {
        String name = PLACES[random.nextInt(PLACES.length)] + " " + TRAITS[random.nextInt(TRAITS.length)];
        placeSpawnArea();
        placeCentre();
        placeBalconies();
        placeWestPieces();
        placePickups();

        List<Obstacle> obstacles = new ArrayList<>(shared);
        obstacles.addAll(west);
        for (Obstacle o : west) {
            obstacles.add(mirror(o));
        }
        List<PickupSpawn> pickups = new ArrayList<>(sharedPickups);
        pickups.addAll(westPickups);
        for (PickupSpawn p : westPickups) {
            pickups.add(new PickupSpawn(p.pickupId(), -p.x(), p.y(), symmetry == Symmetry.MIRROR ? p.z() : -p.z()));
        }
        List<SpawnPoint> team0 = new ArrayList<>();
        List<SpawnPoint> team1 = new ArrayList<>();
        for (int i = -1; i <= 1; i++) {
            double z = 4.0 * i;
            team0.add(new SpawnPoint(-halfW + SPAWN_OFFSET, 0, z, Math.toRadians(90)));
            team1.add(new SpawnPoint(halfW - SPAWN_OFFSET, 0, symmetry == Symmetry.MIRROR ? z : -z,
                    Math.toRadians(270)));
        }
        return new GameMap(id, 0, name, -halfW, halfW, -halfD, halfD, obstacles, List.of(team0, team1), pickups,
                placeWater(obstacles, pickups), 3.0);
    }

    /**
     * One small decorative pool of water on open floor, outside the spawn areas and away from the pickups, tucked
     * next to a structure when it can be (puddles gather at the foot of rocks). It is drawn last from the random
     * source, so the solids of an arena are the same with or without it.
     */
    private List<WaterPool> placeWater(List<Obstacle> obstacles, List<PickupSpawn> pickups) {
        double radius = MIN_POOL + random.nextDouble() * (MAX_POOL - MIN_POOL);
        List<double[]> snug = new ArrayList<>();
        List<double[]> open = new ArrayList<>();
        double limitX = halfW - SPAWN_DEPTH - radius;
        double limitZ = halfD - WALL_MARGIN - radius;
        for (double x = -limitX; x <= limitX; x += 1) {
            for (double z = -limitZ; z <= limitZ; z += 1) {
                double nearest = Double.MAX_VALUE;
                for (Obstacle o : obstacles) {
                    double dx = x - Math.max(o.minX(), Math.min(x, o.maxX()));
                    double dz = z - Math.max(o.minZ(), Math.min(z, o.maxZ()));
                    nearest = Math.min(nearest, Math.sqrt(dx * dx + dz * dz));
                }
                boolean nearPickup = false;
                for (PickupSpawn p : pickups) {
                    double dx = x - p.x();
                    double dz = z - p.z();
                    nearPickup |= dx * dx + dz * dz < (radius + POOL_CLEARANCE) * (radius + POOL_CLEARANCE);
                }
                if (nearPickup || nearest < radius + POOL_CLEARANCE) {
                    continue;
                }
                (nearest <= radius + POOL_CLEARANCE + 1.5 ? snug : open).add(new double[] {x, z});
            }
        }
        List<double[]> candidates = snug.isEmpty() ? open : snug;
        if (candidates.isEmpty()) {
            return List.of();
        }
        double[] at = candidates.get(random.nextInt(candidates.size()));
        return List.of(new WaterPool(at[0], at[1], radius));
    }

    // ------------------------------------------------------------------------------------------ spawn and centre

    /**
     * Keeps each spawn area clear except for a row of crates in front of the spawn points. The crates are just over
     * chest height, so nobody can shoot from one spawn into the other when a round starts.
     */
    private void placeSpawnArea() {
        Piece cover = new Piece();
        double front = -halfW + SPAWN_OFFSET + 3;
        double h = SPAWN_COVER;
        switch (random.nextInt(3)) {
            case 0 -> cover.add(box("spawn_cover", front, -4.8, front + 1, 4.8, h));
            case 1 -> {
                cover.add(box("spawn_crates", front, -4.8, front + 1.4, -1.6, h));
                cover.add(box("spawn_crates", front, 1.6, front + 1.4, 4.8, h));
                // A stack ahead of the gap, so the middle spawn is covered too.
                cover.add(box("spawn_crates", front + 1.8, -0.9, front + 3, 0.9, h));
            }
            default -> {
                cover.add(box("spawn_cover", front, -2.4, front + 1, 2.4, h));
                cover.add(box("spawn_crates", front + 1.4, -5, front + 2.8, -2.4, h));
                cover.add(box("spawn_crates", front + 1.4, 2.4, front + 2.8, 5, h));
            }
        }
        commitWest(cover);
        reserveWest(new Rect(-halfW, -6.5, -halfW + SPAWN_DEPTH, 6.5));
    }

    /** One structure on the middle line, symmetric by construction under both symmetries. */
    private void placeCentre() {
        Piece centre = new Piece();
        switch (random.nextInt(4)) {
            case 0 -> {
                double a = 1.3 + random.nextDouble() * 0.5;
                double b = 1.6 + random.nextDouble() * 0.7;
                centre.add(box("centre_low", -a, -b, a, b, LOW));
                if (random.nextBoolean()) {
                    twinWalls(centre, 6 + random.nextDouble() * 3);
                }
            }
            case 1 -> {
                double a = 1.8 + random.nextDouble() * 0.7;
                double b = 2.0 + random.nextDouble();
                Obstacle top = box("centre_hill", -a, -b, a, b, HILL);
                centre.add(top);
                double run = 4.5 + random.nextDouble();
                ramp(centre, top, Rise.POS_X, run);
                ramp(centre, top, Rise.NEG_X, run);
                centre.pickups.add(new PickupSpawn("overshield_orb", 0, HILL, 0));
            }
            case 2 -> {
                double px = 2.5 + random.nextDouble() * 1.5;
                double pz = 3 + random.nextDouble() * 3;
                double s = 0.8 + random.nextDouble() * 0.3;
                double h = 3.0 + random.nextDouble() * 0.6;
                for (int sx = -1; sx <= 1; sx += 2) {
                    for (int sz = -1; sz <= 1; sz += 2) {
                        centre.add(box("centre_pillar", sx * px - s, sz * pz - s, sx * px + s, sz * pz + s, h));
                    }
                }
            }
            default -> {
                double half = 2.5 + random.nextDouble() * 1.5;
                centre.add(box("centre_wall", -0.8, -half, 0.8, half, 4));
                double x = 4 + random.nextDouble();
                centre.add(box("centre_crates", -x - 1, -1.2, -x + 1, 1.2, LOW));
                centre.add(box("centre_crates", x - 1, -1.2, x + 1, 1.2, LOW));
            }
        }
        commitShared(centre);
    }

    private void twinWalls(Piece piece, double from) {
        double length = 3.5 + random.nextDouble() * 1.5;
        double half = 0.8 + random.nextDouble() * 0.3;
        piece.add(box("centre_wall", -half, from, half, from + length, 4));
        piece.add(box("centre_wall", -half, -from - length, half, -from, 4));
    }

    /** Raised walkways against the long walls, reached by a ramp at each end. */
    private void placeBalconies() {
        boolean north;
        boolean south;
        if (symmetry == Symmetry.HALF_TURN) {
            // A half turn maps north onto south, so balconies come in pairs.
            north = random.nextInt(10) < 5;
            south = north;
        } else {
            north = random.nextInt(10) < 4;
            south = random.nextInt(10) < 4;
        }
        double width = 4 + random.nextDouble() * 3;
        double depth = 3;
        double run = 6;
        boolean health = random.nextBoolean();
        for (int side = -1; side <= 1; side += 2) {
            if (side > 0 ? !north : !south) {
                continue;
            }
            Piece balcony = new Piece();
            double inner = side > 0 ? halfD - depth : -halfD;
            double outer = side > 0 ? halfD : -halfD + depth;
            Obstacle deck = box("balcony", -width, inner, width, outer, BALCONY);
            balcony.add(deck);
            ramp(balcony, deck, Rise.POS_X, run);
            ramp(balcony, deck, Rise.NEG_X, run);
            if (health) {
                balcony.pickups.add(new PickupSpawn("health_pack", 0, BALCONY, (inner + outer) / 2));
            }
            if (fits(balcony, false)) {
                commitShared(balcony);
            }
        }
    }

    // ------------------------------------------------------------------------------------------ west half

    private void placeWestPieces() {
        double midFrom = -halfW + SPAWN_DEPTH + GAP + 2;
        double midTo = midFrom + 5;
        // The piece in front of each spawn is what stops the two spawns seeing each other: it must exist.
        boolean centre = false;
        for (int attempt = 0; attempt < 30 && !centre; attempt++) {
            double x = midFrom + random.nextDouble() * (midTo - midFrom);
            centre = tryWest(laneBlocker(x, (random.nextDouble() - 0.5) * 2));
        }
        double outerZ = halfD - WALL_MARGIN - 2;
        double innerFrom = midTo + 2;
        for (int lane = -1; lane <= 1; lane += 2) {
            if (random.nextInt(10) < 9) {
                for (int attempt = 0; attempt < 10; attempt++) {
                    double x = midFrom + random.nextDouble() * (midTo - midFrom);
                    double z = lane * (5.5 + random.nextDouble() * Math.max(0.5, outerZ - 7.5));
                    if (tryWest(sidePiece(x, z))) {
                        break;
                    }
                }
            }
            int inner = 1 + random.nextInt(2);
            for (int k = 0; k < inner; k++) {
                for (int attempt = 0; attempt < 10; attempt++) {
                    double x = innerFrom + random.nextDouble() * Math.max(0.5, -3 - innerFrom);
                    double z = lane * (2.5 + random.nextDouble() * Math.max(0.5, outerZ - 2.5));
                    if (tryWest(innerPiece(x, z))) {
                        break;
                    }
                }
            }
            if (random.nextInt(10) < 5) {
                for (int attempt = 0; attempt < 6; attempt++) {
                    double x = midFrom + random.nextDouble() * Math.max(0.5, -4 - midFrom);
                    double z = lane * (halfD - WALL_MARGIN - 1.5);
                    double length = 4 + random.nextDouble() * 2;
                    Piece flank = new Piece();
                    flank.add(box("flank_cover", x - length / 2, z - 0.5, x + length / 2, z + 0.5, LOW));
                    if (tryWest(flank)) {
                        break;
                    }
                }
            }
        }
        fill();
        breakOpenLanes();
    }

    /**
     * Adds smaller pieces at free spots of the west half until the arena reaches a floor occupancy picked for this
     * arena (between {@link #MIN_FILL} and {@link #MAX_FILL}), so arenas range from airy to crowded but are never
     * bare.
     */
    private void fill() {
        double target = MIN_FILL + random.nextDouble() * (MAX_FILL - MIN_FILL);
        double total = 4 * halfW * halfD;
        for (int attempt = 0; attempt < 120 && occupied() / total < target; attempt++) {
            // Anywhere in the west half; the spawn area itself is reserved, so pieces only go beside it.
            double x = -halfW + WALL_MARGIN + 1 + random.nextDouble() * (halfW - WALL_MARGIN - 3);
            double z = (random.nextDouble() * 2 - 1) * (halfD - WALL_MARGIN - 1.5);
            tryWest(random.nextInt(3) == 0 ? innerPiece(x, z) : laneBreaker(x, z), BREAKER_GAP);
        }
    }

    /** Floor area covered by solids so far, both halves. */
    private double occupied() {
        double area = 0;
        for (Rect r : solidsFloor) {
            area += r.area();
        }
        return area;
    }

    /**
     * Looks along the middle half of the arena for stretches with nothing to hide behind at chest height and stands
     * something in them (on the west side; the mirror covers the east), longest first, until none is left or no
     * piece fits any more.
     */
    private void breakOpenLanes() {
        double limit = ArenaGenerator.MAX_OPEN_LANE * 2 * halfW * 0.85;
        for (int pass = 0; pass < 30; pass++) {
            List<double[]> runs = openRuns(limit);
            boolean placed = false;
            for (double[] run : runs) {
                double from = Math.max(run[1] + 1, -halfW + WALL_MARGIN + 1);
                double to = Math.min(run[2] - 1, -2);
                for (int attempt = 0; attempt < 16 && !placed && to > from; attempt++) {
                    double x = from + random.nextDouble() * (to - from);
                    double z = run[0] + (random.nextDouble() - 0.5) * 1.5;
                    placed = tryWest(laneBreaker(x, z), BREAKER_GAP);
                }
                if (placed) {
                    break;
                }
            }
            if (!placed) {
                return;
            }
        }
    }

    /** Every {z, startX, endX} open chest-height run longer than {@code limit}, longest first. */
    private List<double[]> openRuns(double limit) {
        List<Obstacle> all = new ArrayList<>(shared);
        all.addAll(west);
        for (Obstacle o : west) {
            all.add(mirror(o));
        }
        List<double[]> runs = new ArrayList<>();
        double band = halfD / 2;
        for (double z = -band; z <= band + 1e-9; z += 2) {
            double start = -halfW;
            for (double x = -halfW; x <= halfW + 1e-9; x += 0.5) {
                boolean blocked = ArenaGenerator.blocksSight(all, x, z);
                if (blocked || x >= halfW) {
                    if (x - start > limit) {
                        runs.add(new double[] {z, start, x});
                    }
                    start = x + 0.5;
                }
            }
        }
        runs.sort((a, b) -> Double.compare(b[2] - b[1], a[2] - a[1]));
        return runs;
    }

    /** Something at least chest high and small: crates stacked high, a pillar, a short wall or a block. */
    private Piece laneBreaker(double x, double z) {
        Piece piece = new Piece();
        switch (random.nextInt(4)) {
            case 0 -> {
                double a = 0.8 + random.nextDouble() * 0.5;
                double b = 1 + random.nextDouble() * 1.2;
                piece.add(box("crates", x - a, z - b, x + a, z + b, SPAWN_COVER));
            }
            case 1 -> {
                double sz = 0.8 + random.nextDouble() * 0.4;
                piece.add(box("pillar", x - sz, z - sz, x + sz, z + sz, 3 + random.nextDouble() * 0.6));
            }
            case 2 -> {
                double half = 1.6 + random.nextDouble();
                piece.add(box("wall", x - 0.7, z - half, x + 0.7, z + half, 3.5));
            }
            default -> {
                double a = 1.2 + random.nextDouble() * 0.8;
                double b = 1.2 + random.nextDouble() * 0.8;
                piece.add(box("block", x - a, z - b, x + a, z + b, 2.0 + random.nextDouble() * 0.15));
            }
        }
        return piece;
    }

    /** A tall piece across the middle lane: a hill with ramps, a solid block, a wall or a pair of pillars. */
    private Piece laneBlocker(double x, double z) {
        Piece piece = new Piece();
        switch (random.nextInt(4)) {
            case 0 -> {
                double a = 1.8 + random.nextDouble() * 0.7;
                double b = 2.4 + random.nextDouble() * 0.8;
                Obstacle top = box("hill", x - a, z - b, x + a, z + b, HILL);
                piece.add(top);
                double run = 4.5 + random.nextDouble();
                ramp(piece, top, Rise.NEG_Z, run);
                if (random.nextBoolean()) {
                    ramp(piece, top, Rise.POS_Z, run);
                }
                piece.pickups.add(new PickupSpawn("health_pack", x, HILL, z));
            }
            case 1 -> {
                double a = 1.2 + random.nextDouble() * 0.6;
                double b = 2.8 + random.nextDouble() * 0.8;
                piece.add(box("block", x - a, z - b, x + a, z + b, 2.0 + random.nextDouble() * 0.15));
            }
            case 2 -> {
                double b = 2.8 + random.nextDouble();
                piece.add(box("wall", x - 0.75, z - b, x + 0.75, z + b, 4));
            }
            default -> {
                double s = 0.9 + random.nextDouble() * 0.3;
                double gap = 1.2 + random.nextDouble() * 0.4;
                double h = 3 + random.nextDouble() * 0.6;
                piece.add(box("pillar", x - s, z - gap - 2 * s, x + s, z - gap, h));
                piece.add(box("pillar", x - s, z + gap, x + s, z + gap + 2 * s, h));
                // Between the pillars stands a crate stack, so the lane is blocked at chest height.
                piece.add(box("crates", x - 0.7, z - gap, x + 0.7, z + gap, LOW + 0.3));
            }
        }
        return piece;
    }

    /** Something for the north or south lane: a pillar, a block, a crate group, a wall or a small hill. */
    private Piece sidePiece(double x, double z) {
        Piece piece = new Piece();
        switch (random.nextInt(5)) {
            case 0 -> {
                double s = 0.9 + random.nextDouble() * 0.4;
                piece.add(box("pillar", x - s, z - s * 1.5, x + s, z + s * 1.5, 3 + random.nextDouble() * 0.6));
            }
            case 1 -> {
                double a = 1.3 + random.nextDouble() * 0.6;
                piece.add(box("block", x - a, z - a, x + a, z + a, 2.0 + random.nextDouble() * 0.15));
            }
            case 2 -> crates(piece, x, z);
            case 3 -> {
                double half = 2 + random.nextDouble() * 1.5;
                piece.add(box("wall", x - half, z - 0.75, x + half, z + 0.75, 4));
            }
            default -> {
                double a = 1.8 + random.nextDouble() * 0.5;
                double b = 1.8 + random.nextDouble() * 0.5;
                Obstacle top = box("hill", x - a, z - b, x + a, z + b, HILL);
                piece.add(top);
                ramp(piece, top, random.nextBoolean() ? Rise.POS_X : Rise.NEG_X, 4.5 + random.nextDouble());
                piece.pickups.add(new PickupSpawn("shield_cell", x, HILL, z));
            }
        }
        return piece;
    }

    /** Something closer to the middle: crates, a short wall or a pillar. */
    private Piece innerPiece(double x, double z) {
        Piece piece = new Piece();
        switch (random.nextInt(3)) {
            case 0 -> crates(piece, x, z);
            case 1 -> {
                double half = 1.8 + random.nextDouble() * 1.2;
                piece.add(box("wall", x - 0.7, z - half, x + 0.7, z + half, 3.5 + random.nextDouble() * 0.5));
            }
            default -> {
                double s = 0.8 + random.nextDouble() * 0.4;
                piece.add(box("pillar", x - s, z - s, x + s, z + s, 3 + random.nextDouble() * 0.6));
            }
        }
        return piece;
    }

    /** One to three crate stacks in a line or an L, touching each other. */
    private void crates(Piece piece, double x, double z) {
        double size = 1.4 + random.nextDouble() * 0.5;
        int count = 2 + random.nextInt(2);
        boolean alongX = random.nextBoolean();
        for (int i = 0; i < count; i++) {
            double ox = alongX ? i * size : 0;
            double oz = alongX ? 0 : i * size;
            if (i == 2 && random.nextBoolean()) {
                // The third stack turns the line into an L.
                ox = size;
                oz = size;
            }
            piece.add(box("crates", x + ox - size / 2, z + oz - size / 2, x + ox + size / 2, z + oz + size / 2,
                    LOW + random.nextInt(2) * 0.2));
        }
    }

    // ------------------------------------------------------------------------------------------ pickups

    /** Health and shield on both halves (hill tops count), and the overshield on the middle line. */
    private void placePickups() {
        int health = 0;
        int shields = 0;
        for (PickupSpawn p : westPickups) {
            health += p.pickupId().equals("health_pack") ? 1 : 0;
            shields += p.pickupId().equals("shield_cell") ? 1 : 0;
        }
        if (health == 0) {
            groundPickup("health_pack");
        }
        if (shields == 0) {
            groundPickup("shield_cell");
        }
        boolean orb = sharedPickups.stream().anyMatch(p -> p.pickupId().equals("overshield_orb"));
        if (!orb) {
            if (free(0, 0, 1.6)) {
                sharedPickups.add(new PickupSpawn("overshield_orb", 0, 0, 0));
            } else {
                // Off the centre, a pair keeps both symmetries: (0, z) and (0, -z).
                for (double z = 3; z < halfD - 3; z += 1) {
                    if (free(0, z, 1.6) && free(0, -z, 1.6)) {
                        sharedPickups.add(new PickupSpawn("overshield_orb", 0, 0, z));
                        sharedPickups.add(new PickupSpawn("overshield_orb", 0, 0, -z));
                        break;
                    }
                }
            }
        }
    }

    private void groundPickup(String item) {
        for (int attempt = 0; attempt < 40; attempt++) {
            double x = -halfW + SPAWN_DEPTH + 1 + random.nextDouble() * (halfW - SPAWN_DEPTH - 4);
            double z = (random.nextDouble() * 2 - 1) * (halfD - 3);
            if (free(x, z, 1.6) && farFromPickups(x, z)) {
                westPickups.add(new PickupSpawn(item, x, 0, z));
                return;
            }
        }
    }

    private boolean farFromPickups(double x, double z) {
        for (PickupSpawn p : westPickups) {
            if (Math.abs(p.x() - x) < 4 && Math.abs(p.z() - z) < 4) {
                return false;
            }
        }
        return true;
    }

    /** True when no solid (or ramp) lies within {@code margin} of the point. */
    private boolean free(double x, double z, double margin) {
        for (Rect r : solidsFloor) {
            if (r.contains(x, z, margin)) {
                return false;
            }
        }
        return true;
    }

    // ------------------------------------------------------------------------------------------ placement

    private boolean tryWest(Piece piece) {
        return tryWest(piece, GAP);
    }

    private boolean tryWest(Piece piece, double gap) {
        if (!fits(piece, true, gap)) {
            return false;
        }
        commitWest(piece);
        return true;
    }

    private boolean fits(Piece piece, boolean westOnly) {
        return fits(piece, westOnly, GAP);
    }

    /** Whether the piece stays inside the arena (and the west half), {@code gap} clear of everything placed. */
    private boolean fits(Piece piece, boolean westOnly, double gap) {
        Rect f = piece.footprint;
        boolean againstWall = !westOnly;
        double margin = againstWall ? 0 : WALL_MARGIN;
        if (f.minX() < -halfW + margin || f.maxZ() > halfD - margin || f.minZ() < -halfD + margin
                || (westOnly ? f.maxX() > -GAP / 2 - 0.25 : f.maxX() > halfW - margin)) {
            return false;
        }
        for (Rect other : taken) {
            if (f.overlaps(other, gap)) {
                return false;
            }
        }
        // The foot of a balcony ramp lies along the wall, so pieces against the wall may reach it.
        double edge = againstWall ? 0 : 0.5;
        for (Rect runOut : piece.runOuts) {
            if (runOut.minX() < -halfW + edge || runOut.maxX() > (westOnly ? -0.5 : halfW - edge)
                    || runOut.minZ() < -halfD + edge || runOut.maxZ() > halfD - edge) {
                return false;
            }
            for (Rect other : taken) {
                if (runOut.overlaps(other, 0.3)) {
                    return false;
                }
            }
        }
        return true;
    }

    private void commitWest(Piece piece) {
        west.addAll(piece.solids);
        westPickups.addAll(piece.pickups);
        reserveWest(piece.footprint);
        for (Rect runOut : piece.runOuts) {
            reserveWest(runOut);
        }
        for (Obstacle o : piece.solids) {
            Rect r = new Rect(o.minX(), o.minZ(), o.maxX(), o.maxZ());
            solidsFloor.add(r);
            solidsFloor.add(mirror(r));
        }
    }

    private void commitShared(Piece piece) {
        shared.addAll(piece.solids);
        sharedPickups.addAll(piece.pickups);
        taken.add(piece.footprint);
        taken.addAll(piece.runOuts);
        for (Obstacle o : piece.solids) {
            solidsFloor.add(new Rect(o.minX(), o.minZ(), o.maxX(), o.maxZ()));
        }
    }

    private void reserveWest(Rect rect) {
        taken.add(rect);
        taken.add(mirror(rect));
    }

    // ------------------------------------------------------------------------------------------ geometry

    private Obstacle box(String kind, double minX, double minZ, double maxX, double maxZ, double height) {
        return new Obstacle(kind + "_" + counter++, minX, 0, minZ, maxX, height, maxZ, true, true, true);
    }

    /**
     * Adds a ramp against one side of a platform, as wide as that side, climbing towards it; {@code rise} is the
     * direction in which the ramp gets higher. The floor beyond its foot is kept free.
     */
    private void ramp(Piece piece, Obstacle top, Rise rise, double run) {
        double h = top.maxY();
        String id = "ramp_" + counter++;
        Obstacle ramp;
        Rect runOut;
        switch (rise) {
            case POS_X -> {
                ramp = new Obstacle(id, top.minX() - run, 0, top.minZ(), top.minX(), h, top.maxZ(), true, true,
                        true, rise);
                runOut = new Rect(top.minX() - run - RUN_OUT, top.minZ(), top.minX() - run, top.maxZ());
            }
            case NEG_X -> {
                ramp = new Obstacle(id, top.maxX(), 0, top.minZ(), top.maxX() + run, h, top.maxZ(), true, true,
                        true, rise);
                runOut = new Rect(top.maxX() + run, top.minZ(), top.maxX() + run + RUN_OUT, top.maxZ());
            }
            case POS_Z -> {
                ramp = new Obstacle(id, top.minX(), 0, top.minZ() - run, top.maxX(), h, top.minZ(), true, true,
                        true, rise);
                runOut = new Rect(top.minX(), top.minZ() - run - RUN_OUT, top.maxX(), top.minZ() - run);
            }
            case NEG_Z -> {
                ramp = new Obstacle(id, top.minX(), 0, top.maxZ(), top.maxX(), h, top.maxZ() + run, true, true,
                        true, rise);
                runOut = new Rect(top.minX(), top.maxZ() + run, top.maxX(), top.maxZ() + run + RUN_OUT);
            }
            default -> throw new IllegalArgumentException("A ramp needs a direction");
        }
        piece.add(ramp);
        piece.runOuts.add(runOut);
    }

    /** The east-half copy of a west-half obstacle. */
    Obstacle mirror(Obstacle o) {
        boolean turn = symmetry == Symmetry.HALF_TURN;
        Rise rise = switch (o.rise()) {
            case POS_X -> Rise.NEG_X;
            case NEG_X -> Rise.POS_X;
            case POS_Z -> turn ? Rise.NEG_Z : Rise.POS_Z;
            case NEG_Z -> turn ? Rise.POS_Z : Rise.NEG_Z;
            case NONE -> Rise.NONE;
        };
        return new Obstacle("e_" + o.id(), -o.maxX(), o.minY(), turn ? -o.maxZ() : o.minZ(), -o.minX(), o.maxY(),
                turn ? -o.minZ() : o.maxZ(), o.blocksProjectiles(), o.blocksMovement(), o.blocksLineOfSight(), rise);
    }

    private Rect mirror(Rect r) {
        return symmetry == Symmetry.MIRROR
                ? new Rect(-r.maxX(), r.minZ(), -r.minX(), r.maxZ())
                : new Rect(-r.maxX(), -r.maxZ(), -r.minX(), -r.minZ());
    }
}
