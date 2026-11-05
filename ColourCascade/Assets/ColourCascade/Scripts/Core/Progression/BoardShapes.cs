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
        // Update 3.3 (kept at the end so stored values never change).
        /// <summary>A rectangle with its corners cut away.</summary>
        Corners,
        /// <summary>A T (or an upside-down T): the cells under its arms are short columns.</summary>
        TShape,
        /// <summary>A plus: four arms around a middle.</summary>
        Cross,
        /// <summary>A diamond: every column a different height.</summary>
        Diamond,
    }

    /// <summary>
    /// A board shape as data: rows top first, '.' an open cell, 'x' a gap, '#' a stone (the notation of LevelLayout).
    /// The built-in shapes are listed in BoardShapeLibrary; more can be added in the config (campaign → shapes →
    /// extraShapes) and join their family's variants, wherever that family is used, as long as they pass
    /// BoardShapes.IsFair.
    /// </summary>
    [Serializable]
    public class BoardShapeTemplate
    {
        public BoardFamily family = BoardFamily.Asymmetric;
        public string[] rows = new string[0];
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
    /// The board shapes, as text templates (BoardShapeTemplate) plus a few generated families: plain rectangles of the
    /// listed sizes, tall and wide boards, and the stone patterns of LayoutLibrary. A library can carry extra templates
    /// from the config; an extra that is not fair (BoardShapes.IsFair) is left out. Gameplay never refers to a shape:
    /// the engine plays any layout of open cells, stones and gaps.
    /// </summary>
    public sealed class BoardShapeLibrary
    {
        static readonly Dictionary<BoardFamily, string[][]> BuiltIn = new Dictionary<BoardFamily, string[][]>
        {
            [BoardFamily.Hollow] = new[]
            {
                new[] { "........", "........", "........", "...xx...", "...xx...", "........", "........", "........", "........" },
                new[] { ".........", ".........", ".........", "...xxx...", "...xxx...", "...xxx...", ".........", ".........", "........." },
                new[] { "........", "........", "........", ".xx..xx.", ".xx..xx.", "........", "........", "........", "........" },
                new[] { ".........", ".........", ".........", "...xxx...", "...xxx...", "...xxx...", "...xxx...", ".........", ".........", "........." },
            },
            [BoardFamily.TwinTowers] = new[]
            {
                new[] { "....x....", "....x....", "....x....", "....x....", "....x....", "....x....", "....x....", "....x....", "....x...." },
                new[] { "....xx....", "....xx....", "....xx....", "....xx....", "....xx....", "....xx....", "....xx....", "....xx....", "....xx...." },
                new[] { "....xxxxx", "....xxxxx", "....x....", "....x....", "....x....", "....x....", "....x....", "....x....", "....x....", "....x...." },
            },
            [BoardFamily.Bunks] = new[]
            {
                new[] { "........", "........", "........", "........", "xxxxxxxx", "........", "........", "........", "........", "........" },
                new[] { ".........", ".........", ".........", ".........", ".........", "xxxxxxxxx", ".........", ".........", ".........", "........." },
            },
            [BoardFamily.StoneWall] = new[]
            {
                new[] { "....#....", "....#....", "....#....", "....#....", "....#....", "....#....", "....#....", "....#....", "....#...." },
                new[] { "........", "........", "........", "........", "########", "........", "........", "........", "........", "........" },
                new[] { ".........", ".........", ".........", ".........", "....#....", "....#....", "....#....", "....#....", "....#...." },
            },
            [BoardFamily.Passage] = new[]
            {
                new[] { "........", "........", "........", "........", "xxx..xxx", "........", "........", "........", "........", "........" },
                new[] { ".........", ".........", ".........", ".........", "xxxx.xxxx", ".........", ".........", ".........", ".........", "........." },
                new[] { "....x....", "....x....", "....x....", "....x....", "....x....", "....x....", ".........", ".........", "....x...." },
                new[] { "........", "........", "........", "........", "..xxxxxx", "........", "........", "........", "........", "........" },
            },
            [BoardFamily.Asymmetric] = new[]
            {
                new[] { ".....xxxx", ".....xxxx", ".....xxxx", ".........", ".........", ".........", ".........", ".........", ".........", "........." },
                new[] { ".....xxxx", "......xxx", ".......xx", "........x", ".........", ".........", ".........", ".........", "........." },
                new[] { ".........", ".........", ".........", ".........", ".........", "x........", "xx.......", "xxx......", "xxxx....." },
            },
            [BoardFamily.ThreeSections] = new[]
            {
                new[] { "....x....", "....x....", "....x....", "....x....", "....x....", "xxxxxxxxx", ".........", ".........", ".........", "........." },
                new[] { ".......", ".......", ".......", "xxxxxxx", ".......", ".......", ".......", "xxxxxxx", ".......", ".......", "......." },
                new[] { ".........", ".........", ".........", ".........", "xxxxxxxxx", "....x....", "....x....", "....x....", "....x....", "....x...." },
            },
            [BoardFamily.Islands] = new[]
            {
                new[] { "....x....", "....x....", "....x....", "....x....", "....x....", "xxxxxxxxx", "....x....", "....x....", "....x....", "....x....", "....x...." },
                new[] { "....x.....", "....x.....", "....x.....", "....x.....", "....x.....", "xxxxxxxxxx", "....x.....", "....x.....", "....x.....", "....x....." },
            },
            [BoardFamily.Corners] = new[]
            {
                new[] { "x......x", "........", "........", "........", "........", "........", "........", "........", "x......x" },
                new[] { "xx.....xx", "x.......x", ".........", ".........", ".........", ".........", ".........", "x.......x", "xx.....xx" },
                new[] { "xx....xx", "x......x", "........", "........", "........", "........", "........", "........", "x......x", "xx....xx" },
            },
            [BoardFamily.TShape] = new[]
            {
                new[] { ".........", ".........", ".........", ".........", "xx.....xx", "xx.....xx", "xx.....xx", "xx.....xx", "xx.....xx", "xx.....xx" },
                new[] { "xx.....xx", "xx.....xx", "xx.....xx", "xx.....xx", ".........", ".........", ".........", ".........", "........." },
            },
            [BoardFamily.Cross] = new[]
            {
                new[] { "xxx...xxx", "xxx...xxx", "xxx...xxx", ".........", ".........", ".........", "xxx...xxx", "xxx...xxx", "xxx...xxx" },
                new[] { "xxx....xxx", "xxx....xxx", "xxx....xxx", "..........", "..........", "..........", "..........", "xxx....xxx", "xxx....xxx", "xxx....xxx" },
            },
            [BoardFamily.Diamond] = new[]
            {
                new[] { "xxx...xxx", "xx.....xx", "x.......x", ".........", ".........", ".........", "x.......x", "xx.....xx", "xxx...xxx" },
                new[] { "xxx....xxx", "xx......xx", "x........x", "..........", "..........", "..........", "..........", "..........", "x........x", "xx......xx", "xxx....xxx" },
            },
        };

        // After BuiltIn: static fields are set in the order they are written.
        public static readonly BoardShapeLibrary Default = new BoardShapeLibrary(null);

        readonly Dictionary<BoardFamily, List<string[]>> templates = new Dictionary<BoardFamily, List<string[]>>();

        public BoardShapeLibrary(IEnumerable<BoardShapeTemplate> extras)
        {
            foreach (var kv in BuiltIn) templates[kv.Key] = new List<string[]>(kv.Value);
            if (extras == null) return;
            foreach (var t in extras)
            {
                if (t == null || t.rows == null || t.rows.Length == 0 || !Templated(t.family)) continue;
                try
                {
                    var l = LevelLayout.Parse(t.rows);
                    if (!BoardShapes.IsFair(l, out _)) continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (!templates.TryGetValue(t.family, out var list)) templates[t.family] = list = new List<string[]>();
                list.Add((string[])t.rows.Clone());
            }
        }

        /// <summary>Families described by templates (the others are generated: rectangles, tall, wide, stone patterns).</summary>
        public static bool Templated(BoardFamily f) => f != BoardFamily.Rectangle && f != BoardFamily.Obstructed && f != BoardFamily.Tower && f != BoardFamily.Wide;

        public int VariantCount(BoardFamily f)
        {
            switch (f)
            {
                case BoardFamily.Rectangle: return BoardShapes.RectangleSizes.Length;
                case BoardFamily.Obstructed: return LayoutLibrary.StonePatterns.Length;
                case BoardFamily.Tower: return 3;
                case BoardFamily.Wide: return 3;
                default: return templates.TryGetValue(f, out var list) && list.Count > 0 ? list.Count : 1;
            }
        }

        /// <summary>The template rows of a templated family's variant (top first).</summary>
        public string[] Rows(BoardFamily f, int variant)
        {
            if (!templates.TryGetValue(f, out var list) || list.Count == 0) return null;
            return list[((variant % list.Count) + list.Count) % list.Count];
        }

        /// <summary>Builds one variant of a family. tier (0-3) only affects Obstructed (how much its stones intrude).</summary>
        public BoardShape Build(BoardFamily f, int variant, int tier, Rng rng)
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
                        var (w, h) = BoardShapes.RectangleSizes[2 + rng.Next(2)];
                        l = null;
                        for (int attempt = 0; attempt < 8 && l == null; attempt++)
                        {
                            var candidate = LayoutLibrary.Build(LayoutLibrary.StonePatterns[variant], w, h, attempt < 6 ? tier : 0, rng);
                            if (BoardShapes.IsFair(candidate, out _)) l = candidate;
                        }
                        if (l == null) l = LevelLayout.Open(w, h);
                        break;
                    }
                case BoardFamily.Tower: l = variant == 0 ? LevelLayout.Open(6, 11) : variant == 1 ? LevelLayout.Open(7, 11) : LevelLayout.Open(6, 10); break;
                case BoardFamily.Wide: l = variant == 0 ? LevelLayout.Open(10, 7) : variant == 1 ? LevelLayout.Open(10, 8) : LevelLayout.Open(9, 7); break;
                case BoardFamily.Rectangle:
                    {
                        var (w, h) = BoardShapes.RectangleSizes[variant];
                        l = LevelLayout.Open(w, h);
                        break;
                    }
                default:
                    {
                        var rows = Rows(f, variant);
                        l = rows != null ? LevelLayout.Parse(rows) : LevelLayout.Open(8, 9);
                        break;
                    }
            }
            return new BoardShape { Family = f, Variant = variant, Name = BoardShapes.NameOf(f), Layout = l };
        }
    }

    /// <summary>
    /// Board shape helpers and fairness checks. Every shape fits the game's board area on every supported screen with
    /// touch-sized cells (at most MaxColumns × MaxRows, BoardFit).
    /// </summary>
    public static class BoardShapes
    {
        public const int MaxColumns = 10;
        public const int MaxRows = 11;
        /// <summary>The smallest separate section a shape may have (smaller ones run out of matches too easily).</summary>
        public const int MinSectionCells = 12;

        /// <summary>Plain board sizes, smallest first; later levels may use the bigger ones.</summary>
        public static readonly (int w, int h)[] RectangleSizes = { (7, 9), (8, 8), (8, 9), (9, 9), (7, 10), (9, 8), (8, 10), (9, 10) };

        public static int VariantCount(BoardFamily f) => BoardShapeLibrary.Default.VariantCount(f);

        /// <summary>Builds one variant of a built-in family (BoardShapeLibrary.Default).</summary>
        public static BoardShape Build(BoardFamily f, int variant, int tier, Rng rng) => BoardShapeLibrary.Default.Build(f, variant, tier, rng);

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
                case BoardFamily.Corners: return "Cut corners";
                case BoardFamily.TShape: return "T-shaped board";
                case BoardFamily.Cross: return "Cross";
                case BoardFamily.Diamond: return "Diamond";
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
                case BoardFamily.Corners: return "New board: cut corners";
                case BoardFamily.TShape: return "New board: a T, with short columns under its arms";
                case BoardFamily.Cross: return "New board: a cross";
                case BoardFamily.Diamond: return "New board: a diamond";
                default: return "";
            }
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
