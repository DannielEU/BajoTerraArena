package com.bajoterra.core.content;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Immutable collection of definitions of one kind, addressable by id and by dense index. */
public final class Registry<T extends Identified> {

    private final String kind;
    private final List<T> items;
    private final Map<String, T> byId;

    /**
     * @param kind human-readable name used in error messages ("slug", "move", ...)
     * @param items definitions whose {@link Identified#index()} equals their position in the list
     */
    public Registry(String kind, List<T> items) {
        this.kind = kind;
        this.items = List.copyOf(items);
        Map<String, T> map = new LinkedHashMap<>();
        for (int i = 0; i < this.items.size(); i++) {
            T item = this.items.get(i);
            if (item.index() != i) {
                throw new IllegalArgumentException(kind + " '" + item.id() + "' has index " + item.index()
                        + " but sits at position " + i);
            }
            if (map.put(item.id(), item) != null) {
                throw new ContentException("Duplicate " + kind + " id '" + item.id() + "'");
            }
        }
        this.byId = Collections.unmodifiableMap(map);
    }

    /** Returns the definition with the given id or throws a {@link ContentException}. */
    public T require(String id) {
        T item = byId.get(id);
        if (item == null) {
            throw new ContentException("Unknown " + kind + " '" + id + "'. Known: " + byId.keySet());
        }
        return item;
    }

    /** Returns the definition with the given id, or null. */
    public T find(String id) {
        return byId.get(id);
    }

    public boolean contains(String id) {
        return byId.containsKey(id);
    }

    public T get(int index) {
        return items.get(index);
    }

    public int size() {
        return items.size();
    }

    public List<T> all() {
        return items;
    }
}
