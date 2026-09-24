using UnityEngine;

namespace AshenWick.Art
{
    /// <summary>
    /// Colour language of "Ashen Wick": cold ash greys and inky blues for the dead world,
    /// warm wax and ember tones for everything that still burns.
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string hex, float a = 1f)
        {
            int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }

        public static Color WithA(this Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        public static Color Mul(this Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

        // Ink & shadows
        public static readonly Color Ink = Hex("0b0c12");
        public static readonly Color InkSoft = Hex("161822");
        public static readonly Color Void = Hex("07070b");

        // Masks / bone (Hollow Knight signature white mask)
        public static readonly Color Mask = Hex("f2efe6");
        public static readonly Color MaskShade = Hex("c9c4b8");
        public static readonly Color Eye = Hex("050508");

        // Wax (Niv and candle folk)
        public static readonly Color Wax = Hex("efe4cc");
        public static readonly Color WaxShade = Hex("c7b594");
        public static readonly Color WaxDeep = Hex("9d8a6b");
        public static readonly Color WaxWarm = Hex("ffd9a0");

        // Fire
        public static readonly Color FlameCore = Hex("fffbe8");
        public static readonly Color FlameYellow = Hex("ffe07a");
        public static readonly Color FlameOrange = Hex("ff9a3c");
        public static readonly Color FlameRed = Hex("e0482a");
        public static readonly Color Ember = Hex("ff6a2b");
        public static readonly Color EmberDim = Hex("a8391d");

        // Ash world
        public static readonly Color Ash = Hex("8c8a8f");
        public static readonly Color AshLight = Hex("c4c1c6");
        public static readonly Color AshPale = Hex("e3e0e4");
        public static readonly Color AshDark = Hex("4a4852");
        public static readonly Color Soot = Hex("23222a");
        public static readonly Color Cloak = Hex("343845");
        public static readonly Color CloakLight = Hex("59607a");

        // Area moods
        public static readonly Color FieldsSky = Hex("3a3a4a");
        public static readonly Color FieldsFog = Hex("6e6a78");
        public static readonly Color WaxworksSky = Hex("2c1a17");
        public static readonly Color WaxworksGlow = Hex("c2561f");
        public static readonly Color CathedralSky = Hex("1b1d2e");
        public static readonly Color CathedralGlow = Hex("7d86b8");
        public static readonly Color HearthSky = Hex("1f0d0b");
        public static readonly Color HearthGlow = Hex("ff5a1f");

        public static readonly Color Clear = new Color(0, 0, 0, 0);
    }
}
