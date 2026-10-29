package com.bajoterra.core.ai;

/**
 * Difficulty knobs of a bot, derived from a single skill value.
 *
 * @param aimNoise random aim error in radians, re-rolled each decision
 * @param turnRate maximum aim rotation speed in rad/s
 * @param dodgeChance probability of rolling away from an incoming slug
 * @param leadsTargets whether the bot predicts target movement
 * @param selector how the bot picks its target
 * @param retreatHealth health fraction below which the bot seeks cover
 * @param memoryTicks ticks a bot keeps hunting an enemy it lost sight of
 * @param reactionTicks ticks between spotting a new target and the first shot at it (aiming starts at once)
 */
public record BotProfile(
        double aimNoise,
        double turnRate,
        double dodgeChance,
        boolean leadsTargets,
        TargetSelector selector,
        double retreatHealth,
        int memoryTicks,
        int reactionTicks) {

    /** Builds a profile for a skill in [0, 1]; out-of-range values are clamped. */
    public static BotProfile fromSkill(double skill) {
        double s = clampSkill(skill);
        return new BotProfile(
                0.14 * (1.0 - s) + 0.015,
                3.0 + 6.0 * s,
                0.8 * s,
                s >= 0.35,
                s >= 0.6 ? TargetSelector.WEAKEST : TargetSelector.NEAREST,
                0.25 + 0.2 * s,
                (int) (120 + 240 * s),
                (int) Math.round(30 - 18 * s));
    }

    /** A skill value clamped to [0, 1]. */
    public static double clampSkill(double skill) {
        return Math.min(1.0, Math.max(0.0, skill));
    }
}
