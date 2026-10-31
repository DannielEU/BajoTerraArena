using System;

namespace Bajoterra.Client.Effects
{
    /// <summary>
    /// Ripples on a small round pool of water: a square grid of surface heights that follows the wave equation, so a
    /// splash spreads out as rings, bounces off the edge and dies away. Only the cells inside the circle are water;
    /// the edge holds still. Purely visual (the game simulates nothing in the water) and engine-free, stepping at a
    /// fixed rate whatever the frame rate, without allocating after construction.
    /// </summary>
    public sealed class RippleField
    {
        /// <summary>Seconds per simulation step.</summary>
        public const float StepSeconds = 1f / 60f;

        /// <summary>Speed of the ripples, metres per second.</summary>
        public const float WaveSpeed = 1.1f;

        /// <summary>Share of the wave kept each step (the rest is lost to the water's friction).</summary>
        public const float Damping = 0.988f;

        private const int MaxStepsPerUpdate = 6;

        private readonly float[] _height;
        private readonly float[] _previous;
        private readonly float[] _next;
        private readonly bool[] _water;
        private readonly float _courant;
        private float _accumulator;

        /// <param name="radius">radius of the pool in metres</param>
        /// <param name="size">cells along each side of the grid (the grid spans the pool's diameter)</param>
        public RippleField(float radius, int size)
        {
            if (radius <= 0f || size < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "A pool needs a radius and at least 4 cells");
            }
            Radius = radius;
            Size = size;
            CellSize = 2f * radius / (size - 1);
            _height = new float[size * size];
            _previous = new float[size * size];
            _next = new float[size * size];
            _water = new bool[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = LocalX(x);
                    float pz = LocalX(z);
                    _water[z * size + x] = px * px + pz * pz <= radius * radius;
                }
            }
            float c = WaveSpeed * StepSeconds / CellSize;
            _courant = Math.Min(0.45f, c * c);
        }

        public float Radius { get; }

        public int Size { get; }

        /// <summary>Distance between two neighbouring cells, in metres.</summary>
        public float CellSize { get; }

        /// <summary>Position (metres from the centre) of column or row <paramref name="index"/>.</summary>
        public float LocalX(int index)
        {
            return -Radius + index * CellSize;
        }

        /// <summary>Height of the surface at a cell, in metres above the still water.</summary>
        public float Height(int x, int z)
        {
            return _height[z * Size + x];
        }

        /// <summary>True when the cell is water (inside the circle).</summary>
        public bool IsWater(int x, int z)
        {
            return _water[z * Size + x];
        }

        /// <summary>How much the water is moving overall (sum of squared heights); for tests.</summary>
        public float Energy
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _height.Length; i++)
                {
                    sum += _height[i] * _height[i];
                }
                return sum;
            }
        }

        /// <summary>
        /// Pushes the surface down (positive <paramref name="depth"/>) in a soft round dent of the given radius
        /// around (x, z) metres from the centre: a foot, a slug or a drop hitting the water.
        /// </summary>
        public void Disturb(float x, float z, float radius, float depth)
        {
            float r = Math.Max(radius, CellSize);
            int x0 = Math.Max(1, (int)Math.Floor((x - r + Radius) / CellSize));
            int x1 = Math.Min(Size - 2, (int)Math.Ceiling((x + r + Radius) / CellSize));
            int z0 = Math.Max(1, (int)Math.Floor((z - r + Radius) / CellSize));
            int z1 = Math.Min(Size - 2, (int)Math.Ceiling((z + r + Radius) / CellSize));
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    int i = gz * Size + gx;
                    if (!_water[i])
                    {
                        continue;
                    }
                    float dx = LocalX(gx) - x;
                    float dz = LocalX(gz) - z;
                    float d2 = (dx * dx + dz * dz) / (r * r);
                    if (d2 < 1f)
                    {
                        float k = (1f - d2) * (1f - d2);
                        _height[i] -= depth * k;
                    }
                }
            }
        }

        /// <summary>Advances the ripples by <paramref name="seconds"/> of real time (in fixed steps).</summary>
        public void Update(float seconds)
        {
            _accumulator = Math.Min(_accumulator + Math.Max(0f, seconds), MaxStepsPerUpdate * StepSeconds);
            while (_accumulator >= StepSeconds)
            {
                _accumulator -= StepSeconds;
                Step();
            }
        }

        private void Step()
        {
            int n = Size;
            for (int z = 1; z < n - 1; z++)
            {
                for (int x = 1; x < n - 1; x++)
                {
                    int i = z * n + x;
                    if (!_water[i])
                    {
                        _next[i] = 0f;
                        continue;
                    }
                    float h = _height[i];
                    float laplacian = _height[i - 1] + _height[i + 1] + _height[i - n] + _height[i + n] - 4f * h;
                    _next[i] = (2f * h - _previous[i] + _courant * laplacian) * Damping;
                }
            }
            Array.Copy(_height, _previous, _height.Length);
            Array.Copy(_next, _height, _next.Length);
        }
    }
}
