package com.bajoterra.core.world;

/** Placement of a pickup in the map; {@code pickupId} names a pickup definition. */
public record PickupSpawn(String pickupId, double x, double y, double z) {
}
