using System;
using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 3.2: board shapes. Split boards, gaps, holes, stone walls and narrow passages are made of the cells the engine
    /// already had (voids and stones), and these tests check they behave as real shapes: each section falls and fills on
    /// its own, groups never join across a gap, specials and power-ups act on their true area, treasure is collected on its
    /// own section's floor, nothing ever lands in a gap, and every shape fits the screen with touch-sized cells.
    /// </summary>
    public class BoardShapeTests
    {
        static IEnumerable<BoardShape> AllShapes()
        {
            foreach (BoardFamily f in Enum.GetValues(typeof(BoardFamily)))
                for (int v = 0; v < BoardShapes.VariantCount(f); v++)
                    yield return BoardShapes.Build(f, v, 3, new Rng((ulong)(v + 1)));
        }

        static string Column(Board b, int c)
        {
            var chars = new char[b.Height];
            for (int r = 0; r < b.Height; r++)
            {
                var t = b[c, r];
                chars[r] = t.Type == CellType.Void ? 'x' : t.Type == CellType.Stone ? '#' : (char)('0' + t.Colour);
            }
            return new string(chars);
        }

        // ================================================================== Every shape

        [Test]
        public void EveryShapeIsFairAndFitsEveryScreenWithTouchSizedCells()
        {
            // Phones from 16:9 to 19.5:9 (a notch takes 150 reference pixels), a 3:4 tablet and a 10:16 tablet.
            var screens = new (float w, float h, float inset)[] { (1080, 1920, 0), (1080, 2400, 150), (1170, 2532, 150), (1536, 2048, 0), (1200, 1920, 0) };
            int count = 0;
            foreach (var shape in AllShapes())
            {
                count++;
                var l = shape.Layout;
                string at = shape.Family + " variant " + shape.Variant;
                Assert.IsTrue(BoardShapes.IsFair(l, out var error), at + ": " + error);
                Assert.LessOrEqual(l.Width, BoardShapes.MaxColumns, at);
                Assert.LessOrEqual(l.Height, BoardShapes.MaxRows, at);
                foreach (var (w, h, inset) in screens)
                    Assert.GreaterOrEqual(BoardFit.CellOnScreen(w, h, inset, l.Width, l.Height), BoardFit.MinTouchCell, at + " on " + w + " x " + h);
            }
            Assert.GreaterOrEqual(count, 35, "the library has many shapes");
        }

        [Test]
        public void SplitShapesReallyAreSplit()
        {
            Assert.AreEqual(1, BoardShapes.Sections(BoardShapes.Build(BoardFamily.Hollow, 0, 0, new Rng(1)).Layout).Count, "a hollow board is one piece around its hole");
            Assert.AreEqual(2, BoardShapes.Sections(BoardShapes.Build(BoardFamily.TwinTowers, 0, 0, new Rng(1)).Layout).Count);
            Assert.AreEqual(2, BoardShapes.Sections(BoardShapes.Build(BoardFamily.Bunks, 0, 0, new Rng(1)).Layout).Count);
            Assert.AreEqual(2, BoardShapes.Sections(BoardShapes.Build(BoardFamily.StoneWall, 0, 0, new Rng(1)).Layout).Count, "until the wall is broken");
            Assert.AreEqual(1, BoardShapes.Sections(BoardShapes.Build(BoardFamily.Passage, 0, 0, new Rng(1)).Layout).Count, "joined through the opening");
            for (int v = 0; v < BoardShapes.VariantCount(BoardFamily.ThreeSections); v++)
                Assert.AreEqual(3, BoardShapes.Sections(BoardShapes.Build(BoardFamily.ThreeSections, v, 0, new Rng(1)).Layout).Count, "three sections, variant " + v);
            for (int v = 0; v < BoardShapes.VariantCount(BoardFamily.Islands); v++)
                Assert.AreEqual(4, BoardShapes.Sections(BoardShapes.Build(BoardFamily.Islands, v, 0, new Rng(1)).Layout).Count, "four islands, variant " + v);
        }

        [Test]
        public void UnfairShapesAreRefused()
        {
            Assert.IsFalse(BoardShapes.IsFair(LevelLayout.Parse(new[] { "..x.....", "..x.....", "..x.....", "..x....." }), out var e1), "a 2 x 4 section is too small");
            StringAssert.Contains("Section", e1);
            Assert.IsTrue(BoardShapes.IsFair(LevelLayout.Parse(new[] { "......", "......", "x.xxxx", "......", "......" }), out var e2), "a one-cell opening is fine: " + e2);
            Assert.IsFalse(BoardShapes.IsFair(LevelLayout.Parse(new[] { "......", ".....x", "....x.", "......", "......" }), out var e3), "...but a dead-end pocket is not");
            StringAssert.Contains("dead end", e3);
            Assert.IsFalse(BoardShapes.IsFair(LevelLayout.Open(11, 8), out var e4), "too wide for touch-sized cells");
        }

        // ================================================================== Gravity and refill in sections

        [Test]
        public void TwinTowersFallAndFillOnTheirOwn()
        {
            var s = Play.Level(new[] { "12x34", "34x12", "00x43", "12x21" }, null);
            string right3 = Column(s.Board, 3), right4 = Column(s.Board, 4);
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(2, o.GroupSize);
            Assert.IsTrue(o.Falls.All(m => m.From.Col <= 1 && m.To.Col == m.From.Col), "tiles fall only in their own tower");
            Assert.IsTrue(o.Spawns.All(sp => sp.At.Col <= 1 && sp.FromBoardTop), "the left tower fills from its own top");
            Assert.AreEqual(right3, Column(s.Board, 3), "the right tower is untouched");
            Assert.AreEqual(right4, Column(s.Board, 4));
            for (int r = 0; r < 4; r++) Assert.AreEqual(CellType.Void, s.Board[2, r].Type, "the gap stays a gap");
        }

        [Test]
        public void TheLowerHalfFillsFromBeneathTheGapAndTheUpperHalfFromTheTop()
        {
            var s = Play.Level(new[] { "122", "314", "xxx", "441", "213" }, null);
            string upper = Column(s.Board, 0).Substring(3) + Column(s.Board, 1).Substring(3) + Column(s.Board, 2).Substring(3);
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(2, o.Spawns.Count);
            foreach (var sp in o.Spawns)
            {
                Assert.AreEqual(1, sp.SectionTop.Row, "the lower half's top is the row under the gap");
                Assert.IsFalse(sp.FromBoardTop, "new tiles appear from beneath the gap, not from the top of the board");
            }
            Assert.AreEqual(upper, Column(s.Board, 0).Substring(3) + Column(s.Board, 1).Substring(3) + Column(s.Board, 2).Substring(3), "the upper half is untouched");
            o = Play.Tap(s, 1, 4);
            Assert.IsTrue(o.Spawns.Count > 0 && o.Spawns.All(sp => sp.FromBoardTop && sp.At.Row >= 3), "the upper half fills from the top and never below the gap");
            Assert.IsTrue(o.Falls.All(m => m.To.Row >= 3), "nothing falls through the gap");
        }

        [Test]
        public void ANarrowPassageLetsOnlyItsColumnDrainDownwards()
        {
            var s = Play.Level(new[] { "1234", "3412", "x2xx", "4004", "1231" }, null);
            int topTile = s.Board[1, 4].Id;
            var o = Play.Tap(s, 1, 1);
            Assert.AreEqual(2, o.GroupSize);
            Assert.IsTrue(o.Falls.Any(m => m.From.Col == 1 && m.From.Row >= 2 && m.To.Row <= 1), "tiles above the opening fall through it");
            Assert.AreEqual(topTile, s.Board[1, 3].Id, "the column over the opening moved down one");
            Assert.IsTrue(o.Spawns.Any(sp => sp.At.Col == 1 && sp.FromBoardTop), "that column refills from the top of the board");
            var beside = o.Spawns.Single(sp => sp.At.Col == 2);
            Assert.IsFalse(beside.FromBoardTop, "the column beside it refills from beneath the gap");
            Assert.AreEqual(new GridPos(2, 1), beside.SectionTop);
        }

        // ================================================================== Matching

        [Test]
        public void GroupsNeverJoinAcrossAGapButDoGoAroundAHole()
        {
            var finder = new GroupFinder();
            var cells = new List<GridPos>();
            var split = Play.Level(new[] { "12x34", "00x00", "34x12" }, null);
            Assert.AreEqual(2, finder.FindGroup(split.Board, new GridPos(0, 1), cells), "the gap splits the four 0s into two pairs");
            var ring = Play.Level(new[] { "000", "0x0", "000" }, null);
            Assert.AreEqual(8, finder.FindGroup(ring.Board, new GridPos(0, 0), cells), "a group goes round the hole");
            var wall = Play.Level(new[] { "12#34", "00#00", "34#12" }, null);
            Assert.AreEqual(2, finder.FindGroup(wall.Board, new GridPos(0, 1), cells), "a stone wall splits groups too");
        }

        // ================================================================== Specials around barriers

        [Test]
        public void ARocketFliesOverAGapAndBreaksTheStonesInItsLine()
        {
            var s = Play.Specials(new[] { "12x#34", "-1x#25", "34x#12" }, null);
            int stones = s.Board.CountType(CellType.Stone);
            var o = Play.Tap(s, 0, 1);
            var hit = o.Cleared.Select(c => c.Pos).ToList();
            CollectionAssert.IsSupersetOf(hit, new[] { new GridPos(1, 1), new GridPos(4, 1), new GridPos(5, 1) }, "the whole row on both sides of the gap");
            Assert.IsTrue(o.Cleared.Any(c => c.WasStone && c.Pos == new GridPos(3, 1)), "the stone in its line is broken");
            Assert.AreEqual(CellType.Void, s.Board[2, 1].Type, "the gap is untouched");
            Assert.AreEqual(stones - 1, s.Board.CountType(CellType.Stone));
            Assert.AreEqual(CellType.Colour, s.Board[3, 1].Type, "the broken stone's cell fills like any other");
        }

        [Test]
        public void ABombReachesBothSidesOfAGapAndLeavesTheGapAlone()
        {
            var s = Play.Specials(new[] { "12x34", "34x12", "1*x43", "23x21", "41x34" }, null);
            var o = Play.Tap(s, 1, 2);
            var hit = new HashSet<GridPos>(o.Cleared.Select(c => c.Pos));
            for (int r = 0; r < 5; r++)
            {
                Assert.IsTrue(hit.Contains(new GridPos(0, r)), "column 0 row " + r);
                Assert.IsTrue(hit.Contains(new GridPos(3, r)), "the far side of the gap is in the 5 x 5 square, row " + r);
                Assert.IsFalse(hit.Contains(new GridPos(4, r)), "column 4 is outside it");
                Assert.AreEqual(CellType.Void, s.Board[2, r].Type);
            }
        }

        [Test]
        public void APrismClearsItsColourInEverySection()
        {
            var s = Play.Specials(new[] { "12x31", "@3x14", "xxxxx", "41x21", "13x41" }, null);
            int ones = TestUtil.CountColour(s.Board, 1);
            var o = Play.Tap(s, 0, 3, 1);
            Assert.AreEqual(ones, o.Cleared.Count(c => c.Colour == 1 && !c.WasStone), "every 1 on the board, in all four sections");
        }

        // ================================================================== Treasure

        [Test]
        public void TreasureIsCollectedOnTheFloorOfItsOwnSection()
        {
            var s = Play.Level(new[] { "3$4", "211", "xxx", "345", "123" }, null, 20, GoalSpec.CollectTreasure(1));
            var o = Play.Tap(s, 1, 3);
            Assert.AreEqual(1, o.Treasures.Count, "it drops onto the gap, the floor of the upper section, and is collected");
            Assert.AreEqual(new GridPos(1, 3), o.Treasures[0].At);
            Assert.IsTrue(s.Passed);
        }

        [Test]
        public void TreasureIsOnlyPlacedWhereItCanBeBroughtDown()
        {
            foreach (var shape in AllShapes())
                for (int seed = 1; seed <= 6; seed++)
                {
                    var l = shape.Layout.Clone();
                    var placed = CampaignPlacement.PlaceTreasure(l, 4, 2, 7, new Rng((ulong)seed));
                    Assert.IsTrue(l.Validate(out var error), shape.Family + ": " + error);
                    foreach (var t in placed)
                    {
                        int floor = BoardShapes.SectionFloor(l, t.Col, t.Row);
                        Assert.GreaterOrEqual(t.Row - floor, 2, "at least two tiles to dig");
                        Assert.IsTrue(l.IsOpen(t.Col - 1, floor) && l.IsOpen(t.Col + 1, floor),
                            shape.Family + ": the last tile under the treasure at " + t + " can be matched on both sides (never an edge or a gap)");
                        for (int y = floor; y < t.Row; y++) Assert.AreEqual(ObstacleKind.None, l.ObstacleAt(t.Col, y));
                    }
                    var columns = placed.Select(p => p.Col).ToList();
                    for (int i = 0; i < columns.Count; i++)
                        for (int j = i + 1; j < columns.Count; j++) Assert.Greater(Math.Abs(columns[i] - columns[j]), 1, "treasure starts in separate columns");
                }
        }

        [Test]
        public void TreasureHeldUpByIceWaitsUntilTheIceIsFreed()
        {
            // The dependency later levels use: a frozen tile under the treasure holds it up. Clearing beneath the ice
            // doesn't bring the treasure down (rows top first; the ice is under the treasure in column 1).
            var held = Play.Level(new[] { "3$45", "2410", "0532", "1225" }, new[] { "....", ".i..", "....", "...." }, 20, GoalSpec.CollectTreasure(1));
            var o = Play.Tap(held, 1, 0);
            Assert.AreEqual(0, o.Treasures.Count, "clearing beneath the ice doesn't free the treasure");
            Assert.AreEqual(CellType.Treasure, held.Board[1, 3].Type, "it waits on the frozen tile");
            Assert.AreEqual(ObstacleKind.Ice, held.Board[1, 2].Obstacle);

            // CampaignPlacement puts the ice there, and a thoughtful player still brings the treasure down.
            var layout = LevelLayout.Open(7, 8);
            var treasure = CampaignPlacement.PlaceTreasure(layout, 1, 4, 4, new Rng(3));
            Assert.AreEqual(1, treasure.Count);
            Assert.AreEqual(1, CampaignPlacement.BlockTreasure(layout, treasure, ObstacleKind.Ice, 1, new Rng(1)));
            Assert.AreEqual(ObstacleKind.Ice, layout.ObstacleAt(treasure[0].Col, treasure[0].Row - 1), "ice right under the treasure");
            var spec = new LevelSpec { Id = "held", Mode = GameMode.Campaign, Number = 300, Layout = layout, Colours = 5, Moves = 40, Target = 1, Goals = new[] { GoalSpec.CollectTreasure(1), GoalSpec.FreeIce(1) }, Specials = SpecialTier.Prisms };
            int passed = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = (ulong)seed });
                s.Start();
                Bots.PlayOut(s, BotKind.Planner, null, 10000, true);
                if (s.Passed) passed++;
            }
            Assert.GreaterOrEqual(passed, 6, "freed and brought down in most games (the bot looks one move ahead, a player further)");
        }

        // ================================================================== Power-ups around gaps

        [Test]
        public void PowerUpsActOnRealCellsOnly()
        {
            var s = Play.Level(new[] { "0x00", "1x11", "0x01" }, null);
            var o = Play.Use(s, Play.At(PowerUpId.Blast, 0, 1));
            Assert.AreEqual(3, o.Cleared.Count, "a blast beside the gap clears only the cells that exist");
            for (int r = 0; r < 3; r++) Assert.AreEqual(CellType.Void, s.Board[1, r].Type);
            Assert.AreNotEqual(PowerUpFailure.None, Play.Refused(s, Play.Swap(0, 1, 1, 1)), "nothing can be swapped into a gap");

            var burst = Play.Level(new[] { "12x31", "31x13", "12x21" }, null);
            int ones = TestUtil.CountColour(burst.Board, 1);
            o = Play.Use(burst, Play.At(PowerUpId.ColourBurst, 0, 2));
            Assert.AreEqual(ones, o.Cleared.Count, "Colour Burst clears its colour on both sides of the gap");

            var wall = Play.Level(new[] { "1234", "4321", "####", "1234", "4321" }, null);
            o = Play.Use(wall, Play.At(PowerUpId.TileBreaker, 1, 2));
            Assert.IsTrue(o.Cleared.Any(c => c.WasStone), "Tile Breaker breaks a stone in a wall");
            Assert.IsTrue(o.Falls.Any(m => m.From.Col == 1 && m.From.Row > 2 && m.To.Row <= 2), "and the tiles above pour through the opening");
        }

        // ================================================================== Recovery and long play

        [Test]
        public void RecoveryOnAnIslandBoardNeverFillsAGap()
        {
            var layout = LevelLayout.Parse(new[] { "01x01", "10x10", "xxxxx", "01x01", "10x10" });
            var board = BoardGenerator.Generate(layout, new TileSource(new Rng(1), 2));
            Assert.IsFalse(GroupFinder.HasLegalMove(board));
            var result = BoardRecovery.EnsurePlayable(board, new Rng(7), new List<TileMove>());
            Assert.AreNotEqual(RecoveryResult.Failed, result);
            Assert.IsTrue(GroupFinder.HasLegalMove(board));
            TestUtil.AssertBoardInvariants(board, layout);
        }

        [Test]
        public void LongPlayOnEveryShapeKeepsTheBoardWhole()
        {
            // Every shape with ice, chains, moss and treasure placed by the campaign's rules, and special tiles: random
            // play with power-ups never puts a tile in a gap, leaves an empty cell or adds a stone.
            var recipe = new LevelRecipe { IceCount = 8, ThickPermille = 400, ChainCount = 5, MaxLinks = 2, MossSeeds = 4, TreasureCount = 2, TreasureDepthMin = 2, TreasureDepthMax = 6 };
            recipe.Mechanics.AddRange(new[] { Mechanic.Ice, Mechanic.Chains, Mechanic.Moss, Mechanic.Treasure });
            int games = 0, actions = 0, treasures = 0;
            foreach (var shape in AllShapes())
            {
                var layout = shape.Layout.Clone();
                var goals = CampaignProgression.Decorate(recipe, layout, 5, null, new Rng((ulong)(shape.Variant + 3)));
                Assert.IsTrue(layout.Validate(out var error), shape.Family + ": " + error);
                var spec = new LevelSpec { Id = "shape", Mode = GameMode.Campaign, Number = 300, Layout = layout, Colours = 5, Moves = 60, Target = 1, Goals = goals, Specials = SpecialTier.Prisms, MossCap = layout.ObstacleCount(ObstacleKind.Moss) + 2 };
                for (int seed = 1; seed <= 2; seed++)
                {
                    var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = new ScoringRules(), BoardSeed = (ulong)seed });
                    s.Start();
                    games++;
                    var rng = new Rng((ulong)(seed * 31 + shape.Variant));
                    var finder = new GroupFinder();
                    var groups = new List<GroupInfo>();
                    var o = new MoveOutcome();
                    int stones = s.Board.CountType(CellType.Stone);
                    for (int step = 0; step < 60 && s.AcceptsMoves; step++)
                    {
                        bool acted;
                        if (rng.ChancePermille(120))
                            acted = s.TryApplyPowerUp(Play.At(rng.Next(2) == 0 ? PowerUpId.Blast : PowerUpId.TileBreaker, rng.Next(s.Board.Width), rng.Next(s.Board.Height)), o, out _);
                        else
                        {
                            if (!Bots.Choose(s.Board, BotKind.Casual, rng, finder, groups, out var pick)) break;
                            acted = s.TryPlay(pick, o, rng.Next(5));
                        }
                        if (!acted) continue;
                        actions++;
                        treasures += o.Treasures.Count;
                        if (o.FinalePending) s.RunFinale(o);
                        if (!o.RecoveryFailed) TestUtil.AssertBoardInvariants(s.Board, layout);
                        Assert.LessOrEqual(s.Board.CountType(CellType.Stone), stones, shape.Family + ": stones never appear");
                    }
                }
            }
            TestContext.WriteLine(games + " games, " + actions + " actions, " + treasures + " treasures collected on " + AllShapes().Count() + " shapes");
            Assert.Greater(treasures, 10, "treasure reaches its floors on the new shapes");
        }

        [Test]
        public void ChainsAreNeverPlacedWhereTheyCantBeMatched()
        {
            foreach (var shape in AllShapes())
            {
                var l = shape.Layout.Clone();
                CampaignPlacement.PlaceChains(l, 10, 3, false, new Rng(5));
                for (int r = 0; r < l.Height; r++)
                    for (int c = 0; c < l.Width; c++)
                    {
                        if (l.ObstacleAt(c, r) != ObstacleKind.Chain) continue;
                        Assert.GreaterOrEqual(BoardShapes.OpenNeighbours(l, c, r), 3, shape.Family + ": a chained tile at " + new GridPos(c, r) + " has room for a match");
                        if (c == 0 || c == l.Width - 1) Assert.LessOrEqual(l.StrengthAt(c, r), 2, "edge columns never hold three links");
                    }
            }
        }
    }
}
