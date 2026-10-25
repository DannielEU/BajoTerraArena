package com.bajoterra.core;

import static org.assertj.core.api.Assertions.assertThat;

import net.jqwik.api.ForAll;
import net.jqwik.api.Property;
import net.jqwik.api.constraints.IntRange;

/**
 * Comprueba que JUnit, AssertJ y jqwik funcionan con bytecode del JDK 27 (spike del hito M0).
 * Las pruebas reales de propiedades del juego se agregan desde M1.
 */
class ToolchainSmokeTest {

    @Property
    void jqwikRunsOnThisJdk(
            @ForAll @IntRange(min = 0, max = 1000) int a,
            @ForAll @IntRange(min = 0, max = 1000) int b) {
        assertThat(a + b).isEqualTo(b + a);
    }
}
