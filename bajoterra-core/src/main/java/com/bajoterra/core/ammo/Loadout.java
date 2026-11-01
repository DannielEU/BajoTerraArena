package com.bajoterra.core.ammo;

import com.bajoterra.core.slug.SlugDefinition;
import java.util.Arrays;
import java.util.List;

/**
 * The five slugs a combatant carries and their shot-based lock.
 *
 * <p>Firing the slug in a slot as shot number {@code n} locks it for the following {@code reloadShots} shots: with
 * a reload of 3, shots n+1, n+2 and n+3 must use other slugs and the slug is available again for shot n+4.
 * Slugs are recycled, never consumed, so the loadout always holds exactly {@link #SIZE} slugs.
 */
public final class Loadout {

    /** Number of slugs every combatant carries. */
    public static final int SIZE = 5;

    private static final int NEVER_FIRED = Integer.MIN_VALUE / 2;

    private final SlugDefinition[] slugs;
    private final int[] firedAtShot = new int[SIZE];
    private final int defaultReloadShots;
    private int shotCounter;
    private int selected;

    /**
     * @param slugs exactly {@link #SIZE} slug definitions (duplicates allowed, see {@link LoadoutValidator})
     * @param defaultReloadShots lock length used by slugs without their own override
     */
    public Loadout(List<SlugDefinition> slugs, int defaultReloadShots) {
        if (slugs.size() != SIZE) {
            throw new IllegalArgumentException("A loadout needs exactly " + SIZE + " slugs, got " + slugs.size());
        }
        if (defaultReloadShots < 0 || defaultReloadShots >= SIZE) {
            throw new IllegalArgumentException("reloadShots must be in [0, " + (SIZE - 1) + "]");
        }
        this.slugs = slugs.toArray(new SlugDefinition[0]);
        this.defaultReloadShots = defaultReloadShots;
        reset();
    }

    /** Unlocks every slug and selects the first one (start of a round). */
    public void reset() {
        Arrays.fill(firedAtShot, NEVER_FIRED);
        shotCounter = 0;
        selected = 0;
    }

    public SlugDefinition slug(int slot) {
        return slugs[slot];
    }

    /** Total shots fired from this loadout since the last reset. */
    public int shotCounter() {
        return shotCounter;
    }

    public int selected() {
        return selected;
    }

    /** Lock length in shots of the slug in the given slot. */
    public int reloadShotsOf(int slot) {
        return slugs[slot].reloadShots(defaultReloadShots);
    }

    public boolean isAvailable(int slot) {
        return shotCounter - firedAtShot[slot] >= reloadShotsOf(slot);
    }

    /** Shots that must still be fired before the slot can be used again (0 when available). */
    public int shotsUntilReady(int slot) {
        return Math.max(0, reloadShotsOf(slot) - (shotCounter - firedAtShot[slot]));
    }

    public boolean hasAvailable() {
        for (int i = 0; i < SIZE; i++) {
            if (isAvailable(i)) {
                return true;
            }
        }
        return false;
    }

    /** Selects a slot if it is available. Returns whether the selection changed or was already active. */
    public boolean select(int slot) {
        if (slot < 0 || slot >= SIZE || !isAvailable(slot)) {
            return false;
        }
        selected = slot;
        return true;
    }

    /**
     * Moves the selection to the next ({@code direction > 0}) or previous available slot, skipping locked ones.
     * Returns false and keeps the selection when no other slot is available.
     */
    public boolean cycle(int direction) {
        if (direction == 0) {
            return false;
        }
        int step = direction > 0 ? 1 : -1;
        for (int i = 1; i < SIZE; i++) {
            int candidate = Math.floorMod(selected + step * i, SIZE);
            if (isAvailable(candidate)) {
                selected = candidate;
                return true;
            }
        }
        return false;
    }

    /**
     * The slot a fusion shot pairs with the selected one: the next available slot after it holding a different slug
     * of the same kind (attacks fuse with attacks, help with help: a fused attack never heals its victim), or the
     * same slug when nothing else is left; -1 when there is none.
     */
    public int fusionPartner() {
        int same = -1;
        boolean attack = slugs[selected].targeting().reachesEnemies();
        for (int i = 1; i < SIZE; i++) {
            int candidate = (selected + i) % SIZE;
            if (!isAvailable(candidate) || slugs[candidate].targeting().reachesEnemies() != attack) {
                continue;
            }
            if (!slugs[candidate].id().equals(slugs[selected].id())) {
                return candidate;
            }
            if (same < 0) {
                same = candidate;
            }
        }
        return same;
    }

    /**
     * Fires another slot together with the selected one (the second slug of a fusion shot), locking it as if it had
     * been the next shot. The caller must have checked {@link #isAvailable(int)}.
     */
    public SlugDefinition fireAlongside(int slot) {
        if (!isAvailable(slot)) {
            throw new IllegalStateException("Slot " + slot + " is locked");
        }
        shotCounter++;
        firedAtShot[slot] = shotCounter;
        return slugs[slot];
    }

    /** Fires the selected slot, locking it. The caller must have checked {@link #isAvailable(int)}. */
    public SlugDefinition fireSelected() {
        if (!isAvailable(selected)) {
            throw new IllegalStateException("Selected slot " + selected + " is locked");
        }
        shotCounter++;
        firedAtShot[selected] = shotCounter;
        return slugs[selected];
    }
}
