using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// Keyboard + gamepad bindings through the legacy Input Manager (KeyCode based,
    /// so it works in a fresh project without configuring axes).
    ///
    ///  Move      : Arrows / A,D  (gamepad: left stick)
    ///  Look/aim  : Up / W, Down / S
    ///  Jump      : Z / Space / K         (pad A)
    ///  Attack    : X / J                 (pad X)
    ///  Dash      : C / Left Shift / L    (pad RB)
    ///  Flame     : F / V / I  — tap: spell "Вспышка", hold: heal (pad B)
    ///  Interact  : Up / W
    ///  Pause     : Esc                   (pad Start)
    /// </summary>
    public static class Controls
    {
        static float Axis(string name)
        {
            try { return Input.GetAxisRaw(name); } catch { return 0f; }
        }

        public static float MoveX
        {
            get
            {
                float x = 0f;
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;
                if (x == 0f)
                {
                    float a = Axis("Horizontal");
                    if (Mathf.Abs(a) > 0.35f) x = Mathf.Sign(a);
                }
                return x;
            }
        }

        public static bool Up { get { return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Axis("Vertical") > 0.5f; } }
        public static bool Down { get { return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Axis("Vertical") < -0.5f; } }
        public static bool UpPressed { get { return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W); } }
        public static bool DownPressed { get { return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S); } }

        public static bool JumpPressed { get { return Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton0); } }
        public static bool JumpHeld { get { return Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.JoystickButton0); } }
        public static bool AttackPressed { get { return Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2); } }
        public static bool DashPressed { get { return Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton5); } }
        public static bool FlamePressed { get { return Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.JoystickButton1); } }
        public static bool FlameHeld { get { return Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.V) || Input.GetKey(KeyCode.I) || Input.GetKey(KeyCode.JoystickButton1); } }

        public static bool InteractPressed { get { return UpPressed; } }
        public static bool PausePressed { get { return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7); } }

        public static bool ConfirmPressed
        {
            get
            {
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Z) ||
                       Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton0);
            }
        }

        public static bool MenuUp { get { return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W); } }
        public static bool MenuDown { get { return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S); } }
        public static bool BackPressed { get { return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1); } }
    }
}
