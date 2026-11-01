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
 * A slide along the floor ({@code "slide"}): pressing the button while running on the ground drives the body along
 * the running direction, from {@code speed} m/s down to {@code endSpeed} m/s over the length of the move. Pressing
 * it while standing still (or keeping it held) does nothing, so the same key can also crouch.
 */
public final class SlideBehavior implements MoveBehavior {

    private static final String TYPE = "slide";
    /** How far the stick must be pushed (0..1) for the press to count as running. */
    public static final double MIN_INPUT = 0.5;

    private final double speed;
    private final double endSpeed;

    public SlideBehavior(double speed, double endSpeed) {
        this.speed = speed;
        this.endSpeed = endSpeed;
    }

    @Override
    public boolean canStart(Combatant self, MoveRunner runner, boolean freshPress) {
        double mx = self.input.moveX;
        double mz = self.input.moveZ;
        return freshPress && self.grounded && mx * mx + mz * mz >= MIN_INPUT * MIN_INPUT;
    }

    @Override
    public void onTick(Combatant self, MoveRunner runner, CombatArena arena) {
        double t = Math.min(1.0, runner.elapsed() / (double) Math.max(1, runner.active().durationTicks()));
        double now = speed + (endSpeed - speed) * t;
        runner.driveHorizontal(runner.dirX() * now, runner.dirZ() * now);
    }

    @Override
    public String type() {
        return TYPE;
    }

    @Override
    public Map<String, Double> params() {
        return Map.of("speed", speed, "endSpeed", endSpeed);
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
            double speed = spec.requireDouble("speed", 0.1, 60);
            return new SlideBehavior(speed, spec.optDouble("endSpeed", speed * 0.5, 0, 60));
        }
    }
}
