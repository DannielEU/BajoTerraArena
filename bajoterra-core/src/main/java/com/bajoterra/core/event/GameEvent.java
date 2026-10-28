package com.bajoterra.core.event;

/** A mutable, reusable event record stored in the {@link EventBuffer} ring; the meaning of the fields depends on
 * the {@link EventType}. */
public final class GameEvent {

    public long sequence;
    public EventType type = EventType.SHOT;
    public int tick;
    public int actor = -1;
    public int subject = -1;
    public int a;
    public int b;
    public double x;
    public double y;
    public double z;
    public double value;

    void reset(long newSequence, EventType newType, int newTick) {
        this.sequence = newSequence;
        this.type = newType;
        this.tick = newTick;
        this.actor = -1;
        this.subject = -1;
        this.a = 0;
        this.b = 0;
        this.x = 0;
        this.y = 0;
        this.z = 0;
        this.value = 0;
    }
}
