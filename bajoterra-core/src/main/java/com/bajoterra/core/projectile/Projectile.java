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
    /** Metres flown since the last patch of fire of its trail (negative before the first one). */
    public double trailGap;
    /** Metres of trail laid so far. */
    public double trailLaid;
    /** Collision radius: the slug's own, grown while its slinger is megamorphed. */
    public double radius;
    /** The second slug of a fusion shot, whose effects go off together with this one's; null for a plain shot. */
    public SlugDefinition partner;
    /** Id of the tornado that already threw this slug off course (0 = none: each slug is thrown once). */
    public int deflectedBy;
    /** Horizontal unit direction a stuck slug faces: the way it was flying, or towards whoever stopped it. */
    public double stuckDirX;
    public double stuckDirZ;
}
