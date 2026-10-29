package com.bajoterra.core.ai;

import com.bajoterra.core.entity.Combatant;
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

    double noiseYaw;
    double noisePitch;
    int noiseUntilTick;

    int unstickUntilTick;
    double lastCheckX;
    double lastCheckZ;
    int lastCheckTick;
    double damageTakenAtLastPlan;
    int lastHurtTick = Integer.MIN_VALUE / 2;

    int rollSlot = -2;
    int crouchSlot = -1;
    int jumpSlot = -1;

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
        unstickUntilTick = 0;
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
