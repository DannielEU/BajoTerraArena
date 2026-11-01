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
 * A kick off a wall in mid-air ({@code "wallkick"}): pressing the button again while airborne with a wall within
 * reach of the body ({@link Combatant#wallNormalX}) throws it {@code vertical} m/s upwards and {@code push} m/s away
 * from the wall. At most {@code maxKicks} kicks before landing, and never twice in a row off the same wall (the next
 * kick needs a wall facing another way, so two facing walls can be climbed like a chimney).
 */
public final class WallKickBehavior implements MoveBehavior {

    private static final String TYPE = "wallkick";
    /** A wall whose normal is closer than this (cosine) to the last kick's counts as the same wall. */
    private static final double SAME_WALL = 0.5;

    private final double vertical;
    private final double push;
    private final int maxKicks;

    public WallKickBehavior(double vertical, double push, int maxKicks) {
        this.vertical = vertical;
        this.push = push;
        this.maxKicks = maxKicks;
    }

    @Override
    public boolean canStart(Combatant self, MoveRunner runner, boolean freshPress) {
        if (!freshPress || self.grounded || self.wallKicks >= maxKicks) {
            return false;
        }
        double nx = self.wallNormalX;
        double nz = self.wallNormalZ;
        if (nx == 0 && nz == 0) {
            return false;
        }
        return self.wallKicks == 0 || nx * self.lastKickX + nz * self.lastKickZ < SAME_WALL;
    }

    @Override
    public void onStart(Combatant self, MoveRunner runner, CombatArena arena) {
        self.vel.y = vertical;
        self.grounded = false;
        // The kick replaces whatever was pushing the body sideways: it always leaves the wall.
        self.knock.x = self.wallNormalX * push;
        self.knock.z = self.wallNormalZ * push;
        self.wallKicks++;
        self.lastKickX = self.wallNormalX;
        self.lastKickZ = self.wallNormalZ;
    }

    @Override
    public String type() {
        return TYPE;
    }

    @Override
    public Map<String, Double> params() {
        return Map.of("vertical", vertical, "push", push, "maxKicks", (double) maxKicks);
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
            return new WallKickBehavior(spec.optDouble("vertical", 8.0, 0, 30), spec.optDouble("push", 7.0, 0, 30),
                    spec.optInt("maxKicks", 2, 1, 5));
        }
    }
}
