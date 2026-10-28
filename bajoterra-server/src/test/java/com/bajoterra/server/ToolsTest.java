package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.bajoterra.protocol.ProtocolVersion;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

/** The command-line tools: server entry point, bot match runner and content catalog. */
class ToolsTest {

    @TempDir
    Path temp;

    @Test
    void theServerEntryPointStartsListeningAndGreetsClients() throws Exception {
        try (GameServerMain.Running running = GameServerMain.start(new String[] {"--port", "0", "--map", "caverna"});
                HeadlessClient client = new HeadlessClient("127.0.0.1", running.port(), "tool-test")) {
            long deadline = System.nanoTime() + 10_000_000_000L;
            while (client.welcome() == null && System.nanoTime() < deadline) {
                Thread.sleep(5);
            }

            assertThat(running.port()).isPositive();
            assertThat(client.welcome()).isNotNull();
            assertThat(client.welcome().getProtocolVersion()).isEqualTo(ProtocolVersion.CURRENT);
            assertThat(client.welcome().getContent().getMapIdsList()).contains("caverna");
        }
    }

    @Test
    void theQuickRulesOptionStartsAServerAndAnUnknownRuleSetIsRejected() throws Exception {
        try (GameServerMain.Running running = GameServerMain.start(new String[] {"--port", "0", "--rules", "quick"})) {
            assertThat(running.port()).isPositive();
        }
        assertThatThrownBy(() -> GameServerMain.start(new String[] {"--port", "0", "--rules", "endless"}))
                .isInstanceOf(IllegalArgumentException.class).hasMessageContaining("endless");
    }

    @Test
    void unknownCommandLineOptionsAreRejected() {
        assertThatThrownBy(() -> GameServerMain.start(new String[] {"--colour", "red"}))
                .isInstanceOf(IllegalArgumentException.class).hasMessageContaining("--colour");
        assertThatThrownBy(() -> BotMatchRunner.main(new String[] {"--bogus", "1"}))
                .isInstanceOf(IllegalArgumentException.class).hasMessageContaining("--bogus");
    }

    @Test
    void theBotRunnerPrintsABalanceReport() throws Exception {
        PrintStream original = System.out;
        ByteArrayOutputStream captured = new ByteArrayOutputStream();
        System.setOut(new PrintStream(captured, true, StandardCharsets.UTF_8));
        try {
            BotMatchRunner.main(new String[] {"--matches", "4", "--threads", "2", "--seed", "3", "--skill-a", "0.9",
                "--skill-b", "0.4", "--map", "caverna"});
        } finally {
            System.setOut(original);
        }

        String report = captured.toString(StandardCharsets.UTF_8);
        assertThat(report).contains("Running 4 mirrored bot matches").contains("matches finished     : 4 / 4")
                .contains("team A wins").contains("simulation speed");
    }

    @Test
    void theRunnerValidatesItsOptions() {
        assertThatThrownBy(() -> new BotMatchRunner.Options(0, 1, "caverna", 0.5, 0.5, 1))
                .isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> new BotMatchRunner.Options(1, 1, "caverna", 0.5, 0.5, 0))
                .isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void theCatalogListsEveryKindOfContent() {
        String catalog = ContentCatalogPrinter.render(CONTENT);

        assertThat(catalog).contains("## Babosas", "## Personajes", "## Movimientos", "## Objetos", "## Mapas",
                "## Ventajas elementales", "## Tipos de efecto registrados");
        for (var slug : CONTENT.slugs().all()) {
            assertThat(catalog).contains("`" + slug.id() + "`");
        }
        assertThat(catalog).contains("`roll`", "`caverna`", "`health_pack`", "`eli`");
        assertThat(catalog).contains("quemadura 4/s", "cura 35", "área r=3.0");
        assertThat(catalog).contains("| FIRE | ICE | ×1.25 |");
    }

    @Test
    void theCatalogCanBeWrittenToAFile() throws IOException {
        Path file = temp.resolve("catalog.md");

        ContentCatalogPrinter.main(new String[] {file.toString()});

        assertThat(Files.readString(file)).startsWith("# Catálogo de contenido");
    }

    @Test
    void theCatalogIsPrintedToTheConsoleWithoutArguments() throws IOException {
        PrintStream original = System.out;
        ByteArrayOutputStream captured = new ByteArrayOutputStream();
        System.setOut(new PrintStream(captured, true, StandardCharsets.UTF_8));
        try {
            ContentCatalogPrinter.main(new String[0]);
        } finally {
            System.setOut(original);
        }

        assertThat(captured.toString(StandardCharsets.UTF_8)).contains("## Babosas");
    }
}
