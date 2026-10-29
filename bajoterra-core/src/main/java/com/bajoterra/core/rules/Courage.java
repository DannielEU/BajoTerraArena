package com.bajoterra.core.rules;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;

/**
 * Comeback mechanic ("courage"): survivors of a team with fewer combatants standing than the other fight harder, so
 * losing a teammate does not decide the round on its own. Every tick each survivor gets the courage level the
 * {@link MatchRules} give for their team's disadvantage; each level makes their slugs hit harder, their blaster
 * recharge faster and them take less damage, and gaining a level restores part of their shield at once (a second
 * wind when a teammate falls). Tuned with headless matches where one member of a team plays far worse than the rest:
 * it turns rounds that were lost as soon as that member fell into rounds that can still be won.
 */
public final class Courage {

    /** Highest courage level (one combatant left against three). */
    public static final int MAX_LEVEL = 2;
    /** Extra damage dealt per level, as a fraction. */
    public static final double DAMAGE_BONUS_PER_LEVEL = 0.35;
    /** Extra fire rate per level, as a fraction (the interval between shots is divided by 1 + this). */
    public static final double FIRE_RATE_BONUS_PER_LEVEL = 0.35;
    /** Damage taken is reduced by this fraction per level. */
    public static final double DAMAGE_REDUCTION_PER_LEVEL = 0.2;
    /** Fraction of the maximum shield restored for every level gained. */
    public static final double SHIELD_BURST_FRACTION = 0.5;

    private final MatchRules rules;
    private final CombatArena arena;

    public Courage(MatchRules rules, CombatArena arena) {
        this.rules = rules;
        this.arena = arena;
    }

    /** Recomputes every survivor's level from the current head count. */
    public void update() {
        int alive0 = arena.aliveCount(0);
        int alive1 = arena.aliveCount(1);
        for (Combatant c : arena.combatants()) {
            if (!c.alive) {
                continue;
            }
            int own = c.team == 0 ? alive0 : alive1;
            int enemy = c.team == 0 ? alive1 : alive0;
            int level = rules.courageLevel(own, enemy);
            if (level != c.courage) {
                change(c, level);
            }
        }
    }

    private void change(Combatant c, int level) {
        double restored = 0;
        if (level > c.courage) {
            restored = arena.restoreShield(null, c,
                    c.character.maxShield() * SHIELD_BURST_FRACTION * (level - c.courage));
        }
        GameEvent event = arena.events().begin(EventType.COURAGE, arena.tick());
        event.subject = c.id;
        event.a = level;
        event.b = c.courage;
        event.value = restored;
        c.courage = level;
    }

    /** Applies a combatant's courage to this tick's stats (after the statuses, which reset them). */
    public static void modify(Combatant c) {
        if (c.courage > 0) {
            c.stats.damageDealtMultiplier *= 1.0 + DAMAGE_BONUS_PER_LEVEL * c.courage;
            c.stats.fireIntervalMultiplier /= 1.0 + FIRE_RATE_BONUS_PER_LEVEL * c.courage;
            c.stats.damageTakenMultiplier *= 1.0 - DAMAGE_REDUCTION_PER_LEVEL * c.courage;
        }
    }
}
