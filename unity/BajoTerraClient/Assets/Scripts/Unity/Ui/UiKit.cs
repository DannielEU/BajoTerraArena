using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if BAJOTERRA_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Bajoterra.Unity
{
    /// <summary>The look of a button.</summary>
    public enum ButtonStyle
    {
        /// <summary>The main action of a screen: bright accent colour.</summary>
        Primary,

        /// <summary>Everything else: dark with a light edge.</summary>
        Secondary,

        /// <summary>Leaving, quitting, abandoning.</summary>
        Danger,

        /// <summary>Confirming something positive (ready).</summary>
        Success,

        /// <summary>Text with a faint background, for tabs and small actions.</summary>
        Ghost
    }

    /// <summary>
    /// Helpers to build uGUI widgets in code, so the project needs no prefabs, sprites or font assets. All sizes are
    /// in a 1920 x 1080 reference space (the canvas scaler adapts it to the real resolution). The colours are the
    /// game's interface palette: deep blue-black panels, cyan accents, gold highlights.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.027f, 0.04f, 0.07f, 1f);
        public static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.115f, 0.9f);
        public static readonly Color PanelRaised = new Color(0.085f, 0.115f, 0.18f, 0.95f);
        public static readonly Color EdgeColor = new Color(0.25f, 0.36f, 0.55f, 0.55f);
        public static readonly Color AccentColor = new Color(0.22f, 0.72f, 1f, 1f);
        public static readonly Color AccentDeep = new Color(0.08f, 0.42f, 0.78f, 1f);
        public static readonly Color GoldColor = new Color(1f, 0.78f, 0.28f, 1f);
        public static readonly Color DangerColor = new Color(1f, 0.36f, 0.3f, 1f);
        public static readonly Color SuccessColor = new Color(0.3f, 0.88f, 0.5f, 1f);
        public static readonly Color ButtonColor = new Color(0.12f, 0.16f, 0.25f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.96f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.6f, 0.69f, 0.83f, 1f);

        private static Font _font;

        /// <summary>The font used by every text: Unity's built-in one.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null)
                    {
                        _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                    }
                }
                return _font;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _font = null;
        }

        // ------------------------------------------------------------------------------------------ canvas

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Makes sure there is an event system with an input module that matches the active input backend.</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null)
            {
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(parent, false);
#if BAJOTERRA_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------------------------------ layout

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchors a rect to a point of its parent with a fixed size (the pivot follows the anchor).</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>Stretches a rect over a fraction of its parent along x (for bars) and the full height.</summary>
        public static void StretchFraction(RectTransform rect, float fraction)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------------------------------ basic widgets

        /// <summary>A flat, square-cornered rectangle (backgrounds, overlays).</summary>
        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A rounded rectangle (cards, bars, chips).</summary>
        public static Image Card(Transform parent, string name, Color color, int radius = 14)
        {
            Image image = Panel(parent, name, color);
            image.sprite = UiSprites.Rounded(radius);
            image.type = Image.Type.Sliced;
            return image;
        }

        /// <summary>A thin rounded outline drawn over a card.</summary>
        public static Image Edge(Transform parent, Color color, int radius = 14, int thickness = 2)
        {
            Image image = Panel(parent, "Edge", color);
            image.sprite = UiSprites.Outline(radius, thickness);
            image.type = Image.Type.Sliced;
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>A card with a faint outline and a soft sheen, the base of most panels.</summary>
        public static Image Window(Transform parent, string name, Color color, int radius = 18)
        {
            Image card = Card(parent, name, color, radius);
            Edge(card.transform, EdgeColor, radius, 2);
            Image sheen = Panel(card.transform, "Sheen", new Color(1f, 1f, 1f, 0.035f));
            sheen.sprite = UiSprites.Gradient();
            Stretch(sheen.rectTransform, 3f);
            return card;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor,
            Color color)
        {
            RectTransform rect = Rect(name, parent);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>Text with a dark drop shadow so it reads over any background.</summary>
        public static Text Shadowed(Transform parent, string name, string text, int size, TextAnchor anchor,
            Color color, bool bold = false)
        {
            Text label = Label(parent, name, text, size, anchor, color);
            if (bold)
            {
                label.fontStyle = FontStyle.Bold;
            }
            AddShadow(label, 2f);
            return label;
        }

        public static void AddShadow(Text text, float distance)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>A dark outline around any graphic (the crosshair ticks, small markers).</summary>
        public static void AddOutlineTo(Graphic graphic, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(distance, -distance);
        }

        public static void AddOutline(Text text, float distance)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(distance, -distance);
        }

        // ------------------------------------------------------------------------------------------ buttons

        public static Button MakeButton(Transform parent, string name, string caption, Vector2 size,
            UnityAction onClick)
        {
            return MakeButton(parent, name, caption, size, onClick, ButtonStyle.Secondary);
        }

        public static Button MakeButton(Transform parent, string name, string caption, Vector2 size,
            UnityAction onClick, ButtonStyle style, int fontSize = 26)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            Image background = rect.gameObject.AddComponent<Image>();
            background.sprite = UiSprites.Rounded(12);
            background.type = Image.Type.Sliced;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0f;
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            StyleColors(style, out Color normal, out Color hover, out Color pressed, out Color text);
            var fx = rect.gameObject.AddComponent<ButtonFx>();
            fx.Setup(background, button, normal, hover, pressed);
            if (style != ButtonStyle.Ghost)
            {
                Edge(rect, new Color(1f, 1f, 1f, style == ButtonStyle.Secondary ? 0.14f : 0.22f), 12, 2);
                Image sheen = Panel(rect, "Sheen", new Color(1f, 1f, 1f, 0.1f));
                sheen.sprite = UiSprites.Gradient();
                Stretch(sheen.rectTransform, 2f);
            }
            Text label = Label(rect, "Caption", caption, fontSize, TextAnchor.MiddleCenter, text);
            label.fontStyle = FontStyle.Bold;
            Stretch(label.rectTransform);
            AddShadow(label, 1.5f);
            return button;
        }

        private static void StyleColors(ButtonStyle style, out Color normal, out Color hover, out Color pressed,
            out Color text)
        {
            switch (style)
            {
                case ButtonStyle.Primary:
                    normal = AccentDeep;
                    hover = new Color(0.14f, 0.55f, 0.95f);
                    pressed = new Color(0.06f, 0.33f, 0.62f);
                    text = Color.white;
                    break;
                case ButtonStyle.Danger:
                    normal = new Color(0.48f, 0.13f, 0.14f);
                    hover = new Color(0.72f, 0.2f, 0.2f);
                    pressed = new Color(0.36f, 0.09f, 0.1f);
                    text = new Color(1f, 0.9f, 0.88f);
                    break;
                case ButtonStyle.Success:
                    normal = new Color(0.1f, 0.5f, 0.28f);
                    hover = new Color(0.16f, 0.68f, 0.38f);
                    pressed = new Color(0.07f, 0.38f, 0.2f);
                    text = Color.white;
                    break;
                case ButtonStyle.Ghost:
                    normal = new Color(1f, 1f, 1f, 0.05f);
                    hover = new Color(1f, 1f, 1f, 0.16f);
                    pressed = new Color(1f, 1f, 1f, 0.08f);
                    text = TextColor;
                    break;
                default:
                    normal = ButtonColor;
                    hover = new Color(0.2f, 0.27f, 0.42f);
                    pressed = new Color(0.09f, 0.12f, 0.2f);
                    text = TextColor;
                    break;
            }
        }

        /// <summary>Sets the caption of a button made with <see cref="MakeButton(Transform,string,string,Vector2,UnityAction,ButtonStyle,int)"/>.</summary>
        public static void SetCaption(Button button, string caption)
        {
            button.GetComponentInChildren<Text>().text = caption;
        }

        /// <summary>Changes the style of an existing button (for toggles and selected tabs).</summary>
        public static void Restyle(Button button, ButtonStyle style)
        {
            StyleColors(style, out Color normal, out Color hover, out Color pressed, out Color text);
            ButtonFx fx = button.GetComponent<ButtonFx>();
            if (fx != null)
            {
                fx.Recolor(normal, hover, pressed);
            }
            button.GetComponentInChildren<Text>().color = text;
        }

        /// <summary>Recolors a button's background directly.</summary>
        public static void SetColor(Button button, Color color)
        {
            ButtonFx fx = button.GetComponent<ButtonFx>();
            if (fx != null)
            {
                fx.Recolor(color, Color.Lerp(color, Color.white, 0.2f), Color.Lerp(color, Color.black, 0.25f));
            }
            else
            {
                button.GetComponent<Image>().color = color;
            }
        }

        // ------------------------------------------------------------------------------------------ inputs

        public static InputField MakeField(Transform parent, string name, string initial, Vector2 size)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            Image background = rect.gameObject.AddComponent<Image>();
            background.sprite = UiSprites.Rounded(10);
            background.type = Image.Type.Sliced;
            background.color = new Color(0.02f, 0.03f, 0.06f, 0.95f);
            Edge(rect, new Color(0.3f, 0.45f, 0.7f, 0.45f), 10, 2);
            InputField field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            Text text = Label(rect, "Text", string.Empty, 26, TextAnchor.MiddleLeft, TextColor);
            Stretch(text.rectTransform, 14f);
            text.raycastTarget = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            field.textComponent = text;
            field.text = initial;
            return field;
        }

        /// <summary>A horizontal slider with a rounded track, a filled part and a round handle.</summary>
        public static Slider MakeSlider(Transform parent, string name, Vector2 size, float min, float max,
            float value, UnityAction<float> onChange)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            Image track = rect.gameObject.AddComponent<Image>();
            track.sprite = UiSprites.Rounded(6);
            track.type = Image.Type.Sliced;
            track.color = new Color(0.02f, 0.03f, 0.06f, 0.95f);

            RectTransform fillArea = Rect("Fill Area", rect);
            Stretch(fillArea, 0f);
            fillArea.offsetMin = new Vector2(4f, size.y * 0.5f - 6f);
            fillArea.offsetMax = new Vector2(-4f, -(size.y * 0.5f - 6f));
            Image fill = Card(fillArea, "Fill", AccentColor, 6);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = Rect("Handle Slide Area", rect);
            Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(size.y * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-size.y * 0.5f, 0f);
            Image handle = Panel(handleArea, "Handle", Color.white);
            handle.sprite = UiSprites.Circle();
            handle.rectTransform.sizeDelta = new Vector2(size.y, size.y);
            handle.raycastTarget = true;

            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChange != null)
            {
                slider.onValueChanged.AddListener(onChange);
            }
            return slider;
        }
    }
}
