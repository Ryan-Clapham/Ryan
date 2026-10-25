using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    public sealed class RegionInfo
    {
        public int Index;
        /// <summary>Visual theme (0-5), repeats every six regions.</summary>
        public int Theme;
        public int Cycle;
        public string Name = "";
        public int FirstLevel;
        public int LastLevel;
    }

    public static class Regions
    {
        public static readonly string[] ThemeNames =
        {
            "Sunrise Meadow", "Coral Lagoon", "Crystal Caverns", "Ember Canyon", "Frost Peaks", "Starlight Nebula",
        };

        static readonly string[] Numerals = { "", " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X" };

        public static RegionInfo ForIndex(int index, int levelsPerRegion)
        {
            if (index < 0) index = 0;
            int theme = index % ThemeNames.Length;
            int cycle = index / ThemeNames.Length;
            string suffix = cycle < Numerals.Length ? Numerals[cycle] : " " + (cycle + 1);
            return new RegionInfo
            {
                Index = index,
                Theme = theme,
                Cycle = cycle,
                Name = ThemeNames[theme] + suffix,
                FirstLevel = index * levelsPerRegion + 1,
                LastLevel = (index + 1) * levelsPerRegion,
            };
        }

        public static RegionInfo ForLevel(int level, int levelsPerRegion)
        {
            if (level < 1) level = 1;
            return ForIndex((level - 1) / Math.Max(1, levelsPerRegion), levelsPerRegion);
        }
    }

    /// <summary>
    /// Produces campaign levels on demand: hand-authored overrides where configured, otherwise generated
    /// from the level number with a deterministic seed, so the campaign never runs out. Targets are
    /// calibrated by simulating a reference player, which keeps them fair whatever the configuration.
    /// </summary>
    public sealed class CampaignCatalog
    {
        readonly CampaignConfig cfg;
        readonly ScoringRules scoring;
        readonly Dictionary<int, LevelSpec> cache = new Dictionary<int, LevelSpec>();
        readonly Dictionary<int, LevelOverride> overrides = new Dictionary<int, LevelOverride>();

        public CampaignCatalog(CampaignConfig cfg, ScoringRules scoring)
        {
            this.cfg = cfg;
            this.scoring = scoring;
            if (cfg.overrides != null)
                foreach (var o in cfg.overrides)
                    if (o != null && o.level >= 1) overrides[o.level] = o;
        }

        public CampaignConfig Config => cfg;
        public int LevelsPerRegion => Math.Max(1, cfg.levelsPerRegion);

        public RegionInfo RegionForLevel(int level) => Regions.ForLevel(level, LevelsPerRegion);
        public RegionInfo Region(int index) => Regions.ForIndex(index, LevelsPerRegion);
        public bool IsMilestone(int level) => cfg.milestoneEvery > 0 && level % cfg.milestoneEvery == 0;
        public bool IsRegionFinale(int level) => level % LevelsPerRegion == 0;

        public LevelSpec Get(int level)
        {
            if (level < 1) level = 1;
            LevelSpec spec;
            if (cache.TryGetValue(level, out spec)) return spec;
            spec = Build(level);
            cache[level] = spec;
            return spec;
        }

        public void ClearCache() => cache.Clear();

        LevelSpec Build(int n)
        {
            ulong seed = StableHash.Of("campaign", n);
            var rng = new Rng(seed);
            LevelOverride ov;
            overrides.TryGetValue(n, out ov);

            int cols, rows;
            SizeFor(n, out cols, out rows);
            if (ov != null && ov.columns > 0 && ov.rows > 0)
            {
                cols = ov.columns;
                rows = ov.rows;
            }

            LevelLayout layout = null;
            if (ov != null && ov.layout != null && ov.layout.Length > 0)
            {
                try
                {
                    layout = LevelLayout.Parse(ov.layout);
                    string error;
                    if (!layout.Validate(out error)) layout = null;
                }
                catch (ArgumentException)
                {
                    layout = null;
                }
            }
            if (layout == null) layout = GeneratedLayout(n, cols, rows, rng);

            int colours = ov != null && ov.colours > 0 ? ov.colours : DefaultColours(n);
            colours = Math.Max(2, Math.Min(TileColours.Max, colours));
            int moves = ov != null && ov.moves > 0 ? ov.moves : DefaultMoves(n, layout, rng);
            int assist = ov != null && ov.assistPermille >= 0 ? ov.assistPermille : DefaultAssist(n);

            var region = RegionForLevel(n);
            var spec = new LevelSpec
            {
                Id = "campaign:" + n,
                Mode = GameMode.Campaign,
                Number = n,
                Title = "Level " + n,
                Layout = layout,
                Colours = colours,
                ColourWeights = DefaultWeights(n, colours),
                AssistPermille = assist,
                Moves = moves,
                Seed = seed,
                RegionIndex = region.Index,
                IsMilestone = IsMilestone(n),
                IsRegionFinale = IsRegionFinale(n),
                TutorialKey = ov != null ? ov.tutorial ?? "" : "",
                Note = ov != null && !string.IsNullOrEmpty(ov.note) ? ov.note : DefaultNote(n, layout),
            };

            int target = ov != null && ov.target > 0 ? ov.target : 0;
            if (target <= 0)
            {
                int median = LevelCalibrator.MedianFullScore(spec, scoring, cfg.calibrationRuns, BotKind.Greedy);
                target = NumberFormat.RoundNice((int)((long)median * TargetPermille(n) / 1000));
            }
            spec.Target = target;
            spec.Star2 = ov != null && ov.star2 > target ? ov.star2 : NumberFormat.RoundNice((int)((long)target * cfg.star2Permille / 1000));
            spec.Star3 = ov != null && ov.star3 > spec.Star2 ? ov.star3 : NumberFormat.RoundNice((int)((long)target * cfg.star3Permille / 1000));
            if (spec.Star3 <= spec.Star2) spec.Star3 = spec.Star2 + 50;
            int perMove = (int)((long)target * cfg.leftoverBonusPermille / 1000 / Math.Max(1, moves));
            spec.LeftoverBonusPerMove = Math.Max(10, (perMove + 5) / 10 * 10);
            return spec;
        }

        void SizeFor(int n, out int cols, out int rows)
        {
            cols = 8;
            rows = 9;
            if (cfg.boardSizes == null) return;
            int bestFrom = int.MinValue;
            foreach (var step in cfg.boardSizes)
            {
                if (step == null || step.fromLevel > n || step.fromLevel < bestFrom) continue;
                bestFrom = step.fromLevel;
                cols = Math.Max(3, step.columns);
                rows = Math.Max(3, step.rows);
            }
        }

        LevelLayout GeneratedLayout(int n, int cols, int rows, Rng rng)
        {
            if (n < cfg.stonesFromLevel || n % 5 == 1) return LevelLayout.Open(cols, rows);
            int tier = Math.Min(3, (n - cfg.stonesFromLevel) / 15);
            bool shaped = n >= cfg.shapedBoardsFromLevel && rng.ChancePermille(300);
            var pool = shaped ? LayoutLibrary.ShapedPatterns : LayoutLibrary.StonePatterns;
            return LayoutLibrary.Build(pool[rng.Next(pool.Length)], cols, rows, tier, rng);
        }

        static int DefaultColours(int n)
        {
            if (n <= 2) return 3;
            if (n <= 12) return 4;
            if (n <= 30) return n % 3 == 0 ? 5 : 4;
            if (n <= 80) return 5;
            return n % 4 == 0 ? 6 : 5;
        }

        static int DefaultMoves(int n, LevelLayout layout, Rng rng)
        {
            int cells = layout.Width * layout.Height;
            if (cells <= 36) return 12;
            if (cells <= 49) return 15;
            return 16 + rng.Next(7);
        }

        static int DefaultAssist(int n)
        {
            return Math.Max(30, 280 - 14 * (n - 1));
        }

        static int[] DefaultWeights(int n, int colours)
        {
            var w = new int[colours];
            for (int i = 0; i < colours; i++) w[i] = 10;
            if (n >= 12 && n % 7 == 3) w[n % colours] = 15;
            else if (n >= 12 && n % 7 == 5) w[n % colours] = 6;
            return w;
        }

        string DefaultNote(int n, LevelLayout layout)
        {
            if (IsRegionFinale(n)) return "Region finale";
            if (layout.StoneCount > 0) return "Stones block falling tiles";
            return "";
        }

        int TargetPermille(int n)
        {
            var levels = cfg.targetCurveLevels;
            var values = cfg.targetCurvePermille;
            int count = Math.Min(levels.Length, values.Length);
            if (count == 0) return 600;
            if (n <= levels[0]) return values[0];
            for (int i = 1; i < count; i++)
            {
                if (n > levels[i]) continue;
                int span = Math.Max(1, levels[i] - levels[i - 1]);
                return values[i - 1] + (values[i] - values[i - 1]) * (n - levels[i - 1]) / span;
            }
            return values[count - 1];
        }
    }
}
