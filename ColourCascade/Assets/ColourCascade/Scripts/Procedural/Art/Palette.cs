namespace ColourCascade.Procedural
{
    /// <summary>
    /// The Colour Cascade palette. A deep night-violet world so the six candy tile colours glow, with a
    /// warm coral primary action colour and gold reserved for rewards (coins and stars).
    /// </summary>
    public static class Palette
    {
        // Tile colours, in introduction order: Red, Yellow, Blue, Green, Purple, Orange.
        public static readonly Rgba[] Tiles =
        {
            Rgba.Hex("#F5455C"),
            Rgba.Hex("#FFC23A"),
            Rgba.Hex("#3D8BFF"),
            Rgba.Hex("#2DCB78"),
            Rgba.Hex("#A65CF6"),
            Rgba.Hex("#FF8A2B"),
        };

        public static readonly string[] SymbolNames = { "Heart", "Star", "Circle", "Triangle", "Diamond", "Cross" };

        public static readonly Rgba BgDeep = Rgba.Hex("#120D33");
        public static readonly Rgba BgMid = Rgba.Hex("#22175A");
        public static readonly Rgba BgHigh = Rgba.Hex("#3A1F73");
        public static readonly Rgba Surface = Rgba.Hex("#2B2270");
        public static readonly Rgba SurfaceHigh = Rgba.Hex("#3A2F8F");
        public static readonly Rgba SurfaceLow = Rgba.Hex("#1C1650");
        public static readonly Rgba BoardBack = Rgba.Hex("#0E0A2A");
        public static readonly Rgba Slot = Rgba.Hex("#2A2268");

        public static readonly Rgba TextPrimary = Rgba.Hex("#FFFFFF");
        public static readonly Rgba TextSecondary = Rgba.Hex("#CBC4F5");
        public static readonly Rgba TextMuted = Rgba.Hex("#8D85C6");
        public static readonly Rgba TextDark = Rgba.Hex("#24184F");

        public static readonly Rgba Primary = Rgba.Hex("#FF6A4D");
        public static readonly Rgba PrimaryLip = Rgba.Hex("#C7344B");
        public static readonly Rgba Secondary = Rgba.Hex("#5B4FD6");
        public static readonly Rgba SecondaryLip = Rgba.Hex("#392F9C");
        public static readonly Rgba Success = Rgba.Hex("#2DCB78");
        public static readonly Rgba SuccessLip = Rgba.Hex("#1A8C52");
        public static readonly Rgba Gold = Rgba.Hex("#FFC23A");
        public static readonly Rgba GoldLip = Rgba.Hex("#D9860F");
        public static readonly Rgba Danger = Rgba.Hex("#F5455C");
        public static readonly Rgba Teal = Rgba.Hex("#33D1C6");
        public static readonly Rgba Disabled = Rgba.Hex("#4A4479");
        public static readonly Rgba DisabledLip = Rgba.Hex("#332E5C");

        public static readonly Rgba Stone = Rgba.Hex("#727896");
        public static readonly Rgba StoneDark = Rgba.Hex("#4B4F6B");

        /// <summary>Map region themes: sky top, sky bottom, accent (path/decor), decoration tint.</summary>
        public static readonly Rgba[][] Regions =
        {
            new[] { Rgba.Hex("#FFB36B"), Rgba.Hex("#5BC98A"), Rgba.Hex("#FFE7A8"), Rgba.Hex("#2E9D63") }, // Sunrise Meadow
            new[] { Rgba.Hex("#46D3E0"), Rgba.Hex("#2A6FD6"), Rgba.Hex("#FF8A9A"), Rgba.Hex("#1E5BB8") }, // Coral Lagoon
            new[] { Rgba.Hex("#7A5CF0"), Rgba.Hex("#2B1F7A"), Rgba.Hex("#7FF1FF"), Rgba.Hex("#4E3BC2") }, // Crystal Caverns
            new[] { Rgba.Hex("#FF8A4C"), Rgba.Hex("#B5305A"), Rgba.Hex("#FFD36B"), Rgba.Hex("#8E2448") }, // Ember Canyon
            new[] { Rgba.Hex("#BDE6FF"), Rgba.Hex("#5C8FE0"), Rgba.Hex("#FFFFFF"), Rgba.Hex("#4677C9") }, // Frost Peaks
            new[] { Rgba.Hex("#4B2B9E"), Rgba.Hex("#120D33"), Rgba.Hex("#FF7AD9"), Rgba.Hex("#2E1D6E") }, // Starlight Nebula
        };
    }
}
