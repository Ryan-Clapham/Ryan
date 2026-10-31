using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// Static shape of a board: which cells are open, stone or void, plus optional fixed starting colours.
    /// Text notation (rows written top first):
    ///   '.' open cell with a random colour, '#' stone, 'x' void (no cell),
    ///   'R','Y','B','G','P','O' (or digits 0-5) open cell with a fixed starting colour.
    /// Obstacles are a second, optional set of rows of the same size (ApplyObstacleRows), in the notation of
    /// Board.ToObstacleString: '.' none, 'i'/'I' ice with 1/2 layers, '1'-'3' chain links. They go on open cells.
    /// </summary>
    public sealed class LevelLayout
    {
        public readonly int Width;
        public readonly int Height;
        readonly CellType[] types;
        readonly int[] fixedColours;
        readonly ObstacleKind[] obstacles;
        readonly int[] strengths;

        public LevelLayout(int width, int height)
        {
            Width = width;
            Height = height;
            types = new CellType[width * height];
            fixedColours = new int[width * height];
            obstacles = new ObstacleKind[width * height];
            strengths = new int[width * height];
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
            }
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
                    if (def == null || !IsOpen(c, r))
                    {
                        error = "Obstacle at " + new GridPos(c, r) + " must be a known kind on an open cell";
                        return false;
                    }
                    if (StrengthAt(c, r) < 1 || StrengthAt(c, r) > def.MaxStrength)
                    {
                        error = def.Name + " at " + new GridPos(c, r) + " needs a strength from 1 to " + def.MaxStrength;
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
                    chars[c] = t == CellType.Stone ? '#' : t == CellType.Void ? 'x' : f >= 0 ? TileColours.Letters[f] : '.';
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
