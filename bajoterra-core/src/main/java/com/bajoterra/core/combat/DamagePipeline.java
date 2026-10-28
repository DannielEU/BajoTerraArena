package com.bajoterra.core.combat;

import com.bajoterra.core.slug.ElementChart;

/**
 * Ordered chain of {@link DamageModifier}s every hit goes through before reaching the target's vitals
 * (Chain of Responsibility). The standard order is: elemental matchup, status vulnerabilities, invulnerability,
 * friendly fire.
 */
public final class DamagePipeline {

    private final DamageModifier[] modifiers;

    public DamagePipeline(DamageModifier... modifiers) {
        this.modifiers = modifiers.clone();
    }

    /**
     * The game's default pipeline.
     *
     * @param chart elemental matchup table
     * @param friendlyFireMultiplier damage multiplier between teammates (0 disables friendly fire)
     */
    public static DamagePipeline standard(ElementChart chart, double friendlyFireMultiplier) {
        return new DamagePipeline(
                event -> event.amount *= chart.multiplier(event.element, event.target.character.affinity()),
                event -> event.amount *= event.target.stats.elementTakenMultiplier(event.element),
                event -> {
                    if (event.target.stats.invulnerable) {
                        event.amount = 0;
                    }
                },
                event -> {
                    if (event.source != null && event.source != event.target
                            && event.source.team == event.target.team) {
                        event.amount *= friendlyFireMultiplier;
                    }
                });
    }

    /** Runs the chain; stops early once the damage has been fully negated. */
    public void run(DamageEvent event) {
        for (DamageModifier modifier : modifiers) {
            modifier.modify(event);
            if (!(event.amount > 0)) {
                event.amount = 0;
                return;
            }
        }
    }
}
