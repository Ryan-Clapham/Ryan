using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Advanced Daily Challenge and Weekly Rush puzzles (update 3.3, docs/DESIGN.md §5 and §6), for players past the
    /// campaign's introductory levels (DailyConfig / RushConfig advancedFromLevel). They reuse the campaign's systems:
    /// board shapes from its library (only the families the player has already played), goal recipes sized by
    /// CampaignProgression and placed by CampaignPlacement (which only uses playable cells), and bot calibration. A
    /// puzzle that cannot be made fair falls back to the original challenge, so nobody is ever given a broken one.
    /// </summary>
    public sealed partial class ChallengeFactory
    {
        // How often each family is drawn for a Daily Challenge (once met): varied, mostly shaped, with fewer plain boards
        // and fewer of the slowest shapes.
        static readonly (BoardFamily family, int weight)[] DailyFamilies =
        {
            (BoardFamily.Rectangle, 4), (BoardFamily.Obstructed, 5), (BoardFamily.Hollow, 10), (BoardFamily.Corners, 10), (BoardFamily.Tower, 8),
            (BoardFamily.Wide, 8), (BoardFamily.TwinTowers, 10), (BoardFamily.Bunks, 10), (BoardFamily.StoneWall, 10), (BoardFamily.Passage, 8),
            (BoardFamily.Asymmetric, 10), (BoardFamily.TShape, 10), (BoardFamily.Cross, 10), (BoardFamily.Diamond, 10),
            (BoardFamily.ThreeSections, 5), (BoardFamily.Islands, 4),
        };

        // Weekly Rush boards are split or irregular (once met); plain or stone boards only until the first is met.
        static readonly (BoardFamily family, int weight)[] WeeklyFamilies =
        {
            (BoardFamily.Hollow, 8), (BoardFamily.Corners, 8), (BoardFamily.TwinTowers, 10), (BoardFamily.Bunks, 10), (BoardFamily.StoneWall, 10),
            (BoardFamily.Passage, 10), (BoardFamily.Asymmetric, 10), (BoardFamily.TShape, 10), (BoardFamily.Cross, 10), (BoardFamily.Diamond, 10),
            (BoardFamily.ThreeSections, 6), (BoardFamily.Islands, 5),
        };

        /// <summary>Whether a player at this campaign level gets the advanced Daily Challenge.</summary>
        public bool AdvancedDaily(int campaignLevel) => campaign != null && daily.advancedFromLevel > 0 && campaignLevel >= daily.advancedFromLevel;

        /// <summary>Whether a player at this campaign level gets the advanced Weekly Rush.</summary>
        public bool AdvancedWeekly(int campaignLevel) => campaign != null && rush.advancedFromLevel > 0 && campaignLevel >= rush.advancedFromLevel;

        /// <summary>
        /// The board families a player at a campaign level has played (each family's first level is behind them), as a
        /// bit per family; plain and stone boards always count. Players with the same families met share a puzzle.
        /// </summary>
        public int FamiliesMet(int campaignLevel)
        {
            int mask = (1 << (int)BoardFamily.Rectangle) | (1 << (int)BoardFamily.Obstructed);
            if (campaign == null) return mask;
            foreach (BoardFamily f in Enum.GetValues(typeof(BoardFamily)))
                if (campaign.Shapes.FromLevel(f) < campaignLevel) mask |= 1 << (int)f;
            return mask;
        }

        static bool Has(int mask, BoardFamily f) => (mask & (1 << (int)f)) != 0;

        /// <summary>
        /// How demanding an advanced challenge is (the campaign's 0-100 intensity): from about 38 for a player who has
        /// just reached the advanced challenges to about 68 once every shape is met, a little either way by the date.
        /// </summary>
        int AdvancedIntensity(int met, Rng rng)
        {
            int reach = Math.Max(daily.advancedFromLevel, 121);
            foreach (BoardFamily f in Enum.GetValues(typeof(BoardFamily)))
                if (Has(met, f) && campaign.Shapes.FromLevel(f) < 10000) reach = Math.Max(reach, campaign.Shapes.FromLevel(f));
            int i = 38 + (reach - 121) * 26 / 230 + rng.Next(9) - 4;
            return Math.Max(30, Math.Min(70, i));
        }

        static BoardFamily PickFamily((BoardFamily family, int weight)[] table, int met, Rng rng, BoardFamily fallback)
        {
            int total = 0;
            foreach (var e in table) if (Has(met, e.family)) total += e.weight;
            if (total <= 0) return fallback;
            int x = rng.Next(total);
            foreach (var e in table)
            {
                if (!Has(met, e.family)) continue;
                x -= e.weight;
                if (x < 0) return e.family;
            }
            return fallback;
        }

        /// <summary>The family's shape for a challenge, or a plain board when it would not be fair.</summary>
        BoardShape ChallengeShape(BoardFamily family, Rng rng)
        {
            var library = campaign.Library;
            var shape = library.Build(family, rng.Next(Math.Max(1, library.VariantCount(family))), 2, rng);
            if (!BoardShapes.IsFair(shape.Layout, out _)) shape = library.Build(BoardFamily.Rectangle, 2, 0, rng);
            return shape;
        }

        static int ColoursFor(LevelLayout layout, int colours)
        {
            // Small separate sections need bigger groups to be found: four colours there (as in the campaign).
            int smallest = int.MaxValue;
            foreach (var section in BoardShapes.Sections(layout)) smallest = Math.Min(smallest, section.Count);
            return smallest < 40 ? Math.Min(colours, 4) : colours;
        }

        static Mechanic? ToMechanic(MechanicSet m)
        {
            switch (m)
            {
                case MechanicSet.Ice: return Mechanic.Ice;
                case MechanicSet.Chains: return Mechanic.Chains;
                case MechanicSet.Treasure: return Mechanic.Treasure;
                case MechanicSet.Moss: return Mechanic.Moss;
                case MechanicSet.ColourGoals: return Mechanic.Colour;
                default: return null;
            }
        }

        static bool Unlocked(Mechanic m, MechanicSet unlocked)
        {
            switch (m)
            {
                case Mechanic.Ice: return unlocked.Has(MechanicSet.Ice);
                case Mechanic.Chains: return unlocked.Has(MechanicSet.Chains);
                case Mechanic.Treasure: return unlocked.Has(MechanicSet.Treasure);
                case Mechanic.Moss: return unlocked.Has(MechanicSet.Moss);
                case Mechanic.Colour: return true;
                case Mechanic.Specials: return (unlocked & (MechanicSet.Rockets | MechanicSet.Bombs | MechanicSet.Prisms)) != MechanicSet.None;
                default: return false;
            }
        }

        static bool InSections(BoardFamily f) => f == BoardFamily.TwinTowers || f == BoardFamily.Bunks || f == BoardFamily.Passage ||
                                               f == BoardFamily.ThreeSections || f == BoardFamily.Islands;

        static LevelPlan PlanFor(BoardFamily family, int intensity) => new LevelPlan { Level = 0, Tier = 0, Intensity = intensity, Family = family };

        static void SetMossCap(LevelSpec spec)
        {
            int moss = spec.Layout.ObstacleCount(ObstacleKind.Moss);
            spec.MossCap = moss > 0 ? moss + 2 : 0;
        }

        /// <summary>A copy for bot games that varies the refills from game to game (a fixed-seed puzzle would replay one
        /// game), so the checks see how the puzzle plays for different players.</summary>
        static LevelSpec Varied(LevelSpec s, int moves, GoalSpec[] goals)
        {
            return new LevelSpec
            {
                Id = s.Id, Mode = GameMode.Campaign, Layout = s.Layout, Colours = s.Colours, ColourWeights = s.ColourWeights,
                AssistPermille = s.AssistPermille, Moves = moves, Target = s.Target, Seed = s.Seed, FixedSeed = false,
                Specials = s.Specials, MossCap = s.MossCap, Goals = goals,
            };
        }

        // ------------------------------------------------------------------ Daily Challenge

        /// <summary>
        /// The advanced Daily Challenge: a board shape the player has met; the score target plus two objectives that go
        /// together (the campaign's pairings: obstacles with obstacles, special-tile goals with the obstacles that give
        /// them a purpose, treasure held up by ice or chains on harder days); moves sized from a thoughtful player
        /// (DailyConfig.moves to advancedMaxMoves). It must pass a fairness check (a thoughtful player completes it in 9
        /// games of 12, and again on 8 of 12 more), or it is made smaller; null if no version passes.
        /// </summary>
        LevelSpec BuildAdvancedDaily(string dayKey, MechanicSet unlocked, int met)
        {
            ulong seed = StableHash.Of("daily:" + dayKey);
            var rng = new Rng(StableHash.Of("daily-advanced:" + dayKey));
            int intensity = AdvancedIntensity(met, rng);
            var family = PickFamily(DailyFamilies, met, rng, BoardFamily.Rectangle);
            var shape = ChallengeShape(family, new Rng(StableHash.Of("daily-shape:" + dayKey)));
            family = shape.Family;

            // Two objectives: the day's planned obstacle first (if unlocked), then a partner that goes with it.
            var recipe = new LevelRecipe { Intensity = intensity };
            Mechanic? first = null;
            foreach (var m in Mechanics.ObstaclesIn(ChallengeMechanics.DailyPlan(dayKey) & unlocked))
            {
                first = ToMechanic(m);
                break;
            }
            if (!first.HasValue)
            {
                var pool = new List<Mechanic>();
                foreach (var m in new[] { Mechanic.Ice, Mechanic.Chains, Mechanic.Treasure, Mechanic.Moss, Mechanic.Colour })
                    if (Unlocked(m, unlocked)) pool.Add(m);
                first = pool[rng.Next(pool.Count)];
            }
            recipe.Mechanics.Add(first.Value);
            for (int guard = 0; guard < 8 && recipe.Mechanics.Count < 2; guard++)
            {
                var partner = CampaignProgression.Partner(recipe, rng, intensity, 301, family);
                if (!partner.HasValue) break;
                if (partner.Value == Mechanic.Score || !Unlocked(partner.Value, unlocked)) continue;
                recipe.Mechanics.Add(partner.Value);
            }
            if (recipe.Has(Mechanic.Specials))
            {
                CampaignProgression.ChooseSpecialGoal(recipe, 301, rng);
                // Combinations need two specials side by side: not on boards split into sections.
                if (recipe.SpecialGoal == GoalKind.FireCombo && InSections(family))
                {
                    recipe.SpecialGoal = GoalKind.CreateSpecial;
                    recipe.SpecialKind = SpecialKind.None;
                }
            }
            var plan = PlanFor(family, intensity);
            CampaignProgression.Size(recipe, plan, rng);
            CampaignProgression.ScaleToBoard(recipe, shape.Layout.OpenCellCount, CampaignProgression.BoardPermille(family));
            // Colours as in the campaign: four when chains of several links, big specials or lots of treasure need large
            // groups (and on boards of small sections), five otherwise.
            plan.Recipe = recipe;
            int colours = ColoursFor(shape.Layout, Math.Min(5, CampaignProgression.ColoursFor(plan, rng)));

            var spec = new LevelSpec
            {
                Id = "daily:" + dayKey,
                Mode = GameMode.Daily,
                Title = "Daily Challenge",
                Layout = shape.Layout,
                Colours = colours,
                AssistPermille = 40,
                Moves = Math.Max(5, daily.moves),
                Seed = seed,
                FixedSeed = true,
                Specials = Mechanics.Tier(unlocked),
            };
            int maxMoves = Math.Max(spec.Moves, daily.advancedMaxMoves);
            bool treasure = recipe.Has(Mechanic.Treasure);
            // Each size is placed twice (two placements) before a smaller one is tried.
            var scales = new[] { 100, 100, 80, 80, 65, 65, 50, 50, 40 };
            for (int attempt = 0; attempt < scales.Length; attempt++)
            {
                var r = scales[attempt] == 100 ? recipe : CampaignDesign.Scaled(recipe, scales[attempt]);
                var layout = shape.Layout.Clone();
                var goals = CampaignProgression.Decorate(r, layout, colours, null, new Rng(StableHash.Combine(seed, 0xDA11 + (ulong)attempt)));
                if (goals.Length == 0 || !layout.Validate(out _)) continue;
                spec.Layout = layout;
                SetMossCap(spec);
                bool specials = false;
                foreach (var g in goals) specials |= g.IsSpecialGoal;

                // Moves: what a thoughtful player needs in three games of four, plus room (more on gentler days).
                int needed = LevelCalibrator.MovesNeededWhenPassed(Varied(spec, 60, goals), scoring, 8, 60, BotKind.Planner, 75, out _);
                int slack = 480 - 3 * intensity + (treasure ? 100 : 0) + (specials ? 50 : 0);
                int moves = Math.Max(needed + 4, Math.Min(needed + 12, (needed * (1000 + slack) + 999) / 1000));
                // Too long for a daily: the next (smaller) version, skipping a second placement of this size.
                if (moves > maxMoves && attempt < scales.Length - 1)
                {
                    if (attempt + 1 < scales.Length && scales[attempt + 1] == scales[attempt]) attempt++;
                    continue;
                }
                spec.Moves = Math.Max(daily.moves, Math.Min(maxMoves, moves));

                // The target: from what a casual-to-good player (Mixed bot) scores with every move played, a little lower
                // than a plain day's because the objectives come first.
                spec.Goals = null;
                int reference = LevelCalibrator.MedianFullScore(spec, scoring, daily.calibrationRuns, BotKind.Mixed);
                int targetPermille = Math.Max(100, daily.targetPermille - daily.goalTargetEasingPermille);
                spec.Target = NumberFormat.RoundNice((int)((long)reference * targetPermille / 1000));
                spec.Star2 = Math.Max(spec.Target + 50, NumberFormat.RoundNice((int)((long)reference * daily.star2Permille / 1000)));
                spec.Star3 = Math.Max(spec.Star2 + 50, NumberFormat.RoundNice((int)((long)reference * daily.star3Permille / 1000)));
                var all = new List<GoalSpec> { GoalSpec.Score(spec.Target) };
                all.AddRange(goals);
                var withTarget = all.ToArray();

                for (int extra = 0; extra < 3; extra++)
                {
                    if (DailyIsFair(spec, withTarget, treasure))
                    {
                        spec.Goals = withTarget;
                        string board = BoardShapes.NameOf(family);
                        spec.BoardName = board;
                        spec.Note = (board.Length > 0 ? board + " · " : "") + spec.Moves + " moves. Reach the target and complete every goal!";
                        return spec;
                    }
                    if (spec.Moves >= maxMoves) break;
                    spec.Moves = Math.Min(maxMoves, spec.Moves + 3);
                }
            }
            return null;
        }

        /// <summary>A thoughtful player (Planner bot) completes the target and every objective in 9 games of 12 (8 with
        /// treasure), and again in 8 of 12 more (5 of 6 after a clean sweep).</summary>
        bool DailyIsFair(LevelSpec spec, GoalSpec[] goals, bool treasure)
        {
            var probe = Varied(spec, spec.Moves, goals);
            int slack = treasure ? 1 : 0;
            int wins = LevelCalibrator.PassCount(probe, scoring, 12, BotKind.Planner, 600);
            if (wins < 9 - slack) return false;
            if (wins == 12) return LevelCalibrator.PassCount(probe, scoring, 6, BotKind.Planner, 800) >= 5 - slack;
            return LevelCalibrator.PassCount(probe, scoring, 12, BotKind.Planner, 800) >= 8 - slack;
        }

        // ------------------------------------------------------------------ Weekly Rush

        /// <summary>
        /// The advanced Weekly Rush: a split or irregular board the player has met, the week's theme with up to two more
        /// mechanics, and up to three objectives that each add RushConfig.objectiveBonus points once completed (the run
        /// still lasts its full time; medals stay score-based). Objectives are sized so a thoughtful player completes all
        /// of them within a typical run (45 moves) in 6 games of 8; the medal scores move with the board, bonuses
        /// included, so the same effort earns the same medal. Null when nothing could be placed.
        /// </summary>
        LevelSpec BuildAdvancedWeekly(string weekKey, MechanicSet unlocked, int met)
        {
            var plainSpec = WeeklyPlain(weekKey);
            int plain = LevelCalibrator.MedianRushScore(plainSpec, scoring, 8, RushProbeMoves);
            var rng = new Rng(StableHash.Of("weekly-advanced:" + weekKey));
            int intensity = AdvancedIntensity(met, rng);
            var family = PickFamily(WeeklyFamilies, met, rng, BoardFamily.Obstructed);
            var shape = ChallengeShape(family, new Rng(StableHash.Of("weekly-shape:" + weekKey)));
            family = shape.Family;

            MechanicSet theme;
            var set = ChallengeMechanics.WeeklyFor(weekKey, unlocked, out theme);
            var recipe = new LevelRecipe { Intensity = intensity };
            foreach (var m in Mechanics.ObstaclesIn(set))
            {
                var mech = ToMechanic(m);
                if (mech.HasValue && recipe.Mechanics.Count < 3) recipe.Mechanics.Add(mech.Value);
            }
            SpecialKind themeSpecial = theme == MechanicSet.Rockets ? SpecialKind.RocketRow : theme == MechanicSet.Bombs ? SpecialKind.Bomb :
                theme == MechanicSet.Prisms ? SpecialKind.Prism : SpecialKind.None;
            if (themeSpecial != SpecialKind.None)
            {
                // A specials week: set some of that special off (the board starts with a few of them).
                if (recipe.Mechanics.Count >= 3) recipe.Mechanics.RemoveAt(2);
                recipe.Mechanics.Add(Mechanic.Specials);
                recipe.SpecialGoal = GoalKind.FireSpecial;
                recipe.SpecialKind = themeSpecial;
                recipe.SpecialAmount = themeSpecial == SpecialKind.RocketRow ? 4 : 3;
            }
            if (recipe.Mechanics.Count == 0) return null;
            CampaignProgression.Size(recipe, PlanFor(family, intensity), rng);
            CampaignProgression.ScaleToBoard(recipe, shape.Layout.OpenCellCount, CampaignProgression.BoardPermille(family));

            var spec = new LevelSpec
            {
                Id = plainSpec.Id,
                Mode = GameMode.WeeklyRush,
                Title = plainSpec.Title,
                Layout = shape.Layout,
                Colours = ColoursFor(shape.Layout, plainSpec.Colours),
                AssistPermille = plainSpec.AssistPermille,
                Moves = 0,
                TimeLimitSeconds = plainSpec.TimeLimitSeconds,
                Seed = plainSpec.Seed,
                FixedSeed = true,
                Specials = Mechanics.Tier(set),
                ObjectiveBonus = Math.Max(0, rush.objectiveBonus),
            };
            // The biggest version a thoughtful player still completes within a typical run.
            var scales = new[] { 125, 100, 75, 55 };
            bool placed = false;
            for (int attempt = 0; attempt < scales.Length; attempt++)
            {
                var r = scales[attempt] == 100 ? recipe : CampaignDesign.Scaled(recipe, scales[attempt]);
                if (themeSpecial != SpecialKind.None) r.SpecialAmount = recipe.SpecialAmount;
                var layout = shape.Layout.Clone();
                var drng = new Rng(StableHash.Combine(StableHash.Of("weekly-board:" + weekKey), (ulong)attempt));
                var goals = CampaignProgression.Decorate(r, layout, spec.Colours, null, drng);
                ChallengeMechanics.PlaceThemeSpecials(layout, theme, drng);
                if (goals.Length == 0 || !layout.Validate(out _)) continue;
                spec.Layout = layout;
                spec.Goals = goals;
                SetMossCap(spec);
                placed = true;
                // A thoughtful player completes every objective within a typical run.
                if (LevelCalibrator.PassCount(Varied(spec, RushProbeMoves, goals), scoring, 8, BotKind.Planner, 900) >= 6) break;
            }
            if (!placed) return null;

            int with = LevelCalibrator.MedianRushScore(spec, scoring, 8, RushProbeMoves);
            // Medal scores move with the board, objective bonuses included, so they are earned at the plain board's
            // rates. Shaped boards with specials and bonuses can score far more than a plain one, so the range is wider
            // than the original's (0.7× to 4×).
            long ratio = plain > 0 ? Math.Max(700, Math.Min(4000, (long)with * 1000 / plain)) : 1000;
            spec.Target = ScaleTier(plainSpec.Target, ratio);
            spec.Star2 = Math.Max(spec.Target + 50, ScaleTier(plainSpec.Star2, ratio));
            spec.Star3 = Math.Max(spec.Star2 + 50, ScaleTier(plainSpec.Star3, ratio));
            string name = ChallengeMechanics.ThemeName(theme), board = BoardShapes.NameOf(family);
            spec.BoardName = board;
            string head = name.Length > 0 && board.Length > 0 ? name + " · " + board : name + board;
            spec.Note = (head.Length > 0 ? head + ": " : "") + rush.seconds + " seconds. Each objective adds " +
                        NumberFormat.Thousands(spec.ObjectiveBonus) + " points!";
            return spec;
        }
    }
}
