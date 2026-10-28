package com.bajoterra.core.content;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.io.IOException;
import java.net.URL;
import java.net.URLClassLoader;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

/** Content authors get precise, aggregated error messages instead of a crash on the first typo. */
class ContentLoaderErrorsTest {

    @TempDir
    Path extraContent;

    private void write(String relative, String json) throws IOException {
        Path file = extraContent.resolve(relative);
        Files.createDirectories(file.getParent());
        Files.writeString(file, json, StandardCharsets.UTF_8);
    }

    private ContentException loadWithExtras() throws IOException {
        try (URLClassLoader loader = new URLClassLoader(new URL[] {extraContent.toUri().toURL()},
                getClass().getClassLoader())) {
            return org.assertj.core.api.Assertions.catchThrowableOfType(ContentException.class,
                    () -> ContentLoader.load(loader));
        }
    }

    @Test
    void theShippedContentLoadsCleanly() {
        Content content = ContentLoader.load(getClass().getClassLoader());

        assertThat(content.slugs().size()).isGreaterThanOrEqualTo(14);
    }

    @Test
    void extraContentOnTheClasspathIsMergedAutomatically() throws IOException {
        write("content/slugs/zz_extra.json", """
                { "id": "zz_extra", "onHit": [ { "type": "damage", "amount": 9 } ] }
                """);
        try (URLClassLoader loader = new URLClassLoader(new URL[] {extraContent.toUri().toURL()},
                getClass().getClassLoader())) {
            Content content = ContentLoader.load(loader);

            assertThat(content.slugs().find("zz_extra")).isNotNull();
        }
    }

    @Test
    void anInvalidEffectParameterNamesTheFileAndTheField() throws IOException {
        write("content/slugs/bad_damage.json", """
                { "id": "bad_damage", "onHit": [ { "type": "damage", "amount": -4 } ] }
                """);

        ContentException error = loadWithExtras();

        assertThat(error).hasMessageContaining("content/slugs/bad_damage.json")
                .hasMessageContaining("onHit[0].amount");
    }

    @Test
    void anUnknownEffectTypeListsTheKnownOnes() throws IOException {
        write("content/slugs/bad_type.json", """
                { "id": "bad_type", "onHit": [ { "type": "teleport" } ] }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("unknown effect type 'teleport'")
                .hasMessageContaining("damage");
    }

    @Test
    void allProblemsAreReportedTogether() throws IOException {
        write("content/slugs/bad_one.json", "{ \"id\": \"bad_one\", \"element\": \"PLASMA\" }");
        write("content/slugs/bad_two.json", "{ \"onHit\": [] }");
        write("content/slugs/not_json.json", "this is not json");

        ContentException error = loadWithExtras();

        assertThat(error.getMessage()).contains("bad_one.json").contains("bad_two.json").contains("not_json.json");
        assertThat(error.getMessage()).contains("3 content problem(s)");
    }

    @Test
    void duplicateIdsAreRejected() throws IOException {
        write("content/slugs/infurnus_copy.json", """
                { "id": "infurnus", "onHit": [ { "type": "damage", "amount": 1 } ] }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("Duplicate slug id 'infurnus'");
    }

    @Test
    void aSupportSlugThatTargetsEnemiesIsInconsistent() throws IOException {
        write("content/slugs/odd_support.json", """
                { "id": "odd_support", "role": "SUPPORT", "targeting": "ENEMIES",
                  "onHit": [ { "type": "heal", "amount": 5 } ] }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("odd_support")
                .hasMessageContaining("SUPPORT requires targeting");
    }

    @Test
    void aSlugWithoutEffectsIsRejected() throws IOException {
        write("content/slugs/empty_slug.json", "{ \"id\": \"empty_slug\" }");

        assertThat(loadWithExtras()).hasMessageContaining("empty_slug").hasMessageContaining("no effects");
    }

    @Test
    void aCharacterCannotReferenceAnUnknownMove() throws IOException {
        write("content/characters/ghost.json", """
                { "id": "ghost", "maxHp": 50, "maxShield": 0, "speed": 5, "moves": ["levitate"],
                  "blaster": { "fireIntervalTicks": 20, "reloadShots": 3 } }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("content/characters/ghost.json")
                .hasMessageContaining("unknown move 'levitate'");
    }

    @Test
    void aMapWithASpawnInsideAnObstacleIsRejected() throws IOException {
        write("content/maps/bad_map.json", """
                { "id": "bad_map", "bounds": {"minX": -10, "maxX": 10, "minZ": -10, "maxZ": 10},
                  "obstacles": [ { "id": "block", "min": [-1, 0, -1], "max": [1, 2, 1] } ],
                  "spawns": {
                    "0": [ {"pos": [0, 0, 0]}, {"pos": [-8, 0, 2]}, {"pos": [-8, 0, 4]} ],
                    "1": [ {"pos": [8, 0, 0]}, {"pos": [8, 0, 2]}, {"pos": [8, 0, 4]} ] } }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("bad_map").hasMessageContaining("inside an obstacle");
    }

    @Test
    void aMapNeedsThreeSpawnsPerTeam() throws IOException {
        write("content/maps/small_map.json", """
                { "id": "small_map", "bounds": {"minX": -10, "maxX": 10, "minZ": -10, "maxZ": 10},
                  "spawns": { "0": [ {"pos": [-8, 0, 0]} ], "1": [ {"pos": [8, 0, 0]} ] } }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("at least 3 spawn points");
    }

    @Test
    void aMapWhoseTeamsCannotReachEachOtherIsRejected() throws IOException {
        write("content/maps/split_map.json", """
                { "id": "split_map", "bounds": {"minX": -10, "maxX": 10, "minZ": -10, "maxZ": 10},
                  "obstacles": [ { "id": "divider", "min": [-0.5, 0, -10], "max": [0.5, 3, 10] } ],
                  "spawns": {
                    "0": [ {"pos": [-8, 0, -2]}, {"pos": [-8, 0, 0]}, {"pos": [-8, 0, 2]} ],
                    "1": [ {"pos": [8, 0, -2]}, {"pos": [8, 0, 0]}, {"pos": [8, 0, 2]} ] } }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("no walkable path");
    }

    @Test
    void anObstacleThatIsTooThinIsRejected() throws IOException {
        write("content/maps/thin_map.json", """
                { "id": "thin_map", "bounds": {"minX": -10, "maxX": 10, "minZ": -10, "maxZ": 10},
                  "obstacles": [ { "id": "paper", "min": [0, 0, -3], "max": [0.05, 2, 3] } ],
                  "spawns": {
                    "0": [ {"pos": [-8, 0, -2]}, {"pos": [-8, 0, 0]}, {"pos": [-8, 0, 2]} ],
                    "1": [ {"pos": [8, 0, -2]}, {"pos": [8, 0, 0]}, {"pos": [8, 0, 2]} ] } }
                """);

        assertThat(loadWithExtras()).hasMessageContaining("paper").hasMessageContaining("thinner");
    }

    @Test
    void specAccessorsExplainWhatWasWrong() {
        Spec spec = com.bajoterra.core.Fixtures.spec("{\"n\": \"text\", \"r\": 5, \"list\": 3}");

        assertThatThrownBy(() -> spec.requireDouble("n", 0, 1)).hasMessageContaining("test.n");
        assertThatThrownBy(() -> spec.requireDouble("r", 0, 1)).hasMessageContaining("outside [0.0, 1.0]");
        assertThatThrownBy(() -> spec.list("list")).hasMessageContaining("expected an array");
        assertThatThrownBy(() -> spec.requireString("missing")).hasMessageContaining("required non-empty string");
    }
}
