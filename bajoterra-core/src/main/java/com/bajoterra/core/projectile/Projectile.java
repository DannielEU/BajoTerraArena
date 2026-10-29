package com.bajoterra.core.projectile;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.SlugDefinition;

/** A slug in flight. Instances live in a {@link ProjectilePool} and are reused, never allocated during play. */
public final class Projectile {

    public boolean active;
    /** Unique, increasing id (used by snapshots to match projectiles between frames). */
    public int id;
    public Combatant owner;
    public SlugDefinition slug;
    public final Vec3 pos = new Vec3();
    public final Vec3 vel = new Vec3();
    /** Ticks of flight left, or of fuse once a sticky slug is stuck. */
    public int ticksLeft;
    public int pierceLeft;
    /** Bit i is set once the projectile has already hit combatant i (a piercing slug never hits twice). */
    public int hitMask;
    /** A sticky slug that has landed: it no longer flies and waits for its fuse to burn out. */
    public boolean stuck;
    /** The combatant a stuck slug clings to (it moves with them), or null when it is stuck to the world. */
    public Combatant stuckTo;
    /** Where a slug stuck to a combatant sits, relative to their feet. */
    public final Vec3 stuckOffset = new Vec3();
}
