using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// A chunky progress bar with cut corners and a navy edge: a dark track, a pale "ghost" that trails behind when the
    /// value drops (so a hit is visible as a chunk draining away), the glossy coloured fill on top, and an optional
    /// number inside. The fill is sized by anchors, so it works with the sliced plate sprite.
    /// </summary>
    public sealed class UiBar
    {
        private readonly Image _track;
        private readonly Image _ghost;
        private readonly Image _fill;
        private readonly Text _text;
        private float _shown = -1f;
        private float _trail = -1f;
        private string _lastText;

        public UiBar(Transform parent, string name, Vector2 size, Color fill, int radius = 8, int fontSize = 0)
        {
            _track = UiKit.Card(parent, name, new Color(0.02f, 0.06f, 0.12f, 0.88f), radius);
            _track.rectTransform.sizeDelta = size;
            _ghost = UiKit.Card(_track.transform, "Ghost", new Color(1f, 1f, 1f, 0.55f), radius);
            _fill = UiKit.Card(_track.transform, "Fill", fill, radius);
            UiKit.Gradient(_fill, new Color(1f, 1f, 1f, 1f), new Color(0.62f, 0.62f, 0.66f, 1f));
            UiKit.StretchFraction(_ghost.rectTransform, 1f);
            UiKit.StretchFraction(_fill.rectTransform, 1f);
            if (size.y >= 12f)
            {
                // A glossy band over the top half, like the show's plastic-and-metal gadgets.
                Image gloss = UiKit.Card(_track.transform, "Gloss", Color.white, radius);
                UiKit.Gradient(gloss, new Color(1f, 1f, 1f, 0.3f), new Color(1f, 1f, 1f, 0.02f));
                UiKit.Inset(gloss.rectTransform, 2f, 0f, 2f, 2f);
                gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            }
            UiKit.Edge(_track.transform, UiKit.Ink, radius, size.y >= 16f ? 3 : 2);
            if (fontSize > 0)
            {
                _text = UiKit.Label(_track.transform, "Value", string.Empty, fontSize, TextAnchor.MiddleCenter,
                    UiKit.TextColor);
                UiKit.Strong(_text);
                UiKit.Stretch(_text.rectTransform);
                UiKit.AddShadow(_text, 1.5f);
            }
        }

        public RectTransform Rect
        {
            get { return _track.rectTransform; }
        }

        public void SetFillColor(Color color)
        {
            _fill.color = color;
        }

        public void SetGhostColor(Color color)
        {
            _ghost.color = color;
        }

        /// <summary>Moves the bar towards <paramref name="fraction"/> (0 to 1); call every frame.</summary>
        public void Set(float fraction, float deltaTime)
        {
            fraction = Mathf.Clamp01(fraction);
            if (_shown < 0f)
            {
                _shown = fraction;
                _trail = fraction;
            }
            // The fill follows quickly, the ghost drains slowly after a drop and rises together with the fill.
            _shown = Mathf.MoveTowards(_shown, fraction, deltaTime * 2.5f);
            if (fraction >= _trail)
            {
                _trail = fraction;
            }
            else
            {
                _trail = Mathf.MoveTowards(_trail, fraction, deltaTime * 0.45f);
            }
            UiKit.StretchFraction(_fill.rectTransform, _shown);
            UiKit.StretchFraction(_ghost.rectTransform, Mathf.Max(_trail, _shown));
        }

        public void SetText(string text)
        {
            if (_text != null && text != _lastText)
            {
                _lastText = text;
                _text.text = text;
            }
        }
    }
}
