using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Makes a button feel alive: it grows a little and brightens under the pointer, sinks when pressed and plays the
    /// interface sounds. It works on unscaled time, so it also animates while the game is paused.
    /// </summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        private Image _background;
        private Selectable _selectable;
        private Color _normal;
        private Color _hover;
        private Color _pressed;
        private bool _over;
        private bool _down;
        private float _blend;
        private float _scale = 1f;

        /// <summary>Called when the pointer enters or a button is clicked (set once by the game for sound).</summary>
        public static System.Action Hovered;

        public static System.Action Clicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Hovered = null;
            Clicked = null;
        }

        /// <summary>Sets the three colours of the background and starts listening to the pointer.</summary>
        public void Setup(Image background, Selectable selectable, Color normal, Color hover, Color pressed)
        {
            _background = background;
            _selectable = selectable;
            _normal = normal;
            _hover = hover;
            _pressed = pressed;
            _background.color = normal;
        }

        public void Recolor(Color normal, Color hover, Color pressed)
        {
            _normal = normal;
            _hover = hover;
            _pressed = pressed;
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
            _scale = 1f;
            transform.localScale = Vector3.one;
            if (_background != null)
            {
                _background.color = Interactive ? _normal : DisabledColor();
            }
        }

        private void Update()
        {
            if (_background == null)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            bool interactive = Interactive;
            float targetBlend = interactive && _over ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, targetBlend, dt * 8f);
            float targetScale = !interactive ? 1f : _down ? 0.97f : _over ? 1.035f : 1f;
            _scale = Mathf.Lerp(_scale, targetScale, 1f - Mathf.Exp(-dt * 18f));
            transform.localScale = new Vector3(_scale, _scale, 1f);
            Color target = !interactive ? DisabledColor() : _down ? _pressed : Color.Lerp(_normal, _hover, _blend);
            _background.color = Color.Lerp(_background.color, target, 1f - Mathf.Exp(-dt * 20f));
        }

        private Color DisabledColor()
        {
            return new Color(_normal.r * 0.5f, _normal.g * 0.5f, _normal.b * 0.5f, _normal.a * 0.6f);
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
