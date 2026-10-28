package com.bajoterra.protocol;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.Hello;
import com.bajoterra.protocol.generated.InputFrame;
import java.io.IOException;
import java.nio.ByteBuffer;
import org.junit.jupiter.api.Test;

class FrameCodecTest {

    private static ClientMessage hello(String name) {
        return ClientMessage.newBuilder()
                .setHello(Hello.newBuilder().setProtocolVersion(ProtocolVersion.CURRENT).setPlayerName(name))
                .build();
    }

    @Test
    void aFrameRoundTrips() throws IOException {
        ByteBuffer frame = FrameCodec.encode(hello("Eli"));

        byte[] payload = FrameCodec.decodeNext(frame);

        assertThat(payload).isNotNull();
        assertThat(ClientMessage.parseFrom(payload)).isEqualTo(hello("Eli"));
        assertThat(frame.remaining()).isZero();
    }

    @Test
    void theLengthPrefixIsABigEndianInteger() {
        ByteBuffer frame = FrameCodec.encode(hello("x"));
        int size = frame.getInt(0);

        assertThat(size).isEqualTo(hello("x").getSerializedSize());
        assertThat(frame.remaining()).isEqualTo(4 + size);
    }

    @Test
    void incompleteFramesWaitForMoreBytes() throws IOException {
        ByteBuffer whole = FrameCodec.encode(hello("Kord"));
        byte[] bytes = new byte[whole.remaining()];
        whole.get(bytes);

        for (int available = 0; available < bytes.length; available++) {
            ByteBuffer partial = ByteBuffer.wrap(bytes, 0, available);
            assertThat(FrameCodec.decodeNext(partial)).as("%d of %d bytes", available, bytes.length).isNull();
            assertThat(partial.position()).as("nothing consumed").isZero();
        }
        assertThat(FrameCodec.decodeNext(ByteBuffer.wrap(bytes))).isNotNull();
    }

    @Test
    void severalFramesInOneBufferAreExtractedInOrder() throws IOException {
        ByteBuffer first = FrameCodec.encode(hello("one"));
        ByteBuffer second = FrameCodec.encode(hello("two"));
        ByteBuffer both = ByteBuffer.allocate(first.remaining() + second.remaining());
        both.put(first).put(second).flip();

        assertThat(ClientMessage.parseFrom(FrameCodec.decodeNext(both)).getHello().getPlayerName()).isEqualTo("one");
        assertThat(ClientMessage.parseFrom(FrameCodec.decodeNext(both)).getHello().getPlayerName()).isEqualTo("two");
        assertThat(FrameCodec.decodeNext(both)).isNull();
    }

    @Test
    void absurdLengthPrefixesAreRejected() {
        ByteBuffer negative = ByteBuffer.allocate(8).putInt(-5).putInt(0).flip();
        ByteBuffer huge = ByteBuffer.allocate(8).putInt(FrameCodec.MAX_FRAME_BYTES + 1).putInt(0).flip();

        assertThatThrownBy(() -> FrameCodec.decodeNext(negative)).isInstanceOf(IOException.class);
        assertThatThrownBy(() -> FrameCodec.decodeNext(huge)).isInstanceOf(IOException.class);
    }

    @Test
    void oversizedMessagesCannotBeEncoded() {
        ClientMessage big = ClientMessage.newBuilder()
                .setHello(Hello.newBuilder().setPlayerName("x".repeat(FrameCodec.MAX_FRAME_BYTES)))
                .build();

        assertThatThrownBy(() -> FrameCodec.encode(big)).isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void inputFramesAreSmall() {
        ClientMessage input = ClientMessage.newBuilder().setInput(InputFrame.newBuilder()
                .setSequence(100_000).setMoveX(1).setMoveZ(-1).setAimYaw(3.14f).setAimPitch(-0.4f)
                .setHasCamera(true).setCamX(10).setCamY(1.7f).setCamZ(-30).setFire(true).setMoveMask(7)
                .setSelectSlot(9).setCycleSlot(-1)).build();

        assertThat(FrameCodec.encode(input).remaining()).as("bytes per input frame at 60 Hz").isLessThan(64);
    }
}
