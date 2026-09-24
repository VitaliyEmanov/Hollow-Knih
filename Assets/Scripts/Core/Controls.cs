using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// Keyboard + gamepad bindings.
    ///
    ///  Move      : Arrows / A,D  (gamepad: left stick / d-pad)
    ///  Look/aim  : Up / W, Down / S
    ///  Jump      : Z / Space / K         (pad A)
    ///  Attack    : X / J                 (pad X)
    ///  Dash      : C / Left Shift / L    (pad RB)
    ///  Flame     : F / V / I  — tap: spell "Вспышка", hold: heal (pad B)
    ///  Interact  : Up / W
    ///  Pause     : Esc                   (pad Start)
    ///
    /// Works with either input backend. The legacy Input Manager is used when enabled;
    /// when a project has "Active Input Handling = Input System Package (New)" (the Unity 6
    /// default), UnityEngine.Input throws, and we read the new Input System through
    /// reflection instead, so the game needs no package reference to compile.
    /// </summary>
    public static class Controls
    {
        // ------------------------------------------------------------------
        // Backend
        // ------------------------------------------------------------------

        static bool? legacy;

        static bool Legacy
        {
            get
            {
                if (legacy == null)
                {
                    try
                    {
                        Input.GetKey(KeyCode.Space);
                        legacy = true;
                    }
                    catch (Exception)
                    {
                        legacy = false;
                        NewInput.Init();
                    }
                }
                return legacy.Value;
            }
        }

        /// <summary>Is the key held?</summary>
        public static bool Held(KeyCode k)
        {
            if (Legacy) return Input.GetKey(k);
            return NewInput.Held(k);
        }

        /// <summary>Was the key pressed this frame?</summary>
        public static bool Tap(KeyCode k)
        {
            if (Legacy) return Input.GetKeyDown(k);
            return NewInput.Pressed(k);
        }

        public static bool AnyDown
        {
            get
            {
                if (Legacy) return Input.anyKeyDown;
                return NewInput.AnyPressed();
            }
        }

        static float StickX
        {
            get
            {
                if (Legacy)
                {
                    try { return Input.GetAxisRaw("Horizontal"); } catch (Exception) { return 0f; }
                }
                return NewInput.Stick().x;
            }
        }

        static float StickY
        {
            get
            {
                if (Legacy)
                {
                    try { return Input.GetAxisRaw("Vertical"); } catch (Exception) { return 0f; }
                }
                return NewInput.Stick().y;
            }
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        public static float MoveX
        {
            get
            {
                float x = 0f;
                if (Held(KeyCode.LeftArrow) || Held(KeyCode.A)) x -= 1f;
                if (Held(KeyCode.RightArrow) || Held(KeyCode.D)) x += 1f;
                if (x == 0f)
                {
                    float a = StickX;
                    if (Mathf.Abs(a) > 0.35f) x = Mathf.Sign(a);
                }
                return x;
            }
        }

        public static bool Up { get { return Held(KeyCode.UpArrow) || Held(KeyCode.W) || StickY > 0.5f; } }
        public static bool Down { get { return Held(KeyCode.DownArrow) || Held(KeyCode.S) || StickY < -0.5f; } }
        public static bool UpPressed { get { return Tap(KeyCode.UpArrow) || Tap(KeyCode.W); } }
        public static bool DownPressed { get { return Tap(KeyCode.DownArrow) || Tap(KeyCode.S); } }

        public static bool JumpPressed { get { return Tap(KeyCode.Z) || Tap(KeyCode.Space) || Tap(KeyCode.K) || Tap(KeyCode.JoystickButton0); } }
        public static bool JumpHeld { get { return Held(KeyCode.Z) || Held(KeyCode.Space) || Held(KeyCode.K) || Held(KeyCode.JoystickButton0); } }
        public static bool AttackPressed { get { return Tap(KeyCode.X) || Tap(KeyCode.J) || Tap(KeyCode.JoystickButton2); } }
        public static bool DashPressed { get { return Tap(KeyCode.C) || Tap(KeyCode.LeftShift) || Tap(KeyCode.L) || Tap(KeyCode.JoystickButton5); } }
        public static bool FlamePressed { get { return Tap(KeyCode.F) || Tap(KeyCode.V) || Tap(KeyCode.I) || Tap(KeyCode.JoystickButton1); } }
        public static bool FlameHeld { get { return Held(KeyCode.F) || Held(KeyCode.V) || Held(KeyCode.I) || Held(KeyCode.JoystickButton1); } }

        public static bool InteractPressed { get { return UpPressed; } }
        public static bool PausePressed { get { return Tap(KeyCode.Escape) || Tap(KeyCode.JoystickButton7); } }

        public static bool ConfirmPressed
        {
            get
            {
                return Tap(KeyCode.Return) || Tap(KeyCode.KeypadEnter) || Tap(KeyCode.Z) ||
                       Tap(KeyCode.Space) || Tap(KeyCode.X) || Tap(KeyCode.J) || Tap(KeyCode.JoystickButton0);
            }
        }

        public static bool MenuUp { get { return Tap(KeyCode.UpArrow) || Tap(KeyCode.W); } }
        public static bool MenuDown { get { return Tap(KeyCode.DownArrow) || Tap(KeyCode.S); } }
        public static bool BackPressed { get { return Tap(KeyCode.Escape) || Tap(KeyCode.JoystickButton1); } }

        // ------------------------------------------------------------------
        // New Input System through reflection
        // ------------------------------------------------------------------

        static class NewInput
        {
            static bool ready;
            static PropertyInfo keyboardCurrent, gamepadCurrent, anyKey;
            static PropertyInfo keyIndexer;
            static Type keyEnum;
            static PropertyInfo isPressedProp, wasPressedProp;
            static MethodInfo readVector2;
            static readonly Dictionary<KeyCode, object> keyMap = new Dictionary<KeyCode, object>();
            static readonly Dictionary<KeyCode, PropertyInfo> padMap = new Dictionary<KeyCode, PropertyInfo>();
            static PropertyInfo leftStick, dpad;

            public static void Init()
            {
                if (ready) return;
                try
                {
                    Type kb = null, pad = null, button = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (kb == null) kb = asm.GetType("UnityEngine.InputSystem.Keyboard");
                        if (pad == null) pad = asm.GetType("UnityEngine.InputSystem.Gamepad");
                        if (keyEnum == null) keyEnum = asm.GetType("UnityEngine.InputSystem.Key");
                        if (button == null) button = asm.GetType("UnityEngine.InputSystem.Controls.ButtonControl");
                    }
                    if (kb == null || keyEnum == null || button == null)
                    {
                        Debug.LogError("Ashen Wick: legacy Input is disabled and the Input System package was not found. " +
                                       "Set Edit > Project Settings > Player > Active Input Handling to \"Both\".");
                        return;
                    }
                    keyboardCurrent = kb.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                    anyKey = kb.GetProperty("anyKey");
                    keyIndexer = kb.GetProperty("Item", new[] { keyEnum });
                    isPressedProp = button.GetProperty("isPressed");
                    wasPressedProp = button.GetProperty("wasPressedThisFrame");

                    Map(KeyCode.LeftArrow, "LeftArrow"); Map(KeyCode.RightArrow, "RightArrow");
                    Map(KeyCode.UpArrow, "UpArrow"); Map(KeyCode.DownArrow, "DownArrow");
                    Map(KeyCode.Space, "Space"); Map(KeyCode.Return, "Enter"); Map(KeyCode.KeypadEnter, "NumpadEnter");
                    Map(KeyCode.Escape, "Escape"); Map(KeyCode.Backspace, "Backspace"); Map(KeyCode.LeftShift, "LeftShift");
                    foreach (var c in "ACDFIJKLSVWXZ") Map((KeyCode)Enum.Parse(typeof(KeyCode), c.ToString()), c.ToString());

                    if (pad != null)
                    {
                        gamepadCurrent = pad.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                        padMap[KeyCode.JoystickButton0] = pad.GetProperty("buttonSouth");
                        padMap[KeyCode.JoystickButton1] = pad.GetProperty("buttonEast");
                        padMap[KeyCode.JoystickButton2] = pad.GetProperty("buttonWest");
                        padMap[KeyCode.JoystickButton5] = pad.GetProperty("rightShoulder");
                        padMap[KeyCode.JoystickButton7] = pad.GetProperty("startButton");
                        leftStick = pad.GetProperty("leftStick");
                        dpad = pad.GetProperty("dpad");
                        var stickType = leftStick != null ? leftStick.PropertyType : null;
                        if (stickType != null) readVector2 = stickType.GetMethod("ReadValue", Type.EmptyTypes);
                    }
                    ready = true;
                }
                catch (Exception e)
                {
                    Debug.LogError("Ashen Wick: could not initialise the Input System backend: " + e.Message);
                }
            }

            static void Map(KeyCode code, string name)
            {
                try { keyMap[code] = Enum.Parse(keyEnum, name); } catch (Exception) { }
            }

            static object Control(KeyCode k)
            {
                object key;
                if (keyMap.TryGetValue(k, out key))
                {
                    var kb = keyboardCurrent.GetValue(null, null);
                    return kb == null ? null : keyIndexer.GetValue(kb, new[] { key });
                }
                PropertyInfo p;
                if (gamepadCurrent != null && padMap.TryGetValue(k, out p) && p != null)
                {
                    var gp = gamepadCurrent.GetValue(null, null);
                    return gp == null ? null : p.GetValue(gp, null);
                }
                return null;
            }

            static bool Read(object control, PropertyInfo prop)
            {
                if (control == null || prop == null) return false;
                try { return (bool)prop.GetValue(control, null); } catch (Exception) { return false; }
            }

            public static bool Held(KeyCode k) { return ready && Read(Control(k), isPressedProp); }
            public static bool Pressed(KeyCode k) { return ready && Read(Control(k), wasPressedProp); }

            public static bool AnyPressed()
            {
                if (!ready) return false;
                var kb = keyboardCurrent.GetValue(null, null);
                if (kb != null && anyKey != null && Read(anyKey.GetValue(kb, null), wasPressedProp)) return true;
                foreach (var k in padMap.Keys) if (Pressed(k)) return true;
                return false;
            }

            public static Vector2 Stick()
            {
                if (!ready || gamepadCurrent == null || readVector2 == null) return Vector2.zero;
                var gp = gamepadCurrent.GetValue(null, null);
                if (gp == null) return Vector2.zero;
                try
                {
                    var v = (Vector2)readVector2.Invoke(leftStick.GetValue(gp, null), null);
                    if (v.sqrMagnitude < 0.1f && dpad != null)
                    {
                        var d = dpad.GetValue(gp, null);
                        var m = d.GetType().GetMethod("ReadValue", Type.EmptyTypes);
                        if (m != null) v = (Vector2)m.Invoke(d, null);
                    }
                    return v;
                }
                catch (Exception) { return Vector2.zero; }
            }
        }
    }
}
