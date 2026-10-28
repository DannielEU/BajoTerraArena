using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The player's devices as the game sees them. Everything above this interface is independent of the input
    /// backend (the Input System package or the old Input Manager).
    /// </summary>
    public interface IControls
    {
        /// <summary>Reads the devices; call once per frame before using the other members.</summary>
        void Poll();

        /// <summary>Strafe axis, -1 (left) to 1 (right).</summary>
        float MoveX { get; }

        /// <summary>Forward axis, -1 to 1.</summary>
        float MoveZ { get; }

        /// <summary>Mouse movement this frame in pixels (x right, y up).</summary>
        Vector2 Look { get; }

        bool Fire { get; }

        /// <summary>True while the key bound to the move with this id is held.</summary>
        bool IsMoveHeld(string moveId);

        /// <summary>Slot key pressed this frame (0-9), or -1.</summary>
        int SlotKey { get; }

        /// <summary>Mouse wheel notch this frame: -1, 0 or 1.</summary>
        int Scroll { get; }

        /// <summary>Primary click pressed this frame (cycles the spectated player when eliminated).</summary>
        bool ClickPressed { get; }

        bool PausePressed { get; }

        /// <summary>True while the scoreboard key (Tab) is held.</summary>
        bool ScoreboardHeld { get; }

        bool DebugPressed { get; }
    }
}
