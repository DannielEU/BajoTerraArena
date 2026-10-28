package com.bajoterra.core.content;

import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.effect.EffectRegistry;
import com.bajoterra.core.item.PickupDefinition;
import com.bajoterra.core.move.MoveBehaviorRegistry;
import com.bajoterra.core.move.MoveDefinition;
import com.bajoterra.core.slug.ElementChart;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.GameMap;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.io.InputStream;
import java.net.URL;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.function.BiFunction;
import java.util.function.Function;

/**
 * Loads and validates all game content. Every JSON file under {@code content/<kind>/} on the classpath is read, so
 * a new slug, move, character, item or map is added by dropping a file in the right folder; effect and move
 * behavior <em>code</em> is discovered through the service loader. All problems found are reported together.
 */
public final class ContentLoader {

    private static final ObjectMapper MAPPER = new ObjectMapper();

    private ContentLoader() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Loads everything visible to {@code loader}; throws a {@link ContentException} listing every problem. */
    public static Content load(ClassLoader loader) {
        List<String> problems = new ArrayList<>();
        EffectRegistry effects = EffectRegistry.load(loader);
        MoveBehaviorRegistry behaviors = MoveBehaviorRegistry.load(loader);
        ElementChart chart = readElements(loader, problems);

        Registry<MoveDefinition> moves = registry("move",
                parseAll(loader, "content/moves", problems, spec -> ContentParsers.move(spec, behaviors)),
                MoveDefinition::withIndex, problems);
        Registry<CharacterDefinition> characters = registry("character",
                parseAll(loader, "content/characters", problems, spec -> ContentParsers.character(spec, moves)),
                CharacterDefinition::withIndex, problems);
        Registry<PickupDefinition> pickups = registry("pickup",
                parseAll(loader, "content/items", problems, spec -> ContentParsers.pickup(spec, effects)),
                PickupDefinition::withIndex, problems);
        Registry<SlugDefinition> slugs = registry("slug",
                parseAll(loader, "content/slugs", problems, spec -> ContentParsers.slug(spec, effects)),
                SlugDefinition::withIndex, problems);
        Registry<GameMap> maps = registry("map",
                parseAll(loader, "content/maps", problems, ContentParsers::map),
                GameMap::withIndex, problems);

        Content content = new Content(chart, effects, behaviors, moves, characters, pickups, slugs, maps);
        if (problems.isEmpty()) {
            problems.addAll(ContentValidator.validate(content));
        }
        if (!problems.isEmpty()) {
            throw ContentException.of(problems);
        }
        return content;
    }

    private static ElementChart readElements(ClassLoader loader, List<String> problems) {
        URL url = loader.getResource("content/elements.json");
        if (url == null) {
            return ElementChart.neutral();
        }
        try {
            return ContentParsers.elements(new Spec(readJson(url), "content/elements.json"));
        } catch (IOException | ContentException | IllegalArgumentException e) {
            problems.add("content/elements.json: " + e.getMessage());
            return ElementChart.neutral();
        }
    }

    private static JsonNode readJson(URL url) throws IOException {
        try (InputStream in = url.openStream()) {
            return MAPPER.readTree(in);
        }
    }

    private static <T> List<T> parseAll(ClassLoader loader, String directory, List<String> problems,
            Function<Spec, T> parser) {
        List<T> parsed = new ArrayList<>();
        for (ResourceScanner.Resource resource : ResourceScanner.listJson(loader, directory)) {
            String path = directory + "/" + resource.name();
            try {
                JsonNode node = readJson(resource.url());
                if (node == null || !node.isObject()) {
                    problems.add(path + ": expected a JSON object");
                    continue;
                }
                parsed.add(parser.apply(new Spec(node, path)));
            } catch (IOException | ContentException | IllegalArgumentException e) {
                String message = e.getMessage();
                problems.add(message != null && message.startsWith(path) ? message : path + ": " + message);
            }
        }
        return parsed;
    }

    private static <T extends Identified> Registry<T> registry(String kind, List<T> items,
            BiFunction<T, Integer, T> indexer, List<String> problems) {
        List<T> sorted = new ArrayList<>(items);
        sorted.sort(Comparator.comparing(Identified::id));
        List<T> indexed = new ArrayList<>(sorted.size());
        for (int i = 0; i < sorted.size(); i++) {
            indexed.add(indexer.apply(sorted.get(i), i));
        }
        try {
            return new Registry<>(kind, indexed);
        } catch (ContentException e) {
            problems.add(e.getMessage());
            return new Registry<>(kind, List.of());
        }
    }
}
