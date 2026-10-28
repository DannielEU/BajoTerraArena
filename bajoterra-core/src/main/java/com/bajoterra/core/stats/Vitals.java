package com.bajoterra.core.stats;

/**
 * Health, shield and temporary overshield of a combatant.
 *
 * <p>Damage consumes the overshield first, then the shield, and only the overflow reaches health. Piercing damage
 * skips the overshield and the shield. A combatant is defeated when all of them are exhausted.
 */
public final class Vitals {

    /** Remainders below this are rounding noise and count as empty, so floating point cannot keep a corpse alive. */
    private static final double EPSILON = 1e-9;

    private final double maxHp;
    private final double maxShield;
    private double hp;
    private double shield;
    private double overshield;
    private double overshieldDecayPerTick;

    public Vitals(double maxHp, double maxShield) {
        if (!(maxHp > 0) || !(maxShield >= 0)) {
            throw new IllegalArgumentException("Invalid vitals: hp=" + maxHp + " shield=" + maxShield);
        }
        this.maxHp = maxHp;
        this.maxShield = maxShield;
        reset();
    }

    /** Restores health and shield to their maximum and clears any overshield. */
    public void reset() {
        hp = maxHp;
        shield = maxShield;
        overshield = 0;
        overshieldDecayPerTick = 0;
    }

    /**
     * Applies damage.
     *
     * @param amount raw damage (ignored when not positive or when already defeated)
     * @param shieldPierce fraction in [0, 1] of the damage that bypasses overshield and shield
     * @return the damage effectively absorbed by overshield, shield and health combined
     */
    public double applyDamage(double amount, double shieldPierce) {
        if (!(amount > 0) || isDefeated()) {
            return 0;
        }
        double piercing = amount * Math.min(1.0, Math.max(0.0, shieldPierce));
        double blockable = amount - piercing;
        double dealt = 0;

        double fromOvershield = Math.min(overshield, blockable);
        overshield -= fromOvershield;
        blockable -= fromOvershield;
        dealt += fromOvershield;

        double fromShield = Math.min(shield, blockable);
        shield -= fromShield;
        blockable -= fromShield;
        dealt += fromShield;

        double fromHealth = Math.min(hp, blockable + piercing);
        hp -= fromHealth;
        dealt += fromHealth;
        overshield = snap(overshield);
        shield = snap(shield);
        hp = snap(hp);
        return dealt;
    }

    /** Heals up to the maximum health. Returns the amount actually healed. Defeated combatants cannot be healed. */
    public double heal(double amount) {
        if (!(amount > 0) || isDefeated()) {
            return 0;
        }
        double before = hp;
        hp = Math.min(maxHp, hp + amount);
        return hp - before;
    }

    /** Restores shield up to the maximum. Returns the amount actually restored. */
    public double restoreShield(double amount) {
        if (!(amount > 0) || isDefeated()) {
            return 0;
        }
        double before = shield;
        shield = Math.min(maxShield, shield + amount);
        return shield - before;
    }

    /**
     * Adds a temporary overshield that decays linearly to zero over {@code durationTicks}. A stronger or fresher
     * overshield replaces a weaker one; it never stacks beyond the new amount.
     */
    public void addOvershield(double amount, int durationTicks) {
        if (!(amount > 0) || durationTicks <= 0 || isDefeated()) {
            return;
        }
        if (amount >= overshield) {
            overshield = amount;
            overshieldDecayPerTick = amount / durationTicks;
        }
    }

    /** Advances the overshield decay by one tick. */
    public void tick() {
        if (overshield > 0) {
            overshield = Math.max(0, overshield - overshieldDecayPerTick);
        }
    }

    private static double snap(double value) {
        return value < EPSILON ? 0 : value;
    }

    /** True when overshield, shield and health are all exhausted. */
    public boolean isDefeated() {
        return hp <= 0 && shield <= 0 && overshield <= 0;
    }

    public double hp() {
        return hp;
    }

    public double maxHp() {
        return maxHp;
    }

    public double shield() {
        return shield;
    }

    public double maxShield() {
        return maxShield;
    }

    public double overshield() {
        return overshield;
    }

    /** Combined health and shield as a fraction of their combined maximum, in [0, 1]. */
    public double totalFraction() {
        return (hp + shield) / (maxHp + maxShield);
    }

    public double hpFraction() {
        return hp / maxHp;
    }
}
