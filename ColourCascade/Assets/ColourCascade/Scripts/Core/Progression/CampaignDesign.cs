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
        /// <summary>A goal about special tiles: make some, set some off, or fire a combination.</summary>
        Specials,
        /// <summary>A score to reach as well as the other goals (its amount is calibrated with the level's moves).</summary>
        Score,
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
        /// <summary>Mechanic.Specials: CreateSpecial, FireSpecial or FireCombo, which special (None = any) and how many.</summary>
        public GoalKind SpecialGoal = GoalKind.CreateSpecial;
        public SpecialKind SpecialKind;
        public int SpecialAmount;
        /// <summary>A special tile placed on the board at the start ('-', '|', '*' or '@'), or '\0' for none.</summary>
        public char StartSpecial;
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
            if (n <= 60) return new DifficultyBand { Name = "Develop", MinMoves = 16, MaxMoves = 30, SlackPermille = 200 };
            if (n <= 120) return new DifficultyBand { Name = "Combine", MinMoves = 20, MaxMoves = 35, SlackPermille = 180 };
            if (n <= 200) return new DifficultyBand { Name = "Advanced", MinMoves = 24, MaxMoves = 40, SlackPermille = 150 };
            return new DifficultyBand { Name = "Expert", MinMoves = 28, MaxMoves = 45, SlackPermille = 110 };
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
                    // The two rockets show what a combination does (their cross frees 10 of the ice at once). Four more
                    // frozen tiles sit out of their reach, so the level still takes ordinary play to finish: it was once
                    // a one-tap win (docs/DESIGN.md §3.2).
                    level = s.combosFromLevel, colours = 5, moves = 20, tutorial = TutorialKeys.Combos, note = "New: special combos",
                    layout = new[] { "........", "........", "........", "........", "...-|...", "........", "........", "........", "........" },
                    obstacles = new[] { "........", "...i....", "...i....", "........", "iii..iii", "........", ".i.i..i.", ".i.i..i.", "........" },
                    goals = new[] { GoalSpec.FireCombo(1), GoalSpec.FreeIce(14) },
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
                    // Four patches in two rows, so clearing it takes a few moves (two went in a move or two).
                    level = s.mossFromLevel, colours = 4, moves = 22, tutorial = TutorialKeys.Moss, note = "New: moss",
                    layout = new[] { "........", "........", "........", "..GG.YY.", "........", "........", ".RR..BB.", "........", "........" },
                    obstacles = new[] { "........", "........", "........", "...m.m..", "........", "........", ".m....m.", "........", "........" },
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
            if (IsSpecialsPractice(n, s))
            {
                // Practice for a new special tile: make it or set it off, alongside a goal the player already knows.
                r.Practice = true;
                PracticeSpecials(r, n, s);
                Size(r, n, s, rng);
                return r;
            }
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
                if (n % 10 == 6 || rng.ChancePermille(n < 61 ? 200 : 120)) return null;

                var first = Pick(pool, weights, rng);
                r.Mechanics.Add(first);
                // Mixed goals ("free 3 frozen tiles and collect 10 blue"): from the colour-goal introduction on, about half
                // the levels combine two kinds of goal, more later on; milestones nearly always do. Partners can also be
                // goals about special tiles (make two rockets, set off a bomb, fire a combination) and, a little later,
                // a score to reach as well. Three goals from the prisms.
                Add(pool, weights, Mechanic.Specials, n >= s.rocketsFromLevel, Recent(n, s.rocketsFromLevel) ? 30 : 20);
                Add(pool, weights, Mechanic.Score, n >= s.rocketsFromLevel + 4, 12);
                int comboChance = n <= s.colourGoalsFromLevel + s.practiceLevels ? 0 : n < s.rocketsFromLevel ? 450 : n < s.bombsFromLevel ? 550 : n < 121 ? 650 : n < 201 ? 700 : 750;
                if (n % 10 == 0) comboChance += 300;
                if (pool.Count > 1 && rng.ChancePermille(comboChance))
                {
                    int i = pool.IndexOf(first);
                    pool.RemoveAt(i);
                    weights.RemoveAt(i);
                    var second = Pick(pool, weights, rng);
                    r.Mechanics.Add(second);
                    int threeChance = n < s.prismsFromLevel ? 0 : n < s.chainsFromLevel ? 100 : n < 121 ? 150 : 250;
                    if (pool.Count > 1 && rng.ChancePermille(threeChance))
                    {
                        int j = pool.IndexOf(second);
                        pool.RemoveAt(j);
                        weights.RemoveAt(j);
                        r.Mechanics.Add(Pick(pool, weights, rng));
                    }
                }
            }
            if (r.Has(Mechanic.Specials)) ChooseSpecialGoal(r, n, s, rng);
            Size(r, n, s, rng);
            return r;
        }

        /// <summary>The three practice levels after rockets, bombs, combinations and prisms: make the new special, set it
        /// off, then both, each with a goal the player already knows.</summary>
        static void PracticeSpecials(LevelRecipe r, int n, MechanicSchedule s)
        {
            int k;
            SpecialKind kind;
            if (After(n, s.prismsFromLevel, s.practiceLevels)) { k = n - s.prismsFromLevel; kind = SpecialKind.Prism; }
            else if (After(n, s.combosFromLevel, s.practiceLevels)) { k = n - s.combosFromLevel; kind = SpecialKind.None; }
            else if (After(n, s.bombsFromLevel, s.practiceLevels)) { k = n - s.bombsFromLevel; kind = SpecialKind.Bomb; }
            else { k = n - s.rocketsFromLevel; kind = SpecialKind.RocketRow; }
            r.Mechanics.Add(Mechanic.Specials);
            r.Mechanics.Add(k == 2 ? Mechanic.Colour : Mechanic.Ice);
            r.SpecialKind = kind;
            if (kind == SpecialKind.None)
            {
                // Combination practice starts with a rocket on the board: make a special next to it, then tap.
                r.SpecialGoal = GoalKind.FireCombo;
                r.SpecialAmount = k == 3 ? 2 : 1;
                r.StartSpecial = k == 2 ? '|' : '-';
                return;
            }
            if (kind == SpecialKind.Prism)
            {
                // A prism takes a group of 12: too rare to ask for this early. Its practice starts with one on the
                // board to aim at the ice, then asks for specials of any kind.
                if (k == 1)
                {
                    r.StartSpecial = '@';
                    r.SpecialGoal = GoalKind.FireSpecial;
                    r.SpecialAmount = 1;
                    return;
                }
                r.SpecialKind = SpecialKind.None;
                r.SpecialGoal = k == 2 ? GoalKind.FireSpecial : GoalKind.CreateSpecial;
                r.SpecialAmount = 3;
                return;
            }
            r.SpecialGoal = k == 2 ? GoalKind.FireSpecial : GoalKind.CreateSpecial;
            r.SpecialAmount = kind == SpecialKind.RocketRow ? 2 : 1;
            // "Set off 2 bombs" starts with one on the board: the player makes the second.
            if (kind == SpecialKind.Bomb && k == 2)
            {
                r.StartSpecial = '*';
                r.SpecialAmount = 2;
            }
        }

        /// <summary>A special-tile goal for a mixed level, from the specials the player knows (the newest most often).</summary>
        static void ChooseSpecialGoal(LevelRecipe r, int n, MechanicSchedule s, Rng rng)
        {
            var options = new List<int>();
            var weights = new List<int>();
            // Encoded as goal kind * 10 + special kind.
            void Opt(GoalKind g, SpecialKind k, bool ok, int w)
            {
                if (!ok) return;
                options.Add((int)g * 10 + (int)k);
                weights.Add(w);
            }
            // Bombs (groups of 9+) and prisms (12+) depend too much on the board to be asked for by name in a mixed
            // level (simulation): those levels ask for rockets, specials of any kind, or a combination.
            Opt(GoalKind.CreateSpecial, SpecialKind.RocketRow, true, 20);
            Opt(GoalKind.FireSpecial, SpecialKind.RocketRow, true, 14);
            Opt(GoalKind.CreateSpecial, SpecialKind.None, n >= s.bombsFromLevel, 14);
            Opt(GoalKind.FireSpecial, SpecialKind.None, n >= s.bombsFromLevel, 12);
            Opt(GoalKind.FireCombo, SpecialKind.None, n >= s.combosFromLevel, Recent(n, s.combosFromLevel) ? 22 : 12);
            int total = 0;
            foreach (var w in weights) total += w;
            int x = rng.Next(total), pick = options[0];
            for (int i = 0; i < options.Count; i++)
            {
                x -= weights[i];
                if (x < 0)
                {
                    pick = options[i];
                    break;
                }
            }
            r.SpecialGoal = (GoalKind)(pick / 10);
            r.SpecialKind = (SpecialKind)(pick % 10);
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

        /// <summary>The mechanic a practice level focuses on, or null when n is not a practice level (special tiles'
        /// practice levels are handled by PracticeSpecials).</summary>
        static Mechanic? PracticeFocus(int n, MechanicSchedule s)
        {
            int p = Math.Max(0, s.practiceLevels);
            if (After(n, s.iceFromLevel, p) || After(n, s.thickIceFromLevel, p)) return Mechanic.Ice;
            if (After(n, s.colourGoalsFromLevel, p)) return Mechanic.Colour;
            if (After(n, s.chainsFromLevel, p)) return Mechanic.Chains;
            if (After(n, s.treasureFromLevel, p)) return Mechanic.Treasure;
            if (After(n, s.mossFromLevel, p)) return Mechanic.Moss;
            return null;
        }

        static bool After(int n, int intro, int practice) => n > intro && n <= intro + practice;

        static bool IsSpecialsPractice(int n, MechanicSchedule s)
        {
            int p = Math.Max(0, s.practiceLevels);
            return After(n, s.rocketsFromLevel, p) || After(n, s.bombsFromLevel, p) || After(n, s.prismsFromLevel, p) || After(n, s.combosFromLevel, p);
        }

        /// <summary>How much of each mechanic, by band; less of each when a level combines two.</summary>
        static void Size(LevelRecipe r, int n, MechanicSchedule s, Rng rng)
        {
            int band = n <= 60 ? 0 : n <= 120 ? 1 : n <= 200 ? 2 : 3;
            bool two = r.Mechanics.Count > 1;
            int pct = r.Mechanics.Count > 2 ? 50 : two ? 65 : r.Practice ? 85 : 100;
            int Scale(int v) => Math.Max(1, v * pct / 100);
            int collectPct = two ? 65 : 100;

            if (r.Has(Mechanic.Ice))
            {
                int[] lo = { 6, 8, 10, 12 }, hi = { 9, 12, 14, 16 };
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
            // Moss needs several separate patches to be a goal at all: two or three side by side go in a move or two.
            if (r.Has(Mechanic.Moss)) r.MossSeeds = Math.Max(4, Scale(band <= 1 ? 5 + rng.Next(2) : 6 + rng.Next(3)));
            if (r.Has(Mechanic.Colour))
            {
                int[] lo = { 16, 20, 24, 28 }, hi = { 24, 28, 32, 36 };
                r.CollectAmount = Math.Max(1, (lo[band] + rng.Next(hi[band] - lo[band] + 1)) * collectPct / 100);
            }
            if (r.Has(Mechanic.Specials) && r.SpecialAmount <= 0)
            {
                // Rockets come from groups of 6+, bombs 9+, prisms 12+: fewer of the bigger ones.
                bool rocket = SpecialEffects.IsRocket(r.SpecialKind);
                int amount;
                if (r.SpecialGoal == GoalKind.FireCombo) amount = 1 + (band >= 1 && rng.ChancePermille(400) ? 1 : 0);
                else if (rocket) amount = 2 + rng.Next(band >= 1 ? 3 : 2);
                else amount = 3 + rng.Next(band >= 1 ? 3 : 2);
                if (r.Mechanics.Count > 2) amount = Math.Max(1, amount * 2 / 3);
                r.SpecialAmount = amount;
            }
            // Thick-ice practice really has thick ice (with a little, four frozen tiles went in two moves).
            if (After(n, s.thickIceFromLevel, s.practiceLevels) && r.Has(Mechanic.Ice)) r.ThickPermille = Math.Max(r.ThickPermille, 400);
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
                int placed = BoardDecorator.PlaceTreasure(layout, r.TreasureCount, rng);
                if (placed > 0) goals.Add(GoalSpec.CollectTreasure(placed));
            }
            if (r.Has(Mechanic.Moss))
            {
                int placed = BoardDecorator.PlaceMossSpread(layout, r.MossSeeds, rng);
                if (placed > 0) goals.Add(GoalSpec.ClearMoss(placed));
            }
            if (r.Has(Mechanic.Ice))
            {
                int placed = BoardDecorator.PlaceIce(layout, r.IceCount, r.ThickPermille, rng);
                if (placed > 0) goals.Add(GoalSpec.FreeIce(placed));
            }
            if (r.Has(Mechanic.Chains))
            {
                int placed = BoardDecorator.PlaceChains(layout, r.ChainCount, r.MaxLinks, rng);
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
            if (r.StartSpecial != '\0') PlaceStartSpecial(layout, r.StartSpecial, rng);
            // Special-tile goals ride along with a board goal (a level of nothing but "make 2 rockets" is a score level
            // in disguise), so they are only added when something else was placed.
            if (r.Has(Mechanic.Specials) && r.SpecialAmount > 0 && goals.Count > 0)
            {
                if (r.SpecialGoal == GoalKind.FireCombo) goals.Add(GoalSpec.FireCombo(r.SpecialAmount));
                else if (r.SpecialGoal == GoalKind.FireSpecial) goals.Add(GoalSpec.FireSpecial(r.SpecialKind, r.SpecialAmount));
                else goals.Add(GoalSpec.CreateSpecial(r.SpecialKind, r.SpecialAmount));
            }
            return goals.ToArray();
        }

        /// <summary>One special tile somewhere in the middle of the board, on a free cell.</summary>
        static void PlaceStartSpecial(LevelLayout layout, char piece, Rng rng)
        {
            for (int guard = 0; guard < 200; guard++)
            {
                int c = 1 + rng.Next(Math.Max(1, layout.Width - 2)), r = 1 + rng.Next(Math.Max(1, layout.Height - 3));
                if (!BoardDecorator.Free(layout, c, r)) continue;
                layout.SetPiece(c, r, piece);
                return;
            }
        }

        /// <summary>A copy with every amount scaled (used when a level turns out to need more moves than its band allows).</summary>
        public static LevelRecipe Scaled(LevelRecipe r, int percent)
        {
            var c = new LevelRecipe
            {
                IceCount = Math.Max(r.IceCount > 0 ? 4 : 0, r.IceCount * percent / 100),
                // Fewer frozen tiles, and only a little less of them thick (75% at half size): thinning both as much
                // left trivial levels, keeping all of it left some too hard (simulation).
                ThickPermille = r.ThickPermille * (100 + percent) / 200,
                ChainCount = Math.Max(r.ChainCount > 0 ? 3 : 0, r.ChainCount * percent / 100),
                MaxLinks = percent < 80 ? Math.Max(1, r.MaxLinks - 1) : r.MaxLinks,
                TreasureCount = Math.Max(r.TreasureCount > 0 ? 1 : 0, r.TreasureCount * percent / 100),
                MossSeeds = Math.Max(r.MossSeeds > 0 ? 3 : 0, r.MossSeeds * percent / 100),
                CollectAmount = Math.Max(r.CollectAmount > 0 ? 10 : 0, r.CollectAmount * percent / 100),
                SpecialGoal = r.SpecialGoal,
                SpecialKind = r.SpecialKind,
                SpecialAmount = Math.Max(r.SpecialAmount > 0 ? 1 : 0, r.SpecialAmount * percent / 100),
                StartSpecial = r.StartSpecial,
                TutorialKey = r.TutorialKey,
                Note = r.Note,
                Practice = r.Practice,
            };
            c.Mechanics.AddRange(r.Mechanics);
            return c;
        }

        static string SpecialName(SpecialKind k)
        {
            if (SpecialEffects.IsRocket(k)) return "Rockets";
            if (k == SpecialKind.Bomb) return "Bombs";
            if (k == SpecialKind.Prism) return "Prisms";
            return "Specials";
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
                    case Mechanic.Specials: parts.Add(r.SpecialGoal == GoalKind.FireCombo ? "Combos" : SpecialName(r.SpecialKind)); break;
                    case Mechanic.Score: parts.Add("Score"); break;
                }
            }
            return string.Join(" + ", parts.ToArray());
        }
    }
}
