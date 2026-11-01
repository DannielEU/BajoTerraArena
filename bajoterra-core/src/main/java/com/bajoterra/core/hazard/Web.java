package com.bajoterra.core.hazard;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.WebSpec;

/** A web trap on the floor (Arachnet). Instances live in a {@link WebSystem}'s fixed pool and are reused. */
public final class Web {

    public boolean active;
    /** Unique, increasing id (snapshots match webs between frames by it). */
    public int id;
    /** Who spun it; it only catches whoever that slug could hit. */
    public Combatant owner;
    public SlugDefinition slug;
    public WebSpec spec;
    /** Its middle, on the floor. */
    public double x;
    public double y;
    public double z;
    public int ticksLeft;
}
