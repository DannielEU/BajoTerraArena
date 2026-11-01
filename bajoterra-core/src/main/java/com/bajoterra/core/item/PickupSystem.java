package com.bajoterra.core.item;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.slug.TargetingRule;
import java.util.List;

/** Lets alive combatants collect available pickups and respawns them after their cooldown. */
public final class PickupSystem {

    private final CombatArena arena;
    private final List<Pickup> pickups;
    private final EffectContext context = new EffectContext();

    public PickupSystem(CombatArena arena, List<Pickup> pickups) {
        this.arena = arena;
        this.pickups = pickups;
    }

    public List<Pickup> pickups() {
        return pickups;
    }

    /**
     * Makes every pickup available again (start of a round), except those that first show up some way into the
     * round (the megamorph crystal), which are scheduled instead.
     */
    public void resetAll() {
        for (Pickup pickup : pickups) {
            int delay = pickup.definition.firstSpawnTicks();
            pickup.available = delay == 0;
            pickup.respawnTick = arena.tick() + delay;
        }
    }

    public void update() {
        int tick = arena.tick();
        for (Pickup pickup : pickups) {
            if (!pickup.available) {
                if (tick >= pickup.respawnTick) {
                    pickup.available = true;
                    GameEvent event = arena.events().begin(EventType.PICKUP_RESPAWNED, tick);
                    event.a = pickup.index;
                }
                continue;
            }
            PickupDefinition definition = pickup.definition;
            // When two combatants reach a pickup on the same tick, who gets it rotates instead of favouring low ids.
            Combatant[] all = arena.combatants();
            int offset = tick % all.length;
            for (int k = 0; k < all.length; k++) {
                Combatant c = all[(k + offset) % all.length];
                if (!c.alive || !definition.requirement().satisfiedBy(c)) {
                    continue;
                }
                double dx = c.pos.x - pickup.pos.x;
                double dz = c.pos.z - pickup.pos.z;
                double dy = Math.abs(c.pos.y - pickup.pos.y);
                if (dx * dx + dz * dz <= definition.radius() * definition.radius() && dy < 1.5) {
                    collect(pickup, c);
                    break;
                }
            }
        }
    }

    private void collect(Pickup pickup, Combatant collector) {
        pickup.available = false;
        pickup.respawnTick = arena.tick() + pickup.definition.respawnTicks();
        context.set(arena, null, collector, null, TargetingRule.ANY, pickup.pos, null);
        pickup.definition.effect().apply(context);
        GameEvent event = arena.events().begin(EventType.PICKUP_TAKEN, arena.tick());
        event.subject = collector.id;
        event.a = pickup.index;
    }
}
