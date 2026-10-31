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
        readonly MechanicSchedule schedule;

        public CampaignCatalog(CampaignConfig cfg, ScoringRules scoring)
        {
            this.cfg = cfg;
            this.scoring = scoring;
            schedule = cfg.mechanics ?? new MechanicSchedule();
            // Built-in introduction levels first, so a hand-made level in the config can replace any of them.
            foreach (var o in CampaignDesign.IntroLevels(schedule))
                if (o != null && o.level >= 1) overrides[o.level] = o;
            if (cfg.overrides != null)
                foreach (var o in cfg.overrides)
                    if (o != null && o.level >= 1) overrides[o.level] = o;
        }

        public MechanicSchedule Schedule => schedule;

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
                    if (ov.obstacles != null && ov.obstacles.Length > 0) layout.ApplyObstacleRows(ov.obstacles);
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

            // Mechanics and goals. Generated levels get a recipe from their own generator, so the board shape and
            // everything drawn from rng above stay exactly as they were before mechanics existed.
            GoalSpec[] goals = null;
            LevelRecipe recipe = null;
            if (ov != null && ov.goals != null && ov.goals.Length > 0)
            {
                goals = new GoalSpec[ov.goals.Length];
                for (int i = 0; i < ov.goals.Length; i++) goals[i] = ov.goals[i].Clone();
            }
            else if (ov == null || ov.layout == null || ov.layout.Length == 0)
            {
                var designRng = new Rng(StableHash.Combine(seed, 0xD351));
                recipe = CampaignDesign.Recipe(n, schedule, designRng);
                // Chains need a matching neighbour to break, so chain levels use fewer colours until late on.
                if (recipe != null && recipe.Has(Mechanic.Chains) && (ov == null || ov.colours <= 0)) colours = Math.Min(colours, n < 121 ? 4 : 5);
            }

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
                Note = ov != null && !string.IsNullOrEmpty(ov.note) ? ov.note : recipe != null ? CampaignDesign.NoteFor(recipe) : DefaultNote(n, layout),
                Specials = CampaignDesign.SpecialsFor(n, schedule),
            };
            if (recipe != null && !DecorateAndSize(spec, recipe, n)) recipe = null;
            if (recipe == null && goals != null && goals.Length > 0)
            {
                spec.Goals = goals;
                SetMossCap(spec);
                // Hand-made goal levels (the introductions) never get fewer moves than a casual player needs, with room to spare.
                int calibrated = CalibrateMoves(spec, n, true);
                spec.Moves = ov != null && ov.moves > 0 ? Math.Max(ov.moves, calibrated) : calibrated;
            }
            if (spec.Goals != null && spec.Goals.Length > 0)
            {
                CalibrateStars(spec, ov);
                return spec;
            }

            int target = ov != null && ov.target > 0 ? ov.target : 0;
            if (target <= 0)
            {
                if (n < schedule.iceFromLevel)
                {
                    int median = LevelCalibrator.MedianFullScore(spec, scoring, cfg.calibrationRuns, BotKind.Greedy);
                    target = NumberFormat.RoundNice((int)((long)median * TargetPermille(n) / 1000));
                }
                else
                {
                    // From the first mechanic on, score targets come from what a casual player scores with every move
                    // played: the band and the ten-level rhythm pick how many casual games should reach it.
                    var scores = LevelCalibrator.FullScores(spec, scoring, Math.Max(5, cfg.calibrationRuns), BotKind.Casual);
                    int pct = CampaignDesign.ScoreTargetPercentile(n);
                    target = NumberFormat.RoundNice(Math.Max(100, scores[Math.Min(scores.Length - 1, scores.Length * pct / 100)]));
                }
            }
            spec.Target = target;
            if (ov != null && ov.goals != null && ov.goals.Length > 0)
            {
                spec.Goals = new GoalSpec[ov.goals.Length];
                for (int i = 0; i < ov.goals.Length; i++) spec.Goals[i] = ov.goals[i].Clone();
            }
            spec.Star2 = ov != null && ov.star2 > target ? ov.star2 : NumberFormat.RoundNice((int)((long)target * cfg.star2Permille / 1000));
            spec.Star3 = ov != null && ov.star3 > spec.Star2 ? ov.star3 : NumberFormat.RoundNice((int)((long)target * cfg.star3Permille / 1000));
            if (spec.Star3 <= spec.Star2) spec.Star3 = spec.Star2 + 50;
            int perMove = (int)((long)target * cfg.leftoverBonusPermille / 1000 / Math.Max(1, moves));
            spec.LeftoverBonusPerMove = Math.Max(10, (perMove + 5) / 10 * 10);
            return spec;
        }

        static void SetMossCap(LevelSpec spec)
        {
            int moss = spec.Layout.ObstacleCount(ObstacleKind.Moss);
            spec.MossCap = moss > 0 ? moss + 2 : 0;
        }

        /// <summary>
        /// Puts a recipe on the board and sizes its moves. If a casual player would need more moves than the band
        /// allows, the recipe is scaled down (70%, then 50%) and tried again. False if nothing could be placed.
        /// </summary>
        bool DecorateAndSize(LevelSpec spec, LevelRecipe recipe, int n)
        {
            var band = CampaignDesign.BandFor(n);
            var baseLayout = spec.Layout;
            // Treasure needs open columns to the floor: treasure levels use an open board of the same size.
            if (recipe.Has(Mechanic.Treasure) && (baseLayout.StoneCount > 0 || baseLayout.OpenCellCount < baseLayout.Width * baseLayout.Height))
                baseLayout = LevelLayout.Open(baseLayout.Width, baseLayout.Height);
            int[] scales = { 100, 70, 50 };
            bool any = false;
            for (int attempt = 0; attempt < scales.Length; attempt++)
            {
                var r = scales[attempt] == 100 ? recipe : CampaignDesign.Scaled(recipe, scales[attempt]);
                var layout = baseLayout.Clone();
                var rng = new Rng(StableHash.Combine(spec.Seed, 0xD352 + (ulong)attempt));
                var goals = CampaignDesign.Decorate(r, layout, spec.Colours, spec.ColourWeights, rng);
                string error;
                if (goals.Length == 0 || !layout.Validate(out error)) continue;
                spec.Layout = layout;
                spec.Goals = goals;
                SetMossCap(spec);
                any = true;
                int needed;
                spec.Moves = CalibrateMoves(spec, n, recipe.Practice || recipe.Has(Mechanic.Treasure), out needed);
                int permille = 1000 + band.SlackPermille + CampaignDesign.RhythmPermille(n) + (recipe.Practice || recipe.Has(Mechanic.Treasure) ? 150 : 0);
                if ((long)needed * permille / 1000 <= band.MaxMoves + (layout.TreasureCount > 0 ? 5 : 0)) break;
            }
            if (!any)
            {
                spec.Goals = null;
                spec.Note = DefaultNote(n, spec.Layout);
            }
            return any;
        }

        int CalibrateMoves(LevelSpec spec, int n, bool practice)
        {
            int needed;
            return CalibrateMoves(spec, n, practice, out needed);
        }

        /// <summary>
        /// Moves for a goal level: what a casual player typically needs (median of bot games with plenty of moves),
        /// plus the band's spare room and the ten-level rhythm, within the band's move range.
        /// </summary>
        int CalibrateMoves(LevelSpec spec, int n, bool practice, out int needed)
        {
            var band = CampaignDesign.BandFor(n);
            // Treasure has to be dug out tile by tile: those levels may go a few moves past the band's limit.
            if (spec.Layout.TreasureCount > 0) band.MaxMoves += 5;
            needed = LevelCalibrator.MedianMovesNeeded(spec, scoring, Math.Max(3, cfg.goalCalibrationRuns), 80, BotKind.Casual);
            int permille = 1000 + band.SlackPermille + CampaignDesign.RhythmPermille(n) + (practice ? 150 : 0);
            int moves = (needed * permille + 999) / 1000;
            return Math.Max(band.MinMoves, Math.Min(band.MaxMoves, moves));
        }

        /// <summary>
        /// Stars for a goal level: completing it is one star; two and three come from the scores a thoughtful
        /// player (the Planner bot) finishes with, spare-move bonus included. The target shown is below two stars.
        /// </summary>
        void CalibrateStars(LevelSpec spec, LevelOverride ov)
        {
            var results = LevelCalibrator.PlannerResults(spec, scoring, Math.Max(3, cfg.goalCalibrationRuns * 3 / 5));
            int moves = Math.Max(1, spec.Moves);
            int medianScore = results.Count > 0 ? results[results.Count / 2].Score : 0;
            if (medianScore <= 0) medianScore = 150 * moves;
            int perMove = (int)((long)medianScore * cfg.leftoverBonusPermille / 1000 / moves);
            spec.LeftoverBonusPerMove = Math.Max(10, (perMove + 5) / 10 * 10);
            var finals = new List<int>();
            foreach (var r in results) finals.Add(r.Score + r.MovesLeft * spec.LeftoverBonusPerMove);
            finals.Sort();
            int star2, star3;
            if (finals.Count >= 2)
            {
                star2 = finals[finals.Count * 35 / 100];
                star3 = finals[Math.Min(finals.Count - 1, finals.Count * 75 / 100)];
            }
            else
            {
                star2 = medianScore * 5 / 4;
                star3 = medianScore * 3 / 2;
            }
            spec.Star2 = ov != null && ov.star2 > 0 ? ov.star2 : NumberFormat.RoundNice(Math.Max(100, star2));
            spec.Star3 = ov != null && ov.star3 > spec.Star2 ? ov.star3 : NumberFormat.RoundNice(Math.Max(star3, spec.Star2 + 1));
            if (spec.Star3 <= spec.Star2) spec.Star3 = spec.Star2 + 50;
            spec.Target = ov != null && ov.target > 0 ? ov.target : NumberFormat.RoundNice(Math.Max(50, spec.Star2 * 3 / 4));
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
