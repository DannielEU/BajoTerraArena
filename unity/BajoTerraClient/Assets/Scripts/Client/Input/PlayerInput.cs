using Bajoterra.Protocol;

namespace Bajoterra.Client.Input
{
    /// <summary>
    /// One tick of player intent, independent of the device that produced it. The same value drives the local
    /// prediction and the <c>InputFrame</c> sent to the server, so both always see identical input.
    /// </summary>
    public struct PlayerInput
    {
        /// <summary>Strafe axis in [-1, 1]; positive moves right.</summary>
        public float MoveX;

        /// <summary>Forward axis in [-1, 1].</summary>
        public float MoveZ;

        /// <summary>Camera yaw in radians (0 faces +Z).</summary>
        public float AimYaw;

        /// <summary>Camera pitch in radians (positive looks up).</summary>
        public float AimPitch;

        /// <summary>World position of the camera; the shot converges on the point under the crosshair.</summary>
        public Vec3 CameraPosition;

        public bool HasCamera;

        public bool Fire;

        /// <summary>Fusion button held: the next shot fuses two slugs once the fusion energy is full.</summary>
        public bool Fusion;

        /// <summary>Bit i requests the i-th move of the character's move set (held while the key is down).</summary>
        public uint MoveMask;

        /// <summary>Slot 0-4 to select, or -1 for no change.</summary>
        public int SelectSlot;

        /// <summary>-1 selects the previous available slot, +1 the next one.</summary>
        public int CycleSlot;

        /// <summary>Celebration dance 1-3 to start this tick, or 0.</summary>
        public uint Dance;

        /// <summary>An input with nothing pressed and no slot change requested.</summary>
        public static PlayerInput Idle
        {
            get { return new PlayerInput { SelectSlot = -1 }; }
        }

        public InputFrame ToFrame(uint sequence)
        {
            return new InputFrame
            {
                Sequence = sequence,
                MoveX = MoveX,
                MoveZ = MoveZ,
                AimYaw = AimYaw,
                AimPitch = AimPitch,
                HasCamera = HasCamera,
                CamX = CameraPosition.X,
                CamY = CameraPosition.Y,
                CamZ = CameraPosition.Z,
                Fire = Fire,
                Fusion = Fusion,
                MoveMask = MoveMask,
                SelectSlot = SelectSlot,
                CycleSlot = CycleSlot,
                Dance = Dance
            };
        }
    }
}
