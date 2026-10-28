package com.bajoterra.core.move;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.math.Trig;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.world.World;

/** Integrates combatant movement: input steering, gravity, knockback decay and collision with the world. */
public final class MovementSystem {

    /** Downward acceleration in m/s^2 (a bit above real gravity for a snappier jump). */
    public static final double GRAVITY = 25.0;

    private static final double KNOCK_DECAY_PER_TICK = 0.92;

    private final World world;

    public MovementSystem(World world) {
        this.world = world;
    }

    /** Moves one combatant for one tick. Dead combatants only fall to the ground. */
    public void integrate(Combatant c) {
        double dt = SimulationConstants.TICK_SECONDS;
        MoveRunner runner = c.moves;
        double vx = 0;
        double vz = 0;
        if (c.alive && !c.stats.stunned) {
            if (runner.isDriven()) {
                vx = runner.drivenX();
                vz = runner.drivenZ();
            } else {
                double mx = c.input.moveX;
                double mz = c.input.moveZ;
                double length = Trig.hypot(mx, mz);
                if (length > 1.0) {
                    mx /= length;
                    mz /= length;
                }
                double speed = c.character.speed() * c.stats.speedMultiplier;
                double sin = Trig.sin(c.yaw);
                double cos = Trig.cos(c.yaw);
                // forward = (sin, cos), right = (cos, -sin)
                vx = (sin * mz + cos * mx) * speed;
                vz = (cos * mz - sin * mx) * speed;
            }
        }
        c.vel.x = vx;
        c.vel.z = vz;
        if (c.grounded && c.vel.y <= 0) {
            c.vel.y = 0;
        } else {
            c.vel.y -= GRAVITY * dt;
        }
        c.pos.x += (c.vel.x + c.knock.x) * dt;
        c.pos.y += c.vel.y * dt;
        c.pos.z += (c.vel.z + c.knock.z) * dt;
        c.knock.x *= KNOCK_DECAY_PER_TICK;
        c.knock.z *= KNOCK_DECAY_PER_TICK;
        world.resolveCombatant(c);
    }
}
