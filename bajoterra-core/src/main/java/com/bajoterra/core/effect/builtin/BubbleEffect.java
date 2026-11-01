package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.effect.StatusSet;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.move.MovementSystem;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/**
 * Traps the target in Bubbaleone's bubble ({@code "bubble"}): they cannot move, fire or start moves, and the bubble
 * floats them up {@code height} metres at {@code rise} m/s and holds them there, out of their cover. The bubble is
 * fragile: the first slug or blast that hurts them after it formed pops it
 * ({@link com.bajoterra.core.effect.StatusSet#breakFragile}), and they drop back down free. Otherwise it lasts its
 * duration.
 */
public final class BubbleEffect extends AbstractStatusEffect {

    private final double height;
    private final double rise;

    public BubbleEffect(Params params, double height, double rise) {
        super(params);
        this.height = height;
        this.rise = rise;
    }

    @Override
    public double strength() {
        return 1.0;
    }

    @Override
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        Combatant target = ctx.target();
        // Where it floats to: measured from where it caught them (a fresh bubble on a floating one keeps the height).
        if (instance.value() == 0 || instance.value() < target.pos.y) {
            instance.setValue(target.pos.y + height);
        }
        target.grounded = false;
        CombatArena arena = ctx.arena();
        GameEvent event = arena.events().begin(EventType.BUBBLE, arena.tick());
        event.actor = ctx.source() == null ? -1 : ctx.source().id;
        event.subject = target.id;
        event.a = 1;
        event.x = target.pos.x;
        event.y = target.pos.y;
        event.z = target.pos.z;
    }

    @Override
    protected void onPeriodic(StatusInstance instance, Combatant owner, CombatArena arena) {
        // The next movement step takes one tick of gravity off: hand it back so the bubble rises evenly, then hovers.
        double hold = MovementSystem.GRAVITY * SimulationConstants.TICK_SECONDS;
        double below = instance.value() - owner.pos.y;
        double step = rise * SimulationConstants.TICK_SECONDS;
        owner.vel.y = (below > step ? rise : Math.max(0, below) * SimulationConstants.TICKS_PER_SECOND) + hold;
        owner.grounded = false;
        owner.knock.x *= 0.8;
        owner.knock.z *= 0.8;
    }

    @Override
    protected void onExpire(StatusInstance instance, Combatant owner, CombatArena arena) {
        owner.vel.y = 0;
        GameEvent event = arena.events().begin(EventType.BUBBLE, arena.tick());
        event.subject = owner.id;
        event.a = 2;
        event.x = owner.pos.x;
        event.y = owner.pos.y;
        event.z = owner.pos.z;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.stunned = true;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "bubble";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new BubbleEffect(Params.parse(spec, "bubble", 1, "bubble", StatusSet.FRAGILE, "debuff", "cc"),
                    spec.optDouble("height", 1.6, 0, 6),
                    spec.optDouble("rise", 2.2, 0.1, 20));
        }
    }
}
