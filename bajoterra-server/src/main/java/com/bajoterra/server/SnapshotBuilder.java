package com.bajoterra.server;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.effect.StatusSet;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.item.Pickup;
import com.bajoterra.core.move.MoveBehavior;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.move.MoveRunner;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.rules.Match;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.stats.StatBlock;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import com.bajoterra.protocol.generated.CharacterInfo;
import com.bajoterra.protocol.generated.CombatantState;
import com.bajoterra.protocol.generated.ContentDetails;
import com.bajoterra.protocol.generated.ContentManifest;
import com.bajoterra.protocol.generated.EventBatch;
import com.bajoterra.protocol.generated.EventKind;
import com.bajoterra.protocol.generated.LocalState;
import com.bajoterra.protocol.generated.MapData;
import com.bajoterra.protocol.generated.MatchInfo;
import com.bajoterra.protocol.generated.MoveInfo;
import com.bajoterra.protocol.generated.ObstacleInfo;
import com.bajoterra.protocol.generated.PickupInfo;
import com.bajoterra.protocol.generated.PickupPlacement;
import com.bajoterra.protocol.generated.PickupState;
import com.bajoterra.protocol.generated.Posture;
import com.bajoterra.protocol.generated.ProjectileState;
import com.bajoterra.protocol.generated.SlotState;
import com.bajoterra.protocol.generated.SlugInfo;
import com.bajoterra.protocol.generated.Snapshot;
import com.bajoterra.protocol.generated.SpawnInfo;
import com.bajoterra.protocol.generated.StatusState;
import com.bajoterra.protocol.generated.Targeting;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;

/** Converts the simulation state into the wire messages (the only place where core and protocol meet). */
public final class SnapshotBuilder {

    /** Upper bound on events sent per batch so one chaotic tick cannot produce an oversized frame. */
    public static final int MAX_EVENTS_PER_BATCH = 200;

    private SnapshotBuilder() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Lists every content id in index order; clients resolve snapshot indices through it. */
    public static ContentManifest manifest(Content content) {
        ContentManifest.Builder builder = ContentManifest.newBuilder();
        content.slugs().all().forEach(slug -> builder.addSlugIds(slug.id()));
        content.characters().all().forEach(character -> builder.addCharacterIds(character.id()));
        content.moves().all().forEach(move -> builder.addMoveIds(move.id()));
        content.maps().all().forEach(map -> builder.addMapIds(map.id()));
        content.pickups().all().forEach(pickup -> builder.addPickupIds(pickup.id()));
        for (Element element : Element.values()) {
            builder.addElementIds(element.name());
        }
        return builder.build();
    }

    /**
     * Numeric content data for the client (HUD labels and movement prediction), in the same index order as
     * {@link #manifest(Content)}. Times are already in ticks and posture masks already resolved.
     */
    public static ContentDetails details(Content content) {
        ContentDetails.Builder builder = ContentDetails.newBuilder();
        for (SlugDefinition slug : content.slugs().all()) {
            builder.addSlugs(SlugInfo.newBuilder().setId(slug.id()).setName(slug.name())
                    .setElement(slug.element().ordinal())
                    .setTargeting(Targeting.forNumber(slug.targeting().ordinal()))
                    .setRole(slug.role().name())
                    .setProjectileRadius((float) slug.projectile().radius())
                    .setProjectileSpeed((float) slug.projectile().speed())
                    .setFireDelayMultiplier((float) slug.fireDelayMultiplier())
                    .setSticky(slug.projectile().sticky())
                    .setFuseTicks(slug.projectile().fuseTicks())
                    .setBlastRadius((float) slug.onExpire().areaRadius()));
        }
        for (var character : content.characters().all()) {
            CharacterInfo.Builder info = CharacterInfo.newBuilder().setId(character.id()).setName(character.name())
                    .setMaxHp((float) character.maxHp()).setMaxShield((float) character.maxShield())
                    .setSpeed((float) character.speed()).setHitRadius((float) character.hitRadius())
                    .setHitHeight((float) character.hitHeight())
                    .setFireIntervalTicks(character.blaster().fireIntervalTicks())
                    .setReloadShots(character.blaster().reloadShots())
                    .setAffinity(character.affinity().ordinal());
            character.moves().forEach(move -> info.addMoveIndices(move.index()));
            builder.addCharacters(info);
        }
        for (MoveDefinition move : content.moves().all()) {
            builder.addMoves(moveInfo(move));
        }
        for (var pickup : content.pickups().all()) {
            builder.addPickups(PickupInfo.newBuilder().setId(pickup.id()).setName(pickup.name())
                    .setRadius((float) pickup.radius()));
        }
        return builder.build();
    }

    private static MoveInfo moveInfo(MoveDefinition move) {
        MoveInfo.Builder builder = MoveInfo.newBuilder().setId(move.id())
                .setDurationTicks(move.durationTicks()).setCooldownTicks(move.cooldownTicks())
                .setHoldable(move.holdable()).setStance(move.stance()).setInterruptible(move.interruptible())
                .setBlocksFiring(move.blocksFiring()).setAllowedFromMask(move.allowedFromMask())
                .setSpeedMultiplier((float) move.speedMultiplier())
                .setHitboxHeightMultiplier((float) move.hitboxHeightMultiplier())
                .setInvulnerableFromTick(move.invulnerableFromTick())
                .setInvulnerableToTick(move.invulnerableToTick());
        MoveBehavior behavior = move.behavior();
        builder.setBehaviorType(behavior.type());
        // Sorted so that the encoded bytes do not depend on the iteration order of Map.of (it varies per JVM run).
        for (Map.Entry<String, Double> param : new TreeMap<>(behavior.params()).entrySet()) {
            builder.putBehaviorParams(param.getKey(), param.getValue().floatValue());
        }
        return builder.build();
    }

    /** The arena geometry, spawn points and pickup placements, for drawing and for client-side collision. */
    public static MapData mapData(GameMap map, Content content) {
        MapData.Builder builder = MapData.newBuilder().setName(map.name())
                .setMinX((float) map.minX()).setMaxX((float) map.maxX())
                .setMinZ((float) map.minZ()).setMaxZ((float) map.maxZ())
                .setGroundY((float) map.groundY());
        for (Obstacle o : map.obstacleList()) {
            builder.addObstacles(ObstacleInfo.newBuilder().setId(o.id())
                    .setMinX((float) o.minX()).setMinY((float) o.minY()).setMinZ((float) o.minZ())
                    .setMaxX((float) o.maxX()).setMaxY((float) o.maxY()).setMaxZ((float) o.maxZ())
                    .setBlocksProjectiles(o.blocksProjectiles()).setBlocksMovement(o.blocksMovement())
                    .setBlocksLineOfSight(o.blocksLineOfSight())
                    .setRise(o.rise().ordinal()));
        }
        for (PickupSpawn spawn : map.pickups()) {
            builder.addPickups(PickupPlacement.newBuilder()
                    .setPickupIndex(content.pickups().require(spawn.pickupId()).index())
                    .setX((float) spawn.x()).setY((float) spawn.y()).setZ((float) spawn.z()));
        }
        List<List<SpawnPoint>> spawns = map.spawns();
        for (int team = 0; team < spawns.size(); team++) {
            for (SpawnPoint spawn : spawns.get(team)) {
                builder.addSpawns(SpawnInfo.newBuilder().setTeam(team).setX((float) spawn.x())
                        .setY((float) spawn.y()).setZ((float) spawn.z()).setYaw((float) spawn.yaw()));
            }
        }
        return builder.build();
    }

    /**
     * The extra state the owner of {@code c} needs to continue the movement simulation from this tick (see
     * {@code LocalState} in game.proto). {@code tick} is the tick the snapshot describes.
     */
    public static LocalState localState(Combatant c, int tick) {
        StatBlock fromStatuses = new StatBlock();
        c.statuses.modify(fromStatuses);
        MoveRunner runner = c.moves;
        LocalState.Builder builder = LocalState.newBuilder()
                .setVelY((float) c.vel.y)
                .setKnockX((float) c.knock.x).setKnockZ((float) c.knock.z)
                .setGrounded(c.grounded)
                .setStatusSpeedMultiplier((float) fromStatuses.speedMultiplier)
                .setStunned(fromStatuses.stunned)
                .setMoveElapsedTicks(runner.elapsed())
                .setMoveDirX((float) runner.dirX()).setMoveDirZ((float) runner.dirZ());
        for (int slot = 0; slot < c.character.moves().size(); slot++) {
            builder.addMoveCooldownTicks(Math.max(0, runner.cooldownUntil(slot) - tick));
        }
        return builder.build();
    }

    /** Builds the full world snapshot. {@code ackSequence} is filled per receiving player afterwards. */
    public static Snapshot snapshot(GameSimulation sim) {
        Snapshot.Builder builder = Snapshot.newBuilder().setTick(sim.tickCount()).setMatch(matchInfo(sim.match()));
        for (Combatant c : sim.combatants()) {
            builder.addCombatants(combatant(c));
        }
        ProjectilePool pool = sim.projectiles();
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (p.active) {
                builder.addProjectiles(ProjectileState.newBuilder().setId(p.id).setSlugIndex(p.slug.index())
                        .setOwner(p.owner.id).setX((float) p.pos.x).setY((float) p.pos.y).setZ((float) p.pos.z)
                        .setVx((float) p.vel.x).setVy((float) p.vel.y).setVz((float) p.vel.z)
                        .setStuck(p.stuck).setFuseTicks(p.stuck ? Math.max(0, p.ticksLeft) : 0)
                        .setStuckTo(p.stuck && p.stuckTo != null ? p.stuckTo.id : -1));
            }
        }
        for (Pickup pickup : sim.pickups()) {
            builder.addPickups(PickupState.newBuilder().setIndex(pickup.index).setAvailable(pickup.available));
        }
        return builder.build();
    }

    public static MatchInfo matchInfo(Match match) {
        return MatchInfo.newBuilder()
                .setPhase(com.bajoterra.protocol.generated.MatchPhase.forNumber(match.phase().ordinal()))
                .setTicksLeft(Math.max(0, match.ticksLeft()))
                .setRound(match.roundNumber())
                .setWinsTeam0(match.wins(0))
                .setWinsTeam1(match.wins(1))
                .setLastRoundWinner(match.lastRoundWinner())
                .setWinner(match.winner())
                .build();
    }

    public static CombatantState combatant(Combatant c) {
        CombatantState.Builder builder = CombatantState.newBuilder()
                .setId(c.id)
                .setX((float) c.pos.x).setY((float) c.pos.y).setZ((float) c.pos.z)
                .setYaw((float) c.yaw).setPitch((float) c.pitch)
                .setHp((float) c.vitals.hp()).setShield((float) c.vitals.shield())
                .setOvershield((float) c.vitals.overshield())
                .setAlive(c.alive)
                .setPosture(Posture.forNumber(c.posture.ordinal()))
                .setSelectedSlot(c.loadout.selected())
                .setHitboxHeight((float) c.hitHeight())
                .setCourage(c.courage);
        MoveDefinition active = c.moves.active();
        builder.setActiveMove(active == null ? -1 : active.index());
        for (int slot = 0; slot < Loadout.SIZE; slot++) {
            builder.addSlots(SlotState.newBuilder().setSlugIndex(c.loadout.slug(slot).index())
                    .setShotsUntilReady(c.loadout.shotsUntilReady(slot)));
        }
        for (int i = 0; i < StatusSet.CAPACITY; i++) {
            StatusInstance status = c.statuses.at(i);
            if (status.active()) {
                builder.addStatuses(StatusState.newBuilder().setId(status.definition().statusId())
                        .setRemainingTicks(Math.max(0, status.remainingTicks())).setStacks(status.stacks()));
            }
        }
        return builder.build();
    }

    /**
     * Collects the events written since {@code fromSequence}.
     *
     * @return the batch; its {@code nextSequence} is available through {@link Batch#nextSequence()}
     */
    public static Batch events(GameSimulation sim, long fromSequence) {
        EventBuffer events = sim.events();
        long start = Math.max(fromSequence, events.oldestAvailable());
        long end = Math.min(events.written(), start + MAX_EVENTS_PER_BATCH);
        EventBatch.Builder builder = EventBatch.newBuilder().setTick(sim.tickCount());
        for (long sequence = start; sequence < end; sequence++) {
            GameEvent event = events.at(sequence);
            builder.addEvents(com.bajoterra.protocol.generated.GameEvent.newBuilder()
                    .setKind(EventKind.forNumber(event.type.ordinal()))
                    .setTick(event.tick).setActor(event.actor).setSubject(event.subject)
                    .setA(event.a).setB(event.b)
                    .setX((float) event.x).setY((float) event.y).setZ((float) event.z)
                    .setValue((float) event.value));
        }
        return new Batch(builder.build(), end);
    }

    /** An event batch plus the sequence number to resume from. */
    public record Batch(EventBatch batch, long nextSequence) {
    }

    /** Ticks between two snapshots. */
    public static int snapshotInterval() {
        return SimulationConstants.TICKS_PER_SNAPSHOT;
    }
}
