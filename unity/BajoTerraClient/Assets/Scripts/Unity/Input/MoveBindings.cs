using System.Collections.Generic;

namespace Bajoterra.Unity
{
    /// <summary>A key a move can be bound to, independent of the input backend.</summary>
    public enum MoveKey
    {
        None,
        Space,
        LeftShift,
        LeftCtrl,
        C,
        F,
        Q,
        E,
        R,
        V,
        X,
        Z
    }

    /// <summary>
    /// Which key triggers which move, by move id. A character's move set decides which of these exist: a move the
    /// server adds later is bound by adding a line here (or an extra key falls back to the unassigned list), with no
    /// change to the input code or to the protocol.
    /// </summary>
    public static class MoveBindings
    {
        private static readonly Dictionary<string, MoveKey[]> Table = new Dictionary<string, MoveKey[]>
        {
            { "jump", new[] { MoveKey.Space } },
            { "wall_kick", new[] { MoveKey.Space } },
            { "crouch", new[] { MoveKey.C, MoveKey.LeftCtrl } },
            { "roll", new[] { MoveKey.LeftShift } },
            { "dash", new[] { MoveKey.F } },
            { "slide", new[] { MoveKey.V } },
            { "double_jump", new[] { MoveKey.Space } }
        };

        // Q fires a fusion shot, so it is not handed out to moves.
        private static readonly MoveKey[] Spare = { MoveKey.E, MoveKey.R, MoveKey.X, MoveKey.Z };

        /// <summary>Keys for a move id; unknown moves get a spare key based on their name so they are still reachable.</summary>
        public static MoveKey[] KeysFor(string moveId)
        {
            if (Table.TryGetValue(moveId, out MoveKey[] keys))
            {
                return keys;
            }
            int hash = 0;
            for (int i = 0; i < moveId.Length; i++)
            {
                hash = hash * 31 + moveId[i];
            }
            return new[] { Spare[(hash & 0x7fffffff) % Spare.Length] };
        }
    }
}
