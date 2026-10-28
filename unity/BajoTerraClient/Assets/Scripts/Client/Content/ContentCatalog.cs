using System;
using System.Collections.Generic;
using Bajoterra.Protocol;

namespace Bajoterra.Client.Content
{
    /// <summary>
    /// The game content as the server announced it in <c>Welcome</c>: ids in index order plus the numeric details.
    /// Snapshots and events refer to content by index, so this is how the client turns an index into a name, an
    /// element or the parameters of a move. Adding a slug or a move on the server needs no client change here.
    /// </summary>
    public sealed class ContentCatalog
    {
        private readonly Dictionary<string, int> _characterIndex = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _slugIndex = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _moveIndex = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _pickupIndex = new Dictionary<string, int>();

        public ContentCatalog(ContentManifest manifest, ContentDetails details)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Details = details ?? throw new ArgumentNullException(nameof(details));
            Require(manifest.SlugIds.Count == details.Slugs.Count, "slugs");
            Require(manifest.CharacterIds.Count == details.Characters.Count, "characters");
            Require(manifest.MoveIds.Count == details.Moves.Count, "moves");
            Require(manifest.PickupIds.Count == details.Pickups.Count, "pickups");
            Index(manifest.SlugIds, _slugIndex);
            Index(manifest.CharacterIds, _characterIndex);
            Index(manifest.MoveIds, _moveIndex);
            Index(manifest.PickupIds, _pickupIndex);
        }

        public ContentManifest Manifest { get; }

        public ContentDetails Details { get; }

        public int SlugCount
        {
            get { return Details.Slugs.Count; }
        }

        public SlugInfo Slug(int index)
        {
            return Details.Slugs[index];
        }

        public MoveInfo Move(int index)
        {
            return Details.Moves[index];
        }

        public CharacterInfo Character(int index)
        {
            return Details.Characters[index];
        }

        public PickupInfo Pickup(int index)
        {
            return Details.Pickups[index];
        }

        public string ElementName(int element)
        {
            return element >= 0 && element < Manifest.ElementIds.Count ? Manifest.ElementIds[element] : "NEUTRAL";
        }

        /// <summary>Index of the slug with the given id, or -1.</summary>
        public int SlugIndexOf(string id)
        {
            return _slugIndex.TryGetValue(id, out int index) ? index : -1;
        }

        public int CharacterIndexOf(string id)
        {
            return _characterIndex.TryGetValue(id, out int index) ? index : -1;
        }

        public int MoveIndexOf(string id)
        {
            return _moveIndex.TryGetValue(id, out int index) ? index : -1;
        }

        public int PickupIndexOf(string id)
        {
            return _pickupIndex.TryGetValue(id, out int index) ? index : -1;
        }

        /// <summary>The character with the given id, or null.</summary>
        public CharacterInfo FindCharacter(string id)
        {
            int index = CharacterIndexOf(id);
            return index < 0 ? null : Character(index);
        }

        /// <summary>The moves of a character in move-mask bit order.</summary>
        public MoveInfo[] MoveSetOf(CharacterInfo character)
        {
            var moves = new MoveInfo[character.MoveIndices.Count];
            for (int i = 0; i < moves.Length; i++)
            {
                moves[i] = Move((int)character.MoveIndices[i]);
            }
            return moves;
        }

        private static void Require(bool aligned, string what)
        {
            if (!aligned)
            {
                throw new ArgumentException("The server's content details do not match its manifest (" + what + ")");
            }
        }

        private static void Index(IList<string> ids, Dictionary<string, int> into)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                into[ids[i]] = i;
            }
        }
    }
}
