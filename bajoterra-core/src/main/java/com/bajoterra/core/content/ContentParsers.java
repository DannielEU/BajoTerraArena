package com.bajoterra.core.content;

import com.bajoterra.core.character.BlasterSpec;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Posture;
import com.bajoterra.core.item.PickupDefinition;
import com.bajoterra.core.item.PickupRequirement;
import com.bajoterra.core.move.MoveBehavior;
import com.bajoterra.core.move.MoveBehaviorRegistry;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.sim.Ticks;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.ElementChart;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.SlugRole;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/** Converts the JSON of each content kind into immutable definitions, reporting precise field-level errors. */
final class ContentParsers {

    private ContentParsers() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Reads a duration given either as {@code <base>Ticks} or {@code <base>Seconds}. */
    static int ticks(Spec spec, String base, int fallback) {
        if (spec.has(base + "Ticks")) {
            return spec.requireInt(base + "Ticks", 0, 1_000_000);
        }
        if (spec.has(base + "Seconds")) {
            return Ticks.fromSeconds(spec.requireDouble(base + "Seconds", 0, 10_000));
        }
        return fallback;
    }

    static ElementChart elements(Spec root) {
        ElementChart.Builder builder = ElementChart.builder();
        for (String attackerName : root.fieldNames()) {
            Element attacker = parseElement(root, attackerName);
            Spec row = root.requireObject(attackerName);
            for (String defenderName : row.fieldNames()) {
                builder.set(attacker, parseElement(row, defenderName), row.requireDouble(defenderName, 0, 10));
            }
        }
        return builder.build();
    }

    private static Element parseElement(Spec spec, String name) {
        try {
            return Element.parse(name);
        } catch (IllegalArgumentException e) {
            throw spec.fail(name, "'" + name + "' is not an element");
        }
    }

    static MoveDefinition move(Spec spec, MoveBehaviorRegistry behaviors) {
        String id = spec.requireString("id");
        boolean holdable = spec.optBoolean("holdable", false);
        int mask = 0;
        for (String name : spec.stringList("allowedFrom")) {
            try {
                mask |= Posture.valueOf(name.trim().toUpperCase(Locale.ROOT)).bit();
            } catch (IllegalArgumentException e) {
                throw spec.fail("allowedFrom", "'" + name + "' is not a posture");
            }
        }
        if (mask == 0) {
            throw spec.fail("allowedFrom", "needs at least one posture");
        }
        int invulnerableFrom = 0;
        int invulnerableTo = 0;
        Spec invulnerable = spec.optObject("invulnerable");
        if (invulnerable != null) {
            invulnerableFrom = ticks(invulnerable, "from", 0);
            invulnerableTo = ticks(invulnerable, "to", 0);
        }
        MoveBehavior behavior = behaviors.create(spec.optObject("behavior"));
        return new MoveDefinition(id, 0,
                ticks(spec, "duration", 0),
                ticks(spec, "cooldown", 0),
                holdable,
                spec.optBoolean("stance", false),
                spec.optBoolean("interruptible", holdable),
                spec.optBoolean("blocksFiring", false),
                mask,
                spec.optDouble("speedMultiplier", 1.0, 0, 10),
                spec.optDouble("hitboxHeightMultiplier", 1.0, 0.1, 1.5),
                invulnerableFrom,
                invulnerableTo,
                behavior);
    }

    static CharacterDefinition character(Spec spec, Registry<MoveDefinition> moves) {
        List<MoveDefinition> moveSet = new ArrayList<>();
        for (String moveId : spec.stringList("moves")) {
            MoveDefinition move = moves.find(moveId);
            if (move == null) {
                throw spec.fail("moves", "unknown move '" + moveId + "'");
            }
            moveSet.add(move);
        }
        Spec blaster = spec.requireObject("blaster");
        BlasterSpec blasterSpec = new BlasterSpec(
                Math.max(1, ticks(blaster, "fireInterval", 21)), blaster.optInt("reloadShots", 3, 0, 9));
        String id = spec.requireString("id");
        return new CharacterDefinition(id, 0,
                spec.optString("name", id),
                spec.requireDouble("maxHp", 1, 10_000),
                spec.requireDouble("maxShield", 0, 10_000),
                spec.requireDouble("speed", 0.5, 30),
                spec.optDouble("hitRadius", 0.4, 0.1, 2),
                spec.optDouble("hitHeight", 1.8, 0.5, 4),
                spec.optEnum("affinity", Element.class, Element.NEUTRAL),
                spec.optDouble("healPotency", 1.0, 0.1, 5),
                moveSet,
                blasterSpec);
    }

    static PickupDefinition pickup(Spec spec, EffectRegistry effects) {
        String id = spec.requireString("id");
        return new PickupDefinition(id, 0, spec.optString("name", id),
                effects.createAll(spec.list("effects")),
                Math.max(1, ticks(spec, "respawn", 900)),
                spec.optDouble("radius", 1.2, 0.2, 10),
                spec.optEnum("requirement", PickupRequirement.class, PickupRequirement.ANY));
    }

    static SlugDefinition slug(Spec spec, EffectRegistry effects) {
        String id = spec.requireString("id");
        ProjectileSpec projectile = ProjectileSpec.standard();
        Spec flight = spec.optObject("projectile");
        if (flight != null) {
            int fuse = 0;
            if (flight.optBoolean("sticky", false)) {
                fuse = Math.max(1, ticks(flight, "fuse", 90));
            }
            projectile = new ProjectileSpec(
                    flight.optDouble("speed", 45, 1, 200),
                    flight.optDouble("gravity", 4, 0, 60),
                    flight.optDouble("radius", 0.25, 0.05, 2),
                    Math.max(1, ticks(flight, "lifetime", 120)),
                    flight.optInt("pierce", 0, 0, 5),
                    fuse);
        }
        return SlugDefinition.builder(id)
                .name(spec.optString("name", id))
                .element(spec.optEnum("element", Element.class, Element.NEUTRAL))
                .role(spec.optEnum("role", SlugRole.class, SlugRole.DAMAGE))
                .targeting(spec.optEnum("targeting", TargetingRule.class, TargetingRule.ENEMIES))
                .projectile(projectile)
                .fireDelayMultiplier(spec.optDouble("fireDelayMultiplier", 1.0, 0.1, 5))
                .reloadShotsOverride(spec.optInt("reloadShots", -1, -1, 9))
                .pierceWorld(spec.optBoolean("pierceWorld", false))
                .botWeight(spec.optDouble("botWeight", 1.0, 0.01, 10))
                .onHit(effects.createAll(spec.list("onHit")))
                .onWorldHit(effects.createAll(spec.list("onWorldHit")))
                .onTick(effects.createAll(spec.list("onTick")))
                .onExpire(effects.createAll(spec.list("onExpire")))
                .build();
    }

    static GameMap map(Spec spec) {
        String id = spec.requireString("id");
        Spec bounds = spec.requireObject("bounds");
        List<Obstacle> obstacles = new ArrayList<>();
        int counter = 0;
        for (Spec o : spec.list("obstacles")) {
            double[] min = o.requireVector("min", 3);
            double[] max = o.requireVector("max", 3);
            Obstacle.Rise rise;
            try {
                rise = Obstacle.Rise.parse(o.optString("ramp", ""));
            } catch (IllegalArgumentException e) {
                throw o.fail("ramp", e.getMessage());
            }
            obstacles.add(new Obstacle(o.optString("id", "obstacle_" + counter++),
                    min[0], min[1], min[2], max[0], max[1], max[2],
                    o.optBoolean("blocksProjectiles", true),
                    o.optBoolean("blocksMovement", true),
                    o.optBoolean("blocksLineOfSight", true),
                    rise));
        }
        Spec spawnSpec = spec.requireObject("spawns");
        List<List<SpawnPoint>> spawns = new ArrayList<>();
        for (String team : List.of("0", "1")) {
            List<SpawnPoint> points = new ArrayList<>();
            for (Spec s : spawnSpec.list(team)) {
                double[] pos = s.requireVector("pos", 3);
                points.add(new SpawnPoint(pos[0], pos[1], pos[2], Math.toRadians(s.optDouble("yaw", 0, -360, 360))));
            }
            spawns.add(points);
        }
        List<PickupSpawn> pickups = new ArrayList<>();
        for (Spec p : spec.list("pickups")) {
            double[] pos = p.requireVector("pos", 3);
            pickups.add(new PickupSpawn(p.requireString("item"), pos[0], pos[1], pos[2]));
        }
        Spec nav = spec.optObject("nav");
        double navCell = nav == null ? 3.0 : nav.optDouble("cellSize", 3.0, 1.0, 10.0);
        return new GameMap(id, 0, spec.optString("name", id),
                bounds.requireDouble("minX", -1000, 1000), bounds.requireDouble("maxX", -1000, 1000),
                bounds.requireDouble("minZ", -1000, 1000), bounds.requireDouble("maxZ", -1000, 1000),
                obstacles, spawns, pickups, navCell);
    }
}
