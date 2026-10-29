using System.Collections.Generic;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Live head-and-shoulders portraits of the heroes for the lobby cards: each hero stands in a small studio far
    /// below the cavern, idling, with a camera of its own that draws into a texture the card shows. The cameras only
    /// run while the portraits are on screen.
    /// </summary>
    public sealed class HeroPortraits
    {
        private const int TextureWidth = 256;
        private const int TextureHeight = 300;
        private const float Spacing = 12f;

        private static readonly Vector3 StudioOrigin = new Vector3(0f, -300f, 0f);
        private static readonly Color Backdrop = new Color(0.07f, 0.3f, 0.36f);

        private sealed class Sitter
        {
            public ICharacterModel Model;
            public Camera Camera;
            public RenderTexture Texture;
            public Transform Root;
            public float Height;
            public float Phase;
        }

        private readonly Transform _root;
        private readonly Dictionary<string, Sitter> _sitters = new Dictionary<string, Sitter>();
        private float _time;
        private bool _running;

        public HeroPortraits(Transform parent)
        {
            _root = new GameObject("Hero portraits").transform;
            _root.SetParent(parent, false);
            _root.position = StudioOrigin;
        }

        /// <summary>The portrait texture of a hero, building the hero's studio the first time.</summary>
        public Texture Get(string characterId, float radius, float height)
        {
            if (_sitters.TryGetValue(characterId, out Sitter existing))
            {
                return existing.Texture;
            }
            var sitter = new Sitter { Height = height, Phase = _sitters.Count * 1.9f };
            sitter.Root = new GameObject(characterId).transform;
            sitter.Root.SetParent(_root, false);
            sitter.Root.localPosition = new Vector3(_sitters.Count * Spacing, 0f, 0f);
            // Facing the camera (models look along +z), turned a little to show some depth.
            sitter.Root.localRotation = Quaternion.Euler(0f, 200f, 0f);
            var spin = new GameObject("Spin").transform;
            spin.SetParent(sitter.Root, false);
            spin.localPosition = new Vector3(0f, height * 0.5f, 0f);
            sitter.Model = CharacterModels.Create(spin, characterId, radius, height, Palette.TeamOwn);
            sitter.Model.Body.localPosition = new Vector3(0f, -height * 0.5f, 0f);

            // Head and shoulders, framed the same way for a tiny mole and a tall Kord.
            sitter.Texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Portrait " + characterId,
                antiAliasing = 4
            };
            var cameraObject = new GameObject("Camera", typeof(Camera));
            cameraObject.transform.SetParent(sitter.Root.parent, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = Spacing * 0.5f;
            camera.targetTexture = sitter.Texture;
            camera.depth = -10f;
            camera.enabled = _running;
            Vector3 target = sitter.Root.localPosition + new Vector3(0f, height * 0.8f, 0f);
            Vector3 eye = target + new Vector3(0.1f * height, 0.04f * height, -1.25f * height - 0.35f);
            cameraObject.transform.localPosition = eye;
            cameraObject.transform.localRotation = Quaternion.LookRotation(target - eye, Vector3.up);
            sitter.Camera = camera;

            // A warm key light from the front and a teal rim from behind, like the show's cave lighting.
            Vector3 world = sitter.Root.position;
            Gfx.AddPointLight(sitter.Root, "Key", world + new Vector3(-0.8f, height * 1.1f, -1.4f), new Color(1f, 0.9f, 0.75f),
                4f + height, 1.6f);
            Gfx.AddPointLight(sitter.Root, "Rim", world + new Vector3(0.6f, height * 1.05f, 1.2f), new Color(0.3f, 1f, 0.9f),
                3f + height, 2.2f);
            _sitters.Add(characterId, sitter);
            return sitter.Texture;
        }

        /// <summary>Starts or stops the cameras (they cost a render each frame while running).</summary>
        public void SetRunning(bool running)
        {
            if (running == _running)
            {
                return;
            }
            _running = running;
            foreach (Sitter sitter in _sitters.Values)
            {
                sitter.Camera.enabled = running;
            }
        }

        /// <summary>Idles the heroes; call once per frame while the portraits are shown.</summary>
        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            foreach (Sitter sitter in _sitters.Values)
            {
                var pose = new CharacterPose
                {
                    DeltaTime = deltaTime,
                    Height = sitter.Height,
                    Pitch = 0.1f + Mathf.Sin(_time * 0.7f + sitter.Phase) * 0.05f,
                    SlugColor = UiKit.AccentColor
                };
                sitter.Model.Pose(in pose);
                sitter.Root.localRotation = Quaternion.Euler(0f, 200f + Mathf.Sin(_time * 0.5f + sitter.Phase) * 8f, 0f);
            }
        }

        public void Destroy()
        {
            foreach (Sitter sitter in _sitters.Values)
            {
                sitter.Model.Destroy();
                sitter.Camera.targetTexture = null;
                sitter.Texture.Release();
                Object.Destroy(sitter.Texture);
            }
            _sitters.Clear();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }
    }
}
