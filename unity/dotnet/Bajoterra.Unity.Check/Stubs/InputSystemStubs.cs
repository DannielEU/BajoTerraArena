// Signatures of the parts of the Input System package (com.unity.inputsystem 1.x) that the game code uses.
#pragma warning disable CS0649, CS0067, CS8981
namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None, Space, Enter, Tab, Escape, LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
    }

    namespace Controls
    {
        public class ButtonControl
        {
            public bool isPressed { get; }
            public bool wasPressedThisFrame { get; }
            public bool wasReleasedThisFrame { get; }
        }

        public class KeyControl : ButtonControl
        {
        }

        public class Vector2Control
        {
            public Vector2 ReadValue() => default;
        }
    }

    public class InputDevice
    {
    }

    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; }
        public Controls.KeyControl this[Key key] => null;
    }

    public class Mouse : InputDevice
    {
        public static Mouse current { get; }
        public Controls.Vector2Control delta { get; }
        public Controls.Vector2Control scroll { get; }
        public Controls.ButtonControl leftButton { get; }
        public Controls.ButtonControl rightButton { get; }
    }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : EventSystems.BaseInputModule
    {
        public void AssignDefaultActions()
        {
        }
    }
}
