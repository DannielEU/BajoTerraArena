package com.bajoterra.protocol;

import com.google.protobuf.MessageLite;
import java.io.IOException;
import java.nio.ByteBuffer;

/**
 * Length-prefixed framing used on stream transports: a 4-byte big-endian payload length followed by the protobuf
 * bytes. A C# client implements the same trivial format.
 */
public final class FrameCodec {

    /** Largest accepted payload; protects the server from absurd length prefixes. */
    public static final int MAX_FRAME_BYTES = 64 * 1024;

    private static final int HEADER_BYTES = 4;

    private FrameCodec() {
        throw new AssertionError("Utility class, not instantiable");
    }

    /** Serialises a message into a ready-to-send frame (length prefix included). */
    public static ByteBuffer encode(MessageLite message) {
        int size = message.getSerializedSize();
        if (size > MAX_FRAME_BYTES) {
            throw new IllegalArgumentException("Message too large: " + size + " bytes");
        }
        byte[] bytes = new byte[HEADER_BYTES + size];
        ByteBuffer buffer = ByteBuffer.wrap(bytes);
        buffer.putInt(size);
        try {
            message.writeTo(com.google.protobuf.CodedOutputStream.newInstance(bytes, HEADER_BYTES, size));
        } catch (IOException e) {
            throw new IllegalStateException("Cannot serialise message", e);
        }
        buffer.position(0);
        buffer.limit(bytes.length);
        return buffer;
    }

    /**
     * Tries to extract one complete frame payload from the front of {@code accumulated}.
     *
     * @param accumulated bytes received so far, in read mode (position..limit); consumed bytes are skipped
     * @return the payload, or null when the frame is not complete yet
     * @throws IOException if the length prefix is invalid
     */
    public static byte[] decodeNext(ByteBuffer accumulated) throws IOException {
        if (accumulated.remaining() < HEADER_BYTES) {
            return null;
        }
        int size = accumulated.getInt(accumulated.position());
        if (size < 0 || size > MAX_FRAME_BYTES) {
            throw new IOException("Invalid frame length: " + size);
        }
        if (accumulated.remaining() < HEADER_BYTES + size) {
            return null;
        }
        accumulated.position(accumulated.position() + HEADER_BYTES);
        byte[] payload = new byte[size];
        accumulated.get(payload);
        return payload;
    }
}
