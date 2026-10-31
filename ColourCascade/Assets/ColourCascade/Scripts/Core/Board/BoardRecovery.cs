using System.Collections.Generic;

namespace ColourCascade.Core
{
    public enum RecoveryResult
    {
        /// <summary>The board already had a legal move.</summary>
        NotNeeded,
        /// <summary>Tiles were rearranged and the board now has at least one legal move.</summary>
        Shuffled,
        /// <summary>No arrangement of the existing tiles can produce a legal move. The board is left unchanged.</summary>
        Failed,
    }

    /// <summary>
    /// Dead-board recovery. Rearranges the existing free colour tiles (barriers and every tile under an obstacle
    /// keep their cells, TileRules.CanShuffle) until a legal move exists. Tries a bounded number of random shuffles, then falls back to a constructive arrangement that
    /// is guaranteed to work whenever recovery is possible at all. Never loops indefinitely and never touches
    /// score, moves or progress.
    /// </summary>
    public static class BoardRecovery
    {
        public const int DefaultRandomAttempts = 12;

        public static RecoveryResult EnsurePlayable(Board board, Rng rng, List<TileMove> moves, int randomAttempts = DefaultRandomAttempts)
        {
            moves?.Clear();
            if (GroupFinder.HasLegalMove(board)) return RecoveryResult.NotNeeded;

            var positions = new List<GridPos>();
            var original = new List<Tile>();
            for (int r = 0; r < board.Height; r++)
            {
                for (int c = 0; c < board.Width; c++)
                {
                    var t = board[c, r];
                    if (!TileRules.CanShuffle(t)) continue;
                    positions.Add(new GridPos(c, r));
                    original.Add(t);
                }
            }
            if (positions.Count < 2) return RecoveryResult.Failed;

            var working = new List<Tile>(original);
            for (int attempt = 0; attempt < randomAttempts; attempt++)
            {
                rng.Shuffle(working);
                Assign(board, positions, working);
                if (GroupFinder.HasLegalMove(board))
                {
                    RecordMoves(positions, original, working, moves);
                    return RecoveryResult.Shuffled;
                }
            }

            // Constructive fallback: put two tiles of the most common colour on an adjacent pair of cells.
            var counts = new int[TileColours.Max + 1];
            int bestColour = -1;
            for (int i = 0; i < original.Count; i++)
            {
                int colour = original[i].Colour;
                if (colour < 0 || colour >= counts.Length) continue;
                counts[colour]++;
                if (bestColour < 0 || counts[colour] > counts[bestColour]) bestColour = colour;
            }
            var pairs = new List<int>();
            var indexOf = new Dictionary<GridPos, int>();
            for (int i = 0; i < positions.Count; i++) indexOf[positions[i]] = i;
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                int j;
                if (indexOf.TryGetValue(new GridPos(p.Col + 1, p.Row), out j)) { pairs.Add(i); pairs.Add(j); }
                if (indexOf.TryGetValue(new GridPos(p.Col, p.Row + 1), out j)) { pairs.Add(i); pairs.Add(j); }
            }

            if (bestColour < 0 || counts[bestColour] < 2 || pairs.Count == 0)
            {
                Assign(board, positions, original);
                return RecoveryResult.Failed;
            }

            int pick = rng.Next(pairs.Count / 2) * 2;
            int slotA = pairs[pick], slotB = pairs[pick + 1];
            var rest = new List<Tile>(original.Count);
            var chosen = new List<Tile>(2);
            for (int i = 0; i < original.Count; i++)
            {
                if (chosen.Count < 2 && original[i].Colour == bestColour) chosen.Add(original[i]);
                else rest.Add(original[i]);
            }
            rng.Shuffle(rest);
            int k = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == slotA) working[i] = chosen[0];
                else if (i == slotB) working[i] = chosen[1];
                else working[i] = rest[k++];
            }
            Assign(board, positions, working);
            if (!GroupFinder.HasLegalMove(board))
            {
                Assign(board, positions, original);
                return RecoveryResult.Failed;
            }
            RecordMoves(positions, original, working, moves);
            return RecoveryResult.Shuffled;
        }

        static void Assign(Board board, List<GridPos> positions, List<Tile> tiles)
        {
            for (int i = 0; i < positions.Count; i++) board[positions[i]] = tiles[i];
        }

        static void RecordMoves(List<GridPos> positions, List<Tile> original, List<Tile> arranged, List<TileMove> moves)
        {
            if (moves == null) return;
            var from = new Dictionary<int, GridPos>(original.Count);
            for (int i = 0; i < original.Count; i++) from[original[i].Id] = positions[i];
            for (int i = 0; i < arranged.Count; i++)
            {
                var src = from[arranged[i].Id];
                if (src != positions[i])
                    moves.Add(new TileMove { TileId = arranged[i].Id, From = src, To = positions[i] });
            }
        }
    }
}
