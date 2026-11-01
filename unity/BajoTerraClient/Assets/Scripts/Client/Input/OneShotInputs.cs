namespace Bajoterra.Client.Input
{
    /// <summary>
    /// Remembers presses that happen between two fixed input ticks (a slot key, a wheel notch, a dance) until the next tick
    /// sends them, so a short press at a high frame rate is never lost. The server also latches these, so one frame
    /// carrying the press is enough.
    /// </summary>
    public sealed class OneShotInputs
    {
        private int _select = -1;
        private int _cycle;
        private uint _dance;

        /// <summary>Requests slot <paramref name="slot"/> (0-4); the last request before the next tick wins.</summary>
        public void Select(int slot)
        {
            if (slot >= 0 && slot < Hud.HudModel.SlotCount)
            {
                _select = slot;
            }
        }

        /// <summary>Requests moving the selection; notches accumulate and are sent as one direction.</summary>
        public void Cycle(int direction)
        {
            _cycle += direction;
        }

        /// <summary>Requests celebration dance <paramref name="dance"/> (1-3).</summary>
        public void Dance(uint dance)
        {
            _dance = dance;
        }

        /// <summary>Writes the pending presses into <paramref name="input"/> and forgets them.</summary>
        public void Drain(ref PlayerInput input)
        {
            input.SelectSlot = _select;
            input.CycleSlot = _cycle > 0 ? 1 : _cycle < 0 ? -1 : 0;
            input.Dance = _dance;
            _select = -1;
            _cycle = 0;
            _dance = 0;
        }
    }
}
