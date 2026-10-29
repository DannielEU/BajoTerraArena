package com.bajoterra.core.sim;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.character.CharacterDefinition;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;

/** Completes both teams to {@link MatchConfig#TEAM_SIZE} members with bots and orders the slots by team. */
public final class BotFiller {

    /** Skill given to filler bots in matches without people, or with people on both teams. */
    public static final double DEFAULT_SKILL = 0.7;
    /** Skill of the bots that fill a team of people playing against bots. */
    public static final double ALLY_SKILL = 0.7;
    /** Skill of the bots that play against a team of people. */
    public static final double RIVAL_SKILL = 0.5;

    private BotFiller() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /**
     * Returns the participants arranged as team 0 first and team 1 second (combatant ids follow this order),
     * with random bot characters added until each team has three members (unless filling is disabled). When people
     * play against bots, their teammates and rivals get {@link MatchConfig#allySkill()} and
     * {@link MatchConfig#rivalSkill()} (by default {@link #ALLY_SKILL} and {@link #RIVAL_SKILL}).
     */
    public static List<SlotConfig> fill(MatchConfig config, Content content) {
        SplittableRandom random = new SplittableRandom(config.seed() ^ 0x5DEECE66DL);
        List<CharacterDefinition> roster = content.characters().all();
        List<SlotConfig> arranged = new ArrayList<>();
        boolean[] people = new boolean[2];
        for (SlotConfig slot : config.slots()) {
            people[slot.team()] |= !slot.bot();
        }
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
                arranged.add(SlotConfig.bot(team, character.id(), fillerSkill(config, people, team)));
                count++;
            }
        }
        return arranged;
    }

    /** People against bots get the configured ally and rival skills; otherwise every bot is equal. */
    static double fillerSkill(MatchConfig config, boolean[] people, int team) {
        if (people[0] == people[1]) {
            return DEFAULT_SKILL;
        }
        return people[team] ? config.allySkill() : config.rivalSkill();
    }
}
