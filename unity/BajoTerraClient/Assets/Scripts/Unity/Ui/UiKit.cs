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
        /// <summary>The main action of a screen: slug-fire orange and yellow.</summary>
        Primary,

        /// <summary>Everything else: blue metal.</summary>
        Secondary,

        /// <summary>Leaving, quitting, abandoning.</summary>
        Danger,

        /// <summary>Confirming something positive (ready), or the selected tab: slime green.</summary>
        Success,

        /// <summary>A see-through plate with a glowing edge, for tabs and small actions.</summary>
        Ghost
    }

    /// <summary>
    /// Helpers to build uGUI widgets in code, so the project needs no prefabs or sprites. All sizes are in a
    /// 1920 x 1080 reference space (the canvas scaler adapts it to the real resolution). The look follows the show:
    /// blue metal plates with cut corners, a steel rim and a thick navy outline; chunky cartoon buttons with a lip
    /// underneath; fire-coloured titles with a heavy edge; bioluminescent teal for highlights. Two fonts ship in
    /// <c>Resources/Fonts</c>: a heavy comic display face for titles and buttons and a rounded one for the rest.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.035f, 0.07f, 0.17f, 1f);
        public static readonly Color PanelColor = new Color(0.07f, 0.2f, 0.37f, 0.94f);
        public static readonly Color PanelRaised = new Color(0.11f, 0.29f, 0.5f, 0.96f);
        public static readonly Color EdgeColor = new Color(0.62f, 0.86f, 1f, 0.95f);
        public static readonly Color AccentColor = new Color(0.22f, 0.93f, 0.86f, 1f);
        public static readonly Color AccentDeep = new Color(0.04f, 0.5f, 0.56f, 1f);
        public static readonly Color GoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        public static readonly Color FireColor = new Color(1f, 0.48f, 0.08f, 1f);
        public static readonly Color DangerColor = new Color(1f, 0.34f, 0.26f, 1f);
        public static readonly Color SuccessColor = new Color(0.56f, 0.92f, 0.22f, 1f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.42f, 0.76f, 1f);
        public static readonly Color TextColor = new Color(0.97f, 0.99f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.66f, 0.85f, 0.93f, 1f);

        /// <summary>The yellow-to-orange of the show's logo letters.</summary>
        public static readonly Color FireTop = new Color(1f, 0.92f, 0.36f, 1f);

        public static readonly Color FireBottom = new Color(1f, 0.45f, 0.05f, 1f);

        private static readonly Color SteelTop = new Color(0.82f, 0.95f, 1f, 1f);
        private static readonly Color SteelBottom = new Color(0.36f, 0.56f, 0.8f, 1f);

        private static Font _font;
        private static Font _display;

        /// <summary>The font of ordinary text: a rounded heavy face that stays readable when small.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("Fonts/LilitaOne");
                    if (_font == null)
                    {
                        _font = BuiltinFont();
                    }
                }
                return _font;
            }
        }

        /// <summary>The comic display font of titles, buttons and big numbers.</summary>
        public static Font DisplayFont
        {
            get
            {
                if (_display == null)
                {
                    _display = Resources.Load<Font>("Fonts/LuckiestGuy");
                    if (_display == null)
                    {
                        _display = Font;
                    }
                }
                return _display;
            }
        }

        private static Font BuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _font = null;
            _display = null;
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

        /// <summary>Stretches a rect over its parent with a different inset on each side.</summary>
        public static void Inset(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Stretches a rect over a fraction of its parent along x (for bars) and the full height.</summary>
        public static void StretchFraction(RectTransform rect, float fraction)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------------------------------ colour

        public static Color Lighter(Color color, float amount)
        {
            Color mixed = Color.Lerp(color, Color.white, amount);
            mixed.a = color.a;
            return mixed;
        }

        public static Color Darker(Color color, float amount)
        {
            Color mixed = Color.Lerp(color, Color.black, amount);
            mixed.a = color.a;
            return mixed;
        }

        /// <summary>Adds (or updates) a vertical gradient on a graphic.</summary>
        public static UiGradient Gradient(Graphic graphic, Color top, Color bottom)
        {
            UiGradient gradient = graphic.GetComponent<UiGradient>();
            if (gradient == null)
            {
                gradient = graphic.gameObject.AddComponent<UiGradient>();
            }
            gradient.Set(top, bottom);
            return gradient;
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

        /// <summary>A plate with cut corners and a soft top-to-bottom shade (bars, chips, tiles, rows).</summary>
        public static Image Card(Transform parent, string name, Color color, int radius = 14)
        {
            Image image = Panel(parent, name, color);
            image.sprite = UiSprites.Plate(Chamfer(radius));
            image.type = Image.Type.Sliced;
            Gradient(image, Color.white, new Color(0.78f, 0.78f, 0.82f, 1f));
            return image;
        }

        /// <summary>A thin outline drawn over a card.</summary>
        public static Image Edge(Transform parent, Color color, int radius = 14, int thickness = 2)
        {
            Image image = Panel(parent, "Edge", color);
            image.sprite = UiSprites.PlateOutline(Chamfer(radius), thickness);
            image.type = Image.Type.Sliced;
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>
        /// A metal plate, the base of every window: a navy outline, a steel rim, the body in <paramref name="color"/>
        /// (lighter at the top), a glossy band and, on big plates, a rivet in each corner. The returned image is the
        /// outline; put the content in it.
        /// </summary>
        public static Image Window(Transform parent, string name, Color color, int radius = 18)
        {
            int chamfer = Chamfer(radius);
            Image outline = Panel(parent, name, Palette.WithAlpha(Ink, Mathf.Max(color.a, 0.6f)));
            outline.sprite = UiSprites.Plate(chamfer);
            outline.type = Image.Type.Sliced;

            Image body = Panel(outline.transform, "Body", Color.white);
            body.sprite = UiSprites.Plate(Mathf.Max(2, chamfer - 2));
            body.type = Image.Type.Sliced;
            Stretch(body.rectTransform, 4f);
            Gradient(body, Lighter(color, 0.14f), Darker(color, 0.35f));

            Image sheen = Panel(outline.transform, "Sheen", Color.white);
            sheen.sprite = UiSprites.Plate(Mathf.Max(2, chamfer - 2));
            sheen.type = Image.Type.Sliced;
            Inset(sheen.rectTransform, 4f, 0f, 4f, 4f);
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            Gradient(sheen, new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0f));

            Image rim = Panel(outline.transform, "Rim", Color.white);
            rim.sprite = UiSprites.PlateOutline(Mathf.Max(2, chamfer - 2), 3);
            rim.type = Image.Type.Sliced;
            Stretch(rim.rectTransform, 3f);
            Gradient(rim, SteelTop, Palette.WithAlpha(SteelBottom, 0.85f));

            if (radius >= 18)
            {
                Rivet(outline.transform, new Vector2(0f, 1f), new Vector2(chamfer * 0.5f + 12f, -chamfer * 0.5f - 12f));
                Rivet(outline.transform, new Vector2(1f, 1f), new Vector2(-chamfer * 0.5f - 12f, -chamfer * 0.5f - 12f));
                Rivet(outline.transform, new Vector2(0f, 0f), new Vector2(chamfer * 0.5f + 12f, chamfer * 0.5f + 12f));
                Rivet(outline.transform, new Vector2(1f, 0f), new Vector2(-chamfer * 0.5f - 12f, chamfer * 0.5f + 12f));
            }
            return outline;
        }

        private static void Rivet(Transform parent, Vector2 anchor, Vector2 offset)
        {
            Image head = Panel(parent, "Rivet", Palette.WithAlpha(Ink, 0.9f));
            head.sprite = UiSprites.Circle();
            Place(head.rectTransform, anchor, new Vector2(13f, 13f), offset);
            head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image cap = Panel(head.transform, "Cap", Color.white);
            cap.sprite = UiSprites.Circle();
            Stretch(cap.rectTransform, 2.5f);
            Gradient(cap, SteelTop, SteelBottom);
        }

        /// <summary>The cut of a plate's corners for a given old-style corner radius.</summary>
        private static int Chamfer(int radius)
        {
            return Mathf.Clamp(Mathf.RoundToInt(radius * 0.8f), 3, 26);
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

        /// <summary>Text with a navy drop shadow so it reads over any background.</summary>
        public static Text Shadowed(Transform parent, string name, string text, int size, TextAnchor anchor,
            Color color, bool bold = false)
        {
            Text label = Label(parent, name, text, size, anchor, color);
            if (bold)
            {
                Strong(label);
            }
            AddShadow(label, 2f);
            return label;
        }

        /// <summary>Gives a text the navy cartoon edge that makes it pop (used where plain text would be bold).</summary>
        public static void Strong(Text text)
        {
            Stroke(text).Set(Ink, Mathf.Clamp(text.fontSize * 0.07f, 1.5f, 4f));
        }

        /// <summary>The display font with a heavy navy edge and a drop: screen titles, big numbers.</summary>
        public static void Heading(Text text)
        {
            text.font = DisplayFont;
            UiStroke stroke = Stroke(text);
            float width = Mathf.Clamp(text.fontSize * 0.08f, 2f, 9f);
            stroke.Set(Ink, width, text.fontSize >= 60 ? 16 : 10);
            stroke.SetDrop(Palette.WithAlpha(Ink, 0.75f), new Vector2(0f, -width * 0.9f));
        }

        /// <summary>A <see cref="Heading"/> painted yellow to orange, like the show's logo letters.</summary>
        public static void Fire(Text text)
        {
            text.color = Color.white;
            Gradient(text, FireTop, FireBottom);
            Heading(text);
        }

        /// <summary>A <see cref="Heading"/> painted with any two colours.</summary>
        public static void Painted(Text text, Color top, Color bottom)
        {
            text.color = Color.white;
            Gradient(text, top, bottom);
            Heading(text);
        }

        private static UiStroke Stroke(Text text)
        {
            UiStroke stroke = text.GetComponent<UiStroke>();
            return stroke != null ? stroke : text.gameObject.AddComponent<UiStroke>();
        }

        public static void AddShadow(Text text, float distance)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Palette.WithAlpha(Ink, 0.85f);
            shadow.effectDistance = new Vector2(distance * 0.5f, -distance);
        }

        /// <summary>A navy outline around any graphic (the crosshair ticks, small markers).</summary>
        public static void AddOutlineTo(Graphic graphic, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.WithAlpha(Ink, 0.8f);
            outline.effectDistance = new Vector2(distance, -distance);
        }

        public static void AddOutline(Text text, float distance)
        {
            Stroke(text).Set(Ink, distance + 0.5f);
        }

        // ------------------------------------------------------------------------------------------ buttons

        public static Button MakeButton(Transform parent, string name, string caption, Vector2 size,
            UnityAction onClick)
        {
            return MakeButton(parent, name, caption, size, onClick, ButtonStyle.Secondary);
        }

        /// <summary>
        /// A chunky cartoon button: a navy outline, a darker lip along the bottom that the face sinks into when
        /// pressed, a glossy face in the style's colours and a caption with a heavy edge.
        /// </summary>
        public static Button MakeButton(Transform parent, string name, string caption, Vector2 size,
            UnityAction onClick, ButtonStyle style, int fontSize = 26)
        {
            int chamfer = Mathf.Clamp(Mathf.RoundToInt(size.y * 0.2f), 6, 18);
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            Image outline = rect.gameObject.AddComponent<Image>();
            outline.sprite = UiSprites.Plate(chamfer);
            outline.type = Image.Type.Sliced;
            outline.color = Ink;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = outline;
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

            Image lip = Panel(rect, "Lip", Color.white);
            lip.sprite = UiSprites.Plate(Mathf.Max(3, chamfer - 2));
            lip.type = Image.Type.Sliced;
            Stretch(lip.rectTransform, 3f);

            Image face = Panel(rect, "Face", Color.white);
            face.sprite = UiSprites.Plate(Mathf.Max(3, chamfer - 2));
            face.type = Image.Type.Sliced;
            UiGradient faceShade = Gradient(face, Color.white, Color.white);

            Image shine = Panel(face.transform, "Shine", Color.white);
            shine.sprite = UiSprites.Plate(Mathf.Max(3, chamfer - 3));
            shine.type = Image.Type.Sliced;
            Inset(shine.rectTransform, 3f, 0f, 3f, 3f);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            Gradient(shine, new Color(1f, 1f, 1f, 0.42f), new Color(1f, 1f, 1f, 0.04f));

            Image rim = Panel(face.transform, "Rim", new Color(1f, 1f, 1f, 0.3f));
            rim.sprite = UiSprites.PlateOutline(Mathf.Max(3, chamfer - 2), 2);
            rim.type = Image.Type.Sliced;
            Stretch(rim.rectTransform);

            Text label = Label(rect, "Caption", caption, fontSize, TextAnchor.MiddleCenter, TextColor);
            if (fontSize >= 22)
            {
                label.font = DisplayFont;
            }
            Stroke(label).Set(Ink, Mathf.Clamp(fontSize * 0.09f, 1.5f, 4f));
            Stroke(label).SetDrop(Palette.WithAlpha(Ink, 0.6f), new Vector2(0f, -2f));

            var fx = rect.gameObject.AddComponent<ButtonFx>();
            fx.Setup(button, face.rectTransform, faceShade, lip, rim, label);
            ApplyStyle(fx, style);
            return button;
        }

        private static void ApplyStyle(ButtonFx fx, ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary:
                    fx.Recolor(new Color(1f, 0.86f, 0.26f), new Color(1f, 0.46f, 0.06f), new Color(0.62f, 0.2f, 0.02f),
                        Color.white, true);
                    break;
                case ButtonStyle.Danger:
                    fx.Recolor(new Color(1f, 0.45f, 0.36f), new Color(0.78f, 0.1f, 0.12f), new Color(0.42f, 0.04f, 0.07f),
                        Color.white, true);
                    break;
                case ButtonStyle.Success:
                    fx.Recolor(new Color(0.7f, 0.97f, 0.3f), new Color(0.2f, 0.62f, 0.1f), new Color(0.1f, 0.34f, 0.06f),
                        Color.white, true);
                    break;
                case ButtonStyle.Ghost:
                    fx.Recolor(new Color(0.2f, 0.75f, 0.8f, 0.34f), new Color(0.05f, 0.3f, 0.42f, 0.5f),
                        new Color(0f, 0f, 0f, 0f), new Color(0.75f, 1f, 0.97f), false);
                    break;
                default:
                    fx.Recolor(new Color(0.42f, 0.72f, 1f), new Color(0.11f, 0.32f, 0.66f), new Color(0.05f, 0.15f, 0.38f),
                        Color.white, true);
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
            ButtonFx fx = button.GetComponent<ButtonFx>();
            if (fx != null)
            {
                ApplyStyle(fx, style);
            }
        }

        /// <summary>Recolors a button's face from a single colour.</summary>
        public static void SetColor(Button button, Color color)
        {
            ButtonFx fx = button.GetComponent<ButtonFx>();
            if (fx != null)
            {
                fx.Recolor(Lighter(color, 0.3f), color, Darker(color, 0.5f), Color.white, true);
            }
            else
            {
                button.GetComponent<Image>().color = color;
            }
        }

        // ------------------------------------------------------------------------------------------ inputs

        public static InputField MakeField(Transform parent, string name, string initial, Vector2 size,
            string placeholder = null)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            Image outline = rect.gameObject.AddComponent<Image>();
            outline.sprite = UiSprites.Plate(8);
            outline.type = Image.Type.Sliced;
            outline.color = Ink;
            Image well = Panel(rect, "Well", Color.white);
            well.sprite = UiSprites.Plate(6);
            well.type = Image.Type.Sliced;
            Stretch(well.rectTransform, 3f);
            Gradient(well, new Color(0.02f, 0.07f, 0.14f, 1f), new Color(0.05f, 0.16f, 0.26f, 1f));
            Image rim = Panel(rect, "Rim", new Color(0.4f, 0.75f, 0.95f, 0.55f));
            rim.sprite = UiSprites.PlateOutline(6, 2);
            rim.type = Image.Type.Sliced;
            Stretch(rim.rectTransform, 3f);
            InputField field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = outline;
            Text text = Label(rect, "Text", string.Empty, 27, TextAnchor.MiddleLeft, TextColor);
            Stretch(text.rectTransform, 16f);
            text.raycastTarget = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            field.textComponent = text;
            if (placeholder != null)
            {
                Text hint = Label(rect, "Placeholder", placeholder, 25, TextAnchor.MiddleLeft,
                    new Color(0.55f, 0.72f, 0.82f, 0.6f));
                Stretch(hint.rectTransform, 16f);
                field.placeholder = hint;
            }
            field.text = initial;
            return field;
        }

        /// <summary>A horizontal slider with a sunken track, a glowing teal fill and a steel knob.</summary>
        public static Slider MakeSlider(Transform parent, string name, Vector2 size, float min, float max,
            float value, UnityAction<float> onChange)
        {
            RectTransform rect = Rect(name, parent);
            rect.sizeDelta = size;
            Image track = rect.gameObject.AddComponent<Image>();
            track.sprite = UiSprites.Plate(6);
            track.type = Image.Type.Sliced;
            track.color = Ink;
            Image well = Panel(rect, "Well", new Color(0.03f, 0.1f, 0.18f, 1f));
            well.sprite = UiSprites.Plate(5);
            well.type = Image.Type.Sliced;
            Stretch(well.rectTransform, 3f);

            RectTransform fillArea = Rect("Fill Area", rect);
            Stretch(fillArea, 0f);
            fillArea.offsetMin = new Vector2(5f, 5f);
            fillArea.offsetMax = new Vector2(-5f, -5f);
            Image fill = Card(fillArea, "Fill", Color.white, 6);
            Gradient(fill, new Color(0.6f, 1f, 0.95f), AccentDeep);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = Rect("Handle Slide Area", rect);
            Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(size.y * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-size.y * 0.5f, 0f);
            Image handle = Panel(handleArea, "Handle", Ink);
            handle.sprite = UiSprites.Circle();
            handle.rectTransform.sizeDelta = new Vector2(size.y + 12f, size.y + 12f);
            handle.raycastTarget = true;
            Image knob = Panel(handle.transform, "Knob", Color.white);
            knob.sprite = UiSprites.Circle();
            Stretch(knob.rectTransform, 4f);
            Gradient(knob, SteelTop, SteelBottom);

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

        // ------------------------------------------------------------------------------------------ show pieces

        /// <summary>
        /// The game's logo: the name in fire letters with a navy, steel and navy edge, on a blue metal banner with
        /// pointed ends, and a teal tab with <paramref name="tab"/> hanging from it.
        /// </summary>
        public static RectTransform Logo(Transform parent, string name, string tab, float width, int fontSize)
        {
            float height = fontSize * 1.32f;
            RectTransform root = Rect("Logo", parent);
            root.sizeDelta = new Vector2(width, height + 54f);

            Image banner = Panel(root, "Banner", Ink);
            banner.sprite = UiSprites.Plate(Mathf.RoundToInt(height * 0.42f));
            banner.type = Image.Type.Sliced;
            Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(width, height), Vector2.zero);
            Image metal = Panel(banner.transform, "Metal", Color.white);
            metal.sprite = UiSprites.Plate(Mathf.RoundToInt(height * 0.42f) - 3);
            metal.type = Image.Type.Sliced;
            Stretch(metal.rectTransform, 5f);
            Gradient(metal, new Color(0.3f, 0.66f, 1f), new Color(0.06f, 0.2f, 0.52f));
            Image shine = Panel(banner.transform, "Shine", Color.white);
            shine.sprite = UiSprites.Plate(Mathf.RoundToInt(height * 0.42f) - 4);
            shine.type = Image.Type.Sliced;
            Inset(shine.rectTransform, 6f, 0f, 6f, 6f);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            Gradient(shine, new Color(1f, 1f, 1f, 0.32f), new Color(1f, 1f, 1f, 0.02f));
            Image rim = Panel(banner.transform, "Rim", Color.white);
            rim.sprite = UiSprites.PlateOutline(Mathf.RoundToInt(height * 0.42f) - 3, 4);
            rim.type = Image.Type.Sliced;
            Stretch(rim.rectTransform, 4f);
            Gradient(rim, SteelTop, SteelBottom);

            Text title = Label(banner.transform, "Title", name, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(0f, -fontSize * 0.04f);
            title.font = DisplayFont;
            Gradient(title, FireTop, FireBottom);
            UiStroke stroke = Stroke(title);
            float edge = fontSize * 0.035f;
            stroke.SetLayers(new[] { Ink, new Color(0.85f, 0.96f, 1f), Ink }, new[] { edge, edge * 2.4f, edge * 3.6f }, 16);
            stroke.SetDrop(Palette.WithAlpha(Ink, 0.7f), new Vector2(0f, -edge * 1.6f));

            if (!string.IsNullOrEmpty(tab))
            {
                float tabWidth = Mathf.Min(width * 0.46f, 120f + tab.Length * fontSize * 0.2f);
                Image plate = Panel(root, "Tab", Ink);
                plate.sprite = UiSprites.Plate(14);
                plate.type = Image.Type.Sliced;
                Place(plate.rectTransform, new Vector2(0.5f, 0f), new Vector2(tabWidth, 62f), new Vector2(0f, 0f));
                Image glow = Panel(plate.transform, "Glow", Color.white);
                glow.sprite = UiSprites.Plate(12);
                glow.type = Image.Type.Sliced;
                Stretch(glow.rectTransform, 4f);
                Gradient(glow, new Color(0.45f, 1f, 0.92f), new Color(0.02f, 0.5f, 0.55f));
                Text caption = Label(plate.transform, "Caption", tab, 40, TextAnchor.MiddleCenter, Color.white);
                Stretch(caption.rectTransform);
                caption.font = DisplayFont;
                Stroke(caption).Set(Ink, 3f, 10);
                plate.transform.SetAsFirstSibling();
            }
            return root;
        }

        /// <summary>A small teal tab with a caption, for section names ("CONSEJO", "JUGADORES").</summary>
        public static Text Tag(Transform parent, string name, string caption, Vector2 size)
        {
            Image plate = Panel(parent, name, Ink);
            plate.sprite = UiSprites.Plate(8);
            plate.type = Image.Type.Sliced;
            plate.rectTransform.sizeDelta = size;
            Image glow = Panel(plate.transform, "Glow", Color.white);
            glow.sprite = UiSprites.Plate(6);
            glow.type = Image.Type.Sliced;
            Stretch(glow.rectTransform, 3f);
            Gradient(glow, new Color(0.45f, 1f, 0.92f), new Color(0.02f, 0.5f, 0.55f));
            Text text = Label(plate.transform, "Caption", caption, Mathf.RoundToInt(size.y * 0.56f), TextAnchor.MiddleCenter,
                Color.white);
            Stretch(text.rectTransform);
            text.font = DisplayFont;
            Stroke(text).Set(Ink, 2f);
            return text;
        }
    }
}
