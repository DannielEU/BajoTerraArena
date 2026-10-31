using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The fifteen slugs modelled in Blender (<c>unity/art/blender/bt_slug_models.py</c>), one FBX per slug id in
    /// <c>Resources/Models/Slugs</c>. Each model is a single mesh standing on its foot and facing +Z; this hands out
    /// copies of it with the game's own materials (slightly glossy, like slime, with a touch of glow so a slug in flight
    /// still reads in the dark cavern; <c>Glow</c> parts such as Phosphoro's belly shine on their own), scaled to a
    /// height of one unit and pivoting on the middle of the body. A slug without a model gives null and the caller
    /// keeps its simple round stand-in.
    /// </summary>
    public static class SlugModels
    {
        public const string ResourcePath = "Models/Slugs/";

        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Prefabs.Clear();
            Materials.Clear();
        }

        /// <summary>True when the slug has a 3D model.</summary>
        public static bool Has(string slugId)
        {
            return Prefab(slugId) != null;
        }

        /// <summary>
        /// A new copy of the slug's model under <paramref name="parent"/>: one unit tall, its centre on the parent's
        /// origin, facing the parent's +Z. Null when the slug has no model.
        /// </summary>
        public static Transform Create(string slugId, Transform parent)
        {
            GameObject prefab = Prefab(slugId);
            if (prefab == null)
            {
                return null;
            }
            var pivot = new GameObject("Slug model " + slugId).transform;
            pivot.SetParent(parent, false);
            GameObject model = Object.Instantiate(prefab, pivot, false);
            model.name = slugId;
            Bounds bounds = default;
            bool first = true;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }
                Material[] source = renderer.sharedMaterials;
                var converted = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    converted[i] = Convert(source[i]);
                }
                renderer.sharedMaterials = converted;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // Measured in the pivot's space: the imported model may carry its own axis rotation.
                Bounds part = TransformBounds(filter.transform, pivot, filter.sharedMesh.bounds);
                if (first)
                {
                    bounds = part;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(part);
                }
            }
            // Scale about the model's own origin so it is one unit tall, then move its middle onto the pivot.
            float k = 1f / Mathf.Max(0.01f, bounds.size.y);
            Transform t = model.transform;
            t.localScale = t.localScale * k;
            t.localPosition = (t.localPosition - bounds.center) * k;
            return pivot;
        }

        private static GameObject Prefab(string slugId)
        {
            if (string.IsNullOrEmpty(slugId))
            {
                return null;
            }
            if (!Prefabs.TryGetValue(slugId, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>(ResourcePath + slugId);
                Prefabs[slugId] = prefab;
            }
            return prefab;
        }

        /// <summary>A mesh's local bounds as an axis-aligned box in the space of <paramref name="root"/>.</summary>
        private static Bounds TransformBounds(Transform part, Transform root, Bounds local)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * part.localToWorldMatrix;
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);
                Vector3 p = toRoot.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>The game's version of an imported material, shared by every copy of every slug.</summary>
        private static Material Convert(Material imported)
        {
            if (imported == null)
            {
                return MaterialFactory.Lit(Color.magenta);
            }
            string name = imported.name;
            if (Materials.TryGetValue(name, out Material material) && material != null)
            {
                return material;
            }
            Color c = imported.color;
            switch (name)
            {
                case "Eye":
                case "Tooth":
                    material = MaterialFactory.Surface(null, c, 0.75f, 0f);
                    break;
                case "Pupil":
                case "SlugEyeBlack":
                    material = MaterialFactory.Surface(null, c, 0.85f, 0f);
                    break;
                default:
                    if (name.Contains("Glow"))
                    {
                        material = MaterialFactory.Emissive(c, 1.6f, 0.7f);
                    }
                    else
                    {
                        // Slime: glossy, and a faint glow of its own colour so it still reads in the dark.
                        material = MaterialFactory.LitInstance(c);
                        MaterialFactory.SetSmoothness(material, 0.62f);
                        MaterialFactory.SetEmission(material, c * 0.12f);
                    }
                    break;
            }
            Materials[name] = material;
            return material;
        }
    }
}
