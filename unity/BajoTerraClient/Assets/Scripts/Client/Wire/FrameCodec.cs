using System;
using System.IO;
using Bajoterra.Protocol;
using Google.Protobuf;

namespace Bajoterra.Client.Wire
{
    /// <summary>The protocol was violated by the peer (invalid frame length, undecodable payload).</summary>
    public sealed class ProtocolViolationException : Exception
    {
        public ProtocolViolationException(string message) : base(message)
        {
        }

        public ProtocolViolationException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Wire framing shared with the Java server: a 4-byte big-endian payload length followed by the protobuf
    /// payload, at most <see cref="MaxFrameBytes"/> bytes.
    /// </summary>
    public static class FrameCodec
    {
        public const int HeaderBytes = 4;
        public const int MaxFrameBytes = 64 * 1024;

        /// <summary>Encodes <paramref name="message"/> as one frame: the length header followed by the payload.</summary>
        public static byte[] Encode(IMessage message)
        {
            int size = message.CalculateSize();
            if (size > MaxFrameBytes)
            {
                throw new ProtocolViolationException("Message of " + size + " bytes exceeds the frame limit");
            }
            var frame = new byte[HeaderBytes + size];
            frame[0] = (byte)(size >> 24);
            frame[1] = (byte)(size >> 16);
            frame[2] = (byte)(size >> 8);
            frame[3] = (byte)size;
            // The Stream-based API on purpose: no Span<T> crosses the boundary with Google.Protobuf, whose Span type
            // may come from a different assembly than the one this code is compiled against.
            using (var payload = new MemoryStream(frame, HeaderBytes, size, true))
            {
                message.WriteTo(payload);
            }
            return frame;
        }
    }

    /// <summary>
    /// Incremental reader: feed it whatever the socket returned and take complete messages out. A frame may arrive
    /// split across several reads, and several frames may arrive in one read.
    /// </summary>
    public sealed class FrameReader
    {
        private byte[] _buffer = new byte[16 * 1024];
        private int _start;
        private int _end;

        /// <summary>Number of buffered bytes that do not yet form a complete frame.</summary>
        public int Pending
        {
            get { return _end - _start; }
        }

        /// <summary>Appends bytes received from the network.</summary>
        public void Feed(byte[] data, int offset, int count)
        {
            if (count <= 0)
            {
                return;
            }
            if (_end + count > _buffer.Length)
            {
                int pending = _end - _start;
                if (pending + count > _buffer.Length)
                {
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, pending + count));
                }
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, pending);
                _start = 0;
                _end = pending;
            }
            Buffer.BlockCopy(data, offset, _buffer, _end, count);
            _end += count;
        }

        /// <summary>
        /// Extracts the next complete message; returns false when more bytes are needed.
        /// </summary>
        /// <exception cref="ProtocolViolationException">The length prefix or the payload is invalid.</exception>
        public bool TryRead(out ServerMessage message)
        {
            message = null;
            if (_end - _start < FrameCodec.HeaderBytes)
            {
                return false;
            }
            int length = (_buffer[_start] << 24) | (_buffer[_start + 1] << 16) | (_buffer[_start + 2] << 8)
                         | _buffer[_start + 3];
            if (length < 0 || length > FrameCodec.MaxFrameBytes)
            {
                throw new ProtocolViolationException("Invalid frame length " + length);
            }
            if (_end - _start < FrameCodec.HeaderBytes + length)
            {
                return false;
            }
            try
            {
                message = ServerMessage.Parser.ParseFrom(_buffer, _start + FrameCodec.HeaderBytes, length);
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new ProtocolViolationException("Undecodable server message", e);
            }
            _start += FrameCodec.HeaderBytes + length;
            if (_start == _end)
            {
                _start = 0;
                _end = 0;
            }
            return true;
        }
    }
}
