using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The thick cartoon outline of the show's logo: copies of the graphic drawn behind it in a ring of directions,
    /// one ring per layer (so a navy-steel-navy edge is three layers), plus an optional drop shadow under them all.
    /// Unity's own <see cref="Outline"/> only draws four diagonal copies, which leaves gaps once the edge is wider
    /// than a couple of pixels.
    /// </summary>
    public sealed class UiStroke : BaseMeshEffect
    {
        private static readonly List<UIVertex> Source = new List<UIVertex>();
        private static readonly List<UIVertex> Output = new List<UIVertex>();

        private Color[] _colors = { new Color(0f, 0f, 0f, 1f) };
        private float[] _widths = { 2f };
        private int _directions = 8;
        private Color _dropColor;
        private Vector2 _drop;

        /// <summary>A single-colour edge <paramref name="width"/> units wide.</summary>
        public void Set(Color color, float width, int directions = 8)
        {
            _colors = new[] { color };
            _widths = new[] { width };
            _directions = Mathf.Max(4, directions);
            Dirty();
        }

        /// <summary>
        /// Several edges, innermost first: <paramref name="widths"/> are distances from the glyph, each larger than the
        /// one before.
        /// </summary>
        public void SetLayers(Color[] colors, float[] widths, int directions)
        {
            _colors = colors;
            _widths = widths;
            _directions = Mathf.Max(4, directions);
            Dirty();
        }

        /// <summary>A shadow of the outermost edge, shifted by <paramref name="offset"/>.</summary>
        public void SetDrop(Color color, Vector2 offset)
        {
            _dropColor = color;
            _drop = offset;
            Dirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
            {
                return;
            }
            Source.Clear();
            Output.Clear();
            vh.GetUIVertexStream(Source);
            int layers = Mathf.Min(_colors.Length, _widths.Length);
            float outer = layers > 0 ? _widths[layers - 1] : 0f;
            if (_dropColor.a > 0f)
            {
                Ring(_dropColor, outer, _drop);
            }
            for (int layer = layers - 1; layer >= 0; layer--)
            {
                Ring(_colors[layer], _widths[layer], Vector2.zero);
            }
            Output.AddRange(Source);
            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
        }

        private void Ring(Color color, float width, Vector2 shift)
        {
            for (int d = 0; d < _directions; d++)
            {
                float angle = d * Mathf.PI * 2f / _directions;
                var offset = new Vector3(Mathf.Cos(angle) * width + shift.x, Mathf.Sin(angle) * width + shift.y, 0f);
                for (int i = 0; i < Source.Count; i++)
                {
                    UIVertex vertex = Source[i];
                    vertex.position += offset;
                    Color32 own = vertex.color;
                    vertex.color = new Color(color.r, color.g, color.b, color.a * own.a / 255f);
                    Output.Add(vertex);
                }
            }
        }

        private void Dirty()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }
    }
}
