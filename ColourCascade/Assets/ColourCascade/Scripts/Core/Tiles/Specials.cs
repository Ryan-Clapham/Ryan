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
        /// <summary>Clears the 5 × 5 square centred on it, cut off at the board's edges (SpecialRules.bombSize).</summary>
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
        /// <summary>
        /// Side of a bomb's square, centred on the bomb (it was 3, then 4 in early 2.1 builds). Odd sizes are centred
        /// exactly and cut off at the board's edges; an even size has no centre cell, so its extra row and column lean
        /// towards the middle of the board.
        /// </summary>
        public int bombSize = 5;
        /// <summary>Combinations (docs/MECHANICS.md §2.3): each extra bomb widens a square or a band by this many
        /// cells (a square of 5 becomes 7, a band of 1 becomes 3), and each extra rocket widens a cross the same way.</summary>
        public int comboWidening = 2;

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
                case SpecialKind.Bomb: Square(b, p, rules.bombSize, cells); break;
                case SpecialKind.Prism: OfColour(b, colour, cells); break;
            }
            cells.Remove(p);
        }

        /// <summary>
        /// The connected group of special tiles containing p: every special reachable from p through orthogonally
        /// adjacent specials (docs/MECHANICS.md §2.3). p comes first, then the others in breadth-first order (right,
        /// left, up, down). Not a special: empty.
        /// </summary>
        public static void FindCluster(Board b, GridPos p, List<GridPos> members)
        {
            members.Clear();
            if (!b.InBounds(p) || b[p].Type != CellType.Special) return;
            members.Add(p);
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                for (int d = 0; d < 4; d++)
                {
                    var n = new GridPos(m.Col + Board.DCol[d], m.Row + Board.DRow[d]);
                    if (b.InBounds(n) && b[n].Type == CellType.Special && !members.Contains(n)) members.Add(n);
                }
            }
        }

        /// <summary>
        /// The cells a group of specials fired together reaches (docs/MECHANICS.md §2.3), with the group's tapped (or
        /// leading) special at members[0]; the members' own cells are left out. One rule set covers groups of any size:
        /// prisms first (one clears the chosen colour, two or more every colour tile), then the rockets and bombs fire
        /// together from the lead (or, when the lead is a prism, from the first rocket or bomb in the group): a lone
        /// one fires as itself; bombs alone make a square of bombSize + 2 per extra bomb; rockets alone a cross (row and
        /// column) one line wide, + 2 lines per rocket beyond two; one rocket with bombs a band in the rocket's
        /// direction 1 + 2 per bomb wide; several rockets with bombs a cross 1 + 2 per bomb + 2 per rocket beyond two
        /// wide. Two specials give exactly the old pair combinations (rocket + rocket a cross, rocket + bomb three
        /// lines, prism + rocket or bomb the colour then that special, prism + prism everything; bomb + bomb a square
        /// two cells bigger than one bomb's).
        /// </summary>
        public static void ClusterArea(Board b, IList<GridPos> members, int colour, SpecialRules rules, List<GridPos> cells)
        {
            rules = rules ?? Defaults;
            if (members == null || members.Count == 0) return;
            if (members.Count == 1)
            {
                Single(b, members[0], b[members[0]].Special, colour, cells, rules);
                return;
            }
            int prisms = 0, bombs = 0, rockets = 0;
            GridPos centre = members[0];
            bool centreFound = b[members[0]].Special != SpecialKind.Prism;
            SpecialKind loneRocket = SpecialKind.None;
            for (int i = 0; i < members.Count; i++)
            {
                var k = b[members[i]].Special;
                if (k == SpecialKind.Prism)
                {
                    prisms++;
                    continue;
                }
                if (k == SpecialKind.Bomb) bombs++;
                else if (IsRocket(k))
                {
                    rockets++;
                    loneRocket = k;
                }
                if (!centreFound)
                {
                    centre = members[i];
                    centreFound = true;
                }
            }
            if (prisms >= 2)
            {
                for (int i = 0; i < b.CellCount; i++)
                    if (b.GetByIndex(i).Type == CellType.Colour) cells.Add(b.PosOf(i));
            }
            else if (prisms == 1) OfColour(b, colour, cells);
            int w = Math.Max(0, rules.comboWidening);
            int others = bombs + rockets;
            if (others == 1) Single(b, centre, bombs == 1 ? SpecialKind.Bomb : loneRocket, colour, cells, rules);
            else if (others > 1)
            {
                if (rockets == 0) Square(b, centre, rules.bombSize + w * (bombs - 1), cells);
                else if (bombs == 0) Cross(b, centre, 1 + w * (rockets - 2), cells);
                else if (rockets == 1)
                {
                    int half = w * bombs / 2;
                    if (loneRocket == SpecialKind.RocketRow) Rows(b, centre.Row - half, centre.Row + half, cells);
                    else Columns(b, centre.Col - half, centre.Col + half, cells);
                }
                else Cross(b, centre, 1 + w * bombs + w * (rockets - 2), cells);
            }
            Dedupe(cells);
            for (int i = 0; i < members.Count; i++) cells.Remove(members[i]);
        }

        /// <summary>A row and a column through p, each width lines wide (odd widths centred on p).</summary>
        static void Cross(Board b, GridPos p, int width, List<GridPos> cells)
        {
            int half = Math.Max(0, width - 1) / 2;
            Rows(b, p.Row - half, p.Row + half, cells);
            Columns(b, p.Col - half, p.Col + half, cells);
        }

        /// <summary>Kept for two specials (callers from before groups existed): the pair's area, tapped one first.</summary>
        public static void Combined(Board b, GridPos p, SpecialKind a, SpecialKind c, List<GridPos> cells, SpecialRules rules = null)
        {
            GridPos partner;
            if (!FindPartner(b, p, out partner)) return;
            ClusterArea(b, new[] { p, partner }, MostCommonColour(b), rules, cells);
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
        /// A size × size square on p: odd sizes centred exactly on p and cut off at the board's edges (cells off the
        /// board are never touched); even sizes have no centre cell and use Block.
        /// </summary>
        public static void Square(Board b, GridPos p, int size, List<GridPos> cells)
        {
            if (size % 2 == 0)
            {
                Block(b, p, size, cells);
                return;
            }
            int half = size / 2;
            for (int r = p.Row - half; r <= p.Row + half; r++)
                for (int c = p.Col - half; c <= p.Col + half; c++)
                    if (b.InBounds(c, r)) cells.Add(new GridPos(c, r));
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
            var members = new List<GridPos>();
            FindCluster(b, p, members);
            ClusterArea(b, members, MostCommonColour(b), null, scratch);
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
