package com.bajoterra.core.slug;

import com.bajoterra.core.entity.Combatant;

/**
 * Who a slug can affect. A projectile passes straight through combatants the rule does not accept (a healing slug
 * flies through enemies and only collides with allies or the world).
 */
public enum TargetingRule {
    /** Only combatants of the other team. */
    ENEMIES,
    /** Only teammates, excluding the shooter. */
    ALLIES,
    /** Teammates including the shooter. */
    ALLIES_AND_SELF,
    /** Everybody. */
    ANY;

    /** True when the slug can hit enemies (an attack), false when it only reaches the shooter's team (support). */
    public boolean reachesEnemies() {
        return this == ENEMIES || this == ANY;
    }

    /** True when {@code target} is a valid victim of a slug fired by {@code source}. Liveness is checked elsewhere. */
    public boolean accepts(Combatant source, Combatant target) {
        boolean sameTeam = source.team == target.team;
        return switch (this) {
            case ENEMIES -> !sameTeam;
            case ALLIES -> sameTeam && source != target;
            case ALLIES_AND_SELF -> sameTeam;
            case ANY -> true;
        };
    }
}
