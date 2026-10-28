package com.bajoterra.core.sim;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.content.Registry;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.SlugRole;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.SplittableRandom;

/** Generates sensible ten-slug loadouts for bots and for players who did not pick one. */
public final class LoadoutBuilder {

    private static final int SUPPORT_SLOTS = 2;
    private static final int CONTROL_SLOTS = 3;

    private LoadoutBuilder() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * Picks two support slugs, three control slugs and fills the rest with damage slugs, respecting the duplicate
     * limit and falling back to any slug when a role runs out. Deterministic for a given random source.
     */
    public static List<String> balanced(Registry<SlugDefinition> catalog, SplittableRandom random,
            int maxDuplicates) {
        Map<SlugRole, List<SlugDefinition>> byRole = new HashMap<>();
        for (SlugDefinition slug : catalog.all()) {
            byRole.computeIfAbsent(slug.role(), role -> new ArrayList<>()).add(slug);
        }
        Map<String, Integer> counts = new HashMap<>();
        List<String> picked = new ArrayList<>(Loadout.SIZE);
        int damageSlots = Loadout.SIZE - SUPPORT_SLOTS - CONTROL_SLOTS;
        pick(byRole.get(SlugRole.SUPPORT), SUPPORT_SLOTS, picked, counts, random, maxDuplicates);
        pick(byRole.get(SlugRole.CONTROL), CONTROL_SLOTS, picked, counts, random, maxDuplicates);
        pick(byRole.get(SlugRole.DAMAGE), damageSlots, picked, counts, random, maxDuplicates);
        // A role may have been too small; complete with anything that still fits.
        while (picked.size() < Loadout.SIZE) {
            if (!pickOne(catalog.all(), picked, counts, random, maxDuplicates)) {
                // Catalog too small for the duplicate limit: allow extra copies rather than failing.
                SlugDefinition any = catalog.get(random.nextInt(catalog.size()));
                picked.add(any.id());
            }
        }
        return picked;
    }

    private static void pick(List<SlugDefinition> pool, int amount, List<String> picked,
            Map<String, Integer> counts, SplittableRandom random, int maxDuplicates) {
        if (pool == null || pool.isEmpty()) {
            return;
        }
        for (int i = 0; i < amount; i++) {
            pickOne(pool, picked, counts, random, maxDuplicates);
        }
    }

    private static boolean pickOne(List<SlugDefinition> pool, List<String> picked, Map<String, Integer> counts,
            SplittableRandom random, int maxDuplicates) {
        List<SlugDefinition> candidates = new ArrayList<>();
        for (SlugDefinition slug : pool) {
            if (counts.getOrDefault(slug.id(), 0) < maxDuplicates) {
                candidates.add(slug);
            }
        }
        if (candidates.isEmpty()) {
            return false;
        }
        SlugDefinition chosen = candidates.get(random.nextInt(candidates.size()));
        counts.merge(chosen.id(), 1, Integer::sum);
        picked.add(chosen.id());
        return true;
    }
}
