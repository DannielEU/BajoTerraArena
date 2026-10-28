package com.bajoterra.core.effect;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.stats.StatBlock;
import java.util.List;

/**
 * Base class of effects that persist on a combatant for some time (Template Method). Applying the effect attaches a
 * {@link StatusInstance} to the target; the owning {@link StatusSet} then drives the lifecycle hooks
 * {@link #onApply}, {@link #onPeriodic}, {@link #modify} and {@link #onExpire}.
 *
 * <p>Subclasses are immutable flyweights; per-target state lives in the {@link StatusInstance}.
 */
public abstract class AbstractStatusEffect implements Effect {

    private final Params params;

    protected AbstractStatusEffect(Params params) {
        this.params = params;
    }

    /**
     * Common parameters of every status effect.
     *
     * @param statusId identifier shared by every instance of the same status ("burn", "slow", ...)
     * @param durationTicks how long the status lasts
     * @param stacking behaviour when applied to a target that already has it
     * @param maxStacks stack limit when {@code stacking} is {@link StackingPolicy#STACK}
     * @param periodTicks interval of {@link #onPeriodic}; 0 for statuses without periodic behaviour
     * @param tags labels used by cleanse effects ("debuff", "burn", ...)
     */
    public record Params(String statusId, int durationTicks, StackingPolicy stacking, int maxStacks,
            int periodTicks, List<String> tags) {

        public Params {
            if (statusId == null || statusId.isBlank() || durationTicks <= 0 || maxStacks < 1 || periodTicks < 0) {
                throw new IllegalArgumentException("Invalid status params for '" + statusId + "'");
            }
            tags = List.copyOf(tags);
        }

        /**
         * Reads the shared fields from a spec: {@code durationTicks} or {@code durationSeconds}, {@code stacking},
         * {@code maxStacks}.
         */
        public static Params parse(Spec spec, String statusId, int periodTicks, String... tags) {
            int duration = spec.requireDurationTicks();
            StackingPolicy policy = spec.optEnum("stacking", StackingPolicy.class, StackingPolicy.REFRESH);
            int maxStacks = spec.optInt("maxStacks", 3, 1, 20);
            return new Params(statusId, duration, policy, maxStacks, periodTicks, List.of(tags));
        }
    }

    @Override
    public final void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target == null || !target.alive) {
            return;
        }
        target.statuses.apply(this, ctx);
    }

    public final String statusId() {
        return params.statusId();
    }

    public final int durationTicks() {
        return params.durationTicks();
    }

    public final StackingPolicy stacking() {
        return params.stacking();
    }

    public final int maxStacks() {
        return params.maxStacks();
    }

    public final int periodTicks() {
        return params.periodTicks();
    }

    public final boolean hasTag(String tag) {
        return params.tags().contains(tag);
    }

    /** Strength used by {@link StackingPolicy#REPLACE_IF_STRONGER}; higher wins. */
    public double strength() {
        return 0;
    }

    /** Called when an instance is created or re-applied; typically stores a magnitude in {@code instance}. */
    protected void onApply(StatusInstance instance, EffectContext ctx) {
        // nothing by default
    }

    /** Called every {@link #periodTicks()} ticks while the status is active. */
    protected void onPeriodic(StatusInstance instance, Combatant owner, CombatArena arena) {
        // nothing by default
    }

    /** Called when the status runs out (not when it is removed by a cleanse). */
    protected void onExpire(StatusInstance instance, Combatant owner, CombatArena arena) {
        // nothing by default
    }

    /** Applies this status' modifiers to the combatant's per-tick stats. */
    protected void modify(StatusInstance instance, StatBlock stats) {
        // nothing by default
    }
}
