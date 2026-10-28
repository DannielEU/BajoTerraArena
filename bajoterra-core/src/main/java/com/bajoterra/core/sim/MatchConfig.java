package com.bajoterra.core.sim;

import com.bajoterra.core.ammo.LoadoutValidator;
import com.bajoterra.core.rules.EliminationRoundsRules;
import com.bajoterra.core.rules.MatchRules;
import java.util.ArrayList;
import java.util.List;

/**
 * Immutable configuration of a match.
 *
 * @param seed seed of the deterministic random source
 * @param mapId id of the map to play on
 * @param slots participants; team sizes may be 1-3 per team before bot filling
 * @param rules the match rule set
 * @param friendlyFire damage multiplier between teammates (0 disables friendly fire)
 * @param maxDuplicateSlugs most copies of one slug allowed in a loadout
 * @param fillWithBots complete both teams to {@link #TEAM_SIZE} members with bots (disable for scripted tests)
 */
public record MatchConfig(
        long seed,
        String mapId,
        List<SlotConfig> slots,
        MatchRules rules,
        double friendlyFire,
        int maxDuplicateSlugs,
        boolean fillWithBots) {

    /** Number of combatants per team in a full match. */
    public static final int TEAM_SIZE = 3;

    public MatchConfig {
        slots = List.copyOf(slots);
        if (friendlyFire < 0) {
            throw new IllegalArgumentException("friendlyFire must be >= 0");
        }
    }

    /** Starts a builder with best-of-five rules, no friendly fire and at most two copies per slug. */
    public static Builder builder(String mapId) {
        return new Builder(mapId);
    }

    /** Fluent builder for {@link MatchConfig}. */
    public static final class Builder {
        private final String mapId;
        private long seed = 1;
        private final List<SlotConfig> slots = new ArrayList<>();
        private MatchRules rules = EliminationRoundsRules.bestOfFive();
        private double friendlyFire;
        private int maxDuplicateSlugs = LoadoutValidator.DEFAULT_MAX_DUPLICATES;
        private boolean fillWithBots = true;

        private Builder(String mapId) {
            this.mapId = mapId;
        }

        public Builder seed(long value) {
            this.seed = value;
            return this;
        }

        public Builder slot(SlotConfig slot) {
            this.slots.add(slot);
            return this;
        }

        public Builder rules(MatchRules value) {
            this.rules = value;
            return this;
        }

        public Builder friendlyFire(double value) {
            this.friendlyFire = value;
            return this;
        }

        public Builder maxDuplicateSlugs(int value) {
            this.maxDuplicateSlugs = value;
            return this;
        }

        public Builder fillWithBots(boolean value) {
            this.fillWithBots = value;
            return this;
        }

        public MatchConfig build() {
            return new MatchConfig(seed, mapId, slots, rules, friendlyFire, maxDuplicateSlugs, fillWithBots);
        }
    }
}
