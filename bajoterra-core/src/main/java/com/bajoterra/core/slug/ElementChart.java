package com.bajoterra.core.slug;

import java.util.Arrays;

/** Damage multipliers between an attacking element and a defending element. Immutable. */
public final class ElementChart {

    private final double[] table = new double[Element.COUNT * Element.COUNT];

    private ElementChart() {
        Arrays.fill(table, 1.0);
    }

    /** A chart where every matchup is neutral (multiplier 1). */
    public static ElementChart neutral() {
        return new ElementChart();
    }

    /** Creates a mutable builder. */
    public static Builder builder() {
        return new Builder();
    }

    /** Multiplier applied when {@code attacker} hits a target whose affinity is {@code defender}. */
    public double multiplier(Element attacker, Element defender) {
        return table[attacker.ordinal() * Element.COUNT + defender.ordinal()];
    }

    /** Builder for {@link ElementChart}. */
    public static final class Builder {
        private final ElementChart chart = new ElementChart();

        private Builder() {
        }

        public Builder set(Element attacker, Element defender, double multiplier) {
            if (!(multiplier >= 0)) {
                throw new IllegalArgumentException("Multiplier must be >= 0: " + multiplier);
            }
            chart.table[attacker.ordinal() * Element.COUNT + defender.ordinal()] = multiplier;
            return this;
        }

        public ElementChart build() {
            ElementChart copy = new ElementChart();
            System.arraycopy(chart.table, 0, copy.table, 0, chart.table.length);
            return copy;
        }
    }
}
