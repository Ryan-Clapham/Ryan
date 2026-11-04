using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Update 2.1: special tiles score what they actually reach (50 a tile, 75 in a combination, obstacles and stones
    /// on top), bombs reach the 5 × 5 square centred on them, rockets and bombs break stones, specials left on the board go off when the moves
    /// run out, the score breakdown, every power-up's points, tutorial power-ups and the tutorial hand.
    /// </summary>
    public class ScoringAndFinaleTests
    {
        const int Tile = 50, Combo = 75;

        /// <summary>A w × h checkerboard of colours 0 and 1 (no groups), with pieces at (col, row) (row 0 at the bottom).</summary>
        static string[] Grid(int w, int h, params (int col, int row, char piece)[] pieces)
        {
            var rows = new string[h];
            for (int i = 0; i < h; i++)
            {
                int r = h - 1 - i;
                var chars = new char[w];
                for (int c = 0; c < w; c++) chars[c] = (c + r) % 2 == 0 ? '0' : '1';
                foreach (var p in pieces) if (p.row == r) chars[p.col] = p.piece;
                rows[i] = new string(chars);
            }
            return rows;
        }

        static void AssertEachTileScoredOnce(MoveOutcome o, PuzzleSession s)
        {
            Assert.AreEqual(o.Cleared.Count, o.Cleared.Select(c => c.TileId).Distinct().Count(), "a tile was removed twice");
            Assert.AreEqual(o.SpecialsFired.Count, o.SpecialsFired.Select(f => f.TileId).Distinct().Count(), "a special fired twice");
            Assert.AreEqual(s.Score, s.Breakdown.Total, "every point is in the breakdown exactly once");
        }

        // ------------------------------------------------------------------ Rockets

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(8)]
        [TestCase(10)]
        public void ARowRocketScoresEveryTileOfItsRow(int width)
        {
            var s = Play.Specials(Grid(width, 9, (0, 4, '-')), null);
            var o = Play.Tap(s, 0, 4);
            Assert.AreEqual(width - 1, o.Cleared.Count);
            Assert.AreEqual((width - 1) * Tile, o.ScoreGained, "a longer row scores more");
            AssertEachTileScoredOnce(o, s);
        }

        [TestCase(6)]
        [TestCase(9)]
        [TestCase(12)]
        public void AColumnRocketScoresEveryTileOfItsColumn(int height)
        {
            var s = Play.Specials(Grid(8, height, (2, 0, '|')), null);
            var o = Play.Tap(s, 2, 0);
            Assert.AreEqual((height - 1) * Tile, o.ScoreGained);
        }

        [Test]
        public void ARocketScoresOnlyWhatItReaches()
        {
            // A hole, a tile, the rocket, a stone, a tile, a frozen tile: the hole scores nothing, the stone breaks
            // (100), the frozen tile loses its ice (100) and stays.
            var s = Play.Specials(new[] { "101010", "x0-#01", "010101" }, new[] { "......", ".....i", "......" });
            var o = Play.Tap(s, 2, 1);
            Assert.AreEqual(2 * Tile + s.Scoring.stonePoints + s.Scoring.iceLayerPoints, o.ScoreGained);
            Assert.AreEqual(1, o.Cleared.Count(c => c.WasStone));
            Assert.AreEqual(CellType.Void, s.Board[0, 1].Type, "holes are untouched");
            AssertEachTileScoredOnce(o, s);
        }

        // ------------------------------------------------------------------ Bombs

        [TestCase(0, 8, 0)]
        [TestCase(3, 8, 2)]
        [TestCase(4, 8, 2)]
        [TestCase(7, 8, 4)]
        [TestCase(4, 9, 3)]
        [TestCase(1, 3, 0)]
        public void TheBombSquareLeansToTheMiddleAndStaysOnTheBoard(int pos, int extent, int expectedStart)
        {
            // Width 8: column 0 -> 0-3, 3 -> 2-5, 4 -> 2-5, 7 -> 4-7; the middle row of 9 leans down (3-6); a board
            // narrower than the square: all of it.
            Assert.AreEqual(expectedStart, SpecialEffects.BlockStart(pos, extent, 4));
        }

        [TestCase(3, 4, 25)]
        [TestCase(4, 4, 25)]
        [TestCase(1, 1, 16)]
        [TestCase(0, 0, 9)]
        [TestCase(7, 8, 9)]
        [TestCase(7, 0, 9)]
        [TestCase(0, 8, 9)]
        [TestCase(0, 4, 15)]
        [TestCase(7, 4, 15)]
        [TestCase(3, 0, 15)]
        [TestCase(3, 8, 15)]
        public void ABombHitsTheFiveByFiveSquareCentredOnItCutAtTheEdges(int col, int row, int cells)
        {
            // 8 x 9: the middle reaches all 25 cells, an edge 15 (three rows or columns of five), a corner 9.
            var s = Play.Specials(Grid(8, 9, (col, row, '*')), null);
            var o = Play.Tap(s, col, row);
            Assert.AreEqual(cells - 1, o.Cleared.Count, "the square on the board, less the bomb");
            Assert.AreEqual((cells - 1) * Tile, o.ScoreGained);
            Assert.IsTrue(o.Cleared.All(c => System.Math.Abs(c.Pos.Col - col) <= 2 && System.Math.Abs(c.Pos.Row - row) <= 2), "centred on the bomb");
            Assert.AreEqual(cells - 1, o.EffectCells.Count, "the view is told exactly these cells, nothing off the board");
            Assert.IsTrue(o.EffectCells.All(e => s.Board.InBounds(e.Pos)));
        }

        [TestCase(5, 5)]
        [TestCase(6, 6)]
        [TestCase(7, 7)]
        [TestCase(8, 9)]
        [TestCase(10, 12)]
        public void ABombInTheMiddleOfAnyBoardReachesTwentyFiveCells(int w, int h)
        {
            var s = Play.Specials(Grid(w, h, (w / 2, h / 2, '*')), null);
            var o = Play.Tap(s, w / 2, h / 2);
            Assert.AreEqual(24, o.Cleared.Count);
            TestContext.WriteLine(w + " x " + h + ": a bomb reaches 25 of " + (w * h) + " cells (" + (2500 / (w * h)) + "%)");
        }

        [Test]
        public void OnBoardsSmallerThanItsSquareABombClearsEverything()
        {
            var s = Play.Specials(Grid(3, 3, (1, 1, '*')), null);
            var o = Play.Tap(s, 1, 1);
            Assert.AreEqual(8, o.Cleared.Count);
            Assert.AreEqual(8 * Tile, o.ScoreGained);
        }

        [Test]
        public void ABombBreaksStonesAndHitsObstaclesInItsSquare()
        {
            // Bomb at (2,2) on 6 x 6: columns 0-4, rows 0-4. Two stones and a frozen tile inside; a stone outside.
            var rows = Grid(6, 6, (2, 2, '*'), (1, 1, '#'), (4, 4, '#'), (5, 5, '#'));
            var obstacles = new[] { "......", "......", "...i..", "......", "......", "......" };
            var s = Play.Specials(rows, obstacles);
            var o = Play.Tap(s, 2, 2);
            Assert.AreEqual(2, o.Cleared.Count(c => c.WasStone), "both stones in the square");
            Assert.AreEqual(1, s.Board.CountType(CellType.Stone), "the one outside stays");
            Assert.AreEqual(1, o.ObstacleHits.Count);
            int tiles = o.Cleared.Count(c => !c.WasStone);
            Assert.AreEqual(21, tiles, "25 cells less the bomb, two stones and the frozen tile (which stays)");
            Assert.AreEqual(tiles * Tile + 2 * s.Scoring.stonePoints + s.Scoring.iceLayerPoints, o.ScoreGained);
            AssertEachTileScoredOnce(o, s);
        }

        // ------------------------------------------------------------------ Stones

        [Test]
        public void OnlySpecialsAndTileBreakerBreakStones()
        {
            // A group next to a stone: the stone stays.
            var a = TestUtil.Session(GameMode.Campaign, 9, 100000, 4, "0#12", "0012", "3333");
            Play.Tap(a, 0, 0);
            Assert.AreEqual(1, a.Board.CountType(CellType.Stone), "matching never breaks stones");
            // Blast and Colour Burst leave stones alone.
            var b = TestUtil.Session(GameMode.Campaign, 9, 100000, 4, "0#12", "1012", "3233");
            Play.Use(b, Play.At(PowerUpId.Blast, 1, 1));
            Assert.AreEqual(1, b.Board.CountType(CellType.Stone), "Blast doesn't break stones");
            Play.Use(b, Play.At(PowerUpId.ColourBurst, 3, 0));
            Assert.AreEqual(1, b.Board.CountType(CellType.Stone));
            // A prism (a colour) never reaches a stone.
            var c = Play.Specials(Grid(5, 5, (2, 2, '@'), (0, 0, '#')), null);
            Play.Tap(c, 2, 2, 0);
            Assert.AreEqual(1, c.Board.CountType(CellType.Stone));
            // Tile Breaker does.
            var d = TestUtil.Session(GameMode.Campaign, 9, 100000, 4, "0#12", "1012", "3233");
            var o = Play.Use(d, Play.At(PowerUpId.TileBreaker, 1, 2));
            Assert.AreEqual(0, d.Board.CountType(CellType.Stone));
            Assert.AreEqual(d.Scoring.stonePoints, o.ScoreGained);
        }

        [Test]
        public void ABrokenStoneLetsTilesFallThroughAndTheBoardRefills()
        {
            // Column 2 is split by a stone in row 2. The rocket in row 2 breaks it.
            var rows = new[] { "01010", "10101", "-0#01", "10101", "01010" };
            var s = Play.Specials(rows, null);
            int above = s.Board[2, 3].Id;
            var o = Play.Tap(s, 0, 2);
            Assert.IsTrue(o.Cleared.Any(c => c.WasStone && c.Pos == new GridPos(2, 2)));
            Assert.IsTrue(o.Falls.Any(f => f.TileId == above && f.To.Row < 3), "the tile above falls into the opened column");
            TestUtil.AssertBoardInvariants(s.Board, s.Level.Layout);
            Assert.AreEqual(0, s.Board.CountType(CellType.Empty));
        }

        // ------------------------------------------------------------------ Combinations and chain reactions

        [Test]
        public void EveryCombinationPaysTheCombinationRateOnWhatItReaches()
        {
            var cases = new (char a, char b)[] { ('-', '|'), ('-', '*'), ('*', '*'), ('@', '-'), ('@', '*'), ('@', '@') };
            foreach (var (a, b) in cases)
            {
                var s = Play.Specials(Grid(8, 9, (3, 4, a), (4, 4, b)), null);
                var o = Play.Tap(s, 3, 4, 0);
                Assert.AreEqual(1, o.SpecialsFired.Count, a + "+" + b);
                Assert.AreNotEqual(SpecialKind.None, o.SpecialsFired[0].ComboWith, a + "+" + b + " fire together");
                int tiles = o.Cleared.Count(c => !c.WasStone && c.Wave == o.SpecialsFired[0].Wave);
                Assert.Greater(tiles, 0);
                Assert.AreEqual(tiles * Tile, s.Breakdown.Specials, a + "+" + b);
                Assert.AreEqual(tiles * (Combo - Tile), s.Breakdown.ComboBonus, a + "+" + b + ": the combination bonus is its own line");
                AssertEachTileScoredOnce(o, s);
            }
        }

        [Test]
        public void TwoPrismsClearTheWholeBoardForABigScoreButNotTheLevel()
        {
            // Every colour tile goes (70 tiles at 75 = 5,250), but thick ice and double chains only take one step:
            // a full-board clear is a huge score, not an automatic pass (docs/MECHANICS.md §2.3).
            var rows = Grid(8, 9, (3, 4, '@'), (4, 4, '@'));
            var obstacles = new[] { "........", "........", ".I....I.", "........", "........", "........", ".2....2.", "........", "........" };
            var s = Play.Specials(rows, obstacles, SpecialTier.Prisms, 20, GoalSpec.FreeIce(2), GoalSpec.BreakChains(2));
            var o = Play.Tap(s, 3, 4, 0);
            int tiles = o.Cleared.Count(c => !c.WasStone);
            Assert.AreEqual(70 - 4, tiles, "every colour tile except the four under obstacles that stay");
            Assert.GreaterOrEqual(o.ScoreGained, tiles * Combo);
            Assert.IsFalse(s.Passed, "thick ice and double chains are still there");
            Assert.AreEqual(0, s.Goals.Progress(0));
            AssertEachTileScoredOnce(o, s);
        }

        [Test]
        public void ChainReactionsScoreEachTileOnceWhicheverSpecialRemovesIt()
        {
            // Row rocket -> column rocket -> bomb: three specials, each fires once, no tile counted twice.
            var s = Play.Specials(Grid(8, 9, (0, 4, '-'), (5, 4, '|'), (5, 7, '*')), null);
            var o = Play.Tap(s, 0, 4);
            Assert.AreEqual(3, o.SpecialsFired.Count);
            int tiles = o.Cleared.Count(c => !c.WasStone);
            Assert.AreEqual(tiles * Tile, o.ScoreGained);
            AssertEachTileScoredOnce(o, s);
            // Each tile in the cleared set appears once although the rocket lines and the bomb overlap.
            Assert.AreEqual(tiles, o.Cleared.Select(c => c.Pos).Distinct().Count());
        }

        // ------------------------------------------------------------------ Specials left on the board

        [Test]
        public void SpecialsLeftWhenTheMovesRunOutGoOffBeforeTheResult()
        {
            // One move left; the rocket in row 1 would free the last frozen tile, but the player uses the move elsewhere.
            var s = Play.Specials(new[] { "33212", "-0102", "12345" }, new[] { ".....", "....i", "....." }, SpecialTier.Prisms, 1, GoalSpec.FreeIce(1));
            var last = Play.Tap(s, 0, 2);
            Assert.IsTrue(last.FinalePending);
            Assert.IsFalse(last.Ended, "the result waits for the specials");
            Assert.IsTrue(s.FinalePending);
            Assert.IsFalse(s.AcceptsMoves, "no more moves");
            int moves = s.MovesMade, score = s.Score;
            var o = new MoveOutcome();
            Assert.IsTrue(s.RunFinale(o));
            Assert.AreEqual(OutcomeKind.Finale, o.Kind);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(moves, s.MovesMade, "no move is charged");
            Assert.AreEqual(0, s.MovesLeft);
            Assert.Greater(s.Score, score);
            Assert.AreEqual(s.Score - score, s.Breakdown.Finale, "the finale's points are their own line");
            Assert.IsTrue(s.Goals.IsComplete(0), "goal progress counts");
            Assert.AreEqual(SessionState.Won, s.State, "the goal it completed passes the level");
            Assert.AreEqual(s.Score, s.FinalScore, "nothing is added for moves");
            Assert.IsTrue(o.Ended);
            Assert.IsFalse(s.RunFinale(o), "only once");
        }

        [Test]
        public void AdjacentSpecialsLeftAtTheEndGoOffTogetherAsACombination()
        {
            // A rocket and a rocket side by side when the moves run out fire as one combination (a cross).
            var s = Play.Specials(new[] { "00123", "45-|1", "23451" }, null, SpecialTier.Prisms, 1);
            Play.Tap(s, 0, 2);
            Assert.IsTrue(s.FinalePending);
            var o = new MoveOutcome();
            s.RunFinale(o);
            Assert.AreEqual(1, o.SpecialsFired.Count, "one combination");
            Assert.AreEqual(2, o.SpecialsFired[0].ComboSize);
            Assert.AreEqual(1, o.ComboMembers.Count);
            Assert.AreEqual(0, Play.CountPieces(s.Board, CellType.Special));
            Assert.Greater(s.FinaleBreakdown.ComboBonus, 0, "the combination bonus is part of the finale");
            AssertEachTileScoredOnce(o, s);
            Assert.AreEqual(SessionState.Failed, s.State, "its 1,000,000 target was not reached: the finale scores, it doesn't pass levels by itself");
        }

        [Test]
        public void AFailedLevelStaysFailedWhenTheFinaleDoesNotFinishItsGoals()
        {
            var s = Play.Specials(new[] { "33212", "-0102", "12345" }, new[] { ".....", "....I", "....." }, SpecialTier.Prisms, 1, GoalSpec.FreeIce(1));
            Play.Tap(s, 0, 2);
            s.RunFinale(new MoveOutcome());
            Assert.AreEqual(SessionState.Failed, s.State, "thick ice needs two hits");
            Assert.AreEqual(EndReason.OutOfMoves, s.EndReason);
            // Extra Moves: a special made now goes off again if these run out too.
            Assert.IsTrue(s.AddMoves(1));
            Assert.IsFalse(s.FinalePending);
            Assert.AreEqual(SessionState.Playing, s.State);
        }

        [Test]
        public void TheFinaleRunsInDailyAndEndlessButNotInTheTimedRush()
        {
            foreach (var mode in new[] { GameMode.Daily, GameMode.Endless, GameMode.WeeklyRush })
            {
                var setup = TestUtil.Setup(mode, 1, 100000, TileColours.Max, new[] { "33212", "-0102", "12345" }, null);
                setup.Level.Specials = SpecialTier.Prisms;
                var s = new PuzzleSession(setup);
                s.Start();
                s.StartTimer();
                var o = Play.Tap(s, 0, 2);
                if (mode == GameMode.WeeklyRush)
                {
                    Assert.IsFalse(o.FinalePending, "Weekly Rush is timed: moves don't run out");
                    continue;
                }
                Assert.IsTrue(o.FinalePending, mode.ToString());
                s.RunFinale(o);
                Assert.AreEqual(SessionState.Finished, s.State, mode.ToString());
                Assert.Greater(s.Breakdown.Finale, 0, mode.ToString());
            }
        }

        [Test]
        public void FinishingSetsOffTheSpecialsAndAddsNothingForUnusedMoves()
        {
            var s = Play.Specials(new[] { "33212", "-0102", "12345" }, null, SpecialTier.Prisms, 10, GoalSpec.Collect(3, 2));
            Play.Tap(s, 0, 2);
            Assert.IsTrue(s.Passed);
            int moves = s.MovesLeft;
            var o = new MoveOutcome();
            Assert.IsTrue(s.Finish(o));
            Assert.AreEqual(OutcomeKind.Finale, o.Kind, "the rocket went off first");
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.Greater(s.Breakdown.Finale, 0);
            Assert.Greater(moves, 0);
            Assert.AreEqual(s.Score, s.FinalScore, "unused moves score nothing");
            Assert.AreEqual(s.Breakdown.Total, s.FinalScore, "the breakdown is the whole final score");
            Assert.AreEqual(s.Breakdown.Finale, s.FinaleBreakdown.Total, "and the finale's own lines add up to its line");
            int final = s.FinalScore;
            Assert.IsFalse(s.Finish(o));
            Assert.AreEqual(final, s.FinalScore, "finishing twice changes nothing");
            var ctx = TestUtil.Context();
            var r = ctx.FinishRun(s);
            Assert.AreEqual(s.FinalScore, r.FinalScore);
            Assert.AreEqual(s.FinaleBreakdown.Specials, r.FinaleBreakdown.Specials);
            Assert.AreEqual(s.Breakdown.Finale, r.Breakdown.Finale, "the result screen gets the breakdown");
            Assert.AreSame(r, ctx.FinishRun(s), "and the result is recorded once");
        }

        // ------------------------------------------------------------------ Every power-up's points

        [Test]
        public void EveryPowerUpScoresWhatItActuallyDoes()
        {
            var sc = new ScoringRules();
            // Blast: 20 a tile in its 3 x 3 square, obstacles on top, and the goals it serves.
            var a = Play.Level(new[] { "0000", "0000", "0000" }, new[] { "....", ".i..", "...." }, 20, GoalSpec.Collect(0, 20), GoalSpec.FreeIce(1));
            var blast = Play.Use(a, Play.At(PowerUpId.Blast, 1, 1));
            Assert.AreEqual(8 * sc.powerUpPointsPerTile + sc.iceLayerPoints, blast.ScoreGained, "8 tiles and one ice layer (the frozen tile stays)");
            Assert.AreEqual(8, a.Goals.Progress(0));
            Assert.AreEqual(1, a.Goals.Progress(1));
            Assert.AreEqual(blast.ScoreGained, a.Breakdown.PowerUps + a.Breakdown.Obstacles);
            // Tile Breaker: a tile 20, a stone 100, thick ice both layers (the tile stays).
            var b = Play.Level(new[] { "0#122", "34555" }, new[] { ".....", "....I" }, 20);
            Assert.AreEqual(sc.powerUpPointsPerTile, Play.Use(b, Play.At(PowerUpId.TileBreaker, 0, 1)).ScoreGained);
            Assert.AreEqual(sc.stonePoints, Play.Use(b, Play.At(PowerUpId.TileBreaker, 1, 1)).ScoreGained);
            Assert.AreEqual(2 * sc.iceLayerPoints, Play.Use(b, Play.At(PowerUpId.TileBreaker, 4, 0)).ScoreGained);
            // Swap and Colour Shift move or change nothing that scores.
            var c = Play.Level(new[] { "0123", "1233" }, null, 20);
            Assert.AreEqual(0, Play.Use(c, Play.Swap(0, 0, 1, 0)).ScoreGained);
            Assert.AreEqual(0, Play.Use(c, Play.Shift(0, 1, 2)).ScoreGained);
            // Colour Burst: 20 for each tile of the colour.
            var d = Play.Level(new[] { "0120", "1201", "2011" }, null, 20, GoalSpec.Collect(0, 4));
            var burst = Play.Use(d, Play.At(PowerUpId.ColourBurst, 1, 0));
            Assert.AreEqual(4 * sc.powerUpPointsPerTile, burst.ScoreGained);
            Assert.AreEqual(4, d.Goals.Progress(0));
            foreach (var s in new[] { a, b, c, d }) Assert.AreEqual(s.Score, s.Breakdown.Total);
        }

        // ------------------------------------------------------------------ Tutorial power-ups

        static PuzzleSession QuickLevel(GameContext ctx, int number, int moves)
        {
            var spec = ctx.Catalog.Get(number);
            var quick = new LevelSpec
            {
                Id = spec.Id, Mode = GameMode.Campaign, Number = number, Layout = LevelLayout.Parse(new[] { "000", "111", "222" }),
                Colours = 3, Moves = moves, Target = 10, Star2 = 20, Star3 = 30,
            };
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = quick, BoardSeed = 1 });
            s.Start();
            ctx.BeginRun(s);
            return s;
        }

        [Test]
        public void TutorialPowerUpsAreKeptOnTheFirstWinOnlyAndNeverLinger()
        {
            var store = new MemorySaveStorage();
            var ctx = TestUtil.Context(null, store);
            ctx.Data.HighestUnlocked = 61;
            var def = ctx.PowerUps.Get(PowerUpId.ColourBurst);
            int level = def.unlockLevel;

            // A failed attempt that can still be revived keeps its loan; leaving takes it back.
            Assert.IsNotNull(ctx.LendIntroPowerUpFor(level));
            var fail = TestUtil.Session(GameMode.Campaign, 1, 100000, 3, "000", "111", "222");
            ctx.BeginRun(fail);
            Play.Tap(fail, 0, 2);
            Assert.AreEqual(SessionState.Failed, fail.State);
            ctx.FinishRun(fail);
            Assert.AreEqual(1, ctx.PowerUpUse.Lent(def.id), "Out of Moves could still continue this attempt");
            ctx.AbandonRun(fail);
            Assert.AreEqual(0, ctx.PowerUpUse.Lent(def.id), "gone once the player leaves");

            // First win with the free use unused: it is kept, in the normal inventory.
            ctx.LendIntroPowerUpFor(level);
            var first = QuickLevel(ctx, level, 3);
            Play.Tap(first, 0, 2);
            first.Finish();
            var r = ctx.FinishRun(first);
            Assert.AreEqual(1, r.KeptPowerUps);
            Assert.AreSame(def, r.KeptPowerUp);
            Assert.AreEqual(1, ctx.Inventory.Owned(def.id), "transferred to the normal inventory");
            Assert.AreEqual(0, ctx.Inventory.Trials(def.id));
            Assert.AreEqual(0, ctx.PowerUpUse.Lent(def.id), "the loan itself is over");
            Assert.IsTrue(ctx.Rewards.TutorialPowerUpsSettled(def.id));

            // The next level: no trace of the loan on the bar.
            Assert.IsNull(ctx.LendIntroPowerUpFor(level + 1));
            Assert.AreEqual(1, ctx.PowerUpUse.Available(def.id), "only the one the player now owns");

            // Replays lend again, but keep nothing: no stockpiling.
            for (int replay = 0; replay < 3; replay++)
            {
                Assert.IsNotNull(ctx.LendIntroPowerUpFor(level));
                Assert.AreEqual(2, ctx.PowerUpUse.Available(def.id), "lent + owned during the attempt");
                var again = QuickLevel(ctx, level, 3);
                Play.Tap(again, 0, 2);
                again.Finish();
                var rr = ctx.FinishRun(again);
                Assert.AreEqual(0, rr.KeptPowerUps, "replay " + replay);
                Assert.AreEqual(1, ctx.Inventory.Owned(def.id));
                Assert.AreEqual(0, ctx.PowerUpUse.Lent(def.id));
            }

            // Saved: the reward stays settled after a restart, and the owned use is still there.
            Assert.IsTrue(ctx.Save());
            var reopened = TestUtil.Context(null, store);
            Assert.IsTrue(reopened.Rewards.TutorialPowerUpsSettled(def.id));
            Assert.AreEqual(1, reopened.Inventory.Owned(def.id));
            Assert.AreEqual(0, reopened.PowerUpUse.Lent(def.id));
        }

        [Test]
        public void PlayersWhoGotTheOldFreeTrialsGetNothingMore()
        {
            var ctx = TestUtil.Context();
            ctx.Data.HighestUnlocked = 11;
            var blast = ctx.PowerUps.Get(PowerUpId.Blast);
            Assert.AreEqual(2, ctx.Rewards.GrantTrial(blast, ctx.Inventory), "an older version's once-only grant");
            ctx.LendIntroPowerUpFor(blast.unlockLevel);
            var s = QuickLevel(ctx, blast.unlockLevel, 3);
            Play.Tap(s, 0, 2);
            s.Finish();
            Assert.AreEqual(0, ctx.FinishRun(s).KeptPowerUps);
            Assert.AreEqual(2, ctx.Inventory.Trials(blast.id), "their trials stay as they were");
            Assert.AreEqual(0, ctx.Inventory.Owned(blast.id));
        }

        // ------------------------------------------------------------------ The tutorial hand

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2400f)]
        [TestCase(1536f, 2048f)]
        [TestCase(1170f, 2532f)]
        public void TheHandsFingertipLandsOnTheTargetAtEveryAspectRatio(float width, float height)
        {
            float half = width / 2f;
            // A button in the top-right corner (the symbols button), one in the top-left (pause), one mid-board.
            var targets = new[] { (half - 94f, height / 2f - 120f), (-half + 94f, height / 2f - 120f), (0f, 0f), (half - 60f, -300f), (-half + 70f, 200f) };
            foreach (var (tx, ty) in targets)
            {
                HandPointer.Place(tx, ty, 170f, half, out float x, out float y, out bool flip);
                HandPointer.Tip(x, y, 170f, flip, out float tipX, out float tipY);
                Assert.AreEqual(tx, tipX, 0.01f, "fingertip x at " + tx + " on " + width + " x " + height);
                Assert.AreEqual(ty - HandPointer.Rest * 170f, tipY, 0.01f, "it rests just below and taps up onto the target");
                Assert.LessOrEqual(x + 85f, half, "the hand stays on screen");
                Assert.GreaterOrEqual(x - 85f, -half);
            }
        }
    }
}
