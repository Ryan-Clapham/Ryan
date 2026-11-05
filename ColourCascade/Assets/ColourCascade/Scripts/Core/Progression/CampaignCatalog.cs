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
        // One lazily built entry per level: a level is built once even when asked for from two threads, and different
        // levels can be built at the same time (generation shares no mutable state but the progression's plans, which
        // lock themselves).
        readonly Dictionary<int, System.Lazy<LevelSpec>> cache = new Dictionary<int, System.Lazy<LevelSpec>>();
        readonly Dictionary<int, LevelOverride> overrides = new Dictionary<int, LevelOverride>();
        readonly MechanicSchedule schedule;
        readonly CampaignProgression progression;
        // Built-in introduction levels (not replaced by the config): never given fewer moves than calibration says.
        readonly HashSet<LevelOverride> builtInIntros = new HashSet<LevelOverride>();

        public CampaignCatalog(CampaignConfig cfg, ScoringRules scoring)
        {
            this.cfg = cfg;
            this.scoring = scoring;
            schedule = cfg.mechanics ?? new MechanicSchedule();
            progression = new CampaignProgression(cfg.progressionFromLevel > 0 ? cfg.progressionFromLevel : int.MaxValue, cfg.shapes, schedule);
            // Built-in introduction levels first, so a hand-made level in the config can replace any of them.
            foreach (var o in CampaignDesign.IntroLevels(schedule))
                if (o != null && o.level >= 1)
                {
                    overrides[o.level] = o;
                    builtInIntros.Add(o);
                }
            if (cfg.overrides != null)
                foreach (var o in cfg.overrides)
                    if (o != null && o.level >= 1) overrides[o.level] = o;
        }

        public MechanicSchedule Schedule => schedule;
        public CampaignProgression Progression => progression;

        public CampaignConfig Config => cfg;
        public int LevelsPerRegion => Math.Max(1, cfg.levelsPerRegion);

        public RegionInfo RegionForLevel(int level) => Regions.ForLevel(level, LevelsPerRegion);
        public RegionInfo Region(int index) => Regions.ForIndex(index, LevelsPerRegion);
        public bool IsMilestone(int level) => cfg.milestoneEvery > 0 && level % cfg.milestoneEvery == 0;
        public bool IsRegionFinale(int level) => level % LevelsPerRegion == 0;

        readonly object gate = new object();

        /// <summary>The level (generated the first time it is asked for, then kept). Safe to call from any thread: a
        /// level being generated in the background (Prewarm) is waited for, never built twice.</summary>
        public LevelSpec Get(int level)
        {
            if (level < 1) level = 1;
            System.Lazy<LevelSpec> entry;
            lock (gate)
            {
                if (!cache.TryGetValue(level, out entry))
                {
                    int n = level;
                    entry = new System.Lazy<LevelSpec>(() => Build(n), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
                    cache[level] = entry;
                }
            }
            try
            {
                return entry.Value;
            }
            catch
            {
                // Never keep a failed build: the next request tries again.
                lock (gate)
                    if (cache.TryGetValue(level, out var current) && current == entry) cache.Remove(level);
                throw;
            }
        }

        public bool IsGenerated(int level)
        {
            lock (gate) return cache.TryGetValue(level, out var entry) && entry.IsValueCreated;
        }

        /// <summary>
        /// Generates a level on a background thread so it is ready when the player gets there (later levels check their
        /// fairness with bot games, which takes a moment: about 60-120 ms on a desktop, several times that on a phone).
        /// Does nothing if it is already generated. Errors are ignored: Get would build it again.
        /// </summary>
        public void Prewarm(int level)
        {
            if (level < 1) return;
            lock (gate)
                if (cache.ContainsKey(level)) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Get(level);
                }
                catch (Exception)
                {
                }
            });
        }

        public void ClearCache()
        {
            lock (gate) cache.Clear();
        }

        LevelSpec Build(int n)
        {
            ulong seed = StableHash.Of("campaign", n);
            var rng = new Rng(seed);
            LevelOverride ov;
            overrides.TryGetValue(n, out ov);
            if (ov == null && n >= progression.FromLevel) return BuildProgression(n, seed);

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
                // Chains need a matching neighbour to break, so chain levels use fewer colours until late on. Bombs,
                // prisms and combinations need big groups (9+, 12+, two specials side by side): levels that ask for
                // them use four colours too.
                if (recipe != null && (ov == null || ov.colours <= 0))
                {
                    // Six colours with obstacles to clear made levels a thoughtful player failed half the time.
                    colours = Math.Min(colours, 5);
                    if (recipe.Has(Mechanic.Chains)) colours = Math.Min(colours, n < 201 ? 4 : 5);
                    if (recipe.Has(Mechanic.Specials) && (!SpecialEffects.IsRocket(recipe.SpecialKind) || recipe.SpecialGoal == GoalKind.FireCombo)) colours = Math.Min(colours, 4);
                }
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
                Specials = ov != null && ov.specials >= 0 ? (SpecialTier)Math.Min(3, ov.specials) : CampaignDesign.SpecialsFor(n, schedule),
            };
            if (recipe != null && !DecorateAndSize(spec, recipe, n)) recipe = null;
            if (recipe == null && goals != null && goals.Length > 0)
            {
                spec.Goals = goals;
                SetMossCap(spec);
                if (ov != null && ov.mossCap > 0) spec.MossCap = ov.mossCap;
                // Built-in introductions never get fewer moves than a casual player needs, with room to spare; levels
                // from the config (or imported) keep exactly the moves they give.
                if (ov != null && ov.moves > 0 && !builtInIntros.Contains(ov)) spec.Moves = ov.moves;
                else
                {
                    int calibrated = CalibrateMoves(spec, n, true);
                    spec.Moves = ov != null && ov.moves > 0 ? Math.Max(ov.moves, calibrated) : calibrated;
                }
            }
            return Finish(spec, n, ov);
        }

        /// <summary>
        /// A generated level from the progression on (update 3.2, docs/DESIGN.md §3.5): the plan from CampaignProgression,
        /// its board shape, the recipe placed with CampaignPlacement, moves sized from a thoughtful player (Planner bot)
        /// with spare room that shrinks as the intensity rises, and a check that a thoughtful player passes it reliably.
        /// A level that fails the check is decorated again, given more moves within its band, then made smaller; it is
        /// never left unfair.
        /// </summary>
        LevelSpec BuildProgression(int n, ulong seed)
        {
            var plan = progression.Plan(n);
            var region = RegionForLevel(n);
            var shapeRng = new Rng(StableHash.Combine(seed, 0x5A9E));
            var shape = BoardShapes.Build(plan.Family, plan.Variant, 3, shapeRng);
            string error;
            if (!BoardShapes.IsFair(shape.Layout, out error))
            {
                plan.Family = BoardFamily.Rectangle;
                plan.ShapeIntro = false;
                shape = BoardShapes.Build(BoardFamily.Rectangle, 2, 0, shapeRng);
            }
            // Small separate sections need bigger groups to be found: four colours there.
            int colours = plan.Colours;
            int smallest = int.MaxValue;
            foreach (var section in BoardShapes.Sections(shape.Layout)) smallest = Math.Min(smallest, section.Count);
            if (smallest < 40) colours = Math.Min(colours, 4);
            var spec = new LevelSpec
            {
                Id = "campaign:" + n,
                Mode = GameMode.Campaign,
                Number = n,
                Title = "Level " + n,
                Layout = shape.Layout,
                Colours = colours,
                ColourWeights = DefaultWeights(n, colours),
                AssistPermille = DefaultAssist(n),
                Moves = DefaultMoves(n, shape.Layout, new Rng(seed)),
                Seed = seed,
                RegionIndex = region.Index,
                IsMilestone = IsMilestone(n),
                IsRegionFinale = IsRegionFinale(n),
                TutorialKey = "",
                Specials = CampaignDesign.SpecialsFor(n, schedule),
            };
            var recipe = plan.Recipe;
            if (recipe != null)
            {
                var result = DecorateProgression(spec, plan, recipe, n);
                if (result == Decoration.Unproven && plan.Family != BoardFamily.Rectangle)
                {
                    // No version passed the fairness check on this board: the same goals on a plain board instead.
                    var plain = BoardShapes.Build(BoardFamily.Rectangle, 2 + (int)(seed % 2), 0, shapeRng);
                    var keep = (spec.Layout, spec.Goals, spec.Moves, spec.Colours, spec.ColourWeights, plan.Family, plan.ShapeIntro);
                    spec.Layout = plain.Layout;
                    spec.Colours = plan.Colours;
                    spec.ColourWeights = DefaultWeights(n, plan.Colours);
                    plan.Family = BoardFamily.Rectangle;
                    plan.ShapeIntro = false;
                    var second = DecorateProgression(spec, plan, recipe, n);
                    if (second == Decoration.Nothing || (second == Decoration.Unproven && keep.Item1 != null))
                    {
                        (spec.Layout, spec.Goals, spec.Moves, spec.Colours, spec.ColourWeights, plan.Family, plan.ShapeIntro) = keep;
                        SetMossCap(spec);
                    }
                    result = second == Decoration.Nothing ? result : second;
                }
                if (result == Decoration.Nothing) recipe = null;
            }
            if (recipe == null)
            {
                spec.Goals = null;
                spec.Layout = shape.Layout;
            }
            spec.Note = CampaignProgression.NoteFor(plan, recipe, spec.IsRegionFinale);
            return Finish(spec, n, null);
        }

        /// <summary>
        /// Places a progression recipe and sizes its moves. The level has a budget: what a thoughtful player should need,
        /// 12 + intensity / 4 moves (13 to 37; two more with treasure). A placement that needs over 30% more is made
        /// smaller (80%, 65%, 50%) before it is checked, so levels grow in what they ask, not in how long they take; one
        /// a thoughtful player clears in a handful of moves gets a bigger version. Each version must then pass Solvable,
        /// with more moves within the band if needed. Unproven when no version passed it (the gentlest is kept), Nothing
        /// when nothing could be placed.
        /// </summary>
        enum Decoration { Nothing, Proven, Unproven }

        Decoration DecorateProgression(LevelSpec spec, LevelPlan plan, LevelRecipe recipe, int n)
        {
            var baseLayout = spec.Layout;
            var band = CampaignDesign.ProgressionBand(n);
            var tries = new[] { 100, 100, 80, 65, 50 };
            bool any = false, grew = false;
            LevelLayout bestLayout = null;
            GoalSpec[] bestGoals = null;
            int bestMoves = 0, bestNeeded = int.MaxValue;
            bool treasure = recipe.Has(Mechanic.Treasure);
            int budget = 12 + plan.Intensity / 4 + (treasure ? 2 : 0);
            int high = budget * 13 / 10;
            for (int attempt = 0; attempt < tries.Length; attempt++)
            {
                int scale = tries[attempt];
                var r = scale == 100 ? recipe : CampaignDesign.Scaled(recipe, scale);
                if (grew && attempt == 1) r = CampaignDesign.Scaled(recipe, 135);
                var layout = baseLayout.Clone();
                var rng = new Rng(StableHash.Combine(spec.Seed, 0xD3A0 + (ulong)attempt));
                var goals = CampaignProgression.Decorate(r, layout, spec.Colours, spec.ColourWeights, rng);
                string error;
                if (goals.Length == 0 || !layout.Validate(out error)) continue;
                spec.Layout = layout;
                spec.Goals = goals;
                SetMossCap(spec);
                int needed;
                spec.Moves = ProgressionMoves(spec, n, plan, out needed);
                if (r.Has(Mechanic.Score)) AddScoreGoal(spec, true);
                any = true;
                if (needed < bestNeeded)
                {
                    bestNeeded = needed;
                    bestLayout = spec.Layout;
                    bestGoals = spec.Goals;
                    bestMoves = spec.Moves;
                }
                if (attempt == 0 && !grew && needed <= 6 && !plan.Breather && !plan.ShapeIntro)
                {
                    // Passed by a thoughtful player in a handful of moves: try a bigger version once.
                    grew = true;
                    continue;
                }
                // More than the level's budget: try a smaller version (the last one is checked whatever it needs).
                if (needed > high && attempt < tries.Length - 1) continue;
                int max = MaxProgressionMoves(band, spec.Layout.TreasureCount > 0);
                for (int extra = 0; extra < 3; extra++)
                {
                    if (Solvable(spec, plan)) return Decoration.Proven;
                    if (spec.Moves >= max) break;
                    spec.Moves = Math.Min(max, spec.Moves + Math.Max(3, spec.Moves / 6));
                }
            }
            if (!any) return Decoration.Nothing;
            // Nothing passed the check: keep the gentlest version tried, at the band's most moves.
            spec.Layout = bestLayout;
            spec.Goals = bestGoals;
            spec.Moves = Math.Max(bestMoves, MaxProgressionMoves(band, bestLayout.TreasureCount > 0));
            SetMossCap(spec);
            return Decoration.Unproven;
        }

        static int MaxProgressionMoves(DifficultyBand band, bool treasure) => band.MaxMoves + (treasure ? 3 : 0);

        /// <summary>
        /// Moves for a progression level: what a thoughtful player needs in three of four of the games it passes (Planner
        /// bot, 75th percentile; games that stall are Solvable's business), plus spare room: 50% at the lowest intensity
        /// down to 20% at the highest, more for breathers, new boards, treasure (it depends on how tiles fall) and
        /// special-tile goals; at least 4 and at most 14 more than needed.
        /// </summary>
        int ProgressionMoves(LevelSpec spec, int n, LevelPlan plan, out int needed)
        {
            var band = CampaignDesign.ProgressionBand(n);
            bool treasure = spec.Layout.TreasureCount > 0, specials = false;
            if (spec.Goals != null)
                foreach (var g in spec.Goals) specials |= g.IsSpecialGoal;
            int passed;
            needed = LevelCalibrator.MovesNeededWhenPassed(spec, scoring, Math.Max(6, cfg.goalCalibrationRuns * 8 / 10), 80, BotKind.Planner, 75, out passed);
            int slack = 500 - 3 * plan.Intensity + (plan.Breather ? 200 : 0) + (plan.ShapeIntro ? 150 : 0) + (treasure ? 100 : 0) + (specials ? 50 : 0);
            int moves = (needed * (1000 + slack) + 999) / 1000;
            moves = Math.Max(needed + 4, Math.Min(needed + 14, moves));
            return Math.Max(band.MinMoves, Math.Min(MaxProgressionMoves(band, treasure), moves));
        }

        /// <summary>
        /// A thoughtful player (Planner bot) passes it in 10 games of 12 (11 for breathers and new boards; 9 with
        /// treasure), and again in 9 of 12 more on other boards (10; 8; 5 of 6 after a clean sweep). The second set matters: several placements and move counts are tried against
        /// the first, so a lucky one could pass it alone.
        /// </summary>
        bool Solvable(LevelSpec spec, LevelPlan plan)
        {
            bool gentle = plan.Breather || plan.ShapeIntro;
            // Treasure: one game in four may stall with the last tile under a treasure unmatched. The bot looks one move
            // ahead and never sets that match up; a player can (and has Tile Breaker and Blast for it).
            int slack = spec.Layout.TreasureCount > 0 && !gentle ? 1 : 0;
            int wins = LevelCalibrator.PassCount(spec, scoring, 12, BotKind.Planner, 600);
            if (wins < (gentle ? 11 : 10) - slack) return false;
            // A clean sweep needs a shorter second look.
            if (wins == 12) return LevelCalibrator.PassCount(spec, scoring, 6, BotKind.Planner, 800) >= 5 - slack;
            int confirm = LevelCalibrator.PassCount(spec, scoring, 12, BotKind.Planner, 800);
            return confirm >= (gentle ? 10 : 9) - slack;
        }

        /// <summary>Stars for a goal level, or the target and stars of a score level.</summary>
        LevelSpec Finish(LevelSpec spec, int n, LevelOverride ov)
        {
            if (spec.Goals != null && spec.Goals.Length > 0)
            {
                CalibrateStars(spec, ov, false);
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
            if (n >= schedule.iceFromLevel && (ov == null || ov.star2 <= 0 || ov.star3 <= 0))
            {
                // From the first mechanic on, score levels get calibrated stars too: players keep going after the
                // target, so stars measure what they score with every move, not how early they reach it.
                CalibrateStars(spec, ov, true);
                return spec;
            }
            spec.Star2 = ov != null && ov.star2 > target ? ov.star2 : NumberFormat.RoundNice((int)((long)target * cfg.star2Permille / 1000));
            spec.Star3 = ov != null && ov.star3 > spec.Star2 ? ov.star3 : NumberFormat.RoundNice((int)((long)target * cfg.star3Permille / 1000));
            if (spec.Star3 <= spec.Star2) spec.Star3 = spec.Star2 + 50;
            return spec;
        }

        static void SetMossCap(LevelSpec spec)
        {
            int moss = spec.Layout.ObstacleCount(ObstacleKind.Moss);
            spec.MossCap = moss > 0 ? moss + 2 : 0;
        }

        /// <summary>
        /// Puts a recipe on the board and sizes its moves. If a casual player would need more moves than the band
        /// allows, the recipe is scaled down (80%, 65%, then 50%) and tried again. False if nothing could be placed.
        /// </summary>
        bool DecorateAndSize(LevelSpec spec, LevelRecipe recipe, int n)
        {
            var band = CampaignDesign.BandFor(n);
            var baseLayout = spec.Layout;
            // Treasure needs open columns to the floor: treasure levels use an open board of the same size.
            if (recipe.Has(Mechanic.Treasure) && (baseLayout.StoneCount > 0 || baseLayout.OpenCellCount < baseLayout.Width * baseLayout.Height))
                baseLayout = LevelLayout.Open(baseLayout.Width, baseLayout.Height);
            // "...among the stones": a stone board even where the level would have been open.
            else if (recipe.StoneLayout && baseLayout.StoneCount == 0)
            {
                var stoneRng = new Rng(StableHash.Combine(spec.Seed, 0xD360));
                int tier = Math.Min(3, Math.Max(0, n - cfg.stonesFromLevel) / 15);
                baseLayout = LayoutLibrary.Build(LayoutLibrary.StonePatterns[stoneRng.Next(LayoutLibrary.StonePatterns.Length)], baseLayout.Width, baseLayout.Height, tier, stoneRng);
            }
            // Scaled amounts never go below a working minimum (CampaignDesign.Scaled): halving 4 ice to 2 once left
            // levels a thoughtful player passed in a move or two (simulation).
            int[] scales = { 100, 80, 65, 50 };
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
                return false;
            }
            if (recipe.Has(Mechanic.Score)) AddScoreGoal(spec);
            return true;
        }

        /// <summary>
        /// "...and reach a score": the score most casual players who meet the other goals also reach with every move
        /// played (scoreGoalPercentile of their final scores), so the extra goal asks for efficient play, not luck.
        /// </summary>
        void AddScoreGoal(LevelSpec spec, bool plannerFallback = false)
        {
            var casual = Finals(LevelCalibrator.PassedResults(spec, scoring, Math.Max(5, cfg.goalCalibrationRuns * 2), BotKind.Casual, 200));
            // Later levels (the progression): when few casual games pass, the score comes from a thoughtful player's
            // lower games instead, so a demanding level still gets its score goal.
            if (plannerFallback && casual.Count < 4) casual = Finals(LevelCalibrator.PassedResults(spec, scoring, 8, BotKind.Planner, 700));
            if (casual.Count == 0) return;
            int amount = NumberFormat.RoundNice(Math.Max(100, Percentile(casual, cfg.scoreGoalPercentile)));
            var goals = new GoalSpec[spec.Goals.Length + 1];
            Array.Copy(spec.Goals, goals, spec.Goals.Length);
            goals[spec.Goals.Length] = GoalSpec.Score(amount);
            spec.Goals = goals;
            spec.Target = amount;
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
            // A casual player never aims for big groups, so goals about special tiles are sized from a thoughtful
            // player (Planner bot) instead: from its slower games (making a special depends on the board), with extra
            // room for players who are still learning to build them.
            bool specials = false, combos = false;
            if (spec.Goals != null)
                foreach (var g in spec.Goals)
                {
                    specials |= g.IsSpecialGoal;
                    combos |= g.kind == GoalKind.FireCombo;
                }
            if (specials) needed = (LevelCalibrator.MovesNeeded(spec, scoring, Math.Max(4, cfg.goalCalibrationRuns), 80, BotKind.Planner, 75) * (combos ? 15 : 13) + 9) / 10;
            // Treasure depends a lot on how the tiles under it fall: sized from a casual player's slower games.
            else if (spec.Layout.TreasureCount > 0) needed = LevelCalibrator.MovesNeeded(spec, scoring, Math.Max(4, cfg.goalCalibrationRuns), 80, BotKind.Casual, 75);
            else needed = LevelCalibrator.MedianMovesNeeded(spec, scoring, Math.Max(3, cfg.goalCalibrationRuns), 80, BotKind.Casual);
            int permille = 1000 + band.SlackPermille + CampaignDesign.RhythmPermille(n) + (practice ? 150 : 0);
            int moves = (needed * permille + 999) / 1000;
            return Math.Max(band.MinMoves, Math.Min(band.MaxMoves, moves));
        }

        /// <summary>
        /// Calibrated stars (docs/DESIGN.md §3.2), for goal levels and for score levels from the first mechanic on.
        /// Passing the level (every goal met) is one star. Two and three come from the final scores of bot games that,
        /// like a player chasing stars, play on to the last move once the goals are done: two stars halfway between a
        /// casual player's median and a lower percentile of a thoughtful player's (Planner bot), three stars around
        /// the thoughtful player's median. Medians, not extremes, so a lucky calibration game cannot put three stars
        /// out of reach. Unused moves score nothing (there is no spare-move bonus since 2.1). Goal levels show a
        /// target below two stars.
        /// </summary>
        void CalibrateStars(LevelSpec spec, LevelOverride ov, bool scoreLevel)
        {
            int runs = Math.Max(3, cfg.goalCalibrationRuns * 4 / 5);
            var planner = Finals(LevelCalibrator.PassedResults(spec, scoring, runs, BotKind.Planner, 100));
            var casual = Finals(LevelCalibrator.PassedResults(spec, scoring, runs * 2, BotKind.Casual, 300));
            int moves = Math.Max(1, spec.Moves);
            int median = planner.Count > 0 ? planner[planner.Count / 2] : casual.Count > 0 ? casual[casual.Count / 2] * 3 / 2 : 150 * moves;
            int casualMedian = casual.Count > 0 ? casual[casual.Count / 2] : median * 3 / 5;
            int star2 = (casualMedian + (planner.Count > 0 ? Percentile(planner, cfg.star2Percentile) : median * 4 / 5)) / 2;
            int star3 = planner.Count > 0 ? Percentile(planner, cfg.star3Percentile) : median;
            int scoreGoal = ScoreGoalOf(spec);
            if (scoreGoal > 0) spec.Target = scoreGoal;
            int floor2 = scoreLevel || scoreGoal > 0 ? spec.Target * 23 / 20 : 100;
            star2 = Math.Max(star2, floor2);
            // Bombs and prisms make for a few huge games: keep three stars within reach of a good one.
            star3 = Math.Max(star2 * 11 / 10, Math.Min(star3, star2 * 29 / 20));
            spec.Star2 = ov != null && ov.star2 > 0 ? ov.star2 : NumberFormat.RoundNice(star2);
            spec.Star3 = ov != null && ov.star3 > spec.Star2 ? ov.star3 : NumberFormat.RoundNice(Math.Max(star3, spec.Star2 + 1));
            if (spec.Star3 <= spec.Star2) spec.Star3 = spec.Star2 + 50;
            if (!scoreLevel && scoreGoal <= 0) spec.Target = ov != null && ov.target > 0 ? ov.target : NumberFormat.RoundNice(Math.Max(50, spec.Star2 * 3 / 4));
        }

        static int ScoreGoalOf(LevelSpec spec)
        {
            if (spec.Goals == null) return 0;
            foreach (var g in spec.Goals) if (g.kind == GoalKind.Score) return g.amount;
            return 0;
        }

        static List<int> Finals(List<LevelCalibrator.GameResult> results)
        {
            var finals = new List<int>(results.Count);
            foreach (var r in results) finals.Add(r.Score);
            finals.Sort();
            return finals;
        }

        static int Percentile(List<int> sorted, int pct) => sorted[Math.Max(0, Math.Min(sorted.Count - 1, sorted.Count * pct / 100))];


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
