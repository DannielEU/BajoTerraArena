package com.bajoterra.core.sim;

import java.util.List;

/**
 * Setup of one participant.
 *
 * @param team 0 or 1
 * @param characterId id of a character definition
 * @param slugIds the ten slug ids of the loadout; null or empty asks the simulation to generate a balanced one
 * @param bot whether the participant is controlled by a bot brain (otherwise by network commands)
 * @param botSkill skill of the bot brain in [0, 1]; ignored for humans
 */
public record SlotConfig(int team, String characterId, List<String> slugIds, boolean bot, double botSkill) {

    public SlotConfig {
        if (team < 0 || team > 1) {
            throw new IllegalArgumentException("team must be 0 or 1");
        }
        slugIds = slugIds == null ? List.of() : List.copyOf(slugIds);
    }

    public static SlotConfig bot(int team, String characterId, double skill) {
        return new SlotConfig(team, characterId, List.of(), true, skill);
    }

    public static SlotConfig human(int team, String characterId, List<String> slugIds) {
        return new SlotConfig(team, characterId, slugIds, false, 0);
    }
}
