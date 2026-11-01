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
    /// Draws the slugs in flight. Each server projectile id gets the 3D model of its slug (<see cref="SlugModels"/>)
    /// leaning into its flight with a little wobble, wrapped in a glow of its element's colour, with a streaming trail
    /// and a small light that colours the walls it passes; a slug without a model falls back to a round body with
    /// eyes. Sticky slugs stand up where they land. A Grenuke blinks faster and faster, marks its blast on the floor
    /// with a red circle and hisses, beeping as the fuse burns down. A Phosphoro is the loudest thing in the cavern
    /// on purpose: it glows white in flight, and once stuck it charges up, its light swelling and pulsing faster
    /// with a rising whine, so everybody has a fair warning to look away before it flashes. Visuals are pooled, so a
    /// busy fight allocates nothing per frame.
    /// </summary>
    public sealed class ProjectileViews
    {
        private const int MaxLights = 10;
        /// <summary>How far a flying slug leans forwards, head first, from standing (degrees).</summary>
        private const float FlightLean = 55f;
        private static readonly Color GrenadeBody = new Color(0.95f, 0.26f, 0.1f);
        private static readonly Color Spark = new Color(1f, 0.9f, 0.45f);
        private static readonly Color FlashGlow = new Color(0.82f, 1f, 1f);

        private sealed class Visual
        {
            public Transform Root;
            public Transform Body;
            public Transform Ball;
            public TrailRenderer Trail;
            public Renderer BodyRenderer;
            public Transform Halo;
            public Renderer HaloRenderer;
            public Light Light;
            public GameObject GrenadeParts;
            public Transform Spark;
            public Transform Flare;
            public Renderer FlareRenderer;
            public Billboard FlareSpin;
            public Transform BlastRing;
            public Renderer BlastRingRenderer;
            public Transform BlastFill;
            public Renderer BlastFillRenderer;
            public AudioSource FuseSound;
            public readonly Dictionary<int, Transform> Models = new Dictionary<int, Transform>();
            public Transform Model;
            /// <summary>A fusion shot's second slug: a glowing orb that circles the first one.</summary>
            public Transform PartnerOrb;
            public Renderer PartnerOrbRenderer;
            public bool Fused;
            /// <summary>How much bigger than usual it flies (megamorphosis), 1 normally.</summary>
            public float Giant = 1f;
            public int Stamp;
            public bool Sticky;
            public bool Flash;
            public bool Stuck;
            public int FuseTicks;
            public float BlastRadius;
            public float NextBeep;
            public float ModelHeight;
            public float Phase;
            public Quaternion Facing = Quaternion.identity;
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

        /// <summary>How many slugs are drawn right now (tests).</summary>
        public int ActiveCount
        {
            get { return _active.Count; }
        }

        /// <summary>True when every slug drawn right now uses its 3D model (tests).</summary>
        public bool AllModelled
        {
            get
            {
                foreach (KeyValuePair<uint, Visual> entry in _active)
                {
                    if (entry.Value.Model == null)
                    {
                        return false;
                    }
                }
                return true;
            }
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
                // A stuck slug keeps the facing it landed with.
                if (!p.Stuck && p.Velocity.X * p.Velocity.X + p.Velocity.Z * p.Velocity.Z + p.Velocity.Y * p.Velocity.Y > 1e-4f)
                {
                    visual.Body.localRotation = Quaternion.LookRotation(p.Velocity.ToUnity());
                    Vector3 flat = new Vector3(p.Velocity.X, 0f, p.Velocity.Z);
                    if (flat.sqrMagnitude > 1e-4f)
                    {
                        visual.Facing = Quaternion.LookRotation(flat);
                    }
                }
                visual.Ball.localScale = Vector3.one * (size * pulse);
                PoseModel(visual, p, time);
                float halo = visual.Model != null ? 1.9f : 2.6f;
                visual.Halo.localScale = Vector3.one * (size * halo * pulse);
                if (visual.Fused)
                {
                    float spin = time * 14f + visual.Phase;
                    float orbit = size * 1.1f + 0.2f;
                    visual.PartnerOrb.localPosition = new Vector3(Mathf.Cos(spin) * orbit, Mathf.Sin(spin * 1.3f) * orbit * 0.5f,
                        Mathf.Sin(spin) * orbit);
                    visual.PartnerOrb.localScale = Vector3.one * (size * 1.5f * pulse);
                }
                bool lit = lights < MaxLights || p.Stuck || visual.Flash;
                visual.Light.enabled = lit;
                if (lit)
                {
                    lights++;
                }
                if (visual.Flash)
                {
                    AnimateFlash(visual, p, size, time);
                }
                else if (visual.Sticky)
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
            float own = _content.Slug(p.SlugIndex).ProjectileRadius;
            return (p.Radius > own ? p.Radius : own) * 2.4f;
        }

        /// <summary>
        /// In flight the slug leans head first into its path, wobbling and stretched by the speed; once stuck it
        /// stands up on its foot, facing the way it came.
        /// </summary>
        private void PoseModel(Visual visual, ProjectileFrame p, float time)
        {
            Transform model = visual.Model;
            if (model == null)
            {
                return;
            }
            float h = visual.ModelHeight;
            if (p.Stuck)
            {
                model.rotation = visual.Facing;
                model.localPosition = Vector3.zero;
                float radius = _content.Slug(p.SlugIndex).ProjectileRadius;
                Vector3 at = visual.Root.position;
                float below = p.StuckTo >= 0 ? 0f : _map.SightDistance(new Vec3(at.x, at.y, at.z), new Vec3(0f, -1f, 0f), 3f, 0f);
                if (below > radius + 0.25f)
                {
                    // Stopped in mid-air by its proximity fuse: it hovers, bobbing, facing whoever it caught.
                    model.position = at + Vector3.up * (0.06f * Mathf.Sin(time * 3.2f + visual.Phase));
                }
                else
                {
                    // The server keeps the centre of the slug's ball one radius off the surface: put the foot there.
                    model.position = at + Vector3.up * (h * 0.5f - radius);
                }
                float breathe = 1f + 0.05f * Mathf.Sin(time * 6f + visual.Phase);
                model.localScale = new Vector3(h * breathe, h / breathe, h * breathe);
                return;
            }
            float wobble = Mathf.Sin(time * 13f + visual.Phase) * 9f;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.Euler(FlightLean, 0f, wobble);
            model.localScale = new Vector3(h * 0.9f, h * 1.12f, h * 0.9f);
        }

        private void Configure(Visual visual, ProjectileFrame p)
        {
            SlugInfo info = _content.Slug(p.SlugIndex);
            bool flash = info.FlashRadius > 0f;
            Color color = flash ? FlashGlow : Palette.Element(_content.ElementName((int)info.Element));
            // Slugs that help allies glow whiter, so they read differently from attacks at a glance.
            if (info.Targeting != Targeting.Enemies)
            {
                color = Color.Lerp(color, Color.white, 0.5f);
            }
            visual.Sticky = info.Sticky;
            visual.Flash = flash;
            visual.Stuck = false;
            visual.FuseTicks = (int)Mathf.Max(1, info.FuseTicks);
            visual.BlastRadius = info.BlastRadius;
            visual.Color = color;
            visual.Phase = p.Id * 1.7f;
            visual.Facing = Quaternion.identity;
            // A megamorphed slinger's slugs fly in their giant form.
            visual.Giant = p.Radius > info.ProjectileRadius + 1e-3f ? p.Radius / info.ProjectileRadius : 1f;
            visual.ModelHeight = info.ProjectileRadius * 2.8f * visual.Giant;
            ShowModel(visual, (int)p.SlugIndex, info.Id);
            bool modelled = visual.Model != null;
            visual.Ball.gameObject.SetActive(!modelled);
            visual.BodyRenderer.sharedMaterial = info.Sticky && !flash
                ? MaterialFactory.Emissive(GrenadeBody, 0.75f, 0.75f)
                : MaterialFactory.Emissive(color, 0.9f, 0.7f);
            SetColor(visual.HaloRenderer, new Color(color.r, color.g, color.b, modelled ? 0.28f : 0.4f));
            visual.Light.color = color;
            visual.Light.intensity = flash ? 3.2f : 1.7f;
            visual.Light.range = flash ? 10f : 6.5f;
            float width = Mathf.Max(0.16f, info.ProjectileRadius * visual.Giant * (flash ? 3.4f : 2.6f));
            visual.Trail.widthMultiplier = width;
            visual.Trail.startColor = new Color(color.r, color.g, color.b, 0.9f);
            visual.Trail.endColor = new Color(color.r, color.g, color.b, 0f);
            visual.Fused = p.PartnerSlug >= 0;
            visual.PartnerOrb.gameObject.SetActive(visual.Fused);
            if (visual.Fused)
            {
                // Two slugs in one: the second circles the first, and the trail runs from one colour into the other.
                SlugInfo partner = _content.Slug(p.PartnerSlug);
                Color second = Palette.Element(_content.ElementName((int)partner.Element));
                SetColor(visual.PartnerOrbRenderer, new Color(second.r, second.g, second.b, 0.85f));
                visual.Trail.widthMultiplier = width * 1.6f;
                visual.Trail.endColor = new Color(second.r, second.g, second.b, 0f);
                visual.Light.intensity *= 1.6f;
            }
            visual.Root.position = p.Position.ToUnity();
            visual.Trail.Clear();
            visual.Trail.emitting = true;
            visual.GrenadeParts.SetActive(info.Sticky && !flash && !modelled);
            visual.Flare.gameObject.SetActive(flash);
            visual.BlastRing.gameObject.SetActive(false);
            visual.BlastFill.gameObject.SetActive(false);
            if (info.Sticky)
            {
                // The fuse hisses (a Phosphoro hums) quietly in flight and loudly once stuck.
                visual.FuseSound.clip = flash ? _audio.FlashCharge : _audio.Fuse;
                visual.FuseSound.volume = flash ? 0.35f : 0.25f;
                visual.FuseSound.pitch = flash ? 0.8f : 1f;
                visual.FuseSound.Play();
            }
        }

        /// <summary>Shows this slug's model on a pooled visual (made the first time the visual carries that slug).</summary>
        private static void ShowModel(Visual visual, int slugIndex, string slugId)
        {
            if (visual.Model != null)
            {
                visual.Model.gameObject.SetActive(false);
            }
            if (!visual.Models.TryGetValue(slugIndex, out Transform model))
            {
                model = SlugModels.Create(slugId, visual.Body);
                visual.Models[slugIndex] = model;
            }
            visual.Model = model;
            if (model != null)
            {
                model.gameObject.SetActive(true);
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
            SetColor(visual.HaloRenderer, new Color(glow.r, glow.g, glow.b, on ? 0.75f : 0.35f));
            visual.Light.color = glow;
            visual.Light.intensity = on ? 3.2f : 1.4f;
            visual.Ball.localScale = Vector3.one * (size * (1f + 0.25f * progress) * (on ? 1.06f : 1f));
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
                SetColor(visual.BlastRingRenderer, new Color(Palette.Danger.r, Palette.Danger.g, Palette.Danger.b, alpha));
                SetColor(visual.BlastFillRenderer, new Color(Palette.Danger.r, Palette.Danger.g * 0.6f, Palette.Danger.b, alpha * 0.35f));
            }
        }

        /// <summary>
        /// A Phosphoro: a white star glints around it in flight; once stuck it charges up, the glow and its light
        /// swelling and pulsing faster and faster while the hum rises, up to the moment it flashes.
        /// </summary>
        private void AnimateFlash(Visual visual, ProjectileFrame p, float size, float time)
        {
            if (p.Stuck && !visual.Stuck)
            {
                visual.Stuck = true;
                visual.Trail.emitting = false;
                visual.FuseSound.volume = 0.7f;
                _audio.PlayAt(_audio.FuseBeep, visual.Root.position, 0.5f, 1.6f);
            }
            float progress = p.Stuck ? Mathf.Clamp01(1f - p.FuseTicks / (float)visual.FuseTicks) : 0f;
            float pulseHz = Mathf.Lerp(2f, 16f, progress * progress);
            float beat = 0.5f + 0.5f * Mathf.Sin(time * pulseHz * 2f * Mathf.PI);
            float swell = 1f + 2.6f * progress * progress;
            visual.Halo.localScale = Vector3.one * (size * (3.2f + beat * 0.8f) * swell);
            SetColor(visual.HaloRenderer, new Color(FlashGlow.r, FlashGlow.g, FlashGlow.b, 0.45f + 0.4f * beat * (0.4f + progress)));
            visual.Light.color = Color.Lerp(FlashGlow, Color.white, progress);
            visual.Light.intensity = Mathf.Lerp(2.6f, 9f, progress) * (0.65f + 0.35f * beat);
            visual.Light.range = Mathf.Lerp(9f, 18f, progress);
            visual.Flare.localScale = Vector3.one * (size * (2.4f + 2f * progress) * (0.8f + 0.4f * beat));
            visual.FlareSpin.Roll = time * (40f + 260f * progress) + visual.Phase * 30f;
            SetColor(visual.FlareRenderer, new Color(1f, 1f, 0.95f, 0.55f + 0.45f * beat));
            if (p.Stuck)
            {
                visual.FuseSound.pitch = Mathf.Lerp(0.8f, 2.1f, progress);
                visual.FuseSound.volume = Mathf.Lerp(0.6f, 1f, progress);
            }
        }

        private void SetColor(Renderer renderer, Color color)
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
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            // The stand-in for slugs without a model: a round body with two eyes looking where it flies.
            GameObject ball = Gfx.Primitive(PrimitiveType.Sphere, "Ball", body, null);
            Material eyeWhite = MaterialFactory.Surface(null, Color.white, 0.6f, 0f);
            Material pupil = MaterialFactory.Surface(null, new Color(0.04f, 0.04f, 0.06f), 0.8f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = Gfx.Primitive(PrimitiveType.Sphere, "Eye", ball.transform, eyeWhite);
                eye.transform.localPosition = new Vector3(side * 0.22f, 0.16f, 0.38f);
                eye.transform.localScale = Vector3.one * 0.3f;
                GameObject dot = Gfx.Primitive(PrimitiveType.Sphere, "Pupil", ball.transform, pupil);
                dot.transform.localPosition = new Vector3(side * 0.22f, 0.16f, 0.5f);
                dot.transform.localScale = Vector3.one * 0.15f;
            }

            // Grenade dressing for a sticky slug without a model: a dark band round the middle and a fuse on the
            // back whose tip burns.
            var grenade = new GameObject("Grenade");
            grenade.transform.SetParent(ball.transform, false);
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
            Gfx.NoShadows(ball);

            Transform halo = Gfx.Halo(root.transform, "Glow", root.transform.position, Color.white, 1f);
            halo.SetParent(root.transform, false);
            halo.localPosition = Vector3.zero;

            // Phosphoro's glint: a star of light that spins faster as it charges.
            Transform flare = Gfx.Halo(root.transform, "Flare", root.transform.position, Color.white, 1f, Textures.Flare);
            flare.SetParent(root.transform, false);
            flare.localPosition = Vector3.zero;
            flare.gameObject.SetActive(false);

            Transform orb = Gfx.Halo(root.transform, "Partner", root.transform.position, Color.white, 1f, Textures.Flare);
            orb.SetParent(root.transform, false);
            orb.localPosition = Vector3.zero;
            orb.gameObject.SetActive(false);

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
                Body = body,
                Ball = ball.transform,
                Trail = trail,
                BodyRenderer = ball.GetComponent<Renderer>(),
                Halo = halo,
                HaloRenderer = halo.GetComponent<Renderer>(),
                Light = light,
                GrenadeParts = grenade,
                Spark = spark,
                Flare = flare,
                FlareRenderer = flare.GetComponent<Renderer>(),
                FlareSpin = flare.GetComponent<Billboard>(),
                PartnerOrb = orb,
                PartnerOrbRenderer = orb.GetComponent<Renderer>(),
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
