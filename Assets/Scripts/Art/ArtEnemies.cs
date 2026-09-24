using UnityEngine;
using static AshenWick.Art.Palette;

namespace AshenWick.Art
{
    public static partial class ArtLibrary
    {
        /// <summary>Pale mask face used by most bugs of the Ember Kingdom.</summary>
        static void SmallMask(ArtCanvas c, float cx, float cy, float r, float eyeTilt = 0.1f, bool angry = false)
        {
            c.Ellipse(cx, cy, r, r * 1.08f, Cylinder(cx, r, MaskShade, Mask));
            HollowEyes(c, cx, cy - r * 0.08f, r * 0.42f, r * 0.26f, r * 0.42f, angry ? -0.35f : eyeTilt);
        }

        /// <summary>Glowing seams: the ash plague burns inside its hosts.</summary>
        static void EmberCracks(ArtCanvas c, float cx, float cy, float spread, int count, int seed, float width = 2f)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                var from = new Vector2(cx + ((float)rng.NextDouble() - 0.5f) * spread, cy + ((float)rng.NextDouble() - 0.5f) * spread * 0.6f);
                var dir = ArtCanvas.Rotate(Vector2.right, (float)(rng.NextDouble() * Mathf.PI * 2));
                c.Crack(from, dir, spread * 0.35f, width * 1.8f, EmberDim, seed + i, 5);
                c.Crack(from, dir, spread * 0.35f, width * 0.8f, FlameOrange, seed + i, 5);
            }
        }

        static void AshCap(ArtCanvas c, ArtCanvas.Sdf shape, float minX, float minY, float maxX, float maxY, float fromY, int seed)
        {
            // light layer of fallen ash on the upper part of a shape
            c.Fill(shape, (x, y) =>
            {
                float n = Noise.Fbm(x * 0.12f, y * 0.12f, seed, 3);
                float k = Mathf.Clamp01((y - fromY) / 10f + (n - 0.5f) * 1.5f);
                return AshLight.WithA(k * 0.9f);
            }, minX, minY, maxX, maxY);
        }

        // ------------------------------------------------------------------
        // Пеплоползень — Ash Crawler
        // ------------------------------------------------------------------
        public static ArtCanvas CrawlerBody()
        {
            var c = New(88, 56, 0.5f, 0.1f);
            c.Wobble = 0.8f;
            var shell = ArtCanvas.EllipseSdf(40, 20, 36, 22);
            ArtCanvas.Sdf dome = (x, y) => Mathf.Max(shell(x, y), 8 - y);
            c.Fill(dome, Cylinder(40, 36, Soot, AshDark), 2, 6, 78, 44);
            c.Wobble = 0;
            EmberCracks(c, 40, 22, 40, 3, 21, 1.4f);
            AshCap(c, dome, 2, 6, 78, 44, 26, 4);
            // shell segment lines
            c.Brush(V(26, 10), V(24, 26), V(30, 40), 1.6f, 0.6f, Ink.WithA(0.7f));
            c.Brush(V(46, 10), V(46, 28), V(50, 41), 1.6f, 0.6f, Ink.WithA(0.7f));
            // underside
            c.Box(42, 9, 34, 3, 2, Ink);
            // face peeking from under the shell
            SmallMask(c, 74, 16, 10);
            c.Outline(Ink, 1.3f);
            return c;
        }

        public static ArtCanvas CrawlerLeg()
        {
            var c = New(12, 16, 0.5f, 0.9f);
            c.Capsule(6, 14, 7, 3, 2.2f, 1.4f, Ink);
            return c;
        }

        // ------------------------------------------------------------------
        // Сажевый мотылёк — Soot Moth
        // ------------------------------------------------------------------
        public static ArtCanvas MothBody()
        {
            var c = New(52, 56, 0.5f, 0.5f);
            c.Wobble = 1.4f; c.WobbleScale = 0.3f;
            c.Ellipse(26, 26, 17, 20, VGrad(6, 46, Ink, Soot));
            c.Wobble = 0;
            // fluffy collar
            for (int i = 0; i < 7; i++) c.Circle(14 + i * 4, 34 + Mathf.Sin(i * 1.3f) * 1.5f, 4.5f, AshDark);
            SmallMask(c, 30, 30, 9);
            c.Brush(V(24, 40), V(18, 52), V(10, 50), 1.8f, 0.8f, Ink);
            c.Brush(V(34, 40), V(40, 52), V(48, 50), 1.8f, 0.8f, Ink);
            c.Outline(Ink, 1.2f);
            return c;
        }

        public static ArtCanvas MothWing()
        {
            var c = New(72, 56, 0.08f, 0.35f);
            c.Wobble = 1.2f; c.WobbleScale = 0.2f;
            var pts = new[] { V(4, 18), V(28, 54), V(62, 52), V(70, 36), V(52, 16), V(20, 6) };
            c.Poly(pts, VGrad(0, 54, AshDark, Ash));
            c.Wobble = 0;
            c.Circle(44, 36, 8, AshPale.WithA(0.85f));
            c.Circle(44, 36, 4.5f, Soot);
            c.Circle(45, 37, 1.5f, FlameOrange);
            c.Brush(V(6, 18), V(30, 34), V(60, 46), 1.2f, 0.4f, Ink.WithA(0.6f));
            c.Brush(V(6, 18), V(30, 18), V(54, 18), 1.2f, 0.4f, Ink.WithA(0.6f));
            c.Grain(0.2f, 0.4f, 12);
            c.Outline(Ink, 1.1f);
            return c;
        }

        // ------------------------------------------------------------------
        // Тлеющий страж — Cinder Husk (charging sentinel)
        // ------------------------------------------------------------------
        public static ArtCanvas HuskBody()
        {
            var c = New(90, 120, 0.5f, 0.02f);
            float cx = 45;
            c.Wobble = 0.7f;
            // legs
            c.Capsule(34, 30, 30, 5, 5, 5.5f, Ink);
            c.Capsule(56, 30, 60, 5, 5, 5.5f, Ink);
            // heavy cloak-armour
            c.Poly(new[] { V(18, 22), V(22, 76), V(34, 92), V(58, 92), V(70, 76), V(74, 22), V(62, 16), V(46, 20), V(30, 15) },
                Cylinder(cx, 28, Soot, AshDark));
            c.Wobble = 0;
            EmberCracks(c, cx, 52, 40, 4, 33, 1.5f);
            // pauldrons crusted with ash
            c.Ellipse(24, 80, 12, 9, Ash);
            c.Ellipse(68, 80, 12, 9, Ash);
            AshCap(c, ArtCanvas.EllipseSdf(24, 80, 12, 9), 10, 70, 38, 90, 80, 6);
            AshCap(c, ArtCanvas.EllipseSdf(68, 80, 12, 9), 54, 70, 82, 90, 80, 7);
            // head mask with ash streaks
            c.Wobble = 0.4f;
            c.Ellipse(cx, 102, 15, 16, Cylinder(cx, 15, MaskShade, Mask));
            c.Wobble = 0;
            HollowEyes(c, cx, 100, 6, 3.6f, 6.5f, -0.3f);
            c.Brush(V(38, 116), V(36, 104), V(40, 88), 2.4f, 0.6f, AshDark.WithA(0.8f));
            // two small horns
            c.Brush(V(34, 112), V(26, 120), V(22, 119), 3.5f, 1f, Ink);
            c.Brush(V(56, 112), V(64, 120), V(68, 119), 3.5f, 1f, Ink);
            c.Grain(0.12f, 0.25f, 14);
            c.Outline(Ink, 1.4f);
            return c;
        }

        public static ArtCanvas HuskLance()
        {
            var c = New(120, 20, 0.2f, 0.5f);
            c.Capsule(2, 10, 96, 10, 2.2f, 2.2f, Soot);
            // burnt candle blade
            c.Poly(new[] { V(90, 4), V(118, 10), V(90, 16) }, VGrad(4, 16, WaxDeep, WaxShade));
            c.Capsule(92, 10, 116, 10, 1.1f, 0.3f, FlameOrange);
            c.Outline(Ink, 1.1f);
            return c;
        }

        // ------------------------------------------------------------------
        // Золоплюй — Ash Spitter (bulbous turret)
        // ------------------------------------------------------------------
        public static ArtCanvas SpitterBody()
        {
            var c = New(88, 76, 0.5f, 0.04f);
            float cx = 44;
            c.Wobble = 1f;
            c.Ellipse(cx, 16, 34, 13, Ink);
            c.Ellipse(cx, 36, 30, 30, Cylinder(cx, 30, Soot, AshDark));
            c.Wobble = 0;
            // swollen glowing sac
            c.Ellipse(cx - 4, 40, 18, 18, EmberDim.WithA(0.8f));
            c.Glow(cx - 4, 40, 18, FlameOrange.WithA(0.7f), 1.5f);
            // ribs
            for (int i = -2; i <= 2; i++) c.Brush(V(cx + i * 10, 8), V(cx + i * 13, 36), V(cx + i * 8, 64), 2.4f, 1f, Soot);
            AshCap(c, ArtCanvas.EllipseSdf(cx, 36, 30, 30), 10, 6, 78, 70, 50, 8);
            // mouth crater on top
            c.Ellipse(cx, 64, 11, 5, Ink);
            c.Ellipse(cx, 63, 7, 3, FlameOrange);
            SmallMask(c, cx + 20, 26, 9, 0.1f, true);
            c.Outline(Ink, 1.4f);
            return c;
        }

        // ------------------------------------------------------------------
        // Искра-самоубийца — Ember Wisp
        // ------------------------------------------------------------------
        public static ArtCanvas WispBody()
        {
            var c = New(56, 56, 0.5f, 0.5f);
            c.Glow(28, 28, 27, FlameOrange.WithA(0.7f), 1.8f);
            c.Wobble = 1.5f; c.WobbleScale = 0.25f;
            c.Circle(28, 28, 13, FlameOrange);
            c.Wobble = 0;
            c.Circle(28, 28, 10, FlameYellow);
            SmallMask(c, 28, 27, 8.5f);
            return c;
        }

        // ------------------------------------------------------------------
        // Projectiles & hazards
        // ------------------------------------------------------------------
        public static ArtCanvas AshGlob()
        {
            var c = New(32, 32, 0.5f, 0.5f);
            c.Glow(16, 16, 15, FlameOrange.WithA(0.6f));
            c.Wobble = 1.2f; c.WobbleScale = 0.4f;
            c.Circle(16, 16, 8, AshDark);
            c.Wobble = 0;
            c.Circle(14, 18, 3.5f, FlameOrange);
            return c;
        }

        public static ArtCanvas Feather()
        {
            var c = New(56, 18, 0.5f, 0.5f);
            c.Fill(ArtCanvas.EllipseSdf(28, 9, 26, 6), VGrad(3, 15, AshDark, AshLight), 2, 3, 54, 15);
            c.Line(4, 9, 54, 9, 1.2f, Soot);
            for (int i = 0; i < 7; i++) c.Line(12 + i * 5, 9, 8 + i * 5, 14, 0.8f, Soot.WithA(0.7f));
            c.Capsule(40, 9, 54, 9, 2, 0.4f, FlameOrange);
            c.OuterGlow(FlameOrange.WithA(0.5f), 4);
            return c;
        }

        public static ArtCanvas EmberOrb()
        {
            var c = New(56, 56, 0.5f, 0.5f);
            c.Glow(28, 28, 27, FlameRed.WithA(0.9f), 1.6f);
            c.Circle(28, 28, 12, FlameOrange);
            c.Circle(28, 28, 8, FlameYellow);
            c.Circle(28, 28, 4, FlameCore);
            return c;
        }

        public static ArtCanvas AshSpear()
        {
            var c = New(24, 104, 0.5f, 0.1f);
            c.Poly(new[] { V(12, 2), V(19, 30), V(16, 100), V(8, 100), V(5, 30) }, VGrad(0, 100, Ash, Soot));
            c.Capsule(12, 6, 12, 40, 1.5f, 0.5f, FlameOrange);
            c.OuterGlow(FlameOrange.WithA(0.5f), 5);
            c.Outline(Ink, 1f);
            return c;
        }

        public static ArtCanvas Coal()
        {
            var c = New(44, 44, 0.5f, 0.5f);
            c.Wobble = 1.6f; c.WobbleScale = 0.3f;
            c.Circle(22, 22, 13, Soot);
            c.Wobble = 0;
            EmberCracks(c, 22, 22, 22, 3, 5, 1.3f);
            c.OuterGlow(FlameOrange.WithA(0.8f), 7);
            return c;
        }

        public static ArtCanvas Debris()
        {
            var c = New(44, 40, 0.5f, 0.5f);
            c.Wobble = 1f;
            c.Poly(new[] { V(6, 10), V(14, 34), V(32, 36), V(40, 18), V(28, 4) }, VGrad(0, 36, Soot, Ash));
            AshCap(c, ArtCanvas.PolySdf(new[] { V(6, 10), V(14, 34), V(32, 36), V(40, 18), V(28, 4) }), 0, 0, 44, 40, 28, 2);
            c.Outline(Ink, 1.2f);
            return c;
        }

        /// <summary>Ground shockwave (flame wave running along the floor).</summary>
        public static ArtCanvas Shockwave()
        {
            var c = New(72, 64, 0.5f, 0.02f);
            Teardrop(c, 36, 12, 22, 48, FlameRed.WithA(0.8f), 1.5f);
            Teardrop(c, 36, 10, 16, 36, FlameOrange, 1.4f);
            Teardrop(c, 36, 8, 9, 22, FlameYellow, 1.2f);
            c.OuterGlow(FlameOrange.WithA(0.5f), 6);
            return c;
        }

        /// <summary>Vertical pillar of fire (vents, beams). Tinted/stretched in game.</summary>
        public static ArtCanvas FirePillar()
        {
            var c = New(64, 256, 0.5f, 0f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - 32) / 30f;
                    float n = Noise.Fbm(x * 0.08f, y * 0.03f, 3, 3);
                    float core = Mathf.Clamp01(1f - dx * (1.3f - n * 0.6f));
                    if (core <= 0) continue;
                    Color col = Color.Lerp(FlameRed, FlameYellow, core);
                    col = Color.Lerp(col, FlameCore, Mathf.Clamp01(core * 2f - 1.3f));
                    col.a = Mathf.Clamp01(core * 1.6f) * Mathf.Clamp01((256 - y) / 40f);
                    c.Px[y * 64 + x] = col;
                }
            return c;
        }

        /// <summary>Straight beam used for the Cinder King's sweeping rays (horizontal, pivot left).</summary>
        public static ArtCanvas Beam()
        {
            var c = New(256, 48, 0f, 0.5f);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dy = Mathf.Abs(y + 0.5f - 24) / 23f;
                    float n = Noise.Fbm(x * 0.03f, y * 0.1f, 9, 2);
                    float core = Mathf.Clamp01(1f - dy * (1.2f - n * 0.4f));
                    if (core <= 0) continue;
                    Color col = Color.Lerp(FlameRed, FlameYellow, core);
                    col = Color.Lerp(col, FlameCore, Mathf.Clamp01(core * 2f - 1.2f));
                    col.a = Mathf.Clamp01(core * 1.8f);
                    c.Px[y * 256 + x] = col;
                }
            return c;
        }

        /// <summary>Thin telegraph line (white, tinted in game).</summary>
        public static ArtCanvas TelegraphLine()
        {
            var c = New(64, 8, 0f, 0.5f);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dy = Mathf.Abs(y + 0.5f - 4) / 4f;
                    c.Px[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(1 - dy));
                }
            return c;
        }

        public static ArtCanvas FirePuddle()
        {
            var c = New(96, 24, 0.5f, 0.1f);
            c.Ellipse(48, 6, 44, 5, EmberDim);
            for (int i = 0; i < 6; i++)
                Teardrop(c, 12 + i * 14.5f, 6, 6 + (i % 2) * 2, 10 + (i % 3) * 4, FlameOrange.WithA(0.85f), 1.3f);
            c.Ellipse(48, 5, 36, 3, FlameYellow);
            c.OuterGlow(FlameOrange.WithA(0.5f), 5);
            return c;
        }

        public static ArtCanvas Smoke()
        {
            var c = New(64, 64, 0.5f, 0.5f);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 32f) / 31f, dy = (y - 32f) / 31f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = Noise.Fbm(x * 0.09f, y * 0.09f, 17, 3);
                    float a = Mathf.Clamp01((1f - d) * 1.6f) * Mathf.Clamp01(n * 1.8f - 0.25f);
                    c.Px[y * 64 + x] = new Color(1, 1, 1, a);
                }
            return c;
        }
    }
}
