package com.bajoterra.bench;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.Obstacle;
import com.bajoterra.core.world.SpawnPoint;
import com.bajoterra.core.world.World;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;
import java.util.concurrent.TimeUnit;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Param;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;

/** Swept-sphere projectile queries and combatant push-out against maps of 10, 50 and 200 obstacles. */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class CollisionBenchmark {

    private static final int SAMPLES = 1024;

    @Param({"10", "50", "200"})
    private int obstacles;

    private World world;
    private Combatant walker;
    private final double[] origin = new double[SAMPLES * 3];
    private final double[] step = new double[SAMPLES * 3];
    private int cursor;

    @Setup
    public void setUp() {
        SplittableRandom random = new SplittableRandom(5);
        List<Obstacle> boxes = new ArrayList<>();
        for (int i = 0; i < obstacles; i++) {
            double x = -45 + random.nextDouble() * 90;
            double z = -45 + random.nextDouble() * 90;
            boxes.add(new Obstacle("o" + i, x, 0, z, x + 1 + random.nextDouble() * 3, 1 + random.nextDouble() * 2,
                    z + 1 + random.nextDouble() * 3, true, true, true));
        }
        List<SpawnPoint> west = List.of(new SpawnPoint(-48, 0, 0, 0), new SpawnPoint(-48, 0, 2, 0),
                new SpawnPoint(-48, 0, 4, 0));
        List<SpawnPoint> east = List.of(new SpawnPoint(48, 0, 0, 0), new SpawnPoint(48, 0, 2, 0),
                new SpawnPoint(48, 0, 4, 0));
        GameMap map = new GameMap("bench", 0, "bench", -50, 50, -50, 50, boxes, List.of(west, east), List.of(), 5.0);
        world = new World(map);
        for (int i = 0; i < SAMPLES; i++) {
            origin[i * 3] = -45 + random.nextDouble() * 90;
            origin[i * 3 + 1] = 0.5 + random.nextDouble() * 2;
            origin[i * 3 + 2] = -45 + random.nextDouble() * 90;
            double angle = random.nextDouble() * Math.PI * 2;
            step[i * 3] = Math.sin(angle) * 0.75;   // one tick of a 45 m/s slug
            step[i * 3 + 1] = (random.nextDouble() - 0.5) * 0.2;
            step[i * 3 + 2] = Math.cos(angle) * 0.75;
        }
        Content content = Content.load();
        CharacterDefinition eli = content.characters().require("eli");
        List<SlugDefinition> slugs = new ArrayList<>();
        for (int i = 0; i < Loadout.SIZE; i++) {
            slugs.add(content.slugs().get(i));
        }
        walker = new Combatant(0, 0, eli, new Loadout(slugs, 3));
        walker.respawn(0, 0, 0, 0);
    }

    @Benchmark
    public double sweepOneProjectileTick() {
        int i = (cursor++ & (SAMPLES - 1)) * 3;
        return world.sweepProjectile(origin[i], origin[i + 1], origin[i + 2], step[i], step[i + 1], step[i + 2], 0.25);
    }

    @Benchmark
    public double resolveCombatant() {
        int i = (cursor++ & (SAMPLES - 1)) * 3;
        walker.pos.set(origin[i], 0, origin[i + 2]);
        world.resolveCombatant(walker);
        return walker.pos.x;
    }

    @Benchmark
    public boolean lineOfSight() {
        int i = (cursor++ & (SAMPLES - 1)) * 3;
        return world.hasLineOfSight(origin[i], 1.4, origin[i + 2], origin[i] + step[i] * 20, 1.0,
                origin[i + 2] + step[i + 2] * 20);
    }
}
