package com.bajoterra.server;

import com.bajoterra.core.rules.EliminationRoundsRules;
import com.bajoterra.core.rules.MatchRules;

/**
 * Server settings.
 *
 * @param port TCP port; 0 picks a free one
 * @param realTime when false the scheduler ticks as fast as it can (tests and simulations)
 * @param rules match rules applied to new matches
 * @param seed base seed for the random sources of new matches
 * @param maxRooms upper bound on simultaneous rooms
 * @param mapId map new matches are played on
 */
public record ServerConfig(int port, boolean realTime, MatchRules rules, long seed, int maxRooms, String mapId) {

    public ServerConfig {
        if (port < 0 || port > 65_535 || maxRooms <= 0) {
            throw new IllegalArgumentException("Invalid server configuration");
        }
    }

    /** Real-time server on the given port with best-of-five rules. */
    public static ServerConfig defaults(int port) {
        return new ServerConfig(port, true, EliminationRoundsRules.bestOfFive(), 1L, 64, "caverna");
    }

    public ServerConfig withRealTime(boolean value) {
        return new ServerConfig(port, value, rules, seed, maxRooms, mapId);
    }

    public ServerConfig withRules(MatchRules value) {
        return new ServerConfig(port, realTime, value, seed, maxRooms, mapId);
    }

    public ServerConfig withMap(String value) {
        return new ServerConfig(port, realTime, rules, seed, maxRooms, value);
    }

    public ServerConfig withSeed(long value) {
        return new ServerConfig(port, realTime, rules, value, maxRooms, mapId);
    }
}
