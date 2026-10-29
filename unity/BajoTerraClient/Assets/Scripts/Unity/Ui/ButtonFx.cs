using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Makes a cartoon button feel alive: it grows a little and brightens under the pointer, its face sinks into the
    /// lip when pressed, it greys out when disabled, and it plays the interface sounds. It works on unscaled time, so
    /// it also animates while the game is paused.
    /// </summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        private const float Border = 3f;

        private Selectable _selectable;
        private RectTransform _face;
        private UiGradient _shade;
        private Image _lip;
        private Image _rim;
        private Text _caption;
        private Color _top = Color.white;
        private Color _bottom = Color.white;
        private Color _text = Color.white;
        private bool _raised = true;
        private bool _over;
        private bool _down;
        private float _blend;
        private float _press;
        private float _scale = 1f;
        private float _lastSink = -1f;

        /// <summary>How much of the lip shows under a raised face (less on small buttons).</summary>
        private float _lipDepth = 7f;

        /// <summary>Called when the pointer enters or a button is clicked (set once by the game for sound).</summary>
        public static System.Action Hovered;

        public static System.Action Clicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Hovered = null;
            Clicked = null;
        }

        /// <summary>Wires the parts made by <see cref="UiKit.MakeButton(Transform,string,string,Vector2,UnityEngine.Events.UnityAction,ButtonStyle,int)"/>.</summary>
        public void Setup(Selectable selectable, RectTransform face, UiGradient shade, Image lip, Image rim, Text caption)
        {
            _selectable = selectable;
            _face = face;
            _shade = shade;
            _lip = lip;
            _rim = rim;
            _caption = caption;
            _lipDepth = Mathf.Clamp(((RectTransform)transform).sizeDelta.y * 0.09f, 3.5f, 7f);
            caption.rectTransform.SetParent(face, false);
            UiKit.Inset(caption.rectTransform, 10f, 3f, 10f, 1f);
        }

        /// <summary>
        /// Sets the face gradient, the lip colour and the caption colour; <paramref name="raised"/> is false for flat
        /// buttons with no lip.
        /// </summary>
        public void Recolor(Color top, Color bottom, Color lip, Color text, bool raised)
        {
            _top = top;
            _bottom = bottom;
            _text = text;
            _raised = raised;
            _lip.color = lip;
            _lip.enabled = raised;
            _rim.color = raised ? new Color(1f, 1f, 1f, 0.3f) : new Color(0.45f, 1f, 0.95f, 0.8f);
            _lastSink = -1f;
            Apply(Interactive);
        }

        private bool Interactive
        {
            get { return _selectable == null || _selectable.interactable; }
        }

        private void OnEnable()
        {
            _over = false;
            _down = false;
            _blend = 0f;
            _press = 0f;
            _scale = 1f;
            transform.localScale = Vector3.one;
            if (_face != null)
            {
                _lastSink = -1f;
                Apply(Interactive);
            }
        }

        private void Update()
        {
            if (_face == null)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            bool interactive = Interactive;
            _blend = Mathf.MoveTowards(_blend, interactive && _over ? 1f : 0f, dt * 8f);
            _press = Mathf.MoveTowards(_press, interactive && _down ? 1f : 0f, dt * 30f);
            float targetScale = interactive && _over && !_down ? 1.04f : 1f;
            _scale = Mathf.Lerp(_scale, targetScale, 1f - Mathf.Exp(-dt * 18f));
            transform.localScale = new Vector3(_scale, _scale, 1f);
            Apply(interactive);
        }

        private void Apply(bool interactive)
        {
            Color top = UiKit.Lighter(_top, 0.28f * _blend);
            Color bottom = UiKit.Lighter(_bottom, 0.2f * _blend);
            if (!interactive)
            {
                top = Greyed(top);
                bottom = Greyed(bottom);
            }
            _shade.Set(top, bottom);
            _caption.color = interactive ? _text : new Color(_text.r * 0.7f, _text.g * 0.7f, _text.b * 0.7f, _text.a * 0.8f);

            // The face rests on the lip and sinks into it while pressed; a flat button has no lip.
            float sink = _raised ? Mathf.Lerp(_lipDepth, 1.5f, _press) : 0f;
            if (!Mathf.Approximately(sink, _lastSink))
            {
                _lastSink = sink;
                float lift = _raised ? _lipDepth - sink : 0f;
                UiKit.Inset(_face, Border, Border + sink, Border, Border + lift);
            }
        }

        private static Color Greyed(Color color)
        {
            float grey = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
            Color flat = Color.Lerp(color, new Color(grey, grey, grey, color.a), 0.75f) * 0.62f;
            flat.a = color.a;
            return flat;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Interactive && !_over)
            {
                Hovered?.Invoke();
            }
            _over = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            _down = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactive)
            {
                _down = true;
                Clicked?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _down = false;
        }
    }
}
