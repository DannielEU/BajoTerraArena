using System.Collections.Generic;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the walls raised during a round (Frostcrawler's ice walls) exactly where the server's boxes are: a
    /// block of glassy ice crowned with crystals that shoots up out of the floor, cracks after its first hit and sinks
    /// as it melts in its last seconds. The shatter, the frost and the sounds come from the events
    /// (<see cref="VfxPlayer"/>); this only keeps the walls themselves in step with the snapshots. Pooled: nothing is
    /// allocated per frame once a few walls have been seen.
    /// </summary>
    public sealed class BarrierViews
    {
        private const float RiseSeconds = 0.3f;
        private const float MeltTicks = 90f;
        private const int CrystalVariants = 4;
        private static readonly Color IceColor = new Color(0.62f, 0.9f, 1f);
        private static readonly Color CrackedColor = new Color(0.82f, 0.95f, 1f);

        private sealed class Visual
        {
            public Transform Root;
            public Transform Body;
            public Renderer Slab;
            public GameObject Cracks;
            public Light Light;
            public uint Id;
            public float Age;
            public int Stamp;
            public bool Cracked;
        }

        private readonly Transform _parent;
        private readonly Dictionary<uint, Visual> _active = new Dictionary<uint, Visual>();
        private readonly Stack<Visual> _free = new Stack<Visual>();
        private readonly List<uint> _stale = new List<uint>();
        private readonly Mesh[] _crystals = new Mesh[CrystalVariants];
        private readonly System.Random _random = new System.Random(11);
        private Material _ice;
        private Material _cracked;
        private Material _crystal;
        private Material _rim;
        private Material _crack;
        private int _stamp;

        public BarrierViews(Transform parent)
        {
            _parent = new GameObject("Ice walls").transform;
            _parent.SetParent(parent, false);
        }

        /// <summary>How many walls are drawn right now (tests).</summary>
        public int Count
        {
            get { return _active.Count; }
        }

        public void Apply(Snapshot latest, float deltaTime)
        {
            _stamp++;
            if (latest != null)
            {
                for (int i = 0; i < latest.Barriers.Count; i++)
                {
                    BarrierState b = latest.Barriers[i];
                    if (!_active.TryGetValue(b.Id, out Visual visual))
                    {
                        visual = Acquire();
                        visual.Id = b.Id;
                        visual.Age = 0f;
                        visual.Cracked = false;
                        visual.Slab.sharedMaterial = _ice;
                        visual.Cracks.SetActive(false);
                        _active[b.Id] = visual;
                    }
                    visual.Stamp = _stamp;
                    Place(visual, b, deltaTime);
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
                Visual visual = _active[_stale[i]];
                _active.Remove(_stale[i]);
                visual.Root.gameObject.SetActive(false);
                _free.Push(visual);
            }
        }

        public void Destroy()
        {
            if (_parent != null)
            {
                Object.Destroy(_parent.gameObject);
            }
            for (int i = 0; i < CrystalVariants; i++)
            {
                if (_crystals[i] != null)
                {
                    Object.Destroy(_crystals[i]);
                }
            }
        }

        private void Place(Visual visual, BarrierState b, float deltaTime)
        {
            visual.Age += deltaTime;
            float sizeX = b.MaxX - b.MinX;
            float sizeZ = b.MaxZ - b.MinZ;
            float height = b.MaxY - b.MinY;
            visual.Root.position = new Vector3((b.MinX + b.MaxX) * 0.5f, b.MinY, (b.MinZ + b.MaxZ) * 0.5f);
            // The model is built along x: turn it when the wall runs along z.
            bool alongZ = sizeZ > sizeX;
            visual.Root.rotation = Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);
            float length = alongZ ? sizeZ : sizeX;
            float thickness = alongZ ? sizeX : sizeZ;
            float rise = Mathf.Clamp01(visual.Age / RiseSeconds);
            rise = 1f - (1f - rise) * (1f - rise);
            float melt = Mathf.Clamp01(b.TicksLeft / MeltTicks);
            float sink = Mathf.Lerp(0.35f, 1f, melt);
            visual.Body.localScale = new Vector3(length / 3.2f * Mathf.Lerp(0.85f, 1f, melt), height / 2.2f * rise * sink,
                thickness / 0.5f * Mathf.Lerp(0.8f, 1f, melt));
            bool cracked = b.HitsLeft <= 1;
            if (cracked != visual.Cracked)
            {
                visual.Cracked = cracked;
                visual.Slab.sharedMaterial = cracked ? _cracked : _ice;
                visual.Cracks.SetActive(cracked);
            }
            visual.Light.intensity = 0.9f * rise * melt + 0.2f;
        }

        private Visual Acquire()
        {
            if (_free.Count > 0)
            {
                Visual reused = _free.Pop();
                reused.Root.gameObject.SetActive(true);
                return reused;
            }
            EnsureMaterials();
            var root = new GameObject("Ice wall").transform;
            root.SetParent(_parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);

            // Built for the default wall (3.2 m long along x, 2.2 m tall, 0.5 m thick); scaled to the real one.
            GameObject slab = Gfx.Primitive(PrimitiveType.Cube, "Slab", body, _ice);
            slab.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            slab.transform.localScale = new Vector3(3.1f, 1.9f, 0.46f);
            GameObject rim = Gfx.Primitive(PrimitiveType.Cube, "Frost", body, _rim);
            rim.transform.localPosition = slab.transform.localPosition;
            rim.transform.localScale = new Vector3(3.2f, 1.96f, 0.52f);
            Gfx.NoShadows(rim);

            // A crown of crystals of different heights along the top and leaning out of the sides.
            for (int i = 0; i < 11; i++)
            {
                float x = -1.5f + i * 0.3f + (float)_random.NextDouble() * 0.1f;
                float tall = 0.45f + (float)_random.NextDouble() * 0.55f;
                Mesh mesh = Crystal(i % CrystalVariants);
                GameObject top = Gfx.Make("Crystal", mesh, body, _crystal);
                top.transform.localPosition = new Vector3(x, 1.75f, ((float)_random.NextDouble() - 0.5f) * 0.25f);
                top.transform.localRotation = Quaternion.Euler(((float)_random.NextDouble() - 0.5f) * 30f,
                    (float)_random.NextDouble() * 360f, ((float)_random.NextDouble() - 0.5f) * 30f);
                top.transform.localScale = new Vector3(1f, tall, 1f);
                Gfx.NoShadows(top);
                if (i % 3 == 0)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject foot = Gfx.Make("Crystal", Crystal((i + 1) % CrystalVariants), body, _crystal);
                        foot.transform.localPosition = new Vector3(x + 0.1f, 0f, side * 0.24f);
                        foot.transform.localRotation = Quaternion.Euler(side * -32f, (float)_random.NextDouble() * 360f, 0f);
                        foot.transform.localScale = new Vector3(0.8f, 0.5f + (float)_random.NextDouble() * 0.3f, 0.8f);
                        Gfx.NoShadows(foot);
                    }
                }
            }

            // Cracks: white jagged lines on both faces, shown after the first hit.
            var cracks = new GameObject("Cracks");
            cracks.transform.SetParent(body, false);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = -0.6f;
                float y = 0.2f;
                for (int k = 0; k < 6; k++)
                {
                    float nx = x + 0.22f + (float)_random.NextDouble() * 0.2f;
                    float ny = y + 0.22f + (float)_random.NextDouble() * 0.12f;
                    GameObject line = Gfx.Primitive(PrimitiveType.Cube, "Crack", cracks.transform, _crack);
                    Vector3 from = new Vector3(x, y, side * 0.235f);
                    Vector3 to = new Vector3(nx, ny, side * 0.235f);
                    line.transform.localPosition = (from + to) * 0.5f;
                    line.transform.localRotation = Quaternion.FromToRotation(Vector3.right, to - from);
                    line.transform.localScale = new Vector3((to - from).magnitude, 0.025f, 0.02f);
                    Gfx.NoShadows(line);
                    x = nx;
                    y = ny;
                }
            }
            cracks.SetActive(false);

            Light light = Gfx.AddPointLight(root, "Light", root.position, new Color(0.5f, 0.9f, 1f), 4.5f, 0.9f);
            light.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            return new Visual
            {
                Root = root,
                Body = body,
                Slab = slab.GetComponent<Renderer>(),
                Cracks = cracks,
                Light = light
            };
        }

        private Mesh Crystal(int variant)
        {
            if (_crystals[variant] == null)
            {
                var lean = new Vector2(((float)_random.NextDouble() - 0.5f) * 0.12f, ((float)_random.NextDouble() - 0.5f) * 0.12f);
                _crystals[variant] = MeshKit.Crystal(0.13f + variant * 0.03f, 0.75f, 5 + variant % 2, lean);
            }
            return _crystals[variant];
        }

        private void EnsureMaterials()
        {
            if (_ice != null)
            {
                return;
            }
            _ice = MaterialFactory.Emissive(IceColor, 0.35f, 0.95f);
            _cracked = MaterialFactory.Emissive(CrackedColor, 0.45f, 0.7f);
            _crystal = MaterialFactory.Emissive(new Color(0.75f, 0.97f, 1f), 0.6f, 0.95f);
            _rim = MaterialFactory.Fresnel(new Color(0.75f, 0.97f, 1f, 0.9f), 1.8f, 2.2f, 0.06f);
            _crack = MaterialFactory.Unlit(new Color(0.95f, 1f, 1f));
        }
    }
}
