package com.bajoterra.core.ammo;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.bajoterra.core.Fixtures;
import com.bajoterra.core.slug.SlugDefinition;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

/** The "a fired slug is locked for the next 3 shots" rule, checked case by case. */
class LoadoutCooldownTest {

    private static final List<String> FIVE_DISTINCT = List.of("infurnus", "flaringo", "arachnet", "tormato", "doc");

    private Loadout loadout(int reloadShots) {
        List<SlugDefinition> slugs = new ArrayList<>();
        for (String id : FIVE_DISTINCT) {
            slugs.add(Fixtures.CONTENT.slugs().require(id));
        }
        return new Loadout(slugs, reloadShots);
    }

    @Test
    void startsFullyUnlockedWithFiveSlugs() {
        Loadout loadout = loadout(3);

        assertThat(Loadout.SIZE).isEqualTo(5);
        for (int slot = 0; slot < Loadout.SIZE; slot++) {
            assertThat(loadout.isAvailable(slot)).isTrue();
            assertThat(loadout.shotsUntilReady(slot)).isZero();
        }
    }

    @Test
    void aFiredSlugStaysLockedForTheNextThreeShotsAndReturnsOnTheFourth() {
        Loadout loadout = loadout(3);

        loadout.select(0);
        loadout.fireSelected(); // shot 1 uses slot 0
        assertThat(loadout.isAvailable(0)).isFalse();
        assertThat(loadout.shotsUntilReady(0)).isEqualTo(3);

        loadout.select(1);
        loadout.fireSelected(); // shot 2
        assertThat(loadout.isAvailable(0)).isFalse();
        assertThat(loadout.shotsUntilReady(0)).isEqualTo(2);

        loadout.select(2);
        loadout.fireSelected(); // shot 3
        assertThat(loadout.isAvailable(0)).isFalse();
        assertThat(loadout.shotsUntilReady(0)).isEqualTo(1);

        loadout.select(3);
        loadout.fireSelected(); // shot 4
        assertThat(loadout.isAvailable(0)).isTrue(); // usable for shot 5 (n + 4)
        assertThat(loadout.shotsUntilReady(0)).isZero();
        assertThat(loadout.select(0)).isTrue();
    }

    @Test
    void lockedSlotsCannotBeSelectedOrFired() {
        Loadout loadout = loadout(3);
        loadout.select(3);
        loadout.fireSelected();

        assertThat(loadout.select(3)).isFalse();
        assertThat(loadout.select(4)).isTrue();
        assertThat(loadout.selected()).isEqualTo(4);
    }

    @Test
    void firingALockedSlotIsRejected() {
        Loadout loadout = loadout(3);
        loadout.fireSelected();
        // the selection still points at the slot that was just fired and is now locked
        assertThatThrownBy(loadout::fireSelected).isInstanceOf(IllegalStateException.class);
    }

    @Test
    void cycleSkipsLockedSlotsInBothDirections() {
        Loadout loadout = loadout(3);
        loadout.select(1);
        loadout.fireSelected(); // locks slot 1
        loadout.select(0);

        assertThat(loadout.cycle(1)).isTrue();
        assertThat(loadout.selected()).isEqualTo(2); // 1 is locked

        loadout.select(2);
        assertThat(loadout.cycle(-1)).isTrue();
        assertThat(loadout.selected()).isEqualTo(0);
    }

    @Test
    void cycleWrapsAroundTheLoadout() {
        Loadout loadout = loadout(3);
        loadout.select(4);

        loadout.cycle(1);

        assertThat(loadout.selected()).isZero();
    }

    @Test
    void aSlugCanOverrideTheLockLength() {
        List<SlugDefinition> slugs = new ArrayList<>();
        SlugDefinition heavy = SlugDefinition.builder("heavy").reloadShotsOverride(4).build();
        slugs.add(heavy);
        for (int i = 1; i < Loadout.SIZE; i++) {
            slugs.add(SlugDefinition.builder("plain" + i).build());
        }
        Loadout loadout = new Loadout(slugs, 3);

        loadout.fireSelected();
        for (int slot = 1; slot <= 3; slot++) {
            loadout.select(slot);
            loadout.fireSelected();
        }

        assertThat(loadout.reloadShotsOf(0)).isEqualTo(4);
        assertThat(loadout.isAvailable(0)).isFalse();
        loadout.select(4);
        loadout.fireSelected();
        assertThat(loadout.isAvailable(0)).isTrue();
    }

    @ParameterizedTest
    @ValueSource(ints = {0, 1, 2, 3, 4})
    void lockLengthIsConfigurable(int reload) {
        Loadout loadout = loadout(reload);
        loadout.fireSelected();

        int shotsFiredAfter = 0;
        while (!loadout.isAvailable(0)) {
            loadout.select(1 + shotsFiredAfter % (Loadout.SIZE - 1));
            loadout.fireSelected();
            shotsFiredAfter++;
            assertThat(shotsFiredAfter).isLessThanOrEqualTo(reload);
        }
        assertThat(shotsFiredAfter).isEqualTo(reload);
    }

    @Test
    void resetUnlocksEverything() {
        Loadout loadout = loadout(3);
        loadout.fireSelected();
        loadout.select(4);
        loadout.fireSelected();

        loadout.reset();

        for (int slot = 0; slot < Loadout.SIZE; slot++) {
            assertThat(loadout.isAvailable(slot)).isTrue();
        }
        assertThat(loadout.shotCounter()).isZero();
        assertThat(loadout.selected()).isZero();
    }

    @Test
    void rejectsWrongSizesAndImpossibleLocks() {
        List<SlugDefinition> four = new ArrayList<>(
                FIVE_DISTINCT.subList(0, 4).stream().map(id -> Fixtures.CONTENT.slugs().require(id)).toList());
        assertThatThrownBy(() -> new Loadout(four, 3)).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> loadout(5)).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> loadout(-1)).isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void validatorChecksSizeIdsAndDuplicates() {
        var catalog = Fixtures.CONTENT.slugs();

        assertThat(LoadoutValidator.validate(FIVE_DISTINCT, catalog, 3, 2)).isEmpty();
        assertThat(LoadoutValidator.validate(FIVE_DISTINCT.subList(0, 4), catalog, 3, 2))
                .anyMatch(p -> p.contains("exactly 5"));

        List<String> unknown = new ArrayList<>(FIVE_DISTINCT);
        unknown.set(0, "no_such_slug");
        assertThat(LoadoutValidator.validate(unknown, catalog, 3, 2)).anyMatch(p -> p.contains("Unknown slug"));

        List<String> triple = new ArrayList<>(FIVE_DISTINCT);
        triple.set(1, "infurnus");
        triple.set(2, "infurnus");
        assertThat(LoadoutValidator.validate(triple, catalog, 3, 2)).anyMatch(p -> p.contains("more than 2"));

        assertThat(LoadoutValidator.validate(FIVE_DISTINCT, catalog, 5, 2))
                .anyMatch(p -> p.contains("smaller than the loadout size"));
    }
}
