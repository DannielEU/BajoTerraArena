package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.core.rules.EliminationRoundsRules;
import com.bajoterra.protocol.FrameCodec;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.Hello;
import com.bajoterra.protocol.generated.MatchEnd;
import com.bajoterra.protocol.generated.ServerMessage;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

/** Real sockets, the real scheduler thread and scripted clients: a whole match over TCP. */
class TcpServerIntegrationTest {

    private TcpTransport transport;
    private GameServer server;

    @BeforeEach
    void startServer() throws IOException {
        transport = new TcpTransport(0);
        // Fast mode (no sleeping) so whole matches finish in well under a second of wall time.
        server = new GameServer(CONTENT, transport, ServerConfig.defaults(0).withRealTime(false)
                .withRules(new EliminationRoundsRules(20, 600, 20, 2, 3)).withSeed(11));
        server.start();
    }

    @AfterEach
    void stopServer() {
        server.close();
    }

    @Test
    void aScriptedClientPlaysAWholeMatchOverTcp() throws Exception {
        try (HeadlessClient client = new HeadlessClient("127.0.0.1", transport.port(), "Eli")) {
            client.play("tcp-room", 0, "eli");

            MatchEnd end = client.matchEnd().get(60, TimeUnit.SECONDS);

            assertThat(client.errorsReceived()).as("server errors: %s", client.lastError()).isZero();
            assertThat(client.matchStart()).isNotNull();
            assertThat(client.matchStart().getSlotsCount()).isEqualTo(6);
            assertThat(client.snapshotsReceived()).isGreaterThan(50);
            assertThat(client.eventsReceived()).isGreaterThan(0);
            assertThat(client.shotEventsReceived()).as("the scripted pilot fired its blaster").isGreaterThan(0);
            assertThat(end.getWinsTeam0() + end.getWinsTeam1()).isBetween(0, 3);
        }
    }

    @Test
    void twoClientsPlayAgainstEachOtherOnOppositeTeams() throws Exception {
        try (HeadlessClient a = new HeadlessClient("127.0.0.1", transport.port(), "A");
                HeadlessClient b = new HeadlessClient("127.0.0.1", transport.port(), "B")) {
            a.join("versus", 0, "eli");
            b.join("versus", 1, "pronto");
            awaitMembers(a, 2);
            awaitMembers(b, 2);
            a.ready();
            b.ready();

            MatchEnd endA = a.matchEnd().get(60, TimeUnit.SECONDS);
            MatchEnd endB = b.matchEnd().get(60, TimeUnit.SECONDS);

            assertThat(endB).isEqualTo(endA);
            assertThat(a.matchStart().getYourCombatantId()).isNotEqualTo(b.matchStart().getYourCombatantId());
            assertThat(a.shotEventsReceived()).isGreaterThan(0);
            assertThat(b.shotEventsReceived()).isGreaterThan(0);
        }
    }

    @Test
    void anIncompatibleClientReceivesTheExplanationBeforeTheServerHangsUp() throws Exception {
        try (Socket socket = new Socket("127.0.0.1", transport.port())) {
            socket.setSoTimeout(5_000);
            ByteBuffer hello = FrameCodec.encode(ClientMessage.newBuilder()
                    .setHello(Hello.newBuilder().setProtocolVersion(99).setPlayerName("old")).build());
            socket.getOutputStream().write(hello.array(), hello.arrayOffset(), hello.remaining());
            socket.getOutputStream().flush();

            byte[] everything = socket.getInputStream().readAllBytes();

            ByteBuffer buffer = ByteBuffer.wrap(everything);
            byte[] payload = FrameCodec.decodeNext(buffer);
            assertThat(payload).as("the error frame arrived before the connection closed").isNotNull();
            ServerMessage message = ServerMessage.parseFrom(payload);
            assertThat(message.getError().getCode()).isEqualTo(GameServer.ERROR_BAD_VERSION);
            assertThat(FrameCodec.decodeNext(buffer)).as("and nothing else followed").isNull();
        }
    }

    private static void awaitMembers(HeadlessClient client, int members) throws InterruptedException {
        long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10);
        while (System.nanoTime() < deadline) {
            if (client.lastRoomState() != null && client.lastRoomState().getSlotsCount() == members) {
                return;
            }
            Thread.sleep(5);
        }
        throw new AssertionError("The room never reached " + members + " members");
    }

    @Test
    void garbageOnTheSocketClosesTheConnectionWithoutHurtingTheServer() throws Exception {
        try (Socket socket = new Socket("127.0.0.1", transport.port())) {
            OutputStream out = socket.getOutputStream();
            out.write(ByteBuffer.allocate(8).putInt(FrameCodec.MAX_FRAME_BYTES + 100).putInt(0).array());
            out.flush();
            InputStream in = socket.getInputStream();
            socket.setSoTimeout(5_000);

            assertThat(in.read()).as("the server closed the connection").isEqualTo(-1);
        }

        try (HeadlessClient client = new HeadlessClient("127.0.0.1", transport.port(), "Healthy")) {
            client.play("after-garbage", 0, "eli");
            assertThat(client.matchEnd().get(60, TimeUnit.SECONDS)).isNotNull();
        }
    }

    @Test
    void theSchedulerKeepsTickTimesFarBelowTheFrameBudget() throws Exception {
        try (HeadlessClient client = new HeadlessClient("127.0.0.1", transport.port(), "Timer")) {
            client.play("timing", 0, "eli");
            client.matchEnd().get(60, TimeUnit.SECONDS);
        }

        double meanMillis = server.scheduler().meanTickNanos() / 1e6;
        assertThat(server.scheduler().ticks()).isPositive();
        assertThat(meanMillis).as("mean server iteration in ms (budget 16.7)").isLessThan(2.0);
    }
}
