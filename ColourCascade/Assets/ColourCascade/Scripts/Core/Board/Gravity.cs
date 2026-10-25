using System.Collections.Generic;

namespace ColourCascade.Core
{
    public struct TileMove
    {
        public int TileId;
        public GridPos From;
        public GridPos To;
    }

    public struct TileSpawn
    {
        public int TileId;
        public int Colour;
        public GridPos At;
        /// <summary>0 for the lowest new tile in its section, 1 for the next one up, and so on.</summary>
        public int Order;
        /// <summary>Highest cell of the section this tile was spawned into.</summary>
        public GridPos SectionTop;
        /// <summary>True when the section is open to the top of the board, so the tile can drop in from above.
        /// False when the section sits under a barrier, so the tile appears from beneath it.</summary>
        public bool FromBoardTop;
    }

    /// <summary>Supplies colours for newly created tiles.</summary>
    public interface ITileSource
    {
        int NextColour(Board board, int col, int row);
    }

    /// <summary>
    /// Gravity and refill. Each column is divided into independent sections by barriers (stones or voids).
    /// Tiles fall to the bottom of their own section, and each section refills from its own top.
    /// </summary>
    public static class Gravity
    {
        public static void Apply(Board board, ITileSource source, List<TileMove> moves, List<TileSpawn> spawns, bool refill = true)
        {
            int w = board.Width, h = board.Height;
            for (int c = 0; c < w; c++)
            {
                int r = 0;
                while (r < h)
                {
                    if (board[c, r].IsBarrier)
                    {
                        r++;
                        continue;
                    }
                    int start = r;
                    int end = r;
                    while (end + 1 < h && !board[c, end + 1].IsBarrier) end++;

                    int write = start;
                    for (int i = start; i <= end; i++)
                    {
                        var t = board[c, i];
                        if (t.Type != CellType.Colour) continue;
                        if (i != write)
                        {
                            board[c, write] = t;
                            board[c, i] = default;
                            moves?.Add(new TileMove { TileId = t.Id, From = new GridPos(c, i), To = new GridPos(c, write) });
                        }
                        write++;
                    }

                    for (int i = write; i <= end; i++)
                    {
                        if (!refill)
                        {
                            board[c, i] = default;
                            continue;
                        }
                        int colour = source.NextColour(board, c, i);
                        var tile = board.NewColourTile(colour);
                        board[c, i] = tile;
                        spawns?.Add(new TileSpawn
                        {
                            TileId = tile.Id,
                            Colour = colour,
                            At = new GridPos(c, i),
                            Order = i - write,
                            SectionTop = new GridPos(c, end),
                            FromBoardTop = end == h - 1,
                        });
                    }
                    r = end + 1;
                }
            }
        }
    }

    /// <summary>
    /// Weighted colour source with an optional "assist" that sometimes copies a neighbouring colour,
    /// which makes larger groups more likely (used to keep early levels gentle).
    /// All maths is integer-based so results are identical on every platform.
    /// </summary>
    public sealed class TileSource : ITileSource
    {
        readonly Rng rng;
        readonly int[] weights = new int[TileColours.Max];
        readonly int[] candidates = new int[4];
        int total;

        public int ColourCount { get; private set; }
        public int AssistPermille;

        public TileSource(Rng rng, int colourCount, int[] colourWeights = null, int assistPermille = 0)
        {
            this.rng = rng;
            AssistPermille = assistPermille;
            Configure(colourCount, colourWeights);
        }

        public Rng Random => rng;

        public void Configure(int colourCount, int[] colourWeights = null)
        {
            if (colourCount < 1) colourCount = 1;
            if (colourCount > TileColours.Max) colourCount = TileColours.Max;
            ColourCount = colourCount;
            total = 0;
            for (int i = 0; i < TileColours.Max; i++)
            {
                int wgt = 0;
                if (i < colourCount)
                {
                    wgt = colourWeights != null && i < colourWeights.Length ? colourWeights[i] : 10;
                    if (wgt < 1) wgt = 1;
                }
                weights[i] = wgt;
                total += wgt;
            }
        }

        public int Roll()
        {
            int x = rng.Next(total);
            for (int i = 0; i < ColourCount; i++)
            {
                x -= weights[i];
                if (x < 0) return i;
            }
            return ColourCount - 1;
        }

        public int NextColour(Board board, int col, int row)
        {
            if (AssistPermille > 0 && rng.ChancePermille(AssistPermille))
            {
                int n = 0;
                for (int d = 0; d < 4; d++)
                {
                    int nc = col + Board.DCol[d], nr = row + Board.DRow[d];
                    if (!board.IsColourAt(nc, nr)) continue;
                    int colour = board[nc, nr].Colour;
                    if (colour < ColourCount) candidates[n++] = colour;
                }
                if (n > 0) return candidates[rng.Next(n)];
            }
            return Roll();
        }
    }
}
