package com.bajoterra.protocol;

import static org.assertj.core.api.Assertions.assertThat;

import com.bajoterra.protocol.generated.CombatantState;
import com.bajoterra.protocol.generated.EventKind;
import com.bajoterra.protocol.generated.InputFrame;
import com.bajoterra.protocol.generated.LocalState;
import com.bajoterra.protocol.generated.MatchInfo;
import com.bajoterra.protocol.generated.MatchPhase;
import com.bajoterra.protocol.generated.MoveInfo;
import com.bajoterra.protocol.generated.Posture;
import com.bajoterra.protocol.generated.ProjectileState;
import com.bajoterra.protocol.generated.ServerMessage;
import com.bajoterra.protocol.generated.SlotState;
import com.bajoterra.protocol.generated.Snapshot;
import com.google.protobuf.InvalidProtocolBufferException;
import java.util.HexFormat;
import org.junit.jupiter.api.Test;

/**
 * Golden bytes of representative messages. If one of these tests fails, the wire format changed: an existing Unity
 * client would break. Either undo the change or bump {@link ProtocolVersion#CURRENT} and update the constants.
 */
class ProtocolContractTest {

    private static final String GOLDEN_INPUT_HEX =
            "084d150000003f1d0000803f250000c03f2d000080be30013d0000803f450000"
            + "00404d000040405001580560066801";
    private static final String GOLDEN_SNAPSHOT_HEX =
            "225b08b009104d1a0b0802108827180220013801222d15000020c12500000040"
            + "2dc3f5c83f3d0000a042450000c8415001580260026804720408031002850171"
            + "3d8a3f2a1808091003250000803f2d6666a63f35000000403d00003442";

    private static final String GOLDEN_LOCAL_HEX = "0d00000841150000c03f20012d0000003f38034d0000803f5203001548";
    private static final String GOLDEN_MOVE_HEX =
            "0a046a756d7010011815300140054d0000803f550000803f6a07696d70756c7365720f0a08"
            + "766572746963616c1500000841";

    private static LocalState sampleLocal() {
        return LocalState.newBuilder().setVelY(8.5f).setKnockX(1.5f).setGrounded(true).setStatusSpeedMultiplier(0.5f)
                .setMoveElapsedTicks(3).setMoveDirZ(1f).addMoveCooldownTicks(0).addMoveCooldownTicks(21)
                .addMoveCooldownTicks(72).build();
    }

    private static MoveInfo sampleMove() {
        return MoveInfo.newBuilder().setId("jump").setDurationTicks(1).setCooldownTicks(21).setInterruptible(true)
                .setAllowedFromMask(5).setSpeedMultiplier(1f).setHitboxHeightMultiplier(1f)
                .setBehaviorType("impulse").putBehaviorParams("vertical", 8.5f).build();
    }

    private static InputFrame sampleInput() {
        return InputFrame.newBuilder().setSequence(77).setMoveX(0.5f).setMoveZ(1f).setAimYaw(1.5f)
                .setAimPitch(-0.25f).setHasCamera(true).setCamX(1f).setCamY(2f).setCamZ(3f).setFire(true)
                .setMoveMask(5).setSelectSlot(3).setCycleSlot(-1).build();
    }

    private static ServerMessage sampleSnapshot() {
        return ServerMessage.newBuilder().setSnapshot(Snapshot.newBuilder()
                .setTick(1200).setAckSequence(77)
                .setMatch(MatchInfo.newBuilder().setPhase(MatchPhase.ROUND_ACTIVE).setTicksLeft(5000).setRound(2)
                        .setWinsTeam0(1).setWinsTeam1(0).setLastRoundWinner(0).setWinner(-1))
                .addCombatants(CombatantState.newBuilder().setId(0).setX(-10f).setY(0f).setZ(2f).setYaw(1.57f)
                        .setHp(80f).setShield(25f).setAlive(true).setPosture(Posture.CROUCHING).setActiveMove(1)
                        .setSelectedSlot(4).addSlots(SlotState.newBuilder().setSlugIndex(3).setShotsUntilReady(2))
                        .setHitboxHeight(1.08f))
                .addProjectiles(ProjectileState.newBuilder().setId(9).setSlugIndex(3).setOwner(0).setX(1f)
                        .setY(1.3f).setZ(2f).setVx(45f))
                .build()).build();
    }

    @Test
    void theInputFrameWireFormatIsStable() {
        assertThat(HexFormat.of().formatHex(sampleInput().toByteArray())).isEqualTo(GOLDEN_INPUT_HEX);
    }

    @Test
    void theSnapshotWireFormatIsStable() {
        assertThat(HexFormat.of().formatHex(sampleSnapshot().toByteArray())).isEqualTo(GOLDEN_SNAPSHOT_HEX);
    }

    @Test
    void goldenBytesDecodeBackToTheSameMessages() throws InvalidProtocolBufferException {
        assertThat(InputFrame.parseFrom(HexFormat.of().parseHex(GOLDEN_INPUT_HEX))).isEqualTo(sampleInput());
        assertThat(ServerMessage.parseFrom(HexFormat.of().parseHex(GOLDEN_SNAPSHOT_HEX)))
                .isEqualTo(sampleSnapshot());
    }

    @Test
    void unknownFieldsFromNewerPeersAreTolerated() throws InvalidProtocolBufferException {
        byte[] withExtraField = new byte[sampleInput().toByteArray().length + 3];
        byte[] original = sampleInput().toByteArray();
        System.arraycopy(original, 0, withExtraField, 0, original.length);
        withExtraField[original.length] = (byte) (99 << 3); // field 99, varint
        withExtraField[original.length + 1] = (byte) 0x80;
        withExtraField[original.length + 2] = 0x01;

        InputFrame parsed = InputFrame.parseFrom(withExtraField);

        assertThat(parsed.getSequence()).isEqualTo(77);
    }

    @Test
    void theLocalStateWireFormatIsStable() {
        assertThat(HexFormat.of().formatHex(sampleLocal().toByteArray())).isEqualTo(GOLDEN_LOCAL_HEX);
    }

    @Test
    void theMoveInfoWireFormatIsStable() {
        assertThat(HexFormat.of().formatHex(sampleMove().toByteArray())).isEqualTo(GOLDEN_MOVE_HEX);
    }

    @Test
    void goldenBytesOfTheClientDataDecodeBack() throws InvalidProtocolBufferException {
        assertThat(LocalState.parseFrom(HexFormat.of().parseHex(GOLDEN_LOCAL_HEX))).isEqualTo(sampleLocal());
        assertThat(MoveInfo.parseFrom(HexFormat.of().parseHex(GOLDEN_MOVE_HEX))).isEqualTo(sampleMove());
    }

    @Test
    void enumNumbersMatchTheCoreOrdinals() {
        assertThat(MatchPhase.ROUND_ACTIVE.getNumber()).isEqualTo(2);
        assertThat(EventKind.MATCH_ENDED.getNumber()).isEqualTo(19);
        assertThat(Posture.ACTION.getNumber()).isEqualTo(3);
    }
}
