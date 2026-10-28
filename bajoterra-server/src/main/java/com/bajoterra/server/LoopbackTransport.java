package com.bajoterra.server;

import com.bajoterra.protocol.Transport;
import com.bajoterra.protocol.TransportListener;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.ServerMessage;
import java.util.Map;
import java.util.Queue;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * In-process transport: "clients" are plain objects that talk to the server without any socket. Used by tests and by
 * the local bot runner, and proves the server depends only on the {@link Transport} abstraction.
 */
public final class LoopbackTransport implements Transport {

    /** One simulated client connection. */
    public final class Client {
        private final int id;
        private final Queue<ServerMessage> received = new ConcurrentLinkedQueue<>();
        private volatile boolean open = true;

        private Client(int id) {
            this.id = id;
        }

        public int id() {
            return id;
        }

        public void send(ClientMessage message) {
            if (open) {
                listener.onMessage(id, message);
            }
        }

        /** Next message the server sent, or null when none is waiting. */
        public ServerMessage poll() {
            return received.poll();
        }

        public int pending() {
            return received.size();
        }

        public boolean isOpen() {
            return open;
        }

        public void disconnect() {
            if (open) {
                open = false;
                clients.remove(id);
                listener.onDisconnected(id);
            }
        }
    }

    private final Map<Integer, Client> clients = new ConcurrentHashMap<>();
    private final AtomicInteger nextId = new AtomicInteger(1);
    private TransportListener listener;

    @Override
    public void setListener(TransportListener newListener) {
        this.listener = newListener;
    }

    @Override
    public void start() {
        // Nothing to open: connections are created by connect().
    }

    /** Opens a new client connection. */
    public Client connect() {
        Client client = new Client(nextId.getAndIncrement());
        clients.put(client.id, client);
        listener.onConnected(client.id);
        return client;
    }

    @Override
    public void send(int connectionId, ServerMessage message) {
        Client client = clients.get(connectionId);
        if (client != null) {
            client.received.add(message);
        }
    }

    @Override
    public void disconnect(int connectionId) {
        Client client = clients.remove(connectionId);
        if (client != null) {
            client.open = false;
            listener.onDisconnected(connectionId);
        }
    }

    @Override
    public void close() {
        for (Client client : clients.values()) {
            client.open = false;
        }
        clients.clear();
    }
}
