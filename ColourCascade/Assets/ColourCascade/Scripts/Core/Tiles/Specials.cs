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
        /// <summary>Clears the 3 × 3 square around it.</summary>
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
        /// <summary>Points for each colour tile a special removes.</summary>
        public int pointsPerTile = 20;
        /// <summary>Points for each colour tile removed by two specials fired together.</summary>
        public int comboPointsPerTile = 40;

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

        /// <summary>Adds the cells one special at p reaches (not p itself). colour is used by prisms only.</summary>
        public static void Single(Board b, GridPos p, SpecialKind kind, int colour, List<GridPos> cells)
        {
            switch (kind)
            {
                case SpecialKind.RocketRow: Rows(b, p.Row, p.Row, cells); break;
                case SpecialKind.RocketColumn: Columns(b, p.Col, p.Col, cells); break;
                case SpecialKind.Bomb: Square(b, p, 1, cells); break;
                case SpecialKind.Prism: OfColour(b, colour, cells); break;
            }
            cells.Remove(p);
        }

        /// <summary>
        /// Adds the cells two specials fired together reach, centred on the tapped one at p (docs/MECHANICS.md §2.3).
        /// A prism pair is handled by the caller (the prism's colour first, then the other special alone),
        /// except prism + prism, which reaches every colour tile.
        /// </summary>
        public static void Combined(Board b, GridPos p, SpecialKind a, SpecialKind c, List<GridPos> cells)
        {
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
                Square(b, p, 2, cells);
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

        static void Square(Board b, GridPos p, int radius, List<GridPos> cells)
        {
            for (int r = p.Row - radius; r <= p.Row + radius; r++)
                for (int c = p.Col - radius; c <= p.Col + radius; c++)
                    if (b.InBounds(c, r)) cells.Add(new GridPos(c, r));
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
