using System;
using System.Collections.Generic;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Session
{
    /// <summary>
    /// Holds gameplay events until the render clock reaches the moment they happened. Remote entities are drawn
    /// ~100 ms in the past, so an explosion announced by the server must wait that long to line up with the
    /// slug that causes it.
    /// </summary>
    public sealed class EventScheduler
    {
        /// <summary>Beyond this many waiting events the oldest are released at once (a stalled clock must not leak memory).</summary>
        private const int MaxPending = 4096;

        private readonly Queue<GameEvent> _pending = new Queue<GameEvent>();
        private readonly double _tickSeconds;

        public EventScheduler(int ticksPerSecond)
        {
            _tickSeconds = 1.0 / Math.Max(1, ticksPerSecond);
        }

        public int Pending
        {
            get { return _pending.Count; }
        }

        public void Enqueue(GameEvent gameEvent)
        {
            _pending.Enqueue(gameEvent);
        }

        /// <summary>Hands every event that is due at <paramref name="serverTime"/> to <paramref name="play"/>.</summary>
        public void Release(double serverTime, Action<GameEvent> play)
        {
            while (_pending.Count > 0)
            {
                GameEvent next = _pending.Peek();
                if (next.Tick * _tickSeconds > serverTime && _pending.Count <= MaxPending)
                {
                    return;
                }
                play(_pending.Dequeue());
            }
        }

        public void Clear()
        {
            _pending.Clear();
        }
    }
}
