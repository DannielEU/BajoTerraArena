package com.bajoterra.server;

import com.bajoterra.protocol.FrameCodec;
import com.bajoterra.protocol.ProtocolVersion;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.CombatantState;
import com.bajoterra.protocol.generated.EventBatch;
import com.bajoterra.protocol.generated.GameEvent;
import com.bajoterra.protocol.generated.Hello;
import com.bajoterra.protocol.generated.InputFrame;
import com.bajoterra.protocol.generated.JoinRoom;
import com.bajoterra.protocol.generated.MatchEnd;
import com.bajoterra.protocol.generated.MatchPhase;
import com.bajoterra.protocol.generated.MatchStart;
import com.bajoterra.protocol.generated.Ready;
import com.bajoterra.protocol.generated.RoomState;
import com.bajoterra.protocol.generated.SelectLoadout;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.Slot;
import com.bajoterra.protocol.generated.Snapshot;
import com.bajoterra.protocol.generated.Welcome;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * A scripted game client that speaks the real wire protocol over TCP. It stands in for the Unity client in demos and
 * integration tests: it joins a room, picks a loadout, gets ready and then pilots its combatant from snapshots only
 * (turn towards the nearest enemy, close in, strafe and fire).
 */
public final class HeadlessClient implements AutoCloseable {

    private final Socket socket;
    private final OutputStream out;
    private final Thread reader;
    private final CompletableFuture<MatchEnd> matchEnd = new CompletableFuture<>();
    private final AtomicInteger snapshots = new AtomicInteger();
    private final AtomicInteger eventsReceived = new AtomicInteger();
    private final AtomicInteger shotEvents = new AtomicInteger();
    private final AtomicInteger errors = new AtomicInteger();
    private volatile String lastError = "";
    private volatile MatchStart matchStart;
    private volatile RoomState roomState;
    private volatile Welcome welcome;
    private volatile Snapshot lastSnapshot;
    private volatile boolean autopilot = true;
    private int sequence;

    public HeadlessClient(String host, int port, String name) throws IOException {
        this.socket = new Socket(host, port);
        this.socket.setTcpNoDelay(true);
        this.out = socket.getOutputStream();
        this.reader = new Thread(this::readLoop, "headless-client-" + name);
        this.reader.setDaemon(true);
        this.reader.start();
        send(ClientMessage.newBuilder().setHello(
                Hello.newBuilder().setProtocolVersion(ProtocolVersion.CURRENT).setPlayerName(name)).build());
    }

    /** Joins a room and, when {@code characterId} is not null, picks a character with a generated loadout. */
    public void join(String roomId, int team, String characterId) throws IOException {
        send(ClientMessage.newBuilder().setJoinRoom(JoinRoom.newBuilder().setRoomId(roomId).setTeam(team)).build());
        if (characterId != null) {
            send(ClientMessage.newBuilder().setSelectLoadout(
                    SelectLoadout.newBuilder().setCharacterId(characterId)).build());
        }
    }

    /** Tells the server this player is ready; the match starts once every human in the room is. */
    public void ready() throws IOException {
        send(ClientMessage.newBuilder().setReady(Ready.newBuilder().setReady(true)).build());
    }

    /** Joins a room and immediately gets ready (enough for a solo player). */
    public void play(String roomId, int team, String characterId) throws IOException {
        join(roomId, team, characterId);
        ready();
    }

    /** The server's greeting, or null until it arrives. */
    public Welcome welcome() {
        return welcome;
    }

    /** The newest lobby state received, or null. */
    public RoomState lastRoomState() {
        return roomState;
    }

    public void send(ClientMessage message) throws IOException {
        ByteBuffer frame = FrameCodec.encode(message);
        synchronized (out) {
            out.write(frame.array(), frame.arrayOffset(), frame.remaining());
            out.flush();
        }
    }

    /** Completes when the server announces the end of the match. */
    public CompletableFuture<MatchEnd> matchEnd() {
        return matchEnd;
    }

    public MatchStart matchStart() {
        return matchStart;
    }

    public Snapshot lastSnapshot() {
        return lastSnapshot;
    }

    public int snapshotsReceived() {
        return snapshots.get();
    }

    public int eventsReceived() {
        return eventsReceived.get();
    }

    public int shotEventsReceived() {
        return shotEvents.get();
    }

    public int errorsReceived() {
        return errors.get();
    }

    public String lastError() {
        return lastError;
    }

    /** Disables the scripted pilot (the client then only listens). */
    public void setAutopilot(boolean enabled) {
        this.autopilot = enabled;
    }

    @Override
    public void close() {
        try {
            socket.close();
        } catch (IOException e) {
            // Already closed.
        }
    }

    // ------------------------------------------------------------------------------------------ reading

    private void readLoop() {
        try (InputStream in = socket.getInputStream()) {
            ByteBuffer buffer = ByteBuffer.allocate(2 * FrameCodec.MAX_FRAME_BYTES);
            byte[] chunk = new byte[16 * 1024];
            int count;
            while ((count = in.read(chunk)) >= 0) {
                buffer.put(chunk, 0, count);
                buffer.flip();
                byte[] payload;
                while ((payload = FrameCodec.decodeNext(buffer)) != null) {
                    handle(ServerMessage.parseFrom(payload));
                }
                buffer.compact();
            }
        } catch (IOException e) {
            // The connection ended; the match future stays incomplete unless MatchEnd arrived.
        }
    }

    private void handle(ServerMessage message) throws IOException {
        switch (message.getBodyCase()) {
            case MATCH_START -> matchStart = message.getMatchStart();
            case ROOM_STATE -> roomState = message.getRoomState();
            case WELCOME -> welcome = message.getWelcome();
            case SNAPSHOT -> {
                lastSnapshot = message.getSnapshot();
                snapshots.incrementAndGet();
                if (autopilot && matchStart != null) {
                    pilot(message.getSnapshot());
                }
            }
            case EVENTS -> countEvents(message.getEvents());
            case MATCH_END -> matchEnd.complete(message.getMatchEnd());
            case ERROR -> {
                errors.incrementAndGet();
                lastError = message.getError().getMessage();
            }
            default -> {
                // Welcome, room state and pong need no reaction.
            }
        }
    }

    private void countEvents(EventBatch batch) {
        eventsReceived.addAndGet(batch.getEventsCount());
        for (GameEvent event : batch.getEventsList()) {
            if (event.getKind() == com.bajoterra.protocol.generated.EventKind.SHOT
                    && event.getActor() == matchStart.getYourCombatantId()) {
                shotEvents.incrementAndGet();
            }
        }
    }

    // ------------------------------------------------------------------------------------------ piloting

    /** Computes one input frame from the snapshot: face the nearest enemy, keep a mid distance and shoot. */
    private void pilot(Snapshot snapshot) throws IOException {
        if (snapshot.getMatch().getPhase() != MatchPhase.ROUND_ACTIVE) {
            return;
        }
        int me = matchStart.getYourCombatantId();
        List<Slot> slots = matchStart.getSlotsList();
        CombatantState self = null;
        CombatantState target = null;
        double best = Double.MAX_VALUE;
        for (CombatantState c : snapshot.getCombatantsList()) {
            if (c.getId() == me) {
                self = c;
            }
        }
        if (self == null || !self.getAlive()) {
            return;
        }
        int myTeam = slots.get(me).getTeam();
        for (CombatantState c : snapshot.getCombatantsList()) {
            if (c.getAlive() && slots.get(c.getId()).getTeam() != myTeam) {
                double distance = Math.hypot(c.getX() - self.getX(), c.getZ() - self.getZ());
                if (distance < best) {
                    best = distance;
                    target = c;
                }
            }
        }
        if (target == null) {
            return;
        }
        double dx = target.getX() - self.getX();
        double dz = target.getZ() - self.getZ();
        double yaw = Math.atan2(dx, dz);
        double pitch = Math.atan2(target.getY() + 1.0 - (self.getY() + 1.3), best) + 0.012 * best / 10;
        float forward = best > 14 ? 1f : (best < 8 ? -1f : 0f);
        float strafe = (snapshot.getTick() / 90) % 2 == 0 ? 1f : -1f;
        send(ClientMessage.newBuilder().setInput(InputFrame.newBuilder()
                .setSequence(++sequence)
                .setMoveX(strafe * 0.7f).setMoveZ(forward)
                .setAimYaw((float) yaw).setAimPitch((float) pitch)
                .setFire(true).setSelectSlot(-1)).build());
    }
}
