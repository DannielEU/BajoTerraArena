package com.bajoterra.core.slug;

/** Tactical role of a slug; used by loadout generation and the bots' slug choice. */
public enum SlugRole {
    /** Deals direct or area damage. */
    DAMAGE,
    /** Slows, stuns, pushes or otherwise controls enemies. */
    CONTROL,
    /** Heals, restores shield or protects allies. */
    SUPPORT
}
