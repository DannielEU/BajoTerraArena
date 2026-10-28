package com.bajoterra.core.world;

/**
 * A box in the map, optionally sloped. The three flags let one shape model walls, pillars, low cover and glass-like
 * barriers: a high wall blocks everything, low cover blocks flat shots but not arcs over it, a fence blocks
 * movement but not sight.
 *
 * <p>With a {@link Rise} other than {@link Rise#NONE} the box is a ramp: a wedge whose flat underside is at
 * {@code minY} and whose top surface climbs in a straight line from {@code minY} at one edge to {@code maxY} at the
 * opposite one. Combatants walk up and down it, slugs and sight lines hit its sloped surface.
 *
 * @param blocksProjectiles slugs stop against it (and run their world-hit effects)
 * @param blocksMovement combatants cannot walk through it
 * @param blocksLineOfSight bots cannot see through it
 * @param rise the direction in which a ramp climbs, {@link Rise#NONE} for a plain box
 */
public record Obstacle(
        String id,
        double minX,
        double minY,
        double minZ,
        double maxX,
        double maxY,
        double maxZ,
        boolean blocksProjectiles,
        boolean blocksMovement,
        boolean blocksLineOfSight,
        Rise rise) {

    /**
     * The direction in which a ramp's surface gets higher. The ordinal is the number sent on the wire, so the order
     * must not change.
     */
    public enum Rise {
        NONE,
        POS_X,
        NEG_X,
        POS_Z,
        NEG_Z;

        /** Parses the authoring spelling ({@code "+x"}, {@code "-x"}, {@code "+z"}, {@code "-z"}, or empty). */
        public static Rise parse(String text) {
            return switch (text) {
                case "", "none" -> NONE;
                case "+x" -> POS_X;
                case "-x" -> NEG_X;
                case "+z" -> POS_Z;
                case "-z" -> NEG_Z;
                default -> throw new IllegalArgumentException("'" + text + "' is not one of +x, -x, +z, -z");
            };
        }
    }

    public Obstacle {
        if (!(maxX > minX) || !(maxY > minY) || !(maxZ > minZ)) {
            throw new IllegalArgumentException("Obstacle '" + id + "' has an empty or inverted volume");
        }
        if (rise == null) {
            throw new IllegalArgumentException("Obstacle '" + id + "' has no rise (use Rise.NONE)");
        }
    }

    /** A plain box. */
    public Obstacle(String id, double minX, double minY, double minZ, double maxX, double maxY, double maxZ,
            boolean blocksProjectiles, boolean blocksMovement, boolean blocksLineOfSight) {
        this(id, minX, minY, minZ, maxX, maxY, maxZ, blocksProjectiles, blocksMovement, blocksLineOfSight,
                Rise.NONE);
    }

    public double height() {
        return maxY - minY;
    }

    public boolean isRamp() {
        return rise != Rise.NONE;
    }

    /** True when the point lies inside the box footprint (ignoring height), grown by {@code margin}. */
    public boolean containsXZ(double x, double z, double margin) {
        return x >= minX - margin && x <= maxX + margin && z >= minZ - margin && z <= maxZ + margin;
    }

    /**
     * Height of the surface a body standing at (x, z) would stand on: {@code maxY} for a plain box, the point of the
     * slope for a ramp (the point is clamped into the footprint first, so just outside an edge it is the edge's
     * height).
     */
    public double topAt(double x, double z) {
        return rise == Rise.NONE ? maxY : planeHeight(Math.min(Math.max(x, minX), maxX),
                Math.min(Math.max(z, minZ), maxZ));
    }

    /**
     * Height of a ramp's sloped plane extended over the whole XZ plane. Only meaningful when {@link #isRamp()}; a
     * plain box answers {@code maxY}.
     */
    public double planeHeight(double x, double z) {
        return switch (rise) {
            case NONE -> maxY;
            case POS_X -> minY + slope() * (x - minX);
            case NEG_X -> minY + slope() * (maxX - x);
            case POS_Z -> minY + slope() * (z - minZ);
            case NEG_Z -> minY + slope() * (maxZ - z);
        };
    }

    /** Metres of height gained per metre walked up the ramp (0 for a plain box). */
    public double slope() {
        return switch (rise) {
            case NONE -> 0;
            case POS_X, NEG_X -> (maxY - minY) / (maxX - minX);
            case POS_Z, NEG_Z -> (maxY - minY) / (maxZ - minZ);
        };
    }

    /** Rate of change of {@link #planeHeight} along x (m per m): the slope's x component. */
    public double gradientX() {
        return switch (rise) {
            case POS_X -> slope();
            case NEG_X -> -slope();
            default -> 0;
        };
    }

    /** Rate of change of {@link #planeHeight} along z (m per m): the slope's z component. */
    public double gradientZ() {
        return switch (rise) {
            case POS_Z -> slope();
            case NEG_Z -> -slope();
            default -> 0;
        };
    }
}
