using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>Shared helpers for the obstacle and goal tests (rules in docs/MECHANICS.md).</summary>
    static class Play
    {
        /// <summary>A campaign level with a fixed starting board. Six colours so refills never matter to the assertions.</summary>
        public static PuzzleSession Level(string[] rows, string[] obstacles, int moves = 20, params GoalSpec[] goals)
        {
            return TestUtil.Session(GameMode.Campaign, moves, 1000000, TileColours.Max, rows, obstacles, goals);
        }

        /// <summary>A campaign level whose big groups earn special tiles up to the given tier.</summary>
        public static PuzzleSession Specials(string[] rows, string[] obstacles, SpecialTier tier = SpecialTier.Prisms, int moves = 20, params GoalSpec[] goals)
        {
            var setup = TestUtil.Setup(GameMode.Campaign, moves, 1000000, TileColours.Max, rows, obstacles, goals);
            setup.Level.Specials = tier;
            var s = new PuzzleSession(setup);
            s.Start();
            return s;
        }

        /// <summary>A campaign level with moss, with an optional cap on how far it may grow.</summary>
        public static PuzzleSession Moss(string[] rows, string[] obstacles, int mossCap = 0, int moves = 20, ulong seed = 12345, params GoalSpec[] goals)
        {
            var setup = TestUtil.Setup(GameMode.Campaign, moves, 1000000, TileColours.Max, rows, obstacles, goals);
            setup.Level.MossCap = mossCap;
            setup.BoardSeed = seed;
            var s = new PuzzleSession(setup);
            s.Start();
            return s;
        }

        public static MoveOutcome Tap(PuzzleSession s, int col, int row, int prismColour = -1)
        {
            var o = new MoveOutcome();
            Assert.IsTrue(s.TryPlay(new GridPos(col, row), o, prismColour), "tap at " + col + "," + row + " was refused");
            return o;
        }

        public static int CountPieces(Board b, CellType type)
        {
            int n = 0;
            for (int i = 0; i < b.CellCount; i++) if (b.GetByIndex(i).Type == type) n++;
            return n;
        }

        public static MoveOutcome Use(PuzzleSession s, PowerUpRequest r)
        {
            var o = new MoveOutcome();
            Assert.IsTrue(s.TryApplyPowerUp(r, o, out var failure), r.Id + " was refused: " + failure);
            return o;
        }

        public static PowerUpFailure Refused(PuzzleSession s, PowerUpRequest r)
        {
            var o = new MoveOutcome();
            Assert.IsFalse(s.TryApplyPowerUp(r, o, out var failure), r.Id + " should have been refused");
            return failure;
        }

        public static PowerUpRequest At(PowerUpId id, int col, int row) => new PowerUpRequest { Id = id, A = new GridPos(col, row) };

        public static PowerUpRequest Swap(int c1, int r1, int c2, int r2) => new PowerUpRequest { Id = PowerUpId.Swap, A = new GridPos(c1, r1), B = new GridPos(c2, r2) };

        public static PowerUpRequest Shift(int col, int row, int colour) => new PowerUpRequest { Id = PowerUpId.ColourShift, A = new GridPos(col, row), Colour = colour };

        public static GridPos Find(Board b, int tileId)
        {
            for (int r = 0; r < b.Height; r++)
                for (int c = 0; c < b.Width; c++)
                    if (b[c, r].Type != CellType.Empty && b[c, r].Type != CellType.Void && b[c, r].Id == tileId) return new GridPos(c, r);
            Assert.Fail("tile " + tileId + " is not on the board");
            return default;
        }
    }

    public class IceTests
    {
        static readonly GridPos Centre = new GridPos(1, 1);

        [Test]
        public void FrozenTileDoesNotJoinGroupsAndCannotBeTapped()
        {
            var b = Board.FromRows(new[] { "00" }, new[] { "i." });
            Assert.AreEqual(0, new GroupFinder().GroupSize(b, new GridPos(0, 0)), "a frozen tile starts no group");
            Assert.AreEqual(1, new GroupFinder().GroupSize(b, new GridPos(1, 0)), "a frozen tile joins no group");
            Assert.IsFalse(GroupFinder.HasLegalMove(b));

            var s = Play.Level(new[] { "000" }, new[] { "i.." });
            Assert.IsFalse(s.TryPlay(new GridPos(0, 0), new MoveOutcome()), "tapping a frozen tile does nothing");
            Assert.AreEqual(0, s.MovesMade);
            Assert.AreEqual(2, new GroupFinder().GroupSize(s.Board, new GridPos(1, 0)));
        }

        [Test]
        public void SeveralClearedNeighboursRemoveOnlyOneLayer()
        {
            var s = Play.Level(new[] { "000", "010", "000" }, new[] { "...", ".I.", "..." });
            int id = s.Board[Centre].Id;
            var o = Play.Tap(s, 0, 0);

            Assert.AreEqual(8, o.GroupSize);
            Assert.AreEqual(8, o.Cleared.Count);
            Assert.AreEqual(1, o.ObstacleHits.Count, "four cleared neighbours, one layer");
            var hit = o.ObstacleHits[0];
            Assert.AreEqual(ObstacleKind.Ice, hit.Kind);
            Assert.AreEqual(2, hit.StrengthBefore);
            Assert.AreEqual(1, hit.StrengthAfter);
            Assert.AreEqual(HitCause.AdjacentClear, hit.Cause);
            Assert.IsFalse(hit.Freed);

            var t = s.Board[Centre];
            Assert.AreEqual(id, t.Id, "a frozen tile never moves");
            Assert.AreEqual(ObstacleKind.Ice, t.Obstacle);
            Assert.AreEqual(1, t.Strength);
            Assert.AreEqual(s.Scoring.GroupPoints(8) + 100, o.ScoreGained, "group points plus one ice layer");
            Assert.AreEqual(o.ScoreGained, s.Score);
        }

        [Test]
        public void ThickIceNeedsTwoActionsAndTheFreedTileFallsInTheSameAction()
        {
            var s = Play.Level(new[] { "000", "010", "000" }, new[] { "...", ".I.", "..." }, 20, GoalSpec.FreeIce(1), GoalSpec.Score(1));
            int id = s.Board[Centre].Id;
            Play.Tap(s, 0, 0);
            Assert.AreEqual(0, s.Goals.Progress(0), "one layer left: not freed yet");

            // A Blast in the corner reaches the frozen tile (area hit) and removes the tile below it.
            var o = Play.Use(s, Play.At(PowerUpId.Blast, 0, 0));
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(HitCause.Area, o.ObstacleHits[0].Cause);
            Assert.IsTrue(o.ObstacleHits[0].Freed);
            Assert.IsTrue(o.Falls.Any(f => f.TileId == id && f.From == Centre && f.To == new GridPos(1, 0)), "the freed tile falls into the gap below it");
            var freed = s.Board[Play.Find(s.Board, id)];
            Assert.AreEqual(ObstacleKind.None, freed.Obstacle);
            Assert.AreEqual(0, freed.Strength);
            Assert.AreEqual(1, s.Goals.Progress(0));
            Assert.AreEqual(SessionState.Won, s.State, "every goal met");
        }

        [Test]
        public void FreedByAnAdjacentGroupTheTileFallsAndCountsForTheGoal()
        {
            var s = Play.Level(new[] { "213", "003" }, new[] { ".i.", "..." }, 20, GoalSpec.FreeIce(2));
            int id = s.Board[1, 1].Id;
            var o = Play.Tap(s, 0, 0);

            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.IsTrue(o.ObstacleHits[0].Freed);
            Assert.IsTrue(o.GoalsChanged);
            Assert.AreEqual(1, s.Goals.Progress(0));
            Assert.IsFalse(o.Shuffled, "the untouched pair of 3s keeps a legal move");
            Assert.AreEqual(id, s.Board[1, 0].Id, "freed in this action, it fell in this action");
            Assert.AreEqual(ObstacleKind.None, s.Board[1, 0].Obstacle);
            Assert.AreEqual(SessionState.Playing, s.State);
        }

        [TestCase('I', 1, Description = "thick ice: one layer comes off, the tile stays frozen")]
        [TestCase('1', 0, Description = "chain: adjacent clears do nothing")]
        public void ObstaclesHoldUpTheTilesAboveAndTheSectionBelowRefillsFromBeneath(char obstacle, int expectedHits)
        {
            var s = Play.Level(new[] { "224", "134", "005" }, new[] { "...", obstacle + "..", "..." });
            int above = s.Board[0, 2].Id;
            int held = s.Board[0, 1].Id;
            var o = Play.Tap(s, 0, 0);

            Assert.AreEqual(expectedHits, o.ObstacleHits.Count);
            Assert.AreEqual(held, s.Board[0, 1].Id);
            Assert.AreNotEqual(ObstacleKind.None, s.Board[0, 1].Obstacle, "still an obstacle");
            Assert.AreEqual(above, s.Board[0, 2].Id, "the tile above the obstacle did not fall");
            Assert.IsFalse(o.Falls.Any(f => f.TileId == above));
            var spawn = o.Spawns.Single(sp => sp.At == new GridPos(0, 0));
            Assert.IsFalse(spawn.FromBoardTop, "the gap under the obstacle refills from beneath it");
            Assert.AreEqual(new GridPos(0, 0), spawn.SectionTop);
            Assert.IsTrue(o.Spawns.Any(sp => sp.At == new GridPos(1, 2) && sp.FromBoardTop), "the open column refills from the top");
            Assert.IsFalse(o.Shuffled);
        }

        [Test]
        public void FrozenTilesCannotBeSwappedOrRecoloured()
        {
            var s = Play.Level(new[] { "0011", "2323" }, new[] { "....", ".I.." });
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Swap(1, 0, 0, 0)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Swap(0, 0, 1, 0)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Shift(1, 0, 0)));
            Assert.AreEqual(0, s.PowerUpsUsed);
        }

        [Test]
        public void TileBreakerRemovesEveryLayerAndTheTileStays()
        {
            var s = Play.Level(new[] { "0011", "2323" }, new[] { "....", ".I.." }, 20, GoalSpec.FreeIce(3));
            int id = s.Board[1, 0].Id;
            var o = Play.Use(s, Play.At(PowerUpId.TileBreaker, 1, 0));

            Assert.AreEqual(0, o.Cleared.Count, "the tile is freed, not removed");
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(HitCause.Strong, o.ObstacleHits[0].Cause);
            Assert.AreEqual(2, o.ObstacleHits[0].StrengthBefore);
            Assert.AreEqual(0, o.ObstacleHits[0].StrengthAfter);
            Assert.AreEqual(id, s.Board[1, 0].Id);
            Assert.AreEqual(ObstacleKind.None, s.Board[1, 0].Obstacle);
            Assert.AreEqual(200, o.ScoreGained, "two layers at 100");
            Assert.AreEqual(1, s.Goals.Progress(0));
        }

        [Test]
        public void BlastOnAFrozenTileTakesOneLayerEvenThoughItsNeighboursGoToo()
        {
            var s = Play.Level(new[] { "000", "010", "000" }, new[] { "...", ".I.", "..." });
            var o = Play.Use(s, Play.At(PowerUpId.Blast, 1, 1));

            Assert.AreEqual(8, o.Cleared.Count);
            Assert.AreEqual(1, o.ObstacleHits.Count, "area hit and adjacent clears share the one-step-per-action limit");
            Assert.AreEqual(HitCause.Area, o.ObstacleHits[0].Cause);
            Assert.AreEqual(1, s.Board[Centre].Strength);
            Assert.AreEqual(s.Scoring.PowerUpPoints(8) + 100, o.ScoreGained);
        }

        [Test]
        public void ColourBurstHitsIceOfItsColourOnly()
        {
            // Frozen 1 at (2,1), frozen 0 at (3,0); the only free 0 is at (0,1), away from both. The 4s keep a legal move.
            var s = Play.Level(new[] { "02144", "31205" }, new[] { "..i..", "...i." });
            var o = Play.Use(s, Play.At(PowerUpId.ColourBurst, 0, 1));

            Assert.AreEqual(1, o.Cleared.Count);
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(new GridPos(3, 0), o.ObstacleHits[0].Pos);
            Assert.AreEqual(HitCause.Area, o.ObstacleHits[0].Cause);
            Assert.AreEqual(ObstacleKind.None, s.Board[3, 0].Obstacle);
            Assert.AreEqual(ObstacleKind.Ice, s.Board[2, 1].Obstacle, "another colour's ice is untouched");
            Assert.AreEqual(s.Scoring.PowerUpPoints(1) + 100, o.ScoreGained);
        }

        [Test]
        public void ColourBurstNextToItsOwnFrozenTileStillTakesOneLayer()
        {
            var s = Play.Level(new[] { "000", "455" }, new[] { ".I.", "..." });
            var o = Play.Use(s, Play.At(PowerUpId.ColourBurst, 0, 1));
            Assert.AreEqual(2, o.Cleared.Count);
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(1, s.Board[1, 1].Strength);
        }

        [Test]
        public void ColourBurstCanTargetAFrozenTile()
        {
            var s = Play.Level(new[] { "0011", "2323" }, new[] { "....", ".I.." });
            var o = Play.Use(s, Play.At(PowerUpId.ColourBurst, 1, 0));
            Assert.AreEqual(3, o.GroupColour);
            Assert.AreEqual(1, s.Board[1, 0].Strength, "the targeted frozen tile is hit like the rest of its colour");
        }
    }

    public class ChainTests
    {
        [TestCase(0, Description = "tap a free tile of the group")]
        [TestCase(1, Description = "tap the chained tile itself")]
        public void ChainedTileJoinsItsGroupStaysAndLosesALink(int tapColumn)
        {
            var s = Play.Level(new[] { "111", "000" }, new[] { "...", ".2." }, 20, GoalSpec.Collect(0, 10), GoalSpec.BreakChains(1));
            int id = s.Board[1, 0].Id;
            var o = Play.Tap(s, tapColumn, 0);

            Assert.AreEqual(3, o.GroupSize, "the chained tile counts towards the group");
            Assert.AreEqual(2, o.Cleared.Count);
            Assert.IsFalse(o.Cleared.Any(c => c.TileId == id));
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(HitCause.GroupClear, o.ObstacleHits[0].Cause);
            Assert.AreEqual(2, o.ObstacleHits[0].StrengthBefore);
            Assert.AreEqual(1, o.ObstacleHits[0].StrengthAfter);
            Assert.AreEqual(id, s.Board[1, 0].Id, "chained tiles never move");
            Assert.AreEqual(ObstacleKind.Chain, s.Board[1, 0].Obstacle);
            Assert.AreEqual(s.Scoring.GroupPoints(3) + 150, o.ScoreGained);
            Assert.AreEqual(2, s.TilesCleared);
            Assert.AreEqual(2, s.Goals.Progress(0), "only removed tiles are collected");
            Assert.AreEqual(0, s.Goals.Progress(1), "one link left");
        }

        [Test]
        public void AGroupOfOnlyChainedTilesIsALegalMove()
        {
            var b = Board.FromRows(new[] { "00" }, new[] { "13" });
            Assert.IsTrue(GroupFinder.HasLegalMove(b));
            Assert.AreEqual(2, new GroupFinder().GroupSize(b, new GridPos(0, 0)));

            var s = Play.Level(new[] { "00", "55" }, new[] { "13", ".." }, 20, GoalSpec.BreakChains(5));
            int moves = s.MovesLeft;
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(0, o.Cleared.Count, "nothing leaves the board");
            Assert.AreEqual(2, o.ObstacleHits.Count);
            Assert.AreEqual(moves - 1, s.MovesLeft, "it costs a move like any group");
            Assert.AreEqual(s.Scoring.GroupPoints(2) + 2 * 150, o.ScoreGained);
            Assert.AreEqual(ObstacleKind.None, s.Board[0, 1].Obstacle);
            Assert.AreEqual(2, s.Board[1, 1].Strength);
            Assert.AreEqual(1, s.Goals.Progress(0));
        }

        [Test]
        public void AdjacentClearsDoNotAffectChains()
        {
            var s = Play.Level(new[] { "001", "443" }, new[] { "..2", "..." });
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(0, o.ObstacleHits.Count);
            Assert.AreEqual(2, s.Board[2, 1].Strength);
            Assert.AreEqual(s.Scoring.GroupPoints(2), o.ScoreGained);
        }

        [Test]
        public void ChainedTilesCannotBeSwappedButCanBeRecoloured()
        {
            var s = Play.Level(new[] { "0011", "2233" }, new[] { "1...", "...." }, 20, GoalSpec.BreakChains(1));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Swap(0, 1, 0, 0)));

            var o = Play.Use(s, Play.Shift(0, 1, 2));
            Assert.AreEqual(1, o.Recolours.Count);
            Assert.AreEqual(2, s.Board[0, 1].Colour);
            Assert.AreEqual(ObstacleKind.Chain, s.Board[0, 1].Obstacle, "the chain stays");
            Assert.AreEqual(1, s.Board[0, 1].Strength);

            // Now it belongs to the 2s below it: clearing them frees it.
            Assert.AreEqual(3, new GroupFinder().GroupSize(s.Board, new GridPos(1, 0)));
            var tap = Play.Tap(s, 1, 0);
            Assert.AreEqual(1, tap.ObstacleHits.Count);
            Assert.IsTrue(tap.ObstacleHits[0].Freed);
            Assert.AreEqual(SessionState.Won, s.State);
        }

        [Test]
        public void TileBreakerRemovesEveryLink()
        {
            var s = Play.Level(new[] { "0011", "2233" }, new[] { "3...", "...." });
            int id = s.Board[0, 1].Id;
            var o = Play.Use(s, Play.At(PowerUpId.TileBreaker, 0, 1));
            Assert.AreEqual(3, o.ObstacleHits[0].StrengthBefore);
            Assert.AreEqual(0, o.ObstacleHits[0].StrengthAfter);
            Assert.AreEqual(3 * 150, o.ScoreGained);
            Assert.AreEqual(id, s.Board[0, 1].Id);
            Assert.AreEqual(ObstacleKind.None, s.Board[0, 1].Obstacle);
        }

        [Test]
        public void BlastAndColourBurstTakeOneLink()
        {
            var s = Play.Level(new[] { "000", "000", "000" }, new[] { "...", ".3.", "..." });
            var o = Play.Use(s, Play.At(PowerUpId.Blast, 1, 1));
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(2, s.Board[1, 1].Strength);
            Assert.AreEqual(s.Scoring.PowerUpPoints(8) + 150, o.ScoreGained);

            var t = Play.Level(new[] { "0011", "2233" }, new[] { "2...", "...." });
            var burst = Play.Use(t, Play.At(PowerUpId.ColourBurst, 1, 1));
            Assert.AreEqual(1, burst.Cleared.Count);
            Assert.AreEqual(1, burst.ObstacleHits.Count, "a chain of the burst colour loses one link");
            Assert.AreEqual(1, t.Board[0, 1].Strength);
        }
    }

    public class ObstacleBoardTests
    {
        [Test]
        public void RecoveryReshufflesOnlyFreeTilesAndKeepsObstaclesInPlace()
        {
            string[] rows = { "0123", "1230", "2301", "3012" };
            string[] obstacles = { "...i", "....", "..2.", "I..." };
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var b = Board.FromRows(rows, obstacles);
                Assert.IsFalse(GroupFinder.HasLegalMove(b));
                var before = b.Clone();
                var moves = new List<TileMove>();
                var result = BoardRecovery.EnsurePlayable(b, new Rng(seed), moves);

                Assert.AreEqual(RecoveryResult.Shuffled, result);
                Assert.IsTrue(GroupFinder.HasLegalMove(b));
                var freeBefore = new List<int>();
                var freeAfter = new List<int>();
                for (int i = 0; i < b.CellCount; i++)
                {
                    var was = before.GetByIndex(i);
                    var now = b.GetByIndex(i);
                    if (TileRules.HasObstacle(was)) Assert.AreEqual(was, now, "obstacle moved or changed (seed " + seed + ")");
                    else freeBefore.Add(was.Colour);
                    if (TileRules.IsFree(now)) freeAfter.Add(now.Colour);
                }
                freeBefore.Sort();
                freeAfter.Sort();
                CollectionAssert.AreEqual(freeBefore, freeAfter, "the same free tiles, rearranged");
                foreach (var m in moves)
                    Assert.IsTrue(TileRules.IsFree(b[m.To]), "only free tiles move");
            }
        }

        [Test]
        public void ObstacleNotationRoundTrips()
        {
            string[] rows = { "R..", "#.B", "..x" };
            string[] obstacles = { "i.3", "..I", "1.." };
            var layout = LevelLayout.Parse(rows);
            layout.ApplyObstacleRows(obstacles);
            Assert.IsTrue(layout.Validate(out var error), error);
            CollectionAssert.AreEqual(obstacles, layout.ToObstacleRows());
            CollectionAssert.AreEqual(rows, layout.ToRows(), "obstacles do not disturb the layout notation");
            Assert.AreEqual(2, layout.ObstacleCount(ObstacleKind.Ice));
            Assert.AreEqual(2, layout.ObstacleCount(ObstacleKind.Chain));
            Assert.AreEqual(ObstacleKind.Ice, layout.ObstacleAt(2, 1));
            Assert.AreEqual(2, layout.StrengthAt(2, 1));
            CollectionAssert.AreEqual(obstacles, layout.Clone().ToObstacleRows());

            var board = Board.FromRows(new[] { "000", "111", "222" }, obstacles);
            Assert.AreEqual(string.Join("\n", obstacles), board.ToObstacleString());
            Assert.AreEqual(Board.FromRows("000", "111", "222").ToDebugString(), board.ToDebugString(), "the debug string shows colours only");
        }

        [Test]
        public void LayoutValidationRejectsBadObstacles()
        {
            var onStone = LevelLayout.Parse(new[] { "...", ".#.", "..." });
            onStone.SetObstacle(1, 1, ObstacleKind.Ice, 1);
            Assert.IsFalse(onStone.Validate(out _), "an obstacle needs an open cell");

            var thick = LevelLayout.Open(3, 3);
            thick.SetObstacle(0, 0, ObstacleKind.Ice, 3);
            Assert.IsFalse(thick.Validate(out _), "ice has at most two layers");

            var links = LevelLayout.Open(3, 3);
            links.ApplyObstacleRows(new[] { "...", "...", "4.." });
            Assert.IsFalse(links.Validate(out _), "chains have at most three links");

            Assert.Throws<System.ArgumentException>(() => LevelLayout.Open(3, 3).ApplyObstacleRows(new[] { "...", "...", "z.." }));
            Assert.Throws<System.ArgumentException>(() => LevelLayout.Open(3, 3).ApplyObstacleRows(new[] { "...", "..." }));

            var cleared = LevelLayout.Open(3, 3);
            cleared.SetObstacle(1, 1, ObstacleKind.Chain, 2);
            cleared.SetType(1, 1, CellType.Stone);
            Assert.AreEqual(ObstacleKind.None, cleared.ObstacleAt(1, 1), "turning a cell to stone removes its obstacle");
        }

        [Test]
        public void ObstaclesDoNotChangeTheGeneratedColours()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var plain = LevelLayout.Parse(new[] { "......", "..#...", "......", "......", "...#..", "......" });
                var frozen = plain.Clone();
                frozen.ApplyObstacleRows(new[] { "i.....", "...I..", ".2....", "....3.", "......", "1....i" });
                var a = BoardGenerator.Generate(plain, new TileSource(new Rng(seed), 5, null, 100));
                var b = BoardGenerator.Generate(frozen, new TileSource(new Rng(seed), 5, null, 100));
                Assert.AreEqual(a.ToDebugString(), b.ToDebugString());
                for (int r = 0; r < b.Height; r++)
                    for (int c = 0; c < b.Width; c++)
                    {
                        Assert.AreEqual(frozen.ObstacleAt(c, r), b[c, r].Obstacle);
                        Assert.AreEqual(frozen.StrengthAt(c, r), b[c, r].Strength);
                    }
            }
        }

        [Test]
        public void ObstacleStepPointsComeFromTheScoringRules()
        {
            var rules = new ScoringRules();
            Assert.AreEqual(100, rules.ObstacleStepPoints(ObstacleKind.Ice));
            Assert.AreEqual(150, rules.ObstacleStepPoints(ObstacleKind.Chain));
            Assert.AreEqual(0, rules.ObstacleStepPoints(ObstacleKind.None));
            rules.iceLayerPoints = 7;
            Assert.AreEqual(7, rules.ObstacleStepPoints(ObstacleKind.Ice));

            var layout = LevelLayout.Parse(new[] { "000", "010", "000" });
            layout.ApplyObstacleRows(new[] { "...", ".I.", "..." });
            var spec = new LevelSpec { Id = "points", Mode = GameMode.Campaign, Number = 1, Layout = layout, Colours = 6, Moves = 10, Target = 1000000 };
            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = rules, BoardSeed = 3 });
            s.Start();
            var o = Play.Tap(s, 0, 0);
            Assert.AreEqual(rules.GroupPoints(8) + 7, o.ScoreGained, "the session uses the configured value");
        }

        [Test]
        public void TileRulesFollowTheObstacleDefinitions()
        {
            var free = new Tile { Type = CellType.Colour, Colour = 1 };
            var ice = new Tile { Type = CellType.Colour, Colour = 1, Obstacle = ObstacleKind.Ice, Strength = 1 };
            var chain = new Tile { Type = CellType.Colour, Colour = 1, Obstacle = ObstacleKind.Chain, Strength = 2 };
            var stone = new Tile { Type = CellType.Stone };

            Assert.IsTrue(TileRules.JoinsGroups(free) && !TileRules.JoinsGroups(ice) && TileRules.JoinsGroups(chain) && !TileRules.JoinsGroups(stone));
            Assert.IsTrue(TileRules.Falls(free) && !TileRules.Falls(ice) && !TileRules.Falls(chain) && !TileRules.Falls(stone));
            Assert.IsTrue(!TileRules.IsGravityBarrier(free) && TileRules.IsGravityBarrier(ice) && TileRules.IsGravityBarrier(chain) && TileRules.IsGravityBarrier(stone));
            Assert.IsTrue(TileRules.CanShuffle(free) && !TileRules.CanShuffle(ice) && !TileRules.CanShuffle(chain) && !TileRules.CanShuffle(stone));
            Assert.IsTrue(TileRules.CanSwap(free) && !TileRules.CanSwap(ice) && !TileRules.CanSwap(chain));
            Assert.IsTrue(TileRules.CanRecolour(free) && !TileRules.CanRecolour(ice) && TileRules.CanRecolour(chain) && !TileRules.CanRecolour(stone));
            Assert.IsTrue(TileRules.HitByAdjacentClear(ice) && !TileRules.HitByAdjacentClear(chain) && !TileRules.HitByAdjacentClear(free));
            Assert.IsTrue(TileRules.StaysWhenItsGroupClears(chain) && !TileRules.StaysWhenItsGroupClears(ice) && !TileRules.StaysWhenItsGroupClears(free));
            Assert.IsNull(Obstacles.Get(ObstacleKind.None));
            Assert.AreEqual(2, Obstacles.Get(ObstacleKind.Ice).MaxStrength);
            Assert.AreEqual(3, Obstacles.Get(ObstacleKind.Chain).MaxStrength);
        }
    }

    public class CampaignOnlyMechanicsTests
    {
        [Test]
        public void ChallengeModesAndEndlessHaveNoObstaclesOrGoals()
        {
            var ctx = TestUtil.Context();
            var setups = new List<SessionSetup> { ctx.CreateEndlessRun(), ctx.CreatePracticeRun(LevelLayout.Open(6, 6), 4, 1) };
            for (int d = 0; d < 30; d++) setups.Add(ctx.CreateDailyRun(TestUtil.Today.AddDays(d).ToString("yyyy-MM-dd")));
            foreach (var week in new[] { "2026-W43", "2026-W44", "2026-W52", "2027-W01" }) setups.Add(ctx.CreateWeeklyRun(week));
            foreach (var setup in setups)
            {
                Assert.IsFalse(setup.Level.Layout.HasObstacles, setup.Mode + " " + setup.Level.Id);
                Assert.IsTrue(setup.Level.Goals == null || setup.Level.Goals.Length == 0, setup.Mode + " " + setup.Level.Id);
                var s = new PuzzleSession(setup);
                s.Start();
                Assert.AreEqual(1, s.Goals.Count);
                Assert.AreEqual(GoalKind.Score, s.Goals.Goal(0).kind);
            }
        }

        [TestCase(GameMode.Endless)]
        [TestCase(GameMode.Daily)]
        [TestCase(GameMode.WeeklyRush)]
        [TestCase(GameMode.Practice)]
        public void OtherModesRefuseCampaignMechanics(GameMode mode)
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                TestUtil.Session(mode, 20, 1000, 4, new[] { "0011", "2233" }, new[] { "i...", "...." }));
            Assert.Throws<System.InvalidOperationException>(() =>
                TestUtil.Session(mode, 20, 1000, 4, new[] { "0011", "2233" }, null, GoalSpec.Collect(0, 5)));
        }

        [Test]
        public void TheOpeningLevelsHaveNoMechanics()
        {
            var ctx = TestUtil.Context();
            for (int n = 1; n < ctx.Catalog.Schedule.iceFromLevel; n++)
            {
                var spec = ctx.Catalog.Get(n);
                Assert.IsFalse(spec.Layout.HasObstacles || spec.Layout.HasPieces, "level " + n);
                Assert.IsNull(spec.Goals, "level " + n);
                Assert.AreEqual(SpecialTier.None, spec.Specials, "level " + n);
            }
        }

        [Test]
        public void AHandMadeLevelCanListObstaclesAndGoals()
        {
            var cfg = GameConfigData.CreateDefault();
            var ov = new LevelOverride
            {
                level = 5, moves = 18, colours = 4, target = 1500,
                layout = new[] { "......", "......", "......", "......", "......", "......" },
                obstacles = new[] { "......", ".i..I.", "......", "..12..", "......", "......" },
                goals = new[] { GoalSpec.FreeIce(2), GoalSpec.Collect(1, 20) },
            };
            var list = new List<LevelOverride>(cfg.Campaign.overrides ?? new LevelOverride[0]);
            list.RemoveAll(o => o.level == 5);
            list.Add(ov);
            cfg.Campaign.overrides = list.ToArray();
            var spec = new CampaignCatalog(cfg.Campaign, cfg.Scoring).Get(5);

            Assert.AreEqual(2, spec.Layout.ObstacleCount(ObstacleKind.Ice));
            Assert.AreEqual(2, spec.Layout.ObstacleCount(ObstacleKind.Chain));
            Assert.AreEqual(2, spec.EffectiveGoals().Length);
            Assert.AreEqual(GoalKind.FreeIce, spec.Goals[0].kind);
            Assert.AreEqual(1, spec.Goals[1].colour);
            Assert.AreNotSame(ov.goals[0], spec.Goals[0], "the catalog copies goals so a run cannot change the config");

            var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = cfg.Scoring, BoardSeed = 7 });
            s.Start();
            Assert.AreEqual(ObstacleKind.Ice, s.Board[1, 4].Obstacle);
            Assert.AreEqual(2, s.Board[4, 4].Strength);
            Assert.AreEqual(ObstacleKind.Chain, s.Board[2, 2].Obstacle);
            Assert.AreEqual(2, s.Goals.Count);
        }
    }

    /// <summary>Long randomised games on boards full of obstacles, checking the rules after every action.</summary>
    public class ObstacleLongPlayTests
    {
        [Test]
        public void RandomPlayWithObstaclesKeepsEveryRule()
        {
            int totalIceFreed = 0, totalChainsFreed = 0, totalActions = 0, totalShuffles = 0;
            var scoring = new ScoringRules();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var rng = new Rng(seed);
                var layout = LevelLayout.Open(7, 7);
                if (seed % 3 == 0) layout.SetType(3, 3, CellType.Stone);
                if (seed % 4 == 0) layout.SetType(0, 6, CellType.Void);
                int ice = 0, chains = 0;
                for (int k = 0; k < (seed % 3 == 2 ? 24 : 14); k++)
                {
                    int c = rng.Next(7), r = rng.Next(7);
                    if (!layout.IsOpen(c, r) || layout.ObstacleAt(c, r) != ObstacleKind.None) continue;
                    if (rng.Next(2) == 0) { layout.SetObstacle(c, r, ObstacleKind.Ice, 1 + rng.Next(2)); ice++; }
                    else { layout.SetObstacle(c, r, ObstacleKind.Chain, 1 + rng.Next(3)); chains++; }
                }
                Assert.IsTrue(layout.Validate(out var error), error);
                var spec = new LevelSpec
                {
                    Id = "long" + seed, Mode = GameMode.Campaign, Number = 1, Layout = layout, Colours = 4 + (int)(seed % 3),
                    Moves = 45, Target = 1000000, Star2 = 2000000, Star3 = 3000000, LeftoverBonusPerMove = 50,
                    Goals = new[] { GoalSpec.FreeIce(ice), GoalSpec.BreakChains(chains), GoalSpec.Collect(0, 30) },
                };
                var s = new PuzzleSession(new SessionSetup { Mode = GameMode.Campaign, Level = spec, Scoring = scoring, BoardSeed = seed * 31 });
                s.Start();

                int iceFreed = 0, chainsFreed = 0, collected = 0;
                var finder = new GroupFinder();
                var groups = new List<GroupInfo>();
                var o = new MoveOutcome();
                for (int step = 0; step < 200 && s.AcceptsMoves; step++)
                {
                    var held = new Dictionary<int, GridPos>();
                    for (int r = 0; r < 7; r++)
                        for (int c = 0; c < 7; c++)
                            if (TileRules.HasObstacle(s.Board[c, r])) held[s.Board[c, r].Id] = new GridPos(c, r);
                    int scoreBefore = s.Score;

                    bool acted;
                    int basePoints;
                    if (rng.Next(5) == 0)
                    {
                        acted = TryRandomPowerUp(s, rng, o);
                        int colourTiles = o.Cleared.Count(t => !t.WasStone);
                        basePoints = scoring.PowerUpPoints(colourTiles);
                    }
                    else
                    {
                        Assert.IsTrue(Bots.Choose(s.Board, BotKind.Casual, rng, finder, groups, out var pick), "a playing board always has a move");
                        acted = s.TryPlay(pick, o);
                        Assert.IsTrue(acted);
                        basePoints = scoring.GroupPoints(o.GroupSize);
                    }
                    if (!acted) continue;
                    totalActions++;
                    if (o.Shuffled) totalShuffles++;

                    // One step per obstacle per action; strong hits take everything.
                    Assert.AreEqual(o.ObstacleHits.Count, o.ObstacleHits.Select(h => h.TileId).Distinct().Count(), "an obstacle was hit twice in one action");
                    int obstaclePoints = 0;
                    foreach (var h in o.ObstacleHits)
                    {
                        Assert.AreEqual(h.Cause == HitCause.Strong ? 0 : h.StrengthBefore - 1, h.StrengthAfter);
                        Assert.IsTrue(h.Kind == ObstacleKind.Chain || h.Cause != HitCause.GroupClear, "only chains are hit by their own group");
                        Assert.IsTrue(h.Kind == ObstacleKind.Ice || h.Cause != HitCause.AdjacentClear, "only ice reacts to adjacent clears");
                        obstaclePoints += scoring.ObstacleStepPoints(h.Kind) * (h.StrengthBefore - h.StrengthAfter);
                        if (h.Freed && h.Kind == ObstacleKind.Ice) iceFreed++;
                        if (h.Freed && h.Kind == ObstacleKind.Chain) chainsFreed++;
                    }
                    Assert.AreEqual(basePoints + obstaclePoints, o.ScoreGained, "points = action points + obstacle steps");
                    Assert.AreEqual(scoreBefore + o.ScoreGained, s.Score);
                    collected += o.Cleared.Count(t => !t.WasStone && t.Colour == 0);

                    // Obstacles never move while they hold, and stay within their strengths.
                    for (int r = 0; r < 7; r++)
                        for (int c = 0; c < 7; c++)
                        {
                            var t = s.Board[c, r];
                            if (!TileRules.HasObstacle(t)) continue;
                            Assert.IsTrue(t.Strength >= 1 && t.Strength <= Obstacles.Get(t.Obstacle).MaxStrength);
                            Assert.IsTrue(held.TryGetValue(t.Id, out var was), "an obstacle appeared from nowhere");
                            Assert.AreEqual(was, new GridPos(c, r), "an obstacle moved");
                        }
                    if (!o.RecoveryFailed) TestUtil.AssertBoardInvariants(s.Board, layout);

                    Assert.AreEqual(iceFreed, s.Goals.Progress(0));
                    Assert.AreEqual(chainsFreed, s.Goals.Progress(1));
                    Assert.AreEqual(collected, s.Goals.Progress(2));
                    if (s.Goals.AllComplete) Assert.AreEqual(SessionState.Won, s.State, "complete goals win at once");
                }
                totalIceFreed += iceFreed;
                totalChainsFreed += chainsFreed;
            }
            Assert.Greater(totalActions, 1000, "the test exercised a lot of play");
            Assert.Greater(totalIceFreed, 100);
            Assert.Greater(totalChainsFreed, 100);
            TestContext.WriteLine("actions " + totalActions + ", ice freed " + totalIceFreed + ", chains freed " + totalChainsFreed + ", shuffles " + totalShuffles);
        }

        static bool TryRandomPowerUp(PuzzleSession s, Rng rng, MoveOutcome o)
        {
            var b = s.Board;
            var a = new GridPos(rng.Next(b.Width), rng.Next(b.Height));
            PowerUpRequest req;
            switch (rng.Next(5))
            {
                case 0: req = Play.At(PowerUpId.Blast, a.Col, a.Row); break;
                case 1: req = Play.At(PowerUpId.TileBreaker, a.Col, a.Row); break;
                case 2: req = Play.At(PowerUpId.ColourBurst, a.Col, a.Row); break;
                case 3: req = Play.Shift(a.Col, a.Row, rng.Next(s.ColourCount)); break;
                default:
                    int d = rng.Next(4);
                    req = Play.Swap(a.Col, a.Row, a.Col + Board.DCol[d], a.Row + Board.DRow[d]);
                    break;
            }
            return s.TryApplyPowerUp(req, o, out _);
        }
    }
}
