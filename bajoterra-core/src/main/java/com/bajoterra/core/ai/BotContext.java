package com.bajoterra.core.ai;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.hazard.WebSystem;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.sim.GameSimulation;

/**
 * The blackboard of one bot: what it currently perceives, what it intends to do and the data its behavior-tree
 * leaves share. It is bound to the simulation and combatant at the start of each think and reused afterwards.
 */
public final class BotContext {

    /** What the bot is trying to do. */
    enum Intent {
        NONE,
        ATTACK,
        HEAL
    }

    private static final int MAX_COMBATANTS = 32;

    final boolean[] visible = new boolean[MAX_COMBATANTS];
    /** Tick at which each combatant last came into view; the bot holds fire for its reaction time after it. */
    final int[] spottedTick = new int[MAX_COMBATANTS];
    final Vec3 eye = new Vec3();
    final Vec3 scratch = new Vec3();

    private GameSimulation sim;
    private Combatant self;

    Intent intent = Intent.NONE;
    Combatant target;
    /** The target went out of sight a moment ago: the bot still goes after it but has nothing to shoot at. */
    boolean targetHidden;
    Combatant ally;
    int slot = -1;
    boolean wantFire;
    boolean wantCrouch;

    int lastSeenTick = Integer.MIN_VALUE / 2;
    double lastSeenX;
    double lastSeenZ;

    double waypointX;
    double waypointZ;
    boolean hasWaypoint;
    double moveSpeedFactor = 1.0;
    int strafeSign = 1;
    int strafeUntilTick;

    int coverIndex = -1;
    boolean inCover;
    int peekUntilTick;
    boolean peeking;

    int dodgeTicksLeft;
    double dodgeX;
    double dodgeZ;

    /** Id of the newest stuck grenade the bot already decided about (it reacts to each grenade only once). */
    int grenadeJudgedId;
    int fleeUntilTick;

    /** Id of the newest blinding slug the bot already decided about (it reacts to each flash only once). */
    int flashJudgedId;
    /** Until this tick the bot keeps its eyes off a flash about to go off at (lookAwayX, lookAwayZ). */
    int lookAwayUntilTick;
    double lookAwayX;
    double lookAwayZ;
    /** While blinded the bot staggers about; it picks a new direction to stagger in at this tick. */
    int stumbleUntilTick;

    /** Raising an ice wall for cover: the slot, the spot on the floor to throw it at, until when it tries. */
    int wallSlot = -1;
    double wallX;
    double wallZ;
    int wallUntilTick;
    int wallShots;
    /** No new wall before this tick. */
    int nextWallTick;

    double noiseYaw;
    double noisePitch;
    int noiseUntilTick;

    /** Where the bot steers right now on its way to the waypoint (a waypoint in sight, or a node of the path). */
    double routeX;
    double routeZ;
    boolean hasRoute;
    /** The node the bot joined its path at last time, and the node that path led to (-1 = none). */
    int entryNode = -1;
    int entryTarget = -1;
    /** Times in a row the bot made no progress; a few in a row and it gives up on where it was going. */
    int stuckCount;
    /** Getting unstuck: until this tick it walks along (escapeX, escapeZ), jumping when escapeJump. */
    int escapeUntilTick;
    double escapeX;
    double escapeZ;
    boolean escapeJump;
    /** After giving up on a spot: until this tick it heads for (detourX, detourZ) instead. */
    int detourUntilTick;
    double detourX;
    double detourZ;
    /** Per web slot: the id of the web the bot already decided about, and whether it noticed it (and avoids it). */
    final int[] webJudged = new int[WebSystem.CAPACITY];
    final boolean[] webNoticed = new boolean[WebSystem.CAPACITY];

    /** The goal the bot was heading for at the last progress check, and how far it was. */
    double lastGoalX;
    double lastGoalZ;
    double lastGoalDistance;
    double lastCheckX;
    double lastCheckZ;
    int lastCheckTick;
    double damageTakenAtLastPlan;
    int lastHurtTick = Integer.MIN_VALUE / 2;

    int rollSlot = -2;
    int crouchSlot = -1;
    int jumpSlot = -1;
    int wallKickSlot = -1;
    /** The wall kick button was held last tick (a kick needs a fresh press). */
    boolean kickHeld;

    BotContext() {
        // filled by bind() and the planning cycle
    }

    void bind(GameSimulation newSim, Combatant newSelf) {
        this.sim = newSim;
        this.self = newSelf;
    }

    /** Forgets everything tied to the previous round. */
    void reset() {
        intent = Intent.NONE;
        target = null;
        targetHidden = false;
        ally = null;
        slot = -1;
        wantFire = false;
        wantCrouch = false;
        lastSeenTick = Integer.MIN_VALUE / 2;
        hasWaypoint = false;
        coverIndex = -1;
        inCover = false;
        dodgeTicksLeft = 0;
        grenadeJudgedId = 0;
        fleeUntilTick = 0;
        flashJudgedId = 0;
        lookAwayUntilTick = 0;
        stumbleUntilTick = 0;
        wallSlot = -1;
        wallUntilTick = 0;
        nextWallTick = 0;
        hasRoute = false;
        entryNode = -1;
        entryTarget = -1;
        stuckCount = 0;
        escapeUntilTick = 0;
        detourUntilTick = 0;
        java.util.Arrays.fill(webJudged, 0);
        java.util.Arrays.fill(webNoticed, false);
        damageTakenAtLastPlan = 0;
        lastHurtTick = Integer.MIN_VALUE / 2;
        java.util.Arrays.fill(visible, false);
    }

    public GameSimulation sim() {
        return sim;
    }

    public Combatant self() {
        return self;
    }

    /** True when {@code other} is an alive enemy the bot currently has a line of sight to. */
    public boolean isVisibleEnemy(Combatant other) {
        return other.team != self.team && other.alive && visible[other.id];
    }
}
