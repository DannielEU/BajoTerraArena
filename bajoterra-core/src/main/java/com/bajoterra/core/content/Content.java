package com.bajoterra.core.content;

import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.item.PickupDefinition;
import com.bajoterra.core.move.MoveBehaviorRegistry;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.slug.ElementChart;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.GameMap;

/**
 * Everything loaded from the content files: the immutable catalogs a match is built from. One {@code Content}
 * instance is shared by every simulation in the process.
 */
public record Content(
        ElementChart elementChart,
        EffectRegistry effects,
        MoveBehaviorRegistry moveBehaviors,
        Registry<MoveDefinition> moves,
        Registry<CharacterDefinition> characters,
        Registry<PickupDefinition> pickups,
        Registry<SlugDefinition> slugs,
        Registry<GameMap> maps) {

    /** Loads the content visible to the thread's context class loader (or this module's loader). */
    public static Content load() {
        ClassLoader loader = Thread.currentThread().getContextClassLoader();
        return ContentLoader.load(loader != null ? loader : Content.class.getClassLoader());
    }
}
