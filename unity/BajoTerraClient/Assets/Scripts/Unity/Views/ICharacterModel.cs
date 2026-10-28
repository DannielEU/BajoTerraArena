using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>What a body needs to be drawn this frame. Built by <see cref="CombatantView"/> from the server's state.</summary>
    public struct CharacterPose
    {
        public float DeltaTime;

        /// <summary>The hitbox height now (60 % of standing while crouching or rolling).</summary>
        public float Height;

        /// <summary>How fast it is really moving, in the body's own frame: x to its right, y forward (m/s).</summary>
        public Vector2 Velocity;

        public bool Airborne;
        public bool Rolling;

        /// <summary>0 standing .. 1 down (defeated).</summary>
        public float Fall;

        /// <summary>Aim pitch in radians (positive looks up).</summary>
        public float Pitch;

        /// <summary>Colour of the selected slug, for the blaster's slug dial.</summary>
        public Color SlugColor;
    }

    /// <summary>The visible body of one combatant.</summary>
    public interface ICharacterModel
    {
        /// <summary>The model's root, feet at its origin, facing +Z.</summary>
        Transform Body { get; }

        /// <summary>True when the model animates a roll itself (the owner must not spin it as well).</summary>
        bool HandlesRoll { get; }

        void Pose(in CharacterPose pose);

        /// <summary>Greys the whole body out when defeated, and restores it when the next round brings it back.</summary>
        void SetDead(bool dead);

        /// <summary>Releases what the model holds besides its game objects (animation graphs).</summary>
        void Destroy();
    }

    /// <summary>Picks the best body for a character: the Blender model if there is one, else the primitive stand-in.</summary>
    public static class CharacterModels
    {
        public static ICharacterModel Create(Transform parent, string characterId, float radius, float standingHeight,
            Color team)
        {
            ICharacterModel rigged = RiggedCharacterModel.TryCreate(parent, characterId, standingHeight, team);
            return rigged ?? new PrimitiveCharacterModel(parent, CharacterStyle.For(characterId), radius, standingHeight, team);
        }
    }
}
