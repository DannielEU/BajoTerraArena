package com.bajoterra.protocol;

import com.bajoterra.protocol.generated.ClientMessage;

/**
 * Receives connection events from a {@link Transport}. Callbacks may arrive on the transport's own I/O thread, so
 * implementations must hand the work over to the simulation thread instead of touching game state directly.
 */
public interface TransportListener {

    void onConnected(int connectionId);

    void onMessage(int connectionId, ClientMessage message);

    void onDisconnected(int connectionId);
}
