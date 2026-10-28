namespace Bajoterra.Client.Input
{
    /// <summary>
    /// Remembers presses that happen between two fixed input ticks (a slot key, a wheel notch) until the next tick
    /// sends them, so a short press at a high frame rate is never lost. The server also latches these, so one frame
    /// carrying the press is enough.
    /// </summary>
    public sealed class OneShotInputs
    {
        private int _select = -1;
        private int _cycle;

        /// <summary>Requests slot <paramref name="slot"/> (0-9); the last request before the next tick wins.</summary>
        public void Select(int slot)
        {
            if (slot >= 0 && slot < 10)
            {
                _select = slot;
            }
        }

        /// <summary>Requests moving the selection; notches accumulate and are sent as one direction.</summary>
        public void Cycle(int direction)
        {
            _cycle += direction;
        }

        /// <summary>Writes the pending presses into <paramref name="input"/> and forgets them.</summary>
        public void Drain(ref PlayerInput input)
        {
            input.SelectSlot = _select;
            input.CycleSlot = _cycle > 0 ? 1 : _cycle < 0 ? -1 : 0;
            _select = -1;
            _cycle = 0;
        }
    }
}
