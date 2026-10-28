using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>Small helpers to put procedural meshes and primitives into the scene without colliders.</summary>
    public static class Gfx
    {
        /// <summary>A game object with a mesh and materials.</summary>
        public static GameObject Make(string name, Mesh mesh, Transform parent, params Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        /// <summary>A built-in primitive with its collider removed.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }
            go.transform.SetParent(parent, false);
            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }
            return go;
        }

        public static void NoShadows(GameObject go)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public static Light AddPointLight(Transform parent, string name, Vector3 position, Color color, float range,
            float intensity)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>A camera-facing glow sprite (a quad with the additive material) at a position.</summary>
        public static Transform Halo(Transform parent, string name, Vector3 position, Color color, float size,
            Texture2D sprite = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(Billboard));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<MeshFilter>().sharedMesh = Billboard.QuadMesh();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = HaloMaterial(sprite);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        private static Material _haloSoft;
        private static Material _haloFlare;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _haloSoft = null;
            _haloFlare = null;
        }

        private static Material HaloMaterial(Texture2D sprite)
        {
            if (sprite == null || sprite == Textures.SoftDot)
            {
                return _haloSoft != null ? _haloSoft : _haloSoft = MaterialFactory.GlowInstance(Textures.SoftDot, 1f);
            }
            return _haloFlare != null ? _haloFlare : _haloFlare = MaterialFactory.GlowInstance(sprite, 1f);
        }
    }
}
