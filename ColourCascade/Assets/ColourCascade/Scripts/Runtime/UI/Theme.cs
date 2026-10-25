using ColourCascade.Procedural;
using UnityEngine;

namespace ColourCascade.App
{
    /// <summary>Design tokens: colours, type scale and spacing used by every screen.
    /// Layout units are reference pixels on a 1080 × 1920 portrait canvas.</summary>
    public static class Theme
    {
        public static Color C(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        public static Color Hex(string hex, float alpha = 1f) => C(Rgba.Hex(hex, alpha));

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static readonly Color BgDeep = C(Palette.BgDeep);
        public static readonly Color Surface = C(Palette.Surface);
        public static readonly Color SurfaceHigh = C(Palette.SurfaceHigh);
        public static readonly Color SurfaceLow = C(Palette.SurfaceLow);
        public static readonly Color BoardBack = C(Palette.BoardBack);
        public static readonly Color Slot = C(Palette.Slot);
        public static readonly Color Text = C(Palette.TextPrimary);
        public static readonly Color TextSecondary = C(Palette.TextSecondary);
        public static readonly Color TextMuted = C(Palette.TextMuted);
        public static readonly Color TextDark = C(Palette.TextDark);
        public static readonly Color Primary = C(Palette.Primary);
        public static readonly Color PrimaryLip = C(Palette.PrimaryLip);
        public static readonly Color Secondary = C(Palette.Secondary);
        public static readonly Color SecondaryLip = C(Palette.SecondaryLip);
        public static readonly Color Success = C(Palette.Success);
        public static readonly Color SuccessLip = C(Palette.SuccessLip);
        public static readonly Color Gold = C(Palette.Gold);
        public static readonly Color GoldLip = C(Palette.GoldLip);
        public static readonly Color Danger = C(Palette.Danger);
        public static readonly Color Teal = C(Palette.Teal);
        public static readonly Color Disabled = C(Palette.Disabled);
        public static readonly Color DisabledLip = C(Palette.DisabledLip);
        public static readonly Color Shadow = new Color(0.02f, 0.01f, 0.08f, 0.55f);
        public static readonly Color Dim = new Color(0.03f, 0.02f, 0.1f, 0.72f);

        public static Color Tile(int colour) => C(Palette.Tiles[Mathf.Clamp(colour, 0, Palette.Tiles.Length - 1)]);
        public static Color Region(int theme, int slot) => C(Palette.Regions[((theme % 6) + 6) % 6][slot]);

        public const float RefWidth = 1080f;
        public const float RefHeight = 1920f;

        // Type scale.
        public const int FontDisplay = 116;
        public const int FontTitle = 72;
        public const int FontHeading = 56;
        public const int FontLarge = 46;
        public const int FontBody = 38;
        public const int FontSmall = 32;
        public const int FontTiny = 27;

        // Spacing.
        public const float Margin = 44f;
        public const float Gap = 24f;
        public const float CardRadius = 44f;
        public const float ButtonRadius = 40f;
        public const float TopBarHeight = 132f;
        public const float IconButtonSize = 112f;
        public const float MinTouch = 96f;
    }
}
