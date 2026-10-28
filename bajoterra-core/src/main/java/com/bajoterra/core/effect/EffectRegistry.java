package com.bajoterra.core.effect;

import com.bajoterra.core.content.ContentException;
import com.bajoterra.core.content.Spec;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.ServiceLoader;
import java.util.TreeMap;

/** Maps effect type names to their factories; discovered through {@link ServiceLoader}. */
public final class EffectRegistry {

    private final Map<String, EffectFactory> factories;

    private EffectRegistry(Map<String, EffectFactory> factories) {
        this.factories = factories;
    }

    /** Discovers every {@link EffectFactory} visible to the given class loader. */
    public static EffectRegistry load(ClassLoader loader) {
        Map<String, EffectFactory> map = new TreeMap<>();
        for (EffectFactory factory : ServiceLoader.load(EffectFactory.class, loader)) {
            EffectFactory previous = map.put(factory.type(), factory);
            if (previous != null) {
                throw new ContentException("Two effect factories declare type '" + factory.type() + "': "
                        + previous.getClass().getName() + " and " + factory.getClass().getName());
            }
        }
        return new EffectRegistry(map);
    }

    /** Builds an effect from a spec whose {@code type} field names a registered factory. */
    public Effect create(Spec spec) {
        String type = spec.requireString("type");
        EffectFactory factory = factories.get(type);
        if (factory == null) {
            throw spec.fail("type", "unknown effect type '" + type + "'. Known: " + factories.keySet());
        }
        return factory.create(spec, this);
    }

    /** Builds one effect from a list of specs: none, the single effect, or a composite. */
    public Effect createAll(List<Spec> specs) {
        if (specs.isEmpty()) {
            return Effect.NONE;
        }
        List<Effect> effects = new ArrayList<>(specs.size());
        for (Spec spec : specs) {
            effects.add(create(spec));
        }
        return effects.size() == 1 ? effects.get(0) : new CompositeEffect(effects);
    }

    public boolean knows(String type) {
        return factories.containsKey(type);
    }

    public List<String> types() {
        return List.copyOf(factories.keySet());
    }
}
