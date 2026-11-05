using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>When each board family joins the campaign (docs/DESIGN.md §3.5). Each gets an introduction level.</summary>
    [Serializable]
    public class BoardShapeSchedule
    {
        public int variedSizesFromLevel = 121;
        public int hollowFromLevel = 131;
        public int towerFromLevel = 151;
        public int wideFromLevel = 161;
        public int twinTowersFromLevel = 171;
        public int bunksFromLevel = 191;
        public int stoneWallFromLevel = 211;
        public int passageFromLevel = 231;
        public int asymmetricFromLevel = 251;
        public int threeSectionsFromLevel = 271;
        public int islandsFromLevel = 291;

        public int FromLevel(BoardFamily f)
        {
            switch (f)
            {
                case BoardFamily.Rectangle: return variedSizesFromLevel;
                case BoardFamily.Obstructed: return variedSizesFromLevel;
                case BoardFamily.Hollow: return hollowFromLevel;
                case BoardFamily.Tower: return towerFromLevel;
                case BoardFamily.Wide: return wideFromLevel;
                case BoardFamily.TwinTowers: return twinTowersFromLevel;
                case BoardFamily.Bunks: return bunksFromLevel;
                case BoardFamily.StoneWall: return stoneWallFromLevel;
                case BoardFamily.Passage: return passageFromLevel;
                case BoardFamily.Asymmetric: return asymmetricFromLevel;
                case BoardFamily.ThreeSections: return threeSectionsFromLevel;
                case BoardFamily.Islands: return islandsFromLevel;
                default: return int.MaxValue;
            }
        }
    }

    /// <summary>Everything decided about a generated level before its board is decorated.</summary>
    public sealed class LevelPlan
    {
        public int Level;
        /// <summary>1 (from progressionFromLevel) to 6; see CampaignProgression.TierName.</summary>
        public int Tier;
        /// <summary>0-100: how demanding the level is, from its tier, the ten-level rhythm, the region and a little noise.</summary>
        public int Intensity;
        /// <summary>A level ending in 6: a breather (a score level or one light goal).</summary>
        public bool Breather;
        /// <summary>The first level on a new kind of board: one familiar goal, gently sized.</summary>
        public bool ShapeIntro;
        public BoardFamily Family;
        public int Variant;
        /// <summary>Null for a score level.</summary>
        public LevelRecipe Recipe;
        public int Colours;
    }

    /// <summary>
    /// The campaign's progression from CampaignConfig.progressionFromLevel on (docs/DESIGN.md §3.5). Difficulty is not one
    /// number: each level gets an intensity (its tier rising through the campaign, the ten-level rhythm with a breather
    /// on every level ending in 6, a lift at region finales, a little noise), and the intensity sets how many kinds of
    /// goal it has, how much of each, how strong the obstacles are, how deep treasure starts and whether goals depend on
    /// each other (treasure held up by ice or chains), alongside the board's shape and its colours. Moves are then sized
    /// from a thoughtful player (CampaignCatalog). Board families arrive one at a time with an introduction level, and
    /// each ten levels follow a plan, so neighbouring levels don't repeat a board or a main mechanic.
    /// Deterministic: everything comes from the level number.
    /// </summary>
    public sealed class CampaignProgression
    {
        static readonly BoardFamily[] AllFamilies =
        {
            BoardFamily.Rectangle, BoardFamily.Obstructed, BoardFamily.Hollow, BoardFamily.Tower, BoardFamily.Wide, BoardFamily.TwinTowers,
            BoardFamily.Bunks, BoardFamily.StoneWall, BoardFamily.Passage, BoardFamily.Asymmetric, BoardFamily.ThreeSections, BoardFamily.Islands,
        };

        static readonly Mechanic[] Primaries = { Mechanic.Ice, Mechanic.Chains, Mechanic.Treasure, Mechanic.Moss, Mechanic.Colour };

        readonly int from;
        readonly BoardShapeSchedule shapes;
        readonly MechanicSchedule mechanics;
        readonly Dictionary<int, (BoardFamily family, int variant)[]> familyPlans = new Dictionary<int, (BoardFamily, int)[]>();
        readonly Dictionary<int, Mechanic[]> mechanicPlans = new Dictionary<int, Mechanic[]>();
        readonly Dictionary<BoardFamily, int> appearances = new Dictionary<BoardFamily, int>();
        int plannedFamiliesUpTo = -1, plannedMechanicsUpTo = -1;

        public CampaignProgression(int fromLevel, BoardShapeSchedule shapes, MechanicSchedule mechanics)
        {
            from = Math.Max(1, fromLevel);
            this.shapes = shapes ?? new BoardShapeSchedule();
            this.mechanics = mechanics ?? new MechanicSchedule();
        }

        public int FromLevel => from;
        public BoardShapeSchedule Shapes => shapes;

        // ------------------------------------------------------------------ Tiers and intensity

        public static int Tier(int n) => n < 161 ? 1 : n < 221 ? 2 : n < 281 ? 3 : n < 351 ? 4 : n < 451 ? 5 : 6;

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return "Advanced";
                case 2: return "Skilled";
                case 3: return "Expert";
                case 4: return "Master";
                case 5: return "Grandmaster";
                default: return "Legend";
            }
        }

        /// <summary>
        /// 5-100. A base rising from 38 at the first level of the progression (about where the levels before it were) to
        /// 75 by level 450, then: levels ending
        /// 1-3 easier (-12), 4-5 as the base, 6 a breather (-30), 7-9 harder (+10), 0 a milestone (+6); the first
        /// three levels of a region a little easier and its finale harder; noise of up to 6 either way.
        /// </summary>
        public int Intensity(int n)
        {
            int p = Math.Max(0, Math.Min(330, n - from));
            int value = 38 + p * 37 / 330;
            switch (n % 10)
            {
                case 1:
                case 2:
                case 3: value -= 12; break;
                case 6: value -= 30; break;
                case 7:
                case 8:
                case 9: value += 10; break;
                case 0: value += 6; break;
            }
            int q = (n - 1) % 20;
            if (q < 3) value -= 4;
            if (q == 19) value += 6;
            var rng = new Rng(StableHash.Of("intensity", n));
            value += rng.Range(-6, 7);
            return Math.Max(5, Math.Min(100, value));
        }

        // ------------------------------------------------------------------ Board families

        public bool IsShapeIntro(int n, out BoardFamily family)
        {
            foreach (var f in AllFamilies)
            {
                if (f == BoardFamily.Rectangle || f == BoardFamily.Obstructed) continue;
                if (shapes.FromLevel(f) == n)
                {
                    family = f;
                    return true;
                }
            }
            family = BoardFamily.Rectangle;
            return false;
        }

        /// <summary>How often a family is picked at level n: none before its introduction, rising for 40 levels, with
        /// a boost for the 20 levels after it arrives so players meet it again while it is new.</summary>
        int Weight(BoardFamily f, int n)
        {
            int start = shapes.FromLevel(f);
            if (n < start) return 0;
            int age = n - start;
            switch (f)
            {
                case BoardFamily.Rectangle: return Math.Max(5, 14 - (n - from) / 30);
                case BoardFamily.Obstructed: return 7;
            }
            int w = Math.Min(10, 4 + age / 8);
            if (age > 0 && age <= 20) w = w * 5 / 2;
            // The most demanding shapes grow more common deep in the campaign.
            if (n >= 301 && (f == BoardFamily.Passage || f == BoardFamily.ThreeSections || f == BoardFamily.Islands || f == BoardFamily.StoneWall || f == BoardFamily.Asymmetric)) w += 3;
            return w;
        }

        static bool Simple(BoardFamily f) => f == BoardFamily.Rectangle || f == BoardFamily.Wide || f == BoardFamily.Tower || f == BoardFamily.Hollow;

        /// <summary>The board family and variant of level n (from the ten-level plans, built in order and kept).</summary>
        readonly object gate = new object();

        public (BoardFamily family, int variant) FamilyFor(int n)
        {
            if (n < from) return (BoardFamily.Rectangle, 2);
            int decade = (n - 1) / 10;
            lock (gate)
            {
                EnsureFamilyPlans(decade);
                return familyPlans[decade][(n - 1) % 10];
            }
        }

        void EnsureFamilyPlans(int decade)
        {
            int first = (from - 1) / 10;
            if (plannedFamiliesUpTo < first - 1) plannedFamiliesUpTo = first - 1;
            while (plannedFamiliesUpTo < decade)
            {
                int d = plannedFamiliesUpTo + 1;
                var plan = new (BoardFamily, int)[10];
                var rng = new Rng(StableHash.Of("shapes", d));
                BoardFamily previous = familyPlans.TryGetValue(d - 1, out var before) ? before[9].Item1 : BoardFamily.Rectangle;
                bool havePrevious = before != null;
                for (int i = 0; i < 10; i++)
                {
                    int n = d * 10 + i + 1;
                    BoardFamily pick;
                    if (n < from) pick = BoardFamily.Rectangle;
                    else if (IsShapeIntro(n, out var intro)) pick = intro;
                    else
                    {
                        var options = new List<BoardFamily>();
                        var weights = new List<int>();
                        foreach (var f in AllFamilies)
                        {
                            int w = Weight(f, n);
                            if (w <= 0) continue;
                            if (n % 10 == 6 && !Simple(f)) continue;
                            if ((havePrevious || i > 0) && f == previous) continue;
                            options.Add(f);
                            weights.Add(w);
                        }
                        pick = options.Count == 0 ? BoardFamily.Rectangle : options[PickIndex(weights, rng)];
                    }
                    appearances.TryGetValue(pick, out int seen);
                    appearances[pick] = seen + 1;
                    // Each appearance of a family uses its next variant, so a family never repeats a board in a row.
                    plan[i] = (pick, seen % BoardShapes.VariantCount(pick));
                    previous = pick;
                    havePrevious = true;
                }
                familyPlans[d] = plan;
                plannedFamiliesUpTo = d;
            }
        }

        // ------------------------------------------------------------------ Main mechanics

        /// <summary>The level's main mechanic: each ten levels use every one twice, shuffled, never the same twice in a row.</summary>
        public Mechanic PrimaryFor(int n)
        {
            int decade = (n - 1) / 10;
            lock (gate)
            {
                EnsureMechanicPlans(decade);
                return mechanicPlans[decade][(n - 1) % 10];
            }
        }

        void EnsureMechanicPlans(int decade)
        {
            int first = (from - 1) / 10;
            if (plannedMechanicsUpTo < first - 1) plannedMechanicsUpTo = first - 1;
            while (plannedMechanicsUpTo < decade)
            {
                int d = plannedMechanicsUpTo + 1;
                var deck = new List<Mechanic>();
                foreach (var m in Primaries)
                {
                    deck.Add(m);
                    deck.Add(m);
                }
                var rng = new Rng(StableHash.Of("mechanics", d));
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var t = deck[i];
                    deck[i] = deck[j];
                    deck[j] = t;
                }
                Mechanic? previous = mechanicPlans.TryGetValue(d - 1, out var before) ? before[9] : (Mechanic?)null;
                for (int i = 0; i < deck.Count; i++)
                {
                    var prev = i == 0 ? previous : deck[i - 1];
                    if (!prev.HasValue || deck[i] != prev.Value) continue;
                    for (int k = i + 1; k < deck.Count; k++)
                        if (deck[k] != prev.Value)
                        {
                            var t = deck[i];
                            deck[i] = deck[k];
                            deck[k] = t;
                            break;
                        }
                }
                mechanicPlans[d] = deck.ToArray();
                plannedMechanicsUpTo = d;
            }
        }

        // ------------------------------------------------------------------ The plan

        public LevelPlan Plan(int n)
        {
            var (family, variant) = FamilyFor(n);
            var plan = new LevelPlan
            {
                Level = n, Tier = Tier(n), Intensity = Intensity(n), Breather = n % 10 == 6, Family = family, Variant = variant,
                ShapeIntro = IsShapeIntro(n, out _),
            };
            var rng = new Rng(StableHash.Of("plan", n));
            int intensity = plan.Intensity - (plan.ShapeIntro ? 20 : 0);
            intensity = Math.Max(5, intensity);
            plan.Intensity = intensity;

            var showcase = CampaignDesign.ShowcaseFor(n, mechanics);
            LevelRecipe r = null;
            if (showcase != null)
            {
                // The obstacle pairings met for the first time (update 2.1) keep their levels and their mixes.
                r = new LevelRecipe { Note = showcase.Value.note, StoneLayout = showcase.Value.stones };
                r.Mechanics.AddRange(showcase.Value.mechanics);
                plan.Family = showcase.Value.stones ? BoardFamily.Obstructed : BoardFamily.Rectangle;
            }
            else if (plan.Breather)
            {
                // Every level ending in 6: half are score levels, the rest one light goal.
                if (rng.ChancePermille(500)) r = new LevelRecipe();
                if (r != null) r.Mechanics.Add(PrimaryFor(n));
            }
            else
            {
                r = new LevelRecipe();
                var primary = PrimaryFor(n);
                r.Mechanics.Add(primary);
                // One kind of goal on gentle levels, two from the middle (more often as it rises), three on the hardest.
                int kinds = plan.ShapeIntro ? 1 : intensity < 25 ? (rng.ChancePermille(250) ? 2 : 1) :
                    intensity < 55 ? (rng.ChancePermille(720 + (intensity - 25) * 8) ? 2 : 1) :
                    (rng.ChancePermille(Math.Min(700, (intensity - 45) * 20)) ? 3 : 2);
                if (n % 10 == 0) kinds = Math.Max(kinds, 2);
                while (r.Mechanics.Count < kinds)
                {
                    var partner = Partner(r, rng, intensity, n, family);
                    if (!partner.HasValue) break;
                    r.Mechanics.Add(partner.Value);
                }
            }

            if (r != null)
            {
                r.Intensity = intensity;
                if (r.Has(Mechanic.Specials)) ChooseSpecialGoal(r, n, rng);
                // A combination goal asks for two specials side by side: only alongside one other kind of goal, and
                // not on boards split into sections (they keep specials apart).
                bool sections = plan.Family == BoardFamily.TwinTowers || plan.Family == BoardFamily.Bunks || plan.Family == BoardFamily.Passage ||
                                plan.Family == BoardFamily.ThreeSections || plan.Family == BoardFamily.Islands;
                if (r.Has(Mechanic.Specials) && r.SpecialGoal == GoalKind.FireCombo && (r.Mechanics.Count > 2 || sections))
                {
                    r.SpecialGoal = GoalKind.CreateSpecial;
                    r.SpecialKind = SpecialKind.None;
                }
                Size(r, plan, rng);
                // Amounts are for a board of about 72 open cells: bigger and smaller boards get more or fewer.
                int open = OpenCells(plan.Family, plan.Variant);
                if (open > 0 && showcase == null) ScaleToBoard(r, open, BoardPermille(plan.Family));
            }
            plan.Recipe = r;
            plan.Colours = ColoursFor(plan, rng);
            return plan;
        }

        /// <summary>A second or third goal that goes well with the ones chosen (pairs that make each other matter). The
        /// second is usually another obstacle, so boards mix them; a third is more often about special tiles or a score.</summary>
        static Mechanic? Partner(LevelRecipe r, Rng rng, int intensity, int n, BoardFamily family)
        {
            bool small = family == BoardFamily.Islands || family == BoardFamily.ThreeSections;
            var options = new List<Mechanic>();
            var weights = new List<int>();
            void Opt(Mechanic m, int w)
            {
                if (w <= 0 || r.Has(m)) return;
                options.Add(m);
                weights.Add(w);
            }
            var p = r.Mechanics[0];
            bool obstacles = r.Mechanics.Count > 1;
            bool third = r.Mechanics.Count >= 2;
            // Obstacles pair with obstacles most (mixed boards are the heart of the later campaign).
            Opt(Mechanic.Ice, p == Mechanic.Chains || p == Mechanic.Treasure ? 38 : 26);
            Opt(Mechanic.Chains, p == Mechanic.Ice || p == Mechanic.Moss || p == Mechanic.Treasure ? 38 : 16);
            Opt(Mechanic.Treasure, p == Mechanic.Ice || p == Mechanic.Moss ? 30 : 16);
            Opt(Mechanic.Moss, p == Mechanic.Chains || p == Mechanic.Treasure ? 28 : 18);
            Opt(Mechanic.Colour, p == Mechanic.Colour ? 0 : 12);
            // Special-tile goals: the board's obstacles give them a purpose (ice in lines for rockets, blocks for bombs).
            Opt(Mechanic.Specials, (18 + intensity / 6) * (third ? 3 : 2) / 3);
            // A score to reach as well, more often later on; never as the only partner of a light level.
            // Not on boards of small sections, where big groups are rare.
            Opt(Mechanic.Score, !small && (obstacles || intensity >= 35) ? (20 + (n >= 301 ? 8 : 0)) * (third ? 2 : 1) : 0);
            if (options.Count == 0) return null;
            return options[PickIndex(weights, rng)];
        }

        static void ChooseSpecialGoal(LevelRecipe r, int n, Rng rng)
        {
            // Bombs and prisms by name depend too much on the board; ask for rockets, any special, or a combination.
            int x = rng.Next(100);
            if (x < 26) { r.SpecialGoal = GoalKind.CreateSpecial; r.SpecialKind = SpecialKind.RocketRow; }
            else if (x < 46) { r.SpecialGoal = GoalKind.FireSpecial; r.SpecialKind = SpecialKind.RocketRow; }
            else if (x < 64) { r.SpecialGoal = GoalKind.CreateSpecial; r.SpecialKind = SpecialKind.None; }
            else if (x < 82) { r.SpecialGoal = GoalKind.FireSpecial; r.SpecialKind = SpecialKind.None; }
            else { r.SpecialGoal = GoalKind.FireCombo; r.SpecialKind = SpecialKind.None; }
        }

        /// <summary>How much of each goal, from the intensity: more with every tier, a little less of each when a level
        /// has several (never down to a token single obstacle), and stronger obstacles later on.</summary>
        static void Size(LevelRecipe r, LevelPlan plan, Rng rng)
        {
            int I = r.Intensity;
            int kinds = 0;
            foreach (var m in r.Mechanics) if (m != Mechanic.Score && m != Mechanic.Specials) kinds++;
            int pct = kinds >= 3 ? 65 : kinds == 2 ? 80 : 100;
            // A breather's one goal is a light one.
            if (plan.Breather) pct = pct * 3 / 5;
            int Scale(int v, int min) => Math.Max(min, v * pct / 100);
            bool split = plan.Family != BoardFamily.Rectangle && plan.Family != BoardFamily.Obstructed && plan.Family != BoardFamily.Tower && plan.Family != BoardFamily.Wide;

            if (r.Has(Mechanic.Ice))
            {
                r.IceCount = Scale(7 + I * 13 / 100 + rng.Next(3), 5);
                r.ThickPermille = Math.Min(600, 300 + 3 * I);
                int x = rng.Next(100);
                bool rockets = r.Has(Mechanic.Specials) && SpecialEffects.IsRocket(r.SpecialKind);
                bool blasts = r.Has(Mechanic.Specials) && !rockets;
                r.IceStyle = rockets && x < 60 ? IceStyle.Lines : blasts && x < 60 ? IceStyle.Blocks : split && x < 35 ? IceStyle.Ledges :
                    x < 55 ? IceStyle.Lines : x < 75 ? IceStyle.Blocks : IceStyle.Scatter;
            }
            if (r.Has(Mechanic.Chains))
            {
                r.ChainCount = Scale(4 + I * 7 / 100 + rng.Next(2), 4);
                r.MaxLinks = I < 30 ? 1 : I < 65 ? 2 : 3;
                r.ChainPairs = I >= 40 && rng.ChancePermille(400);
            }
            if (r.Has(Mechanic.Treasure))
            {
                r.TreasureCount = 1 + I / 40 + (kinds >= 2 && I < 50 ? 0 : rng.Next(2));
                if (split && I >= 45) r.TreasureCount = Math.Max(r.TreasureCount, 2);
                r.TreasureCount = Math.Min(4, Math.Max(kinds >= 3 ? 1 : 2, r.TreasureCount));
                r.TreasureDepthMin = 2 + I / 40;
                r.TreasureDepthMax = 4 + I / 25;
                // Treasure held up by an obstacle: free it first.
                if (I >= 40 && (r.Has(Mechanic.Ice) || r.Has(Mechanic.Chains)))
                {
                    r.TreasureBlock = r.Has(Mechanic.Ice) && (!r.Has(Mechanic.Chains) || rng.ChancePermille(500)) ? ObstacleKind.Ice : ObstacleKind.Chain;
                    r.TreasureBlockStrength = 1;
                }
            }
            if (r.Has(Mechanic.Moss)) r.MossSeeds = Scale(4 + I / 18 + rng.Next(2), 4);
            if (r.Has(Mechanic.Colour)) r.CollectAmount = Math.Max(14, (18 + I / 4 + rng.Next(6)) * (kinds >= 2 ? 70 : 100) / 100);
            if (r.Has(Mechanic.Specials) && r.SpecialAmount <= 0)
            {
                bool rocket = SpecialEffects.IsRocket(r.SpecialKind);
                int amount;
                if (r.SpecialGoal == GoalKind.FireCombo) amount = 1 + (I >= 50 && kinds <= 1 && rng.ChancePermille(450) ? 1 : 0);
                else if (rocket) amount = 2 + I / 40 + rng.Next(2);
                else amount = 3 + I / 35 + rng.Next(2);
                if (kinds >= 3) amount = Math.Max(1, amount * 2 / 3);
                r.SpecialAmount = amount;
            }
            // Board-shape introductions start with a special tile when their goal is about specials, and thick ice is
            // never the whole of a gentle level's ice.
            if (plan.ShapeIntro) r.ThickPermille = Math.Min(r.ThickPermille, 250);
        }

        static int OpenCells(BoardFamily f, int variant)
        {
            var shape = BoardShapes.Build(f, variant, 3, new Rng(1));
            return shape.Layout.OpenCellCount;
        }

        /// <summary>Cells in the board's smallest separate section.</summary>
        public static int SmallestSection(BoardFamily f, int variant)
        {
            var shape = BoardShapes.Build(f, variant, 3, new Rng(1));
            int smallest = int.MaxValue;
            foreach (var s in BoardShapes.Sections(shape.Layout)) smallest = Math.Min(smallest, s.Count);
            return smallest;
        }

        /// <summary>
        /// How much a board shape slows play, as the share of a plain board's obstacles it gets (measured: a thoughtful
        /// player needed a median 24 moves for a level's goals on a plain board, 34 on a hollow or stone board, 38
        /// through a narrow passage and 45 on islands or in three sections). The shape itself is part of the difficulty.
        /// </summary>
        public static int BoardPermille(BoardFamily f)
        {
            switch (f)
            {
                case BoardFamily.Islands: return 600;
                case BoardFamily.ThreeSections: return 700;
                case BoardFamily.Passage:
                case BoardFamily.Hollow:
                case BoardFamily.Obstructed: return 850;
                case BoardFamily.TwinTowers: return 1100;
                default: return 1000;
            }
        }

        static void ScaleToBoard(LevelRecipe r, int open, int boardPermille)
        {
            int permille = Math.Max(800, Math.Min(1200, open * 1000 / 72)) * boardPermille / 1000;
            int S(int v, int min) => v <= 0 ? v : Math.Max(min, v * permille / 1000);
            r.IceCount = S(r.IceCount, 5);
            r.ChainCount = S(r.ChainCount, 4);
            r.MossSeeds = S(r.MossSeeds, 4);
            r.CollectAmount = S(r.CollectAmount, 14);
        }

        /// <summary>
        /// Colours: five as a rule; four when chains of several links or a goal about big specials need large groups;
        /// six now and then on a later score-heavy level with room to match (and every fourth score level).
        /// </summary>
        static int ColoursFor(LevelPlan plan, Rng rng)
        {
            var r = plan.Recipe;
            if (r == null || r.Mechanics.Count == 0) return plan.Level % 4 == 0 ? 6 : 5;
            if (r.Has(Mechanic.Chains) && (r.MaxLinks >= 2 || r.ChainCount >= 8)) return 4;
            if (r.Has(Mechanic.Specials) && (r.SpecialGoal == GoalKind.FireCombo || !SpecialEffects.IsRocket(r.SpecialKind))) return 4;
            if (r.Has(Mechanic.Treasure) && r.TreasureCount >= 3) return 4;
            if (plan.Intensity >= 55 && !r.Has(Mechanic.Chains) && !r.Has(Mechanic.Treasure) && Simple(plan.Family) && rng.ChancePermille(300)) return 6;
            return 5;
        }

        // ------------------------------------------------------------------ Decorating

        /// <summary>Places the recipe on the board (any shape) and returns its goals. Each count in a goal is what was
        /// actually placed.</summary>
        public static GoalSpec[] Decorate(LevelRecipe r, LevelLayout layout, int colours, int[] weights, Rng rng)
        {
            var goals = new List<GoalSpec>();
            if (r.Has(Mechanic.Treasure))
            {
                var placed = CampaignPlacement.PlaceTreasure(layout, r.TreasureCount, r.TreasureDepthMin, r.TreasureDepthMax, rng);
                if (placed.Count == 0) placed = CampaignPlacement.PlaceTreasure(layout, r.TreasureCount, 2, layout.Height - 2, rng);
                if (placed.Count > 0)
                {
                    goals.Add(GoalSpec.CollectTreasure(placed.Count));
                    if (r.TreasureBlock != ObstacleKind.None) CampaignPlacement.BlockTreasure(layout, placed, r.TreasureBlock, r.TreasureBlockStrength, rng);
                }
            }
            if (r.Has(Mechanic.Moss))
            {
                BoardDecorator.PlaceMossSpread(layout, r.MossSeeds, rng);
                int moss = layout.ObstacleCount(ObstacleKind.Moss);
                if (moss > 0) goals.Add(GoalSpec.ClearMoss(moss));
            }
            if (r.Has(Mechanic.Ice))
            {
                CampaignPlacement.PlaceIce(layout, r.IceCount, r.ThickPermille, r.IceStyle, rng);
                int ice = layout.ObstacleCount(ObstacleKind.Ice);
                if (ice > 0) goals.Add(GoalSpec.FreeIce(ice));
            }
            if (r.Has(Mechanic.Chains))
            {
                CampaignPlacement.PlaceChains(layout, r.ChainCount, r.MaxLinks, r.ChainPairs, rng);
                int chains = layout.ObstacleCount(ObstacleKind.Chain);
                if (chains > 0) goals.Add(GoalSpec.BreakChains(chains));
            }
            if (r.Has(Mechanic.Colour) && r.CollectAmount > 0)
            {
                var common = new List<int>();
                for (int c = 0; c < colours; c++)
                    if (weights == null || c >= weights.Length || weights[c] >= 10) common.Add(c);
                if (common.Count == 0) common.Add(0);
                goals.Add(GoalSpec.Collect(common[rng.Next(common.Count)], r.CollectAmount));
            }
            if (r.Has(Mechanic.Specials) && r.SpecialAmount > 0 && goals.Count > 0)
            {
                if (r.SpecialGoal == GoalKind.FireCombo) goals.Add(GoalSpec.FireCombo(r.SpecialAmount));
                else if (r.SpecialGoal == GoalKind.FireSpecial) goals.Add(GoalSpec.FireSpecial(r.SpecialKind, r.SpecialAmount));
                else goals.Add(GoalSpec.CreateSpecial(r.SpecialKind, r.SpecialAmount));
            }
            // At most three goals with the score goal (the goals panel shows three).
            int limit = r.Has(Mechanic.Score) ? 2 : 3;
            if (goals.Count > limit) goals.RemoveRange(limit, goals.Count - limit);
            return goals.ToArray();
        }

        /// <summary>The level card's line: the board's name (or its introduction) and what the level asks for.</summary>
        public static string NoteFor(LevelPlan plan, LevelRecipe r, bool regionFinale)
        {
            string board = plan.ShapeIntro ? BoardShapes.IntroNote(plan.Family) : BoardShapes.NameOf(plan.Family);
            string goals = r != null ? CampaignDesign.NoteFor(r) : "";
            string note = string.IsNullOrEmpty(board) ? goals : string.IsNullOrEmpty(goals) || plan.ShapeIntro ? board : board + " · " + goals;
            if (regionFinale) note = string.IsNullOrEmpty(note) ? "Region finale" : "Region finale · " + note;
            return note;
        }

        static int PickIndex(List<int> weights, Rng rng)
        {
            int total = 0;
            foreach (var w in weights) total += w;
            int x = rng.Next(Math.Max(1, total));
            for (int i = 0; i < weights.Count; i++)
            {
                x -= weights[i];
                if (x < 0) return i;
            }
            return weights.Count - 1;
        }
    }
}
