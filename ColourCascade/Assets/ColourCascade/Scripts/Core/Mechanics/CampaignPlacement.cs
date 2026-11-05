using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>How a level's frozen tiles are laid out, which decides how best to free them.</summary>
    public enum IceStyle
    {
        /// <summary>Mirrored pairs spread over the board.</summary>
        Scatter,
        /// <summary>Whole rows of a section: a rocket frees a line at once.</summary>
        Lines,
        /// <summary>Compact blocks: a bomb's square frees a block.</summary>
        Blocks,
        /// <summary>Along the edges of gaps and stones, where tiles arrive from beneath a barrier.</summary>
        Ledges,
    }

    /// <summary>
    /// Obstacle and treasure placement for the campaign's generated levels from CampaignConfig.progressionFromLevel on
    /// (docs/DESIGN.md §3.5). It works on any board shape and only uses cells that can be played: an obstacle never sits
    /// in a dead-end pocket, a chained tile has room for a neighbour of its own colour, and treasure starts in a column
    /// it can fall down to its section's floor (the bottom edge, a stone or a gap). The Daily Challenge and Weekly Rush
    /// keep BoardDecorator, unchanged.
    /// </summary>
    public static class CampaignPlacement
    {
        /// <summary>An open cell that can take an obstacle: no piece or obstacle yet, not in the top row, with at least
        /// minOpen open neighbours.</summary>
        public static bool Fair(LevelLayout l, int c, int r, int minOpen)
        {
            return BoardDecorator.Free(l, c, r) && BoardShapes.OpenNeighbours(l, c, r) >= minOpen;
        }

        /// <summary>Neighbours that are open and carry no obstacle or piece yet (a chained tile needs one of its colour).</summary>
        static int PlainNeighbours(LevelLayout l, int c, int r)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
            {
                int nc = c + Board.DCol[d], nr = r + Board.DRow[d];
                if (l.IsOpen(nc, nr) && l.ObstacleAt(nc, nr) == ObstacleKind.None && l.PieceAt(nc, nr) == '\0') n++;
            }
            return n;
        }

        static void AddMirrored(LevelLayout l, List<GridPos> cells, int c, int r, int minOpen)
        {
            int m = l.Width - 1 - c;
            var a = new GridPos(c, r);
            var b = new GridPos(m, r);
            if (Fair(l, c, r, minOpen) && !cells.Contains(a)) cells.Add(a);
            if (Fair(l, m, r, minOpen) && !cells.Contains(b)) cells.Add(b);
        }

        public static int PlaceIce(LevelLayout l, int count, int thickPermille, IceStyle style, Rng rng)
        {
            int w = l.Width, h = l.Height;
            var cells = new List<GridPos>();
            switch (style)
            {
                case IceStyle.Lines:
                    {
                        // One or two rows, a few apart, across every section they cross.
                        int row = 1 + rng.Next(Math.Max(1, h / 2 - 1));
                        for (int k = 0; k < 3 && cells.Count < count; k++, row += 3)
                            for (int c = 0; c < w && cells.Count < count + 2; c++)
                                if (row < h - 1 && Fair(l, c, row, 2)) cells.Add(new GridPos(c, row));
                        break;
                    }
                case IceStyle.Blocks:
                    {
                        // 2 × 2 or 3 × 3 blocks, mirrored, inside sections.
                        int size = count >= 12 ? 3 : 2;
                        for (int guard = 0; guard < 40 && cells.Count < count; guard++)
                        {
                            int c0 = rng.Next(Math.Max(1, (w + 1) / 2 - size + 1)), r0 = 1 + rng.Next(Math.Max(1, h - size - 2));
                            bool ok = true;
                            for (int dc = 0; dc < size && ok; dc++)
                                for (int dr = 0; dr < size && ok; dr++)
                                    if (!Fair(l, c0 + dc, r0 + dr, 2) || cells.Contains(new GridPos(c0 + dc, r0 + dr))) ok = false;
                            if (!ok) continue;
                            for (int dc = 0; dc < size; dc++)
                                for (int dr = 0; dr < size; dr++) AddMirrored(l, cells, c0 + dc, r0 + dr, 2);
                        }
                        break;
                    }
                case IceStyle.Ledges:
                    {
                        // Cells right under a gap or stone, and beside gaps: where the board's shape matters most.
                        for (int r = 0; r < h - 1; r++)
                            for (int c = 0; c < w; c++)
                            {
                                if (!Fair(l, c, r, 2)) continue;
                                bool under = r + 1 < h && !l.IsOpen(c, r + 1);
                                bool beside = (c > 0 && !l.IsOpen(c - 1, r) && l.InBounds(c - 1, r)) || (c + 1 < w && !l.IsOpen(c + 1, r));
                                if (under || (beside && rng.ChancePermille(500))) cells.Add(new GridPos(c, r));
                            }
                        Shuffle(cells, rng);
                        break;
                    }
            }
            // Scattered mirrored pairs fill the rest (and are the Scatter style).
            for (int guard = 0; cells.Count < count && guard < 300; guard++)
                AddMirrored(l, cells, rng.Next(w), 1 + rng.Next(Math.Max(1, h - 2)), 2);
            int placed = 0;
            foreach (var p in cells)
            {
                if (placed >= count) break;
                if (!Fair(l, p.Col, p.Row, 2)) continue;
                l.SetObstacle(p.Col, p.Row, ObstacleKind.Ice, rng.ChancePermille(thickPermille) ? 2 : 1);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Chained tiles on cells with three open neighbours or more (a chain is broken by a group of its own colour, so
        /// it needs room for one); never two links or more in an edge column, and never next to another chain unless
        /// pairs are asked for (two chained tiles of one colour side by side free each other).
        /// </summary>
        public static int PlaceChains(LevelLayout l, int count, int maxLinks, bool pairs, Rng rng)
        {
            var cells = new List<GridPos>();
            for (int guard = 0; cells.Count < count && guard < 400; guard++)
            {
                int c = rng.Next(l.Width), r = 1 + rng.Next(Math.Max(1, l.Height - 3));
                if (!Fair(l, c, r, 3) || PlainNeighbours(l, c, r) < 3 || cells.Contains(new GridPos(c, r))) continue;
                bool touching = false;
                foreach (var p in cells)
                    if (Math.Abs(p.Col - c) + Math.Abs(p.Row - r) == 1) touching = true;
                if (touching && !pairs) continue;
                cells.Add(new GridPos(c, r));
                int m = l.Width - 1 - c;
                if (m != c && cells.Count < count && Fair(l, m, r, 3) && !cells.Contains(new GridPos(m, r))) cells.Add(new GridPos(m, r));
            }
            int placed = 0;
            foreach (var p in cells)
            {
                if (placed >= count) break;
                int links = 1 + rng.Next(Math.Max(1, maxLinks));
                if (p.Col == 0 || p.Col == l.Width - 1 || BoardShapes.OpenNeighbours(l, p.Col, p.Row) < 4) links = Math.Min(links, 2);
                l.SetObstacle(p.Col, p.Row, ObstacleKind.Chain, links);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// A cell where treasure can start: depth rows above its column section's floor, with every cell under it open and
        /// free of obstacles, and every tile under it with at least two cells it could be matched with (beside it or below
        /// it). The lowest one has nothing below, so it needs open cells on both sides: in an edge column, or beside a gap,
        /// the last tile under the treasure could only ever pair with one neighbour, and games stalled there.
        /// </summary>
        public static bool TreasureCell(LevelLayout l, int c, int r, int depth)
        {
            if (!l.IsOpen(c, r) || r >= l.Height - 1 || l.PieceAt(c, r) != '\0' || l.ObstacleAt(c, r) != ObstacleKind.None) return false;
            int floor = BoardShapes.SectionFloor(l, c, r);
            if (r - floor != depth) return false;
            for (int y = r - 1; y >= floor; y--)
            {
                if (l.ObstacleAt(c, y) != ObstacleKind.None || l.PieceAt(c, y) != '\0') return false;
                int partners = (l.IsOpen(c - 1, y) ? 1 : 0) + (l.IsOpen(c + 1, y) ? 1 : 0) + (y > floor ? 1 : 0);
                if (partners < 2) return false;
            }
            return true;
        }

        /// <summary>
        /// Treasure in different columns (and in different sections where the board has them), each depthMin..depthMax rows
        /// above the floor it falls to: deeper treasure takes more digging. Returns where it went.
        /// </summary>
        public static List<GridPos> PlaceTreasure(LevelLayout l, int count, int depthMin, int depthMax, Rng rng)
        {
            var options = new List<GridPos>();
            for (int c = 0; c < l.Width; c++)
                for (int r = 1; r < l.Height - 1; r++)
                {
                    int depth = r - BoardShapes.SectionFloor(l, c, r);
                    if (depth >= depthMin && depth <= depthMax && TreasureCell(l, c, r, depth)) options.Add(new GridPos(c, r));
                }
            Shuffle(options, rng);
            // Deeper first, so the level asks for the digging it was sized for.
            options.Sort((a, b) => (b.Row - BoardShapes.SectionFloor(l, b.Col, b.Row)).CompareTo(a.Row - BoardShapes.SectionFloor(l, a.Col, a.Row)));
            var sections = BoardShapes.Sections(l);
            var used = new List<int>();
            var placed = new List<GridPos>();
            for (int pass = 0; pass < 2 && placed.Count < count; pass++)
                foreach (var p in options)
                {
                    if (placed.Count >= count) break;
                    bool near = false;
                    foreach (var q in placed)
                        if (Math.Abs(q.Col - p.Col) <= 1) near = true;
                    if (near) continue;
                    int s = SectionOf(sections, p);
                    // First pass: one per section, so split boards ask for digging on each side.
                    if (pass == 0 && used.Contains(s) && sections.Count > 1) continue;
                    if (!TreasureCell(l, p.Col, p.Row, p.Row - BoardShapes.SectionFloor(l, p.Col, p.Row))) continue;
                    l.SetPiece(p.Col, p.Row, '$');
                    placed.Add(p);
                    used.Add(s);
                }
            return placed;
        }

        /// <summary>
        /// The treasure waits on an obstacle: a frozen or chained tile right under it holds it up until it is freed, so
        /// the level asks for the obstacle first (a dependency between goals). Returns how many were placed.
        /// </summary>
        public static int BlockTreasure(LevelLayout l, List<GridPos> treasure, ObstacleKind kind, int strength, Rng rng)
        {
            int placed = 0;
            foreach (var t in treasure)
            {
                int r = t.Row - 1;
                if (r < 0 || !l.IsOpen(t.Col, r) || l.ObstacleAt(t.Col, r) != ObstacleKind.None || l.PieceAt(t.Col, r) != '\0') continue;
                // A chain needs two plain neighbours besides the treasure to find its colour.
                if (kind == ObstacleKind.Chain && PlainNeighbours(l, t.Col, r) < 2) continue;
                if (BoardShapes.OpenNeighbours(l, t.Col, r) < 2) continue;
                l.SetObstacle(t.Col, r, kind, Math.Max(1, strength));
                placed++;
            }
            return placed;
        }

        static int SectionOf(List<List<GridPos>> sections, GridPos p)
        {
            for (int i = 0; i < sections.Count; i++)
                if (sections[i].Contains(p)) return i;
            return -1;
        }

        static void Shuffle<T>(List<T> list, Rng rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
