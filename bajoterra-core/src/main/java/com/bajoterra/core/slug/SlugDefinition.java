package com.bajoterra.core.slug;

import com.bajoterra.core.content.Identified;
import com.bajoterra.core.effect.Effect;

/**
 * Immutable definition of a slug type (a flyweight shared by every fired instance).
 *
 * @param fireDelayMultiplier multiplies the blaster's interval between shots after firing this slug
 * @param reloadShotsOverride shots that must pass before this slug can be fired again; -1 uses the blaster default
 * @param pierceWorld when true the slug flies through obstacles instead of stopping at them
 * @param botWeight how much bots favour this slug (1 = neutral); a balance knob for the AI, not for players
 * @param onHit effects applied to a valid combatant that is hit
 * @param onWorldHit effects applied where the slug stops against an obstacle
 * @param onTick effects applied (without a target) at the slug position every tick of flight
 * @param onExpire effects applied (without a target) where the slug expires in flight
 */
public record SlugDefinition(
        String id,
        int index,
        String name,
        Element element,
        SlugRole role,
        TargetingRule targeting,
        ProjectileSpec projectile,
        double fireDelayMultiplier,
        int reloadShotsOverride,
        boolean pierceWorld,
        double botWeight,
        Effect onHit,
        Effect onWorldHit,
        Effect onTick,
        Effect onExpire) implements Identified {

    public SlugDefinition {
        if (id == null || id.isBlank()) {
            throw new IllegalArgumentException("Slug id is required");
        }
        if (!(fireDelayMultiplier > 0) || reloadShotsOverride < -1 || !(botWeight > 0)) {
            throw new IllegalArgumentException("Invalid slug '" + id + "': fireDelayMultiplier="
                    + fireDelayMultiplier + " reloadShotsOverride=" + reloadShotsOverride);
        }
    }

    /** Number of shots this slug stays locked after being fired. */
    public int reloadShots(int blasterDefault) {
        return reloadShotsOverride >= 0 ? reloadShotsOverride : blasterDefault;
    }

    /** Copy with a different dense index (assigned by the loader once every slug is known). */
    public SlugDefinition withIndex(int newIndex) {
        return new SlugDefinition(id, newIndex, name, element, role, targeting, projectile, fireDelayMultiplier,
                reloadShotsOverride, pierceWorld, botWeight, onHit, onWorldHit, onTick, onExpire);
    }

    /** Starts a builder with sensible defaults. */
    public static Builder builder(String id) {
        return new Builder(id);
    }

    /** Fluent builder for {@link SlugDefinition}. */
    public static final class Builder {
        private final String id;
        private String name;
        private Element element = Element.NEUTRAL;
        private SlugRole role = SlugRole.DAMAGE;
        private TargetingRule targeting = TargetingRule.ENEMIES;
        private ProjectileSpec projectile = ProjectileSpec.standard();
        private double fireDelayMultiplier = 1.0;
        private int reloadShotsOverride = -1;
        private boolean pierceWorld;
        private double botWeight = 1.0;
        private Effect onHit = Effect.NONE;
        private Effect onWorldHit = Effect.NONE;
        private Effect onTick = Effect.NONE;
        private Effect onExpire = Effect.NONE;

        private Builder(String id) {
            this.id = id;
            this.name = id;
        }

        public Builder name(String value) {
            this.name = value;
            return this;
        }

        public Builder element(Element value) {
            this.element = value;
            return this;
        }

        public Builder role(SlugRole value) {
            this.role = value;
            return this;
        }

        public Builder targeting(TargetingRule value) {
            this.targeting = value;
            return this;
        }

        public Builder projectile(ProjectileSpec value) {
            this.projectile = value;
            return this;
        }

        public Builder fireDelayMultiplier(double value) {
            this.fireDelayMultiplier = value;
            return this;
        }

        public Builder reloadShotsOverride(int value) {
            this.reloadShotsOverride = value;
            return this;
        }

        public Builder pierceWorld(boolean value) {
            this.pierceWorld = value;
            return this;
        }

        public Builder botWeight(double value) {
            this.botWeight = value;
            return this;
        }

        public Builder onHit(Effect value) {
            this.onHit = value;
            return this;
        }

        public Builder onWorldHit(Effect value) {
            this.onWorldHit = value;
            return this;
        }

        public Builder onTick(Effect value) {
            this.onTick = value;
            return this;
        }

        public Builder onExpire(Effect value) {
            this.onExpire = value;
            return this;
        }

        public SlugDefinition build() {
            return new SlugDefinition(id, 0, name, element, role, targeting, projectile, fireDelayMultiplier,
                    reloadShotsOverride, pierceWorld, botWeight, onHit, onWorldHit, onTick, onExpire);
        }
    }
}
