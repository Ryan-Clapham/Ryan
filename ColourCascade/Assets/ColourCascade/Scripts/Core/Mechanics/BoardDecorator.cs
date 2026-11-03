using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Places obstacles and treasure on a board layout, the same way for every mode (campaign recipes, Daily
    /// Challenge, Weekly Rush). Cells are chosen with mirror symmetry where the board allows, away from the top row
    /// (new tiles arrive there); treasure only in open interior columns; moss away from stones and edges. Each
    /// function returns how many it placed.
    /// </summary>
    public static class BoardDecorator
    {
        /// <summary>Open cells that can take an obstacle: open, no piece, no obstacle yet, not in the top row.</summary>
        public static bool Free(LevelLayout l, int c, int r)
        {
            return l.IsOpen(c, r) && r < l.Height - 1 && l.PieceAt(c, r) == '\0' && l.ObstacleAt(c, r) == ObstacleKind.None;
        }

        public static int PlaceIce(LevelLayout l, int count, int thickPermille, Rng rng)
        {
            int placed = 0;
            int w = l.Width, h = l.Height;
            int style = rng.Next(3);
            var cells = new List<GridPos>();
            if (style == 0)
            {
                // A band across the board, a few rows up.
                int row = 2 + rng.Next(Math.Max(1, h / 2 - 1));
                for (int k = 0; k < 2 && cells.Count < count; k++)
                    for (int c = 0; c < w; c++)
                        if (Free(l, c, row + k * 3)) cells.Add(new GridPos(c, row + k * 3));
            }
            else if (style == 1)
            {
                // Two mirrored 2 × 2 blocks, then more lower down.
                int[] rows = { h / 2, 2, h - 3 };
                foreach (int row in rows)
                    for (int dc = 0; dc < 2; dc++)
                        for (int dr = 0; dr < 2; dr++)
                        {
                            AddMirrored(l, cells, 1 + dc, row + dr);
                        }
            }
            // Scattered mirrored pairs fill the rest (and are style 2).
            for (int guard = 0; cells.Count < count && guard < 200; guard++)
                AddMirrored(l, cells, rng.Next(w), 1 + rng.Next(Math.Max(1, h - 2)));
            foreach (var p in cells)
            {
                if (placed >= count) break;
                if (!Free(l, p.Col, p.Row)) continue;
                l.SetObstacle(p.Col, p.Row, ObstacleKind.Ice, rng.ChancePermille(thickPermille) ? 2 : 1);
                placed++;
            }
            return placed;
        }

        public static void AddMirrored(LevelLayout l, List<GridPos> cells, int c, int r)
        {
            int m = l.Width - 1 - c;
            if (Free(l, c, r) && !cells.Contains(new GridPos(c, r))) cells.Add(new GridPos(c, r));
            if (Free(l, m, r) && !cells.Contains(new GridPos(m, r))) cells.Add(new GridPos(m, r));
        }

        public static int PlaceChains(LevelLayout l, int count, int maxLinks, Rng rng)
        {
            var cells = new List<GridPos>();
            for (int guard = 0; cells.Count < count && guard < 200; guard++)
                AddMirrored(l, cells, rng.Next(l.Width), 1 + rng.Next(Math.Max(1, l.Height - 3)));
            int placed = 0;
            foreach (var p in cells)
            {
                if (placed >= count) break;
                if (!Free(l, p.Col, p.Row)) continue;
                l.SetObstacle(p.Col, p.Row, ObstacleKind.Chain, 1 + rng.Next(Math.Max(1, maxLinks)));
                placed++;
            }
            return placed;
        }

        /// <summary>A column where treasure at row r can reach the floor fairly: open all the way down (to the bottom edge
        /// or a stone), with an open neighbour beside every cell below it, so no cell under it is a dead-end pocket.</summary>
        public static bool FairTreasureColumn(LevelLayout l, int c, int r)
        {
            // Not the edge columns: the last tile under the treasure would have only one neighbour that could match it.
            if (c <= 0 || c >= l.Width - 1) return false;
            // Open all the way to the bottom edge, with open columns either side: no stone cups or pockets.
            for (int y = r - 1; y >= 0; y--)
            {
                if (!l.IsOpen(c, y) || l.ObstacleAt(c, y) != ObstacleKind.None) return false;
                if (!l.IsOpen(c - 1, y) || !l.IsOpen(c + 1, y)) return false;
            }
            return true;
        }

        public static int PlaceTreasure(LevelLayout l, int count, Rng rng)
        {
            // Treasure starts in different columns, a few rows up, so the player has to dig a path for each.
            var columns = new List<int>();
            for (int c = 0; c < l.Width; c++) columns.Add(c);
            for (int i = columns.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = columns[i];
                columns[i] = columns[j];
                columns[j] = t;
            }
            int placed = 0;
            foreach (int c in columns)
            {
                if (placed >= count) break;
                for (int r = l.Height - 5; r >= l.Height - 7 && r >= 1; r--)
                {
                    if (!l.IsOpen(c, r) || l.PieceAt(c, r) != '\0' || l.ObstacleAt(c, r) != ObstacleKind.None || l.RestsOnPermanentFloor(c, r)) continue;
                    if (!FairTreasureColumn(l, c, r)) continue;
                    l.SetPiece(c, r, '$');
                    placed++;
                    break;
                }
            }
            return placed;
        }

        public static int PlaceMoss(LevelLayout l, int seeds, Rng rng)
        {
            // Moss starts in one or two patches low on the board, on either side.
            var cells = new List<GridPos>();
            bool both = rng.Next(2) == 0;
            int baseRow = 1 + rng.Next(2);
            int[,] patch = { { 1, 0 }, { 2, 0 }, { 1, 1 }, { 2, 1 }, { 1, 2 } };
            for (int k = 0; k < patch.GetLength(0) && cells.Count < seeds; k++)
            {
                int c = patch[k, 0], r = baseRow + patch[k, 1];
                if (Free(l, c, r) && Roomy(l, c, r)) cells.Add(new GridPos(c, r));
                if (both && cells.Count < seeds && Free(l, l.Width - 1 - c, r) && Roomy(l, l.Width - 1 - c, r)) cells.Add(new GridPos(l.Width - 1 - c, r));
            }
            foreach (var p in cells) l.SetObstacle(p.Col, p.Row, ObstacleKind.Moss, 1);
            return cells.Count;
        }

        /// <summary>
        /// Campaign moss: separate patches of one or two tiles spread over the lower two thirds of the board, never
        /// touching each other, so each takes its own clear (a single compact patch went in a move or two).
        /// </summary>
        public static int PlaceMossSpread(LevelLayout l, int seeds, Rng rng)
        {
            var cells = new List<GridPos>();
            int top = Math.Max(2, l.Height * 2 / 3);
            for (int guard = 0; guard < 400 && cells.Count < seeds; guard++)
            {
                int c = rng.Next(l.Width), r = 1 + rng.Next(Math.Max(1, top - 1));
                if (!Free(l, c, r) || !Roomy(l, c, r)) continue;
                bool near = false;
                foreach (var p in cells)
                    if (Math.Abs(p.Col - c) + Math.Abs(p.Row - r) < 3) near = true;
                if (near) continue;
                cells.Add(new GridPos(c, r));
            }
            foreach (var p in cells) l.SetObstacle(p.Col, p.Row, ObstacleKind.Moss, 1);
            return cells.Count;
        }

        /// <summary>All four neighbours are open cells (no stone, void or edge), so the tile there can always be matched.</summary>
        public static bool Roomy(LevelLayout l, int c, int r)
        {
            for (int d = 0; d < 4; d++)
                if (!l.IsOpen(c + Board.DCol[d], r + Board.DRow[d])) return false;
            return true;
        }

    }
}
