using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>Treasure (docs/MECHANICS.md §2.4).</summary>
    public class TreasureTests
    {
        [Test]
        public void TreasureFallsAndIsCollectedAtTheBottom()
        {
            var s = Play.Level(new[] { "121", "2$3", "401", "502" }, null, 20, GoalSpec.CollectTreasure(1));
            int id = s.Board[1, 2].Id;
            var o = Play.Tap(s, 1, 0);
            Assert.AreEqual(1, o.Treasures.Count);
            Assert.AreEqual(id, o.Treasures[0].TileId);
            Assert.AreEqual(new GridPos(1, 2), o.Treasures[0].From);
            Assert.AreEqual(new GridPos(1, 0), o.Treasures[0].At);
            Assert.IsTrue(o.Falls.Any(f => f.TileId == id && f.To == new GridPos(1, 0)), "it is seen falling to the floor");
            Assert.AreEqual(0, Play.CountPieces(s.Board, CellType.Treasure));
            Assert.AreEqual(40 + 500, o.ScoreGained);
            Assert.AreEqual(1, s.Goals.Progress(0));
            Assert.AreEqual(SessionState.Won, s.State);
            TestUtil.AssertBoardInvariants(s.Board, s.Level.Layout);
        }

        [Test]
        public void ATreasureRestingOnAStoneIsCollected()
        {
            var s = Play.Level(new[] { "121", "2$3", "000", "1#2" }, null);
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(1, o.Treasures.Count);
            Assert.AreEqual(new GridPos(1, 1), o.Treasures[0].At, "on top of the stone, never below it");
            Assert.AreEqual(CellType.Stone, s.Board[1, 0].Type);
        }

        [TestCase("I", Description = "thick ice")]
        [TestCase("2", Description = "chain")]
        [TestCase("m", Description = "moss")]
        public void ATreasureWaitsOnATemporaryObstacleUntilTheTileBelowIsGone(string obstacle)
        {
            var s = Play.Level(new[] { "121", "2$3", "000", "131" }, new[] { "...", "...", "...", "." + obstacle + "." });
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(0, o.Treasures.Count, "an obstacle is not the floor");
            Assert.AreEqual(CellType.Treasure, s.Board[1, 1].Type);

            // Strip the obstacle: the treasure now rests on an ordinary tile, still not the floor.
            if (obstacle != "m")
            {
                var strip = Play.Use(s, Play.At(PowerUpId.TileBreaker, 1, 0));
                Assert.AreEqual(0, strip.Treasures.Count);
                Assert.AreEqual(CellType.Treasure, s.Board[1, 1].Type);
            }
            var remove = Play.Use(s, Play.At(PowerUpId.TileBreaker, 1, 0));
            Assert.AreEqual(1, remove.Treasures.Count, "with the tile below gone it reaches the floor");
        }

        [Test]
        public void TwoStackedTreasuresAreBothCollected()
        {
            var s = Play.Level(new[] { "121", "2$3", "4$1", "000", "202" }, null, 20, GoalSpec.CollectTreasure(2));
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(2, o.Treasures.Count);
            Assert.AreEqual(SessionState.Won, s.State);
        }

        [Test]
        public void NothingButGravityMovesTreasure()
        {
            var s = Play.Level(new[] { "0101", "1$10", "0101", "1001" }, null);
            int id = s.Board[1, 2].Id;
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.At(PowerUpId.TileBreaker, 1, 2)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Swap(1, 2, 0, 2)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Shift(1, 2, 0)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.At(PowerUpId.ColourBurst, 1, 2)));
            var blast = Play.Use(s, Play.At(PowerUpId.Blast, 1, 3));
            Assert.IsFalse(blast.Cleared.Any(c => c.TileId == id), "area effects pass over treasure");
            Assert.AreEqual(CellType.Treasure, s.Board[Play.Find(s.Board, id)].Type);

            var b = Board.FromRows("0$0", "101", "010");
            var before = b[1, 2];
            Assert.AreEqual(RecoveryResult.Shuffled, BoardRecovery.EnsurePlayable(b, new Rng(3), null));
            Assert.AreEqual(before, b[1, 2], "reshuffles leave treasure where it is");
        }

        [Test]
        public void LayoutsRejectTreasureThatWouldBeCollectedAtOnce()
        {
            Assert.IsFalse(LevelLayout.Parse(new[] { "...", "...", ".$." }).Validate(out _));
            Assert.IsFalse(LevelLayout.Parse(new[] { "...", ".$.", ".#." }).Validate(out _));
            var ok = LevelLayout.Parse(new[] { ".$.", "...", "..." });
            Assert.IsTrue(ok.Validate(out var error), error);
            Assert.AreEqual(1, ok.TreasureCount);
            CollectionAssert.AreEqual(new[] { ".$.", "...", "..." }, ok.ToRows());
        }

        [Test]
        public void FindWorksOnPieces()
        {
            var b = Board.FromRows("$*", "01");
            Assert.AreEqual(CellType.Treasure, b[0, 1].Type);
            Assert.AreEqual(SpecialKind.Bomb, b[1, 1].Special);
            Assert.AreEqual("$*\n01", b.ToDebugString());
        }
    }

    /// <summary>Moss (docs/MECHANICS.md §2.5).</summary>
    public class MossTests
    {
        [Test]
        public void MossyTilesJoinTheirGroupAndLeaveWithIt()
        {
            var s = Play.Moss(new[] { "0011", "2233" }, new[] { "m...", "...." }, 0, 20, 12345, GoalSpec.ClearMoss());
            Assert.AreEqual(1, s.Goals.Goal(0).amount, "the starting moss");
            Assert.AreEqual(1, s.Goals.Remaining(0));
            var o = Play.Tap(s, 1, 1);
            Assert.AreEqual(2, o.Cleared.Count);
            Assert.AreEqual(1, o.ObstacleHits.Count);
            Assert.AreEqual(ObstacleKind.Moss, o.ObstacleHits[0].Kind);
            Assert.IsTrue(o.ObstacleHits[0].Freed);
            Assert.AreEqual(40 + 100, o.ScoreGained);
            Assert.AreEqual(0, s.Board.CountObstacle(ObstacleKind.Moss));
            Assert.AreEqual(SessionState.Won, s.State);
        }

        [Test]
        public void AreaHitsAndTileBreakerRemoveMossyTiles()
        {
            var s = Play.Moss(new[] { "0011", "2233" }, new[] { "m..m", "...." });
            var blast = Play.Use(s, Play.At(PowerUpId.Blast, 0, 0));
            Assert.IsTrue(blast.ObstacleHits.Any(h => h.Kind == ObstacleKind.Moss && h.Cause == HitCause.Area));
            var breaker = Play.Use(s, Play.At(PowerUpId.TileBreaker, 3, 1));
            Assert.IsTrue(breaker.Cleared.Any(c => c.Pos == new GridPos(3, 1)));
            Assert.AreEqual(0, s.Board.CountObstacle(ObstacleKind.Moss));

            var t = Play.Moss(new[] { "0011", "2233" }, new[] { "...m", "...." });
            var burst = Play.Use(t, Play.At(PowerUpId.ColourBurst, 2, 1));
            Assert.AreEqual(2, burst.Cleared.Count);
            Assert.AreEqual(0, t.Board.CountObstacle(ObstacleKind.Moss));
        }

        [Test]
        public void AMoveThatClearsNoMossLetsItGrowByOneTile()
        {
            var s = Play.Moss(new[] { "001", "223" }, new[] { "..m", "..." }, 0, 20, 12345, GoalSpec.ClearMoss());
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(0, o.ObstacleHits.Count, "adjacent clears do nothing to moss");
            Assert.AreEqual(1, o.MossGrowth.Count);
            var g = o.MossGrowth[0];
            Assert.AreEqual(new GridPos(2, 1), g.From);
            Assert.IsTrue(g.Pos.IsAdjacentTo(g.From));
            Assert.AreEqual(ObstacleKind.Moss, s.Board[g.Pos].Obstacle);
            Assert.AreEqual(g.TileId, s.Board[g.Pos].Id, "the tile stays; moss covers it");
            Assert.AreEqual(2, s.Board.CountObstacle(ObstacleKind.Moss));
            Assert.AreEqual(2, s.Goals.Remaining(0), "the goal shows the moss that grew");
            Assert.AreEqual(0, s.Goals.Progress(0));
        }

        [Test]
        public void GrowthIsRepeatableForTheSameSeed()
        {
            string[] rows = { "01020", "12101", "20212", "01020" };
            string[] moss = { ".....", "..m..", ".....", "....." };
            var picks = new List<string>();
            foreach (ulong seed in new ulong[] { 1, 1, 2, 3, 4, 5 })
            {
                var s = Play.Moss(rows, moss, 0, 20, seed);
                var o = new MoveOutcome();
                Assert.IsTrue(s.TryPlay(new GridPos(1, 0), o) || s.TryPlay(new GridPos(4, 3), o) || PlayAny(s, o));
                picks.Add(o.MossGrowth.Count > 0 ? o.MossGrowth[0].Pos.ToString() : "-");
            }
            Assert.AreEqual(picks[0], picks[1], "same seed, same growth");
        }

        static bool PlayAny(PuzzleSession s, MoveOutcome o)
        {
            var groups = new List<GroupInfo>();
            new GroupFinder().FindAllGroups(s.Board, groups);
            return groups.Count > 0 && s.TryPlay(groups[0].Anchor, o);
        }

        [Test]
        public void MossDoesNotGrowAfterPowerUpsOrMovesThatClearIt()
        {
            var s = Play.Moss(new[] { "0011", "2233", "4545" }, new[] { "...m", "....", "...." });
            var blast = Play.Use(s, Play.At(PowerUpId.Blast, 0, 0));
            Assert.AreEqual(0, blast.MossGrowth.Count, "power-ups cost no move and never make moss grow");

            var t = Play.Moss(new[] { "0011", "2233" }, new[] { "...m", "m..." });
            var o = Play.Tap(t, 2, 1);
            Assert.AreEqual(0, o.MossGrowth.Count, "this move cleared moss");
        }

        [Test]
        public void MossStopsAtItsCap()
        {
            var s = Play.Moss(new[] { "001", "223" }, new[] { "..m", "..." }, 1);
            var o = Play.Tap(s, 0, 1);
            Assert.AreEqual(0, o.MossGrowth.Count);
            Assert.AreEqual(1, s.Board.CountObstacle(ObstacleKind.Moss));
        }

        [Test]
        public void MossOnlyGrowsOntoFreeTiles()
        {
            // The mossy 5 is boxed in by ice, a chain, treasure and a bomb; the 4s are the move.
            var s = Play.Moss(new[] { "3$44", "0512", "1*30" }, new[] { "....", "im1.", "...." });
            var o = Play.Tap(s, 2, 2);
            Assert.AreEqual(0, o.MossGrowth.Count);
            Assert.AreEqual(0, o.Treasures.Count, "moss is not the floor either");
        }

        [Test]
        public void MossyTilesHoldTheirPlace()
        {
            var s = Play.Moss(new[] { "212", "141", "003" }, new[] { "...", ".m.", "..." });
            int above = s.Board[1, 2].Id;
            Play.Tap(s, 0, 0);
            Assert.AreEqual(above, s.Board[1, 2].Id, "tiles above moss stay up");
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Swap(1, 1, 1, 2)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(s, Play.Shift(1, 1, 4)));
            Assert.IsFalse(TileRules.CanShuffle(s.Board[1, 1]));
        }

        [Test]
        public void MossNeverBlocksEveryMove()
        {
            // A board covered in moss still has moves: mossy tiles group with their colour.
            var b = Board.FromRows(new[] { "001", "122" }, new[] { "mmm", "mmm" });
            Assert.IsTrue(GroupFinder.HasLegalMove(b));
        }
    }
}
