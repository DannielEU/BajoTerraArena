package com.bajoterra.core;

import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses;

import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.importer.ImportOption;
import com.tngtech.archunit.junit.AnalyzeClasses;
import com.tngtech.archunit.junit.ArchTest;

/** Reglas de arquitectura del core (se amplian en cada hito). */
@AnalyzeClasses(packages = "com.bajoterra.core", importOptions = ImportOption.DoNotIncludeTests.class)
class ArchitectureTest {

    @ArchTest
    void coreDoesNotDependOnServerOrProtocol(JavaClasses classes) {
        noClasses()
                .that().resideInAPackage("com.bajoterra.core..")
                .should().dependOnClassesThat()
                .resideInAnyPackage("com.bajoterra.server..", "com.bajoterra.protocol..")
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void coreNeverReadsTheWallClock(JavaClasses classes) {
        noClasses()
                .that().resideInAPackage("com.bajoterra.core..")
                .should().callMethod(System.class, "currentTimeMillis")
                .orShould().callMethod(System.class, "nanoTime")
                .allowEmptyShould(true)
                .check(classes);
    }
}
