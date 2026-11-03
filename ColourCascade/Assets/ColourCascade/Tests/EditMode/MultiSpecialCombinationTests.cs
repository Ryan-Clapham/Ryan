using System.Collections.Generic;
using System.Linq;
using ColourCascade.Core;
using NUnit.Framework;

namespace ColourCascade.Tests
{
    /// <summary>
    /// Connected groups of special tiles fire as one combination, whatever their size (docs/MECHANICS.md §2.3): the
    /// rules for every mix of rockets, bombs and prisms, obstacles in the way, chain reactions, goals, the preview, the
    /// end of the level, and no special or tile ever counted twice.
    /// </summary>
    public class MultiSpecialCombinationTests
    {
        const int Tile = 50, Combo = 75;

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

        /// <summary>The cells (colour tiles) a fresh 9 x 11 board loses when the group at (col,row) is tapped.</summary>
        static HashSet<GridPos> Cleared(MoveOutcome o) => new HashSet<GridPos>(o.Cleared.Where(c => !c.WasStone && c.Wave == o.SpecialsFired[0].Wave).Select(c => c.Pos));

        static HashSet<GridPos> Expected(int w, int h, System.Func<int, int, bool> inArea, IEnumerable<(int col, int row)> specials)
        {
            var set = new HashSet<GridPos>();
            var skip = new HashSet<GridPos>(specials.Select(p => new GridPos(p.col, p.row)));
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    if (inArea(c, r) && !skip.Contains(new GridPos(c, r))) set.Add(new GridPos(c, r));
            return set;
        }

        static void AssertOnce(MoveOutcome o, PuzzleSession s)
        {
            Assert.AreEqual(o.Cleared.Count, o.Cleared.Select(c => c.TileId).Distinct().Count(), "a tile was removed twice");
            var fired = o.SpecialsFired.Select(f => f.TileId).Concat(o.ComboMembers.Select(m => m.TileId)).ToList();
            Assert.AreEqual(fired.Count, fired.Distinct().Count(), "a special fired twice");
            Assert.AreEqual(s.Score, s.Breakdown.Total);
        }

        static PuzzleSession Board(int w, int h, params (int col, int row, char piece)[] pieces) => Play.Specials(Grid(w, h, pieces), null);

        // ------------------------------------------------------------------ Groups of one kind

        [Test]
        public void ThreeRocketsMakeACrossThreeLinesWide()
        {
            var s = Board(9, 11, (4, 5, '-'), (5, 5, '|'), (4, 6, '-'));
            var o = Play.Tap(s, 4, 5);
            Assert.AreEqual(1, o.SpecialsFired.Count, "one combination");
            Assert.AreEqual(3, o.SpecialsFired[0].ComboSize);
            Assert.AreEqual(2, o.ComboMembers.Count);
            var expected = Expected(9, 11, (c, r) => System.Math.Abs(r - 5) <= 1 || System.Math.Abs(c - 4) <= 1, new[] { (4, 5), (5, 5), (4, 6) });
            CollectionAssert.AreEquivalent(expected, Cleared(o));
            Assert.AreEqual(expected.Count * Combo, o.ScoreGained, "every tile at the combination rate");
            AssertOnce(o, s);
        }

        [Test]
        public void FourRocketsMakeACrossFiveLinesWide()
        {
            var s = Board(11, 11, (5, 5, '-'), (6, 5, '-'), (7, 5, '|'), (8, 5, '|'));
            var o = Play.Tap(s, 5, 5);
            Assert.AreEqual(4, o.SpecialsFired[0].ComboSize);
            var expected = Expected(11, 11, (c, r) => System.Math.Abs(r - 5) <= 2 || System.Math.Abs(c - 5) <= 2, new[] { (5, 5), (6, 5), (7, 5), (8, 5) });
            CollectionAssert.AreEquivalent(expected, Cleared(o));
            AssertOnce(o, s);
        }

        [Test]
        public void ThreeBombsMakeANineByNineSquare()
        {
            var s = Board(11, 11, (5, 5, '*'), (5, 6, '*'), (6, 6, '*'));
            var o = Play.Tap(s, 5, 5);
            Assert.AreEqual(3, o.SpecialsFired[0].ComboSize);
            var expected = Expected(11, 11, (c, r) => System.Math.Abs(c - 5) <= 4 && System.Math.Abs(r - 5) <= 4, new[] { (5, 5), (5, 6), (6, 6) });
            CollectionAssert.AreEquivalent(expected, Cleared(o), "5 + 2 + 2 = 9, centred on the tapped bomb");
            AssertOnce(o, s);
        }

        // ------------------------------------------------------------------ Mixed groups

        [Test]
        public void ARocketWithTwoBombsClearsABandFiveLinesWide()
        {
            var s = Board(9, 11, (4, 5, '-'), (5, 5, '*'), (5, 4, '*'));
            var o = Play.Tap(s, 4, 5);
            var expected = Expected(9, 11, (c, r) => System.Math.Abs(r - 5) <= 2, new[] { (4, 5), (5, 5), (5, 4) });
            CollectionAssert.AreEquivalent(expected, Cleared(o), "a row rocket: rows 3-7, all the way across");
            AssertOnce(o, s);
            var t = Board(9, 11, (4, 5, '|'), (5, 5, '*'), (5, 4, '*'));
            var col = Play.Tap(t, 4, 5);
            CollectionAssert.AreEquivalent(Expected(9, 11, (c, r) => System.Math.Abs(c - 4) <= 2, new[] { (4, 5), (5, 5), (5, 4) }), Cleared(col), "a column rocket: columns 2-6");
        }

        [Test]
        public void TwoRocketsWithABombClearACrossThreeLinesWide()
        {
            var s = Board(9, 11, (4, 5, '-'), (5, 5, '|'), (4, 4, '*'));
            var o = Play.Tap(s, 4, 5);
            var expected = Expected(9, 11, (c, r) => System.Math.Abs(r - 5) <= 1 || System.Math.Abs(c - 4) <= 1, new[] { (4, 5), (5, 5), (4, 4) });
            CollectionAssert.AreEquivalent(expected, Cleared(o));
            AssertOnce(o, s);
        }

        [Test]
        public void APrismWithARocketAndABombTakesItsColourThenTheirBand()
        {
            // The prism is tapped: colour 0 everywhere, then the rocket + bomb band (3 rows) from the rocket.
            var s = Board(9, 11, (4, 5, '@'), (5, 5, '-'), (6, 5, '*'));
            var o = Play.Tap(s, 4, 5, 0);
            Assert.AreEqual(3, o.SpecialsFired[0].ComboSize);
            var expected = Expected(9, 11, (c, r) => (c + r) % 2 == 0 || System.Math.Abs(r - 5) <= 1, new[] { (4, 5), (5, 5), (6, 5) });
            CollectionAssert.AreEquivalent(expected, Cleared(o));
            Assert.AreEqual(0, o.SpecialsFired[0].Colour);
            AssertOnce(o, s);
        }

        [Test]
        public void TwoPrismsAndABombClearEveryTileAndBreakTheStonesInTheBombsSquare()
        {
            var s = Board(9, 11, (4, 5, '@'), (5, 5, '@'), (6, 5, '*'), (7, 6, '#'), (0, 0, '#'));
            var o = Play.Tap(s, 4, 5, 0);
            Assert.AreEqual(9 * 11 - 3 - 2, o.Cleared.Count(c => !c.WasStone), "every colour tile");
            Assert.AreEqual(1, o.Cleared.Count(c => c.WasStone), "the stone in the bomb's square breaks; the far one stays");
            Assert.AreEqual(1, s.Board.CountType(CellType.Stone));
            AssertOnce(o, s);
        }

        [Test]
        public void ALongMixedChainOfSpecialsFiresOnceAsOneCombination()
        {
            // A snake of five: rocket, bomb, prism, rocket, bomb. All connected, all fire together, each once.
            var s = Board(9, 11, (2, 5, '-'), (3, 5, '*'), (4, 5, '@'), (4, 6, '|'), (5, 6, '*'));
            var o = Play.Tap(s, 2, 5, 1);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(5, o.SpecialsFired[0].ComboSize);
            Assert.AreEqual(4, o.ComboMembers.Count);
            Assert.AreEqual(0, Play.CountPieces(s.Board, CellType.Special));
            int tiles = o.Cleared.Count(c => !c.WasStone);
            Assert.AreEqual(tiles * Combo, o.ScoreGained);
            Assert.AreEqual(tiles * Tile, s.Breakdown.Specials);
            Assert.AreEqual(tiles * (Combo - Tile), s.Breakdown.ComboBonus);
            AssertOnce(o, s);
        }

        // ------------------------------------------------------------------ Rules for what joins

        [Test]
        public void OnlyOrthogonallyConnectedSpecialsJoin()
        {
            // The bomb touches the rocket diagonally only: it is not part of the combination, though the rocket's
            // line... misses it too, so it stays on the board.
            var s = Board(9, 11, (4, 5, '-'), (5, 6, '*'));
            var group = new List<GridPos>();
            s.SpecialGroupAt(new GridPos(4, 5), group);
            Assert.AreEqual(1, group.Count);
            var o = Play.Tap(s, 4, 5);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(1, o.SpecialsFired[0].ComboSize, "a rocket alone");
            Assert.AreEqual(1, Play.CountPieces(s.Board, CellType.Special), "the bomb is still there");
        }

        [Test]
        public void ASpecialTheBlastReachesFiresAloneAfterwardsWithoutLooping()
        {
            // Two rockets combine (a cross); the cross reaches a bomb that sits next to another rocket further away.
            // The bomb is set off by the blast, so it fires alone (no tap), and the rocket next to it is then reached
            // by the bomb's square and fires too. Each special exactly once; the loop ends.
            var s = Board(11, 11, (5, 5, '-'), (6, 5, '|'), (5, 1, '*'), (4, 1, '-'));
            var o = Play.Tap(s, 5, 5);
            Assert.AreEqual(3, o.SpecialsFired.Count, "the combination, then the bomb, then the rocket it reached");
            Assert.AreEqual(2, o.SpecialsFired[0].ComboSize);
            Assert.AreEqual(1, o.SpecialsFired[1].ComboSize, "set off by a blast: alone");
            Assert.AreEqual(1, o.SpecialsFired[2].ComboSize);
            Assert.AreEqual(new[] { 0, 1, 2 }, o.SpecialsFired.Select(f => f.Wave).ToArray());
            AssertOnce(o, s);
            int tilesAtSingleRate = o.Cleared.Count(c => !c.WasStone && c.Wave > 0);
            int tilesAtComboRate = o.Cleared.Count(c => !c.WasStone && c.Wave == 0);
            Assert.AreEqual(tilesAtComboRate * Combo + tilesAtSingleRate * Tile, o.ScoreGained);
        }

        [Test]
        public void PowerUpsSetOffSpecialsOneByOneNotAsACombination()
        {
            // Blast reaches two adjacent rockets: they are set off by it, each alone. Inventory power-ups never combine.
            var s = Board(9, 11, (4, 5, '-'), (5, 5, '|'));
            var o = Play.Use(s, Play.At(PowerUpId.Blast, 4, 4));
            Assert.AreEqual(2, o.SpecialsFired.Count);
            Assert.IsTrue(o.SpecialsFired.All(f => f.ComboSize == 1));
            Assert.AreEqual(0, s.Breakdown.ComboBonus);
            AssertOnce(o, s);
        }

        // ------------------------------------------------------------------ Obstacles, goals, preview

        [Test]
        public void ACombinationHitsEveryObstacleInItsAreaOnce()
        {
            var rows = Grid(9, 11, (4, 5, '-'), (5, 5, '-'), (4, 4, '-'), (1, 5, '#'), (4, 9, '$'));
            var obstacles = new string[11];
            for (int i = 0; i < 11; i++) obstacles[i] = new string('.', 9);
            char[] row5 = obstacles[11 - 1 - 5].ToCharArray();
            row5[7] = 'I';
            obstacles[11 - 1 - 5] = new string(row5);
            char[] row2 = obstacles[11 - 1 - 2].ToCharArray();
            row2[4] = '2';
            obstacles[11 - 1 - 2] = new string(row2);
            var s = Play.Specials(rows, obstacles, SpecialTier.Prisms, 20, GoalSpec.FreeIce(1), GoalSpec.BreakChains(1));
            var o = Play.Tap(s, 4, 5);
            Assert.AreEqual(1, o.Cleared.Count(c => c.WasStone), "the stone in the cross breaks");
            Assert.AreEqual(2, o.ObstacleHits.Count, "the thick ice and the double chain each take one step");
            Assert.IsTrue(o.ObstacleHits.All(h => h.StrengthAfter == 1));
            Assert.AreEqual(o.ObstacleHits.Count, o.ObstacleHits.Select(h => h.TileId).Distinct().Count());
            Assert.AreEqual(1, Play.CountPieces(s.Board, CellType.Treasure), "treasure is never hit");
            Assert.AreEqual(0, s.Goals.Progress(0));
            AssertOnce(o, s);
        }

        [Test]
        public void GoalsCountEverySpecialOnceAndEachCombinationOnce()
        {
            var s = Play.Specials(Grid(9, 11, (4, 5, '-'), (5, 5, '*'), (6, 5, '*')), null, SpecialTier.Prisms, 20,
                GoalSpec.FireSpecial(SpecialKind.Bomb, 2), GoalSpec.FireSpecial(SpecialKind.None, 3), GoalSpec.FireCombo(1));
            Play.Tap(s, 4, 5);
            Assert.AreEqual(2, s.Goals.Progress(0), "both bombs");
            Assert.AreEqual(3, s.Goals.Progress(1), "all three specials");
            Assert.AreEqual(1, s.Goals.Progress(2), "one combination, however big");
            Assert.IsTrue(s.Passed);
        }

        [Test]
        public void ThePreviewShowsExactlyWhatTheGroupHits()
        {
            var s = Board(9, 11, (4, 5, '-'), (5, 5, '*'), (5, 4, '*'));
            var preview = new List<GridPos>();
            s.PreviewSpecial(new GridPos(4, 5), -1, preview, out _, out bool combo);
            Assert.IsTrue(combo);
            var o = Play.Tap(s, 4, 5);
            CollectionAssert.AreEquivalent(preview, o.EffectCells.Select(e => e.Pos), "the preview is the blast");
            CollectionAssert.AreEquivalent(new HashSet<GridPos>(preview), Cleared(o), "and on a board of plain tiles, every one is cleared");
        }

        // ------------------------------------------------------------------ The end of the level

        [Test]
        public void AtTheEndAGroupOfThreeFiresAsOneCombination()
        {
            // The last move leaves a rocket, a rocket and a bomb connected: they go off together, then the result.
            var rows = new[] { "3301234", "1-|0123", "2*12340", "0123401" };
            var s = Play.Specials(rows, null, SpecialTier.Prisms, 1);
            Play.Tap(s, 0, 3);
            Assert.IsTrue(s.FinalePending);
            var o = new MoveOutcome();
            s.RunFinale(o);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(3, o.SpecialsFired[0].ComboSize);
            Assert.Greater(s.FinaleBreakdown.ComboBonus, 0);
            Assert.AreEqual(s.Breakdown.Finale, s.FinaleBreakdown.Total, "the summary's lines are the finale's points");
            Assert.AreEqual(s.FinaleBreakdown.Total, o.ScoreGained, "and exactly what the finale added");
            AssertOnce(o, s);
        }

        [Test]
        public void FinishingUsesTheSameGroups()
        {
            var rows = new[] { "3301234", "1-|0123", "2*12340", "0123401" };
            var s = Play.Specials(rows, null, SpecialTier.Prisms, 10, GoalSpec.Collect(3, 2));
            Play.Tap(s, 0, 3);
            Assert.IsTrue(s.Passed);
            var o = new MoveOutcome();
            s.Finish(o);
            Assert.AreEqual(1, o.SpecialsFired.Count);
            Assert.AreEqual(3, o.SpecialsFired[0].ComboSize);
            Assert.AreEqual(SessionState.Won, s.State);
            Assert.AreEqual(s.Score, s.FinalScore);
            Assert.AreEqual(System.Math.Max(1, StarRating.Compute(s.FinalScore, s.Level.Target, s.Level.Star2, s.Level.Star3)), s.Stars, "stars from the final score, specials included");
        }

        [Test]
        public void RandomBoardsFullOfSpecialsAlwaysResolve()
        {
            // 200 random boards where a third of the cells are specials: every tap, power-up and finale ends, fires each
            // special at most once and scores each tile at most once.
            var rng = new Rng(7);
            const string pieces = "-|*@";
            for (int game = 0; game < 200; game++)
            {
                var rows = new string[7];
                for (int r = 0; r < 7; r++)
                {
                    var chars = new char[7];
                    for (int c = 0; c < 7; c++) chars[c] = rng.Next(3) == 0 ? pieces[rng.Next(4)] : (char)('0' + rng.Next(4));
                    rows[r] = new string(chars);
                }
                var s = Play.Specials(rows, null, SpecialTier.Prisms, 3);
                var o = new MoveOutcome();
                for (int step = 0; step < 10 && s.AcceptsMoves; step++)
                {
                    GridPos pick = default;
                    bool found = false;
                    for (int i = 0; i < s.Board.CellCount && !found; i++)
                        if (s.Board.GetByIndex(i).Type == CellType.Special) { pick = s.Board.PosOf(i); found = true; }
                    if (!found)
                    {
                        var groups = new List<GroupInfo>();
                        if (new GroupFinder().FindAllGroups(s.Board, groups) == 0) break;
                        pick = groups[0].Anchor;
                    }
                    Assert.IsTrue(s.TryPlay(pick, o, rng.Next(4)));
                    AssertOnce(o, s);
                }
                if (s.FinalePending)
                {
                    s.RunFinale(o);
                    AssertOnce(o, s);
                    Assert.IsTrue(s.IsOver);
                }
            }
        }
    }
}
