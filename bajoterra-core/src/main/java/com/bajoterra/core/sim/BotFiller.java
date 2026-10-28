package com.bajoterra.core.sim;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.character.CharacterDefinition;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;

/** Completes both teams to {@link MatchConfig#TEAM_SIZE} members with bots and orders the slots by team. */
public final class BotFiller {

    /** Skill given to filler bots. */
    public static final double DEFAULT_SKILL = 0.7;

    private BotFiller() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * Returns the participants arranged as team 0 first and team 1 second (combatant ids follow this order),
     * with random bot characters added until each team has three members (unless filling is disabled).
     */
    public static List<SlotConfig> fill(MatchConfig config, Content content) {
        SplittableRandom random = new SplittableRandom(config.seed() ^ 0x5DEECE66DL);
        List<CharacterDefinition> roster = content.characters().all();
        List<SlotConfig> arranged = new ArrayList<>();
        for (int team = 0; team < 2; team++) {
            int count = 0;
            for (SlotConfig slot : config.slots()) {
                if (slot.team() == team) {
                    if (++count > MatchConfig.TEAM_SIZE) {
                        throw new IllegalArgumentException("Team " + team + " has more than "
                                + MatchConfig.TEAM_SIZE + " participants");
                    }
                    arranged.add(slot);
                }
            }
            while (config.fillWithBots() && count < MatchConfig.TEAM_SIZE) {
                CharacterDefinition character = roster.get(random.nextInt(roster.size()));
                arranged.add(SlotConfig.bot(team, character.id(), DEFAULT_SKILL));
                count++;
            }
        }
        return arranged;
    }
}
