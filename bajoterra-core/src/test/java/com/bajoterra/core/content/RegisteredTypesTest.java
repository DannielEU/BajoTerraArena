package com.bajoterra.core.content;

import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.move.MoveBehaviorFactory;
import com.tngtech.archunit.core.domain.JavaClass;
import com.tngtech.archunit.core.importer.ClassFileImporter;
import com.tngtech.archunit.core.importer.ImportOption;
import java.util.HashSet;
import java.util.ServiceLoader;
import java.util.Set;
import java.util.TreeSet;
import org.junit.jupiter.api.Test;

/**
 * Guards the plug-in mechanism. A factory that forgets {@code @AutoService} would silently never load; these tests
 * compare what exists on the class path with what the service loader actually discovers.
 */
class RegisteredTypesTest {

    private static Set<String> typesOf(Iterable<? extends EffectFactory> factories) {
        Set<String> types = new TreeSet<>();
        for (EffectFactory factory : factories) {
            types.add(factory.type());
        }
        return types;
    }

    @Test
    void theBuiltInEffectTypesAreExactlyTheDocumentedOnes() {
        Set<String> types = typesOf(ServiceLoader.load(EffectFactory.class));

        assertThat(types).contains("damage", "heal", "restore_shield", "overshield", "knockback", "area", "chain",
                "conditional", "cleanse", "burn", "slow", "freeze", "stun", "regen", "blind");
    }

    @Test
    void theBuiltInMoveBehaviorTypesAreRegistered() {
        Set<String> types = new TreeSet<>();
        for (MoveBehaviorFactory factory : ServiceLoader.load(MoveBehaviorFactory.class)) {
            types.add(factory.type());
        }

        assertThat(types).contains("impulse", "drive");
    }

    @Test
    void everyEffectFactoryClassOnTheClassPathIsDiscoverable() throws ReflectiveOperationException {
        Set<String> discovered = new HashSet<>();
        ServiceLoader.load(EffectFactory.class).forEach(f -> discovered.add(f.getClass().getName()));

        var classes = new ClassFileImporter().withImportOption(ImportOption.Predefined.DO_NOT_INCLUDE_JARS)
                .importPackages("com.bajoterra");
        for (JavaClass type : classes) {
            if (type.isAssignableTo(EffectFactory.class) && !type.isInterface()
                    && type.getModifiers().stream().noneMatch(m -> m.name().equals("ABSTRACT"))) {
                assertThat(discovered).as("%s must be registered with @AutoService", type.getName())
                        .contains(type.getName());
            }
        }
    }

    @Test
    void everyMoveBehaviorFactoryClassOnTheClassPathIsDiscoverable() {
        Set<String> discovered = new HashSet<>();
        ServiceLoader.load(MoveBehaviorFactory.class).forEach(f -> discovered.add(f.getClass().getName()));

        var classes = new ClassFileImporter().withImportOption(ImportOption.Predefined.DO_NOT_INCLUDE_JARS)
                .importPackages("com.bajoterra");
        for (JavaClass type : classes) {
            if (type.isAssignableTo(MoveBehaviorFactory.class) && !type.isInterface()) {
                assertThat(discovered).as("%s must be registered with @AutoService", type.getName())
                        .contains(type.getName());
            }
        }
    }

    @Test
    void twoFactoriesCannotClaimTheSameType() {
        var types = new HashSet<String>();
        for (EffectFactory factory : ServiceLoader.load(EffectFactory.class)) {
            assertThat(types.add(factory.type())).as("duplicate type %s", factory.type()).isTrue();
        }
    }
}
