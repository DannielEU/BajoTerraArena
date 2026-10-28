package com.bajoterra.protocol;

import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses;

import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.importer.ImportOption;
import com.tngtech.archunit.junit.AnalyzeClasses;
import com.tngtech.archunit.junit.ArchTest;

/** The wire contract must stay independent of the game, so a Unity client can be built from it alone. */
@AnalyzeClasses(packages = "com.bajoterra.protocol", importOptions = ImportOption.DoNotIncludeTests.class)
class ProtocolArchitectureTest {

    @ArchTest
    void theProtocolKnowsNothingAboutTheGameOrTheServer(JavaClasses classes) {
        noClasses()
                .should().dependOnClassesThat().resideInAnyPackage("com.bajoterra.core..", "com.bajoterra.server..")
                .allowEmptyShould(true)
                .check(classes);
    }
}
