package com.bajoterra.core.move.builtin;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.move.MoveBehavior;
import com.bajoterra.core.move.MoveBehaviorFactory;
import com.bajoterra.core.move.MoveRunner;
import com.google.auto.service.AutoService;
import java.util.Map;

/**
 * One-off velocity change when the move starts ({@code "impulse"}): {@code vertical} m/s upwards (a jump) and
 * {@code horizontal} m/s along the move direction (a hop).
 */
public final class ImpulseBehavior implements MoveBehavior {

    private static final String TYPE = "impulse";

    private final double vertical;
    private final double horizontal;

    public ImpulseBehavior(double vertical, double horizontal) {
        this.vertical = vertical;
        this.horizontal = horizontal;
    }

    @Override
    public void onStart(Combatant self, MoveRunner runner, CombatArena arena) {
        if (vertical > 0) {
            self.vel.y = vertical;
            self.grounded = false;
        }
        self.knock.x += runner.dirX() * horizontal;
        self.knock.z += runner.dirZ() * horizontal;
    }

    @Override
    public String type() {
        return TYPE;
    }

    @Override
    public Map<String, Double> params() {
        return Map.of("vertical", vertical, "horizontal", horizontal);
    }

    /** Factory registered through the service loader. */
    @AutoService(MoveBehaviorFactory.class)
    public static final class Factory implements MoveBehaviorFactory {
        @Override
        public String type() {
            return TYPE;
        }

        @Override
        public MoveBehavior create(Spec spec) {
            return new ImpulseBehavior(spec.optDouble("vertical", 0, 0, 30), spec.optDouble("horizontal", 0, 0, 30));
        }
    }
}
