using UnityEngine;
using static AshenWick.Art.Palette;

namespace AshenWick.Art
{
    public static partial class ArtLibrary
    {
        /// <summary>HUD health unit: a small candle with a living flame.</summary>
        public static ArtCanvas FlameIcon()
        {
            var c = New(40, 64, 0.5f, 0.5f);
            c.Box(20, 16, 8, 14, 4, Cylinder(20, 8, WaxShade, Wax));
            c.Ellipse(20, 29, 7.5f, 2.2f, WaxShade);
            c.Capsule(14, 28, 14, 20, 1.6f, 2, Wax);
            c.Line(20, 30, 20, 35, 1.8f, Ink);
            Teardrop(c, 20, 40, 6, 20, FlameOrange, 1.2f);
            Teardrop(c, 20, 39, 4, 13, FlameYellow, 1.1f);
            Teardrop(c, 20, 38, 2.2f, 6, FlameCore, 1f);
            c.Outline(Ink, 1.5f);
            c.OuterGlow(FlameOrange.WithA(0.5f), 5);
            return c;
        }

        public static ArtCanvas FlameIconEmpty()
        {
            var c = New(40, 64, 0.5f, 0.5f);
            c.Box(20, 16, 8, 14, 4, Cylinder(20, 8, Soot, AshDark));
            c.Ellipse(20, 29, 7.5f, 2.2f, Soot);
            c.Line(20, 30, 20, 35, 1.8f, Ink);
            for (int k = 0; k < 3; k++) c.Circle(20 + Mathf.Sin(k * 2f) * 2, 40 + k * 6, 2 + k, AshDark.WithA(0.5f));
            c.Outline(Ink, 1.5f);
            return c;
        }

        /// <summary>The wax vessel (soul meter analog): ornate ring frame.</summary>
        public static ArtCanvas VesselFrame()
        {
            var c = New(128, 128, 0.5f, 0.5f);
            c.Stroke(ArtCanvas.CircleSdf(64, 64, 50), Ink, 10, 0, 0, 128, 128);
            c.Stroke(ArtCanvas.CircleSdf(64, 64, 50), Mask, 5, 0, 0, 128, 128);
            // horns / drips ornaments (HK-like soul vessel face)
            c.Brush(V(24, 88), V(10, 112), V(30, 124), 7, 2, Mask);
            c.Brush(V(104, 88), V(118, 112), V(98, 124), 7, 2, Mask);
            c.Capsule(40, 16, 40, 4, 3, 4, Mask);
            c.Capsule(88, 18, 88, 2, 3, 4, Mask);
            c.Capsule(64, 14, 64, 8, 3, 3.5f, Mask);
            c.Outline(Ink, 2f);
            return c;
        }

        public static ArtCanvas VesselFill()
        {
            var c = New(128, 128, 0.5f, 0.5f);
            c.Circle(64, 64, 47, (x, y) =>
            {
                float n = Noise.Fbm(x * 0.06f, y * 0.06f, 3, 3);
                return Color.Lerp(WaxShade, WaxWarm, n + (y - 17) / 94f * 0.3f);
            });
            c.Glow(52, 80, 30, Color.white.WithA(0.5f));
            return c;
        }

        public static ArtCanvas VesselBack()
        {
            var c = New(128, 128, 0.5f, 0.5f);
            c.Circle(64, 64, 48, Void.WithA(0.85f));
            return c;
        }

        /// <summary>Team Raspberry logo: a raspberry made of drupelets, with a leaf crown.</summary>
        public static ArtCanvas RaspberryLogo()
        {
            var c = New(256, 256, 0.5f, 0.5f);
            float cx = 128, cy = 112;
            Color berry = Hex("c2224a"), berryLight = Hex("ff5c85"), berryDark = Hex("6e0f2a");
            // drupelets arranged in a berry shape
            for (int row = 0; row < 7; row++)
            {
                float ry = cy + 62 - row * 18;
                int n = row == 0 ? 3 : (row < 5 ? 5 : (row == 5 ? 4 : 3));
                float span = n * 20;
                for (int k = 0; k < n; k++)
                {
                    float rx = cx - span / 2 + 10 + k * 20 + (row % 2) * 4;
                    float r = 13;
                    c.Circle(rx, ry, r, (x, y) =>
                    {
                        float d = Mathf.Sqrt((x - rx + 4) * (x - rx + 4) + (y - ry - 4) * (y - ry - 4)) / r;
                        return Color.Lerp(berryLight, berry, d);
                    });
                    c.Circle(rx - 4, ry + 4, 3, Color.white.WithA(0.75f));
                }
            }
            // leaves
            Color leaf = Hex("3f8f4a"), leafDark = Hex("24552c");
            for (int k = -2; k <= 2; k++)
            {
                float a = k * 0.5f;
                var tip = V(cx + Mathf.Sin(a) * 60, cy + 82 + Mathf.Cos(a) * 36);
                c.Brush(V(cx, cy + 82), V(cx + Mathf.Sin(a) * 30, cy + 102), tip, 18, 2, k % 2 == 0 ? leaf : leafDark);
            }
            c.Capsule(cx, cy + 90, cx + 6, cy + 128, 5, 3, leafDark);
            c.FlatOutline(Hex("1a0610"), 3f);
            return c;
        }

        /// <summary>Ornamental divider for title cards (fleur of wax drops).</summary>
        public static ArtCanvas Divider()
        {
            var c = New(512, 48, 0.5f, 0.5f);
            c.Capsule(40, 24, 472, 24, 1.2f, 1.2f, Color.white);
            c.Brush(V(256, 24), V(210, 40), V(170, 26), 3, 1, Color.white);
            c.Brush(V(256, 24), V(302, 40), V(342, 26), 3, 1, Color.white);
            c.Brush(V(256, 24), V(220, 8), V(186, 20), 3, 1, Color.white);
            c.Brush(V(256, 24), V(292, 8), V(326, 20), 3, 1, Color.white);
            Teardrop(c, 256, 20, 7, 18, Color.white, 1.1f);
            c.Circle(40, 24, 3, Color.white);
            c.Circle(472, 24, 3, Color.white);
            return c;
        }

        public static ArtCanvas WhitePixel()
        {
            var c = New(4, 4, 0.5f, 0.5f);
            c.Clear(Color.white);
            return c;
        }

        /// <summary>Boss health bar frame.</summary>
        public static ArtCanvas BossBarFrame()
        {
            var c = New(512, 40, 0.5f, 0.5f);
            c.Box(256, 20, 240, 7, 3, Ink);
            c.Stroke(ArtCanvas.BoxSdf(256, 20, 240, 7, 3), Mask, 2, 0, 0, 512, 40);
            c.Brush(V(16, 20), V(6, 34), V(20, 36), 4, 1, Mask);
            c.Brush(V(496, 20), V(506, 34), V(492, 36), 4, 1, Mask);
            c.Brush(V(16, 20), V(6, 6), V(20, 4), 4, 1, Mask);
            c.Brush(V(496, 20), V(506, 6), V(492, 4), 4, 1, Mask);
            return c;
        }
    }
}
