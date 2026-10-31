package com.bajoterra.core.event;

/** Kinds of gameplay events published by the simulation (consumed by snapshots, stats, bots and logs). */
public enum EventType {
    /**
     * A slug was fired. actor = shooter, a = slug index, b = loadout slot, xyz = muzzle, value = ticks until the
     * shooter's blaster may fire again.
     */
    SHOT,
    /**
     * A projectile hit a combatant. actor = shooter, subject = victim, a = slug index, b = 1 for a headshot,
     * xyz = impact point. A sticky slug stays stuck to the victim afterwards.
     */
    PROJECTILE_HIT,
    /**
     * A projectile hit an obstacle (a sticky slug stays stuck there). actor = shooter, a = slug index,
     * xyz = impact point.
     */
    PROJECTILE_WORLD_HIT,
    /**
     * A projectile reached the end of its lifetime (for a stuck sticky slug: its fuse burnt out). actor = shooter,
     * a = slug index, xyz = position.
     */
    PROJECTILE_EXPIRED,
    /** Damage was applied. actor = source, subject = victim, a = element ordinal, b = 1 if critical, value = damage. */
    DAMAGE,
    /** Health was restored. actor = source, subject = target, value = amount healed. */
    HEAL,
    /** Shield was restored. actor = source, subject = target, value = amount restored. */
    SHIELD_RESTORED,
    /** A temporary overshield was granted. actor = source, subject = target, value = amount. */
    OVERSHIELD,
    /** A status effect was applied. actor = source, subject = target, a = status hash. */
    STATUS_APPLIED,
    /** A status effect ended. subject = target, a = status hash. */
    STATUS_EXPIRED,
    /** A combatant was defeated. actor = killer (or -1), subject = victim. */
    DEFEATED,
    /** A pickup was collected. subject = collector, a = pickup index. */
    PICKUP_TAKEN,
    /** A pickup became available again. a = pickup index. */
    PICKUP_RESPAWNED,
    /** A move started. actor = combatant, a = move index. */
    MOVE_STARTED,
    /** A move ended. actor = combatant, a = move index. */
    MOVE_ENDED,
    /** Knockback was applied. subject = target, value = force. */
    KNOCKBACK,
    /** An area effect was triggered. actor = source, a = slug index (-1 for none), xyz = centre, value = radius. */
    EXPLOSION,
    /** A round started. a = round number. */
    ROUND_STARTED,
    /** A round ended. a = winning team (-1 for a draw), b = round number. */
    ROUND_ENDED,
    /** The match ended. a = winning team (-1 for a draw). */
    MATCH_ENDED,
    /**
     * A combatant's courage changed because their team gained or lost the numbers advantage. subject = combatant,
     * a = new courage level (0 = none), b = previous level, value = shield restored.
     */
    COURAGE,
    /**
     * A blinding flash went off (Phosphoro). actor = source, a = slug index (-1 for none), xyz = centre,
     * value = the radius within which it can be seen. Whoever saw it gets a {@code blind} status (STATUS_APPLIED).
     */
    FLASH,
    /**
     * A wall was raised (an ice wall). actor = who raised it, a = slug index, b = barrier id, xyz = middle of its
     * foot, value = hits it can take.
     */
    BARRIER_RAISED,
    /** A slug struck a wall. actor = shooter, a = hits it can still take, b = barrier id, xyz = impact point. */
    BARRIER_HIT,
    /** A wall is gone. a = 1 when it was broken, 0 when it melted; b = barrier id, xyz = middle of its foot. */
    BARRIER_GONE
}
