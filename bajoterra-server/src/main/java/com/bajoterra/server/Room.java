package com.bajoterra.server;

import com.bajoterra.core.ai.BotBrain;
import com.bajoterra.core.ammo.Loadout;
import com.bajoterra.core.ammo.LoadoutValidator;
import com.bajoterra.core.character.CharacterDefinition;
import com.bajoterra.core.content.Content;
import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.sim.BotFiller;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.InputFrame;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import com.bajoterra.protocol.Transport;
import com.bajoterra.protocol.generated.MatchEnd;
import com.bajoterra.protocol.generated.MatchStart;
import com.bajoterra.protocol.generated.RoomState;
import com.bajoterra.protocol.generated.ServerError;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.Slot;
import com.bajoterra.protocol.generated.Snapshot;
import java.util.ArrayList;
import java.util.List;

/**
 * A lobby that turns into a running match: collects up to six humans (three per team), their characters and
 * loadouts, starts the simulation when everybody is ready (filling empty places with bots) and streams snapshots
 * while it runs. Everything here runs on the simulation thread.
 */
final class Room {

    static final int MAX_HUMANS = 2 * MatchConfig.TEAM_SIZE;

    static final int ERROR_ROOM_UNAVAILABLE = 3;
    static final int ERROR_INVALID_LOADOUT = 4;
    static final int ERROR_UNKNOWN_CHARACTER = 5;
    static final int ERROR_NOT_IN_ROOM = 6;

    /** Nicknames of the cave fighters that fill the empty places, by combatant id (shown to players). */
    private static final String[] BOT_NAMES = {"Chispa", "Roca", "Brasa", "Niebla", "Trueno", "Sombra"};

    private final String id;
    private final Content content;
    private final ServerConfig config;
    private final Transport transport;
    private final long seed;
    private final List<Session> members = new ArrayList<>();
    private GameSimulation sim;
    private long nextEventSequence;
    private int matchesPlayed;
    /** Where the difficulty director left the bots beside and against the people of this room. */
    private double allySkill = BotFiller.ALLY_SKILL;
    private double rivalSkill = BotFiller.RIVAL_SKILL;

    Room(String id, Content content, ServerConfig config, Transport transport) {
        this.id = id;
        this.content = content;
        this.config = config;
        this.transport = transport;
        this.seed = config.seed() ^ id.hashCode();
    }

    String id() {
        return id;
    }

    boolean isRunning() {
        return sim != null;
    }

    boolean isEmpty() {
        return members.isEmpty();
    }

    GameSimulation simulation() {
        return sim;
    }

    // ------------------------------------------------------------------------------------------ lobby

    /** Adds a session; returns an error message or null. */
    String join(Session session, int requestedTeam) {
        if (sim != null) {
            return "The match has already started";
        }
        if (members.size() >= MAX_HUMANS) {
            return "The room is full";
        }
        int team = requestedTeam;
        if (team != 0 && team != 1) {
            team = teamSize(0) <= teamSize(1) ? 0 : 1;
        }
        if (teamSize(team) >= MatchConfig.TEAM_SIZE) {
            team = 1 - team;
        }
        session.room = this;
        session.team = team;
        session.ready = false;
        members.add(session);
        broadcastState();
        return null;
    }

    void leave(Session session) {
        members.remove(session);
        if (sim != null && session.combatantId >= 0) {
            // The match goes on: a bot takes over the abandoned combatant.
            sim.combatants()[session.combatantId].brain = new BotBrain(BotFiller.DEFAULT_SKILL);
        }
        session.room = null;
        session.combatantId = -1;
        if (sim == null) {
            broadcastState();
        }
    }

    /** Sets the character and loadout; returns an error message or null. */
    String selectLoadout(Session session, String characterId, List<String> slugIds) {
        CharacterDefinition character = content.characters().find(characterId);
        if (character == null) {
            return "Unknown character '" + characterId + "'";
        }
        if (!slugIds.isEmpty()) {
            List<String> problems = LoadoutValidator.validate(slugIds, content.slugs(),
                    character.blaster().reloadShots(), LoadoutValidator.DEFAULT_MAX_DUPLICATES);
            if (!problems.isEmpty()) {
                return String.join("; ", problems);
            }
        }
        session.characterId = characterId;
        session.slugIds = List.copyOf(slugIds);
        broadcastState();
        return null;
    }

    void setReady(Session session, boolean ready) {
        if (sim != null) {
            return;
        }
        session.ready = ready;
        broadcastState();
        if (!members.isEmpty() && members.stream().allMatch(m -> m.ready)) {
            start();
        }
    }

    // ------------------------------------------------------------------------------------------ match

    private void start() {
        List<Session> ordered = new ArrayList<>(members);
        ordered.sort((a, b) -> Integer.compare(a.team, b.team));
        MatchConfig.Builder builder = MatchConfig.builder(content.maps().find(config.mapId()) != null
                        ? config.mapId() : content.maps().get(0).id())
                .seed(seed + matchesPlayed++)
                .rules(config.rules())
                .fillerSkills(allySkill, rivalSkill);
        List<SlotConfig> humanSlots = new ArrayList<>();
        for (Session session : ordered) {
            SlotConfig slot = SlotConfig.human(session.team, session.characterId, session.slugIds);
            humanSlots.add(slot);
            builder.slot(slot);
        }
        sim = new GameSimulation(content, builder.build());
        nextEventSequence = sim.events().written();
        for (int i = 0; i < ordered.size(); i++) {
            Session session = ordered.get(i);
            session.combatantId = indexOfSlot(humanSlots.get(i));
        }
        for (Session session : ordered) {
            send(session, ServerMessage.newBuilder().setMatchStart(matchStart(session)).build());
        }
        sim.start();
        broadcastState();
    }

    private int indexOfSlot(SlotConfig slot) {
        List<SlotConfig> slots = sim.slots();
        for (int i = 0; i < slots.size(); i++) {
            if (slots.get(i) == slot) {
                return i;
            }
        }
        throw new IllegalStateException("Slot lost while arranging teams");
    }

    private MatchStart matchStart(Session viewer) {
        MatchStart.Builder builder = MatchStart.newBuilder()
                .setMapId(sim.map().id())
                .setYourCombatantId(viewer.combatantId)
                .setMap(SnapshotBuilder.mapData(sim.map(), content));
        for (int i = 0; i < sim.combatants().length; i++) {
            builder.addSlots(slotMessage(i, viewer));
        }
        return builder.build();
    }

    private Slot slotMessage(int combatantId, Session viewer) {
        Combatant c = sim.combatants()[combatantId];
        Session owner = members.stream().filter(m -> m.combatantId == combatantId).findFirst().orElse(null);
        boolean you = owner == viewer;
        Slot.Builder builder = Slot.newBuilder()
                .setCombatantId(combatantId)
                .setTeam(c.team)
                .setCharacterId(c.character.id())
                .setName(owner != null ? owner.name : BOT_NAMES[combatantId % BOT_NAMES.length])
                .setBot(owner == null)
                .setReady(true)
                .setYou(you)
                .setMaxHp((float) c.character.maxHp())
                .setMaxShield((float) c.character.maxShield());
        c.character.moves().forEach(move -> builder.addMoveIds(move.id()));
        if (you) {
            for (int slot = 0; slot < Loadout.SIZE; slot++) {
                builder.addSlugIds(c.loadout.slug(slot).id());
            }
        }
        return builder.build();
    }

    void onInput(Session session, com.bajoterra.protocol.generated.InputFrame frame) {
        session.input.accept(frame);
    }

    /** Advances the room by one tick; sends snapshots when due. */
    void tick() {
        if (sim == null) {
            return;
        }
        for (Session session : members) {
            InputFrame frame = session.input.drain(session.combatantId);
            if (frame != null) {
                sim.submit(frame);
            }
        }
        sim.tick();
        boolean over = sim.match().isOver();
        if (over || sim.tickCount() % SnapshotBuilder.snapshotInterval() == 0) {
            sendSnapshots();
        }
        if (over) {
            finishMatch();
        }
    }

    private void sendSnapshots() {
        Snapshot base = SnapshotBuilder.snapshot(sim);
        SnapshotBuilder.Batch batch = SnapshotBuilder.events(sim, nextEventSequence);
        nextEventSequence = batch.nextSequence();
        ServerMessage events = ServerMessage.newBuilder().setEvents(batch.batch()).build();
        for (Session session : members) {
            Snapshot.Builder personal = base.toBuilder();
            if (session.combatantId >= 0) {
                Combatant own = sim.combatants()[session.combatantId];
                personal.setAckSequence(own.input.sequence).setLocal(SnapshotBuilder.localState(own, sim.tickCount()));
            }
            send(session, ServerMessage.newBuilder().setSnapshot(personal).build());
            send(session, events);
        }
    }

    private void finishMatch() {
        rememberDifficulty();
        MatchEnd end = MatchEnd.newBuilder()
                .setWinner(sim.match().winner())
                .setWinsTeam0(sim.match().wins(0))
                .setWinsTeam1(sim.match().wins(1))
                .build();
        for (Session session : members) {
            send(session, ServerMessage.newBuilder().setMatchEnd(end).build());
            session.ready = false;
            session.combatantId = -1;
        }
        sim = null;
        broadcastState();
    }

    /** When everybody played on one side, the next match starts with the bots as strong as this one ended. */
    private void rememberDifficulty() {
        int team = members.isEmpty() ? -1 : members.get(0).team;
        for (Session session : members) {
            if (session.team != team) {
                return;
            }
        }
        if (team < 0) {
            return;
        }
        double allies = sim.fillerBotSkill(team);
        double rivals = sim.fillerBotSkill(1 - team);
        if (!Double.isNaN(allies)) {
            allySkill = allies;
        }
        if (!Double.isNaN(rivals)) {
            rivalSkill = rivals;
        }
    }

    // ------------------------------------------------------------------------------------------ messages

    void broadcastState() {
        RoomState.Builder state = RoomState.newBuilder().setRoomId(id).setMatchRunning(sim != null);
        for (Session viewer : members) {
            RoomState.Builder personal = state.clone();
            for (Session member : members) {
                personal.addSlots(Slot.newBuilder()
                        .setCombatantId(member.combatantId < 0 ? 0 : member.combatantId)
                        .setTeam(member.team)
                        .setCharacterId(member.characterId)
                        .setName(member.name)
                        .setBot(false)
                        .setReady(member.ready)
                        .setYou(member == viewer));
            }
            send(viewer, ServerMessage.newBuilder().setRoomState(personal).build());
        }
    }

    void sendError(Session session, int code, String message) {
        send(session, ServerMessage.newBuilder()
                .setError(ServerError.newBuilder().setCode(code).setMessage(message)).build());
    }

    private void send(Session session, ServerMessage message) {
        transport.send(session.connectionId, message);
    }

    private int teamSize(int team) {
        int count = 0;
        for (Session member : members) {
            if (member.team == team) {
                count++;
            }
        }
        return count;
    }
}
