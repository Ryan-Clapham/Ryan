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
        /// <summary>Campaign goals (docs/MECHANICS.md §4). Null or empty = one score goal equal to Target.</summary>
        public GoalSpec[] Goals;
        /// <summary>Which special tiles big groups create here (campaign only; None elsewhere).</summary>
        public SpecialTier Specials;
        /// <summary>Most mossy tiles the board may hold; moss stops spreading at this number. 0 = twice the starting moss.</summary>
        public int MossCap;
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

        /// <summary>The goals this level is played with: its own, or a single score goal equal to its target.</summary>
        public GoalSpec[] EffectiveGoals()
        {
            if (Goals != null && Goals.Length > 0) return Goals;
            return new[] { GoalSpec.Score(Target) };
        }

        public int Width => Layout.Width;
        public int Height => Layout.Height;
        public int StoneCount => Layout.StoneCount;
    }
}
