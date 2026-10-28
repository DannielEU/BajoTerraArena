using System;
using System.Runtime.CompilerServices;

namespace Bajoterra.Client
{
    /// <summary>
    /// Minimal 3D vector so the engine-free client code never needs UnityEngine types. Y is up, +Z is forward and
    /// +X is right, the same convention as the server and as Unity.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        public float Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        public float SqrLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return X * X + Y * Y + Z * Z; }
        }

        /// <summary>Unit vector in the same direction, or zero when the length is negligible.</summary>
        public Vec3 Normalized()
        {
            float length = Length;
            return length > 1e-6f ? new Vec3(X / length, Y / length, Z / length) : Zero;
        }

        public static float Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static float Distance(Vec3 a, Vec3 b)
        {
            return (a - b).Length;
        }

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            return new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        /// <summary>Unit vector for a yaw/pitch pair (yaw 0 faces +Z, positive pitch looks up).</summary>
        public static Vec3 FromAngles(float yaw, float pitch)
        {
            float cosPitch = (float)Math.Cos(pitch);
            return new Vec3((float)Math.Sin(yaw) * cosPitch, (float)Math.Sin(pitch), (float)Math.Cos(yaw) * cosPitch);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vec3 operator *(Vec3 a, float scale)
        {
            return new Vec3(a.X * scale, a.Y * scale, a.Z * scale);
        }

        public bool Equals(Vec3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Vec3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ", " + Z.ToString("0.###") + ")";
        }
    }

    /// <summary>Angle helpers shared by interpolation and the camera.</summary>
    public static class Angles
    {
        public const float TwoPi = (float)(2.0 * Math.PI);

        /// <summary>Wraps an angle to [-pi, pi].</summary>
        public static float Wrap(float angle)
        {
            angle = (float)Math.IEEERemainder(angle, 2.0 * Math.PI);
            return angle;
        }

        /// <summary>Interpolates along the shortest arc between two angles.</summary>
        public static float LerpAngle(float from, float to, float t)
        {
            return from + Wrap(to - from) * t;
        }
    }
}
