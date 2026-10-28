package com.bajoterra.core.stats;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.Test;

class VitalsTest {

    @Test
    void shieldAbsorbsDamageBeforeHealth() {
        Vitals vitals = new Vitals(100, 50);

        double dealt = vitals.applyDamage(30, 0);

        assertThat(dealt).isEqualTo(30);
        assertThat(vitals.shield()).isEqualTo(20);
        assertThat(vitals.hp()).isEqualTo(100);
    }

    @Test
    void overflowGoesToHealthWhenShieldBreaks() {
        Vitals vitals = new Vitals(100, 50);

        vitals.applyDamage(80, 0);

        assertThat(vitals.shield()).isZero();
        assertThat(vitals.hp()).isEqualTo(70);
    }

    @Test
    void defeatNeedsBothShieldAndHealthAtZero() {
        Vitals vitals = new Vitals(100, 50);

        vitals.applyDamage(100, 0);
        assertThat(vitals.isDefeated()).isFalse();
        assertThat(vitals.hp()).isEqualTo(50);

        vitals.applyDamage(50, 0);
        assertThat(vitals.isDefeated()).isTrue();
    }

    @Test
    void piercingDamageSkipsTheShield() {
        Vitals vitals = new Vitals(100, 50);

        vitals.applyDamage(20, 0.5);

        assertThat(vitals.shield()).isEqualTo(40);
        assertThat(vitals.hp()).isEqualTo(90);
    }

    @Test
    void healAndShieldRestoreRespectTheMaximum() {
        Vitals vitals = new Vitals(100, 50);
        vitals.applyDamage(70, 0);

        assertThat(vitals.heal(500)).isEqualTo(20);
        assertThat(vitals.restoreShield(500)).isEqualTo(50);
        assertThat(vitals.hp()).isEqualTo(100);
        assertThat(vitals.shield()).isEqualTo(50);
    }

    @Test
    void defeatedCombatantsCannotBeHealed() {
        Vitals vitals = new Vitals(10, 0);
        vitals.applyDamage(10, 0);

        assertThat(vitals.isDefeated()).isTrue();
        assertThat(vitals.heal(5)).isZero();
        assertThat(vitals.restoreShield(5)).isZero();
    }

    @Test
    void overshieldAbsorbsFirstAndDecaysToZero() {
        Vitals vitals = new Vitals(100, 50);
        vitals.addOvershield(40, 4);

        vitals.applyDamage(30, 0);
        assertThat(vitals.overshield()).isEqualTo(10);
        assertThat(vitals.shield()).isEqualTo(50);

        for (int i = 0; i < 4; i++) {
            vitals.tick();
        }
        assertThat(vitals.overshield()).isZero();
    }

    @Test
    void weakerOvershieldDoesNotReplaceAStrongerOne() {
        Vitals vitals = new Vitals(100, 50);
        vitals.addOvershield(40, 100);
        vitals.addOvershield(10, 100);

        assertThat(vitals.overshield()).isEqualTo(40);
    }

    @Test
    void nonPositiveDamageIsIgnored() {
        Vitals vitals = new Vitals(100, 50);

        assertThat(vitals.applyDamage(0, 0)).isZero();
        assertThat(vitals.applyDamage(-5, 0)).isZero();
        assertThat(vitals.applyDamage(Double.NaN, 0)).isZero();
        assertThat(vitals.hp()).isEqualTo(100);
    }

    @Test
    void resetRestoresEverything() {
        Vitals vitals = new Vitals(100, 50);
        vitals.applyDamage(120, 0);
        vitals.addOvershield(10, 10);

        vitals.reset();

        assertThat(vitals.hp()).isEqualTo(100);
        assertThat(vitals.shield()).isEqualTo(50);
        assertThat(vitals.overshield()).isZero();
        assertThat(vitals.totalFraction()).isEqualTo(1.0);
        assertThat(vitals.hpFraction()).isEqualTo(1.0);
    }

    @Test
    void rejectsInvalidMaximums() {
        assertThatThrownBy(() -> new Vitals(0, 10)).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> new Vitals(10, -1)).isInstanceOf(IllegalArgumentException.class);
    }
}
