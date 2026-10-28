using Bajoterra.Client;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>Conversions between the engine-free client types and Unity types.</summary>
    public static class VectorExtensions
    {
        public static Vector3 ToUnity(this Vec3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Vec3 ToClient(this Vector3 v)
        {
            return new Vec3(v.x, v.y, v.z);
        }
    }
}
