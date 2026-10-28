package com.bajoterra.core.effect;

import java.util.List;

/** Applies several effects in order (Composite pattern). */
public final class CompositeEffect implements Effect {

    private final Effect[] children;

    public CompositeEffect(List<Effect> children) {
        this.children = children.toArray(new Effect[0]);
    }

    @Override
    public void apply(EffectContext ctx) {
        for (Effect child : children) {
            child.apply(ctx);
        }
    }

    public int size() {
        return children.length;
    }
}
