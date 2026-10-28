package com.bajoterra.core.effect;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;

/**
 * Mutable, reusable context handed to {@link Effect#apply}. Compound effects (area, chain) temporarily change the
 * target and scale while they delegate to their children and restore them afterwards, so applying effects never
 * allocates.
 */
public final class EffectContext {

    private final Vec3 point = new Vec3();
    private final Vec3 direction = new Vec3();
    private CombatArena arena;
    private Combatant source;
    private Combatant target;
    private SlugDefinition slug;
    private TargetingRule targeting = TargetingRule.ANY;
    private double scale = 1.0;

    /**
     * (Re)initialises the context.
     *
     * @param source who caused the effect (the shooter, or the collector for pickups); may be null
     * @param target the directly affected combatant, or null for world/area effects
     * @param slug the slug being applied, or null for pickups
     * @param pointAt where it happens
     * @param directionAt travel direction of the projectile, or null
     */
    public EffectContext set(CombatArena newArena, Combatant newSource, Combatant newTarget, SlugDefinition newSlug,
            TargetingRule newTargeting, Vec3 pointAt, Vec3 directionAt) {
        this.arena = newArena;
        this.source = newSource;
        this.target = newTarget;
        this.slug = newSlug;
        this.targeting = newTargeting;
        this.scale = 1.0;
        this.point.set(pointAt);
        if (directionAt != null) {
            this.direction.set(directionAt);
        } else {
            this.direction.set(0, 0, 1);
        }
        return this;
    }

    public CombatArena arena() {
        return arena;
    }

    public Combatant source() {
        return source;
    }

    public Combatant target() {
        return target;
    }

    public void setTarget(Combatant newTarget) {
        this.target = newTarget;
    }

    public SlugDefinition slug() {
        return slug;
    }

    public TargetingRule targeting() {
        return targeting;
    }

    /** Where the effect happens (impact point, expiry point or pickup position). */
    public Vec3 point() {
        return point;
    }

    /** Travel direction of the projectile that caused the effect (unit vector, +Z when unknown). */
    public Vec3 direction() {
        return direction;
    }

    /** Magnitude multiplier set by compound effects (falloff, chain decay). */
    public double scale() {
        return scale;
    }

    public void setScale(double newScale) {
        this.scale = newScale;
    }

    /** Healing/protection potency of whoever caused the effect (1 when there is no source). */
    public double sourcePotency() {
        return source == null ? 1.0 : source.character.healPotency();
    }
}
