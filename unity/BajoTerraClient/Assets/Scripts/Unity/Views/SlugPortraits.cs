using System.Collections.Generic;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Little 3D portraits of the slugs for the lobby: each model (<see cref="SlugModels"/>) is photographed once, in a
    /// studio far below the cavern with its own light and camera that see nothing else, and kept as a texture with a
    /// clear background. A slug without a model has no portrait (null).
    /// </summary>
    public static class SlugPortraits
    {
        private const int Size = 160;
        /// <summary>A layer nothing else uses, so the studio's camera and light only see the slug.</summary>
        private const int StudioLayer = 30;
        private static readonly Vector3 StudioOrigin = new Vector3(0f, -600f, 0f);

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        /// <summary>The portrait of a slug (made the first time it is asked for), or null when it has no model.</summary>
        public static Texture2D Get(string slugId)
        {
            if (Cache.TryGetValue(slugId, out Texture2D cached))
            {
                return cached;
            }
            Texture2D texture = Photograph(slugId);
            Cache[slugId] = texture;
            return texture;
        }

        private static Texture2D Photograph(string slugId)
        {
            var studio = new GameObject("Slug portrait studio");
            // Each studio gets its own spot: a destroyed one lingers until the end of the frame.
            studio.transform.position = StudioOrigin + Vector3.right * (Cache.Count * 10f);
            try
            {
                Transform model = SlugModels.Create(slugId, studio.transform);
                if (model == null)
                {
                    return null;
                }
                // Facing the camera, turned a little to show its depth.
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.Euler(0f, 200f, 0f);
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = StudioLayer;
                }

                var key = new GameObject("Key", typeof(Light)).GetComponent<Light>();
                key.transform.SetParent(studio.transform, false);
                key.type = LightType.Point;
                key.range = 12f;
                key.intensity = 2.2f;
                key.color = new Color(1f, 0.97f, 0.9f);
                key.cullingMask = 1 << StudioLayer;
                key.transform.localPosition = new Vector3(-1.4f, 1.6f, -2.4f);
                var rim = new GameObject("Rim", typeof(Light)).GetComponent<Light>();
                rim.transform.SetParent(studio.transform, false);
                rim.type = LightType.Point;
                rim.range = 8f;
                rim.intensity = 1.6f;
                rim.color = new Color(0.45f, 1f, 0.9f);
                rim.cullingMask = 1 << StudioLayer;
                rim.transform.localPosition = new Vector3(1.6f, 1.2f, 1.8f);

                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(studio.transform, false);
                camera.enabled = false;
                camera.cullingMask = 1 << StudioLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 0.58f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.transform.localPosition = new Vector3(0f, 0.05f, -4f);
                camera.transform.localRotation = Quaternion.identity;

                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;
                RenderTexture.active = rt;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Portrait " + slugId };
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                rt.Release();
                Object.Destroy(rt);
                return texture;
            }
            finally
            {
                Object.Destroy(studio);
            }
        }
    }
}
