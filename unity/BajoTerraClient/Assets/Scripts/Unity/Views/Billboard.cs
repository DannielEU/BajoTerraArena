using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>Turns its object to face the main camera every frame (halos, flares).</summary>
    public sealed class Billboard : MonoBehaviour
    {
        private static Mesh _quad;
        private Transform _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _quad = null;
        }

        /// <summary>A unit quad centred on the origin, facing -z (towards a camera looking along +z).</summary>
        public static Mesh QuadMesh()
        {
            if (_quad == null)
            {
                _quad = new Mesh { name = "Billboard" };
                _quad.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                };
                _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                _quad.RecalculateBounds();
            }
            return _quad;
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }
                _camera = main.transform;
            }
            transform.rotation = _camera.rotation;
        }
    }
}
