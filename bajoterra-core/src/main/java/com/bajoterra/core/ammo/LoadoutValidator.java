package com.bajoterra.core.ammo;

import com.bajoterra.core.content.Registry;
import com.bajoterra.core.slug.SlugDefinition;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/** Validates the slugs a player picked before a match. */
public final class LoadoutValidator {

    /** Default limit of copies of the same slug in one loadout. */
    public static final int DEFAULT_MAX_DUPLICATES = 2;

    private LoadoutValidator() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * Checks size, known ids, duplicates and that the loadout cannot lock itself completely.
     *
     * @return the list of problems; empty when the loadout is valid
     */
    public static List<String> validate(
            List<String> slugIds, Registry<SlugDefinition> catalog, int reloadShots, int maxDuplicates) {
        List<String> problems = new ArrayList<>();
        if (slugIds.size() != Loadout.SIZE) {
            problems.add("A loadout needs exactly " + Loadout.SIZE + " slugs, got " + slugIds.size());
        }
        if (reloadShots >= Loadout.SIZE) {
            problems.add("reloadShots (" + reloadShots + ") must be smaller than the loadout size");
        }
        Map<String, Integer> counts = new HashMap<>();
        for (String id : slugIds) {
            if (!catalog.contains(id)) {
                problems.add("Unknown slug '" + id + "'");
                continue;
            }
            int count = counts.merge(id, 1, Integer::sum);
            if (count == maxDuplicates + 1) {
                problems.add("Slug '" + id + "' appears more than " + maxDuplicates + " times");
            }
        }
        return problems;
    }
}
