using System;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Every texture of the game, drawn in code (no image assets to import or to lose). The surface textures tile
    /// seamlessly; <see cref="MeshKit"/> gives meshes UVs in metres divided by the tile size, so a texture keeps
    /// the same scale on a small crate and on a long wall. Textures are made once and shared.
    /// </summary>
    public static class Textures
    {
        /// <summary>Edge of the floor tile in metres.</summary>
        public const float FloorTile = 6f;

        public const float RockTile = 5f;
        public const float BrickTile = 2f;
        public const float PlankTile = 1.5f;
        public const float CrateTile = 1f;

        private static Texture2D _floor;
        private static Texture2D _rock;
        private static Texture2D _bricks;
        private static Texture2D _planks;
        private static Texture2D _crate;
        private static Texture2D _softDot;
        private static Texture2D _ring;
        private static Texture2D _vignette;
        private static Texture2D _flare;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _floor = _rock = _bricks = _planks = _crate = _softDot = _ring = _vignette = _flare = null;
        }

        public static Texture2D Floor
        {
            get { return _floor != null ? _floor : _floor = Bake("Floor", 256, true, FloorPixel); }
        }

        public static Texture2D Rock
        {
            get { return _rock != null ? _rock : _rock = Bake("Rock", 256, true, RockPixel); }
        }

        public static Texture2D Bricks
        {
            get { return _bricks != null ? _bricks : _bricks = Bake("Bricks", 256, true, BrickPixel); }
        }

        public static Texture2D Planks
        {
            get { return _planks != null ? _planks : _planks = Bake("Planks", 256, true, PlankPixel); }
        }

        public static Texture2D Crate
        {
            get { return _crate != null ? _crate : _crate = Bake("Crate", 128, true, CratePixel); }
        }

        /// <summary>A round soft blob (white, alpha falling off to the rim) for glows and particles.</summary>
        public static Texture2D SoftDot
        {
            get { return _softDot != null ? _softDot : _softDot = Bake("SoftDot", 64, false, SoftDotPixel, TextureWrapMode.Clamp); }
        }

        /// <summary>A thin glowing ring, for shock waves and pads.</summary>
        public static Texture2D Ring
        {
            get { return _ring != null ? _ring : _ring = Bake("Ring", 128, false, RingPixel, TextureWrapMode.Clamp); }
        }

        /// <summary>Transparent in the middle, opaque at the corners: tinted red it is the damage vignette.</summary>
        public static Texture2D Vignette
        {
            get { return _vignette != null ? _vignette : _vignette = Bake("Vignette", 128, false, VignettePixel, TextureWrapMode.Clamp); }
        }

        /// <summary>A star-shaped lens flare: a bright core with a horizontal and a vertical streak.</summary>
        public static Texture2D Flare
        {
            get { return _flare != null ? _flare : _flare = Bake("Flare", 128, false, FlarePixel, TextureWrapMode.Clamp); }
        }

        // ------------------------------------------------------------------------------------------ baking

        private static Texture2D Bake(string name, int size, bool mipmaps, Func<float, float, Color> pixel,
            TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipmaps)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                anisoLevel = mipmaps ? 4 : 0
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(mipmaps, false);
            return texture;
        }

        // ------------------------------------------------------------------------------------------ surfaces

        /// <summary>Cracked blue-green flagstones with patches of green moss.</summary>
        private static Color FloorPixel(float u, float v)
        {
            Noise.Worley(u, v, 7, 3, out float nearest, out float second);
            float edge = second - nearest;
            float crack = Smooth(0.0f, 0.16f, edge);
            float tint = 0.82f + 0.36f * Noise.WorleyCellId(u, v, 7, 3);
            float grain = Noise.Fbm(u, v, 6, 4, 11);
            var dark = new Color(0.12f, 0.17f, 0.2f);
            var light = new Color(0.3f, 0.38f, 0.41f);
            Color color = Color.Lerp(dark, light, grain) * tint;
            float moss = Mathf.Clamp01((Noise.Fbm(u, v, 4, 3, 29) - 0.5f) * 3.2f);
            color = Color.Lerp(color, new Color(0.13f, 0.4f, 0.22f), moss * 0.8f);
            color *= 0.38f + 0.62f * crack;
            // A faint lighter lip just inside each crack reads as a chipped edge.
            float lip = Smooth(0.16f, 0.26f, edge) * (1f - Smooth(0.26f, 0.4f, edge));
            color += new Color(0.04f, 0.045f, 0.06f) * lip;
            color.a = 1f;
            return color;
        }

        /// <summary>Layered blue-green rock with horizontal strata (for tall walls and pillars).</summary>
        private static Color RockPixel(float u, float v)
        {
            float strata = Noise.Fbm(u, v, 2, 3, 41, 1, 7);
            float body = Noise.Fbm(u, v, 5, 5, 53);
            var a = new Color(0.1f, 0.18f, 0.23f);
            var b = new Color(0.24f, 0.4f, 0.43f);
            Color color = Color.Lerp(a, b, strata * 0.65f + body * 0.45f);
            Noise.Worley(u, v, 4, 61, out float nearest, out float second);
            float seam = Smooth(0.0f, 0.1f, second - nearest);
            color *= 0.55f + 0.45f * seam;
            // Glittering mineral specks.
            float speck = Noise.Hash(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), 7);
            if (speck > 0.992f)
            {
                color += new Color(0.2f, 0.42f, 0.45f);
            }
            color.a = 1f;
            return color;
        }

        /// <summary>Sandstone blocks laid in a running bond, with dark mortar.</summary>
        private static Color BrickPixel(float u, float v)
        {
            const int rows = 4;
            const int columns = 2;
            int row = Mathf.FloorToInt(v * rows);
            float shifted = u * columns + (row % 2 == 0 ? 0f : 0.5f);
            int column = Mathf.FloorToInt(shifted);
            float bu = shifted - column;
            float bv = v * rows - row;
            float id = Noise.Hash(column, row, 5);
            var baseColor = new Color(0.66f, 0.6f, 0.5f) * (0.8f + 0.35f * id);
            float grain = Noise.Fbm(u, v, 12, 4, 71);
            baseColor *= 0.82f + 0.3f * grain;
            float mortar = Mathf.Min(Mathf.Min(bu, 1f - bu) * 2f * 1.4f, Mathf.Min(bv, 1f - bv) * 2f * 2.2f);
            float edge = Smooth(0.0f, 0.12f, mortar);
            Color color = Color.Lerp(new Color(0.16f, 0.14f, 0.13f), baseColor, edge);
            // Light from the top left: bevel the block faces.
            float bevel = (bv > 0.88f ? 0.12f : 0f) - (bv < 0.12f ? 0.1f : 0f);
            color += new Color(bevel, bevel, bevel) * edge;
            color.a = 1f;
            return color;
        }

        /// <summary>Wooden deck boards running along u.</summary>
        private static Color PlankPixel(float u, float v)
        {
            const int boards = 6;
            int board = Mathf.FloorToInt(v * boards);
            float bv = v * boards - board;
            float id = Noise.Hash(board, 3, 9);
            // Each board has one butt joint somewhere along its length.
            float jointAt = Noise.Hash(board, 4, 9);
            float du = Mathf.Abs(u - jointAt);
            du = Mathf.Min(du, 1f - du);
            float grain = Noise.Fbm(u, v, 2, 4, 83, 1, 24);
            var wood = Color.Lerp(new Color(0.34f, 0.21f, 0.11f), new Color(0.56f, 0.38f, 0.20f), grain);
            wood *= 0.78f + 0.4f * id;
            float seam = Smooth(0.0f, 0.07f, Mathf.Min(bv, 1f - bv));
            float joint = Smooth(0.0f, 0.012f, du);
            Color color = wood * (0.35f + 0.65f * seam) * (0.5f + 0.5f * joint);
            color.a = 1f;
            return color;
        }

        /// <summary>A reinforced crate face: dark frame, vertical slats and a diagonal brace.</summary>
        private static Color CratePixel(float u, float v)
        {
            float frame = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float slat = Mathf.Abs(Mathf.Repeat(u * 5f, 1f) - 0.5f);
            float grain = Noise.Fbm(u, v, 3, 4, 91, 1, 12);
            var wood = Color.Lerp(new Color(0.36f, 0.23f, 0.12f), new Color(0.58f, 0.40f, 0.22f), grain);
            Color color = wood * (0.78f + 0.5f * Smooth(0.0f, 0.1f, 0.5f - slat));
            bool onFrame = frame < 0.11f;
            bool onBrace = Mathf.Abs(u - v) < 0.07f || Mathf.Abs(u + v - 1f) < 0.035f;
            if (onFrame || onBrace)
            {
                color = Color.Lerp(wood * 0.62f, wood * 0.9f, grain);
                float lip = Smooth(0.0f, 0.02f, Mathf.Abs(frame - 0.11f));
                color *= 0.7f + 0.3f * lip;
            }
            // Corner bolts.
            if (Vector2.Distance(new Vector2(u, v), new Vector2(0.055f, 0.055f)) < 0.025f
                || Vector2.Distance(new Vector2(u, v), new Vector2(0.945f, 0.055f)) < 0.025f
                || Vector2.Distance(new Vector2(u, v), new Vector2(0.055f, 0.945f)) < 0.025f
                || Vector2.Distance(new Vector2(u, v), new Vector2(0.945f, 0.945f)) < 0.025f)
            {
                color = new Color(0.62f, 0.62f, 0.64f);
            }
            color.a = 1f;
            return color;
        }

        // ------------------------------------------------------------------------------------------ sprites

        private static Color SoftDotPixel(float u, float v)
        {
            float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - r);
            a *= a;
            return new Color(1f, 1f, 1f, a);
        }

        private static Color RingPixel(float u, float v)
        {
            float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float band = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.82f) / 0.14f);
            float inner = Mathf.Clamp01(1f - r) * 0.12f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(band * band + inner));
        }

        private static Color VignettePixel(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Smooth(0.55f, 1.3f, r);
            return new Color(1f, 1f, 1f, a * a);
        }

        private static Color FlarePixel(float u, float v)
        {
            float x = Mathf.Abs(u - 0.5f) * 2f;
            float y = Mathf.Abs(v - 0.5f) * 2f;
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 1.6f);
            float horizontal = Mathf.Clamp01(1f - x) * Mathf.Clamp01(1f - y * 14f);
            float vertical = Mathf.Clamp01(1f - y) * Mathf.Clamp01(1f - x * 14f);
            float a = Mathf.Clamp01(core * core + horizontal * 0.55f + vertical * 0.55f);
            return new Color(1f, 1f, 1f, a);
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
