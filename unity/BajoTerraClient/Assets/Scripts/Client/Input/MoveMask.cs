using System;
using Bajoterra.Client.Content;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Input
{
    /// <summary>
    /// Translates move ids ("jump", "roll"...) into the bits of <see cref="PlayerInput.MoveMask"/> for the chosen
    /// character. Key bindings talk in move ids, so a character with a different move set, or a move added to the
    /// server later, needs no change in the input code: a move the character lacks simply has no bit.
    /// </summary>
    public sealed class MoveMask
    {
        private readonly string[] _moveIds;

        public MoveMask(ContentCatalog content, CharacterInfo character)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }
            _moveIds = new string[character.MoveIndices.Count];
            for (int i = 0; i < _moveIds.Length; i++)
            {
                _moveIds[i] = content.Move((int)character.MoveIndices[i]).Id;
            }
        }

        /// <summary>The move ids in mask-bit order.</summary>
        public int Count
        {
            get { return _moveIds.Length; }
        }

        public string MoveId(int bit)
        {
            return _moveIds[bit];
        }

        /// <summary>The mask bit of a move, or 0 when the character does not have it.</summary>
        public uint BitFor(string moveId)
        {
            for (int i = 0; i < _moveIds.Length; i++)
            {
                if (_moveIds[i] == moveId)
                {
                    return 1u << i;
                }
            }
            return 0u;
        }
    }
}
