package com.bajoterra.core.move;

import com.bajoterra.core.content.ContentException;
import com.bajoterra.core.content.Spec;
import java.util.List;
import java.util.Map;
import java.util.ServiceLoader;
import java.util.TreeMap;

/** Maps move-behavior type names to their factories; discovered through {@link ServiceLoader}. */
public final class MoveBehaviorRegistry {

    private final Map<String, MoveBehaviorFactory> factories;

    private MoveBehaviorRegistry(Map<String, MoveBehaviorFactory> factories) {
        this.factories = factories;
    }

    /** Discovers every {@link MoveBehaviorFactory} visible to the class loader. */
    public static MoveBehaviorRegistry load(ClassLoader loader) {
        Map<String, MoveBehaviorFactory> map = new TreeMap<>();
        for (MoveBehaviorFactory factory : ServiceLoader.load(MoveBehaviorFactory.class, loader)) {
            MoveBehaviorFactory previous = map.put(factory.type(), factory);
            if (previous != null) {
                throw new ContentException("Two move behavior factories declare type '" + factory.type() + "'");
            }
        }
        return new MoveBehaviorRegistry(map);
    }

    /** Builds the behavior described by a spec, or {@link NoMoveBehavior#INSTANCE} when the spec is null. */
    public MoveBehavior create(Spec spec) {
        if (spec == null) {
            return NoMoveBehavior.INSTANCE;
        }
        String type = spec.requireString("type");
        MoveBehaviorFactory factory = factories.get(type);
        if (factory == null) {
            throw spec.fail("type", "unknown move behavior '" + type + "'. Known: " + factories.keySet());
        }
        return factory.create(spec);
    }

    public List<String> types() {
        return List.copyOf(factories.keySet());
    }
}
