using System.Collections.Generic;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// Lightweight pooled sprite particles (ash, embers, sparks, smoke, afterimages).
    /// Independent from Unity's ParticleSystem so everything is controlled from code.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        sealed class P
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector2 Vel;
            public float Life, Max, Size0, Size1, Grav, Drag, Spin, Wobble, Phase;
            public Color C0, C1;
            public bool Unscaled;
            public bool Active;
        }

        const int PoolSize = 900;
        readonly List<P> pool = new List<P>();
        int cursor;
        Sprite dot, soft, flake, spark, smoke, ring;
        public Color FlashColor;
        public float FlashAlpha;

        void Awake()
        {
            dot = SpriteBank.Get("HardDot", ArtLibrary.HardDot);
            soft = SpriteBank.Get("SoftDot", ArtLibrary.SoftDot);
            flake = SpriteBank.Get("AshFlake", ArtLibrary.AshFlake);
            spark = SpriteBank.Get("HitSpark", ArtLibrary.HitSpark);
            smoke = SpriteBank.Get("Smoke", ArtLibrary.Smoke);
            ring = SpriteBank.Get("Ring", ArtLibrary.Ring);
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteBank.SpriteMaterial;
                go.SetActive(false);
                pool.Add(new P { T = go.transform, R = sr });
            }
        }

        P Next()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                cursor = (cursor + 1) % pool.Count;
                if (!pool[cursor].Active) return pool[cursor];
            }
            cursor = (cursor + 1) % pool.Count;
            return pool[cursor];
        }

        P Emit(Sprite s, Vector2 pos, Vector2 vel, float life, float size0, float size1, Color c0, Color c1, bool additive, int order)
        {
            var p = Next();
            p.Active = true;
            p.T.gameObject.SetActive(true);
            p.T.position = pos;
            p.T.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            p.R.sprite = s;
            p.R.sharedMaterial = additive ? SpriteBank.AdditiveMaterial : SpriteBank.SpriteMaterial;
            p.R.sortingOrder = order;
            p.R.flipX = false;
            p.Vel = vel; p.Life = 0; p.Max = life; p.Size0 = size0; p.Size1 = size1; p.C0 = c0; p.C1 = c1;
            p.Grav = 0; p.Drag = 0; p.Spin = 0; p.Wobble = 0; p.Phase = Random.value * 10f; p.Unscaled = false;
            p.T.localScale = Vector3.one * size0;
            p.R.color = c0;
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime;
            for (int i = 0; i < pool.Count; i++)
            {
                var p = pool[i];
                if (!p.Active) continue;
                float d = p.Unscaled ? udt : dt;
                p.Life += d;
                if (p.Life >= p.Max)
                {
                    p.Active = false;
                    p.T.gameObject.SetActive(false);
                    continue;
                }
                float t = p.Life / p.Max;
                p.Vel.y -= p.Grav * d;
                p.Vel *= 1f / (1f + p.Drag * d);
                Vector2 w = p.Wobble > 0 ? new Vector2(Mathf.Sin(p.Life * 2.3f + p.Phase) * p.Wobble, 0f) : Vector2.zero;
                p.T.position += (Vector3)((p.Vel + w) * d);
                if (p.Spin != 0) p.T.Rotate(0, 0, p.Spin * d);
                p.T.localScale = Vector3.one * Mathf.Lerp(p.Size0, p.Size1, t);
                p.R.color = Color.Lerp(p.C0, p.C1, t);
            }
            FlashAlpha = Mathf.Max(0f, FlashAlpha - udt * 2.2f);
        }

        public void ClearAll()
        {
            foreach (var p in pool)
            {
                p.Active = false;
                if (p.T != null) p.T.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Effects vocabulary
        // ------------------------------------------------------------------

        public void Flash(Color c, float alpha)
        {
            FlashColor = c;
            FlashAlpha = Mathf.Max(FlashAlpha, alpha);
        }

        public void AmbientAsh(Vector2 pos, int area)
        {
            Color c = area == 3 ? new Color(0.9f, 0.6f, 0.5f, 0.55f) : new Color(0.85f, 0.84f, 0.88f, 0.55f);
            var p = Emit(flake, pos, new Vector2(Random.Range(-0.6f, 0.2f), Random.Range(-1.4f, -0.7f)), 14f, Random.Range(0.6f, 1.3f), Random.Range(0.5f, 1.1f), c, c.WithAlpha(0.2f), false, Layer.Fx - 1);
            p.Wobble = 0.6f; p.Spin = Random.Range(-60f, 60f);
            if (Random.value < 0.35f)
            {
                // some flakes fall in front of everything (depth)
                p.R.sortingOrder = Layer.Foreground + 1;
                p.T.localScale *= 1.6f; p.Size0 *= 1.6f; p.Size1 *= 1.6f;
                p.C0 = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 0.45f);
                p.C1 = p.C0.WithAlpha(0.1f);
            }
        }

        public void Ember(Vector2 pos)
        {
            var c = new Color(1f, Random.Range(0.45f, 0.75f), 0.2f, 1f);
            var p = Emit(soft, pos, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1f, 2.4f)), Random.Range(1.5f, 3f), 0.22f, 0.05f, c, c.WithAlpha(0f), true, Layer.Fx);
            p.Wobble = 0.8f;
        }

        /// <summary>Burst of ash chunks + orange sparks — the signature enemy hit.</summary>
        public void HitBurst(Vector2 pos, Vector2 dir, int count = 10, float power = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = (dir.normalized * Random.Range(4f, 10f) + Random.insideUnitCircle * 5f) * power;
                var ash = new Color(0.12f, 0.12f, 0.15f, 1f);
                var p = Emit(flake, pos, v, Random.Range(0.35f, 0.7f), Random.Range(1.4f, 2.6f), 0.4f, ash, ash.WithAlpha(0f), false, Layer.Fx);
                p.Grav = 16f; p.Drag = 2f; p.Spin = Random.Range(-400f, 400f);
            }
            for (int i = 0; i < count / 2 + 2; i++)
            {
                Vector2 v = (dir.normalized * Random.Range(5f, 14f) + Random.insideUnitCircle * 7f) * power;
                var c = new Color(1f, Random.Range(0.5f, 0.85f), 0.3f, 1f);
                var p = Emit(dot, pos, v, Random.Range(0.2f, 0.45f), Random.Range(0.12f, 0.22f), 0.02f, c, c.WithAlpha(0.3f), true, Layer.Fx + 1);
                p.Grav = 10f; p.Drag = 3f;
            }
            var s = Emit(spark, pos, Vector2.zero, 0.12f, 1.1f * power, 0.3f, Color.white, new Color(1f, 0.8f, 0.5f, 0f), true, Layer.Fx + 2);
            s.T.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>Wax splash when Niv is hurt.</summary>
        public void WaxSplash(Vector2 pos)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(4f, 11f) + Vector2.up * 3f;
                var c = new Color(0.95f, 0.9f, 0.8f, 1f);
                var p = Emit(dot, pos, v, Random.Range(0.4f, 0.8f), Random.Range(0.15f, 0.3f), 0.05f, c, c.WithAlpha(0f), false, Layer.Fx);
                p.Grav = 25f;
            }
            Emit(ring, pos, Vector2.zero, 0.3f, 0.3f, 2.6f, new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.7f, 0.4f, 0f), true, Layer.Fx + 2);
        }

        public void Dust(Vector2 pos, int count = 6, float spread = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = new Vector2(Random.Range(-2.5f, 2.5f) * spread, Random.Range(0.3f, 1.5f));
                var c = new Color(0.75f, 0.73f, 0.78f, 0.5f);
                var p = Emit(smoke, pos + new Vector2(Random.Range(-0.3f, 0.3f), 0), v, Random.Range(0.4f, 0.8f), Random.Range(0.25f, 0.4f), Random.Range(0.6f, 1f), c, c.WithAlpha(0f), false, Layer.Fx);
                p.Drag = 3f;
            }
        }

        public void Smoke(Vector2 pos, Color c, float size = 1f, int count = 4)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Emit(smoke, pos + Random.insideUnitCircle * 0.3f * size, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.8f, 2f)) * size,
                    Random.Range(0.8f, 1.6f), 0.5f * size, 1.6f * size, c, c.WithAlpha(0f), false, Layer.Fx - 2);
                p.Drag = 1f; p.Spin = Random.Range(-40f, 40f);
            }
        }

        public void FireBurst(Vector2 pos, float size = 1f, int count = 14)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = Random.insideUnitCircle * 7f * size + Vector2.up * 2f;
                var c = Color.Lerp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.35f, 0.1f), Random.value);
                var p = Emit(soft, pos, v, Random.Range(0.3f, 0.7f), Random.Range(0.6f, 1.2f) * size, 0.1f, c, c.WithAlpha(0f), true, Layer.Fx + 1);
                p.Drag = 3f; p.Grav = -3f;
            }
            Emit(ring, pos, Vector2.zero, 0.35f, 0.4f * size, 3.5f * size, new Color(1f, 0.8f, 0.5f, 0.9f), new Color(1f, 0.4f, 0.1f, 0f), true, Layer.Fx + 2);
            Smoke(pos, new Color(0.15f, 0.14f, 0.16f, 0.6f), size, 3);
        }

        /// <summary>Massive death explosion for bosses: ash storm + embers + rings.</summary>
        public void BossExplosion(Vector2 pos)
        {
            for (int i = 0; i < 70; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(5f, 22f);
                var ash = new Color(0.1f, 0.1f, 0.12f, 1f);
                var p = Emit(flake, pos + Random.insideUnitCircle, v, Random.Range(0.8f, 1.8f), Random.Range(2f, 4f), 0.5f, ash, ash.WithAlpha(0f), false, Layer.Fx);
                p.Drag = 1.5f; p.Grav = 4f; p.Spin = Random.Range(-500f, 500f); p.Unscaled = true;
            }
            for (int i = 0; i < 50; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(3f, 16f);
                var c = new Color(1f, Random.Range(0.4f, 0.9f), 0.2f, 1f);
                var p = Emit(soft, pos, v, Random.Range(1f, 2.5f), Random.Range(0.3f, 0.6f), 0.05f, c, c.WithAlpha(0f), true, Layer.Fx + 1);
                p.Drag = 1f; p.Grav = -1f; p.Unscaled = true;
            }
            for (int i = 0; i < 3; i++)
            {
                var r = Emit(ring, pos, Vector2.zero, 0.6f + i * 0.3f, 0.5f, 8f + i * 4f, new Color(1f, 0.9f, 0.7f, 1f), new Color(1f, 0.4f, 0.1f, 0f), true, Layer.Fx + 3);
                r.Unscaled = true;
            }
        }

        /// <summary>Soft white motes rising (healing, resting, pickups).</summary>
        public void Motes(Vector2 pos, Color c, int count = 10, float radius = 0.8f)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Emit(soft, pos + Random.insideUnitCircle * radius, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1f, 3f)), Random.Range(0.6f, 1.4f),
                    Random.Range(0.25f, 0.45f), 0.02f, c, c.WithAlpha(0f), true, Layer.Fx);
                p.Wobble = 0.5f;
            }
        }

        /// <summary>Inward-flowing sparks (charging heal / boss wind-ups).</summary>
        public void Converge(Vector2 pos, Color c, float radius = 2f, int count = 3)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * radius;
                var p = Emit(soft, pos + off, -off * 2.4f, 0.4f, 0.28f, 0.05f, c, c.WithAlpha(0.2f), true, Layer.Fx);
                p.Drag = 0f;
            }
        }

        public void RingPulse(Vector2 pos, Color c, float from, float to, float life = 0.4f)
        {
            Emit(ring, pos, Vector2.zero, life, from, to, c, c.WithAlpha(0f), true, Layer.Fx + 2);
        }

        /// <summary>Ghost copy of a sprite that fades (dashes, boss lunges).</summary>
        public void Afterimage(SpriteRenderer src, Color c, float life = 0.25f)
        {
            if (src == null || src.sprite == null) return;
            var p = Next();
            p.Active = true;
            p.T.gameObject.SetActive(true);
            p.T.position = src.transform.position;
            p.T.rotation = src.transform.rotation;
            p.R.sprite = src.sprite;
            p.R.flipX = src.flipX;
            p.R.sharedMaterial = SpriteBank.AdditiveMaterial;
            p.R.sortingOrder = src.sortingOrder - 1;
            p.Vel = Vector2.zero; p.Life = 0; p.Max = life; p.Grav = 0; p.Drag = 0; p.Spin = 0; p.Wobble = 0; p.Unscaled = false;
            float s = src.transform.lossyScale.x;
            p.Size0 = p.Size1 = Mathf.Abs(s);
            p.T.localScale = src.transform.lossyScale;
            p.C0 = c; p.C1 = c.WithAlpha(0f);
            p.R.color = c;
            // keep sign of the scale for mirrored rigs
            if (s < 0) { p.R.flipX = !p.R.flipX; }
        }
    }

    public static class ColorExt
    {
        public static Color WithAlpha(this Color c, float a) { return new Color(c.r, c.g, c.b, a); }
    }
}
