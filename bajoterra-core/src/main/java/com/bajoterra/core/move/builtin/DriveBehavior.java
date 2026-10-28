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
 * Drives the horizontal movement at a fixed {@code speed} (m/s) along the direction captured at the start of the
 * move, ignoring steering input ({@code "drive"}). Rolls, dashes and slides are all data variations of it.
 */
public final class DriveBehavior implements MoveBehavior {

    private static final String TYPE = "drive";

    private final double speed;

    public DriveBehavior(double speed) {
        this.speed = speed;
    }

    @Override
    public void onTick(Combatant self, MoveRunner runner, CombatArena arena) {
        runner.driveHorizontal(runner.dirX() * speed, runner.dirZ() * speed);
    }

    @Override
    public String type() {
        return TYPE;
    }

    @Override
    public Map<String, Double> params() {
        return Map.of("speed", speed);
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
            return new DriveBehavior(spec.requireDouble("speed", 0.1, 60));
        }
    }
}
