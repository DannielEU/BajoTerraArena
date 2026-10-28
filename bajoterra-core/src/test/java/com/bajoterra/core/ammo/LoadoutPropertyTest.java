package com.bajoterra.core.ammo;

import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.Fixtures;
import com.bajoterra.core.slug.SlugDefinition;
import java.util.ArrayList;
import java.util.List;
import net.jqwik.api.Arbitraries;
import net.jqwik.api.Arbitrary;
import net.jqwik.api.ForAll;
import net.jqwik.api.Property;
import net.jqwik.api.Provide;
import net.jqwik.api.constraints.IntRange;

/** Invariants of the loadout under arbitrary sequences of selecting, cycling and firing. */
class LoadoutPropertyTest {

    /** kind: 0 = select(slot), 1 = cycle(+1), 2 = cycle(-1), 3 = fire the selected slot if possible. */
    record Op(int kind, int slot) {
    }

    @Provide
    Arbitrary<List<Op>> operations() {
        Arbitrary<Op> op = Arbitraries.integers().between(0, 3).flatMap(kind ->
                Arbitraries.integers().between(0, 9).map(slot -> new Op(kind, slot)));
        return op.list().ofMinSize(1).ofMaxSize(200);
    }

    private Loadout loadout(int reloadShots) {
        List<SlugDefinition> slugs = new ArrayList<>();
        List<SlugDefinition> catalog = Fixtures.CONTENT.slugs().all();
        for (int i = 0; i < Loadout.SIZE; i++) {
            slugs.add(catalog.get(i % catalog.size()));
        }
        return new Loadout(slugs, reloadShots);
    }

    @Property
    void neverMoreThanReloadShotsSlotsAreLockedAndTheTenSlugsAreConserved(
            @ForAll @IntRange(min = 0, max = 9) int reloadShots,
            @ForAll("operations") List<Op> operations) {
        Loadout loadout = loadout(reloadShots);
        SlugDefinition[] original = new SlugDefinition[Loadout.SIZE];
        for (int i = 0; i < Loadout.SIZE; i++) {
            original[i] = loadout.slug(i);
        }

        for (Op op : operations) {
            switch (op.kind()) {
                case 0 -> loadout.select(op.slot());
                case 1 -> loadout.cycle(1);
                case 2 -> loadout.cycle(-1);
                default -> {
                    if (loadout.isAvailable(loadout.selected())) {
                        int slot = loadout.selected();
                        int before = loadout.shotCounter();
                        loadout.fireSelected();
                        assertThat(loadout.shotCounter()).isEqualTo(before + 1);
                        assertThat(loadout.isAvailable(slot)).isEqualTo(reloadShots == 0);
                    }
                }
            }
            int locked = 0;
            for (int slot = 0; slot < Loadout.SIZE; slot++) {
                if (!loadout.isAvailable(slot)) {
                    locked++;
                    assertThat(loadout.shotsUntilReady(slot)).isBetween(1, reloadShots);
                }
                assertThat(loadout.slug(slot)).isSameAs(original[slot]);
            }
            assertThat(locked).isLessThanOrEqualTo(reloadShots);
            assertThat(loadout.hasAvailable()).isTrue();
        }
    }

    @Property
    void aSlotFiredAtShotNIsAvailableExactlyFromShotNPlusReload(
            @ForAll @IntRange(min = 1, max = 9) int reloadShots,
            @ForAll @IntRange(min = 0, max = 9) int firstSlot) {
        Loadout loadout = loadout(reloadShots);
        loadout.select(firstSlot);
        loadout.fireSelected();

        int othersFired = 0;
        int cursor = firstSlot;
        while (!loadout.isAvailable(firstSlot)) {
            cursor = (cursor + 1) % Loadout.SIZE;
            if (cursor == firstSlot) {
                continue;
            }
            assertThat(loadout.select(cursor)).isTrue();
            loadout.fireSelected();
            othersFired++;
        }

        assertThat(othersFired).isEqualTo(reloadShots);
    }
}
