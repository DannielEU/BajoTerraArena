package com.bajoterra.core.effect.builtin;

import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.effect.EffectFactory;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.entity.Combatant;
import com.google.auto.service.AutoService;
import java.util.List;

/** Removes every status carrying one of the given tags ({@code "cleanse"}), e.g. {@code ["burn"]}. */
public final class CleanseEffect implements Effect {

    private final List<String> tags;

    public CleanseEffect(List<String> tags) {
        this.tags = List.copyOf(tags);
    }

    @Override
    public void apply(EffectContext ctx) {
        Combatant target = ctx.target();
        if (target != null) {
            target.statuses.removeByTags(tags);
        }
    }

    /** Factory registered through the service loader. */
    @AutoService(EffectFactory.class)
    public static final class Factory implements EffectFactory {
        @Override
        public String type() {
            return "cleanse";
        }

        @Override
        public Effect create(Spec spec, EffectRegistry registry) {
            List<String> tags = spec.stringList("tags");
            if (tags.isEmpty()) {
                throw spec.fail("tags", "needs at least one tag");
            }
            return new CleanseEffect(tags);
        }
    }
}
