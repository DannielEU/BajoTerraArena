package com.bajoterra.core.effect;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.stats.StatBlock;
import java.util.List;

/** The status effects currently affecting one combatant, stored in a fixed array of reusable instances. */
public final class StatusSet {

    /** Maximum number of simultaneous statuses; when full the one closest to expiring is replaced. */
    public static final int CAPACITY = 8;

    private final StatusInstance[] slots = new StatusInstance[CAPACITY];

    public StatusSet() {
        for (int i = 0; i < CAPACITY; i++) {
            slots[i] = new StatusInstance();
        }
    }

    /** The instance in the given slot; check {@link StatusInstance#active()} before using it. */
    public StatusInstance at(int slot) {
        return slots[slot];
    }

    public boolean has(String statusId) {
        return find(statusId) != null;
    }

    /** Attaches {@code definition} to {@code ctx.target()} following its stacking policy. */
    void apply(AbstractStatusEffect definition, EffectContext ctx) {
        Combatant owner = ctx.target();
        int sourceId = ctx.source() == null ? -1 : ctx.source().id;
        StatusInstance instance = find(definition.statusId());
        if (instance != null) {
            switch (definition.stacking()) {
                case IGNORE -> {
                    return;
                }
                case REPLACE_IF_STRONGER -> {
                    if (definition.strength() <= instance.definition().strength()) {
                        return;
                    }
                    instance.refresh(definition, definition.durationTicks(), sourceId);
                }
                case STACK -> {
                    instance.addStack(definition.maxStacks());
                    instance.refresh(definition, definition.durationTicks(), sourceId);
                }
                case REFRESH -> instance.refresh(definition, definition.durationTicks(), sourceId);
                default -> throw new IllegalStateException("Unhandled stacking policy");
            }
        } else {
            instance = freeSlot();
            instance.start(definition, definition.durationTicks(), sourceId);
        }
        definition.onApply(instance, ctx);
        CombatArena arena = ctx.arena();
        GameEvent event = arena.events().begin(EventType.STATUS_APPLIED, arena.tick());
        event.actor = sourceId;
        event.subject = owner.id;
        event.a = definition.statusId().hashCode();
        event.value = definition.durationTicks();
    }

    /** Advances every status by one tick: periodic hooks first, then expiry. */
    public void tick(Combatant owner, CombatArena arena) {
        for (StatusInstance instance : slots) {
            if (!instance.active()) {
                continue;
            }
            AbstractStatusEffect definition = instance.definition();
            int age = instance.advance();
            int period = definition.periodTicks();
            if (period > 0 && age % period == 0) {
                definition.onPeriodic(instance, owner, arena);
            }
            if (instance.remainingTicks() <= 0) {
                definition.onExpire(instance, owner, arena);
                GameEvent event = arena.events().begin(EventType.STATUS_EXPIRED, arena.tick());
                event.subject = owner.id;
                event.a = definition.statusId().hashCode();
                instance.stop();
            }
        }
    }

    /** Lets every active status modify the per-tick stats. */
    public void modify(StatBlock stats) {
        for (StatusInstance instance : slots) {
            if (instance.active()) {
                instance.definition().modify(instance, stats);
            }
        }
    }

    /** Removes every status that carries at least one of the tags. Returns how many were removed. */
    public int removeByTags(List<String> tags) {
        int removed = 0;
        for (StatusInstance instance : slots) {
            if (!instance.active()) {
                continue;
            }
            for (String tag : tags) {
                if (instance.definition().hasTag(tag)) {
                    instance.stop();
                    removed++;
                    break;
                }
            }
        }
        return removed;
    }

    public void clear() {
        for (StatusInstance instance : slots) {
            instance.stop();
        }
    }

    private StatusInstance find(String statusId) {
        for (StatusInstance instance : slots) {
            if (instance.active() && instance.definition().statusId().equals(statusId)) {
                return instance;
            }
        }
        return null;
    }

    private StatusInstance freeSlot() {
        StatusInstance weakest = slots[0];
        for (StatusInstance instance : slots) {
            if (!instance.active()) {
                return instance;
            }
            if (instance.remainingTicks() < weakest.remainingTicks()) {
                weakest = instance;
            }
        }
        weakest.stop();
        return weakest;
    }
}
