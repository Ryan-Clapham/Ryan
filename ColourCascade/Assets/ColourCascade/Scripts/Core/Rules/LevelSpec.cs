namespace ColourCascade.Core
{
    /// <summary>
    /// Everything needed to play one board: produced by the campaign catalogue, the challenge factory
    /// or the endless/practice setup. Plain data; the session never changes it.
    /// </summary>
    public sealed class LevelSpec
    {
        /// <summary>Stable identifier, e.g. "campaign:12", "daily:2026-10-25", "weekly:2026-W43".</summary>
        public string Id = "";
        public GameMode Mode;
        /// <summary>Campaign level number (0 for other modes).</summary>
        public int Number;
        public string Title = "";
        public LevelLayout Layout;
        public int Colours = 4;
        public int[] ColourWeights;
        public int AssistPermille;
        /// <summary>Move limit; 0 means unlimited.</summary>
        public int Moves;
        /// <summary>Countdown in seconds; 0 means untimed. Only Weekly Rush is timed.</summary>
        public int TimeLimitSeconds;
        public int Target;
        public int Star2;
        public int Star3;
        /// <summary>Campaign only: points awarded per unused move when the target is reached.</summary>
        public int LeftoverBonusPerMove;
        /// <summary>Seed for layout generation and difficulty calibration.</summary>
        public ulong Seed;
        /// <summary>When true the board and refills always use Seed (daily/weekly), so everyone plays the same puzzle.</summary>
        public bool FixedSeed;
        public int RegionIndex;
        public bool IsMilestone;
        public bool IsRegionFinale;
        public string TutorialKey = "";
        public string Note = "";

        public int Width => Layout.Width;
        public int Height => Layout.Height;
        public int StoneCount => Layout.StoneCount;
    }
}
