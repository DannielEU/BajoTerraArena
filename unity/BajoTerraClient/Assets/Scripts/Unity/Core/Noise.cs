using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Small deterministic noise helpers for the procedural textures. Everything tiles seamlessly, so a texture made
    /// from it can be repeated over any surface without visible seams.
    /// </summary>
    public static class Noise
    {
        /// <summary>A stable pseudo-random value in [0, 1) for an integer lattice point.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// Smooth value noise in [0, 1] that repeats every <paramref name="periodX"/> by <paramref name="periodY"/>
        /// lattice cells.
        /// </summary>
        public static float Value(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            int ax = Wrap(x0, periodX);
            int bx = Wrap(x0 + 1, periodX);
            int ay = Wrap(y0, periodY);
            int by = Wrap(y0 + 1, periodY);
            float n00 = Hash(ax, ay, seed);
            float n10 = Hash(bx, ay, seed);
            float n01 = Hash(ax, by, seed);
            float n11 = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        /// <summary>
        /// Fractal noise in [0, 1]: <paramref name="octaves"/> layers of <see cref="Value"/>, each twice as fine and
        /// half as strong. <paramref name="u"/> and <paramref name="v"/> run over [0, 1) once around the tile;
        /// <paramref name="stretchX"/> and <paramref name="stretchY"/> multiply the base frequency per axis.
        /// </summary>
        public static float Fbm(float u, float v, int baseCells, int octaves, int seed, int stretchX = 1,
            int stretchY = 1)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            int cellsX = baseCells * stretchX;
            int cellsY = baseCells * stretchY;
            for (int octave = 0; octave < octaves; octave++)
            {
                float nx = u * cellsX;
                float ny = v * cellsY;
                sum += Value(nx, ny, Mathf.Max(cellsX, 1), Mathf.Max(cellsY, 1), seed + octave * 17) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                cellsX *= 2;
                cellsY *= 2;
            }
            return sum / total;
        }

        /// <summary>
        /// Worley (cellular) noise on a tiling grid: distances to the nearest and second-nearest feature point, in
        /// cell units. The difference of the two is near zero along cell borders, which draws cracked stone.
        /// </summary>
        public static void Worley(float u, float v, int cells, int seed, out float nearest, out float secondNearest)
        {
            float x = u * cells;
            float y = v * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            nearest = 9f;
            secondNearest = 9f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox;
                    int gy = cy + oy;
                    int wx = Wrap(gx, cells);
                    int wy = Wrap(gy, cells);
                    float px = gx + Hash(wx, wy, seed);
                    float py = gy + Hash(wx, wy, seed + 101);
                    float dx = px - x;
                    float dy = py - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < nearest)
                    {
                        secondNearest = nearest;
                        nearest = d;
                    }
                    else if (d < secondNearest)
                    {
                        secondNearest = d;
                    }
                }
            }
        }

        /// <summary>The identifier (0-1) of the Worley cell that owns a point, for a per-cell tint.</summary>
        public static float WorleyCellId(float u, float v, int cells, int seed)
        {
            float x = u * cells;
            float y = v * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float best = 9f;
            float id = 0f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox;
                    int gy = cy + oy;
                    int wx = Wrap(gx, cells);
                    int wy = Wrap(gy, cells);
                    float px = gx + Hash(wx, wy, seed);
                    float py = gy + Hash(wx, wy, seed + 101);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < best)
                    {
                        best = d;
                        id = Hash(wx, wy, seed + 202);
                    }
                }
            }
            return id;
        }

        private static int Wrap(int value, int period)
        {
            int m = value % period;
            return m < 0 ? m + period : m;
        }
    }
}
