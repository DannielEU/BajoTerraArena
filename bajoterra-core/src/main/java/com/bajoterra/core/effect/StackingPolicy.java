package com.bajoterra.core.effect;

/** What happens when a status effect is applied to a combatant that already has it. */
public enum StackingPolicy {
    /** Restart the duration, keeping a single instance. */
    REFRESH,
    /** Add a stack (up to the effect's maximum) and restart the duration. */
    STACK,
    /** Replace the running instance only if the new one is stronger. */
    REPLACE_IF_STRONGER,
    /** Keep the running instance untouched. */
    IGNORE,
    /**
     * Keep whichever runs longer: the new application only replaces the running instance when it would last longer
     * (a strong blinding is not cut short by a weak one).
     */
    LONGEST
}
