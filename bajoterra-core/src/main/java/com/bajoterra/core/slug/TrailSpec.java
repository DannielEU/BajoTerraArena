package com.bajoterra.core.slug;

import com.bajoterra.core.effect.Effect;

/**
 * A wall of fire a slug leaves on the floor under its flight (Infurnus): every {@code spacing} metres it flies low
 * enough, a patch of flames {@code radius} wide and {@code height} tall burns for {@code durationTicks}; whoever the
 * slug could hit and walks into it suffers {@code effects}, at most once every {@code periodTicks}.
 *
 * @param spacing metres flown between two patches
 * @param maxHeight a patch is only left when the floor is at most this far under the slug
 * @param maxLength metres of trail a single slug leaves at most
 * @param startAfter metres flown before the first patch (none at the slinger's feet)
 */
public record TrailSpec(double spacing, double maxHeight, double radius, double height, int durationTicks,
        int periodTicks, double maxLength, double startAfter, Effect effects) {

    /** No trail. */
    public static final TrailSpec NONE = new TrailSpec(1, 0, 0, 0, 0, 1, 0, 0, Effect.NONE);

    /** True when the slug leaves a trail. */
    public boolean exists() {
        return durationTicks > 0 && maxLength > 0;
    }
}
