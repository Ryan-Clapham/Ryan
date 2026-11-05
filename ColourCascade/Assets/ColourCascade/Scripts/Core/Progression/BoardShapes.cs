using System;
using System.Collections.Generic;

namespace ColourCascade.Core
{
    /// <summary>
    /// The kinds of board a generated campaign level can be played on (docs/DESIGN.md §3.5). Every shape is built from
    /// cells the engine already has: open cells, stones (gravity barriers that rockets and bombs break) and voids (no
    /// cell at all). Each column is split into independent sections wherever a stone or void interrupts it, so a
    /// shape's gaps are real: tiles never fall into or across them, groups don't join across them, each section fills
    /// from its own top, and treasure is collected on any permanent floor (the bottom edge, a stone or a gap).
    /// </summary>
    public enum BoardFamily
    {
        /// <summary>A plain rectangle; its size varies.</summary>
        Rectangle,
        /// <summary>A rectangle with stone barriers inside (the patterns of LayoutLibrary).</summary>
        Obstructed,
        /// <summary>A hole (or two) in the middle: the columns under it fill from beneath it.</summary>
        Hollow,
        /// <summary>Narrow and tall.</summary>
        Tower,
        /// <summary>Wide and shallow.</summary>
        Wide,
        /// <summary>Two sections side by side, split by a gap.</summary>
        TwinTowers,
        /// <summary>An upper and a lower section split by a gap; the lower one fills from beneath the gap.</summary>
        Bunks,
        /// <summary>Two sections split by a wall of stones that rockets and bombs can break open.</summary>
        StoneWall,
        /// <summary>Two sections joined only through a narrow opening.</summary>
        Passage,
        /// <summary>Lopsided boards: an L, steps, an uneven floor.</summary>
        Asymmetric,
        /// <summary>Three separate sections.</summary>
        ThreeSections,
        /// <summary>Four separate sections in a 2 × 2 grid.</summary>
        Islands,
    }

    /// <summary>A board shape: its layout, its family and the name shown on the level card.</summary>
    public sealed class BoardShape
    {
        public BoardFamily Family;
        public int Variant;
        public string Name = "";
        public LevelLayout Layout;
    }

    /// <summary>
    /// Builds the campaign's board shapes and checks that they play fairly. All of them fit the game's board area on
    /// every supported screen with touch-sized cells (at most MaxColumns × MaxRows, BoardFit).
    /// </summary>
    public static class BoardShapes
    {
        public const int MaxColumns = 10;
        public const int MaxRows = 11;
        /// <summary>The smallest separate section a shape may have (smaller ones run out of matches too easily).</summary>
        public const int MinSectionCells = 12;

        /// <summary>Plain board sizes, smallest first; later levels may use the bigger ones.</summary>
        public static readonly (int w, int h)[] RectangleSizes = { (7, 9), (8, 8), (8, 9), (9, 9), (7, 10), (9, 8), (8, 10), (9, 10) };

        public static int VariantCount(BoardFamily f)
        {
            switch (f)
            {
                case BoardFamily.Rectangle: return RectangleSizes.Length;
                case BoardFamily.Obstructed: return LayoutLibrary.StonePatterns.Length;
                case BoardFamily.Hollow: return 4;
                case BoardFamily.Tower: return 3;
                case BoardFamily.Wide: return 3;
                case BoardFamily.TwinTowers: return 3;
                case BoardFamily.Bunks: return 2;
                case BoardFamily.StoneWall: return 3;
                case BoardFamily.Passage: return 4;
                case BoardFamily.Asymmetric: return 3;
                case BoardFamily.ThreeSections: return 3;
                case BoardFamily.Islands: return 2;
                default: return 1;
            }
        }

        /// <summary>The name shown on the level card ("Twin towers", "Break the wall", ...); empty for a plain rectangle.</summary>
        public static string NameOf(BoardFamily f)
        {
            switch (f)
            {
                case BoardFamily.Hollow: return "Hollow board";
                case BoardFamily.Tower: return "Tall board";
                case BoardFamily.Wide: return "Wide board";
                case BoardFamily.TwinTowers: return "Twin towers";
                case BoardFamily.Bunks: return "Upper and lower";
                case BoardFamily.StoneWall: return "Break the wall";
                case BoardFamily.Passage: return "Narrow passage";
                case BoardFamily.Asymmetric: return "Lopsided board";
                case BoardFamily.ThreeSections: return "Three sections";
                case BoardFamily.Islands: return "Islands";
                default: return "";
            }
        }

        /// <summary>What the level card says the first time a family appears.</summary>
        public static string IntroNote(BoardFamily f)
        {
            switch (f)
            {
                case BoardFamily.Hollow: return "New board: a hole in the middle";
                case BoardFamily.Tower: return "New board: tall and narrow";
                case BoardFamily.Wide: return "New board: wide and shallow";
                case BoardFamily.TwinTowers: return "New board: two towers, each falls on its own";
                case BoardFamily.Bunks: return "New board: upper and lower halves";
                case BoardFamily.StoneWall: return "New board: rockets and bombs break the wall";
                case BoardFamily.Passage: return "New board: joined by a narrow passage";
                case BoardFamily.Asymmetric: return "New board: lopsided";
                case BoardFamily.ThreeSections: return "New board: three sections";
                case BoardFamily.Islands: return "New board: four islands";
                default: return "";
            }
        }

        /// <summary>Builds one variant of a family. tier (0-3) only affects Obstructed (how much its stones intrude).</summary>
        public static BoardShape Build(BoardFamily f, int variant, int tier, Rng rng)
        {
            int count = VariantCount(f);
            variant = ((variant % count) + count) % count;
            LevelLayout l;
            switch (f)
            {
                case BoardFamily.Obstructed:
                    {
                        // Random patterns (Scatter) can leave a dead-end pocket: try again, then fall back to the pattern
                        // at its gentlest.
                        var (w, h) = RectangleSizes[2 + rng.Next(2)];
                        l = null;
                        for (int attempt = 0; attempt < 8 && l == null; attempt++)
                        {
                            var candidate = LayoutLibrary.Build(LayoutLibrary.StonePatterns[variant], w, h, attempt < 6 ? tier : 0, rng);
                            if (IsFair(candidate, out _)) l = candidate;
                        }
                        if (l == null) l = LevelLayout.Open(w, h);
                        break;
                    }
                case BoardFamily.Hollow: l = Hollow(variant); break;
                case BoardFamily.Tower: l = variant == 0 ? LevelLayout.Open(6, 11) : variant == 1 ? LevelLayout.Open(7, 11) : LevelLayout.Open(6, 10); break;
                case BoardFamily.Wide: l = variant == 0 ? LevelLayout.Open(10, 7) : variant == 1 ? LevelLayout.Open(10, 8) : LevelLayout.Open(9, 7); break;
                case BoardFamily.TwinTowers: l = TwinTowers(variant); break;
                case BoardFamily.Bunks: l = Bunks(variant); break;
                case BoardFamily.StoneWall: l = StoneWall(variant); break;
                case BoardFamily.Passage: l = Passage(variant); break;
                case BoardFamily.Asymmetric: l = Asymmetric(variant); break;
                case BoardFamily.ThreeSections: l = ThreeSections(variant); break;
                case BoardFamily.Islands: l = Islands(variant); break;
                default:
                    {
                        var (w, h) = RectangleSizes[variant];
                        l = LevelLayout.Open(w, h);
                        break;
                    }
            }
            return new BoardShape { Family = f, Variant = variant, Name = NameOf(f), Layout = l };
        }

        // ------------------------------------------------------------------ The shapes (rows top first: '.' open, 'x' gap, '#' stone)

        static LevelLayout Hollow(int v)
        {
            switch (v)
            {
                case 0:
                    return LevelLayout.Parse(new[] { "........", "........", "........", "...xx...", "...xx...", "........", "........", "........", "........" });
                case 1:
                    return LevelLayout.Parse(new[] { ".........", ".........", ".........", "...xxx...", "...xxx...", "...xxx...", ".........", ".........", "........." });
                case 2:
                    // Two windows: four short columns fill from beneath them.
                    return LevelLayout.Parse(new[] { "........", "........", "........", ".xx..xx.", ".xx..xx.", "........", "........", "........", "........" });
                default:
                    return LevelLayout.Parse(new[] { ".........", ".........", ".........", "...xxx...", "...xxx...", "...xxx...", "...xxx...", ".........", ".........", "........." });
            }
        }

        static LevelLayout TwinTowers(int v)
        {
            switch (v)
            {
                case 0: return Columns(9, 9, 4);
                case 1: return Columns(10, 9, 4, 5);
                default:
                    {
                        // Towers of different heights: the right one is lower, and fills from beneath the gap above it.
                        var l = Columns(9, 10, 4);
                        for (int c = 5; c < 9; c++)
                            for (int r = 8; r < 10; r++) l.SetType(c, r, CellType.Void);
                        return l;
                    }
            }
        }

        static LevelLayout Bunks(int v)
        {
            return v == 0 ? RowsGap(8, 10, 5) : RowsGap(9, 10, 4);
        }

        static LevelLayout StoneWall(int v)
        {
            switch (v)
            {
                case 0:
                    {
                        // A wall down the middle: left and right never meet until a rocket or bomb breaks through.
                        var l = LevelLayout.Open(9, 9);
                        for (int r = 0; r < 9; r++) l.SetType(4, r, CellType.Stone);
                        return l;
                    }
                case 1:
                    {
                        // A floor of stones across the board: break it and the upper tiles pour into the lower half.
                        var l = LevelLayout.Open(8, 10);
                        for (int c = 0; c < 8; c++) l.SetType(c, 5, CellType.Stone);
                        return l;
                    }
                default:
                    {
                        // A wall across the lower half only: the halves meet above it.
                        var l = LevelLayout.Open(9, 9);
                        for (int r = 0; r < 5; r++) l.SetType(4, r, CellType.Stone);
                        return l;
                    }
            }
        }

        static LevelLayout Passage(int v)
        {
            switch (v)
            {
                case 0:
                    {
                        // Upper and lower joined by a two-wide opening in the middle: only those columns drain down.
                        var l = RowsGap(8, 10, 5);
                        l.SetType(3, 5, CellType.Empty);
                        l.SetType(4, 5, CellType.Empty);
                        return l;
                    }
                case 1:
                    {
                        // A single-cell opening.
                        var l = RowsGap(9, 10, 5);
                        l.SetType(4, 5, CellType.Empty);
                        return l;
                    }
                case 2:
                    {
                        // Left and right joined by a bridge near the floor: groups can cross there only.
                        var l = Columns(9, 9, 4);
                        l.SetType(4, 1, CellType.Empty);
                        l.SetType(4, 2, CellType.Empty);
                        return l;
                    }
                default:
                    {
                        // The opening at one side.
                        var l = RowsGap(8, 10, 5);
                        l.SetType(0, 5, CellType.Empty);
                        l.SetType(1, 5, CellType.Empty);
                        return l;
                    }
            }
        }

        static LevelLayout Asymmetric(int v)
        {
            switch (v)
            {
                case 0:
                    // An L: the top right corner is missing, so those columns are short and fill from beneath it.
                    return LevelLayout.Parse(new[] { ".....xxxx", ".....xxxx", ".....xxxx", ".........", ".........", ".........", ".........", ".........", ".........", "........." });
                case 1:
                    // Steps down to the right.
                    return LevelLayout.Parse(new[] { ".....xxxx", "......xxx", ".......xx", "........x", ".........", ".........", ".........", ".........", "........." });
                default:
                    // An uneven floor: the left columns stop higher up (treasure lands on the steps).
                    return LevelLayout.Parse(new[] { ".........", ".........", ".........", ".........", ".........", "x........", "xx.......", "xxx......", "xxxx....." });
            }
        }

        static LevelLayout ThreeSections(int v)
        {
            switch (v)
            {
                case 0:
                    {
                        // Two upper towers over a wide lower section.
                        var l = RowsGap(9, 10, 4);
                        for (int r = 5; r < 10; r++) l.SetType(4, r, CellType.Void);
                        return l;
                    }
                case 1:
                    {
                        // Three floors, each filling on its own.
                        var l = LevelLayout.Open(7, 11);
                        for (int c = 0; c < 7; c++)
                        {
                            l.SetType(c, 3, CellType.Void);
                            l.SetType(c, 7, CellType.Void);
                        }
                        return l;
                    }
                default:
                    {
                        // A wide upper section over two lower towers.
                        var l = RowsGap(9, 10, 5);
                        for (int r = 0; r < 5; r++) l.SetType(4, r, CellType.Void);
                        return l;
                    }
            }
        }

        static LevelLayout Islands(int v)
        {
            // Four islands of 16 to 25 cells.
            var l = v == 0 ? Columns(9, 11, 4) : Columns(10, 10, 4);
            int gap = v == 0 ? 5 : 4;
            for (int c = 0; c < l.Width; c++) l.SetType(c, gap, CellType.Void);
            return l;
        }

        /// <summary>An open board with whole void columns.</summary>
        static LevelLayout Columns(int w, int h, params int[] gaps)
        {
            var l = LevelLayout.Open(w, h);
            foreach (int c in gaps)
                for (int r = 0; r < h; r++) l.SetType(c, r, CellType.Void);
            return l;
        }

        /// <summary>An open board with one whole void row (r counted from the bottom).</summary>
        static LevelLayout RowsGap(int w, int h, int row)
        {
            var l = LevelLayout.Open(w, h);
            for (int c = 0; c < w; c++) l.SetType(c, row, CellType.Void);
            return l;
        }

        // ------------------------------------------------------------------ Fair play checks

        /// <summary>The separate playable areas: open cells joined edge to edge (stones and gaps divide them).</summary>
        public static List<List<GridPos>> Sections(LevelLayout l)
        {
            var seen = new bool[l.Width * l.Height];
            var sections = new List<List<GridPos>>();
            var stack = new Stack<GridPos>();
            for (int r = 0; r < l.Height; r++)
                for (int c = 0; c < l.Width; c++)
                {
                    if (!l.IsOpen(c, r) || seen[r * l.Width + c]) continue;
                    var cells = new List<GridPos>();
                    stack.Push(new GridPos(c, r));
                    seen[r * l.Width + c] = true;
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        cells.Add(p);
                        for (int d = 0; d < 4; d++)
                        {
                            int nc = p.Col + Board.DCol[d], nr = p.Row + Board.DRow[d];
                            if (!l.IsOpen(nc, nr) || seen[nr * l.Width + nc]) continue;
                            seen[nr * l.Width + nc] = true;
                            stack.Push(new GridPos(nc, nr));
                        }
                    }
                    sections.Add(cells);
                }
            return sections;
        }

        public static int OpenNeighbours(LevelLayout l, int c, int r)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
                if (l.IsOpen(c + Board.DCol[d], r + Board.DRow[d])) n++;
            return n;
        }

        /// <summary>
        /// A shape plays fairly when it passes LevelLayout.Validate, fits the screen, every separate section has at
        /// least MinSectionCells cells and spans two rows and two columns, and no open cell is a dead end (each has two
        /// open neighbours or more), so no tile is ever stuck where it can only pair with one other.
        /// </summary>
        public static bool IsFair(LevelLayout l, out string error)
        {
            if (!l.Validate(out error)) return false;
            if (l.Width > MaxColumns || l.Height > MaxRows)
            {
                error = "Board " + l.Width + " x " + l.Height + " is larger than " + MaxColumns + " x " + MaxRows;
                return false;
            }
            foreach (var s in Sections(l))
            {
                int minC = int.MaxValue, maxC = -1, minR = int.MaxValue, maxR = -1;
                foreach (var p in s)
                {
                    minC = Math.Min(minC, p.Col);
                    maxC = Math.Max(maxC, p.Col);
                    minR = Math.Min(minR, p.Row);
                    maxR = Math.Max(maxR, p.Row);
                }
                if (s.Count < MinSectionCells || maxC == minC || maxR == minR)
                {
                    error = "Section at " + s[0] + " has only " + s.Count + " cells";
                    return false;
                }
            }
            for (int r = 0; r < l.Height; r++)
                for (int c = 0; c < l.Width; c++)
                    if (l.IsOpen(c, r) && OpenNeighbours(l, c, r) < 2)
                    {
                        error = "Open cell at " + new GridPos(c, r) + " is a dead end";
                        return false;
                    }
            error = null;
            return true;
        }

        /// <summary>The cell a falling piece at (c, r) comes to rest on in an empty column: the lowest open cell of its
        /// column section (the cell just above the bottom edge, a stone or a gap).</summary>
        public static int SectionFloor(LevelLayout l, int c, int r)
        {
            int y = r;
            while (y - 1 >= 0 && l.IsOpen(c, y - 1)) y--;
            return y;
        }

        /// <summary>The highest open cell of the column section containing (c, r).</summary>
        public static int SectionTop(LevelLayout l, int c, int r)
        {
            int y = r;
            while (y + 1 < l.Height && l.IsOpen(c, y + 1)) y++;
            return y;
        }
    }
}
