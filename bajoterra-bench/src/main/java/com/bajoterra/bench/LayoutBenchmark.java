package com.bajoterra.bench;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.slug.SlugDefinition;
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

/**
 * Two design questions answered with numbers.
 *
 * <p>1. Projectile storage: array of objects (the implementation) versus structure of arrays. Both step 64 slugs
 * with gravity and lifetime, nothing else. The simpler object layout is kept unless the arrays win by more than 10 %.
 *
 * <p>2. Content lookup: string id through a map versus dense index into an array (what snapshots use).
 */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class LayoutBenchmark {

    private static final int COUNT = 64;
    private static final double DT = 1.0 / 60;

    /** Object-per-projectile layout, as in ProjectilePool. */
    private static final class Shot {
        double x;
        double y;
        double z;
        double vx;
        double vy;
        double vz;
        int ticksLeft;
        boolean active;
    }

    private final Shot[] objects = new Shot[COUNT];
    private final double[] px = new double[COUNT];
    private final double[] py = new double[COUNT];
    private final double[] pz = new double[COUNT];
    private final double[] vx = new double[COUNT];
    private final double[] vy = new double[COUNT];
    private final double[] vz = new double[COUNT];
    private final int[] life = new int[COUNT];
    private final boolean[] alive = new boolean[COUNT];
    private Content content;
    private String[] ids;
    private int cursor;

    @Setup
    public void setUp() {
        for (int i = 0; i < COUNT; i++) {
            Shot s = new Shot();
            s.x = i;
            s.y = 1.3;
            s.z = i * 0.5;
            s.vx = 20 + i;
            s.vy = 1;
            s.vz = 5;
            s.ticksLeft = Integer.MAX_VALUE;
            s.active = true;
            objects[i] = s;
            px[i] = s.x;
            py[i] = s.y;
            pz[i] = s.z;
            vx[i] = s.vx;
            vy[i] = s.vy;
            vz[i] = s.vz;
            life[i] = Integer.MAX_VALUE;
            alive[i] = true;
        }
        content = Content.load();
        ids = content.slugs().all().stream().map(SlugDefinition::id).toArray(String[]::new);
    }

    @Benchmark
    public double stepObjects() {
        double sum = 0;
        for (Shot s : objects) {
            if (s.active) {
                s.vy -= 4 * DT;
                s.x += s.vx * DT;
                s.y += s.vy * DT;
                s.z += s.vz * DT;
                s.ticksLeft--;
                sum += s.x;
            }
        }
        return sum;
    }

    @Benchmark
    public double stepArrays() {
        double sum = 0;
        for (int i = 0; i < COUNT; i++) {
            if (alive[i]) {
                vy[i] -= 4 * DT;
                px[i] += vx[i] * DT;
                py[i] += vy[i] * DT;
                pz[i] += vz[i] * DT;
                life[i]--;
                sum += px[i];
            }
        }
        return sum;
    }

    @Benchmark
    public SlugDefinition lookupById() {
        return content.slugs().require(ids[nextIndex()]);
    }

    @Benchmark
    public SlugDefinition lookupByIndex() {
        return content.slugs().get(nextIndex());
    }

    /** Cycles through the slug indices; an overflow-free counter (a plain ++ would go negative after 2^31 calls). */
    private int nextIndex() {
        int index = cursor;
        cursor = index + 1 == ids.length ? 0 : index + 1;
        return index;
    }
}
