package com.bajoterra.server;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.rules.MatchPhase;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.LoadoutBuilder;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.core.sim.SlotConfig;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.SplittableRandom;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

/**
 * Headless bot-versus-bot matches without any network: validates the game rules at scale and measures balance and
 * speed. Teams are mirrored (same characters and loadouts) and swap sides on alternate matches, so any win-rate
 * deviation from 50 % comes from the AI skill difference or from a spawn-side bias, not from the line-ups.
 */
public final class BotMatchRunner {

    private static final List<String> LINE_UP = List.of("eli", "kord", "trixie");
    private static final int TICK_LIMIT = SimulationConstants.TICKS_PER_SECOND * 60 * 25;

    /** Parameters of a run. */
    public record Options(int matches, long seed, String mapId, double skillA, double skillB, int threads) {
        public Options {
            if (matches <= 0 || threads <= 0) {
                throw new IllegalArgumentException("matches and threads must be positive");
            }
        }
    }

    /** Aggregated results. Team "A" is the one whose side alternates; skillA may differ from skillB. */
    public record Summary(int matches, int finished, int aWins, int bWins, int draws, int teamZeroWins,
            double averageRounds, double averageSimulatedSeconds, long totalTicks, long shots, long hits,
            long wallNanos) {

        public double aWinRate() {
            return matches == 0 ? 0 : aWins / (double) matches;
        }

        public double teamZeroWinRate() {
            return matches == 0 ? 0 : teamZeroWins / (double) matches;
        }

        public double accuracy() {
            return shots == 0 ? 0 : hits / (double) shots;
        }

        /** Simulation throughput: ticks per wall-clock second summed over all threads. */
        public double ticksPerSecond() {
            return wallNanos == 0 ? 0 : totalTicks / (wallNanos / 1e9);
        }
    }

    private record MatchResult(boolean finished, int winnerTeam, int aTeam, int rounds, int ticks, long shots,
            long hits) {
    }

    private BotMatchRunner() {
        throw new AssertionError("Utility class, not instantiable");
    }

    public static void main(String[] args) throws Exception {
        Options options = parse(args);
        Content content = Content.load();
        System.out.printf(Locale.ROOT,
                "Running %d mirrored bot matches on '%s' (seed %d, skills %.2f vs %.2f, %d threads)%n",
                options.matches(), options.mapId(), options.seed(), options.skillA(), options.skillB(),
                options.threads());
        Summary summary = run(content, options);
        print(summary);
    }

    /** Plays all matches, in parallel, and aggregates the outcome. */
    public static Summary run(Content content, Options options) throws InterruptedException, ExecutionException {
        long begin = System.nanoTime();
        List<MatchResult> results = new ArrayList<>();
        try (ExecutorService pool = Executors.newFixedThreadPool(options.threads())) {
            List<Future<MatchResult>> futures = new ArrayList<>();
            for (int i = 0; i < options.matches(); i++) {
                int index = i;
                futures.add(pool.submit(() -> playOne(content, options, index)));
            }
            for (Future<MatchResult> future : futures) {
                results.add(future.get());
            }
        }
        long wall = System.nanoTime() - begin;

        int finished = 0;
        int aWins = 0;
        int bWins = 0;
        int draws = 0;
        int teamZeroWins = 0;
        long rounds = 0;
        long ticks = 0;
        long shots = 0;
        long hits = 0;
        for (MatchResult r : results) {
            finished += r.finished() ? 1 : 0;
            if (r.winnerTeam() < 0) {
                draws++;
            } else if (r.winnerTeam() == r.aTeam()) {
                aWins++;
            } else {
                bWins++;
            }
            teamZeroWins += r.winnerTeam() == 0 ? 1 : 0;
            rounds += r.rounds();
            ticks += r.ticks();
            shots += r.shots();
            hits += r.hits();
        }
        int n = results.size();
        return new Summary(n, finished, aWins, bWins, draws, teamZeroWins, rounds / (double) n,
                ticks / (double) n / SimulationConstants.TICKS_PER_SECOND, ticks, shots, hits, wall);
    }

    private static MatchResult playOne(Content content, Options options, int index) {
        long seed = options.seed() + index;
        boolean aOnTeamZero = index % 2 == 0;
        SplittableRandom random = new SplittableRandom(seed);
        List<List<String>> loadouts = new ArrayList<>();
        for (int i = 0; i < LINE_UP.size(); i++) {
            loadouts.add(LoadoutBuilder.balanced(content.slugs(), random, 2));
        }
        MatchConfig.Builder builder = MatchConfig.builder(options.mapId()).seed(seed).fillWithBots(false);
        for (int team = 0; team < 2; team++) {
            boolean isA = (team == 0) == aOnTeamZero;
            for (int i = 0; i < LINE_UP.size(); i++) {
                builder.slot(new SlotConfig(team, LINE_UP.get(i), loadouts.get(i), true,
                        isA ? options.skillA() : options.skillB()));
            }
        }
        GameSimulation sim = new GameSimulation(content, builder.build());
        sim.start();
        while (sim.match().phase() != MatchPhase.MATCH_END && sim.tickCount() < TICK_LIMIT) {
            sim.tick();
        }
        long shots = 0;
        long hits = 0;
        for (Combatant c : sim.combatants()) {
            shots += c.shotsFired;
            hits += c.shotsHit;
        }
        boolean finished = sim.match().phase() == MatchPhase.MATCH_END;
        return new MatchResult(finished, finished ? sim.match().winner() : -1, aOnTeamZero ? 0 : 1,
                sim.match().roundNumber(), sim.tickCount(), shots, hits);
    }

    private static void print(Summary s) {
        System.out.printf(Locale.ROOT, "matches finished     : %d / %d%n", s.finished(), s.matches());
        System.out.printf(Locale.ROOT, "team A wins          : %d (%.1f %%)%n", s.aWins(), 100 * s.aWinRate());
        System.out.printf(Locale.ROOT, "team B wins          : %d%n", s.bWins());
        System.out.printf(Locale.ROOT, "draws                : %d%n", s.draws());
        System.out.printf(Locale.ROOT, "team on side 0 wins  : %.1f %%%n", 100 * s.teamZeroWinRate());
        System.out.printf(Locale.ROOT, "average rounds       : %.2f%n", s.averageRounds());
        System.out.printf(Locale.ROOT, "average match length : %.0f simulated seconds%n", s.averageSimulatedSeconds());
        System.out.printf(Locale.ROOT, "accuracy             : %.1f %%%n", 100 * s.accuracy());
        System.out.printf(Locale.ROOT, "simulation speed     : %.0f ticks/s (%.0fx real time)%n", s.ticksPerSecond(),
                s.ticksPerSecond() / SimulationConstants.TICKS_PER_SECOND);
    }

    private static Options parse(String[] args) {
        int matches = 100;
        long seed = 42;
        String map = "caverna";
        double skillA = 0.7;
        double skillB = 0.7;
        int threads = Runtime.getRuntime().availableProcessors();
        for (int i = 0; i + 1 < args.length; i += 2) {
            switch (args[i]) {
                case "--matches" -> matches = Integer.parseInt(args[i + 1]);
                case "--seed" -> seed = Long.parseLong(args[i + 1]);
                case "--map" -> map = args[i + 1];
                case "--skill-a" -> skillA = Double.parseDouble(args[i + 1]);
                case "--skill-b" -> skillB = Double.parseDouble(args[i + 1]);
                case "--threads" -> threads = Integer.parseInt(args[i + 1]);
                default -> throw new IllegalArgumentException("Unknown option " + args[i]
                        + ". Use --matches --seed --map --skill-a --skill-b --threads");
            }
        }
        return new Options(matches, seed, map, skillA, skillB, threads);
    }
}
