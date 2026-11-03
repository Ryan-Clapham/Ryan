using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>A special tile, earned by clearing a big group (docs/MECHANICS.md §2.3). Values are stable.</summary>
    public enum SpecialKind : byte
    {
        None = 0,
        /// <summary>Clears its whole row.</summary>
        RocketRow = 1,
        /// <summary>Clears its whole column.</summary>
        RocketColumn = 2,
        /// <summary>Clears the 4 × 4 square around it (SpecialRules.bombSize).</summary>
        Bomb = 3,
        /// <summary>Clears every tile of a colour the player picks.</summary>
        Prism = 4,
    }

    /// <summary>Which specials a level's group clears may create. Ordered: each tier includes the ones before it.</summary>
    public enum SpecialTier : byte
    {
        None = 0,
        Rockets = 1,
        Bombs = 2,
        Prisms = 3,
    }

    /// <summary>Thresholds and points for special tiles. Part of the scoring section of the config.</summary>
    [Serializable]
    public class SpecialRules
    {
        /// <summary>Smallest group that creates a rocket.</summary>
        public int rocketGroupSize = 6;
        /// <summary>Smallest group that creates a bomb.</summary>
        public int bombGroupSize = 9;
        /// <summary>Smallest group that creates a prism.</summary>
        public int prismGroupSize = 12;
        /// <summary>
        /// Points for each colour tile a special removes (docs/MECHANICS.md §5): what the special actually reaches, so a
        /// rocket on a longer row scores more and holes, stones and obstacles in its way score nothing as tiles.
        /// Obstacles and stones it hits score their own points on top. 50 = a tile's share of the 6-tile group that
        /// makes a rocket (it was 20: a rocket across 8 tiles was worth a group of 4).
        /// </summary>
        public int pointsPerTile = 50;
        /// <summary>Points for each colour tile removed by two specials fired together (1.5 × a single special).</summary>
        public int comboPointsPerTile = 75;
        /// <summary>Side of a bomb's square (it was 3).</summary>
        public int bombSize = 4;
        /// <summary>Side of the square two bombs fired together reach.</summary>
        public int bombComboSize = 6;

        /// <summary>The special a group of this size earns, or None. A level only gets the kinds its tier allows;
        /// a big group earns the strongest allowed kind it qualifies for.</summary>
        public SpecialKind KindFor(int groupSize, SpecialTier tier, bool spansMoreColumnsThanRows)
        {
            if (tier >= SpecialTier.Prisms && groupSize >= prismGroupSize) return SpecialKind.Prism;
            if (tier >= SpecialTier.Bombs && groupSize >= bombGroupSize) return SpecialKind.Bomb;
            if (tier >= SpecialTier.Rockets && groupSize >= rocketGroupSize)
                return spansMoreColumnsThanRows ? SpecialKind.RocketRow : SpecialKind.RocketColumn;
            return SpecialKind.None;
        }
    }

    /// <summary>The cells each special (or pair fired together) reaches. Pure geometry; the session applies the hits.</summary>
    public static class SpecialEffects
    {
        public static bool IsRocket(SpecialKind k) => k == SpecialKind.RocketRow || k == SpecialKind.RocketColumn;

        /// <summary>Strength order used to pick a combination partner: prism, bomb, rocket.</summary>
        public static int Rank(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.Prism: return 3;
                case SpecialKind.Bomb: return 2;
                case SpecialKind.RocketRow:
                case SpecialKind.RocketColumn: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// The special next to p (orthogonally) that fires together with the one at p, or false. With several, the
        /// strongest wins (prism, bomb, rocket); ties go to the first in the order right, left, up, down.
        /// </summary>
        public static bool FindPartner(Board b, GridPos p, out GridPos partner)
        {
            partner = default;
            int best = 0;
            for (int d = 0; d < 4; d++)
            {
                var n = new GridPos(p.Col + Board.DCol[d], p.Row + Board.DRow[d]);
                if (!b.InBounds(n) || b[n].Type != CellType.Special) continue;
                int rank = Rank(b[n].Special);
                if (rank > best)
                {
                    best = rank;
                    partner = n;
                }
            }
            return best > 0;
        }

        /// <summary>Default sizes (SpecialRules) for callers without the scoring rules at hand.</summary>
        static readonly SpecialRules Defaults = new SpecialRules();

        /// <summary>Adds the cells one special at p reaches (not p itself). colour is used by prisms only.</summary>
        public static void Single(Board b, GridPos p, SpecialKind kind, int colour, List<GridPos> cells, SpecialRules rules = null)
        {
            rules = rules ?? Defaults;
            switch (kind)
            {
                case SpecialKind.RocketRow: Rows(b, p.Row, p.Row, cells); break;
                case SpecialKind.RocketColumn: Columns(b, p.Col, p.Col, cells); break;
                case SpecialKind.Bomb: Block(b, p, rules.bombSize, cells); break;
                case SpecialKind.Prism: OfColour(b, colour, cells); break;
            }
            cells.Remove(p);
        }

        /// <summary>
        /// Adds the cells two specials fired together reach, centred on the tapped one at p (docs/MECHANICS.md §2.3).
        /// A prism pair is handled by the caller (the prism's colour first, then the other special alone),
        /// except prism + prism, which reaches every colour tile.
        /// </summary>
        public static void Combined(Board b, GridPos p, SpecialKind a, SpecialKind c, List<GridPos> cells, SpecialRules rules = null)
        {
            rules = rules ?? Defaults;
            if (a == SpecialKind.Prism && c == SpecialKind.Prism)
            {
                for (int i = 0; i < b.CellCount; i++)
                    if (b.GetByIndex(i).Type == CellType.Colour) cells.Add(b.PosOf(i));
                return;
            }
            if (IsRocket(a) && IsRocket(c))
            {
                Rows(b, p.Row, p.Row, cells);
                Columns(b, p.Col, p.Col, cells);
            }
            else if (a == SpecialKind.Bomb && c == SpecialKind.Bomb)
            {
                Block(b, p, rules.bombComboSize, cells);
            }
            else
            {
                var rocket = IsRocket(a) ? a : c;
                if (rocket == SpecialKind.RocketRow) Rows(b, p.Row - 1, p.Row + 1, cells);
                else Columns(b, p.Col - 1, p.Col + 1, cells);
            }
            Dedupe(cells);
        }

        static void Rows(Board b, int from, int to, List<GridPos> cells)
        {
            for (int r = Math.Max(0, from); r <= Math.Min(b.Height - 1, to); r++)
                for (int c = 0; c < b.Width; c++) cells.Add(new GridPos(c, r));
        }

        static void Columns(Board b, int from, int to, List<GridPos> cells)
        {
            for (int c = Math.Max(0, from); c <= Math.Min(b.Width - 1, to); c++)
                for (int r = 0; r < b.Height; r++) cells.Add(new GridPos(c, r));
        }

        /// <summary>
        /// The size × size square around p: centred when size is odd; when it is even, the extra row and column lie
        /// towards the middle of the board. Near an edge the square slides inwards to stay whole, so a bomb reaches
        /// the same number of cells wherever it sits (all of a board smaller than the square).
        /// </summary>
        public static void Block(Board b, GridPos p, int size, List<GridPos> cells)
        {
            int c0 = BlockStart(p.Col, b.Width, size), r0 = BlockStart(p.Row, b.Height, size);
            for (int r = r0; r < r0 + size && r < b.Height; r++)
                for (int c = c0; c < c0 + size && c < b.Width; c++)
                    cells.Add(new GridPos(c, r));
        }

        /// <summary>First column (or row) of a size-wide span containing pos, leaning to the middle and kept on the board.</summary>
        public static int BlockStart(int pos, int extent, int size)
        {
            if (size >= extent) return 0;
            int before = (size - 1) / 2;
            // Even sizes have one more cell on one side: put it on the side towards the middle of the board.
            if (size % 2 == 0 && pos * 2 >= extent) before = size / 2;
            return Math.Max(0, Math.Min(extent - size, pos - before));
        }

        static void OfColour(Board b, int colour, List<GridPos> cells)
        {
            for (int i = 0; i < b.CellCount; i++)
            {
                var t = b.GetByIndex(i);
                if (t.Type == CellType.Colour && t.Colour == colour) cells.Add(b.PosOf(i));
            }
        }

        static void Dedupe(List<GridPos> cells)
        {
            var seen = new HashSet<GridPos>();
            int w = 0;
            for (int i = 0; i < cells.Count; i++)
                if (seen.Add(cells[i])) cells[w++] = cells[i];
            cells.RemoveRange(w, cells.Count - w);
        }

        /// <summary>How many colour tiles a tap on the special at p would reach directly (prisms take the most common
        /// colour). Used by the simple bots to compare a special with the biggest group.</summary>
        public static int EstimateReach(Board b, GridPos p, List<GridPos> scratch)
        {
            scratch.Clear();
            if (!b.InBounds(p) || b[p].Type != CellType.Special) return 0;
            var kind = b[p].Special;
            int colour = MostCommonColour(b);
            GridPos partner;
            if (FindPartner(b, p, out partner))
            {
                var other = b[partner].Special;
                if ((kind == SpecialKind.Prism) != (other == SpecialKind.Prism))
                {
                    Single(b, p, SpecialKind.Prism, colour, scratch);
                    if (kind == SpecialKind.Prism) Single(b, partner, other, colour, scratch);
                    else Single(b, p, kind, colour, scratch);
                }
                else Combined(b, p, kind, other, scratch);
            }
            else Single(b, p, kind, colour, scratch);
            int n = 0;
            for (int i = 0; i < scratch.Count; i++)
                if (b[scratch[i]].Type == CellType.Colour) n++;
            return n;
        }

        /// <summary>The colour with the most colour tiles on the board (lowest index on a tie); -1 if none.
        /// Used when a prism is set off by another effect and nobody can choose.</summary>
        public static int MostCommonColour(Board b)
        {
            var counts = new int[TileColours.Max];
            for (int i = 0; i < b.CellCount; i++)
            {
                var t = b.GetByIndex(i);
                if (t.Type == CellType.Colour && t.Colour >= 0 && t.Colour < counts.Length) counts[t.Colour]++;
            }
            int best = -1;
            for (int c = 0; c < counts.Length; c++)
                if (counts[c] > 0 && (best < 0 || counts[c] > counts[best])) best = c;
            return best;
        }
    }
}
