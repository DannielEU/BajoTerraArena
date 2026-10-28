package com.bajoterra.server;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.protocol.ProtocolVersion;
import com.bajoterra.protocol.Transport;
import com.bajoterra.protocol.TransportListener;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.Hello;
import com.bajoterra.protocol.generated.JoinRoom;
import com.bajoterra.protocol.generated.Pong;
import com.bajoterra.protocol.generated.ServerError;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.Welcome;
import java.io.IOException;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.regex.Pattern;

/**
 * The authoritative game server (Facade over transport, rooms and scheduler).
 *
 * <p>Threading: the transport delivers events on its I/O thread, but they are only queued; every piece of game
 * state is touched by the single scheduler thread, which drains that queue and then ticks the rooms. The
 * simulation therefore needs no locks.
 */
public final class GameServer implements TransportListener, AutoCloseable {

    static final int ERROR_BAD_VERSION = 1;
    static final int ERROR_NOT_GREETED = 2;
    static final int ERROR_BAD_MESSAGE = 7;

    private static final Pattern ROOM_ID = Pattern.compile("[A-Za-z0-9_-]{1,32}");

    private final Content content;
    private final Transport transport;
    private final ServerConfig config;
    private final Queue<Runnable> inbox = new ConcurrentLinkedQueue<>();
    private final Map<Integer, Session> sessions = new HashMap<>();
    private final Map<String, Room> rooms = new HashMap<>();
    private final ServerMessage welcomeTemplate;
    private TickScheduler scheduler;

    public GameServer(Content content, Transport transport, ServerConfig config) {
        this.content = content;
        this.transport = transport;
        this.config = config;
        this.welcomeTemplate = ServerMessage.newBuilder().setWelcome(Welcome.newBuilder()
                .setProtocolVersion(ProtocolVersion.CURRENT)
                .setTickRate(SimulationConstants.TICKS_PER_SECOND)
                .setSnapshotRate(SimulationConstants.SNAPSHOT_RATE_HZ)
                .setContent(SnapshotBuilder.manifest(content))
                .setDetails(SnapshotBuilder.details(content))).build();
    }

    /** Starts the transport and the tick thread. */
    public void start() throws IOException {
        transport.setListener(this);
        transport.start();
        scheduler = new TickScheduler(this::tickOnce, config.realTime(), "bajoterra-sim");
        scheduler.start();
    }

    /** Starts only the transport; the caller drives time with {@link #tickOnce()} (deterministic tests). */
    public void startManual() throws IOException {
        transport.setListener(this);
        transport.start();
    }

    @Override
    public void close() {
        if (scheduler != null) {
            scheduler.close();
        }
        transport.close();
    }

    /** The scheduler, for monitoring tick times; null in manual mode. */
    public TickScheduler scheduler() {
        return scheduler;
    }

    /** Runs one server iteration: handles queued network events, then advances every running room by a tick. */
    public void tickOnce() {
        Runnable task;
        while ((task = inbox.poll()) != null) {
            task.run();
        }
        Iterator<Map.Entry<String, Room>> it = rooms.entrySet().iterator();
        while (it.hasNext()) {
            Room room = it.next().getValue();
            room.tick();
            if (room.isEmpty()) {
                it.remove();
            }
        }
    }

    /** Number of rooms currently open (for tests and monitoring). */
    public int roomCount() {
        return rooms.size();
    }

    // ------------------------------------------------------------------------------------------ transport

    @Override
    public void onConnected(int connectionId) {
        inbox.add(() -> sessions.put(connectionId, new Session(connectionId)));
    }

    @Override
    public void onMessage(int connectionId, ClientMessage message) {
        inbox.add(() -> handle(connectionId, message));
    }

    @Override
    public void onDisconnected(int connectionId) {
        inbox.add(() -> {
            Session session = sessions.remove(connectionId);
            if (session != null && session.room != null) {
                session.room.leave(session);
            }
        });
    }

    // ------------------------------------------------------------------------------------------ handlers

    private void handle(int connectionId, ClientMessage message) {
        Session session = sessions.get(connectionId);
        if (session == null) {
            return;
        }
        if (message.getBodyCase() == ClientMessage.BodyCase.HELLO) {
            hello(session, message.getHello());
            return;
        }
        if (message.getBodyCase() == ClientMessage.BodyCase.PING) {
            transport.send(connectionId, ServerMessage.newBuilder().setPong(Pong.newBuilder()
                    .setClientTime(message.getPing().getClientTime())
                    .setServerTick(scheduler == null ? 0 : (int) scheduler.ticks())).build());
            return;
        }
        if (!session.greeted) {
            error(session, ERROR_NOT_GREETED, "Send Hello first");
            return;
        }
        switch (message.getBodyCase()) {
            case JOIN_ROOM -> joinRoom(session, message.getJoinRoom());
            case SELECT_LOADOUT -> {
                if (requireRoom(session)) {
                    String problem = session.room.selectLoadout(session, message.getSelectLoadout().getCharacterId(),
                            message.getSelectLoadout().getSlugIdsList());
                    if (problem != null) {
                        error(session, Room.ERROR_INVALID_LOADOUT, problem);
                    }
                }
            }
            case READY -> {
                if (requireRoom(session)) {
                    session.room.setReady(session, message.getReady().getReady());
                }
            }
            case INPUT -> {
                if (session.room != null) {
                    session.room.onInput(session, message.getInput());
                }
            }
            default -> error(session, ERROR_BAD_MESSAGE, "Unexpected message " + message.getBodyCase());
        }
    }

    private void hello(Session session, Hello hello) {
        if (hello.getProtocolVersion() != ProtocolVersion.CURRENT) {
            error(session, ERROR_BAD_VERSION, "Protocol version " + hello.getProtocolVersion()
                    + " is not supported; the server speaks " + ProtocolVersion.CURRENT);
            transport.disconnect(session.connectionId);
            return;
        }
        session.greeted = true;
        session.name = hello.getPlayerName().isBlank() ? "player" + session.connectionId : hello.getPlayerName();
        transport.send(session.connectionId, ServerMessage.newBuilder(welcomeTemplate).setWelcome(
                welcomeTemplate.getWelcome().toBuilder().setConnectionId(session.connectionId)).build());
    }

    private void joinRoom(Session session, JoinRoom join) {
        if (session.room != null) {
            error(session, Room.ERROR_ROOM_UNAVAILABLE, "Already in room " + session.room.id());
            return;
        }
        String roomId = join.getRoomId().isBlank() ? "lobby" : join.getRoomId();
        if (!ROOM_ID.matcher(roomId).matches()) {
            error(session, ERROR_BAD_MESSAGE, "Room ids use letters, digits, '-' and '_' (max 32)");
            return;
        }
        Room room = rooms.get(roomId);
        if (room == null) {
            if (rooms.size() >= config.maxRooms()) {
                error(session, Room.ERROR_ROOM_UNAVAILABLE, "The server has no free rooms");
                return;
            }
            room = new Room(roomId, content, config, transport);
            rooms.put(roomId, room);
        }
        String problem = room.join(session, join.getTeam());
        if (problem != null) {
            error(session, Room.ERROR_ROOM_UNAVAILABLE, problem);
        }
    }

    private boolean requireRoom(Session session) {
        if (session.room == null) {
            error(session, Room.ERROR_NOT_IN_ROOM, "Join a room first");
            return false;
        }
        return true;
    }

    private void error(Session session, int code, String message) {
        transport.send(session.connectionId, ServerMessage.newBuilder()
                .setError(ServerError.newBuilder().setCode(code).setMessage(message)).build());
    }
}
