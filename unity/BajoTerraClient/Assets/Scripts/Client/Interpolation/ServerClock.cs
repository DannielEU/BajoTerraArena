using System;

namespace Bajoterra.Client.Interpolation
{
    /// <summary>
    /// Estimates the server's time from the arrival of snapshots, so remote entities can be drawn a fixed
    /// interval in the past (interpolation) regardless of network jitter. The clock never reads the wall clock
    /// itself: callers pass the local time in seconds, which keeps it deterministic to test.
    ///
    /// Each snapshot gives one sample of "local time minus server time", which equals the clock offset plus the
    /// network delay of that packet. The estimate drops immediately to a smaller sample (a packet that arrived
    /// faster is closer to the true offset) and creeps up slowly towards larger ones.
    /// </summary>
    public sealed class ServerClock
    {
        public const double TickSeconds = 1.0 / 60.0;

        /// <summary>Fraction of the distance towards a larger sample that is covered per snapshot.</summary>
        private const double RiseRate = 0.02;

        private double _offset;
        private int _samples;

        public bool IsSynchronized
        {
            get { return _samples > 0; }
        }

        public int Samples
        {
            get { return _samples; }
        }

        /// <summary>Records the arrival of a snapshot describing server tick <paramref name="serverTick"/>.</summary>
        public void Observe(double localNow, int serverTick)
        {
            double sample = localNow - serverTick * TickSeconds;
            if (_samples == 0 || sample < _offset)
            {
                _offset = sample;
            }
            else
            {
                _offset += (sample - _offset) * RiseRate;
            }
            _samples++;
        }

        /// <summary>The server time (seconds) that corresponds to the local time.</summary>
        public double ServerTimeAt(double localNow)
        {
            return localNow - _offset;
        }

        public void Reset()
        {
            _samples = 0;
            _offset = 0.0;
        }

        /// <summary>Seconds of server time that elapse in one client tick of the given rate.</summary>
        public static double SecondsPerTick(int ticksPerSecond)
        {
            return 1.0 / Math.Max(1, ticksPerSecond);
        }
    }
}
