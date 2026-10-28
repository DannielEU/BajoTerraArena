package com.bajoterra.bench;

import com.bajoterra.core.ai.BotBrain;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.sim.GameSimulation;
import java.util.concurrent.TimeUnit;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;

/** Cost of one bot decision (perception plus behavior tree); each bot decides ten times per second. */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.MICROSECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class BotBenchmark {

    private GameSimulation sim;
    private Combatant bot;
    private BotBrain brain;

    @Setup
    public void setUp() {
        Content content = Content.load();
        sim = BenchSupport.runningBotMatch(content, "caverna", 31);
        for (int i = 0; i < 600; i++) {
            sim.tick();
        }
        for (Combatant c : sim.combatants()) {
            if (c.alive && c.brain instanceof BotBrain) {
                bot = c;
                brain = (BotBrain) c.brain;
                break;
            }
        }
    }

    @Benchmark
    public void decide() {
        brain.planNow(sim, bot);
    }
}
