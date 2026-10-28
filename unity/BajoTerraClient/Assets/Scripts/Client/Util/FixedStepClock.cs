using System;

namespace Bajoterra.Client
{
    /// <summary>
    /// Turns variable frame times into a whole number of fixed simulation steps (the classic accumulator), so the
    /// client produces exactly one input per server tick on average whatever its frame rate.
    /// </summary>
    public sealed class FixedStepClock
    {
        private readonly double _step;
        private readonly int _maxStepsPerFrame;
        private double _accumulated;

        public FixedStepClock(double stepSeconds = 1.0 / 60.0, int maxStepsPerFrame = 5)
        {
            if (stepSeconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            }
            _step = stepSeconds;
            _maxStepsPerFrame = maxStepsPerFrame;
        }

        public double StepSeconds
        {
            get { return _step; }
        }

        /// <summary>
        /// Adds the time of the last frame and returns how many fixed steps to run now. After a long stall the
        /// backlog beyond <c>maxStepsPerFrame</c> is discarded instead of being replayed in a burst.
        /// </summary>
        public int Advance(double deltaSeconds)
        {
            _accumulated += Math.Max(0.0, deltaSeconds);
            int steps = (int)(_accumulated / _step);
            _accumulated -= steps * _step;
            if (steps > _maxStepsPerFrame)
            {
                steps = _maxStepsPerFrame;
                _accumulated = 0.0;
            }
            return steps;
        }

        public void Reset()
        {
            _accumulated = 0.0;
        }
    }
}
