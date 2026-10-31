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

    @Override
    public double areaRadius() {
        double largest = 0;
        for (Effect child : children) {
            largest = Math.max(largest, child.areaRadius());
        }
        return largest;
    }

    @Override
    public double flashRadius() {
        double largest = 0;
        for (Effect child : children) {
            largest = Math.max(largest, child.flashRadius());
        }
        return largest;
    }

    @Override
    public double flashBeamCos() {
        double narrowest = -1;
        for (Effect child : children) {
            narrowest = Math.max(narrowest, child.flashBeamCos());
        }
        return narrowest;
    }

    @Override
    public boolean raisesWall() {
        for (Effect child : children) {
            if (child.raisesWall()) {
                return true;
            }
        }
        return false;
    }

    public int size() {
        return children.length;
    }
}
