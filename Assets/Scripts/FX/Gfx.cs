using System.Collections.Generic;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>Sorting orders (all sprites live on the default sorting layer).</summary>
    public static class Layer
    {
        public const int Sky = -200;
        public const int Far = -150;
        public const int Mid = -120;
        public const int Near = -90;
        public const int BackProps = -20;
        public const int Terrain = 0;
        public const int Props = 5;
        public const int Enemies = 10;
        public const int Boss = 12;
        public const int Player = 20;
        public const int Projectiles = 25;
        public const int Fx = 30;
        public const int Foreground = 50;
        public const int Darkness = 80;
        public const int Overlay = 90;
    }

    /// <summary>Helpers to assemble sprite rigs from painted parts.</summary>
    public static class Gfx
    {
        public static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Vector2 localPos, int order, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.sharedMaterial = additive ? SpriteBank.AdditiveMaterial : SpriteBank.SpriteMaterial;
            return sr;
        }

        public static SpriteRenderer Glow(Transform parent, Vector2 localPos, float size, Color color, int order)
        {
            var sr = Part(parent, "glow", SpriteBank.Get("Glow", ArtLibrary.Glow), localPos, order, true);
            sr.color = color;
            sr.transform.localScale = Vector3.one * size;
            return sr;
        }

        public static Sprite S(string key, System.Func<ArtCanvas> painter) { return SpriteBank.Get(key, painter); }
    }

    /// <summary>White hit-flash for a group of renderers (uses the AshenWick/Sprite shader).</summary>
    public sealed class Flasher
    {
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        float amount;
        Color color = Color.white;
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        public void Add(SpriteRenderer sr) { if (sr != null) renderers.Add(sr); }

        public void AddAll(Transform root)
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.sharedMaterial == SpriteBank.SpriteMaterial) renderers.Add(sr);
        }

        public void Flash(Color c, float strength = 1f)
        {
            color = c;
            amount = Mathf.Max(amount, strength);
            Apply();
        }

        public void Tick(float dt)
        {
            if (amount <= 0f) return;
            amount = Mathf.Max(0f, amount - dt * 6f);
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                var sr = renderers[i];
                if (sr == null) continue;
                sr.GetPropertyBlock(block);
                block.SetFloat(FlashAmountId, amount);
                block.SetColor(FlashColorId, color);
                sr.SetPropertyBlock(block);
            }
        }
    }
}
