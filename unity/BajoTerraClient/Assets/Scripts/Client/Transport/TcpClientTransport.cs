using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Bajoterra.Client.Wire;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Transport
{
    /// <summary>
    /// TCP transport: one thread connects and reads, one thread writes. Decoded messages wait in a queue that the
    /// game thread drains through <see cref="TryReceive"/>. Messages sent while the connection is still being
    /// established are queued and go out once it is up.
    /// </summary>
    public sealed class TcpClientTransport : IClientTransport
    {
        private const int ConnectTimeoutMs = 5000;

        private readonly ConcurrentQueue<ServerMessage> _inbox = new ConcurrentQueue<ServerMessage>();
        private readonly object _sync = new object();
        private Link _link;
        private ConnectionState _state = ConnectionState.Disconnected;
        private string _failure = string.Empty;

        /// <summary>One connection attempt and everything that belongs to it.</summary>
        private sealed class Link
        {
            public readonly TcpClient Client = new TcpClient { NoDelay = true };
            public readonly BlockingCollection<byte[]> Outbox = new BlockingCollection<byte[]>();
            public volatile bool Closed;

            public void Shutdown()
            {
                Closed = true;
                Outbox.CompleteAdding();
                Client.Close();
            }
        }

        public ConnectionState State
        {
            get
            {
                lock (_sync)
                {
                    return _state;
                }
            }
        }

        public string FailureReason
        {
            get
            {
                lock (_sync)
                {
                    return _failure;
                }
            }
        }

        public void Connect(string host, int port)
        {
            Link link;
            lock (_sync)
            {
                if (_state == ConnectionState.Connecting || _state == ConnectionState.Connected)
                {
                    throw new InvalidOperationException("Already connected or connecting");
                }
                _failure = string.Empty;
                _state = ConnectionState.Connecting;
                link = new Link();
                _link = link;
            }
            new Thread(() => RunReader(link, host, port)) { IsBackground = true, Name = "bajoterra-net-reader" }
                .Start();
        }

        public void Send(ClientMessage message)
        {
            Link link;
            lock (_sync)
            {
                link = _link;
            }
            if (link == null || link.Closed)
            {
                return;
            }
            byte[] frame = FrameCodec.Encode(message);
            try
            {
                link.Outbox.Add(frame);
            }
            catch (InvalidOperationException)
            {
                // Closed in the meantime: the frame is simply dropped.
            }
        }

        public bool TryReceive(out ServerMessage message)
        {
            return _inbox.TryDequeue(out message);
        }

        public void Close()
        {
            lock (_sync)
            {
                if (_link != null)
                {
                    _link.Shutdown();
                    _link = null;
                }
                if (_state != ConnectionState.Failed)
                {
                    _state = ConnectionState.Disconnected;
                }
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void RunReader(Link link, string host, int port)
        {
            try
            {
                IAsyncResult connecting = link.Client.BeginConnect(host, port, null, null);
                if (!connecting.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                {
                    throw new IOException("Timed out connecting to " + host + ":" + port);
                }
                link.Client.EndConnect(connecting);
                lock (_sync)
                {
                    if (link.Closed)
                    {
                        return;
                    }
                    _state = ConnectionState.Connected;
                }
                new Thread(() => RunWriter(link)) { IsBackground = true, Name = "bajoterra-net-writer" }.Start();
                ReadLoop(link);
                Fail(link, "The server closed the connection");
            }
            catch (Exception e) when (IsNetworkFailure(e))
            {
                Fail(link, e.Message);
            }
        }

        private void ReadLoop(Link link)
        {
            var reader = new FrameReader();
            var chunk = new byte[16 * 1024];
            NetworkStream stream = link.Client.GetStream();
            int count;
            while ((count = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                reader.Feed(chunk, 0, count);
                while (reader.TryRead(out ServerMessage message))
                {
                    _inbox.Enqueue(message);
                }
            }
        }

        private void RunWriter(Link link)
        {
            try
            {
                NetworkStream stream = link.Client.GetStream();
                foreach (byte[] frame in link.Outbox.GetConsumingEnumerable())
                {
                    stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception e) when (IsNetworkFailure(e))
            {
                Fail(link, e.Message);
            }
        }

        private static bool IsNetworkFailure(Exception e)
        {
            return e is IOException || e is SocketException || e is ObjectDisposedException
                   || e is ProtocolViolationException || e is InvalidOperationException;
        }

        private void Fail(Link link, string reason)
        {
            lock (_sync)
            {
                if (link.Closed || link != _link)
                {
                    return; // closed on purpose, or an older connection
                }
                _failure = reason;
                _state = ConnectionState.Failed;
                link.Shutdown();
            }
        }
    }
}
