using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>Rockets, bombs and prisms: creation, firing, combinations, chain reactions and power-ups (docs/MECHANICS.md §2.3).</summary>
    public class SpecialTileTests
    {
        /// <summary>An 8 × 4 board whose only group is exactly n zeros along the bottom rows (n ≤ 16).</summary>
        static string[] GroupOf(int n)
        {
            var row0 = new char[8];
            var row1 = new char[8];
            for (int c = 0; c < 8; c++)
            {
                row0[c] = c < n ? '0' : (c % 2 == 0 ? '3' : '4');
                row1[c] = 8 + c < n ? '0' : (c % 2 == 0 ? '4' : '3');
            }
            return new[] { "12121212", "21212121", new string(row1), new string(row0) };
        }

        /// <summary>A 5 × 5 checkerboard of 0 and 1 (no groups) with pieces placed over it.</summary>
        static string[] Checker(int size, params (int col, int row, char piece)[] pieces)
        {
            var rows = new string[size];
            for (int i = 0; i < size; i++)
            {
                int r = size - 1 - i;
                var chars = new char[size];
                for (int c = 0; c < size; c++) chars[c] = (c + r) % 2 == 0 ? '0' : '1';
                foreach (var p in pieces)
                    if (p.row == r) chars[p.col] = p.piece;
                rows[i] = new string(chars);
            }
            return rows;
        }

        static int ColourTiles(Board b, IEnumerable<GridPos> cells) => cells.Count(p => b.InBounds(p) && b[p].Type == CellType.Colour);

        [TestCase(5, SpecialKind.None)]
        [TestCase(6, SpecialKind.RocketRow)]
        [TestCase(8, SpecialKind.RocketRow)]
        [TestCase(9, SpecialKind.Bomb)]
        [TestCase(11, SpecialKind.Bomb)]
        [TestCase(12, SpecialKind.Prism)]
        [TestCase(16, SpecialKind.Prism)]
        public void BigGroupsEarnSpecialsAtTheConfiguredSizes(int size, SpecialKind expected)
        {
            var s = Play.Specials(GroupOf(size), null);
            var o = Play.Tap(s, 0, 0);
            Assert.AreEqual(size, o.GroupSize);
            Assert.AreEqual(expected != SpecialKind.None, o.HasCreated);
            Assert.AreEqual(s.Scoring.GroupPoints(size), o.ScoreGained, "creating a special adds no points of its own");
            Assert.AreEqual(19, s.MovesLeft, "the group's move is the only move spent");
            if (expected == SpecialKind.None)
            {
                Assert.AreEqual(0, Play.CountPieces(s.Board, CellType.Special));
                return;
            }
            Assert.AreEqual(expected, o.Created.Kind);
            Assert.AreEqual(new GridPos(0, 0), o.Created.Pos, "it appears where the player tapped");
            Assert.AreEqual(CellType.Special, s.Board[0, 0].Type);
            Assert.AreEqual(expected, s.Board[0, 0].Special);
            Assert.AreEqual(o.Created.TileId, s.Board[0, 0].Id);
            Assert.AreEqual(1, Play.CountPieces(s.Board, CellType.Special));
        }

        [TestCase(SpecialTier.None, 16, SpecialKind.None)]
        [TestCase(SpecialTier.Rockets, 16, SpecialKind.RocketRow)]
        [TestCase(SpecialTier.Bombs, 16, SpecialKind.Bomb)]
        [TestCase(SpecialTier.Bombs, 8, SpecialKind.RocketRow)]
        public void ALevelOnlyCreatesTheSpecialsItHasTaught(SpecialTier tier, int size, SpecialKind expected)
        {
            var s = Play.Specials(GroupOf(size), null, tier);
            var o = Play.Tap(s, 0, 0);
            Assert.AreEqual(expected, o.HasCreated ? o.Created.Kind : SpecialKind.None);
        }

        [Test]
        public void ThresholdsAreConfigurable()
        {
            var setup = TestUtil.Setup(GameMode.Campaign, 20, 1000000, 6, GroupOf(5), null);
            setup.Level.Specials = SpecialTier.Prisms;
            setup.Scoring.specials.rocketGroupSize = 5;
            var s = new PuzzleSession(setup);
            s.Start();
            Assert.AreEqual(SpecialKind.RocketRow, Play.Tap(s, 0, 0).Created.Kind);
            var rules = new SpecialRules();
            Assert.AreEqual(SpecialKind.RocketColumn, rules.KindFor(7, SpecialTier.Prisms, false), "taller than wide: a column rocket");
            Assert.AreEqual(new[] { 6, 9, 12 }, new[] { rules.rocketGroupSize, rules.bombGroupSize, rules.prismGroupSize }, "the defaults the campaign was balanced with");
        }

        [Test]
        public void AColumnGroupMakesAColumnRocketThatFallsLikeATile()
        {
            string[] rows = { "012", "021", "012", "021", "012", "021", "012" };
            var s = Play.Specials(rows, null);
            var o = Play.Tap(s, 0, 3);
            Assert.AreEqual(SpecialKind.RocketColumn, o.Created.Kind);
            Assert.AreEqual(new GridPos(0, 3), o.Created.Pos);
            Assert.IsTrue(o.Falls.Any(f => f.TileId == o.Created.TileId && f.From == new GridPos(0, 3) && f.To == new GridPos(0, 0)), "it falls into the cleared column");
            Assert.AreEqual(o.Created.TileId, s.Board[0, 0].Id);
        }

        [Test]
        public void WhenTheTappedTileIsChainedTheSpecialTakesTheNearestClearedCell()
        {
            var s = Play.Specials(GroupOf(7), new[] { "........", "........", "........", "1......." });
            var o = Play.Tap(s, 0, 0);
            Assert.AreEqual(7, o.GroupSize);
            Assert.AreEqual(new GridPos(1, 0), o.Created.Pos);
            Assert.AreEqual(ObstacleKind.None, s.Board[0, 0].Obstacle, "the chained tile lost its last link");
        }

        [Test]
        public void SpecialsNeverJoinGroups()
        {
            var b = Board.FromRows("0*0", "121");
            Assert.AreEqual(0, new GroupFinder().GroupSize(b, new GridPos(1, 1)));
            Assert.AreEqual(1, new GroupFinder().GroupSize(b, new GridPos(0, 1)));
            Assert.IsTrue(GroupFinder.HasLegalMove(b), "a special tile is always a legal move");
        }

        [Test]
        public void ARowRocketHitsEverythingInItsRow()
        {
            // Row 1: frozen 0, the rocket, chained 1 (2 links), mossy 2, a stone and a treasure.
            var s = Play.Specials(new[] { "343434", "0-12#$", "434343" }, new[] { "......", "i.2m..", "......" });
            int treasure = s.Board[5, 1].Id;
            int movesBefore = s.MovesLeft;
            var o = Play.Tap(s, 1, 1);

            Assert.AreEqual(OutcomeKind.Special, o.Kind);
            Assert.AreEqual(movesBefore - 1, s.MovesLeft, "firing a special costs a move");
            Assert.AreEqual(0, s.GroupsCleared);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(SpecialKind.RocketRow, o.SpecialsFired[0].Kind);
            Assert.AreEqual(1, o.Cleared.Count, "only the mossy tile leaves the board");
            Assert.AreEqual(new GridPos(3, 1), o.Cleared[0].Pos);
            Assert.AreEqual(3, o.ObstacleHits.Count);
            Assert.IsTrue(o.ObstacleHits.Any(h => h.Kind == ObstacleKind.Ice && h.Freed && h.Cause == HitCause.Area));
            Assert.IsTrue(o.ObstacleHits.Any(h => h.Kind == ObstacleKind.Chain && h.StrengthAfter == 1));
            Assert.IsTrue(o.ObstacleHits.Any(h => h.Kind == ObstacleKind.Moss && h.Freed));
            Assert.AreEqual(CellType.Stone, s.Board[4, 1].Type, "stones stop nothing and are not broken");
            Assert.AreEqual(treasure, s.Board[5, 1].Id, "treasure is untouched");
            Assert.AreEqual(20 + 100 + 150 + 100, o.ScoreGained, "one tile at 20, plus ice, chain and moss");
        }

        [Test]
        public void ABombHitsTheThreeByThreeSquare()
        {
            var s = Play.Specials(Checker(5, (2, 2, '*')), null);
            var o = Play.Tap(s, 2, 2);
            Assert.AreEqual(8, o.Cleared.Count);
            Assert.IsTrue(o.Cleared.All(c => System.Math.Abs(c.Pos.Col - 2) <= 1 && System.Math.Abs(c.Pos.Row - 2) <= 1));
            Assert.AreEqual(8 * 20, o.ScoreGained);
        }

        [Test]
        public void APrismClearsTheChosenColour()
        {
            var rows = new[] { "01210", "12021", "20@02", "01210", "12021" };
            var s = Play.Specials(rows, null);
            Assert.IsTrue(s.NeedsColourChoice(new GridPos(2, 2)));
            int ones = s.Board.CountColour(1);
            var o = Play.Tap(s, 2, 2, 1);
            Assert.AreEqual(ones, o.Cleared.Count);
            Assert.IsTrue(o.Cleared.All(c => c.Colour == 1));
            Assert.AreEqual(1, o.SpecialsFired[0].Colour);
            Assert.AreEqual(1, o.GroupColour);

            var t = Play.Specials(rows, null);
            int common = SpecialEffects.MostCommonColour(t.Board);
            var auto = Play.Tap(t, 2, 2);
            Assert.AreEqual(common, auto.SpecialsFired[0].Colour, "without a choice it takes the most common colour");
            Assert.IsFalse(Play.Specials(Checker(5, (2, 2, '*')), null).NeedsColourChoice(new GridPos(2, 2)));
        }

        [Test]
        public void TwoRocketsTogetherClearARowAndAColumn()
        {
            var s = Play.Specials(Checker(5, (2, 2, '-'), (3, 2, '|')), null);
            var o = Play.Tap(s, 2, 2);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(SpecialKind.RocketColumn, o.SpecialsFired[0].ComboWith);
            Assert.AreEqual(7, o.Cleared.Count, "row 2 and column 2, without the two rockets");
            Assert.IsTrue(o.Cleared.All(c => c.Pos.Row == 2 || c.Pos.Col == 2));
            Assert.AreEqual(7 * 40, o.ScoreGained, "combinations score double");
            Assert.AreEqual(0, Play.CountPieces(s.Board, CellType.Special), "both rockets are used up");
        }

        [Test]
        public void ARocketAndABombClearThreeRows()
        {
            var s = Play.Specials(Checker(5, (2, 2, '*'), (2, 3, '-')), null);
            var o = Play.Tap(s, 2, 2);
            Assert.AreEqual(SpecialKind.RocketRow, o.SpecialsFired[0].ComboWith);
            Assert.AreEqual(13, o.Cleared.Count);
            Assert.IsTrue(o.Cleared.All(c => c.Pos.Row >= 1 && c.Pos.Row <= 3));
        }

        [Test]
        public void TwoBombsTogetherClearFiveByFive()
        {
            var s = Play.Specials(Checker(7, (3, 3, '*'), (4, 3, '*')), null);
            var o = Play.Tap(s, 3, 3);
            Assert.AreEqual(23, o.Cleared.Count);
            Assert.IsTrue(o.Cleared.All(c => System.Math.Abs(c.Pos.Col - 3) <= 2 && System.Math.Abs(c.Pos.Row - 3) <= 2));
            Assert.AreEqual(23 * 40, o.ScoreGained);
        }

        [Test]
        public void APrismWithARocketClearsTheColourThenFiresTheRocket()
        {
            var s = Play.Specials(Checker(5, (2, 2, '@'), (3, 2, '|')), null);
            var expected = new HashSet<GridPos>();
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 5; c++)
                    if (s.Board[c, r].Type == CellType.Colour && (s.Board[c, r].Colour == 0 || c == 3)) expected.Add(new GridPos(c, r));
            var o = Play.Tap(s, 2, 2, 0);
            CollectionAssert.AreEquivalent(expected, o.Cleared.Select(c => c.Pos));
            Assert.AreEqual(expected.Count * 40, o.ScoreGained);
        }

        [Test]
        public void TwoPrismsClearEveryColourTile()
        {
            var s = Play.Specials(Checker(5, (2, 2, '@'), (2, 1, '@')), null);
            Assert.IsTrue(s.NeedsColourChoice(new GridPos(2, 2)));
            var o = Play.Tap(s, 2, 2, 0);
            Assert.AreEqual(23, o.Cleared.Count);
        }

        [Test]
        public void TheStrongestNeighbourIsThePartner()
        {
            var b = Board.FromRows("*-|", "010");
            Assert.IsTrue(SpecialEffects.FindPartner(b, new GridPos(1, 1), out var partner));
            Assert.AreEqual(new GridPos(0, 1), partner, "a bomb beats a rocket");
            Assert.IsFalse(SpecialEffects.FindPartner(Board.FromRows("*0-", "010"), new GridPos(0, 1), out _));
        }

        [Test]
        public void SpecialsHitBySpecialsFireOnceInOrder()
        {
            // The row rocket at (0,2) reaches the bomb at (3,2), which then clears around itself.
            var s = Play.Specials(Checker(5, (0, 2, '-'), (3, 2, '*')), null);
            var o = Play.Tap(s, 0, 2);
            Assert.AreEqual(2, o.SpecialsFired.Count);
            Assert.AreEqual(SpecialKind.RocketRow, o.SpecialsFired[0].Kind);
            Assert.AreEqual(0, o.SpecialsFired[0].Wave);
            Assert.AreEqual(SpecialKind.Bomb, o.SpecialsFired[1].Kind);
            Assert.AreEqual(1, o.SpecialsFired[1].Wave);
            Assert.AreEqual(3, o.Cleared.Count(c => c.Wave == 0));
            Assert.AreEqual(6, o.Cleared.Count(c => c.Wave == 1));
            Assert.AreEqual(9 * 20, o.ScoreGained);

            // Rockets that reach each other fire once each.
            var t = Play.Specials(Checker(5, (0, 2, '-'), (4, 2, '|'), (4, 0, '-')), null);
            var chain = Play.Tap(t, 0, 2);
            Assert.AreEqual(3, chain.SpecialsFired.Count);
            Assert.AreEqual(3, chain.SpecialsFired.Select(f => f.TileId).Distinct().Count());
        }

        [Test]
        public void SpecialsCountForGoals()
        {
            var s = Play.Specials(new[] { "343434", "0-0010", "434343" }, new[] { "......", ".....i", "......" }, SpecialTier.Prisms, 20,
                GoalSpec.Collect(0, 3), GoalSpec.FreeIce(1));
            Play.Tap(s, 1, 1);
            Assert.AreEqual(3, s.Goals.Progress(0));
            Assert.AreEqual(1, s.Goals.Progress(1));
            Assert.AreEqual(SessionState.Won, s.State);
        }

        [Test]
        public void PowerUpsAndSpecials()
        {
            // Blast sets off a bomb in its area.
            var s = Play.Specials(Checker(5, (2, 2, '*')), null);
            var blast = Play.Use(s, Play.At(PowerUpId.Blast, 1, 1));
            Assert.AreEqual(1, blast.SpecialsFired.Count);
            Assert.AreEqual(1, blast.SpecialsFired[0].Wave);
            Assert.AreEqual(20, s.MovesLeft, "power-ups cost no move, even when they fire a special");
            int primary = blast.Cleared.Count(c => c.Wave == 0);
            int bySpecial = blast.Cleared.Count(c => c.Wave == 1);
            Assert.AreEqual(8, primary);
            Assert.AreEqual(s.Scoring.PowerUpPoints(primary) + bySpecial * 20, blast.ScoreGained);

            // Tile Breaker fires a special.
            var t = Play.Specials(Checker(5, (2, 2, '-')), null);
            var breaker = Play.Use(t, Play.At(PowerUpId.TileBreaker, 2, 2));
            Assert.AreEqual(1, breaker.SpecialsFired.Count);
            Assert.AreEqual(4, breaker.Cleared.Count);

            // Swap moves a special with a free tile, but not two specials; nothing recolours or bursts a special.
            var u = Play.Specials(Checker(5, (2, 2, '*'), (3, 2, '-')), null);
            int bomb = u.Board[2, 2].Id;
            Assert.AreEqual(PowerUpFailure.SameColour, Play.Refused(u, Play.Swap(2, 2, 3, 2)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(u, Play.Shift(2, 2, 1)));
            Assert.AreEqual(PowerUpFailure.InvalidTarget, Play.Refused(u, Play.At(PowerUpId.ColourBurst, 2, 2)));
            Play.Use(u, Play.Swap(2, 2, 2, 3));
            Assert.AreEqual(bomb, u.Board[2, 3].Id);
            var burst = Play.Use(u, Play.At(PowerUpId.ColourBurst, 0, 0));
            Assert.AreEqual(0, burst.SpecialsFired.Count, "Colour Burst does not set off specials");
            Assert.AreEqual(2, Play.CountPieces(u.Board, CellType.Special));
        }

        [Test]
        public void ABoardWithOnlyASpecialNeedsNoShuffle()
        {
            var s = Play.Specials(Checker(5, (2, 2, '-')), null);
            Assert.AreEqual(0, s.Shuffles);
            Assert.AreEqual(SessionState.Playing, s.State);
        }

        [Test]
        public void SpecialsStopAtStones()
        {
            var s = Play.Specials(new[] { "01*", "10#", "011" }, null);
            Play.Tap(s, 1, 0);
            Assert.AreEqual(CellType.Special, s.Board[2, 2].Type, "the bomb rests on the stone");
            Assert.AreEqual(CellType.Stone, s.Board[2, 1].Type);
        }
    }
}
