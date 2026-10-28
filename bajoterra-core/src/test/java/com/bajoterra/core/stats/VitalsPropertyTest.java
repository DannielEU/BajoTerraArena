package com.bajoterra.core.stats;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import net.jqwik.api.Arbitraries;
import net.jqwik.api.Arbitrary;
import net.jqwik.api.ForAll;
import net.jqwik.api.Property;
import net.jqwik.api.Provide;
import net.jqwik.api.constraints.DoubleRange;

/** Invariants of health and shield under any sequence of hits, heals and restores. */
class VitalsPropertyTest {

    private static final double EPSILON = 1e-9;

    /** One step: kind 0 = damage, 1 = heal, 2 = shield restore, 3 = tick; amount in [0, 200]; pierce in [0, 1]. */
    record Step(int kind, double amount, double pierce) {
    }

    @Provide
    Arbitrary<List<Step>> steps() {
        Arbitrary<Step> step = Arbitraries.integers().between(0, 3).flatMap(kind ->
                Arbitraries.doubles().between(0, 200).flatMap(amount ->
                        Arbitraries.doubles().between(0, 1).map(pierce -> new Step(kind, amount, pierce))));
        return step.list().ofMinSize(1).ofMaxSize(60);
    }

    @Property
    void poolsStayWithinBoundsWhateverHappens(
            @ForAll @DoubleRange(min = 1, max = 500) double maxHp,
            @ForAll @DoubleRange(min = 0, max = 500) double maxShield,
            @ForAll("steps") List<Step> steps) {
        Vitals vitals = new Vitals(maxHp, maxShield);
        for (Step step : steps) {
            switch (step.kind()) {
                case 0 -> vitals.applyDamage(step.amount(), step.pierce());
                case 1 -> vitals.heal(step.amount());
                case 2 -> vitals.restoreShield(step.amount());
                default -> vitals.tick();
            }
            assertThat(vitals.hp()).isBetween(0.0, maxHp);
            assertThat(vitals.shield()).isBetween(0.0, maxShield);
            assertThat(vitals.overshield()).isGreaterThanOrEqualTo(0.0);
        }
    }

    @Property
    void damageDealtNeverExceedsTheRequestedAmountOrWhatWasLeft(
            @ForAll @DoubleRange(min = 1, max = 500) double maxHp,
            @ForAll @DoubleRange(min = 0, max = 500) double maxShield,
            @ForAll @DoubleRange(min = 0, max = 1000) double amount,
            @ForAll @DoubleRange(min = 0, max = 1) double pierce) {
        Vitals vitals = new Vitals(maxHp, maxShield);
        double before = vitals.hp() + vitals.shield();

        double dealt = vitals.applyDamage(amount, pierce);

        assertThat(dealt).isBetween(0.0, amount + EPSILON);
        assertThat(dealt).isLessThanOrEqualTo(before + EPSILON);
        assertThat(before - (vitals.hp() + vitals.shield()))
                .isCloseTo(dealt, org.assertj.core.data.Offset.offset(1e-6));
    }

    @Property
    void enoughNonPiercingDamageAlwaysDefeats(
            @ForAll @DoubleRange(min = 1, max = 500) double maxHp,
            @ForAll @DoubleRange(min = 0, max = 500) double maxShield) {
        Vitals vitals = new Vitals(maxHp, maxShield);

        vitals.applyDamage(maxHp + maxShield, 0);

        assertThat(vitals.isDefeated()).isTrue();
        assertThat(vitals.hp()).isZero();
        assertThat(vitals.shield()).isZero();
    }

    @Property
    void defeatIsExactlyBothPoolsAtZero(
            @ForAll @DoubleRange(min = 1, max = 300) double maxHp,
            @ForAll @DoubleRange(min = 0, max = 300) double maxShield,
            @ForAll("steps") List<Step> steps) {
        Vitals vitals = new Vitals(maxHp, maxShield);
        for (Step step : steps) {
            if (step.kind() == 0) {
                vitals.applyDamage(step.amount(), step.pierce());
            } else if (step.kind() == 1) {
                vitals.heal(step.amount());
            }
            boolean allEmpty = vitals.hp() <= 0 && vitals.shield() <= 0 && vitals.overshield() <= 0;
            assertThat(vitals.isDefeated()).isEqualTo(allEmpty);
        }
    }
}
