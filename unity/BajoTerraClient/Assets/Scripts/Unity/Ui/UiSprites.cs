using System.Collections.Generic;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The few sprites the interface is made of, drawn in code with signed-distance shapes: plates with cut corners and
    /// rounded rectangles (nine-sliced, so one small texture serves every panel and button), their outlines, circles,
    /// rings, a soft
    /// vertical gradient and a diamond. White with alpha, so each widget tints them with its own colour.
    /// </summary>
    public static class UiSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        /// <summary>A filled rounded rectangle; use it with <c>Image.Type.Sliced</c>.</summary>
        public static Sprite Rounded(int radius = 14)
        {
            return Cached("round" + radius, () => RoundedRect(radius, 0));
        }

        /// <summary>The outline of a rounded rectangle, <paramref name="thickness"/> pixels wide; use it sliced.</summary>
        public static Sprite Outline(int radius = 14, int thickness = 2)
        {
            return Cached("outline" + radius + "_" + thickness, () => RoundedRect(radius, thickness));
        }

        /// <summary>
        /// A filled rectangle with its corners cut at 45 degrees, <paramref name="chamfer"/> pixels deep: the shape of
        /// the show's metal plates and buttons. Use it with <c>Image.Type.Sliced</c>.
        /// </summary>
        public static Sprite Plate(int chamfer = 12)
        {
            return Cached("plate" + chamfer, () => ChamferedRect(chamfer, 0));
        }

        /// <summary>The outline of <see cref="Plate"/>, <paramref name="thickness"/> pixels wide; use it sliced.</summary>
        public static Sprite PlateOutline(int chamfer = 12, int thickness = 2)
        {
            return Cached("plateline" + chamfer + "_" + thickness, () => ChamferedRect(chamfer, thickness));
        }

        /// <summary>A filled circle (not sliced).</summary>
        public static Sprite Circle()
        {
            return Cached("circle", () => Shape(64, (x, y) => Mathf.Clamp01(0.5f * 64f - 0.5f - Dist(x, y, 32f, 32f) + 0.5f)));
        }

        /// <summary>A ring (not sliced).</summary>
        public static Sprite Ring(float thickness = 0.12f)
        {
            return Cached("ring" + thickness, () => Shape(96, (x, y) =>
            {
                float r = Dist(x, y, 48f, 48f) / 48f;
                float edge = 1.2f / 48f;
                float outer = Mathf.Clamp01((1f - r) / edge);
                float inner = Mathf.Clamp01((r - (1f - thickness)) / edge);
                return Mathf.Min(outer, inner);
            }));
        }

        /// <summary>Opaque at the top, transparent at the bottom (a sheen over a panel).</summary>
        public static Sprite Gradient()
        {
            return Cached("gradient", () =>
            {
                var texture = NewTexture(4, 64);
                var pixels = new Color32[4 * 64];
                for (int y = 0; y < 64; y++)
                {
                    float a = y / 63f;
                    for (int x = 0; x < 4; x++)
                    {
                        pixels[y * 4 + x] = new Color(1f, 1f, 1f, a * a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>Opaque at the left edge, transparent at the right (the feathered edge of a side panel).</summary>
        public static Sprite FadeRight()
        {
            return Cached("faderight", () =>
            {
                var texture = NewTexture(64, 4);
                var pixels = new Color32[64 * 4];
                for (int x = 0; x < 64; x++)
                {
                    float a = 1f - x / 63f;
                    for (int y = 0; y < 4; y++)
                    {
                        pixels[y * 64 + x] = new Color(1f, 1f, 1f, a * a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>A diamond (a square turned 45 degrees), not sliced.</summary>
        public static Sprite Diamond()
        {
            return Cached("diamond", () => Shape(64, (x, y) =>
            {
                float d = (Mathf.Abs(x - 32f) + Mathf.Abs(y - 32f)) / 31f;
                return Mathf.Clamp01((1f - d) * 31f);
            }));
        }

        /// <summary>The soft vignette used for the damage flash (transparent centre, red-able edges).</summary>
        public static Sprite Vignette()
        {
            return Cached("vignette", () =>
            {
                Texture2D source = Textures.Vignette;
                return Sprite.Create(source, new Rect(0, 0, source.width, source.height), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>A soft round glow (not sliced).</summary>
        public static Sprite Glow()
        {
            return Cached("glow", () =>
            {
                Texture2D source = Textures.SoftDot;
                return Sprite.Create(source, new Rect(0, 0, source.width, source.height), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ------------------------------------------------------------------------------------------ drawing

        private static Sprite RoundedRect(int radius, int outline)
        {
            int size = radius * 2 + 4;
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to a rounded box of half-size (half - 1) with corner radius `radius`.
                    float px = Mathf.Abs(x + 0.5f - half) - (half - 1f - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - 1f - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
                    float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                    float d = outside + inside - radius;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (outline > 0)
                    {
                        alpha *= Mathf.Clamp01(d + outline + 0.5f);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            int border = radius + 2;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite ChamferedRect(int chamfer, int outline)
        {
            chamfer = Mathf.Max(1, chamfer);
            int size = chamfer * 2 + 8;
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float extent = half - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half);
                    float py = Mathf.Abs(y + 0.5f - half);
                    // Distance to the box, then to the 45-degree cut through (extent, extent - chamfer).
                    float box = Mathf.Max(px - extent, py - extent);
                    float cut = (px + py - (2f * extent - chamfer)) * 0.70710678f;
                    float d = Mathf.Max(box, cut);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (outline > 0)
                    {
                        alpha *= Mathf.Clamp01(d + outline + 0.5f);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            int border = chamfer + 3;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite Shape(int size, System.Func<float, float, float> alpha)
        {
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(x + 0.5f, y + 0.5f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static float Dist(float x, float y, float cx, float cy)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }

        private static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            sprite = make();
            Cache[key] = sprite;
            return sprite;
        }
    }
}
