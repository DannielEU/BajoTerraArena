package com.bajoterra.core.effect;

/** Runtime state of one status effect on one combatant. Instances are preallocated and reused by {@link StatusSet}. */
public final class StatusInstance {

    private AbstractStatusEffect definition;
    private boolean active;
    private int remainingTicks;
    private int ageTicks;
    private int stacks;
    private int sourceId = -1;
    private double value;

    public boolean active() {
        return active;
    }

    public AbstractStatusEffect definition() {
        return definition;
    }

    public int remainingTicks() {
        return remainingTicks;
    }

    /** Ticks since the status was first applied (re-applying it does not restart the count). */
    public int ageTicks() {
        return ageTicks;
    }

    public int stacks() {
        return stacks;
    }

    /** Id of the combatant that applied the status, or -1. */
    public int sourceId() {
        return sourceId;
    }

    /** Free-form magnitude chosen by the effect when it was applied (damage per second, heal rate, ...). */
    public double value() {
        return value;
    }

    public void setValue(double newValue) {
        this.value = newValue;
    }

    void start(AbstractStatusEffect newDefinition, int duration, int source) {
        this.definition = newDefinition;
        this.active = true;
        this.remainingTicks = duration;
        this.ageTicks = 0;
        this.stacks = 1;
        this.sourceId = source;
        this.value = 0;
    }

    void refresh(AbstractStatusEffect newDefinition, int duration, int source) {
        this.definition = newDefinition;
        this.remainingTicks = duration;
        this.sourceId = source;
    }

    void addStack(int maxStacks) {
        if (stacks < maxStacks) {
            stacks++;
        }
    }

    int advance() {
        ageTicks++;
        remainingTicks--;
        return ageTicks;
    }

    void stop() {
        this.active = false;
        this.definition = null;
        this.remainingTicks = 0;
    }
}
