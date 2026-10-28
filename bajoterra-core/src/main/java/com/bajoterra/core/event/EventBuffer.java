package com.bajoterra.core.event;

/**
 * Fixed-capacity ring of preallocated events. Producing an event never allocates; consumers (snapshot builder,
 * statistics, bot perception) read by sequence number and detect overwritten events through
 * {@link #oldestAvailable()}.
 */
public final class EventBuffer {

    private final GameEvent[] ring;
    private final int mask;
    private long written;

    /** @param capacity ring size; must be a power of two */
    public EventBuffer(int capacity) {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0) {
            throw new IllegalArgumentException("Capacity must be a power of two: " + capacity);
        }
        this.ring = new GameEvent[capacity];
        for (int i = 0; i < capacity; i++) {
            ring[i] = new GameEvent();
        }
        this.mask = capacity - 1;
    }

    /** Claims the next slot, resets it and returns it for the caller to fill in. */
    public GameEvent begin(EventType type, int tick) {
        GameEvent event = ring[(int) (written & mask)];
        event.reset(written, type, tick);
        written++;
        return event;
    }

    /** Total number of events produced so far (the next sequence number). */
    public long written() {
        return written;
    }

    /** Sequence number of the oldest event that has not been overwritten yet. */
    public long oldestAvailable() {
        return Math.max(0, written - ring.length);
    }

    /** Returns the event with the given sequence number, or null if it was overwritten or does not exist yet. */
    public GameEvent at(long sequence) {
        if (sequence < oldestAvailable() || sequence >= written) {
            return null;
        }
        return ring[(int) (sequence & mask)];
    }
}
