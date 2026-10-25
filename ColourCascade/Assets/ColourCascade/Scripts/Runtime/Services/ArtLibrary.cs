using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;

namespace ColourCascade.App
{
    /// <summary>
    /// All game sprites, created at runtime from the procedurally generated atlas (one texture, so the UI
    /// batches into very few draw calls) plus a few stretched background textures.
    /// </summary>
    public sealed class ArtLibrary
    {
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<int, Sprite> backgrounds = new Dictionary<int, Sprite>();
        readonly HashSet<string> reportedMissing = new HashSet<string>();
        Sprite fallback;

        public static ArtLibrary Instance { get; private set; }
        public Texture2D Atlas { get; private set; }

        public static ArtLibrary Create(AtlasResult atlas, ImageData mainBackground, IList<ImageData> regionBackgrounds)
        {
            var lib = new ArtLibrary();
            var img = atlas.Image;
            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false);
            tex.name = "ColourCascadeAtlas";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.LoadRawTextureData(img.Pixels);
            tex.Apply(false, true);
            lib.Atlas = tex;
            foreach (var kv in atlas.Sprites)
            {
                var m = kv.Value;
                var sprite = Sprite.Create(tex, new Rect(m.Rect.X, m.Rect.Y, m.Rect.Width, m.Rect.Height), new Vector2(m.PivotX, m.PivotY), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(m.BorderLeft, m.BorderBottom, m.BorderRight, m.BorderTop));
                sprite.name = kv.Key;
                lib.sprites[kv.Key] = sprite;
            }
            lib.backgrounds[-1] = BackgroundSprite(mainBackground);
            for (int i = 0; i < regionBackgrounds.Count; i++) lib.backgrounds[i] = BackgroundSprite(regionBackgrounds[i]);

            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            white.SetPixels32(px);
            white.Apply(false, true);
            lib.fallback = Sprite.Create(white, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            Instance = lib;
            return lib;
        }

        static Sprite BackgroundSprite(ImageData img)
        {
            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false);
            tex.name = img.Name;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.LoadRawTextureData(img.Pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, img.Width, img.Height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        public Sprite Get(string name)
        {
            Sprite s;
            if (name != null && sprites.TryGetValue(name, out s)) return s;
            if (name != null && reportedMissing.Add(name)) Debug.LogWarning("[ColourCascade] Missing sprite '" + name + "'");
            return fallback;
        }

        public Sprite Icon(Icon icon) => Get(ArtNames.IconName(icon));
        public Sprite Tile(int colour) => Get(ArtNames.Tile(colour));
        public Sprite Symbol(int colour) => Get(ArtNames.Symbol(colour));
        public Sprite PowerUp(PowerUpId id) => Get(ArtNames.PowerUp((int)id));
        public Sprite Decor(Decor d) => Get(ArtNames.DecorName(d));

        /// <summary>-1 = main night-sky background, 0-5 = map region gradients.</summary>
        public Sprite Background(int theme)
        {
            Sprite s;
            if (theme >= 0) theme %= 6;
            return backgrounds.TryGetValue(theme, out s) ? s : fallback;
        }
    }

    public static class Fonts
    {
        public static Font Bold { get; private set; }
        public static Font Medium { get; private set; }

        public static void Load()
        {
            Bold = Resources.Load<Font>("Fonts/Fredoka-Bold");
            Medium = Resources.Load<Font>("Fonts/Fredoka-Medium");
            if (Bold == null || Medium == null)
            {
                Debug.LogWarning("[ColourCascade] Fredoka fonts not found in Resources/Fonts; using the built-in font.");
                var builtin = BuiltinFont();
                if (Bold == null) Bold = builtin;
                if (Medium == null) Medium = builtin;
            }
        }

        static Font BuiltinFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }
    }
}
