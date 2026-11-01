using System.Collections.Generic;
using Bajoterra.Protocol;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Draws the web traps Arachnet spins on the floor, as the server lists them: a pale, glinting web that unfurls
    /// when it lands and fades as its time runs out. Webs of your own team are tinted with its colour so you know
    /// they are safe to walk on; an enemy web is plain silk (hard to spot on purpose, but not invisible).
    /// </summary>
    public sealed class WebViews
    {
        private const float UnfurlTicks = 12f;
        private const float FadeTicks = 90f;

        private static Texture2D _texture;

        private sealed class Trap
        {
            public Transform Root;
            public Renderer Renderer;
            public uint Id;
            public int Stamp;
            public float Spin;
        }

        private readonly Transform _parent;
        private readonly int _yourTeam;
        private readonly System.Func<int, int> _teamOf;
        private readonly Dictionary<uint, Trap> _active = new Dictionary<uint, Trap>();
        private readonly Stack<Trap> _free = new Stack<Trap>();
        private readonly List<uint> _stale = new List<uint>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private int _stamp;
        private float _time;

        public WebViews(Transform parent, int yourTeam, System.Func<int, int> teamOf)
        {
            _parent = new GameObject("Webs").transform;
            _parent.SetParent(parent, false);
            _yourTeam = yourTeam;
            _teamOf = teamOf;
        }

        /// <summary>A spider web drawn in code: spokes from the middle and a spiral of silk across them.</summary>
        public static Texture2D Texture
        {
            get { return _texture != null ? _texture : _texture = Bake(); }
        }

        /// <summary>How many webs are drawn right now (tests).</summary>
        public int Count
        {
            get { return _active.Count; }
        }

        public void Apply(Snapshot latest, float deltaTime)
        {
            _stamp++;
            _time += deltaTime;
            int count = latest == null ? 0 : latest.Webs.Count;
            for (int i = 0; i < count; i++)
            {
                WebState w = latest.Webs[i];
                if (!_active.TryGetValue(w.Id, out Trap trap))
                {
                    trap = Acquire();
                    trap.Id = w.Id;
                    trap.Spin = (w.Id * 47) % 360;
                    _active[w.Id] = trap;
                }
                trap.Stamp = _stamp;
                float age = w.LifeTicks - (float)w.TicksLeft;
                float unfurl = Mathf.Clamp01(age / UnfurlTicks);
                float fade = Mathf.Clamp01(w.TicksLeft / FadeTicks);
                float ease = 1f - (1f - unfurl) * (1f - unfurl);
                trap.Root.position = new Vector3(w.X, w.Y + 0.04f, w.Z);
                trap.Root.localScale = Vector3.one * (w.Radius * 2.3f * (0.3f + 0.7f * ease));
                trap.Root.rotation = Quaternion.Euler(90f, trap.Spin, 0f);
                bool own = _teamOf(w.Owner) == _yourTeam;
                Color silk = own ? Color.Lerp(Color.white, Palette.TeamOwn, 0.55f) : new Color(0.95f, 0.95f, 0.88f);
                float glint = 0.8f + 0.2f * Mathf.Sin(_time * 2.4f + w.Id);
                _block.SetColor("_Color", new Color(silk.r, silk.g, silk.b, (own ? 0.55f : 0.7f) * fade * glint));
                trap.Renderer.SetPropertyBlock(_block);
            }

            _stale.Clear();
            foreach (KeyValuePair<uint, Trap> entry in _active)
            {
                if (entry.Value.Stamp != _stamp)
                {
                    _stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                Trap trap = _active[_stale[i]];
                _active.Remove(_stale[i]);
                trap.Root.gameObject.SetActive(false);
                _free.Push(trap);
            }
        }

        public void Destroy()
        {
            if (_parent != null)
            {
                Object.Destroy(_parent.gameObject);
            }
        }

        private Trap Acquire()
        {
            if (_free.Count > 0)
            {
                Trap reused = _free.Pop();
                reused.Root.gameObject.SetActive(true);
                return reused;
            }
            GameObject quad = Gfx.Make("Web", Billboard.QuadMesh(), _parent, MaterialFactory.GlowInstance(Texture, 1.2f));
            Gfx.NoShadows(quad);
            return new Trap { Root = quad.transform, Renderer = quad.GetComponent<Renderer>() };
        }

        private static Texture2D Bake()
        {
            const int size = 256;
            const int spokes = 12;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Web", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Atan2(v, u);
                    // Spokes: thin lines at even angles, a little crooked.
                    float spoke = Mathf.Abs(Mathf.Sin((a + 0.05f * Mathf.Sin(r * 9f)) * spokes * 0.5f));
                    float spokeLine = Mathf.Clamp01(1f - spoke * r * size * 0.11f);
                    // The spiral: rings that sag between the spokes.
                    float sag = 0.025f * (1f - Mathf.Abs(Mathf.Cos(a * spokes * 0.5f)));
                    float ring = Mathf.Abs(Mathf.Sin((r + sag) * Mathf.PI * 11f));
                    float ringLine = Mathf.Clamp01(1f - ring * 7f) * (r > 0.08f ? 1f : 0f);
                    float edge = Mathf.Clamp01((1f - r) * 8f);
                    float alpha = Mathf.Clamp01(Mathf.Max(spokeLine, ringLine * 0.85f)) * edge;
                    byte value = (byte)(255f * alpha);
                    pixels[y * size + x] = new Color32(value, value, value, value);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
