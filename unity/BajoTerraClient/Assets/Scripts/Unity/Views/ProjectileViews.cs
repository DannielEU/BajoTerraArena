using System.Collections.Generic;
using Bajoterra.Client;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the slugs in flight. Each server projectile id gets a little slug (a round body with eyes) wrapped in a
    /// glow of its element's colour, a streaming trail and a small light that colours the walls it passes. Sticky
    /// slugs (Grenuke) also carry a dark band and a lit fuse; once stuck they blink faster and faster, mark their
    /// blast on the floor with a red circle and hiss, beeping as the fuse burns down. Visuals are pooled, so a busy
    /// fight allocates nothing per frame.
    /// </summary>
    public sealed class ProjectileViews
    {
        private const int MaxLights = 10;
        private static readonly Color GrenadeBody = new Color(0.95f, 0.26f, 0.1f);
        private static readonly Color Spark = new Color(1f, 0.9f, 0.45f);

        private sealed class Visual
        {
            public Transform Root;
            public Transform Body;
            public TrailRenderer Trail;
            public Renderer BodyRenderer;
            public Transform Halo;
            public Renderer HaloRenderer;
            public Light Light;
            public GameObject GrenadeParts;
            public Transform Spark;
            public Transform BlastRing;
            public Renderer BlastRingRenderer;
            public Transform BlastFill;
            public Renderer BlastFillRenderer;
            public AudioSource FuseSound;
            public int Stamp;
            public bool Sticky;
            public bool Stuck;
            public int FuseTicks;
            public float BlastRadius;
            public float NextBeep;
            public Color Color;
        }

        private readonly Transform _parent;
        private readonly ContentCatalog _content;
        private readonly ArenaMap _map;
        private readonly AudioPlayer _audio;
        private readonly Dictionary<uint, Visual> _active = new Dictionary<uint, Visual>();
        private readonly Stack<Visual> _free = new Stack<Visual>();
        private readonly List<uint> _stale = new List<uint>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Material _trailMaterial;
        private int _stamp;

        public ProjectileViews(Transform parent, ContentCatalog content, ArenaMap map, AudioPlayer audio)
        {
            _parent = new GameObject("Projectiles").transform;
            _parent.SetParent(parent, false);
            _content = content;
            _map = map;
            _audio = audio;
        }

        public void Apply(WorldFrame frame, float time)
        {
            _stamp++;
            int lights = 0;
            for (int i = 0; i < frame.ProjectileCount; i++)
            {
                ProjectileFrame p = frame.Projectiles[i];
                if (!_active.TryGetValue(p.Id, out Visual visual))
                {
                    visual = Acquire();
                    Configure(visual, p);
                    _active[p.Id] = visual;
                }
                visual.Stamp = _stamp;
                visual.Root.position = p.Position.ToUnity();
                float pulse = 1f + 0.12f * Mathf.Sin(time * 25f + p.Id);
                float size = BallSize(p);
                // A stuck grenade keeps the facing it landed with.
                if (!p.Stuck && p.Velocity.X * p.Velocity.X + p.Velocity.Z * p.Velocity.Z + p.Velocity.Y * p.Velocity.Y > 1e-4f)
                {
                    visual.Body.localRotation = Quaternion.LookRotation(p.Velocity.ToUnity());
                }
                visual.Body.localScale = Vector3.one * (size * pulse);
                visual.Halo.localScale = Vector3.one * (size * 2.6f * pulse);
                bool lit = lights < MaxLights || p.Stuck;
                visual.Light.enabled = lit;
                if (lit)
                {
                    lights++;
                }
                if (visual.Sticky)
                {
                    AnimateGrenade(visual, p, size, time);
                }
            }

            _stale.Clear();
            foreach (KeyValuePair<uint, Visual> entry in _active)
            {
                if (entry.Value.Stamp != _stamp)
                {
                    _stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                Release(_stale[i]);
            }
        }

        public void Destroy()
        {
            if (_parent != null)
            {
                Object.Destroy(_parent.gameObject);
            }
        }

        private float BallSize(ProjectileFrame p)
        {
            return _content.Slug(p.SlugIndex).ProjectileRadius * 2.4f;
        }

        private void Configure(Visual visual, ProjectileFrame p)
        {
            SlugInfo info = _content.Slug(p.SlugIndex);
            Color color = Palette.Element(_content.ElementName((int)info.Element));
            // Slugs that help allies glow whiter, so they read differently from attacks at a glance.
            if (info.Targeting != Targeting.Enemies)
            {
                color = Color.Lerp(color, Color.white, 0.5f);
            }
            visual.Sticky = info.Sticky;
            visual.Stuck = false;
            visual.FuseTicks = (int)Mathf.Max(1, info.FuseTicks);
            visual.BlastRadius = info.BlastRadius;
            visual.Color = color;
            visual.BodyRenderer.sharedMaterial = info.Sticky
                ? MaterialFactory.Emissive(GrenadeBody, 0.75f, 0.75f)
                : MaterialFactory.Emissive(color, 0.9f, 0.7f);
            SetHalo(visual.HaloRenderer, new Color(color.r, color.g, color.b, 0.4f));
            visual.Light.color = color;
            visual.Light.intensity = 1.7f;
            float width = Mathf.Max(0.16f, info.ProjectileRadius * 2.6f);
            visual.Trail.widthMultiplier = width;
            visual.Trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
            visual.Trail.endColor = new Color(color.r, color.g, color.b, 0f);
            visual.Root.position = p.Position.ToUnity();
            visual.Trail.Clear();
            visual.Trail.emitting = true;
            visual.GrenadeParts.SetActive(info.Sticky);
            visual.BlastRing.gameObject.SetActive(false);
            visual.BlastFill.gameObject.SetActive(false);
            if (info.Sticky)
            {
                // The fuse hisses quietly in flight and loudly once stuck.
                visual.FuseSound.volume = 0.25f;
                visual.FuseSound.pitch = 1f;
                visual.FuseSound.Play();
            }
        }

        /// <summary>The fuse spark flickers; once stuck the grenade blinks red faster and faster and swells.</summary>
        private void AnimateGrenade(Visual visual, ProjectileFrame p, float size, float time)
        {
            float flicker = 0.75f + 0.25f * Mathf.Sin(time * 47f + p.Id * 1.7f) * Mathf.Sin(time * 31f);
            visual.Spark.localScale = Vector3.one * (1.1f * flicker);
            if (p.Stuck && !visual.Stuck)
            {
                visual.Stuck = true;
                visual.Trail.emitting = false;
                visual.NextBeep = time;
                visual.FuseSound.volume = 0.6f;
                visual.BlastRing.gameObject.SetActive(visual.BlastRadius > 0f);
                visual.BlastFill.gameObject.SetActive(visual.BlastRadius > 0f);
            }
            if (!p.Stuck)
            {
                return;
            }
            // 0 just stuck .. 1 about to explode.
            float progress = Mathf.Clamp01(1f - p.FuseTicks / (float)visual.FuseTicks);
            float blinkHz = Mathf.Lerp(2.5f, 12f, progress * progress);
            bool on = Mathf.Repeat(time * blinkHz, 1f) < 0.5f;
            Color glow = on ? Palette.Danger : visual.Color;
            SetHalo(visual.HaloRenderer, new Color(glow.r, glow.g, glow.b, on ? 0.75f : 0.35f));
            visual.Light.color = glow;
            visual.Light.intensity = on ? 3.2f : 1.4f;
            visual.Body.localScale = Vector3.one * (size * (1f + 0.25f * progress) * (on ? 1.06f : 1f));
            visual.FuseSound.pitch = 1f + 0.55f * progress;

            if (time >= visual.NextBeep)
            {
                _audio.PlayAt(_audio.FuseBeep, visual.Root.position, 0.55f, 1f + 0.35f * progress);
                visual.NextBeep = time + Mathf.Lerp(0.42f, 0.07f, progress);
            }

            if (visual.BlastRadius > 0f)
            {
                Vector3 at = visual.Root.position;
                float below = _map.SightDistance(new Vec3(at.x, at.y, at.z), new Vec3(0f, -1f, 0f), 12f, 0f);
                float floor = Mathf.Max(_map.GroundY, at.y - below) + 0.06f;
                float diameter = visual.BlastRadius * 2f;
                visual.BlastRing.position = new Vector3(at.x, floor, at.z);
                visual.BlastRing.localScale = Vector3.one * (diameter * (1.04f + (on ? 0.03f : 0f)));
                visual.BlastFill.position = new Vector3(at.x, floor - 0.01f, at.z);
                visual.BlastFill.localScale = Vector3.one * (diameter * 1.25f);
                float alpha = Mathf.Lerp(0.45f, 0.9f, progress) * (on ? 1f : 0.6f);
                SetHalo(visual.BlastRingRenderer, new Color(Palette.Danger.r, Palette.Danger.g, Palette.Danger.b, alpha));
                SetHalo(visual.BlastFillRenderer, new Color(Palette.Danger.r, Palette.Danger.g * 0.6f, Palette.Danger.b, alpha * 0.35f));
            }
        }

        private void SetHalo(Renderer renderer, Color color)
        {
            _block.SetColor("_Color", color);
            renderer.SetPropertyBlock(_block);
        }

        private Visual Acquire()
        {
            Visual visual;
            if (_free.Count > 0)
            {
                visual = _free.Pop();
                visual.Root.gameObject.SetActive(true);
                return visual;
            }
            var root = new GameObject("Slug", typeof(TrailRenderer));
            root.transform.SetParent(_parent, false);

            // The slug: a round body with two eyes looking where it flies.
            GameObject body = Gfx.Primitive(PrimitiveType.Sphere, "Body", root.transform, null);
            Material eyeWhite = MaterialFactory.Surface(null, Color.white, 0.6f, 0f);
            Material pupil = MaterialFactory.Surface(null, new Color(0.04f, 0.04f, 0.06f), 0.8f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = Gfx.Primitive(PrimitiveType.Sphere, "Eye", body.transform, eyeWhite);
                eye.transform.localPosition = new Vector3(side * 0.22f, 0.16f, 0.38f);
                eye.transform.localScale = Vector3.one * 0.3f;
                GameObject dot = Gfx.Primitive(PrimitiveType.Sphere, "Pupil", body.transform, pupil);
                dot.transform.localPosition = new Vector3(side * 0.22f, 0.16f, 0.5f);
                dot.transform.localScale = Vector3.one * 0.15f;
            }

            // Grenade dressing (only shown for sticky slugs): a dark band round the middle and a fuse on the back
            // whose tip burns.
            var grenade = new GameObject("Grenade");
            grenade.transform.SetParent(body.transform, false);
            Material band = MaterialFactory.Surface(null, new Color(0.16f, 0.08f, 0.06f), 0.4f, 0.3f);
            GameObject belt = Gfx.Primitive(PrimitiveType.Cylinder, "Band", grenade.transform, band);
            belt.transform.localScale = new Vector3(1.06f, 0.06f, 1.06f);
            belt.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject fuse = Gfx.Primitive(PrimitiveType.Cylinder, "Fuse", grenade.transform,
                MaterialFactory.Surface(null, new Color(0.35f, 0.25f, 0.15f), 0.2f, 0f));
            fuse.transform.localPosition = new Vector3(0f, 0.5f, -0.28f);
            fuse.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            fuse.transform.localScale = new Vector3(0.09f, 0.18f, 0.09f);
            Transform spark = Gfx.Halo(grenade.transform, "Spark", Vector3.zero, Spark, 1.1f, Textures.Flare);
            spark.localPosition = new Vector3(0f, 0.68f, -0.42f);
            Gfx.NoShadows(body);

            Transform halo = Gfx.Halo(root.transform, "Glow", root.transform.position, Color.white, 1f);
            halo.SetParent(root.transform, false);
            halo.localPosition = Vector3.zero;

            Light light = Gfx.AddPointLight(root.transform, "Light", root.transform.position, Color.white, 6.5f, 1.7f);
            light.transform.localPosition = Vector3.zero;

            // The blast circle lies on the floor in world space, so it is not scaled with the body.
            Transform ring = FlatQuad(root.transform, "Blast ring", Textures.Ring, 1.6f);
            Transform fill = FlatQuad(root.transform, "Blast area", Textures.SoftDot, 1f);

            TrailRenderer trail = root.GetComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.minVertexDistance = 0.08f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            if (_trailMaterial == null)
            {
                _trailMaterial = MaterialFactory.GlowInstance(Textures.SoftDot, 1.3f);
            }
            trail.sharedMaterial = _trailMaterial;

            visual = new Visual
            {
                Root = root.transform,
                Body = body.transform,
                Trail = trail,
                BodyRenderer = body.GetComponent<Renderer>(),
                Halo = halo,
                HaloRenderer = halo.GetComponent<Renderer>(),
                Light = light,
                GrenadeParts = grenade,
                Spark = spark,
                BlastRing = ring,
                BlastRingRenderer = ring.GetComponent<Renderer>(),
                BlastFill = fill,
                BlastFillRenderer = fill.GetComponent<Renderer>(),
                FuseSound = _audio.CreateLoop(root.transform, _audio.Fuse)
            };
            return visual;
        }

        private static Transform FlatQuad(Transform parent, string name, Texture2D sprite, float intensity)
        {
            GameObject quad = Gfx.Make(name, Billboard.QuadMesh(), parent, MaterialFactory.GlowInstance(sprite, intensity));
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Gfx.NoShadows(quad);
            quad.SetActive(false);
            return quad.transform;
        }

        private void Release(uint id)
        {
            Visual visual = _active[id];
            _active.Remove(id);
            visual.FuseSound.Stop();
            visual.Stuck = false;
            visual.Root.gameObject.SetActive(false);
            _free.Push(visual);
        }
    }
}
