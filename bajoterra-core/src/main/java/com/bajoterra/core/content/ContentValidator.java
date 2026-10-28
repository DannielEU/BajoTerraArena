package com.bajoterra.core.content;

import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.item.PickupDefinition;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.SlugRole;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.NavGraph;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import java.util.ArrayList;
import java.util.List;

/**
 * Semantic checks that need several definitions at once (the per-field checks live in the parsers): slugs must do
 * something and match their role, and maps must be playable (valid spawns, reachable teams, sane obstacles).
 */
final class ContentValidator {

    /** Minimum spawn points per team (3v3). */
    static final int MIN_SPAWNS_PER_TEAM = 3;
    /** Minimum obstacle thickness; thinner volumes are too fragile for swept collision. */
    static final double MIN_OBSTACLE_THICKNESS = 0.2;

    /** Steepest ramp that can be walked up (rise over run); a combatant climbs far less than a step each tick. */
    static final double MAX_RAMP_SLOPE = 0.7;

    /** How far below an obstacle's surface a point may be and still count as standing on it. */
    private static final double SURFACE_TOLERANCE = 0.05;

    private ContentValidator() {
        throw new AssertionError("Utility class, not instantiable");
    }

    static List<String> validate(Content content) {
        List<String> problems = new ArrayList<>();
        if (content.slugs().size() == 0) {
            problems.add("content/slugs: no slugs found");
        }
        if (content.characters().size() == 0) {
            problems.add("content/characters: no characters found");
        }
        if (content.maps().size() == 0) {
            problems.add("content/maps: no maps found");
        }
        for (SlugDefinition slug : content.slugs().all()) {
            validateSlug(slug, problems);
        }
        for (GameMap map : content.maps().all()) {
            validateMap(map, content, problems);
        }
        return problems;
    }

    private static void validateSlug(SlugDefinition slug, List<String> problems) {
        String where = "slug '" + slug.id() + "'";
        boolean doesSomething = slug.onHit() != Effect.NONE || slug.onWorldHit() != Effect.NONE
                || slug.onExpire() != Effect.NONE || slug.onTick() != Effect.NONE;
        if (!doesSomething) {
            problems.add(where + ": has no effects at all");
        }
        boolean helpsAllies = slug.targeting() == TargetingRule.ALLIES
                || slug.targeting() == TargetingRule.ALLIES_AND_SELF;
        if (slug.role() == SlugRole.SUPPORT && !helpsAllies) {
            problems.add(where + ": role SUPPORT requires targeting ALLIES or ALLIES_AND_SELF");
        }
        if (slug.role() != SlugRole.SUPPORT && helpsAllies) {
            problems.add(where + ": targeting " + slug.targeting() + " requires role SUPPORT");
        }
    }

    private static void validateMap(GameMap map, Content content, List<String> problems) {
        String where = "map '" + map.id() + "'";
        if (!(map.maxX() > map.minX()) || !(map.maxZ() > map.minZ())) {
            problems.add(where + ": bounds are empty");
            return;
        }
        for (Obstacle o : map.obstacleList()) {
            double thinnest = Math.min(o.maxX() - o.minX(), Math.min(o.maxZ() - o.minZ(), o.maxY() - o.minY()));
            if (thinnest < MIN_OBSTACLE_THICKNESS) {
                problems.add(where + ": obstacle '" + o.id() + "' is thinner than " + MIN_OBSTACLE_THICKNESS + " m");
            }
            if (o.isRamp() && o.slope() > MAX_RAMP_SLOPE) {
                problems.add(where + ": ramp '" + o.id() + "' is steeper than " + MAX_RAMP_SLOPE + " (rise over run)");
            }
            if (o.isRamp() && !o.blocksMovement()) {
                problems.add(where + ": ramp '" + o.id() + "' must block movement (it is a solid wedge to walk on)");
            }
        }
        if (map.spawns().size() != 2) {
            problems.add(where + ": needs spawn lists for team 0 and team 1");
            return;
        }
        for (int team = 0; team < 2; team++) {
            List<SpawnPoint> spawns = map.spawns().get(team);
            if (spawns.size() < MIN_SPAWNS_PER_TEAM) {
                problems.add(where + ": team " + team + " needs at least " + MIN_SPAWNS_PER_TEAM + " spawn points");
            }
            for (SpawnPoint spawn : spawns) {
                if (insideSolid(map, spawn.x(), spawn.y(), spawn.z(), 0.5)) {
                    problems.add(where + ": spawn (" + spawn.x() + ", " + spawn.z() + ") is inside an obstacle");
                }
            }
        }
        for (PickupSpawn pickup : map.pickups()) {
            PickupDefinition definition = content.pickups().find(pickup.pickupId());
            if (definition == null) {
                problems.add(where + ": pickup '" + pickup.pickupId() + "' is not defined");
            }
            if (insideSolid(map, pickup.x(), pickup.y(), pickup.z(), 0.2)) {
                problems.add(where + ": pickup '" + pickup.pickupId() + "' is inside an obstacle");
            }
        }
        NavGraph nav = map.nav();
        if (nav.size() == 0) {
            problems.add(where + ": navigation graph is empty");
        } else if (!map.spawns().get(0).isEmpty() && !map.spawns().get(1).isEmpty()) {
            SpawnPoint a = map.spawns().get(0).get(0);
            SpawnPoint b = map.spawns().get(1).get(0);
            int from = nav.nearestNode(a.x(), a.z());
            int to = nav.nearestNode(b.x(), b.z());
            if (nav.pathLength(from, to) == Float.POSITIVE_INFINITY) {
                problems.add(where + ": there is no walkable path between the two teams' spawns");
            }
        }
    }

    /** True when the point at height {@code y} lies inside a solid obstacle (standing on top of one is fine). */
    private static boolean insideSolid(GameMap map, double x, double y, double z, double margin) {
        for (Obstacle o : map.obstacleList()) {
            if (o.blocksMovement() && o.height() > 0.35 && o.containsXZ(x, z, margin)
                    && y < o.topAt(x, z) - SURFACE_TOLERANCE) {
                return true;
            }
        }
        return false;
    }
}
