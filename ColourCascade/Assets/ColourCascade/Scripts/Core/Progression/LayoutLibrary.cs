namespace ColourCascade.Core
{
    public enum LayoutPattern
    {
        Open,
        Pillars,
        CentreBlock,
        Shelves,
        TwinWalls,
        Steps,
        Scatter,
        Posts,
        Corners,
        Hourglass,
        Brackets,
    }

    /// <summary>
    /// Parametric, mirror-symmetric board layouts. Tier (0-3) controls how much each pattern intrudes.
    /// Stone patterns block gravity and connections; shaped patterns remove cells entirely (voids).
    /// </summary>
    public static class LayoutLibrary
    {
        public static readonly LayoutPattern[] StonePatterns =
        {
            LayoutPattern.Pillars, LayoutPattern.CentreBlock, LayoutPattern.Shelves, LayoutPattern.TwinWalls,
            LayoutPattern.Steps, LayoutPattern.Scatter, LayoutPattern.Posts,
        };

        public static readonly LayoutPattern[] ShapedPatterns =
        {
            LayoutPattern.Corners, LayoutPattern.Hourglass, LayoutPattern.Brackets,
        };

        public static LevelLayout Build(LayoutPattern pattern, int w, int h, int tier, Rng rng)
        {
            var l = LevelLayout.Open(w, h);
            if (tier < 0) tier = 0;
            if (tier > 3) tier = 3;
            int midR = h / 2;
            switch (pattern)
            {
                case LayoutPattern.Pillars:
                    for (int i = 0; i < 2 + tier / 2; i++) Mirror(l, 1, midR - 1 + i, CellType.Stone);
                    break;
                case LayoutPattern.CentreBlock:
                    {
                        int rowsTall = tier >= 2 ? 3 : 2;
                        for (int r = 0; r < rowsTall; r++)
                        {
                            Mirror(l, (w - 1) / 2, midR - 1 + r, CellType.Stone);
                            if (tier >= 3) Mirror(l, (w - 1) / 2 - 1, midR, CellType.Stone);
                        }
                        break;
                    }
                case LayoutPattern.Shelves:
                    for (int i = 0; i < 2 + tier / 2; i++) Mirror(l, i, midR - 1, CellType.Stone);
                    break;
                case LayoutPattern.TwinWalls:
                    for (int i = 0; i < 2 + tier; i++) Mirror(l, 2, 2 + i, CellType.Stone);
                    break;
                case LayoutPattern.Steps:
                    for (int i = 0; i < 2 + tier / 2; i++) Mirror(l, 1 + i, 2 + i, CellType.Stone);
                    break;
                case LayoutPattern.Scatter:
                    {
                        int pairs = 2 + tier;
                        int placed = 0, guard = 0;
                        while (placed < pairs && guard++ < 50)
                        {
                            int c = rng.Next((w + 1) / 2);
                            int r = rng.Range(1, h - 1);
                            if (l.TypeAt(c, r) != CellType.Empty) continue;
                            Mirror(l, c, r, CellType.Stone);
                            placed++;
                        }
                        break;
                    }
                case LayoutPattern.Posts:
                    Mirror(l, 1, 1, CellType.Stone);
                    Mirror(l, 1, h - 2, CellType.Stone);
                    if (tier >= 1) Mirror(l, (w - 1) / 2, midR, CellType.Stone);
                    if (tier >= 2) Mirror(l, 2, midR - 2, CellType.Stone);
                    break;
                case LayoutPattern.Corners:
                    Mirror(l, 0, h - 1, CellType.Void);
                    Mirror(l, 1, h - 1, CellType.Void);
                    Mirror(l, 0, h - 2, CellType.Void);
                    if (tier >= 2) Mirror(l, 0, 0, CellType.Void);
                    if (tier >= 1) Mirror(l, 2, h - 3, CellType.Stone);
                    break;
                case LayoutPattern.Hourglass:
                    Mirror(l, 0, midR, CellType.Void);
                    Mirror(l, 0, midR - 1, CellType.Void);
                    if (tier >= 1) Mirror(l, 0, midR + 1, CellType.Void);
                    if (tier >= 2) Mirror(l, 1, midR, CellType.Void);
                    break;
                case LayoutPattern.Brackets:
                    Mirror(l, 2, 2, CellType.Stone);
                    Mirror(l, 2, h - 3, CellType.Stone);
                    if (tier >= 1)
                    {
                        Mirror(l, 3, 2, CellType.Stone);
                        Mirror(l, 3, h - 3, CellType.Stone);
                    }
                    break;
            }
            string error;
            return l.Validate(out error) ? l : LevelLayout.Open(w, h);
        }

        static void Mirror(LevelLayout l, int c, int r, CellType type)
        {
            if (!l.InBounds(c, r)) return;
            l.SetType(c, r, type);
            int mc = l.Width - 1 - c;
            if (l.InBounds(mc, r)) l.SetType(mc, r, type);
        }
    }
}
