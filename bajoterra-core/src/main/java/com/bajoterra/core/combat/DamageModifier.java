package com.bajoterra.core.combat;

/** One link of the damage chain of responsibility: adjusts (or zeroes) the amount of a pending hit. */
@FunctionalInterface
public interface DamageModifier {

    void modify(DamageEvent event);
}
