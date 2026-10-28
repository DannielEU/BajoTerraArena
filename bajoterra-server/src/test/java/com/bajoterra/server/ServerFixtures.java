package com.bajoterra.server;

import com.bajoterra.core.content.Content;
import com.bajoterra.core.rules.EliminationRoundsRules;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.Hello;
import com.bajoterra.protocol.generated.InputFrame;
import com.bajoterra.protocol.generated.JoinRoom;
import com.bajoterra.protocol.generated.Ready;
import com.bajoterra.protocol.generated.SelectLoadout;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.ProtocolVersion;
import java.util.ArrayList;
import java.util.List;

/** Shared helpers for the server tests. */
final class ServerFixtures {

    static final Content CONTENT = Content.load();

    /** Short rounds so whole matches finish within a few thousand ticks. */
    static final EliminationRoundsRules QUICK_RULES = new EliminationRoundsRules(10, 240, 10, 2, 3);

    private ServerFixtures() {
        throw new AssertionError("Utility class, not instantiable");
    }

    static ServerConfig quickConfig() {
        return ServerConfig.defaults(0).withRealTime(false).withRules(QUICK_RULES).withSeed(5);
    }

    static ClientMessage hello(String name) {
        return ClientMessage.newBuilder().setHello(
                Hello.newBuilder().setProtocolVersion(ProtocolVersion.CURRENT).setPlayerName(name)).build();
    }

    static ClientMessage join(String room, int team) {
        return ClientMessage.newBuilder().setJoinRoom(JoinRoom.newBuilder().setRoomId(room).setTeam(team)).build();
    }

    static ClientMessage loadout(String character, String... slugs) {
        return ClientMessage.newBuilder().setSelectLoadout(
                SelectLoadout.newBuilder().setCharacterId(character).addAllSlugIds(List.of(slugs))).build();
    }

    static ClientMessage ready(boolean ready) {
        return ClientMessage.newBuilder().setReady(Ready.newBuilder().setReady(ready)).build();
    }

    static ClientMessage input(int sequence, float moveX, float moveZ, float yaw, boolean fire) {
        return ClientMessage.newBuilder().setInput(InputFrame.newBuilder().setSequence(sequence)
                .setMoveX(moveX).setMoveZ(moveZ).setAimYaw(yaw).setFire(fire).setSelectSlot(-1)).build();
    }

    /** Drains everything a loopback client has received. */
    static List<ServerMessage> drain(LoopbackTransport.Client client) {
        List<ServerMessage> messages = new ArrayList<>();
        ServerMessage message;
        while ((message = client.poll()) != null) {
            messages.add(message);
        }
        return messages;
    }

    static void ticks(GameServer server, int count) {
        for (int i = 0; i < count; i++) {
            server.tickOnce();
        }
    }
}
