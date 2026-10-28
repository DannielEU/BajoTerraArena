using System.Collections.Generic;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Transport
{
    /// <summary>
    /// In-memory transport for tests and offline tools: the test scripts what the "server" says and inspects what
    /// the client sent. Counterpart of the Java <c>LoopbackTransport</c>.
    /// </summary>
    public sealed class LoopbackClientTransport : IClientTransport
    {
        private readonly Queue<ServerMessage> _inbox = new Queue<ServerMessage>();

        /// <summary>Everything the client has sent, oldest first.</summary>
        public List<ClientMessage> Sent { get; } = new List<ClientMessage>();

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public string FailureReason { get; private set; } = string.Empty;

        public void Connect(string host, int port)
        {
            State = ConnectionState.Connected;
        }

        public void Send(ClientMessage message)
        {
            Sent.Add(message);
        }

        public bool TryReceive(out ServerMessage message)
        {
            if (_inbox.Count == 0)
            {
                message = null;
                return false;
            }
            message = _inbox.Dequeue();
            return true;
        }

        /// <summary>Makes the next <see cref="TryReceive"/> return this message.</summary>
        public void Deliver(ServerMessage message)
        {
            _inbox.Enqueue(message);
        }

        /// <summary>Simulates the connection dropping.</summary>
        public void Fail(string reason)
        {
            FailureReason = reason;
            State = ConnectionState.Failed;
        }

        public void Close()
        {
            State = ConnectionState.Disconnected;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
