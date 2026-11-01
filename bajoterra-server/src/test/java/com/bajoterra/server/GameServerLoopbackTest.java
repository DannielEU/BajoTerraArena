package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static com.bajoterra.server.ServerFixtures.drain;
import static com.bajoterra.server.ServerFixtures.hello;
import static com.bajoterra.server.ServerFixtures.input;
import static com.bajoterra.server.ServerFixtures.join;
import static com.bajoterra.server.ServerFixtures.loadout;
import static com.bajoterra.server.ServerFixtures.quickConfig;
import static com.bajoterra.server.ServerFixtures.ready;
import static com.bajoterra.server.ServerFixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.mapgen.ArenaGenerator;
import com.bajoterra.core.sim.SimulationConstants;
import com.bajoterra.protocol.ProtocolVersion;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.CombatantState;
import com.bajoterra.protocol.generated.MatchPhase;
import com.bajoterra.protocol.generated.MatchStart;
import com.bajoterra.protocol.generated.Ping;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.Snapshot;
import java.io.IOException;
import java.util.List;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

/** The full client protocol, exercised in-process and tick by tick (no sockets, no real time). */
class GameServerLoopbackTest {

    private LoopbackTransport transport;
    private GameServer server;

    @BeforeEach
    void startServer() throws IOException {
        transport = new LoopbackTransport();
        server = new GameServer(CONTENT, transport, quickConfig());
        server.startManual();
    }

    @AfterEach
    void stopServer() {
        server.close();
    }

    private LoopbackTransport.Client greeted(String name) {
        LoopbackTransport.Client client = transport.connect();
        client.send(hello(name));
        server.tickOnce();
        return client;
    }

    private static <T> T last(List<ServerMessage> messages, java.util.function.Predicate<ServerMessage> filter,
            java.util.function.Function<ServerMessage, T> getter) {
        T result = null;
        for (ServerMessage message : messages) {
            if (filter.test(message)) {
                result = getter.apply(message);
            }
        }
        return result;
    }

    private LoopbackTransport.Client inMatch(String name) {
        LoopbackTransport.Client client = greeted(name);
        client.send(join("arena", -1));
        client.send(loadout("eli"));
        client.send(ready(true));
        server.tickOnce();
        return client;
    }

    @Test
    void theWelcomeCarriesTheVersionRatesAndTheContentManifest() {
        LoopbackTransport.Client client = greeted("Eli");

        ServerMessage welcome = drain(client).get(0);

        assertThat(welcome.hasWelcome()).isTrue();
        assertThat(welcome.getWelcome().getProtocolVersion()).isEqualTo(ProtocolVersion.CURRENT);
        assertThat(welcome.getWelcome().getTickRate()).isEqualTo(SimulationConstants.TICKS_PER_SECOND);
        assertThat(welcome.getWelcome().getSnapshotRate()).isEqualTo(SimulationConstants.SNAPSHOT_RATE_HZ);
        assertThat(welcome.getWelcome().getConnectionId()).isEqualTo(client.id());
        assertThat(welcome.getWelcome().getContent().getSlugIdsList())
                .containsExactlyElementsOf(CONTENT.slugs().all().stream().map(s -> s.id()).toList());
        assertThat(welcome.getWelcome().getContent().getMoveIdsList()).contains("jump", "crouch", "roll");
    }

    @Test
    void anIncompatibleClientIsRefusedAndDisconnected() {
        LoopbackTransport.Client client = transport.connect();
        client.send(ClientMessage.newBuilder().setHello(
                com.bajoterra.protocol.generated.Hello.newBuilder().setProtocolVersion(999)).build());
        server.tickOnce();

        List<ServerMessage> messages = drain(client);

        assertThat(messages).hasSize(1);
        assertThat(messages.get(0).getError().getCode()).isEqualTo(GameServer.ERROR_BAD_VERSION);
        assertThat(client.isOpen()).isFalse();
    }

    @Test
    void messagesBeforeHelloAreRejected() {
        LoopbackTransport.Client client = transport.connect();
        client.send(join("arena", 0));
        server.tickOnce();

        assertThat(drain(client).get(0).getError().getCode()).isEqualTo(GameServer.ERROR_NOT_GREETED);
    }

    @Test
    void pingIsAnsweredWithTheSameClientTime() {
        LoopbackTransport.Client client = transport.connect();
        client.send(ClientMessage.newBuilder().setPing(Ping.newBuilder().setClientTime(123_456)).build());
        server.tickOnce();

        assertThat(drain(client).get(0).getPong().getClientTime()).isEqualTo(123_456);
    }

    @Test
    void joiningNeedsAValidRoomIdAndAnUnstartedMatch() {
        LoopbackTransport.Client client = greeted("Eli");
        drain(client);

        client.send(join("bad room!", 0));
        server.tickOnce();

        assertThat(drain(client).get(0).hasError()).isTrue();
        assertThat(server.roomCount()).isZero();
    }

    @Test
    void invalidLoadoutsAndUnknownCharactersAreRejectedWithAnExplanation() {
        LoopbackTransport.Client client = greeted("Eli");
        client.send(join("arena", 0));
        drain(client);

        client.send(loadout("eli", "doc", "doc", "doc"));
        client.send(loadout("nobody"));
        server.tickOnce();

        List<ServerMessage> errors = drain(client).stream().filter(ServerMessage::hasError).toList();
        assertThat(errors).hasSize(2);
        assertThat(errors.get(0).getError().getMessage()).contains("exactly 5");
        assertThat(errors.get(1).getError().getMessage()).contains("Unknown character");
    }

    @Test
    void readyBeforeJoiningARoomIsAnError() {
        LoopbackTransport.Client client = greeted("Eli");
        drain(client);

        client.send(ready(true));
        server.tickOnce();

        assertThat(drain(client).get(0).getError().getCode()).isEqualTo(Room.ERROR_NOT_IN_ROOM);
    }

    @Test
    void whenEveryoneIsReadyTheMatchStartsWithBotsFillingTheTeams() {
        LoopbackTransport.Client client = inMatch("Eli");

        List<ServerMessage> messages = drain(client);

        var start = last(messages, ServerMessage::hasMatchStart, ServerMessage::getMatchStart);
        assertThat(start).isNotNull();
        assertThat(start.getSlotsCount()).isEqualTo(6);
        assertThat(start.getSlotsList().stream().filter(s -> s.getBot()).count()).isEqualTo(5);
        assertThat(start.getSlotsList().stream().filter(s -> s.getYou()).count()).isEqualTo(1);
        var you = start.getSlots(start.getYourCombatantId());
        assertThat(you.getYou()).isTrue();
        assertThat(you.getSlugIdsCount()).as("you see your own five slugs").isEqualTo(5);
        assertThat(you.getMoveIdsList()).containsExactly("jump", "crouch", "roll", "wall_kick");
        assertThat(start.getSlots(start.getYourCombatantId() == 0 ? 3 : 0).getSlugIdsCount())
                .as("others' loadouts stay hidden").isZero();
        assertThat(start.getSlotsList().stream().filter(s -> s.getTeam() == 0).count()).isEqualTo(3);
    }

    @Test
    void snapshotsArriveAtTheSnapshotRateWithTheFullWorld() {
        LoopbackTransport.Client client = inMatch("Eli");
        MatchStart start = last(drain(client), ServerMessage::hasMatchStart, ServerMessage::getMatchStart);

        ticks(server, 60);

        List<Snapshot> snapshots = drain(client).stream().filter(ServerMessage::hasSnapshot)
                .map(ServerMessage::getSnapshot).toList();
        assertThat(snapshots.size()).isBetween(19, 21); // 20 per simulated second
        Snapshot snapshot = snapshots.get(snapshots.size() - 1);
        assertThat(snapshot.getCombatantsCount()).isEqualTo(6);
        for (CombatantState c : snapshot.getCombatantsList()) {
            assertThat(c.getSlotsCount()).isEqualTo(5);
            assertThat(c.getHp()).isPositive();
            assertThat(c.getHitboxHeight()).isPositive();
        }
        assertThat(snapshot.getPickupsCount()).isEqualTo(start.getMap().getPickupsCount()).isPositive();
        for (int i = 1; i < snapshots.size(); i++) {
            assertThat(snapshots.get(i).getTick() - snapshots.get(i - 1).getTick()).isEqualTo(3);
        }
    }

    @Test
    void theMatchRunsThroughCountdownIntoTheFirstRound() {
        LoopbackTransport.Client client = inMatch("Eli");
        drain(client);

        ticks(server, 40);

        Snapshot snapshot = last(drain(client), ServerMessage::hasSnapshot, ServerMessage::getSnapshot);
        assertThat(snapshot.getMatch().getPhase()).isIn(MatchPhase.ROUND_ACTIVE, MatchPhase.COUNTDOWN);
        assertThat(snapshot.getMatch().getRound()).isEqualTo(1);
    }

    @Test
    void inputFramesMoveYourCombatantAndAreAcknowledged() {
        LoopbackTransport.Client client = inMatch("Eli");
        var start = last(drain(client), ServerMessage::hasMatchStart, ServerMessage::getMatchStart);
        int me = start.getYourCombatantId();
        ticks(server, 30); // past the 10-tick countdown
        drain(client);
        Snapshot before = null;
        for (int i = 0; i < 3 && before == null; i++) {
            ticks(server, 3);
            before = last(drain(client), ServerMessage::hasSnapshot, ServerMessage::getSnapshot);
        }
        float startX = before.getCombatants(me).getX();
        float startZ = before.getCombatants(me).getZ();

        // Walk "forward" along the combatant's own facing for one second.
        float yaw = before.getCombatants(me).getYaw();
        client.send(input(41, 0, 1, yaw, false));
        ticks(server, 60);

        Snapshot after = last(drain(client), ServerMessage::hasSnapshot, ServerMessage::getSnapshot);
        CombatantState state = after.getCombatants(me);
        assertThat(Math.hypot(state.getX() - startX, state.getZ() - startZ)).isGreaterThan(3.0);
        assertThat(after.getAckSequence()).isEqualTo(41);
    }

    @Test
    void firingProducesShotEventsAndUsesUpSlots() {
        LoopbackTransport.Client client = inMatch("Eli");
        var start = last(drain(client), ServerMessage::hasMatchStart, ServerMessage::getMatchStart);
        int me = start.getYourCombatantId();
        ticks(server, 30);
        client.send(input(1, 0, 0, 1.0f, true));
        ticks(server, 130);

        List<ServerMessage> messages = drain(client);
        long myShots = messages.stream().filter(ServerMessage::hasEvents)
                .flatMap(m -> m.getEvents().getEventsList().stream())
                .filter(e -> e.getKind() == com.bajoterra.protocol.generated.EventKind.SHOT && e.getActor() == me)
                .count();
        Snapshot snapshot = last(messages, ServerMessage::hasSnapshot, ServerMessage::getSnapshot);
        long locked = snapshot.getCombatants(me).getSlotsList().stream().filter(s -> s.getShotsUntilReady() > 0)
                .count();

        assertThat(myShots).isGreaterThanOrEqualTo(3);
        assertThat(locked).as("fired slugs are locked for the next shots").isBetween(1L, 3L);
    }

    @Test
    void twoHumansOnDifferentTeamsShareOneMatch() {
        LoopbackTransport.Client a = greeted("A");
        LoopbackTransport.Client b = greeted("B");
        a.send(join("duel", 0));
        b.send(join("duel", 1));
        a.send(loadout("eli"));
        b.send(loadout("kord"));
        a.send(ready(true));
        server.tickOnce();
        assertThat(server.roomCount()).isEqualTo(1);
        b.send(ready(true));
        server.tickOnce();

        var startA = last(drain(a), ServerMessage::hasMatchStart, ServerMessage::getMatchStart);
        var startB = last(drain(b), ServerMessage::hasMatchStart, ServerMessage::getMatchStart);

        assertThat(startA.getYourCombatantId()).isNotEqualTo(startB.getYourCombatantId());
        assertThat(startA.getSlots(startA.getYourCombatantId()).getTeam()).isEqualTo(0);
        assertThat(startB.getSlots(startB.getYourCombatantId()).getTeam()).isEqualTo(1);
        assertThat(startB.getSlots(startB.getYourCombatantId()).getCharacterId()).isEqualTo("kord");
        assertThat(startA.getSlotsList().stream().filter(s -> s.getBot()).count()).isEqualTo(4);
    }

    @Test
    void theMatchWaitsUntilEveryHumanIsReady() {
        LoopbackTransport.Client a = greeted("A");
        LoopbackTransport.Client b = greeted("B");
        a.send(join("duel", 0));
        b.send(join("duel", 1));
        a.send(ready(true));
        ticks(server, 20);

        assertThat(drain(a).stream().anyMatch(ServerMessage::hasMatchStart)).isFalse();

        a.send(ready(false));
        b.send(ready(true));
        ticks(server, 5);
        assertThat(drain(b).stream().anyMatch(ServerMessage::hasMatchStart)).isFalse();
    }

    @Test
    void aTeamHoldsAtMostThreeHumans() {
        for (int i = 0; i < 6; i++) {
            LoopbackTransport.Client client = greeted("P" + i);
            client.send(join("full", 0)); // everyone asks for team 0; the overflow is moved to team 1
        }
        server.tickOnce();
        LoopbackTransport.Client seventh = greeted("P7");
        drain(seventh);
        seventh.send(join("full", 0));
        server.tickOnce();

        assertThat(drain(seventh).get(0).getError().getCode()).isEqualTo(Room.ERROR_ROOM_UNAVAILABLE);
    }

    @Test
    void whenAHumanLeavesMidMatchABotTakesOverAndTheMatchContinues() {
        LoopbackTransport.Client a = greeted("A");
        LoopbackTransport.Client b = greeted("B");
        a.send(join("duel", 0));
        b.send(join("duel", 1));
        a.send(ready(true));
        b.send(ready(true));
        server.tickOnce();
        ticks(server, 20);
        var start = last(drain(a), ServerMessage::hasMatchStart, ServerMessage::getMatchStart);
        drain(b);

        a.disconnect();
        ticks(server, 60);

        Snapshot snapshot = last(drain(b), ServerMessage::hasSnapshot, ServerMessage::getSnapshot);
        assertThat(snapshot).as("the remaining player keeps receiving the match").isNotNull();
        assertThat(snapshot.getCombatantsCount()).isEqualTo(6);
        assertThat(start).isNotNull();
        assertThat(server.roomCount()).isEqualTo(1);
    }

    @Test
    void theRoomDisappearsWhenTheLastHumanLeaves() {
        LoopbackTransport.Client client = inMatch("Eli");
        drain(client);

        client.disconnect();
        ticks(server, 2);

        assertThat(server.roomCount()).isZero();
    }

    @Test
    void aFinishedMatchSendsMatchEndAndReturnsToTheLobby() {
        LoopbackTransport.Client client = inMatch("Eli");
        drain(client);

        ServerMessage end = null;
        ServerMessage lobbyState = null;
        for (int i = 0; i < 60 * 60 * 5 && end == null; i++) {
            server.tickOnce();
            for (ServerMessage message : drain(client)) {
                if (message.hasMatchEnd()) {
                    end = message;
                }
                if (message.hasRoomState()) {
                    lobbyState = message; // the lobby state is broadcast right after the match end
                }
            }
        }

        assertThat(end).as("the match finished").isNotNull();
        // The quick rules cap the match at three rounds, so drawn rounds may leave nobody with two wins.
        assertThat(end.getMatchEnd().getWinsTeam0() + end.getMatchEnd().getWinsTeam1()).isBetween(0, 3);
        assertThat(end.getMatchEnd().getWinner()).isBetween(-1, 1);
        assertThat(lobbyState).isNotNull();
        assertThat(lobbyState.getRoomState().getMatchRunning()).isFalse();

        // The same players can start another match.
        client.send(ready(true));
        server.tickOnce();
        assertThat(drain(client).stream().anyMatch(ServerMessage::hasMatchStart)).isTrue();
    }

    @Test
    void everyMatchOfARoomIsPlayedOnANewArena() {
        LoopbackTransport.Client client = inMatch("Eli");
        MatchStart first = last(drain(client), ServerMessage::hasMatchStart,
                ServerMessage::getMatchStart);
        boolean over = false;
        for (int i = 0; i < 60 * 60 * 5 && !over; i++) {
            server.tickOnce();
            over = drain(client).stream().anyMatch(ServerMessage::hasMatchEnd);
        }
        assertThat(over).isTrue();
        client.send(ready(true));
        server.tickOnce();
        MatchStart second = last(drain(client), ServerMessage::hasMatchStart,
                ServerMessage::getMatchStart);

        assertThat(first.getMapId()).isEqualTo(ArenaGenerator.MAP_ID);
        assertThat(second.getMapId()).isEqualTo(ArenaGenerator.MAP_ID);
        assertThat(second.getMap().getObstaclesList()).isNotEqualTo(first.getMap().getObstaclesList());
        assertThat(first.getMap().getName()).isNotBlank();
        assertThat(first.getMap().getSpawnsCount()).isEqualTo(6);
    }

    @Test
    void theServerHonoursTheRoomLimit() throws IOException {
        server.close();
        transport = new LoopbackTransport();
        server = new GameServer(CONTENT, transport,
                new ServerConfig(0, false, ServerFixtures.QUICK_RULES, 1, 1, "caverna"));
        server.startManual();
        LoopbackTransport.Client a = greeted("A");
        LoopbackTransport.Client b = greeted("B");
        a.send(join("one", 0));
        b.send(join("two", 0));
        server.tickOnce();

        assertThat(drain(b).stream().anyMatch(m -> m.hasError()
                && m.getError().getCode() == Room.ERROR_ROOM_UNAVAILABLE)).isTrue();
        assertThat(server.roomCount()).isEqualTo(1);
    }
}
