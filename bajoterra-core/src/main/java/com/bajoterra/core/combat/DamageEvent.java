package com.bajoterra.core.combat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.Element;

/** Mutable damage request that flows through the {@link DamagePipeline}; one instance is reused for every hit. */
public final class DamageEvent {

    /** Who dealt the damage; null for environmental damage. */
    public Combatant source;
    public Combatant target;
    public double amount;
    public Element element = Element.NEUTRAL;
    /** Fraction of the damage that bypasses overshield and shield. */
    public double shieldPierce;

    DamageEvent set(Combatant newSource, Combatant newTarget, double newAmount, Element newElement, double pierce) {
        this.source = newSource;
        this.target = newTarget;
        this.amount = newAmount;
        this.element = newElement;
        this.shieldPierce = pierce;
        return this;
    }
}
