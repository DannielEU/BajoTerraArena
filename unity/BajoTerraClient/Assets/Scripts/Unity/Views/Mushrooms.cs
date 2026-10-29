using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The glowing mushrooms of the show's caverns: a pale stem, a domed cap that shines in its own colour, a darker
    /// band of gills under it and a few bright spots on top. Pure decoration made of primitives without colliders,
    /// shared by the arena and the menu backdrop.
    /// </summary>
    public static class Mushrooms
    {
        public static readonly Color[] Colors =
        {
            new Color(0.2f, 0.95f, 0.85f), new Color(0.35f, 0.65f, 1f), new Color(0.55f, 1f, 0.35f),
            new Color(1f, 0.55f, 0.25f)
        };

        /// <summary>
        /// A clump of two to four mushrooms around <paramref name="anchor"/>, the tallest one about
        /// <paramref name="height"/> high, leaning towards <paramref name="inward"/>.
        /// </summary>
        public static void Clump(Transform parent, Vector3 anchor, Vector3 inward, float height, Color color,
            System.Random random)
        {
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            int count = 2 + random.Next(3);
            for (int i = 0; i < count; i++)
            {
                float scale = i == 0 ? 1f : 0.35f + (float)random.NextDouble() * 0.45f;
                Vector3 at = anchor + inward * ((float)random.NextDouble() * 0.6f)
                                    + side * ((float)(random.NextDouble() - 0.5) * 2f * Mathf.Max(0.6f, height * 0.4f));
                float lean = 6f + (float)random.NextDouble() * 10f;
                One(parent, at, height * scale, color, inward, lean, random);
            }
        }

        /// <summary>One mushroom standing at <paramref name="foot"/> (world position).</summary>
        public static Transform One(Transform parent, Vector3 foot, float height, Color color, Vector3 leanTowards,
            float leanDegrees, System.Random random)
        {
            var root = new GameObject("Mushroom").transform;
            root.SetParent(parent, false);
            root.position = foot;
            Vector3 axis = Vector3.Cross(Vector3.up, leanTowards.sqrMagnitude > 0f ? leanTowards.normalized : Vector3.forward);
            root.rotation = Quaternion.AngleAxis(leanDegrees, axis) * Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

            float capRadius = Mathf.Max(0.18f, height * (0.36f + (float)random.NextDouble() * 0.14f));
            float stemRadius = capRadius * 0.24f;
            float stemHeight = height - capRadius * 0.3f;

            Material stem = MaterialFactory.Emissive(new Color(0.78f, 0.95f, 0.88f), 0.35f, 0.3f);
            GameObject trunk = Gfx.Primitive(PrimitiveType.Cylinder, "Stem", root, stem);
            trunk.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(stemRadius * 2f, stemHeight * 0.5f, stemRadius * 2f);
            Gfx.NoShadows(trunk);

            Material gills = MaterialFactory.Emissive(Color.Lerp(color, Color.black, 0.45f), 0.55f, 0.2f);
            GameObject under = Gfx.Primitive(PrimitiveType.Sphere, "Gills", root, gills);
            under.transform.localPosition = new Vector3(0f, stemHeight, 0f);
            under.transform.localScale = new Vector3(capRadius * 1.9f, capRadius * 0.35f, capRadius * 1.9f);
            Gfx.NoShadows(under);

            Material glow = MaterialFactory.Emissive(color, 0.95f, 0.75f);
            GameObject cap = Gfx.Primitive(PrimitiveType.Sphere, "Cap", root, glow);
            cap.transform.localPosition = new Vector3(0f, stemHeight + capRadius * 0.12f, 0f);
            cap.transform.localScale = new Vector3(capRadius * 2f, capRadius * 1.1f, capRadius * 2f);
            Gfx.NoShadows(cap);

            if (capRadius > 0.3f)
            {
                Material spot = MaterialFactory.Emissive(Color.Lerp(color, Color.white, 0.75f), 1.7f, 0.6f);
                int spots = 3 + random.Next(3);
                for (int i = 0; i < spots; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.5f) / spots * Mathf.PI * 2f;
                    float along = 0.35f + (float)random.NextDouble() * 0.4f;
                    // A point on the cap's upper surface (an ellipsoid of radii capRadius and 0.55 capRadius).
                    var on = new Vector3(Mathf.Cos(angle) * along, 0f, Mathf.Sin(angle) * along);
                    on.y = Mathf.Sqrt(Mathf.Max(0f, 1f - along * along)) * 0.55f;
                    GameObject dot = Gfx.Primitive(PrimitiveType.Sphere, "Spot", root, spot);
                    dot.transform.localPosition = new Vector3(0f, stemHeight + capRadius * 0.12f, 0f) + on * capRadius;
                    float size = capRadius * (0.18f + (float)random.NextDouble() * 0.12f);
                    dot.transform.localScale = new Vector3(size, size * 0.45f, size);
                    Gfx.NoShadows(dot);
                }
            }
            return root;
        }
    }
}
