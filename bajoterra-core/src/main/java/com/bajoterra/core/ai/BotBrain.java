package com.bajoterra.core.ai;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.entity.InputSource;
import com.bajoterra.core.entity.InputState;
import com.bajoterra.core.hazard.Hazard;
import com.bajoterra.core.hazard.Tornado;
import com.bajoterra.core.hazard.Web;
import com.bajoterra.core.item.Pickup;
import com.bajoterra.core.math.Trig;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.NavGraph;
import com.bajoterra.core.world.World;
import java.util.List;

/**
 * Bot controller. It produces exactly the same {@link InputState} a human would, so the server treats both alike.
 *
 * <p>Decisions run at 10 Hz ({@link SimulationConstants#BOT_THINK_INTERVAL_TICKS}) through a behavior tree
 * (heal a wounded ally, else attack a visible enemy, else hunt the last known position, else patrol); aiming,
 * movement and the trigger are updated every tick from those decisions. A bot that notices an enemy grenade stuck
 * next to it runs out of the blast for a moment, whatever the tree chose, and one that notices a blinding slug
 * about to go off turns its eyes away from it (a blinded bot sees nobody until it wears off). It steps out of
 * enemy tornadoes and around the enemy webs it notices, goes for the megamorph crystal and for health when it has
 * nothing to fight, and fires a fusion shot as soon as its energy is full.
 *
 * <p>Getting about: the bot steers for the farthest point of its path it can walk to in a straight line (body-wide
 * sweeps against the world, ice walls included), so it neither hugs corners nor walks into the wall between it and
 * the nearest waypoint. When it makes no progress it backs off along the freest direction, only jumps when what
 * stops it is low enough to jump onto, and after a few failed tries gives up on that spot for a while. Nothing here
 * allocates after construction.
 */
public final class BotBrain implements InputSource {

    private static final double EYE_FRACTION = 0.9;
    private static final double CHEST_FRACTION = 0.55;
    private static final double PREFERRED_MAX_RANGE = 24.0;
    private static final double PREFERRED_MIN_RANGE = 7.0;
    private static final double LOW_COVER_HEIGHT = 1.3;
    private static final double MAX_COVER_SEARCH = 26.0;
    /** An ice wall goes up this far in front of the bot that throws it, towards its target. */
    private static final double WALL_AHEAD = 2.6;
    /** No wall against a target closer than this: there is no time to hide. */
    private static final double WALL_MIN_RANGE = 6.0;
    /** Ticks between two walls of the same bot. */
    private static final int WALL_COOLDOWN_TICKS = 420;
    /** Cosine of the half-angle of the cone in which a bot notices incoming slugs (about 75 degrees). */
    private static final double DODGE_VIEW_COS = 0.26;
    /** Above this {@link com.bajoterra.core.stats.StatBlock#blindness} a bot sees nothing. */
    static final double BLIND_SIGHT = 0.35;
    /** A flying blinding slug is worth looking away from when it has fewer ticks than this left to go off. */
    private static final int FLASH_WARNING_TICKS = 40;
    /** Height above its feet at which the bot checks that a straight walk is clear. */
    private static final double WALK_PROBE_HEIGHT = 0.5;
    /** Highest obstacle (above the feet) a bot tries to jump onto when it is stuck against it. */
    private static final double JUMPABLE = 1.05;
    /** Farthest a bot walks straight to its waypoint without asking the path. */
    private static final double DIRECT_RANGE = 14.0;
    /** A target out of sight for at most this many ticks is still the bot's target (it does not shoot at it). */
    private static final int TARGET_GRACE_TICKS = 24;
    /** Ticks after its last hit during which a hurt bot that took cover stays there once the enemy is out of sight. */
    private static final int HOLD_COVER_TICKS = 150;
    /** Metres by which another way must be shorter before the bot gives up the one it took. */
    private static final double ROUTE_HYSTERESIS = 2.5;
    /** Path waypoints looked ahead for one in a straight line of sight. */
    private static final int LOOK_AHEAD = 6;
    /** Failed progress checks in a row before the bot gives up on where it was going. */
    private static final int GIVE_UP_AFTER = 3;
    /** How far away a bot without a fight goes for each kind of pickup. */
    private static final double CRYSTAL_RANGE = 48.0;
    private static final double HEALTH_RANGE = 26.0;

    private BotProfile profile;
    private double skill;
    private final BotContext context;
    private final BtNode tree;
    private final SlugUtilityScorer scorer = new SlugUtilityScorer();
    private final AimSolver.Aim aim = new AimSolver.Aim();
    private int nextPlanTick;
    private double desiredYaw;
    private double desiredPitch;

    public BotBrain(double skill) {
        this.skill = BotProfile.clampSkill(skill);
        this.profile = BotProfile.fromSkill(this.skill);
        this.context = new BotContext();
        this.tree = buildTree();
    }

    /** How well this bot plays, in [0, 1]. */
    public double skill() {
        return skill;
    }

    /** Changes how well the bot plays from now on (the difficulty director calls it between rounds). */
    public void setSkill(double newSkill) {
        skill = BotProfile.clampSkill(newSkill);
        profile = BotProfile.fromSkill(skill);
    }

    @Override
    public void onRoundStart(GameSimulation sim, Combatant self) {
        context.reset();
        context.bind(sim, self);
        resolveMoveSlots(self);
        desiredYaw = self.yaw;
        desiredPitch = 0;
        nextPlanTick = sim.tickCount() + (self.id * 2) % SimulationConstants.BOT_THINK_INTERVAL_TICKS;
    }

    @Override
    public void think(GameSimulation sim, Combatant self) {
        context.bind(sim, self);
        if (context.rollSlot == -2) {
            resolveMoveSlots(self);
        }
        int tick = sim.tickCount();
        if (tick >= nextPlanTick) {
            plan(tick);
            nextPlanTick = tick + SimulationConstants.BOT_THINK_INTERVAL_TICKS;
        }
        execute(tick);
    }

    /** Runs a full decision cycle right now (perception and behavior tree); used by benchmarks and tests. */
    public void planNow(GameSimulation sim, Combatant self) {
        context.bind(sim, self);
        if (context.rollSlot == -2) {
            resolveMoveSlots(self);
        }
        plan(sim.tickCount());
    }

    // ------------------------------------------------------------------------------------------ planning

    private BtNode buildTree() {
        BtNode healAlly = new Sequence(
                ctx -> ctx.ally != null ? Status.SUCCESS : Status.FAILURE,
                this::chooseSupportSlug,
                this::beginHeal);
        BtNode attack = new Sequence(
                ctx -> ctx.target != null ? Status.SUCCESS : Status.FAILURE,
                this::chooseAttackSlug,
                this::beginAttack);
        BtNode holdCover = ctx -> {
            // Hurt and hidden from the enemy it ran from: stay behind the cover a moment to recover before hunting.
            int tick = ctx.sim().tickCount();
            if (ctx.coverIndex < 0 || tick - ctx.lastHurtTick > HOLD_COVER_TICKS
                    || ctx.self().vitals.totalFraction() > profile.retreatHealth() + 0.25) {
                return Status.FAILURE;
            }
            GameMap map = ctx.sim().map();
            ctx.intent = BotContext.Intent.NONE;
            ctx.waypointX = map.coverX(ctx.coverIndex);
            ctx.waypointZ = map.coverZ(ctx.coverIndex);
            boolean there = Trig.hypot(ctx.waypointX - ctx.self().pos.x, ctx.waypointZ - ctx.self().pos.z) < 0.9;
            ctx.hasWaypoint = !there;
            ctx.moveSpeedFactor = there ? 0.0 : 1.0;
            ctx.inCover = there;
            ctx.wantCrouch = there && map.coverHeight(ctx.coverIndex) < LOW_COVER_HEIGHT;
            return Status.SUCCESS;
        };
        BtNode hunt = ctx -> {
            if (ctx.sim().tickCount() - ctx.lastSeenTick > profile.memoryTicks()) {
                return Status.FAILURE;
            }
            ctx.intent = BotContext.Intent.NONE;
            ctx.waypointX = ctx.lastSeenX;
            ctx.waypointZ = ctx.lastSeenZ;
            ctx.hasWaypoint = true;
            ctx.moveSpeedFactor = 1.0;
            return Status.SUCCESS;
        };
        BtNode patrol = ctx -> {
            ctx.intent = BotContext.Intent.NONE;
            chooseSearchGoal(ctx);
            return Status.SUCCESS;
        };
        return new Selector(healAlly, attack, holdCover, hunt, patrol);
    }

    private void plan(int tick) {
        Combatant self = context.self();
        context.intent = BotContext.Intent.NONE;
        context.wantFire = false;
        context.wantCrouch = false;
        perceive(tick);
        if (self.stats.blindness > BLIND_SIGHT) {
            stumble(tick);
            return;
        }
        detectThreat(tick);
        updateStuck(tick);
        context.damageTakenAtLastPlan = self.damageTaken;
        if (tree.tick(context) == Status.FAILURE) {
            context.intent = BotContext.Intent.NONE;
        }
        seekPickup();
        planWall(tick);
        avoidGrenades(tick);
        avoidFire();
        avoidTornadoes();
        avoidFlashes(tick);
        planRoute(tick);
    }

    /**
     * Goes for a pickup worth having: the megamorph crystal whenever it is up (and, in a fight, when it is only a few
     * steps away), health or shield when hurt and there is nobody to fight. Pickups on top of hills are left alone
     * (bots keep to the floor).
     */
    private void seekPickup() {
        Combatant self = context.self();
        // An enemy seen a moment ago is still the fight, even round a corner.
        boolean fighting = context.intent != BotContext.Intent.NONE
                || context.sim().tickCount() - context.lastSeenTick < 120;
        double bestScore = 0;
        Pickup best = null;
        for (Pickup pickup : context.sim().pickups()) {
            if (!pickup.available || pickup.pos.y > self.pos.y + 0.3
                    || !pickup.definition.requirement().satisfiedBy(self)) {
                continue;
            }
            double distance = Trig.hypot(pickup.pos.x - self.pos.x, pickup.pos.z - self.pos.z);
            double range;
            double value;
            switch (pickup.definition.id()) {
                case "megamorph_crystal" -> {
                    range = fighting ? 7.0 : CRYSTAL_RANGE;
                    value = 1.0;
                }
                case "health_pack" -> {
                    range = fighting ? 4.0 : HEALTH_RANGE;
                    value = self.vitals.hpFraction() < 0.65 ? 0.9 : 0;
                }
                case "shield_cell" -> {
                    range = fighting ? 4.0 : HEALTH_RANGE * 0.7;
                    value = self.vitals.shield() < self.vitals.maxShield() * 0.5 ? 0.6 : 0;
                }
                default -> {
                    range = fighting ? 4.0 : 14.0;
                    value = 0.4;
                }
            }
            double score = value * (1 - distance / range);
            if (score > bestScore) {
                bestScore = score;
                best = pickup;
            }
        }
        if (best == null) {
            return;
        }
        context.waypointX = best.pos.x;
        context.waypointZ = best.pos.z;
        context.hasWaypoint = true;
        context.moveSpeedFactor = 1.0;
        context.inCover = false;
        context.coverIndex = -1;
        context.wantCrouch = false;
    }

    /** A bot caught near an enemy tornado runs out of its reach, away from its eye. */
    private void avoidTornadoes() {
        Combatant self = context.self();
        Tornado wind = context.sim().tornadoes().enemyAt(self, self.pos.x, self.pos.z, 1.0);
        if (wind == null) {
            return;
        }
        double awayX = self.pos.x - wind.x;
        double awayZ = self.pos.z - wind.z;
        double length = Trig.hypot(awayX, awayZ);
        if (length < 1e-3) {
            awayX = -Trig.sin(self.yaw);
            awayZ = -Trig.cos(self.yaw);
            length = 1;
        }
        double out = wind.spec.radius() + 2.0;
        context.waypointX = self.pos.x + awayX / length * out;
        context.waypointZ = self.pos.z + awayZ / length * out;
        context.hasWaypoint = true;
        context.moveSpeedFactor = 1.0;
        context.inCover = false;
        context.wantCrouch = false;
    }

    // ------------------------------------------------------------------------------------------ getting about

    /**
     * Works out where to steer this decision cycle: the waypoint itself when it is close and in a clear straight
     * line, otherwise the farthest node of the path towards it that is.
     */
    private void planRoute(int tick) {
        context.hasRoute = false;
        if (!context.hasWaypoint || context.moveSpeedFactor <= 0) {
            return;
        }
        double goalX = context.waypointX;
        double goalZ = context.waypointZ;
        if (tick < context.detourUntilTick) {
            goalX = context.detourX;
            goalZ = context.detourZ;
        }
        route(context.self(), goalX, goalZ);
    }

    private void route(Combatant self, double goalX, double goalZ) {
        context.hasRoute = true;
        context.routeX = goalX;
        context.routeZ = goalZ;
        if (Trig.hypot(goalX - self.pos.x, goalZ - self.pos.z) < DIRECT_RANGE && walkable(self, goalX, goalZ)) {
            return;
        }
        NavGraph nav = context.sim().map().nav();
        if (nav.size() == 0) {
            return;
        }
        int to = reachableNodeNear(goalX, goalZ, self);
        int from = to < 0 ? -1 : entryNode(self, to);
        if (from < 0 || to < 0) {
            return;
        }
        int best = from;
        int current = from;
        for (int k = 0; k < LOOK_AHEAD && current != to; k++) {
            int next = nav.nextNode(current, to);
            if (next < 0 || !walkable(self, nav.nodeX(next), nav.nodeZ(next))) {
                break;
            }
            best = next;
            current = next;
        }
        if (best == to && walkable(self, goalX, goalZ)) {
            return;
        }
        if (best == from && Trig.hypot(nav.nodeX(from) - self.pos.x, nav.nodeZ(from) - self.pos.z) < 0.8) {
            // Standing on the node already: head for the next one even if a corner hides it.
            int next = from == to ? -1 : nav.nextNode(from, to);
            if (next >= 0 && walkable(self, nav.nodeX(next), nav.nodeZ(next))) {
                best = next;
            } else {
                return;
            }
        }
        context.routeX = nav.nodeX(best);
        context.routeZ = nav.nodeZ(best);
    }

    /** True when the bot's body fits along a straight walk from where it stands to (x, z), at its own height. */
    private boolean walkable(Combatant self, double x, double z) {
        double y = self.pos.y + WALK_PROBE_HEIGHT;
        return context.sim().world().hasClearShot(self.pos.x, y, self.pos.z, x, y, z, self.hitRadius() + 0.05);
    }

    /**
     * The path node to join the path at: among the nodes the bot can walk to in a straight line, the one with the
     * shortest way to {@code to} counting the walk to it. Weighing the whole way (not just the nearest node) keeps the
     * choice steady from one decision to the next, so the bot does not swing between a node ahead and one behind.
     */
    private int entryNode(Combatant self, int to) {
        NavGraph nav = context.sim().map().nav();
        int best = -1;
        double bestCost = Double.MAX_VALUE;
        for (int i = 0; i < nav.size(); i++) {
            double walk = Trig.hypot(nav.nodeX(i) - self.pos.x, nav.nodeZ(i) - self.pos.z);
            if (walk > 10.0) {
                continue;
            }
            double cost = walk + nav.pathLength(i, to);
            if (cost < bestCost && walkable(self, nav.nodeX(i), nav.nodeZ(i))) {
                bestCost = cost;
                best = i;
            }
        }
        int previous = context.entryNode;
        if (best >= 0 && previous >= 0 && previous != best && context.entryTarget == to) {
            // Two ways round an obstacle often cost about the same: stick to the one already taken.
            double walk = Trig.hypot(nav.nodeX(previous) - self.pos.x, nav.nodeZ(previous) - self.pos.z);
            if (walk <= 10.0 && walk + nav.pathLength(previous, to) <= bestCost + ROUTE_HYSTERESIS
                    && walkable(self, nav.nodeX(previous), nav.nodeZ(previous))) {
                best = previous;
            }
        }
        if (best < 0) {
            best = nav.nearestNode(self.pos.x, self.pos.z);
        }
        context.entryNode = best;
        context.entryTarget = to;
        return best;
    }

    /** Nearest path node from which (x, z) can be walked to in a straight line, or the nearest one at all. */
    private int reachableNodeNear(double x, double z, Combatant self) {
        NavGraph nav = context.sim().map().nav();
        World world = context.sim().world();
        double y = self.pos.y + WALK_PROBE_HEIGHT;
        double radius = self.hitRadius() + 0.05;
        int best = -1;
        double bestDistance = 12.0 * 12.0;
        for (int i = 0; i < nav.size(); i++) {
            double dx = nav.nodeX(i) - x;
            double dz = nav.nodeZ(i) - z;
            double d = dx * dx + dz * dz;
            if (d < bestDistance && world.hasClearShot(nav.nodeX(i), y, nav.nodeZ(i), x, y, z, radius)) {
                bestDistance = d;
                best = i;
            }
        }
        return best >= 0 ? best : nav.nearestNode(x, z);
    }

    /**
     * A hurt bot fighting in the open (no cover to run to) throws an ice wall at the floor a few metres in front of it,
     * towards its target, and fights from behind it.
     */
    private void planWall(int tick) {
        Combatant self = context.self();
        Combatant target = context.target;
        if (tick < context.nextWallTick || context.intent != BotContext.Intent.ATTACK || target == null
                || context.inCover || context.coverIndex >= 0 || tick < context.wallUntilTick) {
            return;
        }
        boolean hurt = self.vitals.totalFraction() < profile.retreatHealth() + 0.15
                || tick - context.lastHurtTick < 60 && self.vitals.totalFraction() < 0.8;
        double toX = target.pos.x - self.pos.x;
        double toZ = target.pos.z - self.pos.z;
        double distance = Trig.hypot(toX, toZ);
        if (!hurt || distance < WALL_MIN_RANGE || distance > PREFERRED_MAX_RANGE) {
            return;
        }
        int slot = scorer.wallSlot(context);
        if (slot < 0) {
            return;
        }
        context.wallSlot = slot;
        context.wallX = self.pos.x + toX / distance * WALL_AHEAD;
        context.wallZ = self.pos.z + toZ / distance * WALL_AHEAD;
        context.wallUntilTick = tick + 45;
        context.wallShots = self.shotsFired;
        context.nextWallTick = tick + WALL_COOLDOWN_TICKS;
    }

    /** A bot standing in an enemy's wall of fire steps out of it, away from the patch burning it. */
    private void avoidFire() {
        Combatant self = context.self();
        Hazard burning = context.sim().hazards().touching(self);
        if (burning == null) {
            return;
        }
        double awayX = self.pos.x - burning.x;
        double awayZ = self.pos.z - burning.z;
        double length = Trig.hypot(awayX, awayZ);
        if (length < 1e-3) {
            awayX = -Trig.sin(self.yaw);
            awayZ = -Trig.cos(self.yaw);
            length = 1;
        }
        context.waypointX = self.pos.x + awayX / length * 2.5;
        context.waypointZ = self.pos.z + awayZ / length * 2.5;
        context.hasWaypoint = true;
        context.moveSpeedFactor = 1.0;
        context.inCover = false;
        context.wantCrouch = false;
    }

    /**
     * Blinded: the bot sees nobody, so it neither aims nor fires; it staggers about slowly in short random
     * directions until its sight comes back, the way a dazzled player would.
     */
    private void stumble(int tick) {
        Combatant self = context.self();
        context.intent = BotContext.Intent.NONE;
        context.inCover = false;
        context.wantCrouch = false;
        context.coverIndex = -1;
        if (tick >= context.stumbleUntilTick || !context.hasWaypoint) {
            var random = context.sim().arena().random();
            double angle = random.nextDouble() * Math.PI * 2;
            context.waypointX = self.pos.x + Trig.sin(angle) * 2.5;
            context.waypointZ = self.pos.z + Trig.cos(angle) * 2.5;
            context.stumbleUntilTick = tick + 24 + random.nextInt(30);
        }
        context.hasWaypoint = true;
        context.moveSpeedFactor = 0.35;
    }

    /**
     * Notices blinding slugs about to go off within sight (stuck ones, or flying ones close to the end of their
     * flight) and decides once per slug whether to turn its eyes away until it has popped. A bot knows where its own
     * slugs are; somebody else's it only notices in front of it, and the worse the bot the likelier it stares.
     */
    private void avoidFlashes(int tick) {
        Combatant self = context.self();
        if (tick < context.lookAwayUntilTick) {
            return;
        }
        ProjectilePool pool = context.sim().projectiles();
        World world = context.sim().world();
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (!p.active || p.id <= context.flashJudgedId) {
                continue;
            }
            double reach = p.slug.onExpire().flashRadius();
            if (reach <= 0 || !p.stuck && p.ticksLeft > FLASH_WARNING_TICKS) {
                continue;
            }
            double rx = p.pos.x - context.eye.x;
            double rz = p.pos.z - context.eye.z;
            double flat = Trig.hypot(rx, rz);
            double beamCos = p.slug.onExpire().flashBeamCos();
            if (p.stuck && beamCos > -1 && flat > 1.5
                    && -(rx * p.stuckDirX + rz * p.stuckDirZ) / flat < beamCos) {
                continue;   // behind it: its light shines the other way
            }
            boolean own = p.owner == self;
            boolean behind = flat > 1e-6
                    && (rx * Trig.sin(self.yaw) + rz * Trig.cos(self.yaw)) / flat < DODGE_VIEW_COS;
            Vec3 eye = context.eye;
            if (flat > reach || !own && behind
                    || !world.hasLineOfSight(eye.x, eye.y, eye.z, p.pos.x, p.pos.y, p.pos.z)) {
                continue;
            }
            context.flashJudgedId = p.id;
            double chance = own ? 0.5 + 0.5 * profile.dodgeChance() : profile.dodgeChance();
            if (context.sim().arena().random().nextDouble() < chance) {
                context.lookAwayUntilTick = tick + p.ticksLeft + 4;
                context.lookAwayX = p.pos.x;
                context.lookAwayZ = p.pos.z;
                return;
            }
        }
    }

    /**
     * Notices enemy grenades stuck within their blast radius (each one is judged once, so slow bots sometimes miss
     * one) and overrides the waypoint to run out of the blast until the danger is gone.
     */
    private void avoidGrenades(int tick) {
        Combatant self = context.self();
        ProjectilePool pool = context.sim().projectiles();
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (!p.active || !p.stuck || p.stuckTo == self || p.owner.team == self.team) {
                continue;
            }
            double reach = p.slug.onExpire().areaRadius() + self.hitRadius() + 1.0;
            double awayX = self.pos.x - p.pos.x;
            double awayZ = self.pos.z - p.pos.z;
            double distance = Trig.hypot(awayX, awayZ);
            if (distance > reach) {
                continue;
            }
            // Projectile ids only grow, so remembering the newest one judged means each grenade is judged once.
            if (p.id > context.grenadeJudgedId) {
                context.grenadeJudgedId = p.id;
                if (context.sim().arena().random().nextDouble() < profile.dodgeChance()) {
                    context.fleeUntilTick = tick + p.ticksLeft + 6;
                }
            }
            if (tick >= context.fleeUntilTick) {
                continue;
            }
            if (distance < 1e-3) {
                awayX = -Trig.sin(self.yaw);
                awayZ = -Trig.cos(self.yaw);
                distance = 1.0;
            }
            context.waypointX = self.pos.x + awayX / distance * (reach + 1.0 - distance);
            context.waypointZ = self.pos.z + awayZ / distance * (reach + 1.0 - distance);
            context.hasWaypoint = true;
            context.moveSpeedFactor = 1.0;
            context.inCover = false;
            context.wantCrouch = false;
            return;
        }
    }

    /** Fills visibility, the chosen target and the ally that needs help. */
    private void perceive(int tick) {
        Combatant self = context.self();
        World world = context.sim().world();
        context.eye.set(self.pos.x, self.pos.y + self.hitHeight() * EYE_FRACTION, self.pos.z);
        if (self.damageTaken > context.damageTakenAtLastPlan + 0.5) {
            context.lastHurtTick = tick;
        }
        Combatant ally = null;
        double lowest = 0.6;
        boolean blinded = self.stats.blindness > BLIND_SIGHT;
        for (Combatant other : context.sim().combatants()) {
            boolean seen = !blinded && other != self && other.alive && world.hasLineOfSight(context.eye.x,
                    context.eye.y, context.eye.z, other.pos.x, other.pos.y + other.hitHeight() * CHEST_FRACTION,
                    other.pos.z);
            if (seen && !context.visible[other.id]) {
                context.spottedTick[other.id] = tick;
            }
            context.visible[other.id] = seen;
            if (seen && other.team == self.team) {
                double fraction = other.vitals.totalFraction();
                if (fraction < lowest) {
                    lowest = fraction;
                    ally = other;
                }
            }
        }
        context.ally = ally;
        Combatant previous = context.target;
        context.target = profile.selector().select(context);
        context.targetHidden = false;
        if (context.target != null) {
            context.lastSeenTick = tick;
            context.lastSeenX = context.target.pos.x;
            context.lastSeenZ = context.target.pos.z;
        } else if (previous != null && previous.alive && !blinded
                && tick - context.lastSeenTick <= TARGET_GRACE_TICKS) {
            // Lost from sight an instant ago (a corner, a step): still the target, but nothing to shoot at.
            context.target = previous;
            context.targetHidden = true;
        }
    }

    /**
     * Looks for an enemy slug that will pass close to the bot and schedules a roll away from it. Like a player, a bot
     * only reacts to slugs it can see coming: in front of it, within {@link #DODGE_VIEW_COS} of where it faces.
     */
    private void detectThreat(int tick) {
        Combatant self = context.self();
        if (context.dodgeTicksLeft > 0 || context.rollSlot < 0 || tick < self.moves.cooldownUntil(context.rollSlot)) {
            return;
        }
        ProjectilePool pool = context.sim().projectiles();
        double centerY = self.centerY();
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (!p.active || p.stuck || p.owner.team == self.team) {
                continue;
            }
            TargetingRule rule = p.slug.targeting();
            if (rule == TargetingRule.ALLIES || rule == TargetingRule.ALLIES_AND_SELF) {
                continue;
            }
            double rx = p.pos.x - self.pos.x;
            double ry = p.pos.y - centerY;
            double rz = p.pos.z - self.pos.z;
            double flat = Trig.hypot(rx, rz);
            if (flat > 1e-6 && (rx * Trig.sin(self.yaw) + rz * Trig.cos(self.yaw)) / flat < DODGE_VIEW_COS) {
                continue;
            }
            double speedSq = p.vel.lengthSq();
            double closestTime = -(rx * p.vel.x + ry * p.vel.y + rz * p.vel.z) / speedSq;
            if (closestTime <= 0 || closestTime > 0.7) {
                continue;
            }
            double cx = rx + p.vel.x * closestTime;
            double cy = ry + p.vel.y * closestTime;
            double cz = rz + p.vel.z * closestTime;
            double miss = cx * cx + cy * cy + cz * cz;
            double danger = self.hitRadius() + p.slug.projectile().radius() + 0.4;
            if (miss < danger * danger
                    && context.sim().arena().random().nextDouble() < profile.dodgeChance()) {
                double length = Trig.hypot(p.vel.x, p.vel.z);
                if (length > 1e-6) {
                    double sign = context.sim().arena().random().nextBoolean() ? 1 : -1;
                    context.dodgeX = -p.vel.z / length * sign;
                    context.dodgeZ = p.vel.x / length * sign;
                    context.dodgeTicksLeft = 3;
                }
                return;
            }
        }
    }

    /**
     * Every half second checks whether the bot made progress. If not, it gets unstuck: it walks for a moment along
     * the freest direction near the one it wanted (jumping only when what blocks it is low enough to jump onto), and
     * after {@link #GIVE_UP_AFTER} failures in a row it heads somewhere else for a few seconds.
     */
    private void updateStuck(int tick) {
        Combatant self = context.self();
        if (tick - context.lastCheckTick < 30) {
            return;
        }
        double moved = Trig.hypot(self.pos.x - context.lastCheckX, self.pos.z - context.lastCheckZ);
        boolean held = self.stats.rooted || self.stats.stunned || !self.grounded;
        boolean wanted = context.hasRoute && context.moveSpeedFactor > 0.1 && !context.inCover
                && context.intent != BotContext.Intent.HEAL
                && Trig.hypot(context.routeX - self.pos.x, context.routeZ - self.pos.z) > 1.0;
        // Going somewhere fixed, what counts is getting closer to it: a bot swinging back and forth moves, yet
        // gets nowhere. In a fight the spot to stand on moves with the bot, so there plain movement counts.
        boolean detour = tick < context.detourUntilTick;
        double goalX = detour ? context.detourX : context.waypointX;
        double goalZ = detour ? context.detourZ : context.waypointZ;
        double goalDistance = Trig.hypot(goalX - self.pos.x, goalZ - self.pos.z);
        boolean sameGoal = Trig.hypot(goalX - context.lastGoalX, goalZ - context.lastGoalZ) < 1.0;
        boolean fixedGoal = context.intent == BotContext.Intent.NONE || detour;
        boolean progressed = fixedGoal && sameGoal
                ? context.lastGoalDistance - goalDistance >= 0.4 || goalDistance < 1.5
                : moved >= 0.4;
        if (wanted && !held && !progressed) {
            context.stuckCount++;
            escape(tick);
        } else if (progressed) {
            context.stuckCount = 0;
        }
        context.lastCheckTick = tick;
        context.lastCheckX = self.pos.x;
        context.lastCheckZ = self.pos.z;
        context.lastGoalX = goalX;
        context.lastGoalZ = goalZ;
        context.lastGoalDistance = goalDistance;
    }

    private void escape(int tick) {
        Combatant self = context.self();
        World world = context.sim().world();
        double wantX = context.routeX - self.pos.x;
        double wantZ = context.routeZ - self.pos.z;
        double length = Math.max(1e-6, Trig.hypot(wantX, wantZ));
        wantX /= length;
        wantZ /= length;
        double feet = self.pos.y;
        double radius = self.hitRadius() + 0.05;
        double probe = 1.6;
        // Something low right in front: jump onto it rather than walk round.
        boolean blockedLow = !world.hasClearShot(self.pos.x, feet + WALK_PROBE_HEIGHT, self.pos.z,
                self.pos.x + wantX * probe, feet + WALK_PROBE_HEIGHT, self.pos.z + wantZ * probe, radius);
        boolean clearHigh = world.hasClearShot(self.pos.x, feet + JUMPABLE + 0.3, self.pos.z,
                self.pos.x + wantX * probe, feet + JUMPABLE + 0.3, self.pos.z + wantZ * probe, radius);
        var random = context.sim().arena().random();
        if (blockedLow && clearHigh && context.jumpSlot >= 0 && context.stuckCount <= 1) {
            context.escapeX = wantX;
            context.escapeZ = wantZ;
            context.escapeJump = true;
            context.escapeUntilTick = tick + 14;
            return;
        }
        double base = Trig.atan2(wantX, wantZ);
        double bestScore = -Double.MAX_VALUE;
        double bestX = -wantX;
        double bestZ = -wantZ;
        for (int i = 1; i < 8; i++) {
            double angle = base + i * Math.PI / 4;
            double dx = Trig.sin(angle);
            double dz = Trig.cos(angle);
            if (!world.hasClearShot(self.pos.x, feet + WALK_PROBE_HEIGHT, self.pos.z, self.pos.x + dx * probe * 1.4,
                    feet + WALK_PROBE_HEIGHT, self.pos.z + dz * probe * 1.4, radius)) {
                continue;
            }
            double score = dx * wantX + dz * wantZ + random.nextDouble() * 0.4;
            if (score > bestScore) {
                bestScore = score;
                bestX = dx;
                bestZ = dz;
            }
        }
        context.escapeX = bestX;
        context.escapeZ = bestZ;
        context.escapeJump = false;
        context.escapeUntilTick = tick + 20 + 10 * context.stuckCount;
        if (context.stuckCount >= GIVE_UP_AFTER) {
            // That spot cannot be reached from here: go somewhere else for a while.
            NavGraph nav = context.sim().map().nav();
            if (nav.size() > 0) {
                int node = random.nextInt(nav.size());
                context.detourX = nav.nodeX(node);
                context.detourZ = nav.nodeZ(node);
                context.detourUntilTick = tick + 150;
            }
            context.coverIndex = -1;
            context.stuckCount = 0;
            if (context.intent == BotContext.Intent.NONE) {
                // A roaming goal it cannot reach: pick another one next time.
                context.hasWaypoint = false;
            }
        }
    }

    private Status chooseAttackSlug(BotContext ctx) {
        int chosen = scorer.bestAttackSlot(ctx, ctx.target);
        if (chosen < 0) {
            return Status.FAILURE;
        }
        ctx.slot = chosen;
        return Status.SUCCESS;
    }

    private Status chooseSupportSlug(BotContext ctx) {
        int chosen = scorer.bestSupportSlot(ctx, ctx.ally);
        if (chosen < 0) {
            return Status.FAILURE;
        }
        ctx.slot = chosen;
        return Status.SUCCESS;
    }

    private Status beginHeal(BotContext ctx) {
        ctx.intent = BotContext.Intent.HEAL;
        ctx.wantFire = true;
        ctx.hasWaypoint = false;
        ctx.moveSpeedFactor = 0.0;
        ctx.inCover = false;
        return Status.SUCCESS;
    }

    private Status beginAttack(BotContext ctx) {
        ctx.intent = BotContext.Intent.ATTACK;
        ctx.wantFire = !ctx.targetHidden;
        planPositioning(ctx);
        return Status.SUCCESS;
    }

    /** Decides where to stand while fighting: take cover when hurt, otherwise close in, back off or strafe. */
    private void planPositioning(BotContext ctx) {
        Combatant self = ctx.self();
        Combatant target = ctx.target;
        int tick = ctx.sim().tickCount();
        double distance = Math.sqrt(target.pos.horizontalDistanceSq(self.pos));
        boolean hurt = self.vitals.totalFraction() < profile.retreatHealth();
        boolean recentlyHit = tick - ctx.lastHurtTick < 60 && self.vitals.totalFraction() < 0.7;
        ctx.inCover = false;
        ctx.wantCrouch = false;
        if (hurt || recentlyHit) {
            if (ctx.coverIndex < 0 || !coverStillHidden(ctx, ctx.coverIndex)) {
                ctx.coverIndex = findCover(ctx, target.pos.x, target.pos.z);
            }
            if (ctx.coverIndex >= 0) {
                GameMap map = ctx.sim().map();
                ctx.waypointX = map.coverX(ctx.coverIndex);
                ctx.waypointZ = map.coverZ(ctx.coverIndex);
                ctx.hasWaypoint = true;
                ctx.moveSpeedFactor = 1.0;
                double toCover = Trig.hypot(ctx.waypointX - self.pos.x, ctx.waypointZ - self.pos.z);
                if (toCover < 0.9) {
                    ctx.inCover = true;
                    ctx.moveSpeedFactor = 0.0;
                    managePeek(ctx, tick, map.coverHeight(ctx.coverIndex) < LOW_COVER_HEIGHT);
                }
                return;
            }
        } else {
            ctx.coverIndex = -1;
        }
        if (distance > PREFERRED_MAX_RANGE) {
            ctx.waypointX = target.pos.x;
            ctx.waypointZ = target.pos.z;
            ctx.hasWaypoint = true;
            ctx.moveSpeedFactor = 1.0;
        } else if (distance < PREFERRED_MIN_RANGE) {
            double awayX = self.pos.x - target.pos.x;
            double awayZ = self.pos.z - target.pos.z;
            double length = Math.max(1e-6, Trig.hypot(awayX, awayZ));
            ctx.waypointX = self.pos.x + awayX / length * 5;
            ctx.waypointZ = self.pos.z + awayZ / length * 5;
            ctx.hasWaypoint = true;
            ctx.moveSpeedFactor = 1.0;
            keepInSight(ctx, target);
        } else {
            if (tick >= ctx.strafeUntilTick) {
                ctx.strafeSign = ctx.sim().arena().random().nextBoolean() ? 1 : -1;
                ctx.strafeUntilTick = tick + 45 + ctx.sim().arena().random().nextInt(45);
            }
            double toX = target.pos.x - self.pos.x;
            double toZ = target.pos.z - self.pos.z;
            double length = Math.max(1e-6, Trig.hypot(toX, toZ));
            ctx.waypointX = self.pos.x + (-toZ / length) * ctx.strafeSign * 4;
            ctx.waypointZ = self.pos.z + (toX / length) * ctx.strafeSign * 4;
            ctx.hasWaypoint = true;
            ctx.moveSpeedFactor = 0.8;
            if (!seesFrom(ctx, ctx.waypointX, ctx.waypointZ, target)) {
                // That side ducks out of sight round a corner: strafe the other way.
                ctx.strafeSign = -ctx.strafeSign;
                ctx.waypointX = self.pos.x + (-toZ / length) * ctx.strafeSign * 4;
                ctx.waypointZ = self.pos.z + (toX / length) * ctx.strafeSign * 4;
            }
            keepInSight(ctx, target);
        }
    }

    /**
     * A fighting bot does not step to where it would lose sight of its target (and then step back to find it again,
     * dancing at a corner): when the spot it picked is hidden from the target, it holds its ground and shoots.
     */
    private void keepInSight(BotContext ctx, Combatant target) {
        if (!seesFrom(ctx, ctx.waypointX, ctx.waypointZ, target)) {
            ctx.hasWaypoint = false;
            ctx.moveSpeedFactor = 0.0;
        }
    }

    /** True when a bot standing at (x, z), at its own eye height, would see the target's chest. */
    private boolean seesFrom(BotContext ctx, double x, double z, Combatant target) {
        Combatant self = ctx.self();
        double eyeY = self.pos.y + self.hitHeight() * EYE_FRACTION;
        return ctx.sim().world().hasLineOfSight(x, eyeY, z, target.pos.x,
                target.pos.y + target.hitHeight() * CHEST_FRACTION, target.pos.z);
    }

    /** Behind low cover the bot crouches, and stands up for short moments to shoot. */
    private void managePeek(BotContext ctx, int tick, boolean lowCover) {
        if (!lowCover) {
            ctx.wantCrouch = false;
            return;
        }
        if (tick >= ctx.peekUntilTick) {
            ctx.peeking = !ctx.peeking;
            ctx.peekUntilTick = tick + (ctx.peeking ? 30 : 36);
        }
        ctx.wantCrouch = !ctx.peeking;
        ctx.wantFire = ctx.peeking;
    }

    private boolean coverStillHidden(BotContext ctx, int index) {
        Combatant target = ctx.target;
        return target == null || isHiddenFrom(ctx, index, target.pos.x, target.pos.z);
    }

    private boolean isHiddenFrom(BotContext ctx, int index, double threatX, double threatZ) {
        GameMap map = ctx.sim().map();
        double bodyHeight = map.coverHeight(index) < LOW_COVER_HEIGHT ? 0.5 : 1.0;
        return !ctx.sim().world().hasLineOfSight(threatX, 1.4, threatZ, map.coverX(index), bodyHeight,
                map.coverZ(index));
    }

    /** Nearest cover point that hides the bot from the given threat position, or -1. */
    private int findCover(BotContext ctx, double threatX, double threatZ) {
        Combatant self = ctx.self();
        GameMap map = ctx.sim().map();
        int best = -1;
        double bestDistance = MAX_COVER_SEARCH * MAX_COVER_SEARCH;
        for (int i = 0; i < map.coverCount(); i++) {
            double dx = map.coverX(i) - self.pos.x;
            double dz = map.coverZ(i) - self.pos.z;
            double distance = dx * dx + dz * dz;
            if (distance < bestDistance && isHiddenFrom(ctx, i, threatX, threatZ)) {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    /** With nothing to chase, head for the enemy spawn area and keep roaming between waypoints. */
    private void chooseSearchGoal(BotContext ctx) {
        Combatant self = ctx.self();
        GameMap map = ctx.sim().map();
        double goalDistance = Trig.hypot(ctx.waypointX - self.pos.x, ctx.waypointZ - self.pos.z);
        if (ctx.hasWaypoint && goalDistance > 2.0 && !ctx.inCover) {
            return;
        }
        List<com.bajoterra.core.world.SpawnPoint> enemySpawns = map.spawns().get(1 - self.team);
        if (ctx.hasWaypoint && goalDistance <= 2.0 || enemySpawns.isEmpty()) {
            NavGraph nav = map.nav();
            int node = ctx.sim().arena().random().nextInt(nav.size());
            ctx.waypointX = nav.nodeX(node);
            ctx.waypointZ = nav.nodeZ(node);
        } else {
            com.bajoterra.core.world.SpawnPoint spawn =
                    enemySpawns.get(ctx.sim().arena().random().nextInt(enemySpawns.size()));
            ctx.waypointX = spawn.x();
            ctx.waypointZ = spawn.z();
        }
        ctx.inCover = false;
        ctx.hasWaypoint = true;
        ctx.moveSpeedFactor = 1.0;
    }

    // ------------------------------------------------------------------------------------------ execution

    private void execute(int tick) {
        Combatant self = context.self();
        InputState input = self.input;
        double dt = SimulationConstants.TICK_SECONDS;

        Combatant focus = switch (context.intent) {
            case ATTACK -> context.target;
            case HEAL -> context.ally;
            case NONE -> null;
        };
        if (focus != null && !focus.alive) {
            focus = null;
        }
        if (focus != null && (context.slot < 0 || !self.loadout.isAvailable(context.slot))) {
            context.slot = context.intent == BotContext.Intent.HEAL
                    ? scorer.bestSupportSlot(context, focus)
                    : scorer.bestAttackSlot(context, focus);
        }
        boolean aimed = false;
        boolean walling = tick < context.wallUntilTick && context.wallSlot >= 0
                && self.loadout.isAvailable(context.wallSlot) && self.shotsFired == context.wallShots;
        if (tick < context.lookAwayUntilTick) {
            // Eyes off a flash about to go off: face away from it, a little down, and hold fire.
            desiredYaw = Trig.atan2(self.pos.x - context.lookAwayX, self.pos.z - context.lookAwayZ);
            desiredPitch = -0.25;
        } else if (walling) {
            aimAtFloor(self, context.wallSlot, context.wallX, context.wallZ);
            aimed = true;
        } else if (focus != null && context.slot >= 0) {
            solveAim(self, focus);
            aimed = true;
        } else if (context.hasWaypoint && context.moveSpeedFactor > 0) {
            desiredYaw = Trig.atan2(context.waypointX - self.pos.x, context.waypointZ - self.pos.z);
            desiredPitch = 0;
        }

        double yawError = AimSolver.wrap(desiredYaw - self.yaw);
        double pitchError = desiredPitch - self.pitch;
        double maxStep = profile.turnRate() * dt;
        self.yaw += Math.max(-maxStep, Math.min(maxStep, yawError));
        self.pitch += Math.max(-maxStep, Math.min(maxStep, pitchError));
        double remainingYaw = Math.abs(AimSolver.wrap(desiredYaw - self.yaw));
        double remainingPitch = Math.abs(desiredPitch - self.pitch);
        double tolerance = 0.04 + profile.aimNoise();

        input.aimYaw = self.yaw;
        input.aimPitch = self.pitch;
        input.hasCamera = false;
        // A player needs a moment to react to an enemy that just came into view; so does a bot.
        boolean reacted = walling || focus == null || context.intent != BotContext.Intent.ATTACK
                || tick - context.spottedTick[focus.id] >= profile.reactionTicks();
        input.fire = aimed && reacted && (context.wantFire || walling) && remainingYaw < tolerance
                && remainingPitch < tolerance;
        // Full fusion energy goes into the next attack.
        input.fusion = input.fire && !walling && context.intent == BotContext.Intent.ATTACK && self.fusionReady();
        int wanted = walling ? context.wallSlot : context.slot;
        if (aimed && self.loadout.selected() != wanted && self.loadout.isAvailable(wanted)) {
            input.selectSlot = wanted;
            if (walling) {
                input.fire = false;
            }
        }

        input.moveMask = 0;
        input.moveX = 0;
        input.moveZ = 0;
        if (context.dodgeTicksLeft > 0) {
            context.dodgeTicksLeft--;
            setMoveFromWorldDirection(self, context.dodgeX, context.dodgeZ, 1.0);
            input.moveMask |= 1 << context.rollSlot;
        } else if (tick < context.escapeUntilTick) {
            setMoveFromWorldDirection(self, context.escapeX, context.escapeZ, 1.0);
            if (context.escapeJump && context.jumpSlot >= 0 && self.grounded) {
                input.moveMask |= 1 << context.jumpSlot;
            }
        } else if (context.hasWaypoint && context.moveSpeedFactor > 0) {
            steerToWaypoint(self, tick);
        }
        if (context.wantCrouch && context.crouchSlot >= 0 && self.grounded) {
            input.moveMask |= 1 << context.crouchSlot;
        }
        // In the air next to a wall while dodging or getting unstuck: kick off it, past the apex of the jump.
        boolean evading = context.dodgeTicksLeft > 0 || tick < context.escapeUntilTick;
        boolean wall = self.wallNormalX != 0 || self.wallNormalZ != 0;
        if (evading && wall && !self.grounded && self.vel.y < 2.0 && context.wallKickSlot >= 0 && !context.kickHeld) {
            input.moveMask |= 1 << context.wallKickSlot;
            context.kickHeld = true;
        } else {
            context.kickHeld = false;
        }
    }

    private void solveAim(Combatant self, Combatant focus) {
        ProjectileSpec spec = self.loadout.slug(context.slot).projectile();
        SlugDefinition slug = self.loadout.slug(context.slot);
        double muzzleY = self.pos.y + self.hitHeight() * 0.72;
        double muzzleX = self.pos.x + Trig.sin(self.yaw) * 0.55;
        double muzzleZ = self.pos.z + Trig.cos(self.yaw) * 0.55;
        double aimY = focus.pos.y + focus.hitHeight() * CHEST_FRACTION;
        AimSolver.solve(aim, muzzleX, muzzleY, muzzleZ, focus.pos.x, aimY, focus.pos.z,
                focus.vel.x + focus.knock.x, focus.vel.z + focus.knock.z,
                spec.speed(), spec.gravity(), profile.leadsTargets() && slug.projectile().speed() > 0);
        refreshAimNoise(context.sim().tickCount());
        desiredYaw = aim.yaw + context.noiseYaw;
        desiredPitch = aim.pitch + context.noisePitch;
    }

    /** Aims the slug in {@code slot} at a spot on the floor (where an ice wall should go up). */
    private void aimAtFloor(Combatant self, int slot, double x, double z) {
        ProjectileSpec spec = self.loadout.slug(slot).projectile();
        double muzzleY = self.pos.y + self.hitHeight() * 0.72;
        double muzzleX = self.pos.x + Trig.sin(self.yaw) * 0.55;
        double muzzleZ = self.pos.z + Trig.cos(self.yaw) * 0.55;
        AimSolver.solve(aim, muzzleX, muzzleY, muzzleZ, x, self.pos.y + 0.1, z, 0, 0, spec.speed(), spec.gravity(),
                false);
        desiredYaw = aim.yaw;
        desiredPitch = aim.pitch;
    }

    /**
     * Re-rolls the bot's aim error from the match's seeded random source every twelve ticks, so the error stays
     * constant for a moment (no jitter) but is independent between bots and between matches.
     */
    private void refreshAimNoise(int tick) {
        if (tick >= context.noiseUntilTick) {
            var random = context.sim().arena().random();
            context.noiseYaw = (random.nextDouble() * 2 - 1) * profile.aimNoise();
            context.noisePitch = (random.nextDouble() * 2 - 1) * profile.aimNoise();
            context.noiseUntilTick = tick + 12;
        }
    }

    private void steerToWaypoint(Combatant self, int tick) {
        if (!context.hasRoute) {
            planRoute(tick);
            if (!context.hasRoute) {
                return;
            }
        }
        boolean detour = tick < context.detourUntilTick;
        double goalX = detour ? context.detourX : context.waypointX;
        double goalZ = detour ? context.detourZ : context.waypointZ;
        double dx = context.routeX - self.pos.x;
        double dz = context.routeZ - self.pos.z;
        double length = Trig.hypot(dx, dz);
        boolean atGoal = Math.abs(context.routeX - goalX) < 1e-9 && Math.abs(context.routeZ - goalZ) < 1e-9;
        if (length < 0.8 && !atGoal) {
            // Reached a node of the path before the next decision: look further along it now.
            planRoute(tick);
            dx = context.routeX - self.pos.x;
            dz = context.routeZ - self.pos.z;
            length = Trig.hypot(dx, dz);
        }
        if (length < 0.35) {
            if (detour && atGoal) {
                context.detourUntilTick = 0;
            }
            return;
        }
        double dirX = dx / length;
        double dirZ = dz / length;
        Web web = noticedWebAhead(self, dirX, dirZ);
        if (web != null) {
            // Go round it, on the side away from its middle.
            double side = (web.x - self.pos.x) * dirZ - (web.z - self.pos.z) * dirX;
            double sign = side > 0 ? -1 : 1;
            double swap = dirX;
            dirX = -dirZ * sign * 0.8 + dirX * 0.2;
            dirZ = swap * sign * 0.8 + dirZ * 0.2;
        }
        setMoveFromWorldDirection(self, dirX, dirZ, context.moveSpeedFactor);
    }

    /**
     * An enemy web just ahead of the bot that it noticed. Each web is judged once: a good bot nearly always spots
     * one, a weak bot sometimes walks into it.
     */
    private Web noticedWebAhead(Combatant self, double dirX, double dirZ) {
        Web[] webs = context.sim().webs().webs();
        for (int i = 0; i < webs.length; i++) {
            Web w = webs[i];
            if (!w.active || w.owner == null || w.owner.team == self.team) {
                continue;
            }
            double reach = w.spec.radius() + self.hitRadius() + 1.4;
            double ax = self.pos.x + dirX * 1.2 - w.x;
            double az = self.pos.z + dirZ * 1.2 - w.z;
            if (ax * ax + az * az > reach * reach) {
                continue;
            }
            if (context.webJudged[i] != w.id) {
                context.webJudged[i] = w.id;
                context.webNoticed[i] = context.sim().arena().random().nextDouble()
                        < 0.55 + 0.45 * profile.dodgeChance();
            }
            if (context.webNoticed[i]) {
                return w;
            }
        }
        return null;
    }

    /** Converts a world direction into the strafe/forward axes relative to the bot's facing. */
    private void setMoveFromWorldDirection(Combatant self, double dirX, double dirZ, double factor) {
        double forwardX = Trig.sin(self.yaw);
        double forwardZ = Trig.cos(self.yaw);
        double rightX = Trig.cos(self.yaw);
        double rightZ = -Trig.sin(self.yaw);
        self.input.moveZ = (dirX * forwardX + dirZ * forwardZ) * factor;
        self.input.moveX = (dirX * rightX + dirZ * rightZ) * factor;
    }

    /** Finds the indices of the roll, crouch, jump and wall kick moves in this character's move set. */
    private void resolveMoveSlots(Combatant self) {
        context.rollSlot = -1;
        context.crouchSlot = -1;
        context.jumpSlot = -1;
        context.wallKickSlot = -1;
        List<MoveDefinition> moves = self.character.moves();
        for (int i = 0; i < moves.size(); i++) {
            switch (moves.get(i).id()) {
                case "roll" -> context.rollSlot = i;
                case "crouch" -> context.crouchSlot = i;
                case "jump" -> context.jumpSlot = i;
                case "wall_kick" -> context.wallKickSlot = i;
                default -> {
                    // other moves are not used by the AI
                }
            }
        }
    }

    /** The profile this bot plays with. */
    public BotProfile profile() {
        return profile;
    }

    /** The blackboard, exposed to tests to inspect the current decision. */
    BotContext context() {
        return context;
    }
}
