using System.Collections.Generic;

namespace ColourCascade.Core
{
    public struct GroupInfo
    {
        public GridPos Anchor;
        public int Size;
        public int Colour;
    }

    /// <summary>
    /// Finds orthogonally connected groups of same-coloured tiles. Diagonal contact does not connect. Which tiles
    /// take part is decided by TileRules.JoinsGroups (free tiles and chained tiles; not frozen ones).
    /// Reuses its internal buffers, so repeated searches do not allocate.
    /// </summary>
    public sealed class GroupFinder
    {
        int[] stamp = new int[0];
        int[] stack = new int[0];
        int generation;

        void Ensure(int n)
        {
            if (stamp.Length < n)
            {
                stamp = new int[n];
                stack = new int[n];
                generation = 0;
            }
        }

        int NextGeneration(int n)
        {
            Ensure(n);
            generation++;
            if (generation == int.MaxValue)
            {
                System.Array.Clear(stamp, 0, stamp.Length);
                generation = 1;
            }
            return generation;
        }

        /// <summary>Fills <paramref name="result"/> with the group containing start. Returns its size (0 if start cannot join a group).</summary>
        public int FindGroup(Board board, GridPos start, List<GridPos> result)
        {
            result?.Clear();
            if (!board.InBounds(start)) return 0;
            var first = board[start];
            if (!TileRules.JoinsGroups(first)) return 0;
            return Flood(board, board.Index(start.Col, start.Row), first.Colour, NextGeneration(board.CellCount), result);
        }

        public int GroupSize(Board board, GridPos start)
        {
            return FindGroup(board, start, null);
        }

        int Flood(Board board, int startIndex, int colour, int gen, List<GridPos> result)
        {
            int w = board.Width;
            int top = 0;
            stack[top++] = startIndex;
            stamp[startIndex] = gen;
            int size = 0;
            while (top > 0)
            {
                int idx = stack[--top];
                size++;
                int col = idx % w, row = idx / w;
                result?.Add(new GridPos(col, row));
                for (int d = 0; d < 4; d++)
                {
                    int nc = col + Board.DCol[d], nr = row + Board.DRow[d];
                    if (!board.InBounds(nc, nr)) continue;
                    int ni = nr * w + nc;
                    if (stamp[ni] == gen) continue;
                    var t = board.GetByIndex(ni);
                    if (t.Colour != colour || !TileRules.JoinsGroups(t)) continue;
                    stamp[ni] = gen;
                    stack[top++] = ni;
                }
            }
            return size;
        }

        /// <summary>Lists every group of at least minSize tiles (one entry per group).</summary>
        public int FindAllGroups(Board board, List<GroupInfo> groups, int minSize = 2)
        {
            groups.Clear();
            int gen = NextGeneration(board.CellCount);
            for (int i = 0; i < board.CellCount; i++)
            {
                if (stamp[i] == gen) continue;
                var t = board.GetByIndex(i);
                if (!TileRules.JoinsGroups(t)) continue;
                int size = Flood(board, i, t.Colour, gen, null);
                if (size >= minSize)
                    groups.Add(new GroupInfo { Anchor = board.PosOf(i), Size = size, Colour = t.Colour });
            }
            return groups.Count;
        }

        /// <summary>True if at least one group of two or more matching tiles exists (docs/MECHANICS.md §3.3).</summary>
        public static bool HasLegalMove(Board board)
        {
            int w = board.Width, h = board.Height;
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    var t = board[c, r];
                    if (!TileRules.JoinsGroups(t)) continue;
                    if (c + 1 < w)
                    {
                        var n = board[c + 1, r];
                        if (n.Colour == t.Colour && TileRules.JoinsGroups(n)) return true;
                    }
                    if (r + 1 < h)
                    {
                        var n = board[c, r + 1];
                        if (n.Colour == t.Colour && TileRules.JoinsGroups(n)) return true;
                    }
                }
            }
            return false;
        }
    }
}
