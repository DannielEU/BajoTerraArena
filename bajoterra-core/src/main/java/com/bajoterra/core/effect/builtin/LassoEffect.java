package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.AbstractStatusEffect;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.effect.StatusInstance;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.stats.StatBlock;
import com.google.auto.service.AutoService;

/**
 * Lariat's energy lasso ({@code "lasso"}): it catches the target and hauls them towards whoever threw it, stopping
 * about {@code keep} metres short, with a little {@code lift} so they clear low cover (walls still stop them). While
 * the rope is taut they cannot walk, jump or roll, but they can still aim and fire. The pull is a knockback sized to
 * the distance (knockback fades by {@link #KNOCK_DECAY} a tick, so it carries about {@code speed / 4.8} metres),
 * never faster than {@code maxPull} m/s.
 */
public final class LassoEffect extends AbstractStatusEffect {

    /** Fraction of a knockback left after each tick (see MovementSystem). */
    private static final double KNOCK_DECAY = 0.92;
    /** Metres a knockback of 1 m/s carries a body before it fades out. */
    private static final double METRES_PER_SPEED = SimulationConstants.TICK_SECONDS / (1.0 - KNOCK_DECAY);

    private final double keep;
    private final double maxPull;
    private final double lift;

    public LassoEffect(Params params, double keep, double maxPull, double lift) {
        super(params);
        this.keep = keep;
        this.maxPull = maxPull;
        this.lift = lift;
    }

    @Override
    public double strength() {
        return 1.0;
    }

    @Override
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        Combatant target = ctx.target();
        Combatant thrower = ctx.source();
        if (thrower == null || thrower == target) {
            return;
        }
        double dx = thrower.pos.x - target.pos.x;
        double dz = thrower.pos.z - target.pos.z;
        double distance = Math.sqrt(dx * dx + dz * dz);
        double speed = Math.min(maxPull, Math.max(0, distance - keep) / METRES_PER_SPEED);
        CombatArena arena = ctx.arena();
        if (speed > 0) {
            // Cancel whatever was pushing them first, so the rope always lands them in front of the thrower.
            target.knock.setZero();
            arena.knockback(target, dx, dz, speed, lift);
        }
        GameEvent event = arena.events().begin(EventType.LASSO, arena.tick());
        event.actor = thrower.id;
        event.subject = target.id;
        event.x = target.pos.x;
        event.y = target.pos.y;
        event.z = target.pos.z;
        event.value = speed;
    }

    @Override
    protected void modify(StatusInstance instance, StatBlock stats) {
        stats.rooted = true;
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "lasso";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            return new LassoEffect(Params.parse(spec, "lasso", 0, "lasso", "debuff", "cc"),
                    spec.optDouble("keep", 2.5, 0.5, 20),
                    spec.optDouble("maxPull", 55, 1, 120),
                    spec.optDouble("lift", 3.5, 0, 20));
        }
    }
}
