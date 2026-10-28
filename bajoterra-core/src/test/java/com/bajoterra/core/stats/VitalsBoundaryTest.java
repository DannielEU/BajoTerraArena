package com.bajoterra.core.stats;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.Test;

/** Exact values at the edges of the health, shield and overshield rules. */
class VitalsBoundaryTest {

    @Test
    void damageEqualToTheShieldLeavesHealthUntouched() {
        Vitals vitals = new Vitals(100, 50);

        double dealt = vitals.applyDamage(50, 0);

        assertThat(dealt).isEqualTo(50);
        assertThat(vitals.shield()).isZero();
        assertThat(vitals.hp()).isEqualTo(100);
    }

    @Test
    void damageThatExactlyEmptiesEverythingDefeats() {
        Vitals vitals = new Vitals(100, 50);

        assertThat(vitals.applyDamage(150, 0)).isEqualTo(150);

        assertThat(vitals.isDefeated()).isTrue();
    }

    @Test
    void damageBeyondWhatIsLeftIsNotCounted() {
        Vitals vitals = new Vitals(20, 10);

        assertThat(vitals.applyDamage(1000, 0)).isEqualTo(30);
    }

    @Test
    void pierceFractionsAreClampedToTheValidRange() {
        Vitals vitals = new Vitals(100, 50);

        vitals.applyDamage(20, 5.0);    // clamped to 1: everything goes to health
        assertThat(vitals.hp()).isEqualTo(80);
        assertThat(vitals.shield()).isEqualTo(50);

        vitals.applyDamage(20, -3.0);   // clamped to 0: the shield absorbs
        assertThat(vitals.hp()).isEqualTo(80);
        assertThat(vitals.shield()).isEqualTo(30);
    }

    @Test
    void overshieldAbsorbsBeforeTheShieldAndPierceSkipsBoth() {
        Vitals vitals = new Vitals(100, 50);
        vitals.addOvershield(30, 60);

        vitals.applyDamage(40, 0.25);   // 10 pierces, 30 is absorbed by the overshield

        assertThat(vitals.overshield()).isZero();
        assertThat(vitals.shield()).isEqualTo(50);
        assertThat(vitals.hp()).isEqualTo(90);
    }

    @Test
    void healingAtFullHealthDoesNothingAndExactFillReportsTheDifference() {
        Vitals vitals = new Vitals(100, 50);
        assertThat(vitals.heal(10)).isZero();

        vitals.applyDamage(60, 1.0);
        assertThat(vitals.heal(40)).isEqualTo(40);
        assertThat(vitals.hp()).isEqualTo(80);
        assertThat(vitals.heal(20)).isEqualTo(20);
        assertThat(vitals.hp()).isEqualTo(100);
    }

    @Test
    void nothingBelowZeroCanHealOrRestore() {
        Vitals vitals = new Vitals(100, 50);
        vitals.applyDamage(10, 0);

        assertThat(vitals.heal(0)).isZero();
        assertThat(vitals.heal(-5)).isZero();
        assertThat(vitals.restoreShield(0)).isZero();
        assertThat(vitals.restoreShield(-5)).isZero();
        assertThat(vitals.restoreShield(4)).isEqualTo(4);
        assertThat(vitals.shield()).isEqualTo(44);
    }

    @Test
    void overshieldRulesAtTheEdges() {
        Vitals vitals = new Vitals(100, 50);

        vitals.addOvershield(0, 10);
        vitals.addOvershield(10, 0);
        vitals.addOvershield(-4, 10);
        assertThat(vitals.overshield()).isZero();

        vitals.addOvershield(20, 10);
        vitals.addOvershield(20, 20);   // equal amount: the fresher (slower decaying) one replaces
        for (int i = 0; i < 10; i++) {
            vitals.tick();
        }
        assertThat(vitals.overshield()).isEqualTo(10);   // 20 - 10 ticks * (20 / 20)

        vitals.addOvershield(25, 5);
        assertThat(vitals.overshield()).isEqualTo(25);
        vitals.tick();
        assertThat(vitals.overshield()).isEqualTo(20);   // decays by 25 / 5 per tick now
    }

    @Test
    void overshieldNeverGoesNegativeAndStopsDecayingAtZero() {
        Vitals vitals = new Vitals(100, 50);
        vitals.addOvershield(3, 1);

        vitals.tick();
        vitals.tick();

        assertThat(vitals.overshield()).isZero();
    }

    @Test
    void tinyRemaindersCountAsEmpty() {
        Vitals vitals = new Vitals(10, 0);

        vitals.applyDamage(10 - 1e-12, 0);   // leaves 1e-12: rounding noise

        assertThat(vitals.hp()).isZero();
        assertThat(vitals.isDefeated()).isTrue();
    }

    @Test
    void remaindersJustAboveTheNoiseFloorSurvive() {
        Vitals vitals = new Vitals(10, 0);

        vitals.applyDamage(10 - 1e-6, 0);

        assertThat(vitals.hp()).isGreaterThan(0);
        assertThat(vitals.isDefeated()).isFalse();
    }

    @Test
    void fractionsDescribeTheRemainingPools() {
        Vitals vitals = new Vitals(100, 100);
        vitals.applyDamage(100, 0);   // the whole shield

        assertThat(vitals.totalFraction()).isEqualTo(0.5);
        assertThat(vitals.hpFraction()).isEqualTo(1.0);
        assertThat(vitals.maxHp()).isEqualTo(100);
        assertThat(vitals.maxShield()).isEqualTo(100);
    }

    @Test
    void zeroShieldCharactersAreValid() {
        Vitals vitals = new Vitals(10, 0);

        assertThat(vitals.restoreShield(5)).isZero();
        assertThat(vitals.applyDamage(4, 0)).isEqualTo(4);
        assertThat(vitals.hp()).isEqualTo(6);
    }
}
