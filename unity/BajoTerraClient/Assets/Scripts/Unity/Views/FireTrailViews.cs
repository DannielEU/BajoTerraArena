using System.Collections.Generic;
using Bajoterra.Client.Content;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the walls of fire Infurnus leaves on the floor, patch by patch as the server lists them: flames licking
    /// up from each patch, a glowing bed of embers under them, a few lights that make the walls around flicker, a
    /// "fwoosh" as the fire catches and a crackling roar near the camera. The flames die down in a patch's last
    /// moments. One particle system draws every flame, so even long walls cost little; nothing is allocated per frame.
    /// </summary>
    public sealed class FireTrailViews
    {
        private const int MaxLights = 8;
        private const int Voices = 3;
        private const float FlamesPerSecond = 46f;
        private const float DyingTicks = 40f;

        private static readonly Color Ember = new Color(1f, 0.45f, 0.12f);

        private sealed class Patch
        {
            public Transform Bed;
            public Renderer BedRenderer;
            public Transform Tongue;
            public Renderer TongueRenderer;
            public uint Id;
            public int Stamp;
            public float Owed;
        }

        private readonly Transform _parent;
        private readonly ContentCatalog _content;
        private readonly AudioPlayer _audio;
        private readonly ParticleSystem _flames;
        private readonly Light[] _lights = new Light[MaxLights];
        private readonly AudioSource[] _voices = new AudioSource[Voices];
        private readonly Dictionary<uint, Patch> _active = new Dictionary<uint, Patch>();
        private readonly Stack<Patch> _free = new Stack<Patch>();
        private readonly List<uint> _stale = new List<uint>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly System.Random _random = new System.Random(5);
        private readonly int[] _nearest = new int[Voices];
        private readonly float[] _nearestDistance = new float[Voices];
        private float _time;
        private float _lastWhoosh = -10f;

        public FireTrailViews(Transform parent, ContentCatalog content, AudioPlayer audio)
        {
            _parent = new GameObject("Walls of fire").transform;
            _parent.SetParent(parent, false);
            _content = content;
            _audio = audio;
            _flames = CreateFlames();
            for (int i = 0; i < MaxLights; i++)
            {
                _lights[i] = Gfx.AddPointLight(_parent, "Fire light " + i, Vector3.zero, Ember, 5f, 0f);
                _lights[i].enabled = false;
            }
            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = audio.CreateLoop(_parent, audio.FireLoop);
                _voices[i].volume = 0f;
            }
        }

        /// <summary>How many patches are burning on screen (tests).</summary>
        public int Count
        {
            get { return _active.Count; }
        }

        public void Apply(Snapshot latest, float deltaTime, Vector3 camera)
        {
            _time += deltaTime;
            for (int i = 0; i < Voices; i++)
            {
                _nearest[i] = -1;
                _nearestDistance[i] = float.MaxValue;
            }
            int lights = 0;
            int count = latest == null ? 0 : latest.Hazards.Count;
            for (int i = 0; i < count; i++)
            {
                HazardState h = latest.Hazards[i];
                if (!_active.TryGetValue(h.Id, out Patch patch))
                {
                    patch = Acquire();
                    patch.Id = h.Id;
                    patch.Owed = 0f;
                    _active[h.Id] = patch;
                    if (_time - _lastWhoosh > 0.3f)
                    {
                        _lastWhoosh = _time;
                        _audio.PlayAt(_audio.FireWhoosh, new Vector3(h.X, h.Y, h.Z), 0.7f, Random(0.9f, 1.15f));
                    }
                }
                patch.Stamp = (int)(_time * 1000f);
                SlugInfo slug = _content.Slug((int)h.SlugIndex);
                float radius = Mathf.Max(0.3f, slug.TrailRadius);
                float tall = Mathf.Max(0.4f, slug.TrailHeight);
                float life = Mathf.Clamp01(h.TicksLeft / DyingTicks);
                var at = new Vector3(h.X, h.Y, h.Z);

                patch.Bed.position = at + Vector3.up * 0.03f;
                patch.Bed.localScale = Vector3.one * (radius * 2.6f);
                float flicker = 0.75f + 0.25f * Mathf.Sin(_time * 17f + h.Id * 1.3f);
                _block.SetColor("_Color", new Color(Ember.r, Ember.g, Ember.b, (0.35f + 0.4f * life) * flicker));
                patch.BedRenderer.SetPropertyBlock(_block);
                // A tall tongue of flame over the patch, so the line reads as a wall of fire at a glance.
                float lick = 0.85f + 0.3f * Mathf.Sin(_time * 13f + h.Id * 2.1f) * Mathf.Sin(_time * 7f + h.Id);
                patch.Tongue.position = at + Vector3.up * (tall * 0.5f * lick * (0.4f + 0.6f * life));
                patch.Tongue.localScale = new Vector3(radius * 1.7f, tall * 1.25f * lick * (0.4f + 0.6f * life), 1f);
                _block.SetColor("_Color", new Color(1f, 0.62f, 0.2f, 0.55f * life * lick));
                patch.TongueRenderer.SetPropertyBlock(_block);

                // Flames, fewer and lower as the patch dies.
                patch.Owed += FlamesPerSecond * deltaTime * (0.3f + 0.7f * life);
                while (patch.Owed >= 1f)
                {
                    patch.Owed -= 1f;
                    EmitFlame(at, radius, tall * (0.5f + 0.5f * life));
                }

                if (h.Id % 3 == 0 && lights < MaxLights)
                {
                    Light light = _lights[lights++];
                    light.enabled = true;
                    light.transform.position = at + Vector3.up * 0.8f;
                    light.intensity = (1.4f + 0.5f * Mathf.Sin(_time * 23f + h.Id)) * (0.3f + 0.7f * life);
                }
                TrackNearest(i, (at - camera).sqrMagnitude);
            }
            for (int i = lights; i < MaxLights; i++)
            {
                _lights[i].enabled = false;
            }
            for (int v = 0; v < Voices; v++)
            {
                AudioSource voice = _voices[v];
                if (_nearest[v] < 0)
                {
                    voice.volume = Mathf.MoveTowards(voice.volume, 0f, deltaTime * 2f);
                    if (voice.volume <= 0f && voice.isPlaying)
                    {
                        voice.Stop();
                    }
                    continue;
                }
                HazardState h = latest.Hazards[_nearest[v]];
                voice.transform.position = new Vector3(h.X, h.Y + 0.5f, h.Z);
                voice.volume = Mathf.MoveTowards(voice.volume, 0.55f, deltaTime * 3f);
                if (!voice.isPlaying)
                {
                    voice.pitch = Random(0.9f, 1.1f);
                    voice.Play();
                }
            }

            int stamp = (int)(_time * 1000f);
            _stale.Clear();
            foreach (KeyValuePair<uint, Patch> entry in _active)
            {
                if (entry.Value.Stamp != stamp)
                {
                    _stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                Patch patch = _active[_stale[i]];
                _active.Remove(_stale[i]);
                patch.Bed.gameObject.SetActive(false);
                patch.Tongue.gameObject.SetActive(false);
                _free.Push(patch);
            }
        }

        public void Destroy()
        {
            if (_parent != null)
            {
                Object.Destroy(_parent.gameObject);
            }
        }

        /// <summary>Keeps the three patches nearest the camera for the crackling voices (spread out, not stacked).</summary>
        private void TrackNearest(int index, float distanceSq)
        {
            for (int v = 0; v < Voices; v++)
            {
                if (distanceSq < _nearestDistance[v])
                {
                    for (int k = Voices - 1; k > v; k--)
                    {
                        _nearest[k] = _nearest[k - 1];
                        _nearestDistance[k] = _nearestDistance[k - 1];
                    }
                    _nearest[v] = index;
                    _nearestDistance[v] = distanceSq;
                    return;
                }
            }
        }

        private void EmitFlame(Vector3 at, float radius, float tall)
        {
            float a = (float)_random.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)_random.NextDouble()) * radius * 0.8f;
            var emit = new ParticleSystem.EmitParams
            {
                position = at + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r),
                velocity = new Vector3(Random(-0.25f, 0.25f), tall * Random(1.3f, 2f), Random(-0.25f, 0.25f)),
                startSize = Random(0.35f, 0.7f) * Mathf.Lerp(1f, 0.6f, r / Mathf.Max(0.01f, radius)),
                startLifetime = Random(0.4f, 0.7f),
                startColor = new Color(1f, Random(0.75f, 0.95f), Random(0.35f, 0.55f), 1f)
            };
            _flames.Emit(emit, 1);
        }

        private float Random(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private Patch Acquire()
        {
            if (_free.Count > 0)
            {
                Patch reused = _free.Pop();
                reused.Bed.gameObject.SetActive(true);
                reused.Tongue.gameObject.SetActive(true);
                return reused;
            }
            GameObject bed = Gfx.Make("Embers", Billboard.QuadMesh(), _parent,
                MaterialFactory.GlowInstance(Textures.SoftDot, 1.6f));
            bed.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Gfx.NoShadows(bed);
            Transform tongue = Gfx.Halo(_parent, "Flame", Vector3.zero, new Color(1f, 0.6f, 0.2f), 1f);
            return new Patch
            {
                Bed = bed.transform, BedRenderer = bed.GetComponent<Renderer>(), Tongue = tongue,
                TongueRenderer = tongue.GetComponent<Renderer>()
            };
        }

        private ParticleSystem CreateFlames()
        {
            var go = new GameObject("Flames", typeof(ParticleSystem));
            go.transform.SetParent(_parent, false);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 5000;
            main.gravityModifier = -0.15f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f),
                    new GradientColorKey(new Color(0.85f, 0.15f, 0.05f), 1f)
                },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0.15f)));
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFactory.Glow();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }
    }
}
