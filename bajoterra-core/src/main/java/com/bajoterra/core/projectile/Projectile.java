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
    public int ticksLeft;
    public int pierceLeft;
    /** Bit i is set once the projectile has already hit combatant i (a piercing slug never hits twice). */
    public int hitMask;
}
