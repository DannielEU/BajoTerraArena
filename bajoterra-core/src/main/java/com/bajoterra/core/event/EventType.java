package com.bajoterra.core.event;

/** Kinds of gameplay events published by the simulation (consumed by snapshots, stats, bots and logs). */
public enum EventType {
    /** A slug was fired. actor = shooter, a = slug index, b = projectile id, xyz = muzzle. */
    SHOT,
    /** A projectile hit a combatant. actor = shooter, subject = victim, a = slug index, xyz = impact point. */
    PROJECTILE_HIT,
    /** A projectile hit an obstacle. actor = shooter, a = slug index, xyz = impact point. */
    PROJECTILE_WORLD_HIT,
    /** A projectile reached the end of its lifetime. actor = shooter, a = slug index, xyz = position. */
    PROJECTILE_EXPIRED,
    /** Damage was applied. actor = source, subject = victim, value = damage dealt. */
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
    /** An area effect was triggered. actor = source, xyz = centre, value = radius. */
    EXPLOSION,
    /** A round started. a = round number. */
    ROUND_STARTED,
    /** A round ended. a = winning team (-1 for a draw), b = round number. */
    ROUND_ENDED,
    /** The match ended. a = winning team (-1 for a draw). */
    MATCH_ENDED
}
