package com.bajoterra.core.move;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.entry;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.move.builtin.DriveBehavior;
import com.bajoterra.core.move.builtin.ImpulseBehavior;
import org.junit.jupiter.api.Test;

/** Behaviors describe themselves so the server can tell clients how to mirror the movement they cause. */
class BehaviorDescriptionTest {

    @Test
    void anImpulseDescribesItsVerticalAndHorizontalSpeed() {
        MoveBehavior jump = new ImpulseBehavior(8.5, 2.0);

        assertThat(jump.type()).isEqualTo("impulse");
        assertThat(jump.params()).containsOnly(
                entry("vertical", 8.5),
                entry("horizontal", 2.0));
    }

    @Test
    void aDriveDescribesItsSpeed() {
        MoveBehavior roll = new DriveBehavior(8.0);

        assertThat(roll.type()).isEqualTo("drive");
        assertThat(roll.params()).containsOnly(entry("speed", 8.0));
    }

    @Test
    void aBehaviorWithoutAnythingToMirrorIsNotPredicted() {
        assertThat(NoMoveBehavior.INSTANCE.type()).isEmpty();
        assertThat(NoMoveBehavior.INSTANCE.params()).isEmpty();
    }

    @Test
    void theBuiltInMovesOfTheGameExposeTheirBehaviorTypes() {
        Content content = Content.load();

        assertThat(content.moves().require("jump").behavior().type()).isEqualTo("impulse");
        assertThat(content.moves().require("roll").behavior().type()).isEqualTo("drive");
        assertThat(content.moves().require("dash").behavior().params()).containsEntry("speed", 16.0);
        assertThat(content.moves().require("crouch").behavior().type()).isEmpty();
    }
}
