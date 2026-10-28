package com.bajoterra.core.testext;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.move.MoveBehavior;
import com.bajoterra.core.move.MoveBehaviorFactory;
import com.bajoterra.core.move.MoveRunner;
import com.google.auto.service.AutoService;

/** A new move behavior defined outside the core: a long forward leap. */
public final class LeapBehavior implements MoveBehavior {

    private final double vertical;
    private final double horizontal;

    public LeapBehavior(double vertical, double horizontal) {
        this.vertical = vertical;
        this.horizontal = horizontal;
    }

    @Override
    public void onStart(Combatant self, MoveRunner runner, CombatArena arena) {
        self.vel.y = vertical;
        self.grounded = false;
        self.knock.x += runner.dirX() * horizontal;
        self.knock.z += runner.dirZ() * horizontal;
    }

    /** Registered through the service loader. */
    @AutoService(MoveBehaviorFactory.class)
    public static final class Factory implements MoveBehaviorFactory {
        @Override
        public String type() {
            return "test_leap";
        }

        @Override
        public MoveBehavior create(Spec spec) {
            return new LeapBehavior(spec.requireDouble("vertical", 0, 30), spec.requireDouble("horizontal", 0, 60));
        }
    }
}
