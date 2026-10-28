using Bajoterra.Protocol;

namespace Bajoterra.Client.Interpolation
{
    /// <summary>A received snapshot with the server time it describes.</summary>
    public readonly struct TimedSnapshot
    {
        public readonly double ServerTime;
        public readonly Snapshot Snapshot;

        public TimedSnapshot(double serverTime, Snapshot snapshot)
        {
            ServerTime = serverTime;
            Snapshot = snapshot;
        }
    }

    /// <summary>
    /// The most recent snapshots in time order. Remote entities are drawn by interpolating between the two
    /// snapshots that surround the render time.
    /// </summary>
    public sealed class SnapshotBuffer
    {
        private readonly TimedSnapshot[] _items;
        private int _head;
        private int _count;

        public SnapshotBuffer(int capacity = 32)
        {
            _items = new TimedSnapshot[capacity];
        }

        public int Count
        {
            get { return _count; }
        }

        /// <summary>The newest snapshot, or null.</summary>
        public Snapshot Latest
        {
            get { return _count == 0 ? null : this[_count - 1].Snapshot; }
        }

        /// <summary>The i-th snapshot, 0 being the oldest.</summary>
        public TimedSnapshot this[int i]
        {
            get { return _items[(_head + i) % _items.Length]; }
        }

        /// <summary>Adds a snapshot; one that is not newer than the newest is dropped (late or duplicate).</summary>
        public bool Add(double serverTime, Snapshot snapshot)
        {
            if (_count > 0 && serverTime <= this[_count - 1].ServerTime)
            {
                return false;
            }
            if (_count == _items.Length)
            {
                _head = (_head + 1) % _items.Length;
                _count--;
            }
            _items[(_head + _count) % _items.Length] = new TimedSnapshot(serverTime, snapshot);
            _count++;
            return true;
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Finds the snapshots around <paramref name="renderTime"/>. Returns false when the buffer is empty.
        /// <paramref name="alpha"/> is the fraction (0..1) of the way from <paramref name="from"/> to
        /// <paramref name="to"/>. Before the oldest snapshot both results are the oldest one; after the newest both
        /// are the newest one and <paramref name="overshoot"/> says by how many seconds the render time is beyond it
        /// (the caller may extrapolate moving things for a short while).
        /// </summary>
        public bool Find(double renderTime, out TimedSnapshot from, out TimedSnapshot to, out double alpha,
            out double overshoot)
        {
            from = default;
            to = default;
            alpha = 0.0;
            overshoot = 0.0;
            if (_count == 0)
            {
                return false;
            }
            TimedSnapshot oldest = this[0];
            TimedSnapshot newest = this[_count - 1];
            if (renderTime <= oldest.ServerTime)
            {
                from = oldest;
                to = oldest;
                return true;
            }
            if (renderTime >= newest.ServerTime)
            {
                from = newest;
                to = newest;
                overshoot = renderTime - newest.ServerTime;
                return true;
            }
            for (int i = _count - 1; i > 0; i--)
            {
                TimedSnapshot older = this[i - 1];
                if (older.ServerTime <= renderTime)
                {
                    from = older;
                    to = this[i];
                    alpha = (renderTime - older.ServerTime) / (to.ServerTime - older.ServerTime);
                    return true;
                }
            }
            from = oldest;
            to = oldest;
            return true;
        }
    }
}
