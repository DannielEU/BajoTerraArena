package com.bajoterra.core.hazard;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;

/**
 * A patch of a slug's wall of fire burning on the floor. Instances live in a {@link HazardSystem}'s fixed pool and
 * are reused, never allocated during play.
 */
public final class Hazard {

    public boolean active;
    /** Unique, increasing id (snapshots match patches between frames by it). */
    public int id;
    /** Who fired the slug that left it; it only hurts whoever that slug could hit. */
    public Combatant owner;
    public SlugDefinition slug;
    /** Middle of the patch, on the floor. */
    public double x;
    public double y;
    public double z;
    public int ticksLeft;
}
