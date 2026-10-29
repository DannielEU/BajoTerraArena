using UnityEngine;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The aim marker: four ticks and a dot, a ring around them that fills while the blaster waits for the next shot
    /// (the wait is long now, so the player must be able to see it), and an X that flashes when a slug hits an enemy
    /// (gold and wider for a headshot, red when that hit was the last one).
    /// </summary>
    public sealed class HudCrosshair
    {
        private readonly RectTransform _root;
        private readonly Image[] _ticks = new Image[4];
        private readonly Image _dot;
        private readonly Image _ring;
        private readonly Image _ringBack;
        private readonly Image[] _marker = new Image[4];
        private float _markerTime;
        private bool _markerKill;
        private bool _markerCritical;
        private float _readyPulse;
        private bool _wasReady = true;

        public HudCrosshair(RectTransform parent)
        {
            RectTransform root = UiKit.Rect("Crosshair", parent);
            _root = root;
            UiKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(120f, 120f), Vector2.zero);

            _ringBack = UiKit.Panel(root, "RingBack", new Color(0f, 0f, 0f, 0f));
            _ringBack.sprite = UiSprites.Ring(0.05f);
            Center(_ringBack.rectTransform, 84f);
            _ring = UiKit.Panel(root, "Ring", new Color(1f, 1f, 1f, 0.9f));
            _ring.sprite = UiSprites.Ring(0.05f);
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = 2;
            _ring.fillClockwise = true;
            Center(_ring.rectTransform, 84f);

            Vector2[] offsets = { new Vector2(-17f, 0f), new Vector2(17f, 0f), new Vector2(0f, 17f), new Vector2(0f, -17f) };
            Vector2[] sizes = { new Vector2(13f, 3f), new Vector2(13f, 3f), new Vector2(3f, 13f), new Vector2(3f, 13f) };
            for (int i = 0; i < 4; i++)
            {
                _ticks[i] = UiKit.Panel(root, "Tick " + i, Color.white);
                Center(_ticks[i].rectTransform, 0f);
                _ticks[i].rectTransform.sizeDelta = sizes[i];
                _ticks[i].rectTransform.anchoredPosition = offsets[i];
                UiKit.AddOutlineTo(_ticks[i], 1.2f);
            }
            _dot = UiKit.Panel(root, "Dot", Color.white);
            _dot.sprite = UiSprites.Circle();
            Center(_dot.rectTransform, 5f);

            for (int i = 0; i < 4; i++)
            {
                _marker[i] = UiKit.Panel(root, "Marker " + i, new Color(1f, 1f, 1f, 0f));
                Center(_marker[i].rectTransform, 0f);
                _marker[i].rectTransform.sizeDelta = new Vector2(20f, 4f);
                float angle = 45f + i * 90f;
                _marker[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                float rad = angle * Mathf.Deg2Rad;
                _marker[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 22f;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_root.gameObject.activeSelf != visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Flashes the hit marker; <paramref name="kill"/> makes it red and bigger.</summary>
        public void Hit(bool kill)
        {
            _markerTime = kill ? 0.45f : 0.22f;
            _markerKill = kill;
            _markerCritical = false;
        }

        /// <summary>Turns the current hit marker into a headshot one: gold, wider and longer.</summary>
        public void Critical()
        {
            _markerTime = Mathf.Max(_markerTime, 0.35f);
            _markerCritical = true;
        }

        /// <summary>Updates the ring and the marker; call once per frame.</summary>
        /// <param name="progress">How far the blaster is through its wait (1 = ready).</param>
        /// <param name="slotReady">Whether the selected slug can be fired (a locked one turns the cross red).</param>
        public void Refresh(float progress, bool slotReady, float deltaTime)
        {
            bool ready = progress >= 1f;
            if (ready && !_wasReady)
            {
                _readyPulse = 1f;
            }
            _wasReady = ready;
            _readyPulse = Mathf.MoveTowards(_readyPulse, 0f, deltaTime * 4f);

            Color cross = slotReady ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 0.35f, 0.3f, 0.95f);
            foreach (Image tick in _ticks)
            {
                tick.color = cross;
            }
            _dot.color = cross;

            _ring.fillAmount = ready ? 1f : progress;
            float ringAlpha = ready ? 0.0f + _readyPulse * 0.8f : 0.85f;
            _ring.color = new Color(1f, 0.85f, 0.4f, ringAlpha);
            _ringBack.color = new Color(1f, 1f, 1f, ready ? 0f : 0.18f);
            float scale = 1f + _readyPulse * 0.25f;
            _ring.rectTransform.localScale = new Vector3(scale, scale, 1f);

            if (_markerTime > 0f)
            {
                _markerTime -= deltaTime;
                float fade = Mathf.Clamp01(_markerTime / (_markerKill ? 0.45f : _markerCritical ? 0.35f : 0.22f));
                Color color = _markerKill ? new Color(1f, 0.25f, 0.2f, fade)
                    : _markerCritical ? Palette.WithAlpha(Palette.Critical, fade) : new Color(1f, 1f, 1f, fade);
                float length = _markerKill ? 30f : _markerCritical ? 28f : 20f;
                for (int i = 0; i < _marker.Length; i++)
                {
                    Image part = _marker[i];
                    part.color = color;
                    part.rectTransform.sizeDelta = new Vector2(length, _markerKill || _markerCritical ? 5f : 4f);
                    float rad = (45f + i * 90f) * Mathf.Deg2Rad;
                    part.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad))
                                                          * (_markerCritical ? 27f : 22f);
                }
            }
            else if (_marker[0].color.a > 0f)
            {
                foreach (Image part in _marker)
                {
                    part.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        private static void Center(RectTransform rect, float size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
