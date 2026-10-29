// Signatures of the parts of the Unity UI package (com.unity.ugui 2.0) that the game code uses, written from the
// public documentation. They exist only so the compile check can type-check the code; they do nothing.
#pragma warning disable CS0649, CS0067, CS8981
using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    public abstract class Graphic : MonoBehaviour
    {
        public virtual Color color { get; set; }
        public virtual bool raycastTarget { get; set; }
        public RectTransform rectTransform { get; }
        public virtual Material material { get; set; }

        public virtual void SetVerticesDirty()
        {
        }
    }

    public class VertexHelper
    {
        public int currentVertCount { get; }

        public void PopulateUIVertex(ref UIVertex vertex, int i)
        {
        }

        public void SetUIVertex(UIVertex vertex, int i)
        {
        }

        public void GetUIVertexStream(List<UIVertex> stream)
        {
        }

        public void AddUIVertexTriangleStream(List<UIVertex> verts)
        {
        }

        public void Clear()
        {
        }
    }

    public abstract class BaseMeshEffect : EventSystems.UIBehaviour
    {
        protected Graphic graphic { get; }

        public abstract void ModifyMesh(VertexHelper vh);
    }

    public abstract class MaskableGraphic : Graphic
    {
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }

        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }

        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public bool fillClockwise { get; set; }
        public int fillOrigin { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; }
        public float fadeDuration { get; set; }

        public static ColorBlock defaultColorBlock { get; }
    }

    public class Selectable : MonoBehaviour
    {
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent
        {
        }

        public ButtonClickedEvent onClick { get; set; }
    }

    public class Slider : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }

        public class SliderEvent : UnityEvent<float>
        {
        }

        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public Direction direction { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public float value { get; set; }
        public SliderEvent onValueChanged { get; set; }
    }

    public class InputField : Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }

        public class OnChangeEvent : UnityEvent<string>
        {
        }

        public string text { get; set; }
        public int characterLimit { get; set; }
        public ContentType contentType { get; set; }
        public Text textComponent { get; set; }
        public Graphic placeholder { get; set; }
        public OnChangeEvent onValueChanged { get; set; }
    }

    public class CanvasScaler : MonoBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }

        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }

        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : EventSystems.BaseRaycaster
    {
    }

    public class Shadow : MonoBehaviour
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
    }

    public class Outline : Shadow
    {
    }
}

namespace UnityEngine.EventSystems
{
    public class PointerEventData
    {
    }

    public interface IPointerEnterHandler
    {
        void OnPointerEnter(PointerEventData eventData);
    }

    public interface IPointerExitHandler
    {
        void OnPointerExit(PointerEventData eventData);
    }

    public interface IPointerDownHandler
    {
        void OnPointerDown(PointerEventData eventData);
    }

    public interface IPointerUpHandler
    {
        void OnPointerUp(PointerEventData eventData);
    }

    public abstract class UIBehaviour : MonoBehaviour
    {
        public virtual bool IsActive()
        {
            return true;
        }
    }

    public abstract class BaseRaycaster : MonoBehaviour
    {
    }

    public abstract class BaseInputModule : MonoBehaviour
    {
    }

    public class StandaloneInputModule : BaseInputModule
    {
    }

    public class EventSystem : MonoBehaviour
    {
        public static EventSystem current { get; set; }
    }
}
