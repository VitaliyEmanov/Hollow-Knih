using System;
using UnityEngine;

namespace AshenWick.Art
{
    /// <summary>
    /// Pure C# raster canvas used to paint every sprite of the game procedurally.
    /// Coordinates are in pixels, y axis points UP (row 0 is the bottom row),
    /// which matches Texture2D.SetPixels layout.
    /// Shapes are rendered through signed distance functions, which gives
    /// smooth anti-aliased, slightly "inked" edges similar to hand drawn art.
    /// </summary>
    public sealed class ArtCanvas
    {
        public readonly int W;
        public readonly int H;
        public readonly Color[] Px;

        /// <summary>Normalized pivot used when the canvas becomes a Sprite.</summary>
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);
        /// <summary>Pixels per world unit used when the canvas becomes a Sprite.</summary>
        public float PPU = 64f;

        public delegate float Sdf(float x, float y);
        public delegate Color Paint(float x, float y);

        /// <summary>Amount of edge wobble (in pixels) applied to shapes. Gives a hand drawn feel.</summary>
        public float Wobble;
        public float WobbleScale = 0.08f;
        public int WobbleSeed = 7;

        public ArtCanvas(int w, int h)
        {
            W = w; H = h;
            Px = new Color[w * h];
        }

        public ArtCanvas Clone()
        {
            var c = new ArtCanvas(W, H);
            Array.Copy(Px, c.Px, Px.Length);
            c.Pivot = Pivot; c.PPU = PPU;
            return c;
        }

        public Color Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return new Color(0, 0, 0, 0);
            return Px[y * W + x];
        }

        public void Set(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[y * W + x] = c;
        }

        /// <summary>Source-over blending of a straight alpha color.</summary>
        public void Blend(int x, int y, Color c, float coverage)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            float sa = c.a * coverage;
            if (sa <= 0.0001f) return;
            int i = y * W + x;
            Color d = Px[i];
            float oa = sa + d.a * (1f - sa);
            if (oa <= 0.00001f) { Px[i] = new Color(0, 0, 0, 0); return; }
            float r = (c.r * sa + d.r * d.a * (1f - sa)) / oa;
            float g = (c.g * sa + d.g * d.a * (1f - sa)) / oa;
            float b = (c.b * sa + d.b * d.a * (1f - sa)) / oa;
            Px[i] = new Color(r, g, b, oa);
        }

        /// <summary>Additive light blending: brightens colour, keeps/raises alpha.</summary>
        public void Add(int x, int y, Color c, float amount)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            Color d = Px[i];
            float k = c.a * amount;
            Px[i] = new Color(Mathf.Min(1f, d.r + c.r * k), Mathf.Min(1f, d.g + c.g * k), Mathf.Min(1f, d.b + c.b * k), Mathf.Max(d.a, Mathf.Min(1f, d.a + k)));
        }

        // ------------------------------------------------------------------
        // Core SDF fill
        // ------------------------------------------------------------------

        public void Fill(Sdf sdf, Paint paint, float minX, float minY, float maxX, float maxY, float soft = 1f)
        {
            float pad = Wobble + soft + 2f;
            int x0 = Mathf.Max(0, (int)Math.Floor(minX - pad));
            int y0 = Mathf.Max(0, (int)Math.Floor(minY - pad));
            int x1 = Mathf.Min(W - 1, (int)Math.Ceiling(maxX + pad));
            int y1 = Mathf.Min(H - 1, (int)Math.Ceiling(maxY + pad));
            float wob = Wobble;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = sdf(px, py);
                    if (wob > 0f) d += (Noise.Value2(px * WobbleScale, py * WobbleScale, WobbleSeed) - 0.5f) * 2f * wob;
                    float cov = Mathf.Clamp01(0.5f - d / soft);
                    if (cov <= 0f) continue;
                    Blend(x, y, paint(px, py), cov);
                }
            }
        }

        public void Fill(Sdf sdf, Color color, float minX, float minY, float maxX, float maxY, float soft = 1f)
        {
            Fill(sdf, (x, y) => color, minX, minY, maxX, maxY, soft);
        }

        /// <summary>Paints a stroke (outline) of an SDF shape.</summary>
        public void Stroke(Sdf sdf, Color color, float width, float minX, float minY, float maxX, float maxY)
        {
            float hw = width * 0.5f;
            Fill((x, y) => Mathf.Abs(sdf(x, y)) - hw, color, minX - hw, minY - hw, maxX + hw, maxY + hw);
        }

        // ------------------------------------------------------------------
        // Shape helpers (return the SDF so callers can reuse it for strokes)
        // ------------------------------------------------------------------

        public static Sdf CircleSdf(float cx, float cy, float r)
        {
            return (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        public static Sdf EllipseSdf(float cx, float cy, float rx, float ry)
        {
            float m = Mathf.Min(rx, ry);
            return (x, y) =>
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * m;
            };
        }

        public static Sdf SegmentSdf(float ax, float ay, float bx, float by, float ra, float rb)
        {
            return (x, y) =>
            {
                float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
                float len2 = bax * bax + bay * bay;
                float h = len2 > 0 ? Mathf.Clamp01((pax * bax + pay * bay) / len2) : 0f;
                float dx = pax - bax * h, dy = pay - bay * h;
                return Mathf.Sqrt(dx * dx + dy * dy) - Mathf.Lerp(ra, rb, h);
            };
        }

        public static Sdf BoxSdf(float cx, float cy, float hw, float hh, float round)
        {
            return (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - hw + round;
                float qy = Mathf.Abs(y - cy) - hh + round;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
            };
        }

        /// <summary>Exact signed distance to a polygon (Inigo Quilez).</summary>
        public static Sdf PolySdf(Vector2[] v)
        {
            int n = v.Length;
            return (x, y) =>
            {
                float d = (x - v[0].x) * (x - v[0].x) + (y - v[0].y) * (y - v[0].y);
                float s = 1f;
                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                    float wx = x - v[i].x, wy = y - v[i].y;
                    float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                    float bx = wx - ex * t, by = wy - ey * t;
                    d = Mathf.Min(d, bx * bx + by * by);
                    bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            };
        }

        public static void PolyBounds(Vector2[] v, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue; maxX = maxY = float.MinValue;
            foreach (var p in v)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
        }

        public void Circle(float cx, float cy, float r, Color c) { Fill(CircleSdf(cx, cy, r), c, cx - r, cy - r, cx + r, cy + r); }
        public void Circle(float cx, float cy, float r, Paint p) { Fill(CircleSdf(cx, cy, r), p, cx - r, cy - r, cx + r, cy + r); }
        public void Ellipse(float cx, float cy, float rx, float ry, Color c) { Fill(EllipseSdf(cx, cy, rx, ry), c, cx - rx, cy - ry, cx + rx, cy + ry); }
        public void Ellipse(float cx, float cy, float rx, float ry, Paint p) { Fill(EllipseSdf(cx, cy, rx, ry), p, cx - rx, cy - ry, cx + rx, cy + ry); }

        public void Capsule(float ax, float ay, float bx, float by, float ra, float rb, Color c)
        {
            float r = Mathf.Max(ra, rb);
            Fill(SegmentSdf(ax, ay, bx, by, ra, rb), c, Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r);
        }

        public void Capsule(float ax, float ay, float bx, float by, float ra, float rb, Paint p)
        {
            float r = Mathf.Max(ra, rb);
            Fill(SegmentSdf(ax, ay, bx, by, ra, rb), p, Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r);
        }

        public void Line(float ax, float ay, float bx, float by, float width, Color c) { Capsule(ax, ay, bx, by, width * 0.5f, width * 0.5f, c); }

        public void Box(float cx, float cy, float hw, float hh, float round, Color c) { Fill(BoxSdf(cx, cy, hw, hh, round), c, cx - hw, cy - hh, cx + hw, cy + hh); }
        public void Box(float cx, float cy, float hw, float hh, float round, Paint p) { Fill(BoxSdf(cx, cy, hw, hh, round), p, cx - hw, cy - hh, cx + hw, cy + hh); }

        public void Poly(Vector2[] v, Color c)
        {
            PolyBounds(v, out var a, out var b, out var d, out var e);
            Fill(PolySdf(v), c, a, b, d, e);
        }

        public void Poly(Vector2[] v, Paint p)
        {
            PolyBounds(v, out var a, out var b, out var d, out var e);
            Fill(PolySdf(v), p, a, b, d, e);
        }

        public void PolyStroke(Vector2[] v, Color c, float width)
        {
            PolyBounds(v, out var a, out var b, out var d, out var e);
            Stroke(PolySdf(v), c, width, a, b, d, e);
        }

        /// <summary>Soft radial light (for glows / halos).</summary>
        public void Glow(float cx, float cy, float r, Color c, float power = 2f, bool additive = false)
        {
            int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(W - 1, (int)(cx + r) + 1);
            int y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(H - 1, (int)(cy + r) + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float t = 1f - Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (t <= 0f) continue;
                    float k = Mathf.Pow(t, power);
                    if (additive) Add(x, y, c, k); else Blend(x, y, c, k);
                }
        }

        /// <summary>Tapered brush stroke along a quadratic bezier: great for tattered cloth, veins, cracks.</summary>
        public void Brush(Vector2 a, Vector2 ctrl, Vector2 b, float wa, float wb, Color c, int steps = 16)
        {
            Vector2 prev = a;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Bezier(a, ctrl, b, t);
                float w0 = Mathf.Lerp(wa, wb, (i - 1) / (float)steps);
                float w1 = Mathf.Lerp(wa, wb, t);
                Capsule(prev.x, prev.y, p.x, p.y, w0 * 0.5f, w1 * 0.5f, c);
                prev = p;
            }
        }

        public static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1 - t;
            return u * u * a + 2 * u * t * c + t * t * b;
        }

        /// <summary>Jagged crack made of random segments (glowing seams, fractures).</summary>
        public void Crack(Vector2 from, Vector2 dir, float length, float width, Color c, int seed, int segments = 6)
        {
            var rng = new System.Random(seed);
            Vector2 p = from;
            Vector2 d = dir.normalized;
            float seg = length / segments;
            for (int i = 0; i < segments; i++)
            {
                float ang = ((float)rng.NextDouble() - 0.5f) * 1.2f;
                Vector2 nd = Rotate(d, ang);
                Vector2 q = p + nd * seg;
                float w = Mathf.Lerp(width, width * 0.3f, i / (float)segments);
                Line(p.x, p.y, q.x, q.y, w, c);
                if (rng.NextDouble() < 0.3)
                {
                    Vector2 br = Rotate(nd, (rng.NextDouble() < 0.5 ? 1 : -1) * 0.9f) * seg * 0.8f;
                    Line(q.x, q.y, q.x + br.x, q.y + br.y, w * 0.6f, c);
                }
                p = q;
            }
        }

        public static Vector2 Rotate(Vector2 v, float a)
        {
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        // ------------------------------------------------------------------
        // Post processing
        // ------------------------------------------------------------------

        /// <summary>
        /// Hand-inked finish, applied to every character and prop:
        /// cross-hatching in the shadows, a thin rim of light on the lit (upper-left) edge,
        /// and a brush ink contour that swells on the shadow side and wavers like a real pen.
        /// </summary>
        public void Outline(Color color, float width)
        {
            Hatch(width);
            RimLight(width);
            InkContour(color, width);
        }

        /// <summary>Plain uniform outline (kept for UI shapes that must stay crisp).</summary>
        public void FlatOutline(Color color, float width)
        {
            InkContour(color, width, 0f, 0f);
        }

        /// <summary>Ink contour with variable width: thicker toward the lower right, with noisy pen pressure.</summary>
        public void InkContour(Color color, float width, float swell = 0.9f, float boil = 0.35f)
        {
            var dist = DistanceToOpaque(0.5f);
            var result = new Color[Px.Length];
            // offset copy of the silhouette toward the shadow side makes the line swell there
            float ox = width * swell, oy = -width * swell;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float d = dist[i];
                    if (swell > 0f)
                    {
                        int sx = Mathf.Clamp(Mathf.RoundToInt(x - ox), 0, W - 1), sy = Mathf.Clamp(Mathf.RoundToInt(y - oy), 0, H - 1);
                        d = Mathf.Min(d, dist[sy * W + sx] + width * 0.35f);
                    }
                    float pressure = 1f + (Noise.Value2(x * 0.09f, y * 0.09f, 991) - 0.5f) * 2f * boil;
                    float a = Mathf.Clamp01(width * pressure + 0.5f - d) * color.a;
                    Color src = Px[i];
                    Color under = new Color(color.r, color.g, color.b, a);
                    float oa = src.a + under.a * (1 - src.a);
                    if (oa <= 0.00001f) { result[i] = new Color(0, 0, 0, 0); continue; }
                    result[i] = new Color(
                        (src.r * src.a + under.r * under.a * (1 - src.a)) / oa,
                        (src.g * src.a + under.g * under.a * (1 - src.a)) / oa,
                        (src.b * src.a + under.b * under.a * (1 - src.a)) / oa, oa);
                }
            Array.Copy(result, Px, Px.Length);
        }

        /// <summary>
        /// Pen hatching: diagonal strokes over darker areas and along the shadow-side edge.
        /// Strokes are broken up by noise so they read as individual hand-drawn lines.
        /// </summary>
        public void Hatch(float scale, float strength = 0.55f)
        {
            float spacing = Mathf.Max(3f, scale * 2.6f);
            var inside = new float[Px.Length];
            // distance to transparency (how deep inside the shape a pixel is)
            var d = new float[Px.Length];
            const float big = 1e5f;
            for (int i = 0; i < d.Length; i++) d[i] = Px[i].a > 0.5f ? big : 0f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; if (d[i] == 0f) continue;
                    float v = d[i];
                    if (x > 0) v = Mathf.Min(v, d[i - 1] + 1f);
                    if (y > 0) v = Mathf.Min(v, d[i - W] + 1f);
                    d[i] = v;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x; if (d[i] == 0f) continue;
                    float v = d[i];
                    if (x < W - 1) v = Mathf.Min(v, d[i + 1] + 1f);
                    if (y < H - 1) v = Mathf.Min(v, d[i + W] + 1f);
                    d[i] = v;
                }
            float edgeBand = Mathf.Max(4f, scale * 5f);
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    Color c = Px[i];
                    if (c.a < 0.9f) continue;
                    float lum = c.r * 0.3f + c.g * 0.55f + c.b * 0.15f;
                    // shadow side: is there transparency toward the lower right, close by?
                    float edge = Mathf.Clamp01(1f - d[i] / edgeBand);
                    int rx = Mathf.Min(W - 1, x + 3), ry = Mathf.Max(0, y - 3);
                    bool shadowSide = Px[ry * W + rx].a < 0.5f || Px[y * W + rx].a < 0.5f || Px[ry * W + x].a < 0.5f;
                    float darkness = Mathf.Clamp01((0.55f - lum) * 1.6f);
                    float want = Mathf.Max(shadowSide ? edge : 0f, darkness * 0.6f);
                    if (want <= 0.05f) continue;
                    // diagonal line pattern, jittered per stroke
                    float u = (x + y) / spacing;
                    int stroke = Mathf.FloorToInt(u);
                    float jitter = (Noise.Hash(stroke, 7, 311) - 0.5f) * 0.3f;
                    float f = u - stroke + jitter;
                    float line = Mathf.Clamp01(1f - Mathf.Abs(f - 0.5f) * spacing * 0.9f);
                    // strokes start and stop: break them with low-frequency noise along the line
                    float along = (x - y) * 0.08f;
                    float gap = Noise.Value2(stroke * 1.7f, along, 313);
                    if (gap < 1f - want) continue;
                    // cross-hatch in the deepest shadows
                    if (darkness > 0.55f && shadowSide)
                    {
                        float u2 = (x - y) / spacing;
                        float f2 = u2 - Mathf.Floor(u2);
                        line = Mathf.Max(line, Mathf.Clamp01(1f - Mathf.Abs(f2 - 0.5f) * spacing * 0.9f) * 0.7f);
                    }
                    float k = line * strength * want;
                    Px[i] = new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a);
                }
        }

        /// <summary>Thin pale rim of light on the upper-left edges (backlight from the ash sky).</summary>
        public void RimLight(float scale, float strength = 0.35f)
        {
            int o = Mathf.Max(1, Mathf.RoundToInt(scale * 1.2f));
            var src = (Color[])Px.Clone();
            for (int y = 0; y < H - o; y++)
                for (int x = o; x < W; x++)
                {
                    int i = y * W + x;
                    Color c = src[i];
                    if (c.a < 0.9f) continue;
                    if (src[(y + o) * W + (x - o)].a > 0.4f) continue;
                    float n = Noise.Value2(x * 0.2f, y * 0.2f, 57);
                    float k = strength * (0.6f + n * 0.8f);
                    Px[i] = new Color(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 0.97f, k), Mathf.Lerp(c.b, 0.92f, k), c.a);
                }
        }

        /// <summary>
        /// Painterly texture for large painted areas (backgrounds): directional brush streaks,
        /// darker "watercolour" pooling at shape edges and a dry-brush ragged top edge.
        /// </summary>
        public void Painterly(int seed, float streak = 0.12f, float pooling = 0.25f, float dryBrush = 6f)
        {
            var dist = new float[Px.Length];
            const float big = 1e5f;
            for (int i = 0; i < dist.Length; i++) dist[i] = Px[i].a > 0.3f ? big : 0f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; if (dist[i] == 0f) continue;
                    float v = dist[i];
                    if (x > 0) v = Mathf.Min(v, dist[i - 1] + 1f);
                    if (y > 0) v = Mathf.Min(v, dist[i - W] + 1f);
                    dist[i] = v;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x; if (dist[i] == 0f) continue;
                    float v = dist[i];
                    if (x < W - 1) v = Mathf.Min(v, dist[i + 1] + 1f);
                    if (y < H - 1) v = Mathf.Min(v, dist[i + W] + 1f);
                    dist[i] = v;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color c = Px[i];
                    if (c.a <= 0f) continue;
                    // long, mostly vertical brush streaks
                    float s = (Noise.Fbm(x * 0.35f, y * 0.018f, seed, 3) - 0.5f) * 0.6f + (Noise.Fbm(x * 0.03f, y * 0.05f, seed + 9, 3) - 0.5f) * 0.8f;
                    float k = 1f + s * streak * 2f;
                    // pigment pools at the edge of a wash
                    float d = dist[i];
                    float pool = d < 6f ? (1f - d / 6f) * pooling : 0f;
                    k *= 1f - pool;
                    float a = c.a;
                    // dry brush: ragged, streaky edge
                    if (d < dryBrush)
                    {
                        float bristle = Noise.Value2(x * 0.9f, y * 0.06f, seed + 5);
                        if (bristle < 0.8f - d / dryBrush) a *= 0.55f + bristle * 0.45f;
                    }
                    Px[i] = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), a);
                }
        }

        /// <summary>Paper fibre texture, visible over everything (used on full-screen layers).</summary>
        public void Paper(float amount, int seed)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color c = Px[i];
                    if (c.a <= 0f) continue;
                    float fibre = Noise.Value2(x * 0.7f, y * 0.12f, seed) * 0.6f + Noise.Value2(x * 0.12f, y * 0.7f, seed + 1) * 0.4f;
                    float speck = Noise.Hash(x, y, seed + 2) > 0.985f ? -0.5f : 0f;
                    float k = 1f + (fibre - 0.5f + speck) * amount;
                    Px[i] = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
                }
        }

        /// <summary>Soft outer glow around opaque pixels, painted underneath.</summary>
        public void OuterGlow(Color color, float radius)
        {
            var dist = DistanceToOpaque(0.3f);
            for (int i = 0; i < Px.Length; i++)
            {
                float t = 1f - dist[i] / radius;
                if (t <= 0f || Px[i].a >= 0.999f) continue;
                float ga = t * t * color.a;
                Color src = Px[i];
                float oa = src.a + ga * (1 - src.a);
                if (oa <= 0.00001f) continue;
                Px[i] = new Color(
                    (src.r * src.a + color.r * ga * (1 - src.a)) / oa,
                    (src.g * src.a + color.g * ga * (1 - src.a)) / oa,
                    (src.b * src.a + color.b * ga * (1 - src.a)) / oa, oa);
            }
        }

        /// <summary>Chamfer distance transform: distance (px) from each pixel to nearest pixel with alpha >= threshold.</summary>
        public float[] DistanceToOpaque(float threshold)
        {
            const float big = 1e6f;
            var d = new float[W * H];
            for (int i = 0; i < d.Length; i++) d[i] = Px[i].a >= threshold ? 0f : big;
            const float a = 1f, b = 1.4142f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float v = d[i];
                    if (x > 0) v = Mathf.Min(v, d[i - 1] + a);
                    if (y > 0)
                    {
                        v = Mathf.Min(v, d[i - W] + a);
                        if (x > 0) v = Mathf.Min(v, d[i - W - 1] + b);
                        if (x < W - 1) v = Mathf.Min(v, d[i - W + 1] + b);
                    }
                    d[i] = v;
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x;
                    float v = d[i];
                    if (x < W - 1) v = Mathf.Min(v, d[i + 1] + a);
                    if (y < H - 1)
                    {
                        v = Mathf.Min(v, d[i + W] + a);
                        if (x < W - 1) v = Mathf.Min(v, d[i + W + 1] + b);
                        if (x > 0) v = Mathf.Min(v, d[i + W - 1] + b);
                    }
                    d[i] = v;
                }
            return d;
        }

        /// <summary>Multiplies brightness by fractal noise on opaque pixels (paper / ash grain texture).</summary>
        public void Grain(float amount, float scale, int seed)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color c = Px[i];
                    if (c.a <= 0f) continue;
                    float n = Noise.Fbm(x * scale, y * scale, seed, 3) - 0.5f;
                    float k = 1f + n * amount * 2f;
                    Px[i] = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
                }
        }

        /// <summary>Vertical shading: darkens bottom, lightens top of opaque pixels (volume).</summary>
        public void Shade(float bottomDark, float topLight)
        {
            for (int y = 0; y < H; y++)
            {
                float t = y / (float)(H - 1);
                float k = Mathf.Lerp(1f - bottomDark, 1f + topLight, t);
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color c = Px[i];
                    if (c.a <= 0f) continue;
                    Px[i] = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
                }
            }
        }

        /// <summary>Copies another canvas on top of this one at the given offset.</summary>
        public void Draw(ArtCanvas src, int ox, int oy, bool flipX = false)
        {
            for (int y = 0; y < src.H; y++)
                for (int x = 0; x < src.W; x++)
                {
                    Color c = src.Px[y * src.W + (flipX ? src.W - 1 - x : x)];
                    if (c.a <= 0f) continue;
                    Blend(ox + x, oy + y, c, 1f);
                }
        }

        public void Tint(Color mul)
        {
            for (int i = 0; i < Px.Length; i++)
            {
                Color c = Px[i];
                Px[i] = new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a * mul.a);
            }
        }

        /// <summary>Replaces colour of every pixel with a flat colour, keeping alpha (for hit-flash sprites / silhouettes).</summary>
        public ArtCanvas Silhouette(Color c)
        {
            var o = new ArtCanvas(W, H);
            o.Pivot = Pivot; o.PPU = PPU;
            for (int i = 0; i < Px.Length; i++) o.Px[i] = new Color(c.r, c.g, c.b, Px[i].a * c.a);
            return o;
        }

        public void Clear(Color c)
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = c;
        }

        /// <summary>Vertical gradient fill of the whole canvas.</summary>
        public void VerticalGradient(Color bottom, Color top)
        {
            for (int y = 0; y < H; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(H - 1));
                for (int x = 0; x < W; x++) Px[y * W + x] = c;
            }
        }
    }
}
