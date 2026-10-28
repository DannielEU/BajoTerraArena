package com.bajoterra.core;

import static org.assertj.core.api.Assertions.assertThat;

import net.jqwik.api.ForAll;
import net.jqwik.api.Property;
import net.jqwik.api.constraints.IntRange;

/**
 * Checks that JUnit, AssertJ and jqwik work with JDK 27 bytecode (milestone M0 spike).
 * The real game properties live next to the code they exercise.
 */
class ToolchainSmokeTest {

    @Property
    void jqwikRunsOnThisJdk(
            @ForAll @IntRange(min = 0, max = 1000) int a,
            @ForAll @IntRange(min = 0, max = 1000) int b) {
        assertThat(a + b).isEqualTo(b + a);
    }
}
