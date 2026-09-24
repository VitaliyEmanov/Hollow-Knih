using UnityEngine;

namespace AshenWick.Art
{
    /// <summary>Deterministic value noise (pure managed code, no native Unity calls).</summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        public static float Hash1(int x, int seed) { return Hash(x, 0x5bd1e995 & 0xffff, seed); }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        public static float Value2(float x, float y, int seed)
        {
            int xi = Floor(x), yi = Floor(y);
            float tx = Smooth(x - xi), ty = Smooth(y - yi);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        public static float Value1(float x, int seed)
        {
            int xi = Floor(x);
            float t = Smooth(x - xi);
            return Mathf.Lerp(Hash1(xi, seed), Hash1(xi + 1, seed), t);
        }

        public static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value2(x, y, seed + i * 31) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        public static float Fbm1(float x, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value1(x, seed + i * 17) * amp;
                norm += amp;
                x *= 2.1f; amp *= 0.5f;
            }
            return sum / norm;
        }

        static int Floor(float v) { int i = (int)v; return v < i ? i - 1 : i; }
    }
}
