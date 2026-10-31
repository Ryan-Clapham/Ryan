using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// The level at which each mechanic joins the campaign (docs/DESIGN.md, campaign mechanics). Each gets a
    /// hand-made introduction level there (CampaignDesign.IntroLevels) with its own tutorial, then a few practice
    /// levels that focus on it, then it mixes with the ones before it. Order: ice, rockets, bombs, prisms, chains,
    /// treasure, moss; never two new mechanics in one level.
    /// </summary>
    [Serializable]
    public class MechanicSchedule
    {
        public int iceFromLevel = 21;
        public int colourGoalsFromLevel = 25;
        public int thickIceFromLevel = 28;
        public int rocketsFromLevel = 31;
        public int bombsFromLevel = 41;
        public int combosFromLevel = 47;
        public int prismsFromLevel = 51;
        public int chainsFromLevel = 61;
        public int treasureFromLevel = 71;
        public int mossFromLevel = 101;
        /// <summary>Levels after each introduction that focus on the new mechanic.</summary>
        public int practiceLevels = 3;
    }

    /// <summary>Things a generated level can ask the player to deal with.</summary>
    public enum Mechanic
    {
        Ice,
        Chains,
        Treasure,
        Moss,
        Colour,
    }

    /// <summary>What a generated level contains, decided from its number before the board is decorated.</summary>
    public sealed class LevelRecipe
    {
        public readonly List<Mechanic> Mechanics = new List<Mechanic>();
        public int IceCount;
        /// <summary>Share of the ice that is two layers thick, in permille.</summary>
        public int ThickPermille;
        public int ChainCount;
        public int MaxLinks = 1;
        public int TreasureCount;
        public int MossSeeds;
        public int CollectAmount;
        public string TutorialKey = "";
        public string Note = "";
        /// <summary>A practice level straight after an introduction: kept gentle.</summary>
        public bool Practice;

        public bool Has(Mechanic m) => Mechanics.Contains(m);
    }

    /// <summary>The difficulty band a level belongs to: move range and how much spare room levels get.</summary>
    public struct DifficultyBand
    {
        public string Name;
        public int MinMoves;
        public int MaxMoves;
        /// <summary>Extra moves on top of what a casual player needs, in permille.</summary>
        public int SlackPermille;
    }

    /// <summary>
    /// The campaign's shape beyond the opening levels: when mechanics arrive (MechanicSchedule), the hand-made
    /// introduction levels, and recipes that decorate generated boards with obstacles, treasure and goals.
    /// Deterministic: everything comes from the level number.
    /// </summary>
    public static class CampaignDesign
    {
        // ------------------------------------------------------------------ Bands and rhythm

        public static DifficultyBand BandFor(int n)
        {
            if (n <= 20) return new DifficultyBand { Name = "Learn", MinMoves = 12, MaxMoves = 22, SlackPermille = 400 };
            if (n <= 60) return new DifficultyBand { Name = "Develop", MinMoves = 18, MaxMoves = 30, SlackPermille = 300 };
            if (n <= 120) return new DifficultyBand { Name = "Combine", MinMoves = 22, MaxMoves = 35, SlackPermille = 250 };
            if (n <= 200) return new DifficultyBand { Name = "Advanced", MinMoves = 26, MaxMoves = 40, SlackPermille = 200 };
            return new DifficultyBand { Name = "Expert", MinMoves = 30, MaxMoves = 45, SlackPermille = 130 };
        }

        /// <summary>
        /// Ten-level rhythm, in permille of extra moves: levels ending 1-3 are generous, 4-5 medium, 6 a breather,
        /// 7-9 harder, 0 a milestone (medium).
        /// </summary>
        public static int RhythmPermille(int n)
        {
            switch (n % 10)
            {
                case 1:
                case 2:
                case 3: return 100;
                case 6: return 150;
                case 7:
                case 8:
                case 9: return -50;
                default: return 0;
            }
        }

        /// <summary>
        /// For score levels from the first mechanic on: the percentile of a casual player's scores (every move played)
        /// used as the target, so roughly (100 - this)% of casual games reach it. Set by band, moved by the rhythm.
        /// </summary>
        public static int ScoreTargetPercentile(int n)
        {
            int pct = n <= 60 ? 30 : n <= 120 ? 35 : n <= 200 ? 45 : 55;
            switch (n % 10)
            {
                case 1:
                case 2:
                case 3: pct -= 10; break;
                case 6: pct -= 15; break;
                case 7:
                case 8:
                case 9: pct += 10; break;
                case 0: pct += 5; break;
            }
            return Math.Max(5, Math.Min(80, pct));
        }

        public static SpecialTier SpecialsFor(int n, MechanicSchedule s)
        {
            if (n >= s.prismsFromLevel) return SpecialTier.Prisms;
            if (n >= s.bombsFromLevel) return SpecialTier.Bombs;
            if (n >= s.rocketsFromLevel) return SpecialTier.Rockets;
            return SpecialTier.None;
        }

        // ------------------------------------------------------------------ Hand-made introduction levels

        static readonly string[] Open8x9 = { "........", "........", "........", "........", "........", "........", "........", "........", "........" };

        /// <summary>One introduction level per mechanic, at the level the schedule gives it. Config overrides win.</summary>
        public static LevelOverride[] IntroLevels(MechanicSchedule s)
        {
            return new[]
            {
                new LevelOverride
                {
                    level = s.iceFromLevel, colours = 4, moves = 18, tutorial = TutorialKeys.Ice, note = "New: frozen tiles",
                    layout = Open8x9,
                    obstacles = new[] { "........", "........", "...ii...", "........", "........", ".ii..ii.", "........", "........", "........" },
                    goals = new[] { GoalSpec.FreeIce(6) },
                },
                new LevelOverride
                {
                    level = s.colourGoalsFromLevel, colours = 4, moves = 22, tutorial = TutorialKeys.ColourGoal, note = "New: collect a colour",
                    layout = Open8x9,
                    goals = new[] { GoalSpec.Collect(TileColours.Blue, 18) },
                },
                new LevelOverride
                {
                    level = s.thickIceFromLevel, colours = 4, moves = 24, tutorial = TutorialKeys.ThickIce, note = "New: thick ice",
                    layout = Open8x9,
                    obstacles = new[] { "........", "........", "........", "..I..I..", "........", "i.i..i.i", "........", "........", "........" },
                    goals = new[] { GoalSpec.FreeIce(6) },
                },
                new LevelOverride
                {
                    level = s.rocketsFromLevel, colours = 4, moves = 20, tutorial = TutorialKeys.Rockets, note = "New: rockets",
                    layout = new[] { "........", "........", "........", "........", "...-....", "........", "........", "BBBB....", "BBBB...." },
                    obstacles = new[] { "........", "........", "..iiii..", "........", "iii.iiii", "........", "........", "........", "........" },
                    goals = new[] { GoalSpec.FreeIce(11) },
                },
                new LevelOverride
                {
                    level = s.bombsFromLevel, colours = 4, moves = 18, tutorial = TutorialKeys.Bombs, note = "New: bombs",
                    layout = new[] { "........", "........", "........", "........", "........", "...*....", "........", "RRRRR...", "RRRRR..." },
                    obstacles = new[] { "........", "........", "........", "........", "..IiI...", "..i.i...", "..IiI...", "........", "........" },
                    goals = new[] { GoalSpec.FreeIce(8) },
                },
                new LevelOverride
                {
                    level = s.combosFromLevel, colours = 5, moves = 18, tutorial = TutorialKeys.Combos, note = "New: special combos",
                    layout = new[] { "........", "........", "........", "........", "...-|...", "........", "........", "........", "........" },
                    obstacles = new[] { "........", "...i....", "...i....", "........", "iii..iii", "........", "...i....", "...i....", "........" },
                    goals = new[] { GoalSpec.FreeIce(10) },
                },
                new LevelOverride
                {
                    level = s.prismsFromLevel, colours = 5, moves = 22, tutorial = TutorialKeys.Prisms, note = "New: prisms",
                    layout = new[] { "........", "........", "........", "........", "...@....", "........", "........", "GGGGGGG.", "GGGGGGG." },
                    goals = new[] { GoalSpec.Collect(TileColours.Yellow, 20) },
                },
                new LevelOverride
                {
                    // Every chained tile starts next to a tile of its own colour, so the first clears show how chains break.
                    level = s.chainsFromLevel, colours = 4, moves = 20, tutorial = TutorialKeys.Chains, note = "New: chained tiles",
                    layout = new[] { "........", "...BB...", "........", ".YY..GG.", "........", ".RR..BB.", "........", "...RR...", "........" },
                    obstacles = new[] { "........", "....1...", "........", "..1..1..", "........", ".1....1.", "........", "...1....", "........" },
                    goals = new[] { GoalSpec.BreakChains(6) },
                },
                new LevelOverride
                {
                    level = s.treasureFromLevel, colours = 4, moves = 22, tutorial = TutorialKeys.Treasure, note = "New: treasure",
                    layout = new[] { "........", "........", "........", "........", "........", "........", "..$..$..", "........", "........" },
                    goals = new[] { GoalSpec.CollectTreasure(2) },
                },
                new LevelOverride
                {
                    // Each mossy tile starts next to a tile of its own colour, so the first clears show how moss goes.
                    level = s.mossFromLevel, colours = 4, moves = 22, tutorial = TutorialKeys.Moss, note = "New: moss",
                    layout = new[] { "........", "........", "........", "........", "........", "........", ".RR..BB.", "........", "........" },
                    obstacles = new[] { "........", "........", "........", "........", "........", "........", ".m....m.", "........", "........" },
                    goals = new[] { GoalSpec.ClearMoss() },
                },
            };
        }

        /// <summary>The tutorial shown the first time a level has thick ice, chains of several links, etc., and its intro level.</summary>
        public static bool IsIntroLevel(int n, MechanicSchedule s)
        {
            return n == s.iceFromLevel || n == s.colourGoalsFromLevel || n == s.thickIceFromLevel || n == s.rocketsFromLevel ||
                   n == s.bombsFromLevel || n == s.combosFromLevel || n == s.prismsFromLevel || n == s.chainsFromLevel ||
                   n == s.treasureFromLevel || n == s.mossFromLevel;
        }

        // ------------------------------------------------------------------ Recipes for generated levels

        /// <summary>
        /// Decides what a generated level contains. Practice levels right after an introduction focus on that
        /// mechanic; others pick from everything taught so far, sometimes two at once (more often later on).
        /// Returns null for a plain score level.
        /// </summary>
        public static LevelRecipe Recipe(int n, MechanicSchedule s, Rng rng)
        {
            if (n < s.iceFromLevel) return null;
            var r = new LevelRecipe();
            Mechanic? practice = PracticeFocus(n, s);
            if (practice.HasValue)
            {
                r.Practice = true;
                r.Mechanics.Add(practice.Value);
            }
            else
            {
                var pool = new List<Mechanic>();
                var weights = new List<int>();
                Add(pool, weights, Mechanic.Ice, n >= s.iceFromLevel, Recent(n, s.iceFromLevel) ? 40 : 26);
                Add(pool, weights, Mechanic.Colour, n >= s.colourGoalsFromLevel, 18);
                Add(pool, weights, Mechanic.Chains, n >= s.chainsFromLevel, Recent(n, s.chainsFromLevel) ? 40 : 22);
                Add(pool, weights, Mechanic.Treasure, n >= s.treasureFromLevel, Recent(n, s.treasureFromLevel) ? 36 : 18);
                Add(pool, weights, Mechanic.Moss, n >= s.mossFromLevel, Recent(n, s.mossFromLevel) ? 30 : 10);

                // A breather now and then: a plain score level (levels ending in 6, and a share of the rest).
                if (n % 10 == 6 || rng.ChancePermille(n < 61 ? 220 : 120)) return null;

                var first = Pick(pool, weights, rng);
                r.Mechanics.Add(first);
                int comboChance = n < s.bombsFromLevel ? 0 : n < 121 ? 300 : n < 201 ? 450 : 550;
                if (n % 10 == 0) comboChance += 300;
                if (pool.Count > 1 && rng.ChancePermille(comboChance))
                {
                    int i = pool.IndexOf(first);
                    pool.RemoveAt(i);
                    weights.RemoveAt(i);
                    r.Mechanics.Add(Pick(pool, weights, rng));
                }
            }
            Size(r, n, s, rng);
            return r;
        }

        static bool Recent(int n, int introLevel) => n >= introLevel && n < introLevel + 15;

        static void Add(List<Mechanic> pool, List<int> weights, Mechanic m, bool unlocked, int weight)
        {
            if (!unlocked) return;
            pool.Add(m);
            weights.Add(weight);
        }

        static Mechanic Pick(List<Mechanic> pool, List<int> weights, Rng rng)
        {
            int total = 0;
            foreach (var w in weights) total += w;
            int x = rng.Next(Math.Max(1, total));
            for (int i = 0; i < pool.Count; i++)
            {
                x -= weights[i];
                if (x < 0) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>The mechanic a practice level focuses on, or null when n is not a practice level.</summary>
        static Mechanic? PracticeFocus(int n, MechanicSchedule s)
        {
            int p = Math.Max(0, s.practiceLevels);
            if (After(n, s.iceFromLevel, p) || After(n, s.thickIceFromLevel, p)) return Mechanic.Ice;
            if (After(n, s.colourGoalsFromLevel, p)) return Mechanic.Colour;
            // Specials have no goal of their own: their practice levels are colour goals, which reward big groups.
            if (After(n, s.rocketsFromLevel, p) || After(n, s.bombsFromLevel, p) || After(n, s.prismsFromLevel, p) || After(n, s.combosFromLevel, p))
                return Mechanic.Colour;
            if (After(n, s.chainsFromLevel, p)) return Mechanic.Chains;
            if (After(n, s.treasureFromLevel, p)) return Mechanic.Treasure;
            if (After(n, s.mossFromLevel, p)) return Mechanic.Moss;
            return null;
        }

        static bool After(int n, int intro, int practice) => n > intro && n <= intro + practice;

        /// <summary>How much of each mechanic, by band; less of each when a level combines two.</summary>
        static void Size(LevelRecipe r, int n, MechanicSchedule s, Rng rng)
        {
            int band = n <= 60 ? 0 : n <= 120 ? 1 : n <= 200 ? 2 : 3;
            bool two = r.Mechanics.Count > 1;
            int pct = r.Practice ? 75 : two ? 65 : 100;
            int Scale(int v) => Math.Max(1, v * pct / 100);
            int collectPct = two ? 65 : 100;

            if (r.Has(Mechanic.Ice))
            {
                int[] lo = { 5, 7, 9, 11 }, hi = { 8, 11, 13, 15 };
                r.IceCount = Scale(lo[band] + rng.Next(hi[band] - lo[band] + 1));
                r.ThickPermille = n < s.thickIceFromLevel ? 0 : band == 0 ? 150 : band == 1 ? 300 : 450;
            }
            if (r.Has(Mechanic.Chains))
            {
                int[] lo = { 4, 4, 5, 6 }, hi = { 6, 6, 8, 10 };
                r.ChainCount = Scale(lo[band] + rng.Next(hi[band] - lo[band] + 1));
                r.MaxLinks = n < s.chainsFromLevel + 10 ? 1 : n < 161 ? 2 : 3;
            }
            if (r.Has(Mechanic.Treasure))
            {
                r.TreasureCount = band <= 1 ? 1 + rng.Next(2) : 2 + rng.Next(2);
                if (two) r.TreasureCount = Math.Max(1, r.TreasureCount - 1);
            }
            if (r.Has(Mechanic.Moss)) r.MossSeeds = band <= 1 ? 3 : 3 + rng.Next(3);
            if (r.Has(Mechanic.Colour))
            {
                int[] lo = { 16, 20, 24, 28 }, hi = { 24, 28, 32, 36 };
                r.CollectAmount = Math.Max(1, (lo[band] + rng.Next(hi[band] - lo[band] + 1)) * collectPct / 100);
            }
            if (n == s.thickIceFromLevel + 1 && r.Has(Mechanic.Ice)) r.ThickPermille = Math.Max(r.ThickPermille, 300);
        }

        // ------------------------------------------------------------------ Decorating a generated board

        /// <summary>
        /// Places the recipe's obstacles and treasure on the layout and returns the level's goals. Cells are chosen
        /// with mirror symmetry where the board allows, away from the top row (new tiles arrive there).
        /// </summary>
        public static GoalSpec[] Decorate(LevelRecipe r, LevelLayout layout, int colours, int[] weights, Rng rng)
        {
            var goals = new List<GoalSpec>();
            if (r.Has(Mechanic.Treasure))
            {
                int placed = PlaceTreasure(layout, r.TreasureCount, rng);
                if (placed > 0) goals.Add(GoalSpec.CollectTreasure(placed));
            }
            if (r.Has(Mechanic.Moss))
            {
                int placed = PlaceMoss(layout, r.MossSeeds, rng);
                if (placed > 0) goals.Add(GoalSpec.ClearMoss(placed));
            }
            if (r.Has(Mechanic.Ice))
            {
                int placed = PlaceIce(layout, r.IceCount, r.ThickPermille, rng);
                if (placed > 0) goals.Add(GoalSpec.FreeIce(placed));
            }
            if (r.Has(Mechanic.Chains))
            {
                int placed = PlaceChains(layout, r.ChainCount, r.MaxLinks, rng);
                if (placed > 0) goals.Add(GoalSpec.BreakChains(placed));
            }
            if (r.Has(Mechanic.Colour) && r.CollectAmount > 0)
            {
                // Never ask for a colour the level makes rare.
                var common = new List<int>();
                for (int c = 0; c < colours; c++)
                    if (weights == null || c >= weights.Length || weights[c] >= 10) common.Add(c);
                if (common.Count == 0) common.Add(0);
                goals.Add(GoalSpec.Collect(common[rng.Next(common.Count)], r.CollectAmount));
            }
            return goals.ToArray();
        }

        /// <summary>A copy with every amount scaled (used when a level turns out to need more moves than its band allows).</summary>
        public static LevelRecipe Scaled(LevelRecipe r, int percent)
        {
            var c = new LevelRecipe
            {
                IceCount = Math.Max(r.IceCount > 0 ? 2 : 0, r.IceCount * percent / 100),
                ThickPermille = r.ThickPermille * percent / 100,
                ChainCount = Math.Max(r.ChainCount > 0 ? 2 : 0, r.ChainCount * percent / 100),
                MaxLinks = percent < 80 ? Math.Max(1, r.MaxLinks - 1) : r.MaxLinks,
                TreasureCount = Math.Max(r.TreasureCount > 0 ? 1 : 0, r.TreasureCount * percent / 100),
                MossSeeds = Math.Max(r.MossSeeds > 0 ? 2 : 0, r.MossSeeds * percent / 100),
                CollectAmount = Math.Max(r.CollectAmount > 0 ? 8 : 0, r.CollectAmount * percent / 100),
                TutorialKey = r.TutorialKey,
                Note = r.Note,
                Practice = r.Practice,
            };
            c.Mechanics.AddRange(r.Mechanics);
            return c;
        }

        /// <summary>Open cells that can take an obstacle: open, no piece, no obstacle yet, not in the top row.</summary>
        static bool Free(LevelLayout l, int c, int r)
        {
            return l.IsOpen(c, r) && r < l.Height - 1 && l.PieceAt(c, r) == '\0' && l.ObstacleAt(c, r) == ObstacleKind.None;
        }

        static int PlaceIce(LevelLayout l, int count, int thickPermille, Rng rng)
        {
            int placed = 0;
            int w = l.Width, h = l.Height;
            int style = rng.Next(3);
            var cells = new List<GridPos>();
            if (style == 0)
            {
                // A band across the board, a few rows up.
                int row = 2 + rng.Next(Math.Max(1, h / 2 - 1));
                for (int k = 0; k < 2 && cells.Count < count; k++)
                    for (int c = 0; c < w; c++)
                        if (Free(l, c, row + k * 3)) cells.Add(new GridPos(c, row + k * 3));
            }
            else if (style == 1)
            {
                // Two mirrored 2 × 2 blocks, then more lower down.
                int[] rows = { h / 2, 2, h - 3 };
                foreach (int row in rows)
                    for (int dc = 0; dc < 2; dc++)
                        for (int dr = 0; dr < 2; dr++)
                        {
                            AddMirrored(l, cells, 1 + dc, row + dr);
                        }
            }
            // Scattered mirrored pairs fill the rest (and are style 2).
            for (int guard = 0; cells.Count < count && guard < 200; guard++)
                AddMirrored(l, cells, rng.Next(w), 1 + rng.Next(Math.Max(1, h - 2)));
            foreach (var p in cells)
            {
                if (placed >= count) break;
                if (!Free(l, p.Col, p.Row)) continue;
                l.SetObstacle(p.Col, p.Row, ObstacleKind.Ice, rng.ChancePermille(thickPermille) ? 2 : 1);
                placed++;
            }
            return placed;
        }

        static void AddMirrored(LevelLayout l, List<GridPos> cells, int c, int r)
        {
            int m = l.Width - 1 - c;
            if (Free(l, c, r) && !cells.Contains(new GridPos(c, r))) cells.Add(new GridPos(c, r));
            if (Free(l, m, r) && !cells.Contains(new GridPos(m, r))) cells.Add(new GridPos(m, r));
        }

        static int PlaceChains(LevelLayout l, int count, int maxLinks, Rng rng)
        {
            var cells = new List<GridPos>();
            for (int guard = 0; cells.Count < count && guard < 200; guard++)
                AddMirrored(l, cells, rng.Next(l.Width), 1 + rng.Next(Math.Max(1, l.Height - 3)));
            int placed = 0;
            foreach (var p in cells)
            {
                if (placed >= count) break;
                if (!Free(l, p.Col, p.Row)) continue;
                l.SetObstacle(p.Col, p.Row, ObstacleKind.Chain, 1 + rng.Next(Math.Max(1, maxLinks)));
                placed++;
            }
            return placed;
        }

        /// <summary>A column where treasure at row r can reach the floor fairly: open all the way down (to the bottom edge
        /// or a stone), with an open neighbour beside every cell below it, so no cell under it is a dead-end pocket.</summary>
        static bool FairTreasureColumn(LevelLayout l, int c, int r)
        {
            // Not the edge columns: the last tile under the treasure would have only one neighbour that could match it.
            if (c <= 0 || c >= l.Width - 1) return false;
            // Open all the way to the bottom edge, with open columns either side: no stone cups or pockets.
            for (int y = r - 1; y >= 0; y--)
            {
                if (!l.IsOpen(c, y) || l.ObstacleAt(c, y) != ObstacleKind.None) return false;
                if (!l.IsOpen(c - 1, y) || !l.IsOpen(c + 1, y)) return false;
            }
            return true;
        }

        static int PlaceTreasure(LevelLayout l, int count, Rng rng)
        {
            // Treasure starts in different columns, a few rows up, so the player has to dig a path for each.
            var columns = new List<int>();
            for (int c = 0; c < l.Width; c++) columns.Add(c);
            for (int i = columns.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = columns[i];
                columns[i] = columns[j];
                columns[j] = t;
            }
            int placed = 0;
            foreach (int c in columns)
            {
                if (placed >= count) break;
                for (int r = l.Height - 5; r >= l.Height - 7 && r >= 1; r--)
                {
                    if (!l.IsOpen(c, r) || l.PieceAt(c, r) != '\0' || l.ObstacleAt(c, r) != ObstacleKind.None || l.RestsOnPermanentFloor(c, r)) continue;
                    if (!FairTreasureColumn(l, c, r)) continue;
                    l.SetPiece(c, r, '$');
                    placed++;
                    break;
                }
            }
            return placed;
        }

        static int PlaceMoss(LevelLayout l, int seeds, Rng rng)
        {
            // Moss starts in one or two patches low on the board, on either side.
            var cells = new List<GridPos>();
            bool both = rng.Next(2) == 0;
            int baseRow = 1 + rng.Next(2);
            int[,] patch = { { 1, 0 }, { 2, 0 }, { 1, 1 }, { 2, 1 }, { 1, 2 } };
            for (int k = 0; k < patch.GetLength(0) && cells.Count < seeds; k++)
            {
                int c = patch[k, 0], r = baseRow + patch[k, 1];
                if (Free(l, c, r) && Roomy(l, c, r)) cells.Add(new GridPos(c, r));
                if (both && cells.Count < seeds && Free(l, l.Width - 1 - c, r) && Roomy(l, l.Width - 1 - c, r)) cells.Add(new GridPos(l.Width - 1 - c, r));
            }
            foreach (var p in cells) l.SetObstacle(p.Col, p.Row, ObstacleKind.Moss, 1);
            return cells.Count;
        }

        /// <summary>All four neighbours are open cells (no stone, void or edge), so the tile there can always be matched.</summary>
        static bool Roomy(LevelLayout l, int c, int r)
        {
            for (int d = 0; d < 4; d++)
                if (!l.IsOpen(c + Board.DCol[d], r + Board.DRow[d])) return false;
            return true;
        }

        public static string NoteFor(LevelRecipe r)
        {
            if (r == null || r.Mechanics.Count == 0) return "";
            var parts = new List<string>();
            foreach (var m in r.Mechanics)
            {
                switch (m)
                {
                    case Mechanic.Ice: parts.Add("Ice"); break;
                    case Mechanic.Chains: parts.Add("Chains"); break;
                    case Mechanic.Treasure: parts.Add("Treasure"); break;
                    case Mechanic.Moss: parts.Add("Moss"); break;
                    case Mechanic.Colour: parts.Add("Collect"); break;
                }
            }
            return string.Join(" + ", parts.ToArray());
        }
    }
}
