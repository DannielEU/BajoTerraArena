package com.bajoterra.core.slug;

import java.util.Locale;

/**
 * Elemental affinity of a slug or a character. The interaction matrix lives in data (elements.json);
 * the enum only provides the stable dense index used for table lookups.
 */
public enum Element {
    NEUTRAL,
    FIRE,
    WATER,
    EARTH,
    AIR,
    ENERGY,
    ICE,
    METAL,
    LIGHT;

    /** Number of elements; size of every per-element table. */
    public static final int COUNT = values().length;

    /** Parses a case-insensitive element name. */
    public static Element parse(String name) {
        return valueOf(name.trim().toUpperCase(Locale.ROOT));
    }
}
