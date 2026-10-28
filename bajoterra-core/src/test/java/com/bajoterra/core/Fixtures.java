package com.bajoterra.core;

import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.combat.DamagePipeline;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.content.Spec;
import com.bajoterra.core.effect.Effect;
import com.bajoterra.core.effect.EffectContext;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.event.EventType;
import com.bajoterra.core.event.GameEvent;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.slug.TargetingRule;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/** Shared helpers to build small, fully scripted simulations for tests. */
public final class Fixtures {

    /** Content loaded once for the whole test run. */
    public static final Content CONTENT = Content.load();

    private Fixtures() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Ten slugs cycling through the given ids (tests that need a specific slug on every slot). */
    public static List<String> loadout(String... ids) {
        List<String> list = new ArrayList<>();
        for (int i = 0; i < 10; i++) {
            list.add(ids[i % ids.length]);
        }
        return list;
    }

    /** A human slot with a loadout made of the given slugs. */
    public static SlotConfig human(int team, String characterId, String... slugIds) {
        return SlotConfig.human(team, characterId, loadout(slugIds));
    }

    /** Builds a scripted simulation without bot filling; combatants are the slots in team order. */
    public static GameSimulation scripted(String mapId, long seed, SlotConfig... slots) {
        MatchConfig.Builder builder = MatchConfig.builder(mapId)
                .seed(seed)
                .fillWithBots(false)
                .maxDuplicateSlugs(10);
        for (SlotConfig slot : slots) {
            builder.slot(slot);
        }
        return new GameSimulation(CONTENT, builder.build());
    }

    /** Starts the match and ticks until the first round is active. */
    public static void startRound(GameSimulation sim) {
        sim.start();
        int guard = 0;
        while (!sim.match().phase().isGameplayActive()) {
            sim.tick();
            if (++guard > 10_000) {
                throw new IllegalStateException("Round never became active");
            }
        }
    }

    public static void ticks(GameSimulation sim, int count) {
        for (int i = 0; i < count; i++) {
            sim.tick();
        }
    }

    /** Places a combatant on the ground at the given position, facing {@code yaw}. */
    public static void place(Combatant c, double x, double z, double yaw) {
        c.pos.set(x, 0, z);
        c.vel.setZero();
        c.knock.setZero();
        c.yaw = yaw;
        c.input.aimYaw = yaw;
    }

    /** Parses a JSON object into a {@link Spec}. */
    public static Spec spec(String json) {
        try {
            return new Spec(new ObjectMapper().readTree(json), "test");
        } catch (IOException e) {
            throw new IllegalArgumentException(e);
        }
    }

    /** Builds an effect from JSON using the registered factories. */
    public static Effect effect(String json) {
        return CONTENT.effects().create(spec(json));
    }

    /** A combatant that is not part of a simulation, with a loadout cycling through the slugs. */
    public static Combatant combatant(int id, int team, String characterId, String... slugIds) {
        CharacterDefinition character = CONTENT.characters().require(characterId);
        List<SlugDefinition> slugs = new ArrayList<>();
        for (String slugId : loadout(slugIds)) {
            slugs.add(CONTENT.slugs().require(slugId));
        }
        Combatant c = new Combatant(id, team, character, new Loadout(slugs, character.blaster().reloadShots()));
        c.respawn(0, 0, 0, 0);
        return c;
    }

    /** A bare combat arena (no map, no projectiles) around the given combatants. */
    public static CombatArena arena(Combatant... combatants) {
        return new CombatArena(combatants, DamagePipeline.standard(CONTENT.elementChart(), 0.0),
                new EventBuffer(1024), 1);
    }

    /** Applies an effect as if a slug fired by {@code source} had just hit {@code target}. */
    public static void apply(CombatArena arena, Effect effect, Combatant source, Combatant target) {
        EffectContext ctx = new EffectContext().set(arena, source, target, null, TargetingRule.ENEMIES,
                target == null ? new Vec3() : target.pos, null);
        effect.apply(ctx);
    }

    /** Counts events of a type produced so far. */
    public static int count(GameSimulation sim, EventType type) {
        EventBuffer events = sim.events();
        int total = 0;
        for (long seq = events.oldestAvailable(); seq < events.written(); seq++) {
            GameEvent event = events.at(seq);
            if (event.type == type) {
                total++;
            }
        }
        return total;
    }
}
