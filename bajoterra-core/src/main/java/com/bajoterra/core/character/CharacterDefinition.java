package com.bajoterra.core.character;

import com.bajoterra.core.content.Identified;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.slug.Element;
import java.util.List;

/**
 * Immutable definition of a playable character.
 *
 * @param speed walking speed in m/s
 * @param hitRadius radius of the vertical capsule used for collisions
 * @param hitHeight standing height of the hitbox
 * @param affinity elemental affinity used by the element chart when the character is hit
 * @param healPotency multiplies healing and protection this character casts on others
 * @param moves ordered move set; bit i of an input's move mask requests move i (at most 31 moves)
 */
public record CharacterDefinition(
        String id,
        int index,
        String name,
        double maxHp,
        double maxShield,
        double speed,
        double hitRadius,
        double hitHeight,
        Element affinity,
        double healPotency,
        List<MoveDefinition> moves,
        BlasterSpec blaster) implements Identified {

    public CharacterDefinition {
        if (id == null || id.isBlank()) {
            throw new IllegalArgumentException("Character id is required");
        }
        if (!(maxHp > 0) || !(maxShield >= 0) || !(speed > 0) || !(hitRadius > 0) || !(hitHeight > 0)
                || !(healPotency > 0)) {
            throw new IllegalArgumentException("Invalid character '" + id + "'");
        }
        if (moves.size() > 31) {
            throw new IllegalArgumentException("Character '" + id + "' has more than 31 moves");
        }
        moves = List.copyOf(moves);
    }

    public CharacterDefinition withIndex(int newIndex) {
        return new CharacterDefinition(id, newIndex, name, maxHp, maxShield, speed, hitRadius, hitHeight, affinity,
                healPotency, moves, blaster);
    }
}
