using System;
using UnityEngine;
using static AshenWick.Art.Palette;

namespace AshenWick.Art
{
    /// <summary>Visual theme of an area.</summary>
    public sealed class Theme
    {
        public Color RockDeep, Rock, Rim, Ash, Sky, SkyTop, Fog, Glow;
        public int Seed;

        public static Theme For(int area)
        {
            switch (area)
            {
                case 1: // Воскоплавильни — Waxworks
                    return new Theme { RockDeep = Hex("0d0808"), Rock = Hex("2a1a16"), Rim = Hex("6b3a22"), Ash = Hex("b9a998"), Sky = Hex("1a0e0c"), SkyTop = Hex("3d1f16"), Fog = Hex("6e3a24"), Glow = WaxworksGlow, Seed = 200 };
                case 2: // Собор Угасших — Cathedral of the Snuffed
                    return new Theme { RockDeep = Hex("07080f"), Rock = Hex("1a1c2c"), Rim = Hex("4a5078"), Ash = Hex("c9cbe0"), Sky = Hex("0c0d18"), SkyTop = Hex("262a45"), Fog = Hex("3e4466"), Glow = CathedralGlow, Seed = 300 };
                case 3: // Сердце Горна — Heart of the Hearth
                    return new Theme { RockDeep = Hex("0a0303"), Rock = Hex("2a0f0b"), Rim = Hex("8a2d14"), Ash = Hex("d6b8a8"), Sky = Hex("140504"), SkyTop = Hex("4a150c"), Fog = Hex("7a2a14"), Glow = HearthGlow, Seed = 400 };
                default: // Пепельные Поля — Ashen Fields
                    return new Theme { RockDeep = Hex("0b0b10"), Rock = Hex("26252e"), Rim = Hex("5b5968"), Ash = Hex("d9d6dc"), Sky = Hex("1d1c26"), SkyTop = Hex("4a4858"), Fog = Hex("6e6a78"), Glow = Hex("e8a060"), Seed = 100 };
            }
        }
    }

    public static partial class ArtLibrary
    {
        public const int TilePx = 24;

        /// <summary>
        /// Paints the whole terrain of a room into one canvas.
        /// kind(x,y): 0 air, 1 solid, 2 one-way platform, 3 spikes, 4 molten wax (lava).
        /// </summary>
        public static ArtCanvas Terrain(int tw, int th, Func<int, int, int> kind, Theme t)
        {
            int T = TilePx;
            int W = tw * T, H = th * T;
            var c = new ArtCanvas(W, H) { Pivot = Vector2.zero, PPU = T };
            int seed = t.Seed;

            // 1) solid mask with crumbly, noise-perturbed edges
            var mask = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float nx = (Noise.Fbm(x * 0.045f, y * 0.045f, seed, 3) - 0.5f) * T * 0.55f;
                    float ny = (Noise.Fbm(x * 0.045f + 50, y * 0.045f, seed + 9, 3) - 0.5f) * T * 0.55f;
                    int gx = (int)Math.Floor((x + nx) / T), gy = (int)Math.Floor((y + ny) / T);
                    gx = Mathf.Clamp(gx, 0, tw - 1); gy = Mathf.Clamp(gy, 0, th - 1);
                    // keep true tile shapes near the collision boundary: only allow perturbation
                    // when the real tile agrees within the band (prevents invisible walls / floating floors)
                    int real = kind(Mathf.Clamp(x / T, 0, tw - 1), Mathf.Clamp(y / T, 0, th - 1));
                    bool s = kind(gx, gy) == 1;
                    if (real == 1)
                    {
                        // solid tiles: erode only a few pixels at most
                        int lx = x % T, ly = y % T;
                        bool edgeBand = lx < 3 || ly < 3 || lx >= T - 3 || ly >= T - 3;
                        s = s || !edgeBand;
                    }
                    else
                    {
                        // air tiles: allow a little overhang but not much
                        int lx = x % T, ly = y % T;
                        bool edgeBand = lx < 3 || ly < 3 || lx >= T - 3 || ly >= T - 3;
                        s = s && edgeBand;
                    }
                    mask[y * W + x] = s;
                }

            // 2) distance from each solid pixel to air (chamfer)
            var dist = new float[W * H];
            const float big = 1e5f;
            for (int i = 0; i < dist.Length; i++) dist[i] = mask[i] ? big : 0f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; float v = dist[i]; if (v == 0f) continue;
                    if (x > 0) v = Mathf.Min(v, dist[i - 1] + 1f);
                    if (y > 0) { v = Mathf.Min(v, dist[i - W] + 1f); if (x > 0) v = Mathf.Min(v, dist[i - W - 1] + 1.414f); if (x < W - 1) v = Mathf.Min(v, dist[i - W + 1] + 1.414f); }
                    dist[i] = v;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x; float v = dist[i]; if (v == 0f) continue;
                    if (x < W - 1) v = Mathf.Min(v, dist[i + 1] + 1f);
                    if (y < H - 1) { v = Mathf.Min(v, dist[i + W] + 1f); if (x < W - 1) v = Mathf.Min(v, dist[i + W + 1] + 1.414f); if (x > 0) v = Mathf.Min(v, dist[i + W - 1] + 1.414f); }
                    dist[i] = v;
                }

            // 3) shade rock
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!mask[i]) continue;
                    float d = dist[i];
                    float strata = Noise.Fbm(x * 0.01f, y * 0.06f, seed + 3, 3);
                    float grain = Noise.Value2(x * 0.5f, y * 0.5f, seed + 5);
                    Color col = Color.Lerp(t.Rock, t.RockDeep, Mathf.Clamp01(d / 30f));
                    col = Color.Lerp(col, t.RockDeep, (strata - 0.5f) * 0.9f + 0.2f);
                    col = col.Mul(0.92f + grain * 0.16f);
                    if (d < 7f) col = Color.Lerp(t.Rim, col, d / 7f);
                    if (d < 2.2f) col = Color.Lerp(Ink, col, (d - 0.5f) / 1.7f);

                    // ash blanket on upward facing surfaces
                    int airAbove = 0;
                    for (int k = 1; k <= 12; k++)
                    {
                        int yy = y + k;
                        if (yy >= H || !mask[yy * W + x]) { airAbove = k; break; }
                    }
                    if (airAbove > 0)
                    {
                        float depth = 7f + Noise.Value1(x * 0.15f, seed + 7) * 6f;
                        if (airAbove <= depth)
                        {
                            float ak = 1f - airAbove / (depth + 1f);
                            Color ash = Color.Lerp(t.Ash.Mul(0.7f), t.Ash, ak + grain * 0.2f);
                            col = Color.Lerp(col, ash, Mathf.Clamp01(ak * 1.6f));
                        }
                    }
                    c.Px[i] = col;
                }

            // 4) platforms, spikes, molten wax
            var rng = new System.Random(seed + 13);
            for (int gy = 0; gy < th; gy++)
                for (int gx = 0; gx < tw; gx++)
                {
                    int k = kind(gx, gy);
                    float bx = gx * T, by = gy * T;
                    if (k == 2)
                    {
                        // charred beam platform
                        c.Box(bx + T / 2f, by + T - 5, T / 2f + 1, 4.5f, 1.5f, VGrad(by + T - 10, by + T, t.RockDeep, t.Rim));
                        c.Box(bx + T / 2f, by + T - 1.5f, T / 2f + 1, 1.5f, 1f, t.Ash.WithA(0.9f));
                        if (gx % 3 == 0) c.Capsule(bx + T / 2f, by + T - 8, bx + T / 2f + 1, by + T - 16 - (float)rng.NextDouble() * 12f, 1.3f, 1f, Ink);
                    }
                    else if (k == 3)
                    {
                        // ash thorns: jagged black crystals with pale tips
                        for (int s = 0; s < 3; s++)
                        {
                            float sx = bx + 4 + s * 8 + (float)rng.NextDouble() * 2;
                            float sh = 12 + (float)rng.NextDouble() * 10;
                            float lean = ((float)rng.NextDouble() - 0.5f) * 5;
                            c.Poly(new[] { V(sx - 4.5f, by), V(sx + lean, by + sh), V(sx + 4.5f, by) }, VGrad(by, by + sh, Ink, t.Rim));
                            c.Line(sx + lean * 0.8f, by + sh - 1, sx + lean * 0.5f, by + sh * 0.6f, 1.2f, t.Ash);
                        }
                    }
                    else if (k == 4)
                    {
                        bool top = gy + 1 >= th || kind(gx, gy + 1) != 4;
                        for (int yy = 0; yy < T; yy++)
                            for (int xx = 0; xx < T; xx++)
                            {
                                float fy = yy / (float)T;
                                float n = Noise.Fbm((bx + xx) * 0.05f, (by + yy) * 0.08f, seed + 21, 2);
                                Color col = Color.Lerp(FlameRed, FlameOrange, n);
                                if (top) col = Color.Lerp(col, FlameYellow, Mathf.Clamp01((fy - 0.55f) * 3f));
                                if (top && fy > 0.8f + (n - 0.5f) * 0.2f) continue;
                                c.Px[(int)(by + yy) * W + (int)(bx + xx)] = col;
                            }
                    }
                }
            return c;
        }

        // ------------------------------------------------------------------
        // Parallax backgrounds (tileable horizontally)
        // ------------------------------------------------------------------

        public const int BgW = 1024, BgH = 512;

        /// <summary>Sky layer: vertical gradient + distant light source + haze.</summary>
        public static ArtCanvas Sky(int area)
        {
            var t = Theme.For(area);
            var c = new ArtCanvas(256, 256) { PPU = 8f };
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float fy = y / 255f;
                    Color col = Color.Lerp(t.Sky, t.SkyTop, Mathf.Pow(fy, 0.8f));
                    float haze = Noise.Fbm(x * 0.02f, y * 0.05f, t.Seed + 1, 3);
                    col = Color.Lerp(col, t.Fog, (haze - 0.4f) * 0.35f);
                    c.Px[y * 256 + x] = new Color(col.r, col.g, col.b, 1f);
                }
            // dim sun / furnace glow
            float gx = area == 2 ? 128 : 170, gy = area == 3 ? 60 : 170;
            c.Glow(gx, gy, 120, t.Glow.WithA(area == 2 ? 0.25f : 0.35f), 2f);
            c.Glow(gx, gy, 30, t.Glow.WithA(area == 2 ? 0.2f : 0.5f), 1.5f);
            return c;
        }

        /// <summary>Distant/mid/near silhouette layer. depth 0 = farthest.</summary>
        public static ArtCanvas Backdrop(int area, int depth)
        {
            var t = Theme.For(area);
            var c = new ArtCanvas(BgW, BgH) { Pivot = new Vector2(0.5f, 0f), PPU = 32f };
            var rng = new System.Random(t.Seed * 7 + depth * 31);
            float fade = depth == 0 ? 0.55f : (depth == 1 ? 0.3f : 0.08f);
            Color sil = Color.Lerp(t.RockDeep, t.Fog, fade);
            Color silTop = Color.Lerp(sil, t.Fog, 0.25f);
            ArtCanvas.Paint paint = VGrad(0, BgH * 0.8f, sil, silTop);

            // continuous ground band (tileable: uses periodic noise via sin)
            float baseH = depth == 0 ? 140 : (depth == 1 ? 90 : 40);
            for (int x = 0; x < BgW; x++)
            {
                float u = x / (float)BgW * Mathf.PI * 2;
                float h = baseH + Mathf.Sin(u * 2 + depth) * 18 + Mathf.Sin(u * 5 + 1.3f) * 9 + Mathf.Sin(u * 11) * 4;
                for (int y = 0; y < h; y++) c.Blend(x, y, paint(x, y), 1f);
            }

            int count = depth == 0 ? 7 : (depth == 1 ? 5 : 4);
            for (int i = 0; i < count; i++)
            {
                float x = (i + 0.2f + (float)rng.NextDouble() * 0.6f) * BgW / count;
                float scale = depth == 0 ? 0.8f : (depth == 1 ? 1.1f : 1.5f);
                switch (area)
                {
                    case 0: FieldsShape(c, x, baseH - 10, scale, rng, paint, t, depth); break;
                    case 1: WaxworksShape(c, x, baseH - 10, scale, rng, paint, t, depth); break;
                    case 2: CathedralShape(c, x, baseH - 10, scale, rng, paint, t, depth); break;
                    default: HearthShape(c, x, baseH - 10, scale, rng, paint, t, depth); break;
                }
            }
            // atmospheric fade at the bottom so layers blend into fog
            if (depth < 2) c.Grain(0.06f, 0.05f, t.Seed + depth);
            return c;
        }

        static void FieldsShape(ArtCanvas c, float x, float gy, float s, System.Random r, ArtCanvas.Paint p, Theme t, int depth)
        {
            int kind = r.Next(3);
            if (kind == 0 || depth == 0)
            {
                // giant melted candle stub — remains of the old kingdom's lights
                float w = (40 + (float)r.NextDouble() * 40) * s, h = (120 + (float)r.NextDouble() * 180) * s;
                c.Box(x, gy + h / 2, w / 2, h / 2, w * 0.2f, p);
                c.Ellipse(x, gy + h, w / 2, w * 0.12f, p);
                for (int k = 0; k < 4; k++)
                {
                    float dx = x - w / 2 + (float)r.NextDouble() * w;
                    c.Capsule(dx, gy + h, dx, gy + h - (20 + (float)r.NextDouble() * 60) * s, 5 * s, 7 * s, p);
                }
                c.Brush(V(x, gy + h), V(x + 4, gy + h + 10 * s), V(x + 2, gy + h + 18 * s), 4 * s, 2 * s, p(x, gy + h));
                if (depth == 0)
                {
                    // thin trails of smoke from the dead wick
                    for (int k = 0; k < 6; k++)
                        c.Circle(x + Mathf.Sin(k * 1.3f) * 8 * s + k * 3, gy + h + 24 * s + k * 16 * s, (6 + k * 2) * s, t.Fog.WithA(0.15f));
                }
            }
            else if (kind == 1)
            {
                // dead tree, branches like cracks
                c.Capsule(x, gy, x + 6, gy + 160 * s, 10 * s, 3 * s, p);
                for (int k = 0; k < 5; k++)
                {
                    float by = gy + (60 + k * 22) * s;
                    float dir = k % 2 == 0 ? 1 : -1;
                    c.Brush(V(x + 3, by), V(x + dir * 30 * s, by + 20 * s), V(x + dir * (50 + k * 6) * s, by + 40 * s), 6 * s, 1.5f, p(x, by));
                }
            }
            else
            {
                // leaning spire of a ruined chapel
                float w = 40 * s, h = 240 * s;
                c.Poly(new[] { V(x - w, gy), V(x - w * 0.8f, gy + h * 0.7f), V(x + 6, gy + h), V(x + w * 0.8f, gy + h * 0.66f), V(x + w, gy) }, p);
                c.Ellipse(x, gy + h * 0.5f, 8 * s, 14 * s, t.Glow.WithA(depth == 0 ? 0.25f : 0.4f));
            }
        }

        static void WaxworksShape(ArtCanvas c, float x, float gy, float s, System.Random r, ArtCanvas.Paint p, Theme t, int depth)
        {
            int kind = r.Next(3);
            if (kind == 0)
            {
                // huge vat with molten wax glow
                float w = 110 * s, h = 120 * s;
                c.Box(x, gy + h / 2, w / 2, h / 2, 20 * s, p);
                c.Box(x, gy + h, w / 2 + 10 * s, 8 * s, 4, p);
                c.Ellipse(x, gy + h + 8 * s, w / 2 - 6 * s, 6 * s, t.Glow.WithA(0.7f));
                c.Glow(x, gy + h + 10 * s, w * 0.7f, t.Glow.WithA(0.3f), 2);
                c.Capsule(x - w / 2, gy + h * 0.7f, x - w, gy + h * 1.6f, 6 * s, 6 * s, p);
            }
            else if (kind == 1)
            {
                // chimney stacks
                float w = 26 * s, h = 300 * s;
                c.Box(x, gy + h / 2, w, h / 2, 2, p);
                c.Box(x, gy + h, w + 6 * s, 8 * s, 2, p);
                for (int k = 0; k < 5; k++) c.Circle(x + k * 10 * s, gy + h + 20 * s + k * 18 * s, (12 + k * 5) * s, t.Fog.WithA(0.18f));
            }
            else
            {
                // hanging chains and a mold
                for (int k = 0; k < 3; k++)
                {
                    float cx = x + (k - 1) * 30 * s;
                    for (int y = BgH; y > gy + 120 * s; y -= (int)(10 * s + 1))
                        c.Ellipse(cx, y, 3 * s, 5 * s, p);
                }
                c.Box(x, gy + 110 * s, 50 * s, 14 * s, 4, p);
            }
        }

        static void CathedralShape(ArtCanvas c, float x, float gy, float s, System.Random r, ArtCanvas.Paint p, Theme t, int depth)
        {
            int kind = r.Next(2);
            float w = 70 * s, h = 360 * s;
            if (kind == 0)
            {
                // gothic window arch with pale stained light
                c.Box(x - w, gy + h / 2, 12 * s, h / 2, 2, p);
                c.Box(x + w, gy + h / 2, 12 * s, h / 2, 2, p);
                c.Poly(new[] { V(x - w - 12 * s, gy + h * 0.72f), V(x, gy + h), V(x + w + 12 * s, gy + h * 0.72f), V(x + w - 6, gy + h * 0.72f), V(x, gy + h * 0.9f), V(x - w + 6, gy + h * 0.72f) }, p);
                var win = ArtCanvas.BoxSdf(x, gy + h * 0.45f, w * 0.55f, h * 0.25f, w * 0.5f);
                c.Fill(win, (px, py) => Color.Lerp(t.Glow.WithA(0.25f), t.Glow.WithA(0.5f), Noise.Value2(px * 0.05f, py * 0.05f, 5)), x - w, gy, x + w, gy + h);
                c.Line(x, gy + h * 0.2f, x, gy + h * 0.7f, 3 * s, p(x, gy));
                c.Line(x - w * 0.55f, gy + h * 0.45f, x + w * 0.55f, gy + h * 0.45f, 3 * s, p(x, gy));
            }
            else
            {
                // column with a hanging censer
                c.Box(x, gy + h / 2, 22 * s, h / 2, 4, p);
                c.Box(x, gy + 10 * s, 34 * s, 10 * s, 3, p);
                c.Line(x + 50 * s, BgH, x + 50 * s, gy + 200 * s, 2 * s, p(x, gy));
                c.Ellipse(x + 50 * s, gy + 190 * s, 14 * s, 12 * s, p);
                c.Glow(x + 50 * s, gy + 188 * s, 26 * s, t.Glow.WithA(0.35f), 2);
            }
        }

        static void HearthShape(ArtCanvas c, float x, float gy, float s, System.Random r, ArtCanvas.Paint p, Theme t, int depth)
        {
            int kind = r.Next(2);
            if (kind == 0)
            {
                // massive furnace pillar with glowing grilles
                float w = 60 * s, h = 380 * s;
                c.Box(x, gy + h / 2, w / 2, h / 2, 6, p);
                for (int k = 0; k < 4; k++) c.Box(x, gy + 60 * s + k * 70 * s, w * 0.3f, 6 * s, 2, t.Glow.WithA(0.55f));
                c.Glow(x, gy + 150 * s, w * 1.4f, t.Glow.WithA(0.18f), 2);
            }
            else
            {
                // cracked rock spire with magma veins
                float h = 260 * s;
                c.Poly(new[] { V(x - 50 * s, gy), V(x - 10 * s, gy + h), V(x + 14 * s, gy + h * 0.9f), V(x + 46 * s, gy) }, p);
                c.Crack(V(x, gy + 10), V(0.1f, 1), h * 0.8f, 3 * s, t.Glow.WithA(0.6f), (int)x, 7);
            }
        }

        // ------------------------------------------------------------------
        // Props
        // ------------------------------------------------------------------

        /// <summary>Подсвечник — iron candelabra, the resting place (bench).</summary>
        public static ArtCanvas Candelabra()
        {
            var c = New(128, 150, 0.5f, 0.02f);
            float cx = 64;
            c.Wobble = 0.4f;
            // tripod foot
            c.Poly(new[] { V(30, 2), V(cx, 30), V(98, 2), V(90, 2), V(cx, 22), V(38, 2) }, Soot);
            c.Capsule(cx, 20, cx, 96, 4.5f, 3.5f, Soot);
            // knot ornaments
            c.Circle(cx, 40, 7, AshDark); c.Circle(cx, 72, 6, AshDark);
            // arms
            c.Brush(V(cx, 90), V(cx - 38, 88), V(cx - 40, 112), 5, 4, Soot);
            c.Brush(V(cx, 90), V(cx + 38, 88), V(cx + 40, 112), 5, 4, Soot);
            // cups
            c.Box(cx - 40, 114, 10, 4, 2, AshDark);
            c.Box(cx + 40, 114, 10, 4, 2, AshDark);
            c.Box(cx, 104, 11, 4, 2, AshDark);
            c.Wobble = 0;
            // candles
            c.Box(cx - 40, 126, 6, 10, 3, Cylinder(cx - 40, 6, WaxShade, Wax));
            c.Box(cx + 40, 124, 6, 8, 3, Cylinder(cx + 40, 6, WaxShade, Wax));
            c.Box(cx, 120, 7, 14, 3, Cylinder(cx, 7, WaxShade, Wax));
            c.Line(cx - 40, 136, cx - 40, 140, 1.6f, Ink);
            c.Line(cx + 40, 132, cx + 40, 136, 1.6f, Ink);
            c.Line(cx, 134, cx, 139, 1.6f, Ink);
            // soft seat: an ash-covered cushion at the base (where Niv rests)
            c.Ellipse(cx, 6, 34, 5, AshLight.WithA(0.8f));
            c.Grain(0.08f, 0.3f, 2);
            c.Outline(Ink, 1.3f);
            return c;
        }

        /// <summary>Stone tablet with glowing glyphs (lore).</summary>
        public static ArtCanvas Tablet()
        {
            var c = New(72, 96, 0.5f, 0.02f);
            c.Wobble = 1f;
            c.Poly(new[] { V(10, 2), V(8, 70), V(20, 90), V(52, 92), V(64, 72), V(62, 2) }, VGrad(0, 92, Soot, AshDark));
            c.Wobble = 0;
            var rng = new System.Random(4);
            for (int row = 0; row < 6; row++)
                for (int k = 0; k < 4; k++)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    float x = 18 + k * 11, y = 70 - row * 10;
                    c.Line(x, y, x + 6, y + (float)rng.NextDouble() * 4 - 2, 1.6f, WaxWarm.WithA(0.75f));
                }
            AshCap(c, ArtCanvas.PolySdf(new[] { V(10, 2), V(8, 70), V(20, 90), V(52, 92), V(64, 72), V(62, 2) }), 0, 0, 72, 96, 80, 3);
            c.Outline(Ink, 1.4f);
            return c;
        }

        /// <summary>Altar holding an ability (glowing ember relic floating above).</summary>
        public static ArtCanvas Altar()
        {
            var c = New(96, 80, 0.5f, 0.02f);
            c.Wobble = 0.8f;
            c.Poly(new[] { V(12, 2), V(20, 20), V(28, 24), V(30, 56), V(66, 56), V(68, 24), V(76, 20), V(84, 2) }, VGrad(0, 56, Soot, AshDark));
            c.Box(48, 60, 26, 5, 2, AshDark);
            c.Wobble = 0;
            c.Brush(V(34, 50), V(48, 30), V(62, 50), 2, 2, WaxWarm.WithA(0.6f));
            c.Circle(48, 38, 4, FlameOrange.WithA(0.8f));
            AshCap(c, ArtCanvas.BoxSdf(48, 60, 26, 5, 2), 20, 50, 76, 70, 60, 1);
            c.Outline(Ink, 1.3f);
            return c;
        }

        /// <summary>Wax shard — increases Niv's maximum flame.</summary>
        public static ArtCanvas WaxShard()
        {
            var c = New(40, 48, 0.5f, 0.5f);
            Teardrop(c, 20, 18, 11, 24, Cylinder(20, 11, WaxShade, Wax), 1f);
            c.Ellipse(16, 20, 3, 6, Color.white.WithA(0.8f));
            c.OuterGlow(WaxWarm.WithA(0.8f), 7);
            return c;
        }

        /// <summary>Relic orb shown over the altar.</summary>
        public static ArtCanvas Relic()
        {
            var c = New(48, 48, 0.5f, 0.5f);
            c.Glow(24, 24, 23, FlameOrange.WithA(0.8f), 1.5f);
            c.Circle(24, 24, 10, FlameYellow);
            c.Circle(24, 24, 6, FlameCore);
            c.Stroke(ArtCanvas.CircleSdf(24, 24, 14), WaxWarm, 1.5f, 0, 0, 48, 48);
            return c;
        }

        public static ArtCanvas GateBar()
        {
            var c = New(24, 96, 0.5f, 0f, 24f);
            c.Box(12, 48, 5, 48, 2, VGrad(0, 96, Ink, AshDark));
            c.Poly(new[] { V(4, 88), V(12, 96), V(20, 88) }, AshDark);
            for (int k = 0; k < 4; k++) c.Box(12, 12 + k * 24, 7, 2, 1, Ash.WithA(0.7f));
            c.Outline(Ink, 1f);
            return c;
        }

        /// <summary>Foreground chain (hangs from the top of the view).</summary>
        public static ArtCanvas Chain()
        {
            var c = New(24, 256, 0.5f, 1f, 32f);
            for (int y = 256; y > 0; y -= 14)
            {
                c.Stroke(ArtCanvas.EllipseSdf(12, y - 7, 5, 8), Ink, 3, 0, y - 18, 24, y);
            }
            return c;
        }

        /// <summary>Soft round vignette (black edges).</summary>
        public static ArtCanvas Vignette()
        {
            var c = New(256, 144, 0.5f, 0.5f, 16f);
            for (int y = 0; y < 144; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dx = (x - 128f) / 128f, dy = (y - 72f) / 72f;
                    float d = Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 1.1f);
                    float a = Mathf.Clamp01((d - 0.55f) / 0.7f);
                    c.Px[y * 256 + x] = new Color(0, 0, 0, a * a * 0.9f);
                }
            return c;
        }

        /// <summary>Darkness overlay with a hole of light in the middle (used in the Eclipse phase).</summary>
        public static ArtCanvas DarknessHole()
        {
            var c = New(256, 256, 0.5f, 0.5f, 16f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float dx = (x - 128f) / 128f, dy = (y - 128f) / 128f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.08f) / 0.3f);
                    c.Px[y * 256 + x] = new Color(0.01f, 0.01f, 0.02f, Mathf.SmoothStep(0, 1, a) * 0.97f);
                }
            return c;
        }
    }
}
