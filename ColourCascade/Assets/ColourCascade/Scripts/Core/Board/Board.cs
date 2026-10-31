using System;
using System.Text;

namespace ColourCascade.Core
{
    /// <summary>What occupies a board cell.</summary>
    public enum CellType : byte
    {
        /// <summary>Temporarily empty (between a clear and the refill).</summary>
        Empty = 0,
        /// <summary>A coloured, clearable tile.</summary>
        Colour = 1,
        /// <summary>Stone barrier: never falls, blocks gravity and connections. Only Tile Breaker removes it.</summary>
        Stone = 2,
        /// <summary>No cell at all (used to shape boards). Behaves like an unbreakable barrier and is not drawn.</summary>
        Void = 3,
    }

    /// <summary>Colour indices, in the order new colours are introduced to the player.</summary>
    public static class TileColours
    {
        public const int Red = 0, Yellow = 1, Blue = 2, Green = 3, Purple = 4, Orange = 5;
        public const int Max = 6;
        public static readonly string[] Names = { "Red", "Yellow", "Blue", "Green", "Purple", "Orange" };
        public const string Letters = "RYBGPO";
    }

    public struct Tile : IEquatable<Tile>
    {
        public CellType Type;
        public int Colour;
        /// <summary>Unique per board; lets the view track a tile as it moves.</summary>
        public int Id;
        /// <summary>Layer on a colour tile (ice, chain); None for every other tile. Rules: TileRules.</summary>
        public ObstacleKind Obstacle;
        /// <summary>Remaining ice layers or chain links (0 when there is no obstacle).</summary>
        public int Strength;

        public bool IsColour => Type == CellType.Colour;
        /// <summary>A permanent barrier (stone or void). Temporary ones (ice, chains) are TileRules.IsGravityBarrier.</summary>
        public bool IsBarrier => Type == CellType.Stone || Type == CellType.Void;
        public bool IsEmpty => Type == CellType.Empty;

        public bool Equals(Tile other) => Type == other.Type && Colour == other.Colour && Id == other.Id && Obstacle == other.Obstacle && Strength == other.Strength;
        public override bool Equals(object obj) => obj is Tile t && Equals(t);
        public override int GetHashCode() => (Id * 397) ^ ((int)Type * 31) ^ Colour ^ ((int)Obstacle << 20) ^ (Strength << 24);
        public override string ToString()
        {
            if (Type != CellType.Colour) return Type.ToString();
            return "C" + Colour + "#" + Id + (Obstacle != ObstacleKind.None ? "+" + Obstacle + Strength : "");
        }
    }

    /// <summary>
    /// Grid position. Col 0 is the left edge, Row 0 is the BOTTOM row (gravity pulls towards row 0).
    /// Boards are always described as columns × rows.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int Col;
        public readonly int Row;

        public GridPos(int col, int row)
        {
            Col = col;
            Row = row;
        }

        public bool Equals(GridPos other) => Col == other.Col && Row == other.Row;
        public override bool Equals(object obj) => obj is GridPos p && Equals(p);
        public override int GetHashCode() => (Col * 7919) ^ Row;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
        public override string ToString() => "(" + Col + "," + Row + ")";

        public bool IsAdjacentTo(GridPos o)
        {
            int dc = Col - o.Col, dr = Row - o.Row;
            return (dc == 0 && (dr == 1 || dr == -1)) || (dr == 0 && (dc == 1 || dc == -1));
        }
    }

    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        readonly Tile[] cells;
        int nextId = 1;

        public static readonly int[] DCol = { 1, -1, 0, 0 };
        public static readonly int[] DRow = { 0, 0, 1, -1 };

        public Board(int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentException("Board must be at least 1x1");
            Width = width;
            Height = height;
            cells = new Tile[width * height];
        }

        public int CellCount => cells.Length;

        public bool InBounds(int col, int row) => col >= 0 && row >= 0 && col < Width && row < Height;
        public bool InBounds(GridPos p) => InBounds(p.Col, p.Row);
        public int Index(int col, int row) => row * Width + col;
        public GridPos PosOf(int index) => new GridPos(index % Width, index / Width);

        public Tile this[int col, int row]
        {
            get => cells[row * Width + col];
            set => cells[row * Width + col] = value;
        }

        public Tile this[GridPos p]
        {
            get => cells[p.Row * Width + p.Col];
            set => cells[p.Row * Width + p.Col] = value;
        }

        public Tile GetByIndex(int i) => cells[i];
        public void SetByIndex(int i, Tile t) => cells[i] = t;

        public int AllocateId() => nextId++;

        public Tile NewColourTile(int colour)
        {
            return new Tile { Type = CellType.Colour, Colour = colour, Id = nextId++ };
        }

        public Tile NewStone()
        {
            return new Tile { Type = CellType.Stone, Colour = -1, Id = nextId++ };
        }

        public static Tile VoidTile => new Tile { Type = CellType.Void, Colour = -1, Id = 0 };

        public bool IsColourAt(int col, int row) => InBounds(col, row) && cells[row * Width + col].Type == CellType.Colour;

        /// <summary>A cell that can hold a colour tile (not a stone and not a void).</summary>
        public bool IsOpenCell(int col, int row)
        {
            if (!InBounds(col, row)) return false;
            var t = cells[row * Width + col].Type;
            return t == CellType.Colour || t == CellType.Empty;
        }

        public int CountColour(int colour)
        {
            int n = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].Type == CellType.Colour && cells[i].Colour == colour) n++;
            return n;
        }

        public int CountType(CellType type)
        {
            int n = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].Type == type) n++;
            return n;
        }

        public Board Clone()
        {
            var b = new Board(Width, Height);
            Array.Copy(cells, b.cells, cells.Length);
            b.nextId = nextId;
            return b;
        }

        /// <summary>Rows printed top first. Digits = colours, '#' stone, 'x' void, '.' empty.</summary>
        public string ToDebugString()
        {
            var sb = new StringBuilder();
            for (int r = Height - 1; r >= 0; r--)
            {
                for (int c = 0; c < Width; c++)
                {
                    var t = this[c, r];
                    switch (t.Type)
                    {
                        case CellType.Colour: sb.Append((char)('0' + t.Colour)); break;
                        case CellType.Stone: sb.Append('#'); break;
                        case CellType.Void: sb.Append('x'); break;
                        default: sb.Append('.'); break;
                    }
                }
                if (r > 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Obstacles printed top first: '.' none, 'i' ice (1 layer), 'I' ice (2 layers), '1'-'3' chain with that
        /// many links. Kept separate from ToDebugString, whose output for obstacle-free boards never changes.
        /// </summary>
        public string ToObstacleString()
        {
            var sb = new StringBuilder();
            for (int r = Height - 1; r >= 0; r--)
            {
                for (int c = 0; c < Width; c++) sb.Append(ObstacleChar(this[c, r]));
                if (r > 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        public static char ObstacleChar(Tile t)
        {
            if (t.Type != CellType.Colour) return '.';
            switch (t.Obstacle)
            {
                case ObstacleKind.Ice: return t.Strength >= 2 ? 'I' : 'i';
                case ObstacleKind.Chain: return (char)('0' + Math.Max(1, Math.Min(9, t.Strength)));
                default: return '.';
            }
        }

        /// <summary>Reads one obstacle character (see ToObstacleString). Returns false for an unknown character.</summary>
        public static bool TryParseObstacle(char ch, out ObstacleKind kind, out int strength)
        {
            kind = ObstacleKind.None;
            strength = 0;
            switch (ch)
            {
                case '.': return true;
                case 'i': kind = ObstacleKind.Ice; strength = 1; return true;
                case 'I': kind = ObstacleKind.Ice; strength = 2; return true;
                default:
                    if (ch >= '1' && ch <= '9')
                    {
                        kind = ObstacleKind.Chain;
                        strength = ch - '0';
                        return true;
                    }
                    return false;
            }
        }

        /// <summary>Builds a board from tile rows plus an obstacle overlay of the same size (notation of ToObstacleString).</summary>
        public static Board FromRows(string[] rowsTopFirst, string[] obstacleRowsTopFirst)
        {
            var b = FromRows(rowsTopFirst);
            if (obstacleRowsTopFirst == null) return b;
            if (obstacleRowsTopFirst.Length != b.Height) throw new ArgumentException("Obstacle rows must match the board height");
            for (int i = 0; i < b.Height; i++)
            {
                string line = obstacleRowsTopFirst[i];
                if (line.Length != b.Width) throw new ArgumentException("Obstacle row " + i + " has the wrong length");
                int r = b.Height - 1 - i;
                for (int c = 0; c < b.Width; c++)
                {
                    ObstacleKind kind;
                    int strength;
                    if (!TryParseObstacle(line[c], out kind, out strength)) throw new ArgumentException("Unknown obstacle character '" + line[c] + "'");
                    if (kind == ObstacleKind.None) continue;
                    var t = b[c, r];
                    if (t.Type != CellType.Colour) throw new ArgumentException("Obstacles go on colour tiles only (" + c + "," + r + ")");
                    t.Obstacle = kind;
                    t.Strength = strength;
                    b[c, r] = t;
                }
            }
            return b;
        }

        /// <summary>Builds a board from rows written top first (same notation as ToDebugString).</summary>
        public static Board FromRows(params string[] rowsTopFirst)
        {
            int h = rowsTopFirst.Length;
            int w = rowsTopFirst[0].Length;
            var b = new Board(w, h);
            for (int i = 0; i < h; i++)
            {
                string line = rowsTopFirst[i];
                if (line.Length != w) throw new ArgumentException("Ragged board rows");
                int r = h - 1 - i;
                for (int c = 0; c < w; c++)
                {
                    char ch = line[c];
                    if (ch >= '0' && ch <= '9') b[c, r] = b.NewColourTile(ch - '0');
                    else if (ch == '#') b[c, r] = b.NewStone();
                    else if (ch == 'x') b[c, r] = VoidTile;
                    else b[c, r] = default;
                }
            }
            return b;
        }
    }
}
