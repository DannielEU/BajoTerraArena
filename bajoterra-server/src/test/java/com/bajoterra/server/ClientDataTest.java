package com.bajoterra.server;

import static com.bajoterra.server.ServerFixtures.CONTENT;
import static com.bajoterra.server.ServerFixtures.drain;
import static com.bajoterra.server.ServerFixtures.hello;
import static com.bajoterra.server.ServerFixtures.join;
import static com.bajoterra.server.ServerFixtures.loadout;
import static com.bajoterra.server.ServerFixtures.quickConfig;
import static com.bajoterra.server.ServerFixtures.ready;
import static com.bajoterra.server.ServerFixtures.ticks;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.within;

import com.bajoterra.core.entity.Combatant;
import com.bajoterra.core.sim.GameSimulation;
import com.bajoterra.core.sim.InputFrame;
import com.bajoterra.core.sim.MatchConfig;
import com.bajoterra.core.sim.SlotConfig;
import com.bajoterra.protocol.generated.ContentDetails;
import com.bajoterra.protocol.generated.LocalState;
import com.bajoterra.protocol.generated.MapData;
import com.bajoterra.protocol.generated.MoveInfo;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.Targeting;
import java.io.IOException;
import java.util.List;
import org.junit.jupiter.api.Test;

/** The numeric content, map geometry and local state that the client needs to draw and to predict. */
class ClientDataTest {

    /** A match with one human (team 0) so that nothing but the test drives the first combatant. */
    private static GameSimulation runningSim() {
        GameSimulation sim = new GameSimulation(CONTENT, MatchConfig.builder("caverna").seed(11)
                .slot(SlotConfig.human(0, "eli", List.of())).build());
        sim.start();
        for (int i = 0; i < 400; i++) {
            sim.tick();
        }
        return sim;
    }

    @Test
    void contentDetailsAreAlignedWithTheManifestByIndex() {
        var manifest = SnapshotBuilder.manifest(CONTENT);
        ContentDetails details = SnapshotBuilder.details(CONTENT);

        assertThat(details.getSlugsList()).extracting(s -> s.getId())
                .containsExactlyElementsOf(manifest.getSlugIdsList());
        assertThat(details.getCharactersList()).extracting(c -> c.getId())
                .containsExactlyElementsOf(manifest.getCharacterIdsList());
        assertThat(details.getMovesList()).extracting(m -> m.getId())
                .containsExactlyElementsOf(manifest.getMoveIdsList());
        assertThat(details.getPickupsList()).extracting(p -> p.getId())
                .containsExactlyElementsOf(manifest.getPickupIdsList());
        assertThat(manifest.getElementIdsList()).contains("NEUTRAL", "FIRE", "WATER", "LIGHT");
    }

    @Test
    void characterMoveIndicesFollowTheMoveMaskBitOrder() {
        ContentDetails details = SnapshotBuilder.details(CONTENT);

        var pronto = details.getCharacters(CONTENT.characters().require("pronto").index());

        assertThat(pronto.getMoveIndicesList().stream().map(i -> details.getMoves(i).getId()).toList())
                .containsExactly("jump", "crouch", "roll", "dash", "wall_kick");
        assertThat(pronto.getSpeed()).isEqualTo(7.0f);
        assertThat(pronto.getHitHeight()).isEqualTo(1.15f);
        assertThat(pronto.getFireIntervalTicks()).isEqualTo(42);
        assertThat(pronto.getReloadShots()).isEqualTo(3);
    }

    @Test
    void movesCarryTheNumbersTheClientNeedsToPredictThem() {
        ContentDetails details = SnapshotBuilder.details(CONTENT);

        MoveInfo jump = details.getMoves(CONTENT.moves().require("jump").index());
        MoveInfo roll = details.getMoves(CONTENT.moves().require("roll").index());
        MoveInfo crouch = details.getMoves(CONTENT.moves().require("crouch").index());

        assertThat(jump.getBehaviorType()).isEqualTo("impulse");
        assertThat(jump.getBehaviorParamsMap()).containsEntry("vertical", 8.5f);
        assertThat(roll.getBehaviorType()).isEqualTo("drive");
        assertThat(roll.getBehaviorParamsMap()).containsEntry("speed", 8.0f);
        assertThat(roll.getDurationTicks()).isEqualTo(30);
        assertThat(roll.getBlocksFiring()).isTrue();
        assertThat(roll.getInvulnerableFromTick()).isEqualTo(3);
        assertThat(roll.getInvulnerableToTick()).isEqualTo(21);
        assertThat(crouch.getHoldable()).isTrue();
        assertThat(crouch.getStance()).isTrue();
        assertThat(crouch.getBehaviorType()).isEmpty();
        assertThat(crouch.getAllowedFromMask()).isEqualTo(1);
    }

    @Test
    void slugInfoTellsWhoEachSlugAffects() {
        ContentDetails details = SnapshotBuilder.details(CONTENT);

        var doc = details.getSlugs(CONTENT.slugs().require("doc").index());
        var infurnus = details.getSlugs(CONTENT.slugs().require("infurnus").index());

        assertThat(doc.getTargeting()).isEqualTo(Targeting.ALLIES);
        assertThat(doc.getRole()).isEqualTo("SUPPORT");
        assertThat(infurnus.getTargeting()).isEqualTo(Targeting.ENEMIES);
        assertThat(infurnus.getName()).isNotBlank();
        assertThat(infurnus.getProjectileRadius()).isGreaterThan(0f);
    }

    @Test
    void mapDataDescribesEveryObstaclePickupAndSpawn() {
        var map = CONTENT.maps().require("caverna");

        MapData data = SnapshotBuilder.mapData(map, CONTENT);

        assertThat(data.getObstaclesCount()).isEqualTo(map.obstacleCount());
        assertThat(data.getPickupsCount()).isEqualTo(map.pickups().size());
        assertThat(data.getSpawnsCount()).isEqualTo(6);
        assertThat(data.getMinX()).isEqualTo(-30f);
        assertThat(data.getMaxZ()).isEqualTo(20f);
        for (int i = 0; i < data.getPickupsCount(); i++) {
            assertThat(CONTENT.pickups().get(data.getPickups(i).getPickupIndex()).id())
                    .isEqualTo(map.pickups().get(i).pickupId());
        }
        var wall = data.getObstaclesList().stream().filter(o -> o.getId().equals("center_wall_n")).findFirst()
                .orElseThrow();
        assertThat(wall.getMaxY()).isEqualTo(4f);
        assertThat(wall.getBlocksProjectiles()).isTrue();
        assertThat(wall.getBlocksMovement()).isTrue();
    }

    @Test
    void localStateMirrorsTheMovementStateOfTheCombatant() {
        GameSimulation sim = runningSim();
        Combatant c = sim.combatants()[0];
        assertThat(c.brain).as("the first combatant is the human").isNull();
        // Request a jump so that there is an active move, a cooldown and vertical velocity.
        sim.submit(new InputFrame(0, 1, 0f, 0f, 0f, 0f, false, 0f, 0f, 0f, false, 1, -1, 0));
        sim.tick();
        sim.tick();

        LocalState local = SnapshotBuilder.localState(c, sim.tickCount());

        assertThat(local.getVelY()).isEqualTo((float) c.vel.y);
        assertThat(local.getGrounded()).isEqualTo(c.grounded).isFalse();
        assertThat(local.getMoveCooldownTicksCount()).isEqualTo(c.character.moves().size());
        assertThat(local.getMoveCooldownTicks(0)).isBetween(1, 21);
        assertThat(local.getStatusSpeedMultiplier()).isEqualTo(1f);
        assertThat(local.getStunned()).isFalse();
        assertThat(local.getMoveDirZ()).isCloseTo((float) c.moves.dirZ(), within(1e-6f));
    }

    @Test
    void theWelcomeTheMatchStartAndTheOwnSnapshotCarryTheClientData() throws IOException {
        LoopbackTransport transport = new LoopbackTransport();
        GameServer server = new GameServer(CONTENT, transport, quickConfig());
        server.startManual();
        try {
            LoopbackTransport.Client client = transport.connect();
            client.send(hello("Eli"));
            server.tickOnce();
            client.send(join("arena", -1));
            client.send(loadout("eli"));
            client.send(ready(true));
            ticks(server, 12);

            List<ServerMessage> messages = drain(client);

            ServerMessage welcome = messages.stream().filter(ServerMessage::hasWelcome).findFirst().orElseThrow();
            ServerMessage start = messages.stream().filter(ServerMessage::hasMatchStart).findFirst().orElseThrow();
            ServerMessage snapshot = messages.stream().filter(ServerMessage::hasSnapshot).findFirst().orElseThrow();
            assertThat(welcome.getWelcome().getDetails().getMovesCount()).isEqualTo(CONTENT.moves().size());
            assertThat(start.getMatchStart().getMap().getObstaclesCount()).isGreaterThan(10);
            assertThat(snapshot.getSnapshot().hasLocal()).isTrue();
            assertThat(snapshot.getSnapshot().getLocal().getMoveCooldownTicksCount()).isEqualTo(4);
        } finally {
            server.close();
        }
    }
}
