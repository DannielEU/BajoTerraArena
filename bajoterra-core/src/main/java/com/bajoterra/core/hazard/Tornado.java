package com.bajoterra.core.hazard;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TornadoSpec;

/**
 * A tornado spinning in the arena (Tormato). Instances live in a {@link TornadoSystem}'s fixed pool and are reused,
 * never allocated during play.
 */
public final class Tornado {

    public boolean active;
    /** Unique, increasing id (snapshots match tornadoes between frames by it). */
    public int id;
    /** Who fired the slug; it only drags whoever that slug could hit, and launches its owner's team. */
    public Combatant owner;
    public SlugDefinition slug;
    public TornadoSpec spec;
    /** Its foot, on the floor. */
    public double x;
    public double y;
    public double z;
    /** Horizontal unit direction it drifts in (zero once something stopped it). */
    public double dirX;
    public double dirZ;
    public int ticksLeft;
    /** It swept over a wall of fire and burns whoever it catches. */
    public boolean flaming;
    /** An ice slug was fired into it: it freezes whoever it catches (fire melts it back into a fire tornado). */
    public boolean freezing;
    /** Bit i is set once combatant i has been launched by it (a tornado jump happens once per tornado). */
    public int launchedMask;
}
