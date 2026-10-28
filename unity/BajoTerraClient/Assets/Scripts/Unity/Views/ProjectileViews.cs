using System.Collections.Generic;
using Bajoterra.Client.Content;
using Bajoterra.Client.Interpolation;
using Bajoterra.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the slugs in flight. Each server projectile id gets a little slug (a round body with eyes) wrapped in a
    /// glow of its element's colour, a streaming trail and a small light that colours the walls it passes. Visuals are
    /// pooled, so a busy fight allocates nothing per frame.
    /// </summary>
    public sealed class ProjectileViews
    {
        private const int MaxLights = 10;

        private sealed class Visual
        {
            public Transform Root;
            public Transform Body;
            public TrailRenderer Trail;
            public Renderer BodyRenderer;
            public Transform Halo;
            public Renderer HaloRenderer;
            public Light Light;
            public int Stamp;
        }

        private readonly Transform _parent;
        private readonly ContentCatalog _content;
        private readonly Dictionary<uint, Visual> _active = new Dictionary<uint, Visual>();
        private readonly Stack<Visual> _free = new Stack<Visual>();
        private readonly List<uint> _stale = new List<uint>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Material _trailMaterial;
        private int _stamp;

        public ProjectileViews(Transform parent, ContentCatalog content)
        {
            _parent = new GameObject("Projectiles").transform;
            _parent.SetParent(parent, false);
            _content = content;
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
                visual.Body.localRotation = Quaternion.LookRotation(
                    p.Velocity.X * p.Velocity.X + p.Velocity.Z * p.Velocity.Z + p.Velocity.Y * p.Velocity.Y > 1e-4f
                        ? p.Velocity.ToUnity() : Vector3.forward);
                visual.Body.localScale = Vector3.one * (size * pulse);
                visual.Halo.localScale = Vector3.one * (size * 2.6f * pulse);
                bool lit = lights < MaxLights;
                visual.Light.enabled = lit;
                if (lit)
                {
                    lights++;
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
            visual.BodyRenderer.sharedMaterial = MaterialFactory.Emissive(color, 0.9f, 0.7f);
            _block.SetColor("_Color", new Color(color.r, color.g, color.b, 0.4f));
            visual.HaloRenderer.SetPropertyBlock(_block);
            visual.Light.color = color;
            float width = Mathf.Max(0.16f, info.ProjectileRadius * 2.6f);
            visual.Trail.widthMultiplier = width;
            visual.Trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
            visual.Trail.endColor = new Color(color.r, color.g, color.b, 0f);
            visual.Root.position = p.Position.ToUnity();
            visual.Trail.Clear();
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
            Gfx.NoShadows(body);

            Transform halo = Gfx.Halo(root.transform, "Glow", root.transform.position, Color.white, 1f);
            halo.SetParent(root.transform, false);
            halo.localPosition = Vector3.zero;

            Light light = Gfx.AddPointLight(root.transform, "Light", root.transform.position, Color.white, 6.5f, 1.7f);
            light.transform.localPosition = Vector3.zero;

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
                Light = light
            };
            return visual;
        }

        private void Release(uint id)
        {
            Visual visual = _active[id];
            _active.Remove(id);
            visual.Root.gameObject.SetActive(false);
            _free.Push(visual);
        }
    }
}
