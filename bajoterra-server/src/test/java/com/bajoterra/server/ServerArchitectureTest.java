package com.bajoterra.server;

import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses;

import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.importer.ImportOption;
import com.tngtech.archunit.junit.AnalyzeClasses;
import com.tngtech.archunit.junit.ArchTest;

/** Layering rules of the server. */
@AnalyzeClasses(packages = "com.bajoterra.server", importOptions = ImportOption.DoNotIncludeTests.class)
class ServerArchitectureTest {

    @ArchTest
    void transportsOnlyMoveBytes(JavaClasses classes) {
        // A transport is an adapter: it must work without knowing anything about the game itself.
        noClasses()
                .that().haveSimpleNameEndingWith("Transport")
                .should().dependOnClassesThat().resideInAPackage("com.bajoterra.core..")
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void theServerDoesNotDependOnTheBenchmarks(JavaClasses classes) {
        noClasses()
                .should().dependOnClassesThat().resideInAPackage("com.bajoterra.bench..")
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void headlessToolsWorkWithoutTheNetworkProtocol(JavaClasses classes) {
        // The bot runner, the scheduler and the catalog printer must stay usable without any wire format.
        noClasses()
                .that().haveNameMatching(".*server\\.(BotMatchRunner|ContentCatalogPrinter|TickScheduler)(\\$.*)?")
                .should().dependOnClassesThat().resideInAPackage("com.bajoterra.protocol..")
                .allowEmptyShould(true)
                .check(classes);
    }
}
