package com.bajoterra.core.ai;

import com.bajoterra.core.entity.Combatant;

/** Chooses which visible enemy a bot attacks (Strategy). */
public interface TargetSelector {

    /** Returns the preferred target among the visible enemies, or null when none is visible. */
    Combatant select(BotContext context);

    /** Prefers the closest visible enemy. */
    TargetSelector NEAREST = context -> {
        Combatant best = null;
        double bestDistance = Double.MAX_VALUE;
        for (Combatant enemy : context.sim().combatants()) {
            if (context.isVisibleEnemy(enemy)) {
                double distance = enemy.pos.horizontalDistanceSq(context.self().pos);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = enemy;
                }
            }
        }
        return best;
    };

    /** Prefers the visible enemy with the least health and shield left, to finish kills. */
    TargetSelector WEAKEST = context -> {
        Combatant best = null;
        double lowest = Double.MAX_VALUE;
        for (Combatant enemy : context.sim().combatants()) {
            if (context.isVisibleEnemy(enemy)) {
                double health = enemy.vitals.totalFraction();
                if (health < lowest) {
                    lowest = health;
                    best = enemy;
                }
            }
        }
        return best;
    };
}
