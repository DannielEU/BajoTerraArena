package com.bajoterra.protocol;

/** Version of the wire contract; clients and servers must agree on it in the handshake. */
public final class ProtocolVersion {

    /** Bumped only for breaking changes of {@code game.proto}. */
    public static final int CURRENT = 1;

    private ProtocolVersion() {
        throw new AssertionError("Constants class, not instantiable");
    }
}
