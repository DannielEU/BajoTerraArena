package com.bajoterra.core.combat;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.slug.Element;
import java.util.SplittableRandom;

/**
 * The part of the world effects can act on: the combatants, the event stream, the damage pipeline, the
 * deterministic random source and the clock. It is deliberately independent of maps and projectiles so effects and
 * combat rules can be unit-tested in isolation; {@code GameSimulation} composes it with the rest of the world.
 */
public final class CombatArena {

    private final Combatant[] combatants;
    private final DamagePipeline pipeline;
    private final EventBuffer events;
    private final SplittableRandom random;
    private final DamageEvent scratch = new DamageEvent();
    private SightLine sight = SightLine.OPEN;
    private Structures structures = Structures.NONE;
    private int tick;

    public CombatArena(Combatant[] combatants, DamagePipeline pipeline, EventBuffer events, long seed) {
        this.combatants = combatants;
        this.pipeline = pipeline;
        this.events = events;
        this.random = new SplittableRandom(seed);
    }

    public int tick() {
        return tick;
    }

    public void advanceTick() {
        tick++;
    }

    public Combatant[] combatants() {
        return combatants;
    }

    /** The combatant with the given id, or null for -1 / out-of-range ids (environmental sources). */
    public Combatant byId(int id) {
        return id >= 0 && id < combatants.length ? combatants[id] : null;
    }

    public EventBuffer events() {
        return events;
    }

    public SplittableRandom random() {
        return random;
    }

    /** What blocks sight in the arena; open ground until the simulation plugs its world in. */
    public SightLine sight() {
        return sight;
    }

    public void setSight(SightLine newSight) {
        this.sight = newSight == null ? SightLine.OPEN : newSight;
    }

    /** What can be built in the arena (ice walls); nothing until the simulation plugs its world in. */
    public Structures structures() {
        return structures;
    }

    public void setStructures(Structures newStructures) {
        this.structures = newStructures == null ? Structures.NONE : newStructures;
    }

    /** Number of alive combatants in a team. */
    public int aliveCount(int team) {
        int count = 0;
        for (Combatant c : combatants) {
            if (c.team == team && c.alive) {
                count++;
            }
        }
        return count;
    }

    /**
     * Deals damage through the pipeline.
     *
     * @return the damage effectively absorbed by the target's overshield, shield and health
     */
    public double damage(Combatant source, Combatant target, double amount, Element element, double shieldPierce) {
        return damage(source, target, amount, element, shieldPierce, false);
    }

    /**
     * Deals damage through the pipeline; {@code critical} only marks the {@code DAMAGE} event (b = 1) so clients can
     * show a headshot, the caller has already scaled the amount.
     *
     * @return the damage effectively absorbed by the target's overshield, shield and health
     */
    public double damage(Combatant source, Combatant target, double amount, Element element, double shieldPierce,
            boolean critical) {
        if (!target.alive || !(amount > 0)) {
            return 0;
        }
        DamageEvent event = scratch.set(source, target, amount, element, shieldPierce);
        pipeline.run(event);
        double dealt = target.vitals.applyDamage(event.amount, event.shieldPierce);
        if (dealt > 0) {
            target.damageTaken += dealt;
            if (source != null) {
                source.damageDealt += dealt;
            }
            GameEvent record = events.begin(EventType.DAMAGE, tick);
            record.actor = source == null ? -1 : source.id;
            record.subject = target.id;
            record.a = element.ordinal();
            record.b = critical ? 1 : 0;
            record.value = dealt;
        }
        if (target.vitals.isDefeated()) {
            defeat(target, source);
        }
        return dealt;
    }

    /** Heals a combatant, honouring their heal-received multiplier. Returns the amount healed. */
    public double heal(Combatant source, Combatant target, double amount) {
        if (!target.alive) {
            return 0;
        }
        double healed = target.vitals.heal(amount * target.stats.healReceivedMultiplier);
        if (healed > 0) {
            if (source != null) {
                source.healingDone += healed;
            }
            GameEvent record = events.begin(EventType.HEAL, tick);
            record.actor = source == null ? -1 : source.id;
            record.subject = target.id;
            record.value = healed;
        }
        return healed;
    }

    /** Restores shield, honouring the heal-received multiplier. Returns the amount restored. */
    public double restoreShield(Combatant source, Combatant target, double amount) {
        if (!target.alive) {
            return 0;
        }
        double restored = target.vitals.restoreShield(amount * target.stats.healReceivedMultiplier);
        if (restored > 0) {
            if (source != null) {
                source.healingDone += restored;
            }
            GameEvent record = events.begin(EventType.SHIELD_RESTORED, tick);
            record.actor = source == null ? -1 : source.id;
            record.subject = target.id;
            record.value = restored;
        }
        return restored;
    }

    /** Grants a decaying overshield. */
    public void grantOvershield(Combatant source, Combatant target, double amount, int durationTicks) {
        if (!target.alive) {
            return;
        }
        target.vitals.addOvershield(amount, durationTicks);
        GameEvent record = events.begin(EventType.OVERSHIELD, tick);
        record.actor = source == null ? -1 : source.id;
        record.subject = target.id;
        record.value = amount;
    }

    /**
     * Pushes a combatant away along a horizontal direction.
     *
     * @param dirX horizontal direction x (does not need to be normalised)
     * @param dirZ horizontal direction z
     * @param force impulse in m/s
     * @param lift upward velocity in m/s added on top
     */
    public void knockback(Combatant target, double dirX, double dirZ, double force, double lift) {
        if (!target.alive) {
            return;
        }
        double length = Math.sqrt(dirX * dirX + dirZ * dirZ);
        if (length > 1e-9) {
            target.knock.x += dirX / length * force;
            target.knock.z += dirZ / length * force;
        }
        if (lift > 0) {
            target.vel.y = Math.max(target.vel.y, lift);
            target.grounded = false;
        }
        GameEvent record = events.begin(EventType.KNOCKBACK, tick);
        record.subject = target.id;
        record.value = force;
    }

    private void defeat(Combatant victim, Combatant killer) {
        victim.alive = false;
        victim.deaths++;
        victim.statuses.clear();
        if (killer != null && killer != victim && killer.team != victim.team) {
            killer.kills++;
        }
        GameEvent record = events.begin(EventType.DEFEATED, tick);
        record.actor = killer == null ? -1 : killer.id;
        record.subject = victim.id;
        record.x = victim.pos.x;
        record.y = victim.pos.y;
        record.z = victim.pos.z;
    }
}
