using UnityEngine;
#if BAJOTERRA_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bajoterra.Unity
{
    /// <summary>
    /// Reads keyboard and mouse. With the Input System package active it reads the devices directly (no actions
    /// asset to configure); otherwise it falls back to the old Input Manager, so the project works with either
    /// "Active Input Handling" setting.
    /// </summary>
    public sealed class UnityControls : IControls
    {
        /// <summary>The legacy "Mouse X/Y" axes report pixels times the Input Manager's 0.1 sensitivity.</summary>
        private const float LegacyMouseScale = 10f;

        public float MoveX { get; private set; }

        public float MoveZ { get; private set; }

        public Vector2 Look { get; private set; }

        public bool Fire { get; private set; }

        public bool Fusion { get; private set; }

        public int SlotKey { get; private set; } = -1;

        public int Scroll { get; private set; }

        public bool DancePressed { get; private set; }

        public bool ClickPressed { get; private set; }

        public bool PausePressed { get; private set; }

        public bool ScoreboardHeld { get; private set; }

        public bool DebugPressed { get; private set; }

#if BAJOTERRA_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static readonly Key[] SlotKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
        };

        public void Poll()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            MoveX = 0f;
            MoveZ = 0f;
            SlotKey = -1;
            PausePressed = false;
            DebugPressed = false;
            ScoreboardHeld = false;
            Fusion = false;
            DancePressed = false;
            if (keyboard != null)
            {
                MoveX = (keyboard[Key.D].isPressed ? 1f : 0f) - (keyboard[Key.A].isPressed ? 1f : 0f);
                MoveZ = (keyboard[Key.W].isPressed ? 1f : 0f) - (keyboard[Key.S].isPressed ? 1f : 0f);
                for (int i = 0; i < SlotKeys.Length; i++)
                {
                    if (keyboard[SlotKeys[i]].wasPressedThisFrame)
                    {
                        SlotKey = i;
                    }
                }
                PausePressed = keyboard[Key.Escape].wasPressedThisFrame;
                DebugPressed = keyboard[Key.F3].wasPressedThisFrame;
                ScoreboardHeld = keyboard[Key.Tab].isPressed;
                Fusion = keyboard[Key.Q].isPressed;
                DancePressed = keyboard[Key.B].wasPressedThisFrame;
            }
            if (mouse != null)
            {
                Look = mouse.delta.ReadValue();
                Fire = mouse.leftButton.isPressed;
                Fusion |= mouse.rightButton.isPressed;
                ClickPressed = mouse.leftButton.wasPressedThisFrame;
                float wheel = mouse.scroll.ReadValue().y;
                Scroll = wheel > 0.1f ? 1 : wheel < -0.1f ? -1 : 0;
            }
            else
            {
                Look = Vector2.zero;
                Fire = false;
                ClickPressed = false;
                Scroll = 0;
            }
        }

        public bool IsMoveHeld(string moveId)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }
            foreach (MoveKey key in MoveBindings.KeysFor(moveId))
            {
                if (keyboard[Map(key)].isPressed)
                {
                    return true;
                }
            }
            return false;
        }

        private static Key Map(MoveKey key)
        {
            switch (key)
            {
                case MoveKey.Space: return Key.Space;
                case MoveKey.LeftShift: return Key.LeftShift;
                case MoveKey.LeftCtrl: return Key.LeftCtrl;
                case MoveKey.C: return Key.C;
                case MoveKey.F: return Key.F;
                case MoveKey.Q: return Key.Q;
                case MoveKey.E: return Key.E;
                case MoveKey.R: return Key.R;
                case MoveKey.V: return Key.V;
                case MoveKey.X: return Key.X;
                case MoveKey.Z: return Key.Z;
                default: return Key.None;
            }
        }
#else
        public void Poll()
        {
            MoveX = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            MoveZ = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * LegacyMouseScale;
            Fire = Input.GetMouseButton(0);
            Fusion = Input.GetKey(KeyCode.Q) || Input.GetMouseButton(1);
            ClickPressed = Input.GetMouseButtonDown(0);
            float wheel = Input.mouseScrollDelta.y;
            Scroll = wheel > 0.1f ? 1 : wheel < -0.1f ? -1 : 0;
            SlotKey = -1;
            for (int i = 0; i < 5; i++)
            {
                KeyCode code = KeyCode.Alpha1 + i;
                if (Input.GetKeyDown(code))
                {
                    SlotKey = i;
                }
            }
            PausePressed = Input.GetKeyDown(KeyCode.Escape);
            DebugPressed = Input.GetKeyDown(KeyCode.F3);
            ScoreboardHeld = Input.GetKey(KeyCode.Tab);
            DancePressed = Input.GetKeyDown(KeyCode.B);
        }

        public bool IsMoveHeld(string moveId)
        {
            foreach (MoveKey key in MoveBindings.KeysFor(moveId))
            {
                if (Input.GetKey(Map(key)))
                {
                    return true;
                }
            }
            return false;
        }

        private static KeyCode Map(MoveKey key)
        {
            switch (key)
            {
                case MoveKey.Space: return KeyCode.Space;
                case MoveKey.LeftShift: return KeyCode.LeftShift;
                case MoveKey.LeftCtrl: return KeyCode.LeftControl;
                case MoveKey.C: return KeyCode.C;
                case MoveKey.F: return KeyCode.F;
                case MoveKey.Q: return KeyCode.Q;
                case MoveKey.E: return KeyCode.E;
                case MoveKey.R: return KeyCode.R;
                case MoveKey.V: return KeyCode.V;
                case MoveKey.X: return KeyCode.X;
                case MoveKey.Z: return KeyCode.Z;
                default: return KeyCode.None;
            }
        }
#endif
    }
}
