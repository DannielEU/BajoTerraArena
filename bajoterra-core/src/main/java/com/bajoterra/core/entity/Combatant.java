package com.bajoterra.core.entity;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.effect.StatusSet;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.move.MoveRunner;
import com.bajoterra.core.stats.StatBlock;
import com.bajoterra.core.stats.Vitals;

/**
 * A player or bot inside a match. Identity fields are final; the rest is simulation state that the systems mutate
 * in place (no allocation in the tick loop). Humans and bots are the same type: only {@link #brain} differs.
 */
public final class Combatant {

    public final int id;
    public final int team;
    public final CharacterDefinition character;

    /** Feet position. */
    public final Vec3 pos = new Vec3();
    /** Velocity produced by input and gravity. */
    public final Vec3 vel = new Vec3();
    /** Decaying external velocity (knockback). */
    public final Vec3 knock = new Vec3();
    public double yaw;
    public double pitch;
    public boolean grounded = true;
    public boolean alive = true;
    public Posture posture = Posture.GROUNDED;

    public final Vitals vitals;
    public final StatBlock stats = new StatBlock();
    public final StatusSet statuses = new StatusSet();
    public final InputState input = new InputState();
    public final Loadout loadout;
    public final MoveRunner moves = new MoveRunner();

    /** First tick at which the next shot is allowed. */
    public int nextFireTick;
    /** Courage level: how many combatants this one's team is down (0 when it is not outnumbered). */
    public int courage;
    /** Source that produces this combatant's input each tick; null for network-controlled players. */
    public InputSource brain;

    public int kills;
    public int deaths;
    public int shotsFired;
    public int shotsHit;
    public double damageDealt;
    public double damageTaken;
    public double healingDone;

    public Combatant(int id, int team, CharacterDefinition character, Loadout loadout) {
        this.id = id;
        this.team = team;
        this.character = character;
        this.loadout = loadout;
        this.vitals = new Vitals(character.maxHp(), character.maxShield());
    }

    /** Current hitbox radius. */
    public double hitRadius() {
        return character.hitRadius();
    }

    /** Current hitbox height, shrunk while crouching. */
    public double hitHeight() {
        return character.hitHeight() * stats.hitboxHeightMultiplier;
    }

    /** Height of the hitbox centre above the ground plane. */
    public double centerY() {
        return pos.y + hitHeight() * 0.5;
    }

    /** Resets everything that is per-round and places the combatant at a spawn point. */
    public void respawn(double x, double y, double z, double spawnYaw) {
        pos.set(x, y, z);
        vel.setZero();
        knock.setZero();
        yaw = spawnYaw;
        pitch = 0;
        grounded = true;
        alive = true;
        posture = Posture.GROUNDED;
        vitals.reset();
        statuses.clear();
        stats.reset();
        loadout.reset();
        moves.reset();
        input.reset();
        nextFireTick = 0;
        courage = 0;
    }
}
