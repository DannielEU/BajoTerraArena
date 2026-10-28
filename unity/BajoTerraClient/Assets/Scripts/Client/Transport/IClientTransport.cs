using System;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Transport
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }

    /// <summary>
    /// How the client talks to the server (Adapter pattern, the counterpart of the Java <c>Transport</c>). The
    /// game code only polls: network threads, if any, never touch game objects. A UDP transport can replace the TCP
    /// one without changing any session code.
    /// </summary>
    public interface IClientTransport : IDisposable
    {
        ConnectionState State { get; }

        /// <summary>Why the connection failed or ended; empty while it is healthy.</summary>
        string FailureReason { get; }

        /// <summary>Starts connecting; returns immediately. Watch <see cref="State"/>.</summary>
        void Connect(string host, int port);

        /// <summary>Queues a message for the server. Safe to call from the game thread at 60 Hz.</summary>
        void Send(ClientMessage message);

        /// <summary>Takes the oldest received message, if any.</summary>
        bool TryReceive(out ServerMessage message);

        void Close();
    }
}
