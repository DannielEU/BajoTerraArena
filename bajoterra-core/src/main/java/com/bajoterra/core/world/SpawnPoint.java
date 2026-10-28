package com.bajoterra.core.world;

/** A place where a combatant starts a round, facing {@code yaw} radians (0 = +Z). */
public record SpawnPoint(double x, double y, double z, double yaw) {
}
