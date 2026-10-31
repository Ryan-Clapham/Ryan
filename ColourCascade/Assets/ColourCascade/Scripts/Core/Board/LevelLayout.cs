using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Static shape of a board: which cells are open, stone or void, plus optional fixed starting colours.
    /// Text notation (rows written top first):
    ///   '.' open cell with a random colour, '#' stone, 'x' void (no cell),
    ///   'R','Y','B','G','P','O' (or digits 0-5) open cell with a fixed starting colour,
    ///   '$' treasure, '-' / '|' row / column rocket, '*' bomb, '@' prism (pieces for hand-made levels).
    /// Obstacles are a second, optional set of rows of the same size (ApplyObstacleRows), in the notation of
    /// Board.ToObstacleString: '.' none, 'i'/'I' ice with 1/2 layers, '1'-'3' chain links, 'm' moss. They go on
    /// open cells without a piece.
    /// </summary>
    public sealed class LevelLayout
    {
        public readonly int Width;
        public readonly int Height;
        readonly CellType[] types;
        readonly int[] fixedColours;
        readonly ObstacleKind[] obstacles;
        readonly int[] strengths;
        // Piece characters (Board.PieceChars) or '\0'.
        readonly char[] pieces;

        public LevelLayout(int width, int height)
        {
            Width = width;
            Height = height;
            types = new CellType[width * height];
            fixedColours = new int[width * height];
            obstacles = new ObstacleKind[width * height];
            strengths = new int[width * height];
            pieces = new char[width * height];
            for (int i = 0; i < fixedColours.Length; i++) fixedColours[i] = -1;
        }

        public static LevelLayout Open(int width, int height) => new LevelLayout(width, height);

        public bool InBounds(int c, int r) => c >= 0 && r >= 0 && c < Width && r < Height;

        /// <summary>Empty means "open cell".</summary>
        public CellType TypeAt(int c, int r) => types[r * Width + c];

        public void SetType(int c, int r, CellType t)
        {
            types[r * Width + c] = t == CellType.Colour ? CellType.Empty : t;
            if (t != CellType.Empty && t != CellType.Colour)
            {
                fixedColours[r * Width + c] = -1;
                obstacles[r * Width + c] = ObstacleKind.None;
                strengths[r * Width + c] = 0;
                pieces[r * Width + c] = '\0';
            }
        }

        /// <summary>The piece placed on an open cell ('$' treasure, '-', '|', '*', '@' specials) or '\0'.</summary>
        public char PieceAt(int c, int r) => pieces[r * Width + c];

        /// <summary>Places a piece on an open cell ('\0' removes it). A piece replaces any fixed colour.</summary>
        public void SetPiece(int c, int r, char piece)
        {
            pieces[r * Width + c] = piece;
            if (piece != '\0') fixedColours[r * Width + c] = -1;
        }

        public int PieceCount(char piece)
        {
            int n = 0;
            for (int i = 0; i < pieces.Length; i++) if (pieces[i] == piece) n++;
            return n;
        }

        public int TreasureCount => PieceCount('$');

        /// <summary>True when a piece at (c, r) would rest on a permanent floor: the bottom edge, a stone or a void.</summary>
        public bool RestsOnPermanentFloor(int c, int r)
        {
            if (r == 0) return true;
            var below = TypeAt(c, r - 1);
            return below == CellType.Stone || below == CellType.Void;
        }

        public ObstacleKind ObstacleAt(int c, int r) => obstacles[r * Width + c];

        public int StrengthAt(int c, int r) => strengths[r * Width + c];

        /// <summary>Puts an obstacle on an open cell (ObstacleKind.None removes it).</summary>
        public void SetObstacle(int c, int r, ObstacleKind kind, int strength)
        {
            obstacles[r * Width + c] = kind;
            strengths[r * Width + c] = kind == ObstacleKind.None ? 0 : strength;
        }

        public int ObstacleCount(ObstacleKind kind)
        {
            int n = 0;
            for (int i = 0; i < obstacles.Length; i++) if (obstacles[i] == kind) n++;
            return n;
        }

        /// <summary>Treasure or special tiles placed by the layout (campaign-only, like obstacles).</summary>
        public bool HasPieces
        {
            get
            {
                for (int i = 0; i < pieces.Length; i++) if (pieces[i] != '\0') return true;
                return false;
            }
        }

        public bool HasObstacles
        {
            get
            {
                for (int i = 0; i < obstacles.Length; i++) if (obstacles[i] != ObstacleKind.None) return true;
                return false;
            }
        }

        /// <summary>Reads obstacle rows (top first, same size as the layout).</summary>
        public void ApplyObstacleRows(IList<string> rowsTopFirst)
        {
            if (rowsTopFirst == null || rowsTopFirst.Count == 0) return;
            if (rowsTopFirst.Count != Height) throw new ArgumentException("Obstacle rows: expected " + Height + ", got " + rowsTopFirst.Count);
            for (int i = 0; i < Height; i++)
            {
                string line = rowsTopFirst[i] ?? string.Empty;
                if (line.Length != Width) throw new ArgumentException("Obstacle row " + i + " has length " + line.Length + ", expected " + Width);
                int r = Height - 1 - i;
                for (int c = 0; c < Width; c++)
                {
                    ObstacleKind kind;
                    int strength;
                    if (!Board.TryParseObstacle(line[c], out kind, out strength)) throw new ArgumentException("Unknown obstacle character '" + line[c] + "'");
                    SetObstacle(c, r, kind, strength);
                }
            }
        }

        public string[] ToObstacleRows()
        {
            var rows = new string[Height];
            var chars = new char[Width];
            for (int i = 0; i < Height; i++)
            {
                int r = Height - 1 - i;
                for (int c = 0; c < Width; c++)
                    chars[c] = Board.ObstacleChar(new Tile { Type = CellType.Colour, Obstacle = ObstacleAt(c, r), Strength = StrengthAt(c, r) });
                rows[i] = new string(chars);
            }
            return rows;
        }

        public bool IsOpen(int c, int r) => InBounds(c, r) && types[r * Width + c] == CellType.Empty;

        public int FixedColourAt(int c, int r) => fixedColours[r * Width + c];

        public void SetFixedColour(int c, int r, int colour)
        {
            fixedColours[r * Width + c] = colour;
        }

        public bool HasFixedColours
        {
            get
            {
                for (int i = 0; i < fixedColours.Length; i++) if (fixedColours[i] >= 0) return true;
                return false;
            }
        }

        public int OpenCellCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < types.Length; i++) if (types[i] == CellType.Empty) n++;
                return n;
            }
        }

        public int StoneCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < types.Length; i++) if (types[i] == CellType.Stone) n++;
                return n;
            }
        }

        public LevelLayout Clone()
        {
            var l = new LevelLayout(Width, Height);
            Array.Copy(types, l.types, types.Length);
            Array.Copy(fixedColours, l.fixedColours, fixedColours.Length);
            Array.Copy(obstacles, l.obstacles, obstacles.Length);
            Array.Copy(strengths, l.strengths, strengths.Length);
            Array.Copy(pieces, l.pieces, pieces.Length);
            return l;
        }

        /// <summary>Checks the layout is playable: enough open cells, at least one adjacent open pair and no
        /// isolated open cells that could never be part of a group.</summary>
        public bool Validate(out string error)
        {
            int open = OpenCellCount;
            if (open < 4)
            {
                error = "Layout needs at least 4 open cells";
                return false;
            }
            if (open * 2 < Width * Height)
            {
                error = "Layout must keep at least half of the cells open";
                return false;
            }
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    var kind = ObstacleAt(c, r);
                    if (kind == ObstacleKind.None) continue;
                    var def = Obstacles.Get(kind);
                    if (def == null || !IsOpen(c, r) || PieceAt(c, r) != '\0')
                    {
                        error = "Obstacle at " + new GridPos(c, r) + " must be a known kind on an open cell without a piece";
                        return false;
                    }
                    if (StrengthAt(c, r) < 1 || StrengthAt(c, r) > def.MaxStrength)
                    {
                        error = def.Name + " at " + new GridPos(c, r) + " needs a strength from 1 to " + def.MaxStrength;
                        return false;
                    }
                }
            }
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    char piece = PieceAt(c, r);
                    if (piece == '\0') continue;
                    if (!IsOpen(c, r) || Board.PieceChars.IndexOf(piece) < 0)
                    {
                        error = "Piece at " + new GridPos(c, r) + " must be a known piece on an open cell";
                        return false;
                    }
                    if (piece == '$' && RestsOnPermanentFloor(c, r))
                    {
                        error = "Treasure at " + new GridPos(c, r) + " would be collected before the first move";
                        return false;
                    }
                }
            }
            bool anyPair = false;
            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    if (!IsOpen(c, r)) continue;
                    bool hasNeighbour = false;
                    for (int d = 0; d < 4; d++)
                        if (IsOpen(c + Board.DCol[d], r + Board.DRow[d])) hasNeighbour = true;
                    if (!hasNeighbour)
                    {
                        error = "Open cell at " + new GridPos(c, r) + " is isolated";
                        return false;
                    }
                    anyPair = true;
                }
            }
            error = anyPair ? null : "No adjacent open cells";
            return anyPair;
        }

        public static LevelLayout Parse(IList<string> rowsTopFirst)
        {
            if (rowsTopFirst == null || rowsTopFirst.Count == 0) throw new ArgumentException("Layout has no rows");
            int h = rowsTopFirst.Count;
            int w = rowsTopFirst[0].Length;
            var layout = new LevelLayout(w, h);
            for (int i = 0; i < h; i++)
            {
                string line = rowsTopFirst[i] ?? string.Empty;
                if (line.Length != w) throw new ArgumentException("Layout row " + i + " has length " + line.Length + ", expected " + w);
                int r = h - 1 - i;
                for (int c = 0; c < w; c++)
                {
                    char ch = line[c];
                    switch (ch)
                    {
                        case '#': layout.SetType(c, r, CellType.Stone); break;
                        case 'x':
                        case 'X':
                        case ' ': layout.SetType(c, r, CellType.Void); break;
                        case '.': break;
                        default:
                            if (Board.PieceChars.IndexOf(ch) >= 0)
                            {
                                layout.SetPiece(c, r, ch);
                                break;
                            }
                            int colour = TileColours.Letters.IndexOf(char.ToUpperInvariant(ch));
                            if (colour < 0 && ch >= '0' && ch <= '5') colour = ch - '0';
                            if (colour < 0) throw new ArgumentException("Unknown layout character '" + ch + "'");
                            layout.SetFixedColour(c, r, colour);
                            break;
                    }
                }
            }
            return layout;
        }

        public string[] ToRows()
        {
            var rows = new string[Height];
            var chars = new char[Width];
            for (int i = 0; i < Height; i++)
            {
                int r = Height - 1 - i;
                for (int c = 0; c < Width; c++)
                {
                    var t = TypeAt(c, r);
                    int f = FixedColourAt(c, r);
                    char piece = PieceAt(c, r);
                    chars[c] = t == CellType.Stone ? '#' : t == CellType.Void ? 'x' : piece != '\0' ? piece : f >= 0 ? TileColours.Letters[f] : '.';
                }
                rows[i] = new string(chars);
            }
            return rows;
        }
    }

    public static class BoardGenerator
    {
        /// <summary>Fills a new board from a layout. Cells are filled bottom-up, left to right.</summary>
        public static Board Generate(LevelLayout layout, ITileSource source)
        {
            var board = new Board(layout.Width, layout.Height);
            for (int r = 0; r < layout.Height; r++)
            {
                for (int c = 0; c < layout.Width; c++)
                {
                    var type = layout.TypeAt(c, r);
                    if (type == CellType.Stone) board[c, r] = board.NewStone();
                    else if (type == CellType.Void) board[c, r] = Board.VoidTile;
                    else if (layout.PieceAt(c, r) != '\0' && board.TryNewPiece(layout.PieceAt(c, r), out var piece)) board[c, r] = piece;
                    else
                    {
                        int fixedColour = layout.FixedColourAt(c, r);
                        var tile = board.NewColourTile(fixedColour >= 0 ? fixedColour : source.NextColour(board, c, r));
                        var obstacle = layout.ObstacleAt(c, r);
                        if (obstacle != ObstacleKind.None)
                        {
                            tile.Obstacle = obstacle;
                            tile.Strength = layout.StrengthAt(c, r);
                        }
                        board[c, r] = tile;
                    }
                }
            }
            return board;
        }
    }
}
