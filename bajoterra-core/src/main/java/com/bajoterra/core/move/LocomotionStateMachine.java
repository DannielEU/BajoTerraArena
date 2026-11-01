package com.bajoterra.core.move;

import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.entity.Posture;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Trig;
import java.util.List;

/**
 * Decides, each tick, which move is active for a combatant and applies its data modifiers (State pattern: the
 * current {@link Posture} gates which moves may start). All move behavior comes from {@link MoveDefinition}s, so a
 * new move is added by writing a JSON file and listing it in a character's move set.
 */
public final class LocomotionStateMachine {

    /**
     * Updates the active move of {@code c} and adds its modifiers to {@code c.stats}. Must run after
     * {@code c.stats.reset()} and the status modifiers, and before movement integration.
     */
    public void update(Combatant c, CombatArena arena) {
        MoveRunner runner = c.moves;
        runner.clearDrive();
        List<MoveDefinition> moveSet = c.character.moves();
        int mask = c.input.moveMask;
        if (c.grounded) {
            c.wallKicks = 0;
        }

        MoveDefinition active = runner.active();
        if (active != null && shouldEnd(c, runner, active, mask)) {
            end(c, runner, arena);
        }
        if (c.alive && !c.stats.stunned && !c.stats.rooted) {
            for (int slot = 0; slot < moveSet.size(); slot++) {
                if ((mask & (1 << slot)) != 0 && moveSet.get(slot) != runner.active()
                        && canStart(c, runner, moveSet.get(slot), slot, arena.tick())) {
                    start(c, runner, moveSet.get(slot), slot, arena);
                    break;
                }
            }
        }
        active = runner.active();
        if (active != null) {
            applyModifiers(c, runner, active);
            active.behavior().onTick(c, runner, arena);
            runner.advance();
        }
        c.posture = computePosture(c, active);
        runner.rememberMask(mask);
    }

    private static boolean shouldEnd(Combatant c, MoveRunner runner, MoveDefinition active, int mask) {
        if (c.stats.stunned || c.stats.rooted || !c.alive) {
            return true;
        }
        if (active.holdable()) {
            boolean released = (mask & (1 << runner.activeSlot())) == 0;
            return released || (active.stance() && !c.grounded);
        }
        return runner.elapsed() >= active.durationTicks();
    }

    private static boolean canStart(Combatant c, MoveRunner runner, MoveDefinition move, int slot, int tick) {
        if (tick < runner.cooldownUntil(slot)) {
            return false;
        }
        MoveDefinition active = runner.active();
        if (active != null && !active.interruptible()) {
            return false;
        }
        return (move.allowedFromMask() & c.posture.bit()) != 0
                && move.behavior().canStart(c, runner, (runner.lastMask() & (1 << slot)) == 0);
    }

    private static void start(Combatant c, MoveRunner runner, MoveDefinition move, int slot, CombatArena arena) {
        if (runner.active() != null) {
            end(c, runner, arena);
        }
        double dirX = Trig.sin(c.yaw);
        double dirZ = Trig.cos(c.yaw);
        double inputLength = Trig.hypot(c.input.moveX, c.input.moveZ);
        if (inputLength > 0.1) {
            double right = c.input.moveX / inputLength;
            double forward = c.input.moveZ / inputLength;
            // forward = (sin yaw, cos yaw); right = (cos yaw, -sin yaw)
            dirX = Trig.sin(c.yaw) * forward + Trig.cos(c.yaw) * right;
            dirZ = Trig.cos(c.yaw) * forward - Trig.sin(c.yaw) * right;
        }
        runner.begin(move, slot, arena.tick(), dirX, dirZ, !c.grounded && c.wallKicks > 0);
        GameEvent event = arena.events().begin(EventType.MOVE_STARTED, arena.tick());
        event.actor = c.id;
        event.a = move.index();
        move.behavior().onStart(c, runner, arena);
    }

    private static void end(Combatant c, MoveRunner runner, CombatArena arena) {
        MoveDefinition ended = runner.active();
        ended.behavior().onEnd(c, runner, arena);
        runner.finish();
        GameEvent event = arena.events().begin(EventType.MOVE_ENDED, arena.tick());
        event.actor = c.id;
        event.a = ended.index();
    }

    private static void applyModifiers(Combatant c, MoveRunner runner, MoveDefinition active) {
        c.stats.speedMultiplier *= active.speedMultiplier();
        c.stats.hitboxHeightMultiplier *= active.hitboxHeightMultiplier();
        if (runner.elapsed() >= active.invulnerableFromTick() && runner.elapsed() < active.invulnerableToTick()) {
            c.stats.invulnerable = true;
        }
    }

    private static Posture computePosture(Combatant c, MoveDefinition active) {
        if (active != null) {
            return active.stance() ? Posture.CROUCHING : Posture.ACTION;
        }
        return c.grounded ? Posture.GROUNDED : Posture.AIRBORNE;
    }

    /** Whether the active move forbids firing right now (a roll right after a wall kick still lets it fire). */
    public static boolean blocksFiring(Combatant c) {
        MoveDefinition active = c.moves.active();
        return active != null && active.blocksFiring()
                && !(active.firesAfterWallKick() && c.moves.startedAfterWallKick());
    }
}
