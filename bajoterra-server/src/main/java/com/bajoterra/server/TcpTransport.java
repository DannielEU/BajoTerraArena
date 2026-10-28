package com.bajoterra.server;

import com.bajoterra.protocol.FrameCodec;
import com.bajoterra.protocol.Transport;
import com.bajoterra.protocol.TransportListener;
import com.bajoterra.protocol.generated.ClientMessage;
import com.bajoterra.protocol.generated.ServerMessage;
import com.google.protobuf.InvalidProtocolBufferException;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.StandardSocketOptions;
import java.nio.ByteBuffer;
import java.nio.channels.SelectionKey;
import java.nio.channels.Selector;
import java.nio.channels.ServerSocketChannel;
import java.nio.channels.SocketChannel;
import java.util.Iterator;
import java.util.Map;
import java.util.Queue;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Non-blocking TCP transport on a single selector thread. Messages are length-prefixed protobuf frames
 * ({@link FrameCodec}). A connection that stops reading and lets its outgoing queue grow past
 * {@link #MAX_QUEUED_FRAMES} is dropped instead of slowing the server down.
 */
public final class TcpTransport implements Transport {

    private static final int MAX_QUEUED_FRAMES = 512;
    private static final int READ_BUFFER_BYTES = 2 * FrameCodec.MAX_FRAME_BYTES;

    /** Everything the selector thread knows about one socket. */
    private static final class Connection {
        final int id;
        final SocketChannel channel;
        final ByteBuffer readBuffer = ByteBuffer.allocate(READ_BUFFER_BYTES);
        final Queue<ByteBuffer> writes = new ConcurrentLinkedQueue<>();
        SelectionKey key;
        volatile boolean closed;
        /** Close as soon as the queued frames have been written (an error message must reach the peer first). */
        volatile boolean closeAfterFlush;

        Connection(int id, SocketChannel channel) {
            this.id = id;
            this.channel = channel;
        }
    }

    private final int requestedPort;
    private final Map<Integer, Connection> connections = new ConcurrentHashMap<>();
    private final AtomicInteger nextId = new AtomicInteger(1);
    private final Queue<Runnable> selectorTasks = new ConcurrentLinkedQueue<>();
    private TransportListener listener;
    private Selector selector;
    private ServerSocketChannel server;
    private Thread thread;
    private volatile boolean running;

    /** @param port TCP port to listen on; 0 picks a free one (see {@link #port()}) */
    public TcpTransport(int port) {
        this.requestedPort = port;
    }

    @Override
    public void setListener(TransportListener newListener) {
        this.listener = newListener;
    }

    @Override
    public void start() throws IOException {
        selector = Selector.open();
        server = ServerSocketChannel.open();
        server.configureBlocking(false);
        server.bind(new InetSocketAddress(requestedPort));
        server.register(selector, SelectionKey.OP_ACCEPT);
        running = true;
        thread = new Thread(this::loop, "bajoterra-tcp");
        thread.setDaemon(true);
        thread.start();
    }

    /** The port the server is actually listening on. */
    public int port() {
        return ((InetSocketAddress) serverAddress()).getPort();
    }

    private java.net.SocketAddress serverAddress() {
        try {
            return server.getLocalAddress();
        } catch (IOException e) {
            throw new IllegalStateException("Transport not started", e);
        }
    }

    @Override
    public void send(int connectionId, ServerMessage message) {
        Connection connection = connections.get(connectionId);
        if (connection == null || connection.closed) {
            return;
        }
        if (connection.writes.size() >= MAX_QUEUED_FRAMES) {
            disconnect(connectionId);
            return;
        }
        connection.writes.add(FrameCodec.encode(message));
        selectorTasks.add(() -> enableWrite(connection));
        selector.wakeup();
    }

    /**
     * Closes the connection after everything already queued for it has been written. Closing at once would drop the
     * last message, typically the error that explains why the peer is being disconnected.
     */
    @Override
    public void disconnect(int connectionId) {
        Connection connection = connections.get(connectionId);
        if (connection != null) {
            selectorTasks.add(() -> {
                if (connection.writes.isEmpty()) {
                    closeConnection(connection);
                } else {
                    connection.closeAfterFlush = true;
                    enableWrite(connection);
                }
            });
            selector.wakeup();
        }
    }

    @Override
    public void close() {
        running = false;
        if (selector != null) {
            selector.wakeup();
        }
        if (thread != null) {
            try {
                thread.join(2_000);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
        }
    }

    // ------------------------------------------------------------------------------------------ selector thread

    private void loop() {
        try {
            while (running) {
                selector.select();
                Runnable task;
                while ((task = selectorTasks.poll()) != null) {
                    task.run();
                }
                Iterator<SelectionKey> keys = selector.selectedKeys().iterator();
                while (keys.hasNext()) {
                    SelectionKey key = keys.next();
                    keys.remove();
                    if (!key.isValid()) {
                        continue;
                    }
                    if (key.isAcceptable()) {
                        accept();
                    } else {
                        Connection connection = (Connection) key.attachment();
                        if (key.isReadable()) {
                            read(connection);
                        }
                        if (key.isValid() && key.isWritable()) {
                            write(connection);
                        }
                    }
                }
            }
        } catch (IOException e) {
            System.err.println("TCP transport stopped: " + e);
        } finally {
            shutdown();
        }
    }

    private void accept() throws IOException {
        SocketChannel channel = server.accept();
        if (channel == null) {
            return;
        }
        channel.configureBlocking(false);
        channel.setOption(StandardSocketOptions.TCP_NODELAY, true);
        Connection connection = new Connection(nextId.getAndIncrement(), channel);
        connection.key = channel.register(selector, SelectionKey.OP_READ, connection);
        connections.put(connection.id, connection);
        listener.onConnected(connection.id);
    }

    private void read(Connection connection) {
        try {
            int count = connection.channel.read(connection.readBuffer);
            if (count < 0) {
                closeConnection(connection);
                return;
            }
            connection.readBuffer.flip();
            byte[] payload;
            while ((payload = FrameCodec.decodeNext(connection.readBuffer)) != null) {
                listener.onMessage(connection.id, ClientMessage.parseFrom(payload));
            }
            connection.readBuffer.compact();
        } catch (InvalidProtocolBufferException e) {
            closeConnection(connection);
        } catch (IOException e) {
            closeConnection(connection);
        }
    }

    private void write(Connection connection) {
        try {
            ByteBuffer frame;
            while ((frame = connection.writes.peek()) != null) {
                connection.channel.write(frame);
                if (frame.hasRemaining()) {
                    return;
                }
                connection.writes.poll();
            }
            if (connection.closeAfterFlush) {
                closeConnection(connection);
            } else if (connection.key.isValid()) {
                connection.key.interestOps(SelectionKey.OP_READ);
            }
        } catch (IOException e) {
            closeConnection(connection);
        }
    }

    private void enableWrite(Connection connection) {
        if (!connection.closed && connection.key.isValid()) {
            connection.key.interestOps(SelectionKey.OP_READ | SelectionKey.OP_WRITE);
        }
    }

    private void closeConnection(Connection connection) {
        if (connection.closed) {
            return;
        }
        connection.closed = true;
        connections.remove(connection.id);
        try {
            connection.channel.close();
        } catch (IOException e) {
            // Already closing; nothing else to do.
        }
        listener.onDisconnected(connection.id);
    }

    private void shutdown() {
        for (Connection connection : connections.values()) {
            connection.closed = true;
            try {
                connection.channel.close();
            } catch (IOException e) {
                // Best effort while shutting down.
            }
        }
        connections.clear();
        try {
            server.close();
            selector.close();
        } catch (IOException e) {
            // Best effort while shutting down.
        }
    }
}
