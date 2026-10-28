package com.bajoterra.protocol;

import com.bajoterra.protocol.generated.ServerMessage;
import java.io.IOException;

/**
 * Server-side network abstraction (Adapter pattern). The game server only talks to this interface, so the TCP
 * transport can be swapped for UDP, a loopback used in tests, or anything else without touching game code.
 */
public interface Transport extends AutoCloseable {

    /** Sets the receiver of connection events; must be called before {@link #start()}. */
    void setListener(TransportListener listener);

    /** Starts accepting connections. */
    void start() throws IOException;

    /** Queues a message for a connection; unknown or closed connections are ignored. */
    void send(int connectionId, ServerMessage message);

    /** Closes one connection. */
    void disconnect(int connectionId);

    /** Stops the transport and closes every connection. */
    @Override
    void close();
}
