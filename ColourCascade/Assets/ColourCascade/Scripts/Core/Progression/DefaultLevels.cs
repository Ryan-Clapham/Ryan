namespace ColourCascade.Core
{
    /// <summary>Tutorial hint identifiers (stored in SaveData.Tutorials once shown).</summary>
    public static class TutorialKeys
    {
        public const string TapGroup = "tap_group";
        public const string BiggerGroups = "bigger_groups";
        public const string HoldPreview = "hold_preview";
        public const string Stars = "stars";
        public const string Symbols = "symbols_tip";
        public const string Stones = "stones";
        public const string Shuffle = "shuffle";
        public const string FirstWin = "first_win";
        public const string Endless = "endless_intro";
        public const string Daily = "daily_intro";
        public const string Rush = "rush_intro";
        public static string PowerUpIntro(PowerUpId id) => "pu_intro_" + (int)id;
        // Campaign mechanics (each shown once, on the mechanic's introduction level).
        public const string Ice = "mech_ice";
        public const string ColourGoal = "mech_colour_goal";
        public const string ThickIce = "mech_thick_ice";
        public const string Rockets = "mech_rockets";
        public const string Bombs = "mech_bombs";
        public const string Combos = "mech_combos";
        public const string Prisms = "mech_prisms";
        public const string Chains = "mech_chains";
        public const string Treasure = "mech_treasure";
        public const string Moss = "mech_moss";
        /// <summary>Shown after the player first makes a special tile of their own.</summary>
        public const string FirstSpecial = "mech_first_special";
    }

    /// <summary>The hand-authored opening levels. They teach the game one idea at a time.
    /// Everything after these is generated (see CampaignCatalog). Edit freely in the config asset.</summary>
    public static class DefaultLevels
    {
        public static LevelOverride[] Create()
        {
            return new[]
            {
                new LevelOverride
                {
                    level = 1, colours = 3, moves = 12, assistPermille = 300, tutorial = TutorialKeys.TapGroup,
                    note = "Tap groups of matching tiles",
                    layout = new[]
                    {
                        "RRBBYY",
                        "RYBBYR",
                        "YYRBRR",
                        "BYRRRB",
                        "BBYYBB",
                        "RBYYRY",
                    },
                },
                new LevelOverride { level = 2, colours = 3, moves = 12, assistPermille = 260, tutorial = TutorialKeys.HoldPreview, note = "Bigger groups score far more" },
                new LevelOverride { level = 3, colours = 4, moves = 14, tutorial = TutorialKeys.Stars, note = "A fourth colour joins in" },
                new LevelOverride { level = 4, colours = 4, moves = 15, note = "A bigger board" },
                new LevelOverride { level = 5, colours = 4, moves = 15, tutorial = TutorialKeys.Symbols },
                new LevelOverride { level = 6, colours = 4, moves = 16 },
                new LevelOverride { level = 7, colours = 4, moves = 18, note = "Full-size board" },
                new LevelOverride
                {
                    level = 8, colours = 4, moves = 18, tutorial = TutorialKeys.Stones, note = "New: stone barriers",
                    layout = new[]
                    {
                        "........",
                        "........",
                        "........",
                        "........",
                        ".#....#.",
                        ".#....#.",
                        "........",
                        "........",
                        "........",
                    },
                },
                new LevelOverride
                {
                    level = 9, colours = 4, moves = 18, note = "Each section refills on its own",
                    layout = new[]
                    {
                        "........",
                        "........",
                        "........",
                        "........",
                        "........",
                        "##....##",
                        "........",
                        "........",
                        "........",
                    },
                },
                new LevelOverride { level = 10, colours = 4, moves = 20 },
            };
        }
    }
}
