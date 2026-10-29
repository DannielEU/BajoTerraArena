package com.bajoterra.core.sim;

import com.bajoterra.core.ai.BotBrain;
import com.bajoterra.core.ammo.FireController;
import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.ammo.LoadoutValidator;
import com.bajoterra.core.ammo.SlotSelectionPolicy;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.combat.CombatArena;
import com.bajoterra.core.combat.DamagePipeline;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.event.EventBuffer;
import com.bajoterra.core.item.Pickup;
import com.bajoterra.core.item.PickupDefinition;
import com.bajoterra.core.item.PickupSystem;
import com.bajoterra.core.math.Vec3;
import com.bajoterra.core.move.LocomotionStateMachine;
import com.bajoterra.core.move.MovementSystem;
import com.bajoterra.core.projectile.Projectile;
import com.bajoterra.core.projectile.ProjectilePool;
import com.bajoterra.core.projectile.ProjectileSystem;
import com.bajoterra.core.rules.Courage;
import com.bajoterra.core.rules.Match;
import com.bajoterra.core.rules.MatchListener;
import com.bajoterra.core.slug.SlugDefinition;
import com.bajoterra.core.world.GameMap;
import com.bajoterra.core.world.PickupSpawn;
import com.bajoterra.core.world.SpawnPoint;
import com.bajoterra.core.world.World;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;
import java.util.SplittableRandom;

/**
 * Facade over the whole simulation of one match. The server (or a test) submits {@link Command}s, calls
 * {@link #tick()} at {@link SimulationConstants#TICKS_PER_SECOND} Hz and reads the world back for snapshots.
 *
 * <p>The simulation is single-threaded, deterministic (seeded random, no wall clock) and allocation-free in
 * steady state: given the same content, configuration and command log it reproduces the same match exactly.
 */
public final class GameSimulation implements MatchListener {

    /** Capacity of the projectile pool; plenty for six combatants. */
    public static final int MAX_PROJECTILES = 128;
    /** Capacity of the event ring. */
    public static final int EVENT_CAPACITY = 4096;

    private final Content content;
    private final MatchConfig config;
    private final List<SlotConfig> slots;
    private final GameMap map;
    private final World world;
    private final Combatant[] combatants;
    private final CombatArena arena;
    private final ProjectilePool pool;
    private final ProjectileSystem projectiles;
    private final FireController fireController;
    private final MovementSystem movement;
    private final LocomotionStateMachine locomotion = new LocomotionStateMachine();
    private final PickupSystem pickups;
    private final Match match;
    private final Courage courage;
    private final BotDirector director;
    private final ArrayDeque<Command> commands = new ArrayDeque<>();
    private final int[] placed = new int[2];

    /** Builds a match; teams are completed to three members with bots. */
    public GameSimulation(Content content, MatchConfig config) {
        this.content = content;
        this.config = config;
        this.map = content.maps().require(config.mapId());
        this.world = new World(map);
        this.slots = BotFiller.fill(config, content);
        SplittableRandom loadoutRandom = new SplittableRandom(config.seed() ^ 0xA5A5A5A5L);

        this.combatants = new Combatant[slots.size()];
        for (int i = 0; i < slots.size(); i++) {
            combatants[i] = createCombatant(i, slots.get(i), loadoutRandom);
        }
        this.arena = new CombatArena(combatants,
                DamagePipeline.standard(content.elementChart(), config.friendlyFire()),
                new EventBuffer(EVENT_CAPACITY), config.seed());
        this.pool = new ProjectilePool(MAX_PROJECTILES);
        this.projectiles = new ProjectileSystem(arena, world, pool);
        this.fireController = new FireController(arena, world, projectiles, SlotSelectionPolicy.MANUAL);
        this.movement = new MovementSystem(world);
        this.pickups = new PickupSystem(arena, createPickups());
        this.match = new Match(config.rules(), this, arena);
        this.courage = new Courage(config.rules(), arena);
        this.director = new BotDirector(slots, combatants);
        placeCombatants();
    }

    private Combatant createCombatant(int id, SlotConfig slot, SplittableRandom random) {
        CharacterDefinition character = content.characters().require(slot.characterId());
        List<String> ids = slot.slugIds().isEmpty()
                ? LoadoutBuilder.balanced(content.slugs(), random, config.maxDuplicateSlugs())
                : slot.slugIds();
        List<String> problems = LoadoutValidator.validate(ids, content.slugs(), character.blaster().reloadShots(),
                config.maxDuplicateSlugs());
        if (!problems.isEmpty()) {
            throw new IllegalArgumentException("Invalid loadout for slot " + id + ": " + problems);
        }
        List<SlugDefinition> slugs = new ArrayList<>(ids.size());
        for (String slugId : ids) {
            slugs.add(content.slugs().require(slugId));
        }
        Combatant combatant = new Combatant(id, slot.team(), character,
                new Loadout(slugs, character.blaster().reloadShots()));
        if (slot.bot()) {
            combatant.brain = new BotBrain(slot.botSkill());
        }
        return combatant;
    }

    private List<Pickup> createPickups() {
        List<Pickup> list = new ArrayList<>();
        for (PickupSpawn spawn : map.pickups()) {
            PickupDefinition definition = content.pickups().require(spawn.pickupId());
            list.add(new Pickup(list.size(), definition, new Vec3(spawn.x(), spawn.y(), spawn.z())));
        }
        return list;
    }

    // ---------------------------------------------------------------------------------------- control

    /** Leaves the lobby and starts the first round. */
    public void start() {
        match.start();
    }

    /** Queues a command; it takes effect at the start of the next tick. */
    public void submit(Command command) {
        if (command.combatantId() >= 0 && command.combatantId() < combatants.length) {
            commands.add(command);
        }
    }

    /** Advances the simulation by one tick. */
    public void tick() {
        arena.advanceTick();
        while (!commands.isEmpty()) {
            Command command = commands.poll();
            command.applyTo(combatants[command.combatantId()].input);
        }
        match.tick();
        if (!match.phase().isGameplayActive()) {
            return;
        }
        courage.update();
        // Rotate the processing order every tick so that no team is systematically resolved first.
        int count = combatants.length;
        int offset = arena.tick() % count;
        for (int k = 0; k < count; k++) {
            Combatant c = combatants[(k + offset) % count];
            if (c.alive && c.brain != null) {
                c.brain.think(this, c);
            }
        }
        for (int k = 0; k < count; k++) {
            Combatant c = combatants[(k + offset) % count];
            if (c.alive) {
                c.yaw = c.input.aimYaw;
                c.pitch = c.input.aimPitch;
                c.stats.reset();
                c.statuses.modify(c.stats);
                Courage.modify(c);
                locomotion.update(c, arena);
                movement.integrate(c);
            }
        }
        for (int k = 0; k < count; k++) {
            Combatant c = combatants[(k + offset) % count];
            if (c.alive) {
                fireController.update(c);
            }
        }
        projectiles.step();
        for (Combatant c : combatants) {
            if (c.alive) {
                c.statuses.tick(c, arena);
                c.vitals.tick();
            }
        }
        pickups.update();
    }

    // ---------------------------------------------------------------------------------------- match hooks

    @Override
    public void onRoundStart(int roundNumber) {
        if (roundNumber > 1) {
            director.afterRound(match.lastRoundWinner(), arena);
        }
        pool.releaseAll();
        pickups.resetAll();
        placeCombatants();
        for (Combatant c : combatants) {
            if (c.brain != null) {
                c.brain.onRoundStart(this, c);
            }
        }
    }

    @Override
    public void onMatchEnd(int winnerTeam) {
        director.afterRound(match.lastRoundWinner(), arena);
        pool.releaseAll();
    }

    /**
     * Average skill of the filler bots of a team right now (the difficulty director moves it after every round), or
     * NaN when the team has none. A room passes it on to the next match so the difficulty picks up where it was.
     */
    public double fillerBotSkill(int team) {
        return director.averageSkill(team);
    }

    private void placeCombatants() {
        placed[0] = 0;
        placed[1] = 0;
        for (Combatant c : combatants) {
            List<SpawnPoint> spawns = map.spawns().get(c.team);
            SpawnPoint spawn = spawns.get(placed[c.team]++ % spawns.size());
            c.respawn(spawn.x(), spawn.y(), spawn.z(), spawn.yaw());
        }
    }

    // ---------------------------------------------------------------------------------------- accessors

    public Content content() {
        return content;
    }

    public MatchConfig config() {
        return config;
    }

    /** Participants after bot filling, ordered team 0 then team 1; the list index is the combatant id. */
    public List<SlotConfig> slots() {
        return slots;
    }

    public GameMap map() {
        return map;
    }

    public World world() {
        return world;
    }

    public CombatArena arena() {
        return arena;
    }

    public Combatant[] combatants() {
        return combatants;
    }

    public Match match() {
        return match;
    }

    public ProjectilePool projectiles() {
        return pool;
    }

    public List<Pickup> pickups() {
        return pickups.pickups();
    }

    public EventBuffer events() {
        return arena.events();
    }

    public int tickCount() {
        return arena.tick();
    }

    /** FNV-1a hash of the observable state; equal hashes after equal inputs prove the run is reproducible. */
    public long stateHash() {
        long hash = 0xcbf29ce484222325L;
        hash = mix(hash, arena.tick());
        hash = mix(hash, match.phase().ordinal());
        hash = mix(hash, match.roundNumber());
        hash = mix(hash, match.wins(0));
        hash = mix(hash, match.wins(1));
        for (Combatant c : combatants) {
            hash = mix(hash, Double.doubleToLongBits(c.pos.x));
            hash = mix(hash, Double.doubleToLongBits(c.pos.y));
            hash = mix(hash, Double.doubleToLongBits(c.pos.z));
            hash = mix(hash, Double.doubleToLongBits(c.vitals.hp()));
            hash = mix(hash, Double.doubleToLongBits(c.vitals.shield()));
            hash = mix(hash, c.loadout.shotCounter());
            hash = mix(hash, c.alive ? 1 : 0);
            hash = mix(hash, c.courage);
        }
        for (int i = 0; i < pool.capacity(); i++) {
            Projectile p = pool.at(i);
            if (p.active) {
                hash = mix(hash, p.id);
                hash = mix(hash, Double.doubleToLongBits(p.pos.x));
                hash = mix(hash, Double.doubleToLongBits(p.pos.z));
                hash = mix(hash, p.stuck ? p.ticksLeft : -1);
            }
        }
        return mix(hash, arena.events().written());
    }

    private static long mix(long hash, long value) {
        return (hash ^ value) * 0x100000001b3L;
    }
}
