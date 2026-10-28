using Bajoterra.Client;
using Bajoterra.Client.Camera;
using Bajoterra.Client.Content;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Puts the Unity camera where the engine-free <see cref="ThirdPersonCamera"/> says. The aim (yaw and pitch) is
    /// owned by the rig: mouse movement turns it, and the very same values are what the client sends to the server.
    /// Screen shake is added to the Unity camera only, never to the pose that is sent, so a shaking screen does not
    /// spoil the aim. A pose that is not a number (it would make Unity stop drawing) is refused and the last good one
    /// is kept.
    /// </summary>
    public sealed class CameraRig
    {
        private readonly ThirdPersonCamera _rig = new ThirdPersonCamera();
        private Camera _camera;
        private float _shake;
        private bool _warned;

        public CameraRig(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>The pose computed by the last <see cref="Follow"/>.</summary>
        public CameraPose Pose { get; private set; }

        public Camera Camera
        {
            get { return _camera; }
        }

        /// <summary>Points the rig at a different Unity camera (the game made a new one).</summary>
        public void Rebind(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>Turns the aim by a mouse movement in pixels.</summary>
        public void Look(Vector2 pixels, float sensitivity, bool invertY)
        {
            if (float.IsNaN(pixels.x) || float.IsNaN(pixels.y))
            {
                return;
            }
            float vertical = invertY ? -pixels.y : pixels.y;
            _rig.AddLook(pixels.x * sensitivity, vertical * sensitivity);
        }

        /// <summary>Sets the aim directly (spectating somebody else).</summary>
        public void SetAim(float yaw, float pitch)
        {
            _rig.SetAim(yaw, pitch);
        }

        public float Yaw
        {
            get { return _rig.Yaw; }
        }

        /// <summary>Shakes the screen: <paramref name="metres"/> is the starting size of the shake.</summary>
        public void Shake(float metres)
        {
            _shake = Mathf.Max(_shake, Mathf.Clamp(metres, 0f, 0.6f));
        }

        /// <summary>Solves the camera for a character at <paramref name="feet"/> and moves the Unity camera there.</summary>
        public void Follow(Vec3 feet, float hitboxHeight, ArenaMap map)
        {
            if (_camera == null)
            {
                return;
            }
            CameraPose pose = _rig.Solve(feet, hitboxHeight, map);
            Vector3 position = pose.Position.ToUnity();
            if (!IsFinite(position) || float.IsNaN(pose.Yaw) || float.IsNaN(pose.Pitch))
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("Bajoterra: the camera pose was not a number (feet " + feet.X + ", " + feet.Y + ", "
                                     + feet.Z + "); keeping the last good one.");
                }
                return;
            }
            Pose = pose;
            Transform t = _camera.transform;
            if (_shake > 0.001f)
            {
                position += Random.insideUnitSphere * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 0.9f);
            }
            t.position = position;
            t.rotation = Quaternion.Euler(-pose.Pitch * Mathf.Rad2Deg, pose.Yaw * Mathf.Rad2Deg, 0f);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x)
                     || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
