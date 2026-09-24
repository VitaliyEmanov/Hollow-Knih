using UnityEngine;
using static AshenWick.Art.Palette;

namespace AshenWick.Art
{
    /// <summary>
    /// Procedural "hand painted" art. Every method returns a fresh ArtCanvas.
    /// Style rules (inspired by the ink-and-wash look of Hallownest):
    ///  - dark near-black silhouettes, pale mask faces with huge hollow eyes
    ///  - soft grain, thin ink outline, restrained palette
    ///  - warm light only where something still burns
    /// </summary>
    public static partial class ArtLibrary
    {
        static ArtCanvas New(int w, int h, float pivotX, float pivotY, float ppu = 64f)
        {
            return new ArtCanvas(w, h) { Pivot = new Vector2(pivotX, pivotY), PPU = ppu };
        }

        static Vector2 V(float x, float y) { return new Vector2(x, y); }

        /// <summary>Cylindrical shading (lit from upper left) for rounded bodies.</summary>
        static ArtCanvas.Paint Cylinder(float cx, float halfW, Color dark, Color light, float lightOffset = -0.35f)
        {
            return (x, y) =>
            {
                float t = (x - cx) / halfW - lightOffset;
                float k = Mathf.Clamp01(1f - t * t * 0.9f);
                return Color.Lerp(dark, light, k);
            };
        }

        static ArtCanvas.Paint VGrad(float y0, float y1, Color bottom, Color top)
        {
            return (x, y) => Color.Lerp(bottom, top, Mathf.Clamp01((y - y0) / (y1 - y0)));
        }

        /// <summary>Teardrop SDF (flame / drip / feather tip) pointing up.</summary>
        static ArtCanvas.Sdf TeardropSdf(float cx, float cy, float r, float height, float curve = 0.9f)
        {
            return (x, y) =>
            {
                if (y <= cy) return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                float t = Mathf.Clamp01((y - cy) / height);
                float w = r * Mathf.Pow(1f - t, curve);
                float dx = Mathf.Abs(x - cx) - w;
                float dy = y - (cy + height);
                return Mathf.Max(dx, dy) * 0.9f;
            };
        }

        static void Teardrop(ArtCanvas c, float cx, float cy, float r, float h, Color col, float curve = 0.9f)
        {
            c.Fill(TeardropSdf(cx, cy, r, h, curve), col, cx - r, cy - r, cx + r, cy + h);
        }

        static void Teardrop(ArtCanvas c, float cx, float cy, float r, float h, ArtCanvas.Paint p, float curve = 0.9f)
        {
            c.Fill(TeardropSdf(cx, cy, r, h, curve), p, cx - r, cy - r, cx + r, cy + h);
        }

        /// <summary>Hollow Knight style eyes: two tall black ovals, slightly tilted.</summary>
        static void HollowEyes(ArtCanvas c, float cx, float cy, float spacing, float rx, float ry, float tilt = 0.12f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float ex = cx + s * spacing;
                float tl = -s * tilt;
                c.Fill((x, y) =>
                {
                    float dx = x - ex, dy = y - cy;
                    float rx2 = dx * Mathf.Cos(tl) - dy * Mathf.Sin(tl);
                    float ry2 = dx * Mathf.Sin(tl) + dy * Mathf.Cos(tl);
                    float nx = rx2 / rx, ny = ry2 / ry;
                    return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
                }, Eye, ex - rx - 2, cy - ry - 2, ex + rx + 2, cy + ry + 2);
            }
        }

        // ==================================================================
        // NIV — the little candle
        // ==================================================================

        public static ArtCanvas NivHead()
        {
            var c = New(56, 64, 0.5f, 0.12f);
            float cx = 28;
            c.Wobble = 0.5f;
            // wax head (short thick candle)
            c.Box(cx, 26, 16, 19, 11, Cylinder(cx, 16, WaxShade, Wax));
            // melted concave top pool
            c.Ellipse(cx, 42, 15, 5, WaxShade);
            c.Ellipse(cx + 1, 42.5f, 12, 3.4f, WaxWarm.Mul(0.93f));
            c.Ellipse(cx - 3, 43.5f, 5, 1.2f, Mask);
            // rim lip
            c.Wobble = 0f;
            c.Stroke(ArtCanvas.EllipseSdf(cx, 42, 15, 5), Wax, 1.6f, cx - 16, 36, cx + 16, 48);
            // drips down the face
            float[] dripX = { 14.5f, 22f, 37f, 41f };
            float[] dripL = { 10, 5, 14, 7 };
            for (int i = 0; i < dripX.Length; i++)
            {
                float dx = dripX[i];
                c.Capsule(dx, 40, dx + 0.4f, 40 - dripL[i], 2.3f, 2.8f, Cylinder(dx, 3, WaxShade, Wax, -0.6f));
                c.Circle(dx - 0.6f, 40 - dripL[i] + 0.8f, 0.9f, Mask);
            }
            // hollow eyes
            HollowEyes(c, cx, 22, 6.2f, 4.4f, 7.2f);
            // tiny highlight on wax
            c.Capsule(cx - 11, 14, cx - 11, 32, 1.1f, 1.1f, Mask.WithA(0.55f));
            // wick
            c.Brush(V(cx, 42), V(cx + 1.5f, 48), V(cx + 0.5f, 53), 3.2f, 2f, Ink);
            c.Circle(cx + 0.5f, 53, 1.6f, EmberDim);
            c.Grain(0.08f, 0.25f, 11);
            c.Outline(Ink, 1.4f);
            return c;
        }

        public static ArtCanvas NivCloak()
        {
            var c = New(64, 48, 0.5f, 0.92f);
            c.Wobble = 0.8f;
            var pts = new[]
            {
                V(20, 45), V(44, 45), V(50, 30), V(58, 10), V(55, 3), V(50, 8), V(46, 1), V(40, 6),
                V(34, 0), V(28, 6), V(22, 1), V(17, 7), V(11, 2), V(8, 9), V(13, 30)
            };
            c.Poly(pts, VGrad(0, 46, Cloak.Mul(0.7f), CloakLight));
            c.Wobble = 0;
            // folds
            c.Brush(V(26, 40), V(22, 22), V(18, 6), 2.2f, 0.6f, Ink.WithA(0.55f));
            c.Brush(V(34, 40), V(35, 20), V(34, 3), 2.2f, 0.6f, Ink.WithA(0.5f));
            c.Brush(V(41, 40), V(46, 22), V(49, 8), 2f, 0.6f, Ink.WithA(0.5f));
            // shoulder light
            c.Brush(V(21, 43), V(32, 46), V(43, 43), 2.4f, 2.4f, CloakLight.Mul(1.25f).WithA(0.8f));
            c.Grain(0.12f, 0.3f, 3);
            c.Outline(Ink, 1.4f);
            return c;
        }

        public static ArtCanvas NivLeg()
        {
            var c = New(14, 18, 0.5f, 0.95f);
            c.Capsule(7, 16, 7, 4, 3.4f, 3.8f, Soot);
            c.Ellipse(8, 3.5f, 4.5f, 2.6f, Ink);
            c.Outline(Ink, 1f);
            return c;
        }

        public static ArtCanvas NivFlame()
        {
            var c = New(40, 64, 0.5f, 0.12f);
            float cx = 20;
            Teardrop(c, cx, 16, 11, 44, FlameRed.WithA(0.85f), 1.3f);
            Teardrop(c, cx, 15, 9.5f, 38, FlameOrange, 1.2f);
            Teardrop(c, cx, 14, 7, 28, FlameYellow, 1.1f);
            Teardrop(c, cx, 12.5f, 4.3f, 16, FlameCore, 1f);
            // blue root, like a real candle
            c.Ellipse(cx, 9, 3.5f, 2.2f, Hex("7aa6ff", 0.7f));
            c.OuterGlow(FlameOrange.WithA(0.6f), 6);
            return c;
        }

        public static ArtCanvas Glow()
        {
            var c = New(128, 128, 0.5f, 0.5f);
            c.Glow(64, 64, 63, Color.white, 2.2f);
            return c;
        }

        public static ArtCanvas SoftDot()
        {
            var c = New(32, 32, 0.5f, 0.5f);
            c.Glow(16, 16, 15.5f, Color.white, 1.4f);
            return c;
        }

        public static ArtCanvas HardDot()
        {
            var c = New(16, 16, 0.5f, 0.5f);
            c.Circle(8, 8, 6, Color.white);
            return c;
        }

        public static ArtCanvas AshFlake()
        {
            var c = New(12, 12, 0.5f, 0.5f);
            c.Wobble = 0.7f; c.WobbleScale = 0.6f;
            c.Poly(new[] { V(2, 5), V(6, 10), V(10, 7), V(9, 2), V(4, 1) }, Color.white);
            return c;
        }

        public static ArtCanvas NivBlade()
        {
            var c = New(80, 16, 0.08f, 0.5f);
            // handle wrap
            c.Capsule(2, 8, 12, 8, 2.6f, 2.6f, Soot);
            for (int i = 0; i < 4; i++) c.Line(3 + i * 2.6f, 5.5f, 4.5f + i * 2.6f, 10.5f, 1.1f, CloakLight);
            // guard: small wax bead
            c.Circle(13.5f, 8, 3.6f, Cylinder(13.5f, 3.6f, WaxShade, Wax));
            // needle blade of hardened wick-bone
            c.Capsule(15, 8, 78, 8, 3f, 0.4f, VGrad(4, 12, MaskShade, Mask));
            c.Line(17, 9.2f, 70, 8.3f, 0.8f, Color.white.WithA(0.8f));
            c.Outline(Ink, 1.1f);
            return c;
        }

        /// <summary>Crescent slash effect, facing +X.</summary>
        public static ArtCanvas SlashArc()
        {
            var c = New(140, 150, 0.22f, 0.5f);
            float cy = 75;
            var outer = ArtCanvas.CircleSdf(20, cy, 104);
            var inner = ArtCanvas.CircleSdf(-34, cy, 128);
            ArtCanvas.Sdf cres = (x, y) => Mathf.Max(outer(x, y), -inner(x, y));
            c.Fill(cres, (x, y) =>
            {
                float edge = Mathf.Clamp01(-outer(x, y) / 28f); // 0 at outer edge
                float fadeEnds = Mathf.Clamp01(1f - Mathf.Abs(y - cy) / 72f);
                fadeEnds = Mathf.Pow(fadeEnds, 0.55f);
                Color col = Color.Lerp(Color.white, FlameYellow, edge * 0.9f);
                col = Color.Lerp(col, FlameOrange, Mathf.Clamp01(edge * 1.6f - 0.6f));
                col.a = fadeEnds * Mathf.Lerp(1f, 0.25f, edge);
                return col;
            }, 0, 0, 140, 150);
            return c;
        }

        /// <summary>Small star-like hit spark.</summary>
        public static ArtCanvas HitSpark()
        {
            var c = New(96, 96, 0.5f, 0.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + (i % 2) * 0.2f;
                float len = i % 2 == 0 ? 46 : 26;
                c.Capsule(48, 48, 48 + Mathf.Cos(a) * len, 48 + Mathf.Sin(a) * len, 5f, 0.3f, Color.white);
            }
            c.Circle(48, 48, 10, Color.white);
            return c;
        }

        public static ArtCanvas Ring()
        {
            var c = New(128, 128, 0.5f, 0.5f);
            c.Stroke(ArtCanvas.CircleSdf(64, 64, 56), Color.white, 6, 0, 0, 128, 128);
            c.Glow(64, 64, 64, Color.white.WithA(0.25f), 1f);
            return c;
        }

        /// <summary>Player spell "Вспышка" — a rolling ball of candle fire.</summary>
        public static ArtCanvas FlareBolt()
        {
            var c = New(96, 56, 0.7f, 0.5f);
            // tail
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                c.Circle(66 - i * 10, 28 + Mathf.Sin(i * 1.7f) * 3f, 16 - i * 2.2f, Color.Lerp(FlameYellow, FlameRed, t).WithA(0.9f - t * 0.6f));
            }
            c.Circle(68, 28, 15, FlameOrange);
            c.Circle(70, 29, 10, FlameYellow);
            c.Circle(71, 29, 5.5f, FlameCore);
            c.OuterGlow(FlameOrange.WithA(0.7f), 8);
            return c;
        }

        // ==================================================================
        // NPCs
        // ==================================================================

        /// <summary>Ilva, the Candlemother — ancient, half melted, still warm.</summary>
        public static ArtCanvas Candlemother()
        {
            var c = New(150, 190, 0.5f, 0.02f);
            float cx = 75;
            c.Wobble = 1.2f;
            // pooled wax base
            c.Ellipse(cx, 16, 70, 14, Cylinder(cx, 70, WaxDeep, WaxShade));
            // shawl / cloak mass
            c.Poly(new[] { V(22, 14), V(36, 110), V(58, 132), V(95, 132), V(116, 108), V(130, 14) }, VGrad(0, 132, Soot, Cloak));
            c.Wobble = 0.6f;
            // tall melting head
            c.Box(cx, 128, 26, 40, 20, Cylinder(cx, 26, WaxShade, Wax));
            c.Ellipse(cx, 164, 25, 7, WaxShade);
            c.Ellipse(cx + 2, 165, 20, 4.5f, WaxWarm);
            c.Wobble = 0;
            float[] dx = { 52, 61, 84, 96 };
            float[] dl = { 34, 16, 24, 40 };
            for (int i = 0; i < 4; i++)
                c.Capsule(dx[i], 162, dx[i], 162 - dl[i], 3.2f, 4f, Cylinder(dx[i], 4, WaxShade, Wax, -0.5f));
            // sleepy half-closed eyes
            HollowEyes(c, cx, 132, 11, 6, 7.5f, 0.2f);
            c.Box(cx - 11, 138, 8, 3.5f, 1, WaxShade);
            c.Box(cx + 11, 138, 8, 3.5f, 1, WaxShade);
            // wick and her small tired flame
            c.Brush(V(cx, 165), V(cx - 3, 172), V(cx - 1, 178), 3.6f, 2.4f, Ink);
            Teardrop(c, cx - 1, 181, 5, 12, FlameOrange);
            Teardrop(c, cx - 1, 180, 3, 7, FlameYellow);
            // hands holding a lantern of melted wax
            c.Ellipse(cx - 4, 64, 14, 11, Cylinder(cx, 14, WaxShade, Wax));
            c.Circle(cx - 4, 50, 9, FlameOrange.WithA(0.8f));
            c.Circle(cx - 4, 50, 5, FlameCore);
            c.Grain(0.1f, 0.2f, 5);
            c.Outline(Ink, 1.6f);
            return c;
        }

        /// <summary>Prakh the Chronicler — a soot moth scholar carrying a scroll.</summary>
        public static ArtCanvas Chronicler()
        {
            var c = New(120, 140, 0.5f, 0.02f);
            float cx = 60;
            c.Wobble = 0.9f;
            // folded dusty wings behind
            c.Poly(new[] { V(24, 40), V(10, 110), V(40, 128), V(56, 80) }, VGrad(30, 128, AshDark, Ash));
            c.Poly(new[] { V(96, 40), V(110, 110), V(80, 128), V(64, 80) }, VGrad(30, 128, AshDark, Ash));
            c.Circle(26, 100, 7, AshPale.WithA(0.6f));
            c.Circle(94, 100, 7, AshPale.WithA(0.6f));
            // fluffy robe body
            c.Ellipse(cx, 50, 28, 46, VGrad(4, 96, Soot, AshDark));
            c.Ellipse(cx, 92, 22, 10, Ash);
            // fur collar
            for (int i = 0; i < 9; i++) c.Circle(cx - 20 + i * 5, 92 + Mathf.Sin(i) * 2, 6, AshLight);
            // mask
            c.Wobble = 0.3f;
            c.Ellipse(cx, 110, 17, 16, Cylinder(cx, 17, MaskShade, Mask));
            HollowEyes(c, cx, 110, 6.5f, 4f, 5.5f, 0.05f);
            // round spectacles
            c.Wobble = 0;
            c.Stroke(ArtCanvas.CircleSdf(cx - 6.5f, 110, 6.5f), Hex("b08d57"), 1.4f, 40, 100, 80, 120);
            c.Stroke(ArtCanvas.CircleSdf(cx + 6.5f, 110, 6.5f), Hex("b08d57"), 1.4f, 40, 100, 80, 120);
            // antennae (feathery)
            c.Brush(V(cx - 8, 124), V(cx - 20, 140), V(cx - 32, 136), 2.2f, 1f, Soot);
            c.Brush(V(cx + 8, 124), V(cx + 20, 140), V(cx + 32, 136), 2.2f, 1f, Soot);
            // scroll
            c.Box(cx + 18, 52, 16, 6, 3, AshPale);
            c.Box(cx + 18, 52, 13, 4, 2, Hex("d8cdb4"));
            c.Grain(0.14f, 0.25f, 9);
            c.Outline(Ink, 1.5f);
            return c;
        }
    }
}
