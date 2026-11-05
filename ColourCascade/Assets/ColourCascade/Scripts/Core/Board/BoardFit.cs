using System;

namespace ColourCascade.Core
{
    /// <summary>
    /// How big a board's cells are on the game screen, in reference pixels (the 1080-wide portrait canvas). BoardView sizes
    /// the board with it, and tests use it to check every campaign board keeps touch-sized cells on every supported screen.
    /// </summary>
    public static class BoardFit
    {
        public const float FramePadding = 18f;
        public const float MaxCell = 172f;
        public const float MinCell = 40f;
        /// <summary>The smallest comfortable touch target (docs/DESIGN.md §10).</summary>
        public const float MinTouchCell = 96f;
        /// <summary>The game screen keeps 26 at each side, 232 below the board and 392 above it (GameScreen).</summary>
        public const float SideMargins = 52f, VerticalMargins = 624f;

        /// <summary>The cell size for a board of cols × rows in an area of areaW × areaH.</summary>
        public static float CellSize(float areaW, float areaH, int cols, int rows)
        {
            if (cols <= 0 || rows <= 0) return MinCell;
            float cell = (float)Math.Floor(Math.Min(Math.Min((areaW - FramePadding * 2) / cols, (areaH - FramePadding * 2) / rows), MaxCell));
            return Math.Max(MinCell, cell);
        }

        /// <summary>
        /// The canvas in reference pixels for a screen of the given pixel size: phones (narrower than 0.62) match the
        /// 1080 width, tablets the 1920 height (ScreenAdapter).
        /// </summary>
        public static void Canvas(float screenW, float screenH, out float canvasW, out float canvasH)
        {
            float aspect = screenW / screenH;
            if (aspect > 0.62f)
            {
                canvasH = 1920f;
                canvasW = 1920f * aspect;
            }
            else
            {
                canvasW = 1080f;
                canvasH = 1080f / aspect;
            }
        }

        /// <summary>The cell size of a cols × rows board on a screen of screenW × screenH pixels, less a safe-area inset (reference pixels) at the top.</summary>
        public static float CellOnScreen(float screenW, float screenH, float safeInset, int cols, int rows)
        {
            float w, h;
            Canvas(screenW, screenH, out w, out h);
            return CellSize(w - SideMargins, h - VerticalMargins - safeInset, cols, rows);
        }
    }
}
