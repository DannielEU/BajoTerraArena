package com.bajoterra.core.sim;

import com.bajoterra.core.ai.BotBrain;
import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import java.util.List;

/**
 * Keeps matches between people and bots close (dynamic difficulty). After every round it makes the bots of the team
 * that won play worse and those of the team that lost play better, by more the more lopsided the round was, so
 * whoever is behind gets a hand whatever the player's own level. Only matches with at least one person are adjusted:
 * bot-only matches (balance runs, tests) keep their skills.
 */
final class BotDirector {

    /**
     * Skill moved per round, for a round won with a single survivor. Tuned with headless series of four matches in
     * one room, a bot standing in for the person at skills 0, 0.2, 0.4 and 0.7: they won 49 %, 49 %, 53 % and 55 % of
     * the matches (without the director: 11 %, 19 %, 30 % and 49 %). Bigger steps overshoot and swing between easy and
     * hard matches.
     */
    static final double STEP = 0.12;
    /** Extra skill moved for every other survivor of the winning team (a crushing round moves the bots further). */
    static final double STEP_PER_SURVIVOR = 0.05;
    static final double MIN_SKILL = 0.05;
    static final double MAX_SKILL = 0.95;

    private final Combatant[] combatants;
    /** The brain of each filler bot, by combatant id; null for people (and for bots that replaced a person). */
    private final BotBrain[] bots;
    private final boolean active;

    BotDirector(List<SlotConfig> slots, Combatant[] combatants) {
        this.combatants = combatants;
        this.bots = new BotBrain[combatants.length];
        for (int i = 0; i < combatants.length; i++) {
            if (slots.get(i).bot()) {
                bots[i] = (BotBrain) combatants[i].brain;
            }
        }
        this.active = slots.stream().anyMatch(slot -> !slot.bot());
    }

    /** Average skill of the bots of a team, or NaN when it has none. */
    double averageSkill(int team) {
        double total = 0;
        int count = 0;
        for (int i = 0; i < combatants.length; i++) {
            if (bots[i] != null && combatants[i].team == team) {
                total += bots[i].skill();
                count++;
            }
        }
        return count == 0 ? Double.NaN : total / count;
    }

    /** Called when a round is over, before the next one places the combatants again (or the match ends). */
    void afterRound(int winner, CombatArena arena) {
        if (!active || winner < 0) {
            return;
        }
        int survivors = arena.aliveCount(winner);
        double step = STEP + STEP_PER_SURVIVOR * Math.max(0, survivors - 1);
        for (int i = 0; i < combatants.length; i++) {
            if (bots[i] != null) {
                double delta = combatants[i].team == winner ? -step : step;
                bots[i].setSkill(Math.max(MIN_SKILL, Math.min(MAX_SKILL, bots[i].skill() + delta)));
            }
        }
    }
}
