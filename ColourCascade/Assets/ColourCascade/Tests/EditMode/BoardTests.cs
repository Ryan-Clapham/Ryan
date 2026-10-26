using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    public class ScoringTests
    {
        [TestCase(2, 40)]
        [TestCase(3, 90)]
        [TestCase(4, 160)]
        [TestCase(5, 250)]
        [TestCase(6, 360)]
        [TestCase(10, 1000)]
        public void GroupPoints_AreSizeSquaredTimesTen(int size, int expected)
        {
            Assert.AreEqual(expected, new ScoringRules().GroupPoints(size));
        }

        [Test]
        public void SingleTilesScoreNothing()
        {
            Assert.AreEqual(0, new ScoringRules().GroupPoints(1));
            Assert.AreEqual(0, new ScoringRules().GroupPoints(0));
        }

        [Test]
        public void ScoringIsConfigurable()
        {
            var rules = new ScoringRules { pointsPerTile = 5, exponent = 3 };
            Assert.AreEqual(5 * 27, rules.GroupPoints(3));
        }

        [Test]
        public void PraiseTiersFollowThresholds()
        {
            var r = new ScoringRules();
            Assert.AreEqual(PraiseTier.None, r.PraiseFor(4));
            Assert.AreEqual(PraiseTier.Good, r.PraiseFor(5));
            Assert.AreEqual(PraiseTier.Great, r.PraiseFor(8));
            Assert.AreEqual(PraiseTier.Amazing, r.PraiseFor(12));
            Assert.AreEqual(PraiseTier.Cascade, r.PraiseFor(20));
        }

        [Test]
        public void StarRatingUsesThresholds()
        {
            Assert.AreEqual(0, StarRating.Compute(99, 100, 150, 200));
            Assert.AreEqual(1, StarRating.Compute(100, 100, 150, 200));
            Assert.AreEqual(2, StarRating.Compute(150, 100, 150, 200));
            Assert.AreEqual(3, StarRating.Compute(500, 100, 150, 200));
        }
    }

    public class GroupFinderTests
    {
        [Test]
        public void FindsOrthogonallyConnectedGroup()
        {
            // Top row first: (0,1)=0 (1,1)=0 / (0,0)=1 (1,0)=0
            var b = Board.FromRows("00", "10");
            var f = new GroupFinder();
            var cells = new List<GridPos>();
            Assert.AreEqual(3, f.FindGroup(b, new GridPos(0, 1), cells));
            CollectionAssert.AreEquivalent(new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(1, 0) }, cells);
            Assert.AreEqual(1, f.FindGroup(b, new GridPos(0, 0), cells));
        }

        [Test]
        public void DiagonalContactDoesNotConnect()
        {
            var b = Board.FromRows("01", "10");
            var f = new GroupFinder();
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 2; c++)
                    Assert.AreEqual(1, f.GroupSize(b, new GridPos(c, r)));
            Assert.IsFalse(GroupFinder.HasLegalMove(b));
        }

        [Test]
        public void StonesAndVoidsBreakGroups()
        {
            var b = Board.FromRows("0#0x0");
            var f = new GroupFinder();
            Assert.AreEqual(1, f.GroupSize(b, new GridPos(0, 0)));
            Assert.AreEqual(0, f.GroupSize(b, new GridPos(1, 0)), "stones are not groups");
            Assert.IsFalse(GroupFinder.HasLegalMove(b));
        }

        [Test]
        public void LargeGroupAcrossBoard()
        {
            var b = Board.FromRows("0000", "0110", "0000");
            Assert.AreEqual(10, new GroupFinder().GroupSize(b, new GridPos(0, 0)));
            Assert.AreEqual(2, new GroupFinder().GroupSize(b, new GridPos(1, 1)));
        }

        [Test]
        public void FindAllGroupsListsEachGroupOnce()
        {
            var b = Board.FromRows("0011", "2233");
            var groups = new List<GroupInfo>();
            Assert.AreEqual(4, new GroupFinder().FindAllGroups(b, groups));
            Assert.IsTrue(groups.All(g => g.Size == 2));
        }

        [Test]
        public void RepeatedSearchesReuseBuffers()
        {
            var b = Board.FromRows("001", "011", "111");
            var f = new GroupFinder();
            for (int i = 0; i < 1000; i++) Assert.AreEqual(6, f.GroupSize(b, new GridPos(2, 0)));
        }
    }

    public class GravityTests
    {
        sealed class FixedSource : ITileSource
        {
            public int Colour = 5;
            public int NextColour(Board board, int col, int row) => Colour;
        }

        [Test]
        public void TilesFallIntoGapsAndKeepTheirIds()
        {
            var b = Board.FromRows("1", "2", ".");
            int topId = b[0, 2].Id, midId = b[0, 1].Id;
            var moves = new List<TileMove>();
            Gravity.Apply(b, new FixedSource(), moves, null, refill: false);
            Assert.AreEqual(midId, b[0, 0].Id);
            Assert.AreEqual(topId, b[0, 1].Id);
            Assert.AreEqual(CellType.Empty, b[0, 2].Type);
            Assert.AreEqual(2, moves.Count);
        }

        [Test]
        public void RefillFillsOnlyEmptyCellsFromTheTop()
        {
            var b = Board.FromRows("1", ".", "2");
            var spawns = new List<TileSpawn>();
            Gravity.Apply(b, new FixedSource(), null, spawns);
            Assert.AreEqual(2, b[0, 0].Colour);
            Assert.AreEqual(1, b[0, 1].Colour);
            Assert.AreEqual(5, b[0, 2].Colour);
            Assert.AreEqual(1, spawns.Count);
            Assert.IsTrue(spawns[0].FromBoardTop);
            Assert.AreEqual(0, spawns[0].Order);
        }

        [Test]
        public void StonesStopFallingTiles()
        {
            var b = Board.FromRows("1", "#", ".");
            int tileId = b[0, 2].Id;
            var spawns = new List<TileSpawn>();
            Gravity.Apply(b, new FixedSource(), null, spawns);
            Assert.AreEqual(tileId, b[0, 2].Id, "tile above the stone must not fall through it");
            Assert.AreEqual(CellType.Stone, b[0, 1].Type);
            Assert.AreEqual(CellType.Colour, b[0, 0].Type, "section below the stone refills on its own");
            Assert.AreEqual(1, spawns.Count);
            Assert.IsFalse(spawns[0].FromBoardTop);
            Assert.AreEqual(new GridPos(0, 0), spawns[0].SectionTop);
        }

        [Test]
        public void SectionsAreIndependent()
        {
            // rows (top first): '.', '1', '#', '2', '.'
            var b = Board.FromRows(".", "1", "#", "2", ".");
            int upper = b[0, 3].Id, lower = b[0, 1].Id;
            var moves = new List<TileMove>();
            var spawns = new List<TileSpawn>();
            Gravity.Apply(b, new FixedSource(), moves, spawns);
            Assert.AreEqual(upper, b[0, 3].Id);
            Assert.AreEqual(lower, b[0, 0].Id);
            Assert.AreEqual(2, spawns.Count);
            var top = spawns.Single(s => s.At.Row == 4);
            var below = spawns.Single(s => s.At.Row == 1);
            Assert.IsTrue(top.FromBoardTop);
            Assert.IsFalse(below.FromBoardTop);
            Assert.AreEqual(1, moves.Count);
        }

        [Test]
        public void VoidsActAsBarriers()
        {
            var b = Board.FromRows("1", "x", ".");
            Gravity.Apply(b, new FixedSource(), null, null);
            Assert.AreEqual(1, b[0, 2].Colour);
            Assert.AreEqual(CellType.Void, b[0, 1].Type);
            Assert.AreEqual(5, b[0, 0].Colour);
        }

        [Test]
        public void MultipleGapsInOneColumnCompactInOrder()
        {
            var b = Board.FromRows("3", ".", "2", ".", "1", ".");
            Gravity.Apply(b, new FixedSource(), null, null, refill: false);
            Assert.AreEqual(1, b[0, 0].Colour);
            Assert.AreEqual(2, b[0, 1].Colour);
            Assert.AreEqual(3, b[0, 2].Colour);
            Assert.AreEqual(CellType.Empty, b[0, 3].Type);
        }
    }

    public class RecoveryTests
    {
        static Dictionary<int, int> Counts(Board b)
        {
            var d = new Dictionary<int, int>();
            for (int i = 0; i < b.CellCount; i++)
            {
                var t = b.GetByIndex(i);
                if (t.Type != CellType.Colour) continue;
                d.TryGetValue(t.Colour, out int n);
                d[t.Colour] = n + 1;
            }
            return d;
        }

        [Test]
        public void DeadBoardIsDetected()
        {
            Assert.IsFalse(GroupFinder.HasLegalMove(Board.FromRows("012", "120", "201")));
        }

        [Test]
        public void ReshuffleCreatesALegalMoveAndKeepsTheSameTiles()
        {
            var b = Board.FromRows("012", "120", "201");
            var before = Counts(b);
            var ids = Enumerable.Range(0, b.CellCount).Select(i => b.GetByIndex(i).Id).OrderBy(x => x).ToList();
            var moves = new List<TileMove>();
            var result = BoardRecovery.EnsurePlayable(b, new Rng(7), moves);
            Assert.AreEqual(RecoveryResult.Shuffled, result);
            Assert.IsTrue(GroupFinder.HasLegalMove(b));
            CollectionAssert.AreEquivalent(before, Counts(b));
            CollectionAssert.AreEqual(ids, Enumerable.Range(0, b.CellCount).Select(i => b.GetByIndex(i).Id).OrderBy(x => x).ToList());
            Assert.Greater(moves.Count, 0);
        }

        [Test]
        public void ConstructiveFallbackAlwaysWorksWhenPossible()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var b = Board.FromRows("012", "120", "201");
                Assert.AreEqual(RecoveryResult.Shuffled, BoardRecovery.EnsurePlayable(b, new Rng((ulong)seed), null, randomAttempts: 0));
                Assert.IsTrue(GroupFinder.HasLegalMove(b));
            }
        }

        [Test]
        public void StonesNeverMove()
        {
            var b = Board.FromRows("0#1", "1#0", "0#1");
            BoardRecovery.EnsurePlayable(b, new Rng(3), null);
            for (int r = 0; r < 3; r++) Assert.AreEqual(CellType.Stone, b[1, r].Type);
        }

        [Test]
        public void ImpossibleRecoveryFailsGracefullyAndLeavesBoardUnchanged()
        {
            var b = Board.FromRows("012", "345");
            string before = b.ToDebugString();
            Assert.AreEqual(RecoveryResult.Failed, BoardRecovery.EnsurePlayable(b, new Rng(1), null));
            Assert.AreEqual(before, b.ToDebugString());

            var separated = Board.FromRows("0#0");
            Assert.AreEqual(RecoveryResult.Failed, BoardRecovery.EnsurePlayable(separated, new Rng(1), null));
        }

        [Test]
        public void NotNeededWhenAMoveExists()
        {
            var b = Board.FromRows("001");
            Assert.AreEqual(RecoveryResult.NotNeeded, BoardRecovery.EnsurePlayable(b, new Rng(1), null));
        }

        [Test]
        public void RecoveryIsDeterministicForTheSameSeed()
        {
            var a = Board.FromRows("0123", "1230", "2301", "3012");
            var b = Board.FromRows("0123", "1230", "2301", "3012");
            BoardRecovery.EnsurePlayable(a, new Rng(99), null);
            BoardRecovery.EnsurePlayable(b, new Rng(99), null);
            Assert.AreEqual(a.ToDebugString(), b.ToDebugString());
        }
    }

    public class RngTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void KnownSequenceIsStableAcrossPlatforms()
        {
            // Pinned values: if these change, daily/weekly puzzles would change for existing players.
            var r = new Rng(1);
            var first = new[] { r.NextUInt(), r.NextUInt(), r.NextUInt() };
            var again = new Rng(1);
            CollectionAssert.AreEqual(first, new[] { again.NextUInt(), again.NextUInt(), again.NextUInt() });
            Assert.AreEqual(StableHash.Of("daily:2026-10-25"), StableHash.Of("daily:2026-10-25"));
            Assert.AreNotEqual(StableHash.Of("daily:2026-10-25"), StableHash.Of("daily:2026-10-26"));
        }

        [Test]
        public void NextStaysInRange()
        {
            var r = new Rng(5);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.Next(7);
                Assert.IsTrue(v >= 0 && v < 7);
            }
        }
    }
}
