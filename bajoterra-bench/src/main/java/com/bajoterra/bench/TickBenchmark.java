package com.bajoterra.bench;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.slug.ProjectileSpec;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;
import com.bajoterra.core.world.GameMap;
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

/**
 * Cost of one full simulation tick of a 3v3 bot match, with 0, 32 or 64 extra slugs flying around on top of the
 * ones the bots fire. The 60 Hz budget is 16.7 ms; the target is a mean far below 0.25 ms.
 */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.MICROSECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class TickBenchmark {

    /** Extra projectiles kept alive in the pool for the whole run. */
    @Param({"0", "32", "64"})
    private int projectiles;

    private GameSimulation sim;
    private final Projectile[] extra = new Projectile[64];
    private final SplittableRandom random = new SplittableRandom(99);
    private SlugDefinition ghost;

    @Setup
    public void setUp() {
        Content content = Content.load();
        sim = BenchSupport.runningBotMatch(content, "caverna", 17);
        // A slug that flies through everything and never expires, so the pool load stays constant.
        ghost = SlugDefinition.builder("bench_ghost")
                .targeting(TargetingRule.ENEMIES)
                .projectile(new ProjectileSpec(8, 0, 0.1, 1_000_000, 1000))
                .pierceWorld(true)
                .build();
    }

    @Benchmark
    public void tick() {
        keepExtraProjectilesAlive();
        sim.tick();
    }

    private void keepExtraProjectilesAlive() {
        GameMap map = sim.map();
        ProjectilePool pool = sim.projectiles();
        Combatant owner = sim.combatants()[0];
        for (int i = 0; i < projectiles; i++) {
            Projectile p = extra[i];
            boolean outside = p != null && p.active && (p.pos.x < map.minX() || p.pos.x > map.maxX()
                    || p.pos.z < map.minZ() || p.pos.z > map.maxZ());
            if (p == null || !p.active || outside) {
                if (p == null || !p.active) {
                    p = pool.acquire();
                    if (p == null) {
                        continue;
                    }
                    extra[i] = p;
                }
                p.owner = owner;
                p.slug = ghost;
                p.pos.set(map.minX() + random.nextDouble() * (map.maxX() - map.minX()), 1.3,
                        map.minZ() + random.nextDouble() * (map.maxZ() - map.minZ()));
                double angle = random.nextDouble() * Math.PI * 2;
                p.vel.set(Math.sin(angle) * 8, 0, Math.cos(angle) * 8);
                p.ticksLeft = 1_000_000;
                p.pierceLeft = 1000;
                p.hitMask = 0;
            }
        }
    }
}
