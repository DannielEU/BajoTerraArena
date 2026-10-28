using System;
using Bajoterra.Client.Content;

namespace Bajoterra.Client.Camera
{
    /// <summary>Where the camera is and where it looks.</summary>
    public readonly struct CameraPose
    {
        public readonly Vec3 Position;
        public readonly float Yaw;
        public readonly float Pitch;

        public CameraPose(Vec3 position, float yaw, float pitch)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
        }

        public Vec3 Forward
        {
            get { return Vec3.FromAngles(Yaw, Pitch); }
        }
    }

    /// <summary>
    /// Over-the-shoulder orbit camera. Mouse movement turns the aim (yaw and pitch); the camera sits behind and
    /// slightly to the right of the character, pulled in front of any wall that would block the view. The aim
    /// direction and the camera position are exactly what the client sends to the server, which traces the ray from
    /// the camera to find the point under the crosshair.
    /// </summary>
    public sealed class ThirdPersonCamera
    {
        public const float MinPitch = -1.2f;
        public const float MaxPitch = 1.3f;

        /// <summary>Distance kept between the camera and the surface it was pushed back from.</summary>
        private const float WallMargin = 0.15f;
        private const float ClearanceRadius = 0.2f;

        public float Yaw { get; private set; }

        public float Pitch { get; private set; }

        /// <summary>Distance behind the character, in metres.</summary>
        public float Distance { get; set; } = 3.4f;

        /// <summary>Sideways offset to the right of the aim, in metres.</summary>
        public float ShoulderOffset { get; set; } = 0.6f;

        /// <summary>Height of the pivot as a fraction of the character's hitbox height.</summary>
        public float PivotHeightFraction { get; set; } = 0.88f;

        public void SetAim(float yaw, float pitch)
        {
            Yaw = Angles.Wrap(yaw);
            Pitch = Clamp(pitch, MinPitch, MaxPitch);
        }

        /// <summary>Turns the aim by the given amounts (radians); positive yaw turns right, positive pitch looks up.</summary>
        public void AddLook(float deltaYaw, float deltaPitch)
        {
            SetAim(Yaw + deltaYaw, Pitch + deltaPitch);
        }

        /// <summary>Computes the camera pose for a character standing at <paramref name="feet"/>.</summary>
        public CameraPose Solve(Vec3 feet, float hitboxHeight, ArenaMap map)
        {
            Vec3 pivot = new Vec3(feet.X, feet.Y + hitboxHeight * PivotHeightFraction, feet.Z);
            Vec3 forward = Vec3.FromAngles(Yaw, Pitch);
            Vec3 right = new Vec3((float)Math.Cos(Yaw), 0f, -(float)Math.Sin(Yaw));
            Vec3 desired = pivot - forward * Distance + right * ShoulderOffset;
            Vec3 offset = desired - pivot;
            float length = offset.Length;
            Vec3 position = desired;
            if (map != null && length > 1e-4f)
            {
                Vec3 direction = offset * (1f / length);
                float clear = map.SightDistance(pivot, direction, length, ClearanceRadius);
                if (clear < length)
                {
                    position = pivot + direction * Math.Max(0.25f, clear - WallMargin);
                }
            }
            return new CameraPose(position, Yaw, Pitch);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
