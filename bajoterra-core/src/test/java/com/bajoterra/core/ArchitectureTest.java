package com.bajoterra.core;

import static com.tngtech.archunit.core.domain.JavaCall.Predicates.target;
import static com.tngtech.archunit.core.domain.JavaClass.Predicates.resideInAPackage;
import static com.tngtech.archunit.core.domain.JavaClass.Predicates.type;
import static com.tngtech.archunit.core.domain.properties.HasName.Predicates.nameMatching;
import static com.tngtech.archunit.core.domain.properties.HasOwner.Predicates.With.owner;
import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses;

import com.tngtech.archunit.core.domain.JavaClasses;
import com.tngtech.archunit.core.importer.ImportOption;
import com.tngtech.archunit.junit.AnalyzeClasses;
import com.tngtech.archunit.junit.ArchTest;
import com.tngtech.archunit.library.GeneralCodingRules;

/** Architecture rules of the core: layering, extension points and the determinism guarantees. */
@AnalyzeClasses(packages = "com.bajoterra.core", importOptions = ImportOption.DoNotIncludeTests.class)
class ArchitectureTest {

    // ------------------------------------------------------------------------------------------ layering

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
    void contentDefinitionsAndPlainDataDoNotDependOnTheSimulationFacade(JavaClasses classes) {
        noClasses()
                .that().resideInAnyPackage("..slug..", "..character..", "..item..", "..world..", "..stats..",
                        "..math..", "..event..", "..combat..", "..effect..")
                .should().dependOnClassesThat().resideInAPackage("..sim..")
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void onlyTheSimulationKnowsAboutTheBots(JavaClasses classes) {
        noClasses()
                .that().resideOutsideOfPackages("..ai..", "..sim..")
                .should().dependOnClassesThat().resideInAPackage("..ai..")
                .allowEmptyShould(true)
                .check(classes);
    }

    // ------------------------------------------------------------------------------------------ extension points

    @ArchTest
    void builtInEffectsAreOnlyCreatedByTheirFactories(JavaClasses classes) {
        noClasses()
                .that().resideOutsideOfPackage("..effect.builtin..")
                .should().callConstructorWhere(target(owner(resideInAPackage("..effect.builtin.."))))
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void builtInMoveBehaviorsAreOnlyCreatedByTheirFactories(JavaClasses classes) {
        noClasses()
                .that().resideOutsideOfPackage("..move.builtin..")
                .should().callConstructorWhere(target(owner(resideInAPackage("..move.builtin.."))))
                .allowEmptyShould(true)
                .check(classes);
    }

    // ------------------------------------------------------------------------------------------ determinism

    @ArchTest
    void coreNeverReadsTheWallClock(JavaClasses classes) {
        noClasses()
                .that().resideInAPackage("com.bajoterra.core..")
                .should().callMethod(System.class, "currentTimeMillis")
                .orShould().callMethod(System.class, "nanoTime")
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void coreUsesOnlyTheSeededRandomSource(JavaClasses classes) {
        noClasses()
                .that().resideInAPackage("com.bajoterra.core..")
                .should().dependOnClassesThat().haveFullyQualifiedName("java.util.Random")
                .orShould().dependOnClassesThat().haveFullyQualifiedName("java.util.concurrent.ThreadLocalRandom")
                .orShould().callMethod(Math.class, "random")
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void transcendentalMathGoesThroughStrictMathSoReplaysMatchOnEveryCpu(JavaClasses classes) {
        noClasses()
                .that().resideInAPackage("com.bajoterra.core..")
                .should().callMethodWhere(target(owner(type(Math.class)))
                        .and(target(nameMatching("sin|cos|tan|asin|acos|atan|atan2|hypot|pow|exp|log|log10|cbrt"))))
                .allowEmptyShould(true)
                .check(classes);
    }

    @ArchTest
    void coreHasNoThreadsOrConcurrencyBecauseTheSimulationIsSingleThreaded(JavaClasses classes) {
        noClasses()
                .that().resideInAPackage("com.bajoterra.core..")
                .should().dependOnClassesThat().resideInAnyPackage("java.util.concurrent..")
                .allowEmptyShould(true)
                .check(classes);
    }

    // ------------------------------------------------------------------------------------------ hygiene

    @ArchTest
    void coreDoesNotPrintToTheConsole(JavaClasses classes) {
        GeneralCodingRules.NO_CLASSES_SHOULD_ACCESS_STANDARD_STREAMS.check(classes);
    }

    @ArchTest
    void coreNeverThrowsRawRuntimeExceptions(JavaClasses classes) {
        GeneralCodingRules.NO_CLASSES_SHOULD_THROW_GENERIC_EXCEPTIONS.check(classes);
    }
}
