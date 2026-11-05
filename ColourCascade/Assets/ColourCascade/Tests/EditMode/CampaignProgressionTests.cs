using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.2: the campaign's progression from level 121 (docs/DESIGN.md §3.5). Levels up to 120 are exactly as
    /// before; later ones grow in what they ask, vary their boards, breathe now and then, stay fair for a thoughtful
    /// player and are the same on every device.
    /// </summary>
    public class CampaignProgressionTests
    {
        // One catalog for the fixture: levels 121-500 are generated once (about half a minute) and shared by the tests.
        static readonly Lazy<CampaignCatalog> Shared = new Lazy<CampaignCatalog>(() =>
        {
            var cfg = GameConfigData.CreateDefault();
            var cat = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            Parallel.For(121, 501, n => cat.Get(n));
            return cat;
        });

        static CampaignCatalog Catalog => Shared.Value;

        static int ObstacleCells(LevelSpec s)
        {
            var l = s.Layout;
            return l.ObstacleCount(ObstacleKind.Ice) + l.ObstacleCount(ObstacleKind.Chain) + l.ObstacleCount(ObstacleKind.Moss) + l.TreasureCount;
        }

        static int GoalKinds(LevelSpec s)
        {
            var l = s.Layout;
            return (l.ObstacleCount(ObstacleKind.Ice) > 0 ? 1 : 0) + (l.ObstacleCount(ObstacleKind.Chain) > 0 ? 1 : 0) + (l.ObstacleCount(ObstacleKind.Moss) > 0 ? 1 : 0) +
                   (l.TreasureCount > 0 ? 1 : 0) + (s.EffectiveGoals().Any(g => g.kind == GoalKind.CollectColour) ? 1 : 0);
        }

        static string Shape(LevelLayout l)
        {
            var rows = l.ToRows();
            return l.Width + "x" + l.Height + ":" + string.Join("/", rows.Select(r => new string(r.Select(c => c == 'x' || c == '#' ? c : '.').ToArray())));
        }

        // ================================================================== Levels 1-120 are untouched

        /// <summary>FNV-1a of LevelCodec.Encode for levels 1-120, recorded on the build before update 3.2 (commit 7e4d047)
        /// and checked equal on this one: every target, star, move count, board and goal is exactly as it was.</summary>
        const ulong ExpectedLevels1To120 = 14330711273548788375UL;

        [Test]
        public void LevelsUpTo120AreExactlyAsBefore()
        {
            var cfg = GameConfigData.CreateDefault();
            var cat = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            ulong h = 1469598103934665603UL;
            for (int n = 1; n <= 120; n++)
                foreach (char ch in LevelCodec.Encode(cat.Get(n)))
                {
                    h ^= ch;
                    h *= 1099511628211UL;
                }
            Assert.AreEqual(ExpectedLevels1To120, h);
        }

        // ================================================================== Every later level is valid and fair

        [Test]
        public void EveryLevelFrom121To500IsValidFairAndFitsTheScreen()
        {
            for (int n = 121; n <= 500; n++)
            {
                var spec = Catalog.Get(n);
                var l = spec.Layout;
                string at = "level " + n + " (" + spec.Note + ")";
                Assert.IsTrue(BoardShapes.IsFair(l, out var error), at + ": " + error);
                var band = CampaignDesign.ProgressionBand(n);
                bool goals = spec.Goals != null && spec.Goals.Length > 0;
                // Goal levels are sized within their tier's band; score levels (breathers) keep the short score-level moves.
                Assert.GreaterOrEqual(spec.Moves, goals ? band.MinMoves : 12, at);
                Assert.LessOrEqual(spec.Moves, band.MaxMoves + (l.TreasureCount > 0 ? 3 : 0), at);
                Assert.Less(spec.Target, spec.Star2 + 1, at);
                Assert.Less(spec.Star2, spec.Star3, at);
                Assert.GreaterOrEqual(BoardFit.CellOnScreen(1080, 1920, 0, l.Width, l.Height), BoardFit.MinTouchCell, at + ": touch-sized cells on a 16:9 phone");
                if (spec.Goals == null) continue;
                Assert.LessOrEqual(spec.Goals.Length, 3, at + ": the goals panel shows three");
                foreach (var g in spec.Goals)
                {
                    switch (g.kind)
                    {
                        case GoalKind.FreeIce: Assert.AreEqual(l.ObstacleCount(ObstacleKind.Ice), g.amount, at + ": every frozen tile is asked for"); break;
                        case GoalKind.BreakChains: Assert.AreEqual(l.ObstacleCount(ObstacleKind.Chain), g.amount, at); break;
                        case GoalKind.CollectTreasure: Assert.AreEqual(l.TreasureCount, g.amount, at); break;
                        case GoalKind.ClearMoss: Assert.Greater(l.ObstacleCount(ObstacleKind.Moss), 0, at); break;
                        case GoalKind.CollectColour: Assert.Less(g.colour, spec.Colours, at); break;
                    }
                }
                if (l.ObstacleCount(ObstacleKind.Moss) > 0) Assert.Greater(spec.MossCap, l.ObstacleCount(ObstacleKind.Moss), at);
                // Treasure starts where it can come down: never resting on its floor, with a tile to match on both sides of
                // the last one under it.
                for (int r = 0; r < l.Height; r++)
                    for (int c = 0; c < l.Width; c++)
                    {
                        if (l.PieceAt(c, r) != '$') continue;
                        int floor = BoardShapes.SectionFloor(l, c, r);
                        Assert.Greater(r, floor, at + ": treasure at " + new GridPos(c, r));
                        Assert.IsTrue(l.IsOpen(c - 1, floor) && l.IsOpen(c + 1, floor), at + ": treasure column " + c);
                    }
            }
        }

        [Test]
        public void AThoughtfulPlayerPassesLevelsAcrossTheCampaignOnFreshBoards()
        {
            // Boards the generator never saw: 16 Planner games on each of 16 levels spread over 121-500 (and each new
            // board's introduction). Most levels pass at least 3 games in 4; none falls below half.
            var levels = new[] { 125, 148, 167, 189, 213, 238, 262, 287, 314, 339, 363, 388, 412, 437, 463, 488, 131, 171, 211, 291 };
            int total = 0, wins = 0, below75 = 0;
            foreach (int n in levels)
            {
                var spec = Catalog.Get(n);
                int passed = 0;
                for (int i = 0; i < 16; i++)
                {
                    var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = StableHash.Combine((ulong)n * 7919, (ulong)i + 99) });
                    s.Start();
                    Bots.PlayOut(s, BotKind.Planner, null, 10000, true);
                    if (s.Passed) passed++;
                }
                TestContext.WriteLine("level " + n + ": " + passed + "/16 (" + spec.Note + ", " + spec.Moves + " moves)");
                total += 16;
                wins += passed;
                if (passed < 12) below75++;
                Assert.GreaterOrEqual(passed, 8, "level " + n + " (" + spec.Note + ")");
            }
            Assert.GreaterOrEqual(wins * 100 / total, 82, "a thoughtful player passes most attempts");
            Assert.LessOrEqual(below75, 4);
        }

        // ================================================================== Progression

        [Test]
        public void TheCampaignAsksForMoreAsItGoesOn()
        {
            var ranges = new[] { (121, 200), (201, 300), (301, 400), (401, 500) };
            double lastGoals = 0, lastObstacles = 0, lastKinds = 0;
            foreach (var (a, b) in ranges)
            {
                var specs = Enumerable.Range(a, b - a + 1).Select(n => Catalog.Get(n)).ToList();
                double goals = specs.Average(s => s.EffectiveGoals().Count());
                double obstacles = specs.Average(ObstacleCells);
                double kinds = specs.Average(GoalKinds);
                double moves = specs.Average(s => s.Moves);
                TestContext.WriteLine($"{a}-{b}: {goals:0.00} goals, {kinds:0.00} kinds of objective, {obstacles:0.0} obstacles and treasure, {moves:0.0} moves");
                Assert.Greater(goals, lastGoals, a + "-" + b + ": more goals");
                Assert.Greater(obstacles, lastObstacles, a + "-" + b + ": more to clear");
                Assert.GreaterOrEqual(kinds, lastKinds, a + "-" + b + ": more kinds combined");
                lastGoals = goals;
                lastObstacles = obstacles;
                lastKinds = kinds;
            }
            // Before 3.2, levels 301-400 averaged 1.78 goals and 4.5 obstacles; now the later campaign asks for more.
            Assert.GreaterOrEqual(lastGoals, 2.1);
            Assert.GreaterOrEqual(lastObstacles, 6.5);
        }

        [Test]
        public void BreathersComeBetweenTheHardLevels()
        {
            var breathers = new List<LevelSpec>();
            var hard = new List<LevelSpec>();
            for (int n = 121; n <= 500; n++)
            {
                if (n % 10 == 6) breathers.Add(Catalog.Get(n));
                else if (n % 10 >= 7) hard.Add(Catalog.Get(n));
            }
            Assert.Less(breathers.Average(ObstacleCells), hard.Average(ObstacleCells) * 0.7, "a breather asks for much less");
            Assert.Less(breathers.Average(s => s.EffectiveGoals().Count()), hard.Average(s => s.EffectiveGoals().Count()));
            Assert.Greater(breathers.Count(s => s.Goals == null || s.Goals.Length == 0), 5, "some breathers are score levels");
        }

        [Test]
        public void ObjectivesAreCombinedMoreOftenLater()
        {
            int Combined(int a, int b) => Enumerable.Range(a, b - a + 1).Count(n => GoalKinds(Catalog.Get(n)) >= 2);
            int ThreeKinds(int a, int b) => Enumerable.Range(a, b - a + 1).Count(n => GoalKinds(Catalog.Get(n)) >= 3);
            TestContext.WriteLine("two kinds or more: 121-200 " + Combined(121, 200) + ", 301-400 " + Combined(301, 400) + "; three kinds: " + ThreeKinds(121, 200) + ", " + ThreeKinds(301, 400) + ", " + ThreeKinds(401, 500));
            Assert.Greater(Combined(301, 400), Combined(121, 200) * 100 / 80 * 80 / 100);
            Assert.GreaterOrEqual(Combined(301, 400), 50, "most later levels combine objectives");
            Assert.Greater(ThreeKinds(301, 500), ThreeKinds(121, 200), "three at once mostly later");
            // Treasure that waits on an obstacle (a dependency between goals) appears in the later campaign.
            int held = 0;
            for (int n = 201; n <= 500; n++)
            {
                var l = Catalog.Get(n).Layout;
                for (int r = 1; r < l.Height; r++)
                    for (int c = 0; c < l.Width; c++)
                        if (l.PieceAt(c, r) == '$' && l.ObstacleAt(c, r - 1) != ObstacleKind.None) held++;
            }
            Assert.Greater(held, 10, "treasure held up by ice or chains");
        }

        // ================================================================== Board shapes through the campaign

        [Test]
        public void BoardShapesArriveOneAtATime()
        {
            var shapes = Catalog.Config.shapes;
            var families = Enum.GetValues(typeof(BoardFamily)).Cast<BoardFamily>().Where(f => f != BoardFamily.Rectangle && f != BoardFamily.Obstructed).ToList();
            Assert.AreEqual(families.Count, families.Select(f => shapes.FromLevel(f)).Distinct().Count(), "one new shape at a time");
            foreach (var f in families)
            {
                int intro = shapes.FromLevel(f);
                string name = BoardShapes.NameOf(f);
                var spec = Catalog.Get(intro);
                Assert.AreEqual(BoardShapes.IntroNote(f), spec.Note, "level " + intro + " introduces " + f);
                for (int n = 121; n < intro; n++) StringAssert.DoesNotContain(name, Catalog.Get(n).Note, f + " before its introduction (level " + n + ")");
                int again = Enumerable.Range(intro + 1, 40).Count(n => Catalog.Get(n).Note.Contains(name));
                Assert.GreaterOrEqual(again, 2, f + " comes back soon after it is introduced");
            }
            // Early on mostly plain boards; later most are shaped.
            int Shaped(int a, int b) => Enumerable.Range(a, b - a + 1).Count(n => families.Any(f => Catalog.Get(n).Note.Contains(BoardShapes.NameOf(f))) || Catalog.Get(n).Note.StartsWith("New board", StringComparison.Ordinal));
            TestContext.WriteLine("shaped boards: 121-200 " + Shaped(121, 200) + ", 201-300 " + Shaped(201, 300) + ", 301-400 " + Shaped(301, 400));
            Assert.Less(Shaped(121, 200), Shaped(301, 400));
            Assert.Greater(Shaped(301, 400), 50);
        }

        [Test]
        public void NeighbouringLevelsLookDifferent()
        {
            int same = 0;
            for (int n = 121; n < 500; n++)
                if (Shape(Catalog.Get(n).Layout) == Shape(Catalog.Get(n + 1).Layout)) same++;
            Assert.LessOrEqual(same, 8, "a board shape rarely repeats on the next level");
            int distinct = Enumerable.Range(301, 100).Select(n => Shape(Catalog.Get(n).Layout)).Distinct().Count();
            Assert.GreaterOrEqual(distinct, 30, "levels 301-400 use many different board geometries (19 before 3.2, all 8 x 9)");
            var sizes = Enumerable.Range(121, 380).Select(n => (Catalog.Get(n).Layout.Width, Catalog.Get(n).Layout.Height)).Distinct().Count();
            Assert.GreaterOrEqual(sizes, 8, "board sizes vary");
        }

        // ================================================================== Determinism and switching off

        [Test]
        public void TheCampaignIsTheSameOnEveryDeviceAndFromAnyThread()
        {
            var cfg = GameConfigData.CreateDefault();
            var a = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            var b = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            var levels = new[] { 121, 170, 233, 291, 333, 406, 487 };
            // One catalog built in reverse order on several threads, with a prewarm racing the request.
            b.Prewarm(291);
            Parallel.ForEach(levels.Reverse(), n => b.Get(n));
            foreach (int n in levels)
            {
                Assert.AreEqual(LevelCodec.Encode(a.Get(n)), LevelCodec.Encode(b.Get(n)), "level " + n);
                Assert.AreSame(b.Get(n), b.Get(n), "built once and kept");
            }
            Assert.IsTrue(b.IsGenerated(291));
        }

        [Test]
        public void ALevelSavedToJsonLoadsBackExactlyOnEveryShape()
        {
            // A generated level exported as a level file and loaded back as a hand-made level is the same level and
            // plays the same board, whatever its shape (hand-made levels skip the generator).
            foreach (int n in new[] { 131, 151, 161, 171, 191, 211, 231, 251, 271, 291, 333 })
            {
                var original = Catalog.Get(n);
                string json = LevelCodec.Encode(original);
                var cfg = GameConfigData.CreateDefault();
                cfg.Campaign.overrides = cfg.Campaign.overrides.Concat(new[] { LevelCodec.Decode(json) }).ToArray();
                var loaded = new CampaignCatalog(cfg.Campaign, cfg.Scoring).Get(n);
                Assert.AreEqual(json, LevelCodec.Encode(loaded), "level " + n);
                var x = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = original, Scoring = cfg.Scoring, BoardSeed = 9 });
                var y = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = loaded, Scoring = cfg.Scoring, BoardSeed = 9 });
                x.Start();
                y.Start();
                Assert.AreEqual(x.Board.ToDebugString(), y.Board.ToDebugString(), "level " + n);
                Assert.AreEqual(x.Board.ToObstacleString(), y.Board.ToObstacleString(), "level " + n);
            }
        }

        [Test]
        public void TheProgressionCanBeSwitchedOff()
        {
            var cfg = GameConfigData.CreateDefault();
            cfg.Campaign.progressionFromLevel = 0;
            var old = new CampaignCatalog(cfg.Campaign, cfg.Scoring);
            // Level 323 as it was before update 3.2: one treasure on a plain 8 x 9 board with 50 moves.
            var spec = old.Get(323);
            Assert.AreEqual((8, 9), (spec.Width, spec.Height));
            Assert.AreEqual(50, spec.Moves);
            Assert.AreEqual(1, spec.Layout.TreasureCount);
            Assert.AreNotEqual(LevelCodec.Encode(spec), LevelCodec.Encode(Catalog.Get(323)), "the progression changed it");
        }

        [Test]
        public void ProgressOnAShapedLevelSavesAndLoads()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            int level = ctx.Config.Campaign.shapes.twinTowersFromLevel;
            ctx.Data.HighestUnlocked = level;
            int stars = 0;
            for (int attempt = 0; attempt < 6 && stars == 0; attempt++)
            {
                var setup = ctx.CreateCampaignRun(level);
                setup.BoardSeed = (ulong)(attempt + 1);
                var s = new PuzzleSession(setup);
                s.Start();
                ctx.BeginRun(s);
                Bots.PlayOut(s, BotKind.Planner, null);
                if (s.FinalePending) s.RunFinale(new MoveOutcome());
                stars = ctx.FinishRun(s).Stars;
            }
            Assert.Greater(stars, 0, "the planner completes the twin towers introduction");
            Assert.IsTrue(ctx.Save());
            var reloaded = TestUtil.Context(null, store);
            Assert.AreEqual(stars, reloaded.Campaign.StarsFor(level));
            Assert.AreEqual(level + 1, reloaded.Campaign.HighestUnlocked);
        }
    }
}
