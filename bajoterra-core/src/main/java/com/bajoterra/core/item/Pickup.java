package com.bajoterra.core.item;

import com.bajoterra.core.math.Vec3;

/** A placed pickup in a running match. */
public final class Pickup {

    public final int index;
    public final PickupDefinition definition;
    public final Vec3 pos;
    public boolean available = true;
    /** Tick at which an unavailable pickup shows up again. */
    public int respawnTick;

    public Pickup(int index, PickupDefinition definition, Vec3 pos) {
        this.index = index;
        this.definition = definition;
        this.pos = pos;
    }
}
