using UnityEngine;
using static AshenWick.Art.Palette;

namespace AshenWick.Art
{
    public static partial class ArtLibrary
    {
        // ==================================================================
        // ГОРНАН, Угольный Кузнец — Gornan the Coal Smith
        // A rhinoceros beetle who fed the Great Hearth for a thousand years.
        // ==================================================================

        public static ArtCanvas GornanBody()
        {
            var c = New(280, 230, 0.5f, 0.02f);
            // legs
            c.Wobble = 1f;
            c.Capsule(80, 60, 62, 8, 12, 14, Ink);
            c.Capsule(118, 60, 116, 8, 12, 14, Ink);
            c.Capsule(170, 60, 176, 8, 12, 14, Ink);
            c.Capsule(206, 60, 224, 8, 12, 14, Ink);
            // massive carapace
            var shell = ArtCanvas.EllipseSdf(135, 118, 118, 90);
            ArtCanvas.Sdf body = (x, y) => Mathf.Max(shell(x, y), 42 - y);
            c.Fill(body, Cylinder(125, 118, Void, Soot, -0.5f), 10, 30, 260, 210);
            c.Wobble = 0;
            // coal texture — facets
            var rng = new System.Random(3);
            for (int i = 0; i < 22; i++)
            {
                float px = 40 + (float)rng.NextDouble() * 190, py = 60 + (float)rng.NextDouble() * 130;
                if (shell(px, py) > -8) continue;
                c.Poly(new[] { V(px, py), V(px + 14, py + 8), V(px + 22, py - 4), V(px + 8, py - 12) }, AshDark.WithA(0.35f));
            }
            EmberCracks(c, 130, 120, 170, 9, 77, 2.6f);
            AshCap(c, body, 10, 30, 260, 210, 170, 9);
            // leather apron (front / right)
            c.Wobble = 0.8f;
            c.Poly(new[] { V(170, 150), V(236, 130), V(246, 44), V(200, 34), V(168, 60) }, VGrad(30, 150, Hex("2b1a12"), Hex("4d3020")));
            c.Wobble = 0;
            c.Brush(V(186, 140), V(196, 90), V(190, 44), 2f, 1f, Hex("1b100b"));
            c.Brush(V(214, 132), V(226, 90), V(226, 42), 2f, 1f, Hex("1b100b"));
            // chain belt with coal lumps
            c.Brush(V(40, 70), V(140, 44), V(246, 70), 5f, 5f, Hex("3a3a44"));
            for (int i = 0; i < 6; i++) c.Circle(60 + i * 32, 58 - Mathf.Sin(i / 5f * Mathf.PI) * 12, 6, EmberDim);
            c.Grain(0.15f, 0.12f, 31);
            c.Outline(Ink, 2f);
            return c;
        }

        public static ArtCanvas GornanHead()
        {
            var c = New(130, 120, 0.25f, 0.3f);
            c.Wobble = 0.6f;
            // great horn curving up and forward
            c.Brush(V(40, 70), V(92, 70), V(118, 116), 34, 3, Ink);
            c.Brush(V(46, 76), V(92, 78), V(112, 110), 8, 1.5f, AshDark.WithA(0.7f));
            // helmet-like mask
            c.Ellipse(44, 44, 38, 32, Cylinder(44, 38, MaskShade, Mask));
            c.Wobble = 0;
            // angry slit eyes
            c.Poly(new[] { V(30, 48), V(44, 42), V(46, 36), V(28, 40) }, Eye);
            c.Poly(new[] { V(58, 48), V(76, 42), V(76, 36), V(58, 38) }, Eye);
            c.Circle(38, 41, 1.8f, FlameOrange);
            c.Circle(68, 40, 1.8f, FlameOrange);
            // soot smears and a crack across the mask
            c.Crack(V(20, 60), V(1, -0.8f), 34, 2.2f, Ink, 5, 5);
            c.Brush(V(50, 22), V(58, 18), V(70, 20), 5, 2, AshDark.WithA(0.6f));
            // mandibles
            c.Brush(V(64, 20), V(80, 10), V(90, 16), 7, 2, Ink);
            c.Brush(V(40, 16), V(54, 2), V(66, 6), 7, 2, Ink);
            c.Grain(0.1f, 0.2f, 44);
            c.Outline(Ink, 1.8f);
            return c;
        }

        public static ArtCanvas GornanHammer()
        {
            var c = New(260, 140, 0.06f, 0.5f);
            c.Wobble = 0.6f;
            // arm
            c.Capsule(14, 70, 70, 70, 16, 13, Soot);
            c.Capsule(70, 70, 110, 70, 13, 12, Ink);
            // handle
            c.Capsule(100, 70, 200, 70, 6, 6, Hex("3b2718"));
            for (int i = 0; i < 5; i++) c.Line(104 + i * 6, 62, 108 + i * 6, 78, 2, Hex("7a5a3a"));
            // hammer head (vertical block)
            c.Box(222, 70, 30, 56, 8, Cylinder(222, 30, Void, AshDark, -0.4f));
            c.Wobble = 0;
            // glowing striking faces
            c.Box(222, 124, 26, 4, 2, FlameOrange);
            c.Box(222, 16, 26, 4, 2, FlameOrange);
            EmberCracks(c, 222, 70, 50, 3, 12, 2);
            // rivets
            c.Circle(206, 100, 3, Ash); c.Circle(238, 100, 3, Ash); c.Circle(206, 40, 3, Ash); c.Circle(238, 40, 3, Ash);
            c.Grain(0.12f, 0.2f, 8);
            c.Outline(Ink, 1.8f);
            c.OuterGlow(FlameOrange.WithA(0.35f), 6);
            return c;
        }

        // ==================================================================
        // ТКАЧИХА САЖИ — the Soot Weaver
        // Moth-widow who spins the ash that smothers the kingdom's flames.
        // ==================================================================

        public static ArtCanvas WeaverBody()
        {
            var c = New(150, 220, 0.5f, 0.55f);
            float cx = 75;
            c.Wobble = 1f;
            // long segmented abdomen
            for (int i = 0; i < 6; i++)
            {
                float y = 100 - i * 17;
                float r = 30 - i * 4;
                c.Ellipse(cx, y, r, 13, VGrad(y - 13, y + 13, Ink, Soot));
                c.Ellipse(cx, y + 7, r * 0.8f, 3, AshDark.WithA(0.8f));
            }
            // fluffy thorax
            c.Wobble = 2.4f; c.WobbleScale = 0.25f;
            c.Ellipse(cx, 132, 36, 30, VGrad(100, 162, Soot, Ash));
            c.Wobble = 0;
            for (int i = 0; i < 10; i++) c.Circle(cx - 30 + i * 6.6f, 152 + Mathf.Sin(i * 1.9f) * 3, 7, AshLight);
            // thin legs dangling
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 3; i++)
                    c.Brush(V(cx + s * 20, 124 - i * 8), V(cx + s * (48 + i * 6), 118 - i * 12), V(cx + s * (40 + i * 10), 70 - i * 18), 3, 1.2f, Ink);
            c.Grain(0.14f, 0.2f, 60);
            c.Outline(Ink, 1.8f);
            return c;
        }

        public static ArtCanvas WeaverWing()
        {
            var c = New(280, 210, 0.04f, 0.72f);
            // organic outline: curved leading edge, rounded tip, scalloped trailing edge
            var pts = new System.Collections.Generic.List<Vector2>();
            Vector2 root = V(8, 150);
            for (int i = 0; i <= 14; i++) pts.Add(ArtCanvas.Bezier(root, V(90, 214), V(250, 196), i / 14f));
            for (int i = 1; i <= 8; i++) pts.Add(ArtCanvas.Bezier(V(250, 196), V(292, 170), V(262, 110), i / 8f));
            int sc = 7;
            for (int k = 0; k < sc; k++)
            {
                for (int i = 1; i <= 6; i++)
                {
                    float tt = (k + i / 6f) / sc;
                    Vector2 p = ArtCanvas.Bezier(V(262, 110), V(170, 10), V(30, 70), tt);
                    float bump = Mathf.Sin(i / 6f * Mathf.PI) * 11f;
                    Vector2 n = new Vector2(p.x - 150f, p.y - 150f).normalized;
                    pts.Add(p + n * bump);
                }
            }
            pts.Add(ArtCanvas.Bezier(V(30, 70), V(10, 110), root, 0.5f));
            var poly = pts.ToArray();
            var sdf = ArtCanvas.PolySdf(poly);
            c.Wobble = 1.2f; c.WobbleScale = 0.1f;
            c.Fill(sdf, (x, y) =>
            {
                float n = Noise.Fbm(x * 0.02f, y * 0.02f, 88, 4);
                float d = Mathf.Sqrt((x - 8) * (x - 8) + (y - 150) * (y - 150)) / 260f;
                Color col = Color.Lerp(Soot, Ash, Mathf.Clamp01(d * 0.9f + n * 0.5f - 0.15f));
                return col;
            }, 0, 0, 280, 210);
            c.Wobble = 0;
            // dusty pale band near the edge
            c.Stroke(sdf, AshLight.WithA(0.35f), 9, 0, 0, 280, 210);
            // veins
            c.Brush(V(10, 150), V(120, 176), V(248, 188), 3, 1, Ink.WithA(0.7f));
            c.Brush(V(10, 150), V(130, 150), V(262, 130), 3, 1, Ink.WithA(0.7f));
            c.Brush(V(10, 150), V(110, 110), V(210, 60), 3, 1, Ink.WithA(0.7f));
            c.Brush(V(10, 150), V(70, 100), V(110, 44), 3, 1, Ink.WithA(0.7f));
            c.Brush(V(10, 150), V(40, 110), V(46, 70), 3, 1, Ink.WithA(0.6f));
            // eye spot — pale ash ring with an ember pupil
            c.Circle(172, 146, 30, AshPale.WithA(0.9f));
            c.Circle(172, 146, 22, Soot);
            c.Circle(172, 146, 12, EmberDim);
            c.Circle(176, 150, 5, FlameYellow);
            c.Circle(110, 84, 13, AshPale.WithA(0.6f));
            c.Circle(110, 84, 7, Soot);
            // soot speckles
            var rng = new System.Random(5);
            for (int i = 0; i < 60; i++)
            {
                float x = (float)rng.NextDouble() * 280, y = (float)rng.NextDouble() * 210;
                if (sdf(x, y) < -4) c.Circle(x, y, 1 + (float)rng.NextDouble() * 2, Ink.WithA(0.35f));
            }
            c.Grain(0.18f, 0.18f, 71);
            c.Outline(Ink, 1.6f);
            return c;
        }

        public static ArtCanvas WeaverMask()
        {
            var c = New(100, 130, 0.5f, 0.3f);
            float cx = 50;
            // feathery antennae
            for (int s = -1; s <= 1; s += 2)
            {
                c.Brush(V(cx + s * 10, 96), V(cx + s * 30, 126), V(cx + s * 48, 120), 3, 1.2f, Ink);
                for (int i = 1; i < 7; i++)
                {
                    var p = ArtCanvas.Bezier(V(cx + s * 10, 96), V(cx + s * 30, 126), V(cx + s * 48, 120), i / 7f);
                    c.Line(p.x, p.y, p.x + s * 5, p.y - 7, 1.2f, Soot);
                }
            }
            c.Wobble = 0.5f;
            // long elegant mask, tapering downward
            Teardrop(c, cx, 70, 26, 34, Cylinder(cx, 26, MaskShade, Mask), 0.7f);
            c.Fill(TeardropSdf(cx, 70, 26, 70, 1.2f), Cylinder(cx, 26, MaskShade, Mask), cx - 26, 0, cx + 26, 70);
            c.Wobble = 0;
            // four eyes
            HollowEyes(c, cx, 76, 11, 6, 10, 0.25f);
            HollowEyes(c, cx, 54, 7, 3, 5, 0.1f);
            // veil-like soot tears
            c.Brush(V(cx - 11, 64), V(cx - 13, 40), V(cx - 9, 16), 2.4f, 0.4f, AshDark.WithA(0.8f));
            c.Brush(V(cx + 11, 64), V(cx + 13, 40), V(cx + 9, 16), 2.4f, 0.4f, AshDark.WithA(0.8f));
            c.Grain(0.08f, 0.2f, 21);
            c.Outline(Ink, 1.6f);
            return c;
        }

        // ==================================================================
        // КОРОЛЬ-ОГАРОК — the Cinder King (the Tallow King, burnt to a stub)
        // ==================================================================

        public static ArtCanvas KingBody()
        {
            var c = New(190, 250, 0.5f, 0.02f);
            float cx = 95;
            c.Wobble = 1.2f;
            // tattered cape behind
            c.Poly(new[] { V(40, 190), V(150, 190), V(176, 60), V(184, 4), V(160, 16), V(146, 2), V(126, 18), V(104, 0), V(84, 16), V(62, 2), V(44, 18), V(20, 4), V(10, 60) },
                VGrad(0, 190, Void, Hex("2a1c26")));
            c.Wobble = 0.6f;
            // legs in wax greaves
            c.Capsule(76, 70, 70, 10, 12, 13, Cylinder(72, 13, WaxDeep, WaxShade));
            c.Capsule(114, 70, 120, 10, 12, 13, Cylinder(118, 13, WaxDeep, WaxShade));
            c.Ellipse(68, 8, 17, 7, Soot);
            c.Ellipse(122, 8, 17, 7, Soot);
            // melting wax cuirass
            c.Poly(new[] { V(52, 70), V(46, 150), V(66, 196), V(124, 196), V(144, 150), V(138, 70), V(95, 56) },
                Cylinder(cx, 48, WaxDeep, Wax));
            c.Wobble = 0;
            // runs of molten wax
            float[] dx = { 60, 76, 98, 118, 132 };
            float[] dl = { 60, 90, 70, 100, 50 };
            for (int i = 0; i < dx.Length; i++)
                c.Capsule(dx[i], 190, dx[i] + 1, 190 - dl[i], 3.5f, 5f, Cylinder(dx[i], 5, WaxShade, Wax, -0.6f));
            // burnt heart: hole in the chest with the dying ember inside
            c.Ellipse(cx, 138, 18, 22, Void);
            c.Glow(cx, 136, 22, FlameRed, 1.4f);
            c.Circle(cx, 136, 8, FlameOrange);
            c.Circle(cx, 136, 4, FlameCore);
            EmberCracks(c, cx, 120, 70, 5, 99, 1.8f);
            // ash crust on shoulders
            c.Ellipse(52, 186, 24, 16, Ash);
            c.Ellipse(138, 186, 24, 16, Ash);
            AshCap(c, ArtCanvas.EllipseSdf(52, 186, 24, 16), 26, 168, 78, 204, 184, 3);
            AshCap(c, ArtCanvas.EllipseSdf(138, 186, 24, 16), 112, 168, 164, 204, 184, 4);
            c.Grain(0.12f, 0.15f, 17);
            c.Outline(Ink, 2f);
            return c;
        }

        public static ArtCanvas KingHead()
        {
            var c = New(120, 150, 0.5f, 0.12f);
            float cx = 60;
            // crown of burnt wicks
            float[] wx = { 30, 44, 60, 76, 90 };
            float[] wh = { 22, 34, 44, 34, 22 };
            for (int i = 0; i < 5; i++)
            {
                c.Brush(V(wx[i], 88), V(wx[i] + (i - 2) * 3, 88 + wh[i] * 0.6f), V(wx[i] + (i - 2) * 5, 88 + wh[i]), 5, 2.6f, Ink);
                float fx = wx[i] + (i - 2) * 5, fy = 88 + wh[i];
                Teardrop(c, fx, fy + 3, 4.5f, 12, FlameOrange);
                Teardrop(c, fx, fy + 2, 2.6f, 7, FlameYellow);
            }
            c.Box(cx, 88, 34, 6, 3, Hex("3c3036"));
            c.Wobble = 0.5f;
            // tall royal mask
            c.Box(cx, 54, 30, 36, 20, Cylinder(cx, 30, MaskShade, Mask));
            c.Wobble = 0;
            // hollow eyes, one weeping molten wax
            HollowEyes(c, cx, 58, 12, 7, 12, 0.18f);
            c.Capsule(cx + 12, 46, cx + 13, 24, 2.2f, 3f, WaxWarm);
            // great crack
            c.Crack(V(cx - 6, 88), V(-0.2f, -1), 60, 2.4f, Ink, 12, 6);
            c.Grain(0.08f, 0.2f, 77);
            c.Outline(Ink, 1.8f);
            return c;
        }

        public static ArtCanvas KingSword()
        {
            var c = New(330, 56, 0.08f, 0.5f);
            // gauntlet + grip
            c.Wobble = 0.5f;
            c.Ellipse(22, 28, 18, 16, Cylinder(22, 18, WaxDeep, WaxShade));
            c.Wobble = 0;
            c.Capsule(20, 28, 54, 28, 5, 5, Soot);
            // crossguard of twisted wick
            c.Capsule(56, 8, 56, 48, 5, 5, Ink);
            c.Circle(56, 8, 6, EmberDim); c.Circle(56, 48, 6, EmberDim);
            // blade: pale wax bone with molten core
            c.Poly(new[] { V(62, 16), V(300, 20), V(328, 28), V(300, 36), V(62, 40) }, VGrad(16, 40, WaxShade, Wax));
            c.Capsule(66, 28, 306, 28, 3.2f, 1f, FlameOrange);
            c.Capsule(66, 28, 300, 28, 1.4f, 0.4f, FlameCore);
            // drips along the edge
            for (int i = 0; i < 6; i++) c.Capsule(90 + i * 36, 17, 90 + i * 36, 10 - (i % 3) * 3, 2, 2.4f, WaxShade);
            c.Outline(Ink, 1.6f);
            c.OuterGlow(FlameOrange.WithA(0.35f), 7);
            return c;
        }
    }
}
