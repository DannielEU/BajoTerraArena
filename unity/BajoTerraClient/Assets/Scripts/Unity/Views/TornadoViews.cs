using System.Collections.Generic;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the tornadoes Tormato turns into, as the server lists them: a spinning funnel of wind, wide at the top and
    /// tight at the eye, streaks of dust spiralling up it, a ring of dust whipped up on the floor and a howling roar
    /// that follows it. A tornado that swept over a wall of fire turns into a fire tornado, orange and flickering; one
    /// that swallowed an ice slug turns into a freezing tornado, pale icy blue, glittering with snow. It
    /// grows in its first moments and dies down in its last. Nothing is allocated per frame.
    /// </summary>
    public sealed class TornadoViews
    {
        private const int Rows = 6;
        private const int Sides = 20;
        private const float GrowTicks = 20f;
        private const float DyingTicks = 40f;

        private static readonly Color Wind = new Color(0.78f, 0.94f, 1f);
        private static readonly Color Fire = new Color(1f, 0.5f, 0.15f);
        private static readonly Color Frost = new Color(0.35f, 0.72f, 1f);

        private sealed class Twister
        {
            public Transform Root;
            public Transform Funnel;
            public Renderer FunnelRenderer;
            public Transform Dust;
            public Renderer DustRenderer;
            public ParticleSystem Streaks;
            public Light Light;
            public AudioSource Roar;
            public uint Id;
            public int Stamp;
            public bool Flaming;
            public bool Freezing;
            public float Spin;
        }

        private readonly Transform _parent;
        private readonly AudioPlayer _audio;
        private readonly Mesh _funnelMesh;
        private readonly Dictionary<uint, Twister> _active = new Dictionary<uint, Twister>();
        private readonly Stack<Twister> _free = new Stack<Twister>();
        private readonly List<uint> _stale = new List<uint>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private int _stamp;
        private float _time;

        public TornadoViews(Transform parent, AudioPlayer audio)
        {
            _parent = new GameObject("Tornadoes").transform;
            _parent.SetParent(parent, false);
            _audio = audio;
            _funnelMesh = BuildFunnel();
        }

        /// <summary>How many tornadoes are drawn right now (tests).</summary>
        public int Count
        {
            get { return _active.Count; }
        }

        public void Apply(Snapshot latest, float deltaTime)
        {
            _stamp++;
            _time += deltaTime;
            int count = latest == null ? 0 : latest.Tornadoes.Count;
            for (int i = 0; i < count; i++)
            {
                TornadoState t = latest.Tornadoes[i];
                if (!_active.TryGetValue(t.Id, out Twister twister))
                {
                    twister = Acquire();
                    twister.Id = t.Id;
                    twister.Flaming = !t.Flaming;
                    twister.Freezing = t.Freezing;
                    twister.Spin = t.Id * 37f;
                    twister.Root.position = new Vector3(t.X, t.Y, t.Z);
                    twister.Streaks.Clear();
                    twister.Streaks.Play();
                    twister.Roar.pitch = 0.9f + (t.Id % 5) * 0.05f;
                    twister.Roar.Play();
                    _active[t.Id] = twister;
                }
                twister.Stamp = _stamp;
                if (twister.Flaming != t.Flaming || twister.Freezing != t.Freezing)
                {
                    twister.Flaming = t.Flaming;
                    twister.Freezing = t.Freezing;
                    Recolour(twister);
                }
                Pose(twister, t, deltaTime);
            }

            _stale.Clear();
            foreach (KeyValuePair<uint, Twister> entry in _active)
            {
                if (entry.Value.Stamp != _stamp)
                {
                    _stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                Twister twister = _active[_stale[i]];
                _active.Remove(_stale[i]);
                twister.Roar.Stop();
                twister.Streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                twister.Root.gameObject.SetActive(false);
                _free.Push(twister);
            }
        }

        public void Destroy()
        {
            if (_parent != null)
            {
                Object.Destroy(_parent.gameObject);
            }
            if (_funnelMesh != null)
            {
                Object.Destroy(_funnelMesh);
            }
        }

        private void Pose(Twister twister, TornadoState t, float deltaTime)
        {
            // Drift smoothly between snapshots instead of hopping 20 times a second.
            Vector3 target = new Vector3(t.X, t.Y, t.Z);
            twister.Root.position = Vector3.Lerp(twister.Root.position, target, 1f - Mathf.Exp(-deltaTime * 14f));
            float age = t.LifeTicks - (float)t.TicksLeft;
            float grow = Mathf.Clamp01(age / GrowTicks);
            float fade = Mathf.Clamp01(t.TicksLeft / DyingTicks);
            float strength = Mathf.Min(grow, fade);
            twister.Spin += deltaTime * (420f + 120f * Mathf.Sin(_time * 1.7f + t.Id));
            float sway = Mathf.Sin(_time * 2.3f + t.Id) * 6f;
            twister.Funnel.localRotation = Quaternion.Euler(sway, twister.Spin, sway * 0.6f);
            twister.Funnel.localScale = new Vector3(t.Radius * (0.6f + 0.4f * strength), t.Height * (0.3f + 0.7f * grow),
                t.Radius * (0.6f + 0.4f * strength));
            Color colour = twister.Flaming ? Fire : twister.Freezing ? Frost : Wind;
            float flicker = twister.Flaming ? 0.8f + 0.2f * Mathf.Sin(_time * 19f + t.Id)
                : twister.Freezing ? 0.9f + 0.1f * Mathf.Sin(_time * 7f + t.Id) : 1f;
            float opacity = twister.Flaming ? 0.4f : twister.Freezing ? 0.36f : 0.22f;
            _block.SetColor("_Color", new Color(colour.r, colour.g, colour.b, opacity * strength * flicker));
            twister.FunnelRenderer.SetPropertyBlock(_block);

            twister.Dust.localScale = Vector3.one * (t.Radius * 2.4f * (0.7f + 0.3f * Mathf.Sin(_time * 5f + t.Id)));
            twister.Dust.localRotation = Quaternion.Euler(90f, -twister.Spin * 0.5f, 0f);
            _block.SetColor("_Color", new Color(colour.r, colour.g, colour.b, 0.45f * strength));
            twister.DustRenderer.SetPropertyBlock(_block);

            ParticleSystem.MainModule main = twister.Streaks.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(colour.r, colour.g, colour.b, 0.9f * strength),
                new Color(1f, 1f, 1f, 0.7f * strength));
            ParticleSystem.ShapeModule shape = twister.Streaks.shape;
            shape.radius = t.EyeRadius;
            ParticleSystem.VelocityOverLifetimeModule velocity = twister.Streaks.velocityOverLifetime;
            velocity.z = new ParticleSystem.MinMaxCurve(t.Height * 0.6f, t.Height * 0.95f);

            twister.Light.enabled = twister.Flaming || twister.Freezing;
            if (twister.Freezing)
            {
                twister.Light.color = Frost;
                twister.Light.intensity = (1.8f + 0.3f * Mathf.Sin(_time * 5f + t.Id)) * strength;
                twister.Light.transform.localPosition = new Vector3(0f, t.Height * 0.4f, 0f);
            }
            else if (twister.Flaming)
            {
                twister.Light.color = Fire;
                twister.Light.intensity = (2.2f + 0.8f * Mathf.Sin(_time * 23f + t.Id)) * strength;
                twister.Light.transform.localPosition = new Vector3(0f, t.Height * 0.4f, 0f);
            }
            twister.Roar.volume = 0.7f * strength;
        }

        private static void Recolour(Twister twister)
        {
            ParticleSystemRenderer renderer = twister.Streaks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.Glow();
            ParticleSystem.MainModule main = twister.Streaks.main;
            // Snow is fine and glittery, flames are big tongues, dust is in between.
            main.startSize = twister.Flaming ? new ParticleSystem.MinMaxCurve(0.4f, 0.9f)
                : twister.Freezing ? new ParticleSystem.MinMaxCurve(0.15f, 0.4f) : new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        }

        private Twister Acquire()
        {
            if (_free.Count > 0)
            {
                Twister reused = _free.Pop();
                reused.Root.gameObject.SetActive(true);
                return reused;
            }
            var root = new GameObject("Tornado").transform;
            root.SetParent(_parent, false);

            GameObject funnel = Gfx.Make("Funnel", _funnelMesh, root, MaterialFactory.Fresnel(Wind, 1.6f, 3.2f, 0.0f));
            Gfx.NoShadows(funnel);
            GameObject dust = Gfx.Make("Dust", Billboard.QuadMesh(), root, MaterialFactory.GlowInstance(Textures.Ring, 1.4f));
            dust.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            Gfx.NoShadows(dust);
            Light light = Gfx.AddPointLight(root, "Fire light", root.position, Fire, 9f, 0f);
            light.enabled = false;

            var twister = new Twister
            {
                Root = root,
                Funnel = funnel.transform,
                FunnelRenderer = funnel.GetComponent<Renderer>(),
                Dust = dust.transform,
                DustRenderer = dust.GetComponent<Renderer>(),
                Streaks = CreateStreaks(root),
                Light = light,
                Roar = _audio.CreateLoop(root, _audio.WindLoop)
            };
            twister.Roar.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            Recolour(twister);
            return twister;
        }

        /// <summary>Dust and wind streaks spiralling up round the eye, simulated in the tornado's own space.</summary>
        private static ParticleSystem CreateStreaks(Transform parent)
        {
            var go = new GameObject("Streaks", typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = 0f;
            main.maxParticles = 700;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 260f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0.3f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // The system is turned to point up, so local z is the world's up and orbiting round z spins the column.
            // Every axis in the same mode (two constants), or Unity refuses to move the particles at all.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(4f, 6f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(7f, 10f);
            velocity.radial = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }

        /// <summary>
        /// The funnel: an open tube 1 m tall, narrow at the foot and wide at the top, wobbling a little so the spin
        /// shows; drawn with a rim-lit material it reads as a whirl of wind. Scaled per tornado to its size.
        /// </summary>
        private static Mesh BuildFunnel()
        {
            int ring = Sides + 1;
            var vertices = new Vector3[(Rows + 1) * ring];
            var uvs = new Vector2[vertices.Length];
            for (int row = 0; row <= Rows; row++)
            {
                float v = row / (float)Rows;
                float radius = 0.28f + 0.72f * v * v;
                for (int side = 0; side <= Sides; side++)
                {
                    float a = side * Mathf.PI * 2f / Sides;
                    float wobble = 1f + 0.08f * Mathf.Sin(a * 3f + v * 7f);
                    vertices[row * ring + side] = new Vector3(Mathf.Cos(a) * radius * wobble, v, Mathf.Sin(a) * radius * wobble);
                    uvs[row * ring + side] = new Vector2(side / (float)Sides, v);
                }
            }
            var triangles = new int[Rows * Sides * 6];
            int k = 0;
            for (int row = 0; row < Rows; row++)
            {
                for (int side = 0; side < Sides; side++)
                {
                    int a = row * ring + side;
                    int b = a + 1;
                    int c = a + ring;
                    int d = c + 1;
                    triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                    triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                }
            }
            var mesh = new Mesh { name = "Funnel" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
