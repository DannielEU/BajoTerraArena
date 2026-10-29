using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// A vertical two-colour gradient over any graphic (the fire-coloured titles, the glossy metal plates). It
    /// multiplies the graphic's own colour, so a plate tinted white shows the gradient as it is and a dimmed one stays
    /// dim. The gradient spans the vertices of the mesh: the whole rect for an image, the glyphs for a text.
    /// </summary>
    public sealed class UiGradient : BaseMeshEffect
    {
        private Color _top = Color.white;
        private Color _bottom = Color.white;

        public Color Top
        {
            get { return _top; }
        }

        public Color Bottom
        {
            get { return _bottom; }
        }

        public void Set(Color top, Color bottom)
        {
            if (top == _top && bottom == _bottom)
            {
                return;
            }
            _top = top;
            _bottom = bottom;
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            int count = vh.currentVertCount;
            if (!IsActive() || count == 0)
            {
                return;
            }
            UIVertex vertex = default;
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.y);
                max = Mathf.Max(max, vertex.position.y);
            }
            float span = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Color tint = Color.Lerp(_bottom, _top, (vertex.position.y - min) / span);
                Color own = vertex.color;
                vertex.color = own * tint;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
