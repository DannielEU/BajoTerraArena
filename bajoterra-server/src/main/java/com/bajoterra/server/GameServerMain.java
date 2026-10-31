package com.bajoterra.server;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.mapgen.ArenaGenerator;
import com.bajoterra.core.rules.EliminationRoundsRules;
import com.bajoterra.core.rules.MatchRules;
import java.io.IOException;

/**
 * Entry point of the dedicated server:
 * {@code GameServerMain [--port 7777] [--map aleatoria|caverna] [--rules standard|quick] [--seed n]}.
 * By default every match is played on a new generated arena; {@code --map caverna} plays the hand-made map. Without
 * {@code --seed} the server picks one from the clock, so arenas differ between runs too. The {@code quick} rules play
 * one 15 s round, so a whole match (and the end-of-match screens) can be tried in seconds.
 */
public final class GameServerMain {

    /** A started server and the TCP port it actually listens on. */
    public record Running(GameServer server, int port) implements AutoCloseable {
        @Override
        public void close() {
            server.close();
        }
    }

    private GameServerMain() {
        throw new AssertionError("Entry point, not instantiable");
    }

    public static void main(String[] args) throws IOException, InterruptedException {
        Running running = start(args);
        System.out.println("Bajoterra server listening on TCP port " + running.port());
        Runtime.getRuntime().addShutdownHook(new Thread(running::close));
        Thread.currentThread().join();
    }

    /** Parses the arguments, loads the content and starts listening; does not block. */
    public static Running start(String[] args) throws IOException {
        int port = 7777;
        String map = ArenaGenerator.MAP_ID;
        MatchRules rules = EliminationRoundsRules.bestOfFive();
        long seed = System.currentTimeMillis();
        for (int i = 0; i + 1 < args.length; i += 2) {
            switch (args[i]) {
                case "--port" -> port = Integer.parseInt(args[i + 1]);
                case "--map" -> map = args[i + 1];
                case "--rules" -> rules = rulesNamed(args[i + 1]);
                case "--seed" -> seed = Long.parseLong(args[i + 1]);
                default -> throw new IllegalArgumentException(
                        "Unknown option " + args[i] + ". Use --port --map --rules --seed");
            }
        }
        Content content = Content.load();
        TcpTransport transport = new TcpTransport(port);
        GameServer server = new GameServer(content, transport,
                ServerConfig.defaults(port).withMap(map).withRules(rules).withSeed(seed));
        server.start();
        return new Running(server, transport.port());
    }

    private static MatchRules rulesNamed(String name) {
        return switch (name) {
            case "standard" -> EliminationRoundsRules.bestOfFive();
            // 2 s countdown, one 15 s round, 2 s before the end: a match lasts about 20 s.
            case "quick" -> new EliminationRoundsRules(120, 900, 120, 1, 1);
            default -> throw new IllegalArgumentException("Unknown rules '" + name + "'. Use standard or quick");
        };
    }
}
