using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWick.Art
{
    /// <summary>Converts painted canvases into cached Unity sprites / textures and shared materials.</summary>
    public static class SpriteBank
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static Material spriteMat, additiveMat;

        public static Texture2D ToTexture(ArtCanvas c, bool repeat = false)
        {
            var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false);
            tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(c.Px);
            tex.Apply(false, false);
            return tex;
        }

        public static Sprite ToSprite(ArtCanvas c, bool repeat = false)
        {
            var tex = ToTexture(c, repeat);
            return Sprite.Create(tex, new Rect(0, 0, c.W, c.H), c.Pivot, c.PPU, 0, SpriteMeshType.FullRect);
        }

        public static Sprite Get(string key, Func<ArtCanvas> painter)
        {
            Sprite s;
            if (sprites.TryGetValue(key, out s) && s != null) return s;
            s = ToSprite(ArtCache.Get(key, painter));
            s.name = key;
            sprites[key] = s;
            return s;
        }

        public static Texture2D Tex(string key, Func<ArtCanvas> painter)
        {
            Texture2D t;
            if (textures.TryGetValue(key, out t) && t != null) return t;
            t = ToTexture(ArtCache.Get(key, painter));
            t.name = key;
            textures[key] = t;
            return t;
        }

        /// <summary>Sprite material with hit-flash support (falls back to Sprites/Default).</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMat == null)
                {
                    var sh = Resources.Load<Shader>("AshenWickSprite");
                    if (sh == null || !sh.isSupported) sh = Shader.Find("Sprites/Default");
                    spriteMat = new Material(sh);
                }
                return spriteMat;
            }
        }

        /// <summary>Additive material for glows and fire.</summary>
        public static Material AdditiveMaterial
        {
            get
            {
                if (additiveMat == null)
                {
                    var sh = Resources.Load<Shader>("AshenWickAdditive");
                    if (sh == null || !sh.isSupported) sh = Shader.Find("Sprites/Default");
                    additiveMat = new Material(sh);
                }
                return additiveMat;
            }
        }

        public static bool FlashSupported
        {
            get { return SpriteMaterial.HasProperty("_FlashAmount"); }
        }
    }
}
