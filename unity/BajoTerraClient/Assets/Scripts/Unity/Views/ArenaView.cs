using System.Collections.Generic;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the arena from the geometry the server sent, dressed as a cavern: a flagstone floor, rock walls and
    /// ceiling, crates for low cover, stone pillars, plinths with plank decks and ramps, glowing crystals, spawn pads
    /// and pickups. Nothing here decides gameplay; every solid is a picture of an obstacle the server simulates, so
    /// the two can never disagree about where a wall or a ramp is. The purely decorative things (crystals, rocks,
    /// stalactites, dust) live outside the playable rectangle or high above it and have no colliders.
    /// </summary>
    public sealed class ArenaView
    {
        /// <summary>How high the cavern ceiling is above the floor.</summary>
        public const float CeilingHeight = 14f;

        private const float Margin = 3f;
        private const float SlabSize = 9f;

        private static readonly Color DeckTrim = new Color(0.25f, 0.85f, 1f);

        private readonly Transform _root;
        private readonly ArenaMap _map;
        private readonly int _ownTeam;
        private readonly List<PickupVisual> _pickups = new List<PickupVisual>();
        private readonly List<Light> _crystalLights = new List<Light>();
        private readonly List<float> _crystalPhase = new List<float>();
        private readonly List<float> _crystalBase = new List<float>();
        private readonly System.Random _random;

        private sealed class PickupVisual
        {
            public Transform Root;
            public Transform Spinner;
            public Renderer[] Renderers;
            public Light Light;
            public Transform Marker;
            public Vector3 Rest;
            public float Phase;
        }

        public ArenaView(Transform parent, ArenaMap map, ContentCatalog content, int ownTeam)
        {
            _map = map;
            _ownTeam = ownTeam;
            _random = new System.Random(StableSeed(map.Name));
            _root = new GameObject("Arena").transform;
            _root.SetParent(parent, false);

            BuildFloor();
            BuildShell();
            for (int i = 0; i < map.Obstacles.Length; i++)
            {
                BuildObstacle(map.Obstacles[i], i);
            }
            BuildSpawns();
            BuildCrystals();
            BuildRocks();
            BuildStalactites();
            BuildDust();
            BuildPickups(content);
        }

        /// <summary>Spins and bobs the pickups, hides the ones that were taken and breathes the crystal lights.</summary>
        public void Animate(WorldFrame frame, float time)
        {
            for (int i = 0; i < _pickups.Count; i++)
            {
                PickupVisual pickup = _pickups[i];
                bool available = true;
                for (int k = 0; k < frame.PickupCount; k++)
                {
                    if (frame.Pickups[k].Index == i)
                    {
                        available = frame.Pickups[k].Available;
                        break;
                    }
                }
                pickup.Spinner.gameObject.SetActive(available);
                pickup.Light.enabled = available;
                pickup.Marker.localScale = Vector3.one * (available ? 1.7f : 1.2f);
                if (available)
                {
                    pickup.Spinner.position = pickup.Rest + Vector3.up * (0.95f + Mathf.Sin(time * 2f + pickup.Phase) * 0.12f);
                    pickup.Spinner.rotation = Quaternion.Euler(0f, time * 70f + pickup.Phase * 40f, 0f);
                    pickup.Light.intensity = 1.1f + 0.3f * Mathf.Sin(time * 3f + pickup.Phase);
                }
            }
            for (int i = 0; i < _crystalLights.Count; i++)
            {
                _crystalLights[i].intensity = _crystalBase[i] * (0.88f + 0.12f * Mathf.Sin(time * 0.9f + _crystalPhase[i]));
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        // ------------------------------------------------------------------------------------------ shell

        private void BuildFloor()
        {
            float minX = _map.MinX - Margin;
            float maxX = _map.MaxX + Margin;
            float minZ = _map.MinZ - Margin;
            float maxZ = _map.MaxZ + Margin;
            Material floor = MaterialFactory.Surface(Textures.Floor, Color.white, 0.35f, 0f);
            Slabs("Floor", new Vector3(minX, _map.GroundY - 1f, minZ), new Vector3(maxX, _map.GroundY, maxZ),
                floor, floor, Textures.FloorTile, Textures.FloorTile);
        }

        /// <summary>
        /// Builds a big box as a grid of slabs no longer than <see cref="SlabSize"/>, with UVs that continue across the
        /// joints. Each slab is lit by the crystals near it (a single huge object would only take the lights closest to
        /// its middle).
        /// </summary>
        private void Slabs(string name, Vector3 min, Vector3 max, Material sides, Material top, float tile, float topTile)
        {
            int countX = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / SlabSize));
            int countY = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / SlabSize));
            int countZ = Mathf.Max(1, Mathf.CeilToInt((max.z - min.z) / SlabSize));
            Vector3 step = new Vector3((max.x - min.x) / countX, (max.y - min.y) / countY, (max.z - min.z) / countZ);
            for (int ix = 0; ix < countX; ix++)
            {
                for (int iy = 0; iy < countY; iy++)
                {
                    for (int iz = 0; iz < countZ; iz++)
                    {
                        Vector3 center = min + new Vector3((ix + 0.5f) * step.x, (iy + 0.5f) * step.y, (iz + 0.5f) * step.z);
                        Mesh mesh = MeshKit.WorldBox(step, center, tile, topTile);
                        GameObject go = Gfx.Make(name, mesh, _root, sides, top);
                        go.transform.position = center;
                    }
                }
            }
        }

        /// <summary>Four rock walls just outside the playable rectangle and the ceiling over it.</summary>
        private void BuildShell()
        {
            float thickness = 4f;
            float top = _map.GroundY + CeilingHeight;
            Material rock = MaterialFactory.Surface(Textures.Rock, new Color(1f, 0.97f, 1f), 0.2f, 0f);
            float x0 = _map.MinX - thickness;
            float x1 = _map.MaxX + thickness;
            float z0 = _map.MinZ - thickness;
            float z1 = _map.MaxZ + thickness;
            Slabs("Wall N", new Vector3(x0, _map.GroundY, _map.MaxZ), new Vector3(x1, top, z1), rock, rock, Textures.RockTile, Textures.RockTile);
            Slabs("Wall S", new Vector3(x0, _map.GroundY, z0), new Vector3(x1, top, _map.MinZ), rock, rock, Textures.RockTile, Textures.RockTile);
            Slabs("Wall E", new Vector3(_map.MaxX, _map.GroundY, _map.MinZ), new Vector3(x1, top, _map.MaxZ), rock, rock, Textures.RockTile, Textures.RockTile);
            Slabs("Wall W", new Vector3(x0, _map.GroundY, _map.MinZ), new Vector3(_map.MinX, top, _map.MaxZ), rock, rock, Textures.RockTile, Textures.RockTile);

            // The ceiling must not shadow the whole arena from the skylight.
            int before = _root.childCount;
            Slabs("Ceiling", new Vector3(x0, top, z0), new Vector3(x1, top + 2f, z1), rock, rock, Textures.RockTile, Textures.RockTile);
            for (int i = before; i < _root.childCount; i++)
            {
                Gfx.NoShadows(_root.GetChild(i).gameObject);
            }
        }

        // ------------------------------------------------------------------------------------------ obstacles

        private void BuildObstacle(ObstacleBox box, int index)
        {
            Vector3 size = box.Size.ToUnity();
            Vector3 center = box.Center.ToUnity();
            bool solid = box.BlocksMovement && box.BlocksProjectiles;

            if (!solid)
            {
                // A barrier that lets something through is drawn like glass.
                Mesh glass = MeshKit.Box(size, 1f, 1f, index);
                Material material = MaterialFactory.Fresnel(new Color(0.45f, 0.85f, 1f, 1f), 1.3f, 1.8f, 0.09f);
                GameObject go = Gfx.Make("Glass " + box.Id, glass, _root, material, material);
                go.transform.position = center;
                Gfx.NoShadows(go);
                return;
            }

            if (box.IsRamp)
            {
                BuildRamp(box, size, center, index);
                return;
            }

            bool deck = IsDeck(box);
            Material stone = MaterialFactory.Surface(Textures.Bricks, new Color(0.95f, 0.92f, 0.95f), 0.2f, 0f);
            if (deck)
            {
                Material planks = MaterialFactory.Surface(Textures.Planks, Color.white, 0.25f, 0f);
                Mesh mesh = MeshKit.Box(size, Textures.BrickTile, Textures.PlankTile, index);
                GameObject go = Gfx.Make("Deck " + box.Id, mesh, _root, stone, planks);
                go.transform.position = center;
                Trim(box, 0.06f);
                Rail(box);
                return;
            }

            float height = box.MaxY - box.MinY;
            if (height <= 1.35f)
            {
                // Low cover: a stack of crates.
                Material crate = MaterialFactory.Surface(Textures.Crate, Color.white, 0.2f, 0f);
                Mesh mesh = MeshKit.Box(size, Textures.CrateTile, Textures.CrateTile, index);
                GameObject go = Gfx.Make("Cover " + box.Id, mesh, _root, crate, crate);
                go.transform.position = center;
                return;
            }

            if (height < 2.2f)
            {
                Mesh mesh = MeshKit.Box(size, Textures.BrickTile, Textures.BrickTile, index);
                GameObject go = Gfx.Make("Block " + box.Id, mesh, _root, stone, stone);
                go.transform.position = center;
                return;
            }

            // Tall walls and pillars: dark rock with a capstone.
            Material rock = MaterialFactory.Surface(Textures.Rock, Color.Lerp(Color.white, new Color(0.9f, 0.8f, 1f),
                (index % 3) / 2f), 0.2f, 0f);
            Mesh pillar = MeshKit.Box(size, Textures.RockTile, Textures.BrickTile, index);
            GameObject tower = Gfx.Make("Pillar " + box.Id, pillar, _root, rock, stone);
            tower.transform.position = center;
            // A glowing seam near the top makes tall things easy to read from afar.
            Strip(new Vector3(center.x, box.MaxY - 0.25f, center.z), new Vector3(size.x + 0.06f, 0.07f, size.z + 0.06f),
                MaterialFactory.Emissive(new Color(0.55f, 0.35f, 1f), 1.6f));
        }

        private void BuildRamp(ObstacleBox box, Vector3 size, Vector3 center, int index)
        {
            Material stone = MaterialFactory.Surface(Textures.Bricks, new Color(0.95f, 0.92f, 0.95f), 0.2f, 0f);
            Material planks = MaterialFactory.Surface(Textures.Planks, Color.white, 0.25f, 0f);
            Mesh mesh = MeshKit.Ramp(size, box.Rise, Textures.BrickTile, Textures.PlankTile, index);
            GameObject go = Gfx.Make("Ramp " + box.Id, mesh, _root, stone, planks);
            go.transform.position = center;

            // Glowing guide lines along both edges of the slope, so ramps read as the way up.
            bool alongX = box.Rise == RampDirection.PosX || box.Rise == RampDirection.NegX;
            float length = alongX ? size.x : size.z;
            float width = alongX ? size.z : size.x;
            float rise = size.y;
            float slopeLength = Mathf.Sqrt(length * length + rise * rise);
            Vector3 climb;
            switch (box.Rise)
            {
                case RampDirection.PosX: climb = new Vector3(length, rise, 0f); break;
                case RampDirection.NegX: climb = new Vector3(-length, rise, 0f); break;
                case RampDirection.PosZ: climb = new Vector3(0f, rise, length); break;
                default: climb = new Vector3(0f, rise, -length); break;
            }
            Vector3 lateral = alongX ? Vector3.forward : Vector3.right;
            Vector3 lowMid = new Vector3(center.x, box.MinY, center.z) - new Vector3(climb.x, 0f, climb.z) * 0.5f;
            Material glow = MaterialFactory.Emissive(DeckTrim, 1.8f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 start = lowMid + lateral * (side * (width * 0.5f - 0.05f));
                Vector3 mid = start + climb * 0.5f + Vector3.up * 0.04f;
                GameObject strip = Gfx.Primitive(PrimitiveType.Cube, "Guide", _root, glow);
                strip.transform.position = mid;
                strip.transform.rotation = Quaternion.LookRotation(climb.normalized, Vector3.up);
                strip.transform.localScale = new Vector3(0.1f, 0.05f, slopeLength);
                Gfx.NoShadows(strip);
            }
        }

        /// <summary>True for a box with a ramp reaching its top: a walkable platform.</summary>
        private bool IsDeck(ObstacleBox box)
        {
            for (int i = 0; i < _map.Obstacles.Length; i++)
            {
                ObstacleBox ramp = _map.Obstacles[i];
                if (!ramp.IsRamp || Mathf.Abs(ramp.MaxY - box.MaxY) > 0.05f || box.IsRamp)
                {
                    continue;
                }
                bool touchesX = ramp.MaxX >= box.MinX - 0.05f && ramp.MinX <= box.MaxX + 0.05f;
                bool touchesZ = ramp.MaxZ >= box.MinZ - 0.05f && ramp.MinZ <= box.MaxZ + 0.05f;
                if (touchesX && touchesZ)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A glowing line around the top edge of a platform.</summary>
        private void Trim(ObstacleBox box, float thickness)
        {
            Material glow = MaterialFactory.Emissive(DeckTrim, 1.8f);
            Vector3 size = box.Size.ToUnity();
            Vector3 center = box.Center.ToUnity();
            float y = box.MaxY + 0.012f;
            Strip(new Vector3(center.x, y, box.MinZ + 0.04f), new Vector3(size.x, thickness, 0.07f), glow);
            Strip(new Vector3(center.x, y, box.MaxZ - 0.04f), new Vector3(size.x, thickness, 0.07f), glow);
            Strip(new Vector3(box.MinX + 0.04f, y, center.z), new Vector3(0.07f, thickness, size.z), glow);
            Strip(new Vector3(box.MaxX - 0.04f, y, center.z), new Vector3(0.07f, thickness, size.z), glow);
        }

        /// <summary>Low posts at the corners of a platform, so its edge reads from the ground.</summary>
        private void Rail(ObstacleBox box)
        {
            Material post = MaterialFactory.Surface(Textures.Planks, new Color(0.7f, 0.7f, 0.75f), 0.2f, 0f);
            Material lamp = MaterialFactory.Emissive(new Color(1f, 0.8f, 0.45f), 2.2f);
            float[] xs = { box.MinX + 0.2f, box.MaxX - 0.2f };
            float[] zs = { box.MinZ + 0.2f, box.MaxZ - 0.2f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    GameObject pole = Gfx.Primitive(PrimitiveType.Cube, "Post", _root, post);
                    pole.transform.position = new Vector3(x, box.MaxY + 0.35f, z);
                    pole.transform.localScale = new Vector3(0.12f, 0.7f, 0.12f);
                    GameObject bulb = Gfx.Primitive(PrimitiveType.Sphere, "Lamp", _root, lamp);
                    bulb.transform.position = new Vector3(x, box.MaxY + 0.78f, z);
                    bulb.transform.localScale = Vector3.one * 0.2f;
                    Gfx.NoShadows(bulb);
                }
            }
        }

        private void Strip(Vector3 position, Vector3 scale, Material material)
        {
            GameObject strip = Gfx.Primitive(PrimitiveType.Cube, "Trim", _root, material);
            strip.transform.position = position;
            strip.transform.localScale = scale;
            Gfx.NoShadows(strip);
        }

        // ------------------------------------------------------------------------------------------ spawns

        private void BuildSpawns()
        {
            Material metal = MaterialFactory.Surface(null, new Color(0.12f, 0.13f, 0.17f), 0.6f, 0.4f);
            Material ringTexture = null;
            foreach (SpawnPoint spawn in _map.Spawns)
            {
                Color team = spawn.Team == _ownTeam ? Palette.TeamOwn : Palette.TeamOther;
                Vector3 at = spawn.Position.ToUnity();
                GameObject pad = Gfx.Primitive(PrimitiveType.Cylinder, "Spawn pad", _root, metal);
                pad.transform.position = at + Vector3.up * 0.03f;
                pad.transform.localScale = new Vector3(1.9f, 0.03f, 1.9f);
                Gfx.NoShadows(pad);

                GameObject ring = Gfx.Make("Spawn ring", Billboard.QuadMesh(), _root,
                    ringTexture != null ? ringTexture : ringTexture = MaterialFactory.GlowInstance(Textures.Ring, 1.4f));
                ring.GetComponent<Renderer>().SetPropertyBlock(Tint(team));
                ring.transform.position = at + Vector3.up * 0.07f;
                ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = Vector3.one * 2.6f;
                Gfx.NoShadows(ring);

                Gfx.AddPointLight(_root, "Spawn light", at + Vector3.up * 0.8f, team, 4.5f, 0.7f);
            }
        }

        private static MaterialPropertyBlock Tint(Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            return block;
        }

        // ------------------------------------------------------------------------------------------ decoration

        private static readonly Color[] CrystalColors =
        {
            new Color(0.25f, 0.85f, 1f), new Color(0.65f, 0.35f, 1f), new Color(0.3f, 1f, 0.6f),
            new Color(1f, 0.55f, 0.8f), new Color(0.35f, 0.55f, 1f)
        };

        /// <summary>Clusters of glowing crystals at the foot of the walls, each cluster with a light of its own.</summary>
        private void BuildCrystals()
        {
            float width = _map.MaxX - _map.MinX;
            float depth = _map.MaxZ - _map.MinZ;
            int clusters = 14;
            for (int i = 0; i < clusters; i++)
            {
                // Walk the perimeter at an even pace with some jitter.
                float along = (i + (float)_random.NextDouble() * 0.6f) / clusters * (2f * (width + depth));
                Vector3 position;
                Vector3 inward;
                PerimeterPoint(along, width, depth, out position, out inward);
                Color color = CrystalColors[_random.Next(CrystalColors.Length)];
                BuildCluster(position, inward, color, i);
            }
        }

        private void PerimeterPoint(float along, float width, float depth, out Vector3 position, out Vector3 inward)
        {
            float y = _map.GroundY;
            if (along < width)
            {
                position = new Vector3(_map.MinX + along, y, _map.MaxZ);
                inward = Vector3.back;
            }
            else if (along < width + depth)
            {
                position = new Vector3(_map.MaxX, y, _map.MaxZ - (along - width));
                inward = Vector3.left;
            }
            else if (along < 2f * width + depth)
            {
                position = new Vector3(_map.MaxX - (along - width - depth), y, _map.MinZ);
                inward = Vector3.forward;
            }
            else
            {
                position = new Vector3(_map.MinX, y, _map.MinZ + (along - 2f * width - depth));
                inward = Vector3.right;
            }
        }

        private void BuildCluster(Vector3 anchor, Vector3 inward, Color color, int index)
        {
            Material glow = MaterialFactory.Emissive(color, 2.4f, 0.8f);
            int count = 3 + _random.Next(3);
            for (int i = 0; i < count; i++)
            {
                float radius = 0.14f + (float)_random.NextDouble() * 0.16f;
                float height = 0.9f + (float)_random.NextDouble() * 1.9f;
                Vector2 lean = new Vector2((float)(_random.NextDouble() - 0.5), (float)(_random.NextDouble() - 0.5)) * 0.5f;
                Vector3 side = Vector3.Cross(Vector3.up, inward);
                Vector3 position = anchor + inward * (0.15f + (float)_random.NextDouble() * 0.5f)
                                          + side * ((float)(_random.NextDouble() - 0.5) * 1.6f);
                Mesh mesh = MeshKit.Crystal(radius, height, 6, lean);
                GameObject crystal = Gfx.Make("Crystal", mesh, _root, glow);
                crystal.transform.position = position;
                crystal.transform.rotation = Quaternion.Euler((float)(_random.NextDouble() - 0.5) * 24f,
                    (float)_random.NextDouble() * 360f, (float)(_random.NextDouble() - 0.5) * 24f);
                Gfx.NoShadows(crystal);
            }
            Vector3 center = anchor + inward * 0.5f + Vector3.up * 0.9f;
            Gfx.Halo(_root, "Crystal glow", center + Vector3.up * 0.3f, WithAlpha(color, 0.5f), 4.2f);
            Light light = Gfx.AddPointLight(_root, "Crystal light", center + inward * 0.6f, color, 16f, 1.35f);
            _crystalLights.Add(light);
            _crystalBase.Add(1.35f);
            _crystalPhase.Add(index * 1.7f);
        }

        /// <summary>Boulders along the base of the walls; only their near side pokes into the arena.</summary>
        private void BuildRocks()
        {
            Material rock = MaterialFactory.Surface(Textures.Rock, new Color(1f, 0.95f, 1f), 0.15f, 0f);
            float width = _map.MaxX - _map.MinX;
            float depth = _map.MaxZ - _map.MinZ;
            int count = 46;
            for (int i = 0; i < count; i++)
            {
                float along = (i + (float)_random.NextDouble()) / count * (2f * (width + depth));
                PerimeterPoint(along, width, depth, out Vector3 position, out Vector3 inward);
                float radius = 0.6f + (float)_random.NextDouble() * 1.1f;
                Mesh mesh = MeshKit.Rock(radius, i * 7 + 1, Textures.RockTile);
                GameObject boulder = Gfx.Make("Boulder", mesh, _root, rock);
                // Centre it outside the wall line so at most a fifth of its radius reaches into the arena.
                boulder.transform.position = position - inward * (radius * 0.8f);
                boulder.transform.rotation = Quaternion.Euler(0f, (float)_random.NextDouble() * 360f, 0f);
                boulder.transform.localScale = new Vector3(1f + (float)_random.NextDouble() * 0.5f, 0.8f + (float)_random.NextDouble() * 0.8f,
                    1f + (float)_random.NextDouble() * 0.4f);
            }
        }

        private void BuildStalactites()
        {
            Material rock = MaterialFactory.Surface(Textures.Rock, new Color(0.8f, 0.8f, 0.95f), 0.1f, 0f);
            float ceiling = _map.GroundY + CeilingHeight;
            int count = 46;
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(_map.MinX - 1f, _map.MaxX + 1f, (float)_random.NextDouble());
                float z = Mathf.Lerp(_map.MinZ - 1f, _map.MaxZ + 1f, (float)_random.NextDouble());
                float length = 1.5f + (float)_random.NextDouble() * 4.5f;
                float radius = 0.35f + (float)_random.NextDouble() * 0.9f;
                Mesh mesh = MeshKit.Cone(radius, length, 7, Textures.RockTile);
                GameObject spike = Gfx.Make("Stalactite", mesh, _root, rock);
                spike.transform.position = new Vector3(x, ceiling, z);
                spike.transform.rotation = Quaternion.Euler(180f, (float)_random.NextDouble() * 360f, 0f);
                Gfx.NoShadows(spike);
            }
        }

        private void BuildDust()
        {
            var go = new GameObject("Dust", typeof(ParticleSystem));
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3((_map.MinX + _map.MaxX) * 0.5f, _map.GroundY + 4f, (_map.MinZ + _map.MaxZ) * 0.5f);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.startLifetime = 14f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(0.6f, 0.8f, 1f, 0.35f);
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 16f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_map.MaxX - _map.MinX, 8f, _map.MaxZ - _map.MinZ);
            ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            drift.y = new ParticleSystem.MinMaxCurve(0.02f, 0.14f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.Glow();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            system.Play();
        }

        // ------------------------------------------------------------------------------------------ pickups

        private void BuildPickups(ContentCatalog content)
        {
            for (int i = 0; i < _map.Pickups.Length; i++)
            {
                PickupSlot slot = _map.Pickups[i];
                PickupInfo info = content.Pickup(slot.PickupIndex);
                _pickups.Add(BuildPickup(info.Id, slot.Position.ToUnity(), i));
            }
        }

        private PickupVisual BuildPickup(string id, Vector3 position, int index)
        {
            Color color = Palette.Pickup(id);
            var visual = new PickupVisual
            {
                Rest = position,
                Phase = index * 1.3f
            };
            var holder = new GameObject("Pickup " + id).transform;
            holder.SetParent(_root, false);
            holder.position = position;
            visual.Root = holder;

            // The floor marker stays when the pickup is taken, dimmer, to show where it will come back.
            GameObject marker = Gfx.Make("Marker", Billboard.QuadMesh(), holder,
                MaterialFactory.GlowInstance(Textures.Ring, 1.2f));
            marker.GetComponent<Renderer>().SetPropertyBlock(Tint(WithAlpha(color, 0.8f)));
            marker.transform.localPosition = Vector3.up * 0.06f;
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Gfx.NoShadows(marker);
            visual.Marker = marker.transform;

            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(holder, false);
            visual.Spinner = spinner;
            Material glow = MaterialFactory.Emissive(color, 2.2f, 0.7f);
            Material white = MaterialFactory.Surface(null, new Color(0.9f, 0.92f, 0.95f), 0.5f, 0.1f);
            switch (id)
            {
                case "health_pack":
                    {
                        GameObject box = Gfx.Primitive(PrimitiveType.Cube, "Case", spinner, white);
                        box.transform.localScale = new Vector3(0.52f, 0.4f, 0.34f);
                        GameObject bar = Gfx.Primitive(PrimitiveType.Cube, "CrossH", spinner, glow);
                        bar.transform.localScale = new Vector3(0.34f, 0.1f, 0.36f);
                        GameObject stem = Gfx.Primitive(PrimitiveType.Cube, "CrossV", spinner, glow);
                        stem.transform.localScale = new Vector3(0.1f, 0.3f, 0.36f);
                        break;
                    }
                case "shield_cell":
                    {
                        Mesh crystal = MeshKit.Crystal(0.2f, 0.7f, 6, Vector2.zero);
                        GameObject cell = Gfx.Make("Cell", crystal, spinner, glow);
                        cell.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                        GameObject top = Gfx.Make("CellTop", crystal, spinner, glow);
                        top.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                        top.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                        break;
                    }
                case "overshield_orb":
                    {
                        GameObject orb = Gfx.Primitive(PrimitiveType.Sphere, "Orb", spinner, glow);
                        orb.transform.localScale = Vector3.one * 0.46f;
                        for (int ring = 0; ring < 2; ring++)
                        {
                            GameObject band = Gfx.Make("Band", Billboard.QuadMesh(), spinner,
                                MaterialFactory.GlowInstance(Textures.Ring, 1.6f));
                            band.GetComponent<Renderer>().SetPropertyBlock(Tint(color));
                            band.transform.localScale = Vector3.one * 1.0f;
                            band.transform.localRotation = Quaternion.Euler(ring == 0 ? 0f : 90f, 0f, 0f);
                        }
                        break;
                    }
                default:
                    {
                        GameObject gem = Gfx.Make("Gem", MeshKit.Crystal(0.18f, 0.6f, 5, Vector2.zero), spinner, glow);
                        gem.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                        break;
                    }
            }
            Gfx.Halo(spinner, "Pickup glow", position, WithAlpha(color, 0.65f), 1.9f);
            Light light = Gfx.AddPointLight(holder, "Pickup light", position + Vector3.up * 1.1f, color, 6f, 1.2f);
            visual.Light = light;
            Gfx.NoShadows(spinner.gameObject);
            visual.Renderers = spinner.GetComponentsInChildren<Renderer>();
            return visual;
        }

        /// <summary>A seed that is the same on every run (string.GetHashCode is not guaranteed to be).</summary>
        private static int StableSeed(string text)
        {
            int hash = 17;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash = unchecked(hash * 31 + text[i]);
                }
            }
            return hash;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
