package com.bajoterra.bench;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.combat.DamagePipeline;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.slug.Element;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
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

/** Damage pipeline, effect application and loadout bookkeeping: the small operations done on every shot. */
@State(Scope.Thread)
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
@Fork(1)
public class CombatBenchmark {

    private CombatArena arena;
    private Combatant attacker;
    private Combatant victim;
    private Combatant bystander;
    private Effect damageAndBurn;
    private Effect explosion;
    private Effect heal;
    private final EffectContext context = new EffectContext();
    private final Vec3 point = new Vec3(1, 1, 0);
    private Loadout loadout;

    @Setup
    public void setUp() throws IOException {
        Content content = Content.load();
        List<SlugDefinition> slugs = new ArrayList<>();
        for (int i = 0; i < Loadout.SIZE; i++) {
            slugs.add(content.slugs().get(i % content.slugs().size()));
        }
        attacker = new Combatant(0, 0, content.characters().require("eli"), new Loadout(slugs, 3));
        victim = new Combatant(1, 1, content.characters().require("kord"), new Loadout(slugs, 3));
        bystander = new Combatant(2, 1, content.characters().require("pronto"), new Loadout(slugs, 3));
        for (Combatant c : List.of(attacker, victim, bystander)) {
            c.respawn(0, 0, 0, 0);
        }
        victim.pos.set(1, 0, 0);
        bystander.pos.set(2, 0, 0);
        arena = new CombatArena(new Combatant[] {attacker, victim, bystander},
                DamagePipeline.standard(content.elementChart(), 0), new EventBuffer(4096), 1);
        loadout = attacker.loadout;
        ObjectMapper mapper = new ObjectMapper();
        damageAndBurn = content.effects().createAll(List.of(
                new Spec(mapper.readTree("{\"type\":\"damage\",\"amount\":5}"), "bench"),
                new Spec(mapper.readTree("{\"type\":\"burn\",\"dps\":3,\"durationTicks\":120}"), "bench")));
        explosion = content.effects().create(new Spec(mapper.readTree(
                "{\"type\":\"area\",\"radius\":3,\"effects\":[{\"type\":\"damage\",\"amount\":1},"
                        + "{\"type\":\"knockback\",\"force\":2}]}"), "bench"));
        heal = content.effects().create(new Spec(mapper.readTree("{\"type\":\"heal\",\"amount\":1}"), "bench"));
    }

    private void refill() {
        victim.vitals.reset();
        bystander.vitals.reset();
        victim.alive = true;
        bystander.alive = true;
    }

    @Benchmark
    public double damagePipeline() {
        double dealt = arena.damage(attacker, victim, 0.5, Element.FIRE, 0);
        victim.vitals.reset();
        return dealt;
    }

    @Benchmark
    public void applyDamageAndBurn() {
        refill();
        context.set(arena, attacker, victim, null, TargetingRule.ENEMIES, point, null);
        damageAndBurn.apply(context);
    }

    @Benchmark
    public void applyAreaExplosion() {
        refill();
        context.set(arena, attacker, null, null, TargetingRule.ENEMIES, point, null);
        explosion.apply(context);
    }

    @Benchmark
    public void applyHeal() {
        context.set(arena, attacker, victim, null, TargetingRule.ALLIES, point, null);
        heal.apply(context);
    }

    @Benchmark
    public int loadoutFireAndCycle() {
        if (!loadout.isAvailable(loadout.selected())) {
            loadout.cycle(1);
        }
        loadout.fireSelected();
        loadout.cycle(1);
        return loadout.selected();
    }
}
