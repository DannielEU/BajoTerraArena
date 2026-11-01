package com.bajoterra.core.move;

import java.util.Arrays;

/** Per-combatant runtime state of the move system: the active move, its elapsed ticks and per-move cooldowns. */
public final class MoveRunner {

    private final int[] cooldownUntil = new int[32];
    private MoveDefinition active;
    private int activeSlot = -1;
    private int elapsed;
    private double dirX;
    private double dirZ = 1;
    private boolean driven;
    private double drivenX;
    private double drivenZ;
    private int lastMask;

    public MoveDefinition active() {
        return active;
    }

    /** Index of the active move inside the character's move set, or -1. */
    public int activeSlot() {
        return activeSlot;
    }

    /** Ticks since the active move started. */
    public int elapsed() {
        return elapsed;
    }

    /** Horizontal unit direction captured when the active move started. */
    public double dirX() {
        return dirX;
    }

    public double dirZ() {
        return dirZ;
    }

    /** Tick at which the move in the given slot may start again. */
    public int cooldownUntil(int slot) {
        return cooldownUntil[slot];
    }

    /** The move buttons held on the previous tick (a move that needs a fresh press checks it). */
    public int lastMask() {
        return lastMask;
    }

    void rememberMask(int mask) {
        lastMask = mask;
    }

    /** Lets a behavior override the horizontal velocity for the current tick (input steering is ignored). */
    public void driveHorizontal(double vx, double vz) {
        this.driven = true;
        this.drivenX = vx;
        this.drivenZ = vz;
    }

    /** True when a behavior drives the horizontal movement this tick. */
    public boolean isDriven() {
        return driven;
    }

    public double drivenX() {
        return drivenX;
    }

    public double drivenZ() {
        return drivenZ;
    }

    void begin(MoveDefinition move, int slot, int startTick, double directionX, double directionZ) {
        this.active = move;
        this.activeSlot = slot;
        this.elapsed = 0;
        this.dirX = directionX;
        this.dirZ = directionZ;
        this.cooldownUntil[slot] = startTick + Math.max(1, move.cooldownTicks());
    }

    void finish() {
        this.active = null;
        this.activeSlot = -1;
        this.elapsed = 0;
    }

    void advance() {
        elapsed++;
    }

    /** Called once per tick before behaviors run so the driven flag only lasts one tick. */
    void clearDrive() {
        driven = false;
    }

    /** Clears everything, including cooldowns (new round). */
    public void reset() {
        finish();
        driven = false;
        lastMask = 0;
        Arrays.fill(cooldownUntil, 0);
    }
}
