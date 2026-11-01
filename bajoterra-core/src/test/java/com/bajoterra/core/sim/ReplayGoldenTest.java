package com.bajoterra.core.sim;

import static com.bajoterra.core.Fixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.ai.BotArena;
import com.bajoterra.core.rules.MatchPhase;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;
import org.junit.jupiter.api.Test;

/**
 * Replay guarantees. The same seed and the same command log must produce the same match, bit for bit, on any
 * machine (the core uses StrictMath for trigonometry for exactly this reason). The golden values pin today's
 * behavior: when a gameplay change legitimately alters them, update the constants in the same commit.
 */
class ReplayGoldenTest {

    private static final long GOLDEN_BOT_SEED_1_AFTER_1800_TICKS = -8310141631855470299L;
    private static final long GOLDEN_BOT_SEED_2_AFTER_1800_TICKS = 1983050729323692651L;
    private static final long GOLDEN_COMMAND_LOG_AFTER_1800_TICKS = 4428906723507156540L;

    /** One recorded command and the tick it was submitted on. */
    private record Entry(int tick, InputFrame frame) {
    }

    private long botMatchHash(long seed, int ticks) {
        GameSimulation sim = BotArena.build("caverna", seed, true, 0.7, 0.7);
        sim.start();
        for (int i = 0; i < ticks; i++) {
            sim.tick();
        }
        return sim.stateHash();
    }

    private List<Entry> recordSession(long seed, int ticks) {
        SplittableRandom random = new SplittableRandom(seed);
        List<Entry> log = new ArrayList<>();
        int[] sequence = new int[6];
        for (int tick = 200; tick < ticks; tick += 4) {
            int who = random.nextInt(6);
            float yaw = (who < 3 ? 1.5f : -1.5f) + (float) (random.nextDouble() - 0.5);
            log.add(new Entry(tick, new InputFrame(who, ++sequence[who],
                    (float) (random.nextDouble() * 2 - 1), (float) (random.nextDouble() * 2 - 1),
                    yaw, (float) (random.nextDouble() * 0.2 - 0.1), false, 0, 0, 0, random.nextInt(3) > 0,
                    random.nextInt(8), random.nextInt(12) - 1, random.nextInt(3) - 1)));
        }
        return log;
    }

    private long replayHash(List<Entry> log, int ticks) {
        MatchConfig.Builder builder = MatchConfig.builder("caverna").seed(77).fillWithBots(false);
        List<String> characters = List.of("eli", "kord", "trixie");
        for (int team = 0; team < 2; team++) {
            for (String character : characters) {
                builder.slot(SlotConfig.human(team, character, List.of()));
            }
        }
        GameSimulation sim = new GameSimulation(CONTENT, builder.build());
        sim.start();
        int next = 0;
        for (int tick = 1; tick <= ticks; tick++) {
            while (next < log.size() && log.get(next).tick() <= tick) {
                sim.submit(log.get(next++).frame());
            }
            sim.tick();
        }
        return sim.stateHash();
    }

    @Test
    void sameSeedSameBotMatch() {
        assertThat(botMatchHash(1, 1800)).isEqualTo(botMatchHash(1, 1800));
        assertThat(botMatchHash(2, 1800)).isEqualTo(botMatchHash(2, 1800));
    }

    @Test
    void differentSeedsPlayDifferently() {
        assertThat(botMatchHash(1, 1800)).isNotEqualTo(botMatchHash(2, 1800));
    }

    @Test
    void stateHashTracksProgress() {
        assertThat(botMatchHash(1, 600)).isNotEqualTo(botMatchHash(1, 601));
    }

    @Test
    void aRecordedCommandLogReplaysIdentically() {
        List<Entry> log = recordSession(5, 1800);

        assertThat(replayHash(log, 1800)).isEqualTo(replayHash(log, 1800));
        assertThat(replayHash(recordSession(6, 1800), 1800)).isNotEqualTo(replayHash(log, 1800));
    }

    @Test
    void goldenBotMatchHashes() {
        assertThat(botMatchHash(1, 1800)).as("seed 1").isEqualTo(GOLDEN_BOT_SEED_1_AFTER_1800_TICKS);
        assertThat(botMatchHash(2, 1800)).as("seed 2").isEqualTo(GOLDEN_BOT_SEED_2_AFTER_1800_TICKS);
    }

    @Test
    void goldenCommandLogHash() {
        assertThat(replayHash(recordSession(5, 1800), 1800)).isEqualTo(GOLDEN_COMMAND_LOG_AFTER_1800_TICKS);
    }

    @Test
    void aFinishedMatchHasAStableOutcome() {
        GameSimulation first = BotArena.build("caverna", 9, true, 0.7, 0.7);
        GameSimulation second = BotArena.build("caverna", 9, true, 0.7, 0.7);
        first.start();
        second.start();
        while (first.match().phase() != MatchPhase.MATCH_END && first.tickCount() < 60 * 60 * 25) {
            first.tick();
            second.tick();
            assertThat(second.stateHash()).isEqualTo(first.stateHash());
        }

        assertThat(second.match().winner()).isEqualTo(first.match().winner());
        assertThat(second.match().wins(0)).isEqualTo(first.match().wins(0));
        assertThat(second.match().phase()).isEqualTo(MatchPhase.MATCH_END);
    }
}
