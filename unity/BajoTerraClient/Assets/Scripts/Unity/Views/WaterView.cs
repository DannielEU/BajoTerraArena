using Bajoterra.Client.Content;
using Bajoterra.Client.Effects;
using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The arena's little pools of water (<see cref="ArenaMap.Water"/>): purely decorative, but alive. Each pool is a
    /// rim of wet stones and moss around a dark basin, under a surface that ripples for real
    /// (<see cref="RippleField"/>, drawn by the <c>Bajoterra/Water</c> shader so every ring catches the lights).
    /// Whoever walks through it leaves a wake and splashes at each step, landing in it from a jump throws up a
    /// splash, a slug flying low over it sprays it, a slug that falls into it plops (a fire slug hisses into steam),
    /// a slug stuck in it bubbles, and blasts and flashes nearby shake it. Drops from the ceiling ring it now and
    /// then when nothing else does. Nothing here allocates per frame.
    /// </summary>
    public sealed class WaterView
    {
        /// <summary>Height of the still surface above the floor.</summary>
        public const float SurfaceHeight = 0.06f;

        /// <summary>How far a ripple may dip below the still surface: never down to the basin under it.</summary>
        private const float MaxDip = 0.045f;

        private const int Grid = 56;
        private const int RingCount = 10;
        private const int MaxTracked = 32;
        /// <summary>A foot this close to the floor is in the water (anything higher is a jump over it).</summary>
        private const float Wading = 0.25f;
        /// <summary>Distance walked in the water between two splashing steps.</summary>
        private const float Stride = 0.75f;

        private static readonly Color Spray = new Color(0.75f, 0.95f, 1f, 0.75f);
        private static readonly Color Steam = new Color(0.85f, 0.9f, 0.92f, 0.35f);

        private sealed class Pool
        {
            public WaterSpot Spot;
            public Vector3 Center;
            public RippleField Field;
            public Mesh Mesh;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public float NextDrip;
        }

        private sealed class Ring
        {
            public Transform Root;
            public Renderer Renderer;
            public float Age = 99f;
            public float Life;
            public float Size;
            public float Strength;
        }

        private readonly Transform _root;
        private readonly ContentCatalog _content;
        private readonly AudioPlayer _audio;
        private readonly Pool[] _pools;
        private readonly Ring[] _rings = new Ring[RingCount];
        private readonly ParticleSystem _drops;
        private readonly ParticleSystem _mist;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly System.Random _random;
        private readonly Vector3[] _lastFeet = new Vector3[MaxTracked];
        private readonly bool[] _known = new bool[MaxTracked];
        private readonly float[] _walked = new float[MaxTracked];
        private readonly uint[] _sprayed = new uint[MaxTracked];
        private readonly float[] _sprayTime = new float[MaxTracked];
        private int _nextRing;
        private int _nextSpray;
        private float _time;

        public WaterView(Transform parent, ArenaMap map, ContentCatalog content, AudioPlayer audio)
        {
            _root = new GameObject("Water").transform;
            _root.SetParent(parent, false);
            _content = content;
            _audio = audio;
            _random = new System.Random(map.Name == null ? 7 : map.Name.Length * 31 + map.Water.Length);
            _pools = new Pool[map.Water.Length];
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i] = BuildPool(map.Water[i], map.GroundY, i);
            }
            for (int i = 0; i < RingCount; i++)
            {
                GameObject quad = Gfx.Make("Ripple ring " + i, Billboard.QuadMesh(), _root,
                    MaterialFactory.GlowInstance(Textures.Ring, 0.9f));
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Gfx.NoShadows(quad);
                quad.SetActive(false);
                _rings[i] = new Ring { Root = quad.transform, Renderer = quad.GetComponent<Renderer>() };
            }
            _drops = Particles("Splash drops", 0.9f, 400, 0.05f);
            _mist = Particles("Splash mist", -0.05f, 120, 0.2f);
        }

        /// <summary>How many pools the arena has.</summary>
        public int PoolCount
        {
            get { return _pools.Length; }
        }

        /// <summary>How much the water of a pool is moving (tests).</summary>
        public float Motion(int pool)
        {
            return _pools[pool].Field.Energy;
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
            for (int i = 0; i < _pools.Length; i++)
            {
                Object.Destroy(_pools[i].Mesh);
            }
        }

        // ------------------------------------------------------------------------------------------ per frame

        /// <summary>Lets the combatants and slugs of this frame stir the water, then moves the ripples on.</summary>
        public void Apply(WorldFrame frame, float deltaTime)
        {
            if (_pools.Length == 0)
            {
                return;
            }
            _time += deltaTime;
            for (int i = 0; i < frame.CombatantCount; i++)
            {
                Wade(frame.Combatants[i], deltaTime);
            }
            for (int i = 0; i < frame.ProjectileCount; i++)
            {
                Skim(frame.Projectiles[i]);
            }
            for (int p = 0; p < _pools.Length; p++)
            {
                Pool pool = _pools[p];
                if (_time >= pool.NextDrip)
                {
                    // A drop from the ceiling: the pool is never quite still.
                    pool.NextDrip = _time + 1.2f + (float)_random.NextDouble() * 2.4f;
                    float a = (float)_random.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Sqrt((float)_random.NextDouble()) * pool.Spot.Radius * 0.8f;
                    Vector3 at = pool.Center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    pool.Field.Disturb(at.x - pool.Center.x, at.z - pool.Center.z, 0.07f, 0.012f);
                    AddRing(at, 0.5f, 0.9f, 0.35f);
                    if (_random.NextDouble() < 0.35)
                    {
                        _audio.PlayAt(_audio.Plop, at, 0.12f, 1.4f + (float)_random.NextDouble() * 0.4f);
                    }
                }
                pool.Field.Update(deltaTime);
                Redraw(pool);
            }
            UpdateRings(deltaTime);
        }

        /// <summary>Splashes for the game's events that happen in or next to the water.</summary>
        public void Handle(GameEvent e)
        {
            var position = new Vector3(e.X, e.Y, e.Z);
            switch (e.Kind)
            {
                case EventKind.ProjectileWorldHit:
                case EventKind.ProjectileExpired:
                    {
                        Pool pool = PoolAt(position, 0.1f);
                        if (pool != null && position.y < pool.Center.y + 0.8f)
                        {
                            bool fire = IsFire(e.A);
                            Splash(pool, position, 0.9f, fire);
                            _audio.PlayAt(fire ? _audio.Splish : _audio.Plop, position, 0.7f, Random(0.85f, 1.1f));
                        }
                        break;
                    }
                case EventKind.Explosion:
                case EventKind.Flash:
                    {
                        float reach = Mathf.Max(1f, e.Value);
                        for (int p = 0; p < _pools.Length; p++)
                        {
                            Pool pool = _pools[p];
                            float distance = Vector3.Distance(position, pool.Center) - pool.Spot.Radius;
                            if (distance < reach)
                            {
                                float strength = Mathf.Clamp01(1f - distance / reach);
                                Vector3 near = ClosestOnPool(pool, position);
                                pool.Field.Disturb(near.x - pool.Center.x, near.z - pool.Center.z, 0.6f,
                                    0.05f * strength);
                                AddRing(near, pool.Spot.Radius * 1.4f, 0.7f, 0.6f * strength);
                                if (distance < 0.5f && e.Kind == EventKind.Explosion)
                                {
                                    Splash(pool, near, 1.6f, false);
                                    _audio.PlayAt(_audio.Splash, near, 1f, 0.8f);
                                }
                            }
                        }
                        break;
                    }
            }
        }

        // ------------------------------------------------------------------------------------------ interactions

        /// <summary>A combatant in the water leaves a wake, splashes at every step and makes a splash landing in it.</summary>
        private void Wade(CombatantFrame c, float deltaTime)
        {
            int id = c.Id;
            if (id < 0 || id >= MaxTracked)
            {
                return;
            }
            Vector3 feet = new Vector3(c.Position.X, c.Position.Y, c.Position.Z);
            Vector3 last = _known[id] ? _lastFeet[id] : feet;
            _lastFeet[id] = feet;
            _known[id] = true;
            if (!c.Alive)
            {
                return;
            }
            Pool pool = PoolAt(feet, 0.25f);
            if (pool == null || feet.y > pool.Center.y + Wading)
            {
                _walked[id] = Stride * 0.6f;
                return;
            }
            Vector3 moved = feet - last;
            float flat = new Vector2(moved.x, moved.z).magnitude;
            float speed = deltaTime > 1e-4f ? flat / deltaTime : 0f;
            float body = Mathf.Max(0.25f, c.HitboxHeight * 0.18f);
            float lx = feet.x - pool.Center.x;
            float lz = feet.z - pool.Center.z;
            bool landed = last.y > pool.Center.y + Wading + 0.05f;
            bool entered = PoolAt(last, 0.25f) != pool;
            if (landed)
            {
                Splash(pool, feet, 1.3f, false);
                _audio.PlayAt(_audio.Splash, feet, 0.9f, Random(0.9f, 1.05f));
                return;
            }
            // The wake: the legs keep pushing the water aside while moving, a little even standing.
            pool.Field.Disturb(lx, lz, body, (0.0005f + 0.00045f * Mathf.Min(speed, 8f)) * deltaTime * 60f);
            _walked[id] += flat;
            if (entered || _walked[id] >= Stride)
            {
                _walked[id] = 0f;
                Vector3 step = feet + new Vector3(moved.x, 0f, moved.z) * 2f;
                pool.Field.Disturb(lx, lz, body * 0.8f, 0.02f);
                AddRing(new Vector3(step.x, pool.Center.y, step.z), 0.9f, 0.8f, 0.5f);
                Emit(_drops, new Vector3(feet.x, pool.Center.y + 0.05f, feet.z), Spray, 10, 1.2f, 2.6f, 0.04f);
                _audio.PlayAt(_audio.Splish, feet, 0.45f, Random(0.85f, 1.2f));
            }
        }

        /// <summary>A slug flying low over a pool sprays a line of drops below it; one stuck in the water bubbles.</summary>
        private void Skim(ProjectileFrame p)
        {
            Vector3 at = new Vector3(p.Position.X, p.Position.Y, p.Position.Z);
            Pool pool = PoolAt(at, 0f);
            if (pool == null)
            {
                return;
            }
            float above = at.y - pool.Center.y;
            float lx = at.x - pool.Center.x;
            float lz = at.z - pool.Center.z;
            if (p.Stuck)
            {
                if (above < 0.6f && Mathf.Repeat(_time * 3f + p.Id * 0.37f, 1f) < 0.12f)
                {
                    pool.Field.Disturb(lx, lz, 0.12f, 0.004f);
                    Emit(_mist, new Vector3(at.x, pool.Center.y + 0.04f, at.z), Spray, 1, 0.1f, 0.3f, 0.08f);
                }
                return;
            }
            if (above > 1.4f)
            {
                return;
            }
            // Stir the surface under it, harder the lower it flies; throw up a few drops once per pass.
            float strength = 1f - above / 1.4f;
            pool.Field.Disturb(lx, lz, 0.25f, 0.006f * strength);
            if (!SprayedRecently(p.Id))
            {
                Emit(_drops, new Vector3(at.x, pool.Center.y + 0.05f, at.z), Spray, (int)(6 + 10 * strength), 1f, 3f,
                    0.035f);
                AddRing(new Vector3(at.x, pool.Center.y, at.z), 1.1f, 0.7f, 0.45f * strength);
            }
        }

        /// <summary>True when this projectile already sprayed the water in the last moment (one burst per pass).</summary>
        private bool SprayedRecently(uint id)
        {
            for (int i = 0; i < MaxTracked; i++)
            {
                if (_sprayed[i] == id && _time - _sprayTime[i] < 0.6f)
                {
                    return true;
                }
            }
            _sprayed[_nextSpray] = id;
            _sprayTime[_nextSpray] = _time;
            _nextSpray = (_nextSpray + 1) % MaxTracked;
            return false;
        }

        /// <summary>A splash: a dent in the surface, a crown of drops, a puff of spray (steam for fire) and a ring.</summary>
        private void Splash(Pool pool, Vector3 at, float size, bool steam)
        {
            Vector3 surface = new Vector3(at.x, pool.Center.y + 0.04f, at.z);
            pool.Field.Disturb(at.x - pool.Center.x, at.z - pool.Center.z, 0.25f * size, 0.05f * size);
            Emit(_drops, surface, Spray, (int)(24 * size), 1.6f * size, 3.6f * size, 0.05f);
            Emit(_mist, surface, steam ? Steam : Spray, steam ? 14 : 6, 0.3f, 1.1f, 0.35f * size);
            AddRing(surface, 1.6f * size, 0.9f, 0.85f);
        }

        // ------------------------------------------------------------------------------------------ helpers

        private Pool PoolAt(Vector3 position, float margin)
        {
            for (int i = 0; i < _pools.Length; i++)
            {
                if (_pools[i].Spot.Contains(position.x, position.z, margin))
                {
                    return _pools[i];
                }
            }
            return null;
        }

        private static Vector3 ClosestOnPool(Pool pool, Vector3 position)
        {
            Vector3 offset = new Vector3(position.x - pool.Center.x, 0f, position.z - pool.Center.z);
            float limit = pool.Spot.Radius * 0.85f;
            if (offset.magnitude > limit)
            {
                offset = offset.normalized * limit;
            }
            return pool.Center + offset;
        }

        private bool IsFire(int slugIndex)
        {
            return slugIndex >= 0 && slugIndex < _content.SlugCount
                   && _content.ElementName((int)_content.Slug(slugIndex).Element) == "FIRE";
        }

        private float Random(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private static void Emit(ParticleSystem system, Vector3 position, Color color, int count, float minSpeed,
            float maxSpeed, float size)
        {
            ParticleSystem.MainModule main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.5f);
            var emit = new ParticleSystem.EmitParams { position = position, startColor = color, applyShapeToPosition = true };
            system.Emit(emit, count);
        }

        private void AddRing(Vector3 at, float size, float life, float strength)
        {
            Ring ring = _rings[_nextRing];
            _nextRing = (_nextRing + 1) % RingCount;
            ring.Age = 0f;
            ring.Life = life;
            ring.Size = size;
            ring.Strength = strength;
            ring.Root.position = new Vector3(at.x, at.y + SurfaceHeight + 0.01f, at.z);
            ring.Root.gameObject.SetActive(true);
        }

        private void UpdateRings(float deltaTime)
        {
            for (int i = 0; i < RingCount; i++)
            {
                Ring ring = _rings[i];
                if (ring.Age > ring.Life)
                {
                    continue;
                }
                ring.Age += deltaTime;
                float t = Mathf.Clamp01(ring.Age / ring.Life);
                if (t >= 1f)
                {
                    ring.Root.gameObject.SetActive(false);
                    ring.Age = 99f;
                    continue;
                }
                float ease = 1f - (1f - t) * (1f - t);
                ring.Root.localScale = Vector3.one * (0.15f + ring.Size * ease);
                _block.SetColor("_Color", new Color(0.7f, 1f, 0.95f, (1f - t) * ring.Strength));
                ring.Renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>Copies the ripple heights into the surface mesh, with normals from their slopes.</summary>
        private static void Redraw(Pool pool)
        {
            RippleField field = pool.Field;
            int n = field.Size;
            float cell = field.CellSize;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    pool.Vertices[i].y = Mathf.Clamp(field.Height(x, z), -MaxDip, 0.08f);
                    float left = field.Height(Mathf.Max(0, x - 1), z);
                    float right = field.Height(Mathf.Min(n - 1, x + 1), z);
                    float down = field.Height(x, Mathf.Max(0, z - 1));
                    float up = field.Height(x, Mathf.Min(n - 1, z + 1));
                    // Exaggerated a little so even small ripples catch the light.
                    pool.Normals[i] = new Vector3((left - right) * 2f, 2f * cell, (down - up) * 2f).normalized;
                }
            }
            pool.Mesh.vertices = pool.Vertices;
            pool.Mesh.normals = pool.Normals;
        }

        // ------------------------------------------------------------------------------------------ building

        private Pool BuildPool(WaterSpot spot, float groundY, int index)
        {
            var pool = new Pool
            {
                Spot = spot,
                Center = new Vector3(spot.X, groundY + SurfaceHeight, spot.Z),
                Field = new RippleField(spot.Radius, Grid),
                NextDrip = 0.5f + index
            };
            var basin = new GameObject("Pool " + index).transform;
            basin.SetParent(_root, false);
            basin.position = new Vector3(spot.X, groundY, spot.Z);

            // A dark, wet basin under the water so it looks deeper than it is.
            GameObject bed = Gfx.Make("Basin", Disc(spot.Radius + 0.12f, 40), basin,
                MaterialFactory.Surface(Textures.Floor, new Color(0.05f, 0.16f, 0.17f), 0.85f, 0f));
            bed.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            Gfx.NoShadows(bed);
            GameObject damp = Gfx.Make("Wet edge", Disc(spot.Radius + 0.55f, 40), basin,
                MaterialFactory.Surface(Textures.Floor, new Color(0.5f, 0.6f, 0.58f), 0.7f, 0f));
            damp.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            Gfx.NoShadows(damp);

            // The surface: a square grid the ripples bend; the shader keeps only the circle.
            pool.Mesh = SurfaceGrid(pool.Field, out pool.Vertices, out pool.Normals);
            Shader shader = Resources.Load<Shader>("Shaders/BajoterraWater");
            Material water = shader != null ? new Material(shader) : MaterialFactory.LitInstance(new Color(0.1f, 0.4f, 0.45f));
            water.SetFloat("_Radius", spot.Radius);
            GameObject surface = Gfx.Make("Surface", pool.Mesh, basin, water);
            surface.transform.localPosition = new Vector3(0f, SurfaceHeight, 0f);
            Gfx.NoShadows(surface);

            // A rim of wet stones and tufts of moss, leaving gaps to walk through.
            Material stone = MaterialFactory.Surface(Textures.Rock, new Color(0.34f, 0.4f, 0.4f), 0.4f, 0f);
            Material moss = MaterialFactory.Surface(null, new Color(0.16f, 0.42f, 0.24f), 0.2f, 0f);
            int stones = Mathf.RoundToInt(spot.Radius * 9f);
            for (int i = 0; i < stones; i++)
            {
                float a = (i + (float)_random.NextDouble() * 0.6f) / stones * Mathf.PI * 2f;
                if (Mathf.Repeat(a + index, Mathf.PI * 2f / 3f) < 0.32f)
                {
                    continue;
                }
                float r = spot.Radius + 0.05f + (float)_random.NextDouble() * 0.12f;
                float size = 0.1f + (float)_random.NextDouble() * 0.12f;
                GameObject rock = Gfx.Make("Rim stone", MeshKit.Rock(size, index * 97 + i, Textures.RockTile), basin,
                    i % 4 == 0 ? moss : stone);
                rock.transform.localPosition = new Vector3(Mathf.Cos(a) * r, size * 0.15f, Mathf.Sin(a) * r);
                rock.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg * 1.7f, 0f);
                rock.transform.localScale = new Vector3(1.3f, 0.55f, 1f);
                Gfx.NoShadows(rock);
            }
            return pool;
        }

        private static Mesh SurfaceGrid(RippleField field, out Vector3[] vertices, out Vector3[] normals)
        {
            int n = field.Size;
            vertices = new Vector3[n * n];
            normals = new Vector3[n * n];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    vertices[z * n + x] = new Vector3(field.LocalX(x), 0f, field.LocalX(z));
                    normals[z * n + x] = Vector3.up;
                }
            }
            var triangles = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    triangles[t++] = i;
                    triangles[t++] = i + n;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + n;
                    triangles[t++] = i + n + 1;
                }
            }
            var mesh = new Mesh { name = "Water surface" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(field.Radius * 2f, 0.5f, field.Radius * 2f));
            return mesh;
        }

        /// <summary>A flat disc facing up, centred on the origin.</summary>
        private static Mesh Disc(float radius, int sides)
        {
            var vertices = new Vector3[sides + 1];
            var uv = new Vector2[sides + 1];
            var normals = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * radius * 0.5f, 0.5f + Mathf.Sin(a) * radius * 0.5f);
                normals[i + 1] = Vector3.up;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i == sides - 1 ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Disc" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private ParticleSystem Particles(string name, float gravity, int max, float size)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(_root, false);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = gravity > 0f ? new ParticleSystem.MinMaxCurve(0.35f, 0.8f) : new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.startSize = size;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            if (gravity <= 0f)
            {
                ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            }
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.Glow();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (gravity > 0f)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1.5f;
            }
            return system;
        }
    }
}
