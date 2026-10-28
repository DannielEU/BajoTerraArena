package com.bajoterra.core.ai;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.entity.InputSource;
import com.bajoterra.core.entity.InputState;
import com.bajoterra.core.math.Trig;
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
 * movement and the trigger are updated every tick from those decisions. Nothing here allocates after construction.
 */
public final class BotBrain implements InputSource {

    private static final double EYE_FRACTION = 0.9;
    private static final double CHEST_FRACTION = 0.55;
    private static final double PREFERRED_MAX_RANGE = 24.0;
    private static final double PREFERRED_MIN_RANGE = 7.0;
    private static final double LOW_COVER_HEIGHT = 1.3;
    private static final double MAX_COVER_SEARCH = 26.0;

    private final BotProfile profile;
    private final BotContext context;
    private final BtNode tree;
    private final SlugUtilityScorer scorer = new SlugUtilityScorer();
    private final AimSolver.Aim aim = new AimSolver.Aim();
    private int nextPlanTick;
    private double desiredYaw;
    private double desiredPitch;

    public BotBrain(double skill) {
        this.profile = BotProfile.fromSkill(skill);
        this.context = new BotContext(profile);
        this.tree = buildTree();
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
        return new Selector(healAlly, attack, hunt, patrol);
    }

    private void plan(int tick) {
        Combatant self = context.self();
        context.intent = BotContext.Intent.NONE;
        context.wantFire = false;
        context.wantCrouch = false;
        perceive(tick);
        detectThreat(tick);
        updateStuck(tick);
        context.damageTakenAtLastPlan = self.damageTaken;
        if (tree.tick(context) == Status.FAILURE) {
            context.intent = BotContext.Intent.NONE;
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
        for (Combatant other : context.sim().combatants()) {
            boolean seen = other != self && other.alive && world.hasLineOfSight(context.eye.x, context.eye.y,
                    context.eye.z, other.pos.x, other.pos.y + other.hitHeight() * CHEST_FRACTION, other.pos.z);
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
        context.target = profile.selector().select(context);
        if (context.target != null) {
            context.lastSeenTick = tick;
            context.lastSeenX = context.target.pos.x;
            context.lastSeenZ = context.target.pos.z;
        }
    }

    /** Looks for an enemy slug that will pass close to the bot and schedules a roll away from it. */
    private void detectThreat(int tick) {
        Combatant self = context.self();
        if (context.dodgeTicksLeft > 0 || context.rollSlot < 0 || tick < self.moves.cooldownUntil(context.rollSlot)) {
            return;
        }
        ProjectilePool pool = context.sim().projectiles();
        double centerY = self.centerY();
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (!p.active || p.owner.team == self.team) {
                continue;
            }
            TargetingRule rule = p.slug.targeting();
            if (rule == TargetingRule.ALLIES || rule == TargetingRule.ALLIES_AND_SELF) {
                continue;
            }
            double rx = p.pos.x - self.pos.x;
            double ry = p.pos.y - centerY;
            double rz = p.pos.z - self.pos.z;
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

    /** Every half second checks whether the bot made progress; if not it sidesteps and jumps for a moment. */
    private void updateStuck(int tick) {
        Combatant self = context.self();
        if (tick - context.lastCheckTick < 30) {
            return;
        }
        double moved = Trig.hypot(self.pos.x - context.lastCheckX, self.pos.z - context.lastCheckZ);
        if (context.hasWaypoint && moved < 0.4 && context.moveSpeedFactor > 0.1 && !context.inCover
                && context.intent != BotContext.Intent.HEAL) {
            context.unstickUntilTick = tick + 24;
        }
        context.lastCheckTick = tick;
        context.lastCheckX = self.pos.x;
        context.lastCheckZ = self.pos.z;
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
        ctx.wantFire = true;
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
        }
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
        if (focus != null && context.slot >= 0) {
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
        input.fire = aimed && context.wantFire && remainingYaw < tolerance && remainingPitch < tolerance;
        if (aimed && self.loadout.selected() != context.slot && self.loadout.isAvailable(context.slot)) {
            input.selectSlot = context.slot;
        }

        input.moveMask = 0;
        input.moveX = 0;
        input.moveZ = 0;
        if (context.dodgeTicksLeft > 0) {
            context.dodgeTicksLeft--;
            setMoveFromWorldDirection(self, context.dodgeX, context.dodgeZ, 1.0);
            input.moveMask |= 1 << context.rollSlot;
        } else if (context.hasWaypoint && context.moveSpeedFactor > 0) {
            steerToWaypoint(self, tick);
        }
        if (context.wantCrouch && context.crouchSlot >= 0 && self.grounded) {
            input.moveMask |= 1 << context.crouchSlot;
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
        GameMap map = context.sim().map();
        NavGraph nav = map.nav();
        double goalX = context.waypointX;
        double goalZ = context.waypointZ;
        double nextX = goalX;
        double nextZ = goalZ;
        World world = context.sim().world();
        boolean direct = world.hasClearShot(self.pos.x, 0.5, self.pos.z, goalX, 0.5, goalZ, 0.0)
                && Trig.hypot(goalX - self.pos.x, goalZ - self.pos.z) < 8;
        if (!direct && nav.size() > 0) {
            int from = nav.nearestNode(self.pos.x, self.pos.z);
            int to = nav.nearestNode(goalX, goalZ);
            if (from >= 0 && to >= 0 && from != to) {
                int hop = nav.nextNode(from, to);
                if (hop >= 0) {
                    nextX = nav.nodeX(hop);
                    nextZ = nav.nodeZ(hop);
                }
            }
        }
        double dx = nextX - self.pos.x;
        double dz = nextZ - self.pos.z;
        double length = Trig.hypot(dx, dz);
        if (length < 0.35) {
            return;
        }
        double dirX = dx / length;
        double dirZ = dz / length;
        if (tick < context.unstickUntilTick) {
            double swap = dirX;
            dirX = -dirZ;
            dirZ = swap;
            if (context.jumpSlot >= 0 && self.grounded) {
                self.input.moveMask |= 1 << context.jumpSlot;
            }
        }
        setMoveFromWorldDirection(self, dirX, dirZ, context.moveSpeedFactor);
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

    /** Finds the indices of the roll, crouch and jump moves in this character's move set. */
    private void resolveMoveSlots(Combatant self) {
        context.rollSlot = -1;
        context.crouchSlot = -1;
        context.jumpSlot = -1;
        List<MoveDefinition> moves = self.character.moves();
        for (int i = 0; i < moves.size(); i++) {
            switch (moves.get(i).id()) {
                case "roll" -> context.rollSlot = i;
                case "crouch" -> context.crouchSlot = i;
                case "jump" -> context.jumpSlot = i;
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
