using System;

namespace ColourCascade.Core
{
    /// <summary>
    /// The backing panel of a shaped board (update 3.3): instead of one rectangle behind the whole grid, a panel that
    /// follows the playable cells, with rounded outer corners, so split boards show real gaps between their sections and
    /// holes and cut corners show through. Pure pixel maths (no engine types): the board view turns the three alpha maps
    /// into sprites (panel, its faint outline and its soft shadow), and the art preview draws the same maps to check the
    /// look. Rows run bottom first, as Unity textures do.
    /// </summary>
    public sealed class BoardSilhouette
    {
        /// <summary>Size of the maps in pixels.</summary>
        public int Width, Height;
        /// <summary>Pixels per board cell, and the margin (pixels) around the cell area on every side.</summary>
        public int PixelsPerCell, Margin;
        /// <summary>Alpha 0-255 per pixel, bottom row first: the panel, its outline and its shadow.</summary>
        public byte[] Fill, Stroke, Shadow;

        /// <summary>A board needs a shaped panel when it has a gap (a void cell); stones are part of the board.</summary>
        public static bool IsShaped(LevelLayout l)
        {
            if (l == null) return false;
            for (int r = 0; r < l.Height; r++)
                for (int c = 0; c < l.Width; c++)
                    if (l.TypeAt(c, r) == CellType.Void) return true;
            return false;
        }

        public static bool IsShaped(Board b)
        {
            if (b == null) return false;
            for (int i = 0; i < b.CellCount; i++)
                if (b.GetByIndex(i).Type == CellType.Void) return true;
            return false;
        }

        /// <summary>
        /// Builds the maps. open(c, r) says whether a board cell is playable (row 0 at the bottom). pad: how far the panel
        /// reaches past the cells; radius: its outer corner radius (at least pad); stroke: outline width; blur: how far the
        /// shadow fades. All in pixels, at pixelsPerCell pixels per cell.
        /// </summary>
        public static BoardSilhouette Build(Func<int, int, bool> open, int cols, int rows, int pixelsPerCell, float pad, float radius, float stroke, float blur)
        {
            int ppc = Math.Max(4, pixelsPerCell);
            int margin = (int)Math.Ceiling(pad + blur + 2);
            int w = cols * ppc + 2 * margin, h = rows * ppc + 2 * margin;
            var inside = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                int r = (y - margin) >= 0 ? (y - margin) / ppc : -1;
                for (int x = 0; x < w; x++)
                {
                    int c = (x - margin) >= 0 ? (x - margin) / ppc : -1;
                    inside[y * w + x] = c >= 0 && r >= 0 && c < cols && r < rows && open(c, r);
                }
            }
            // Rounded corners bigger than the padding: shrink the cells by e, then grow them by e + pad. Straight edges end
            // pad beyond the cells; convex corners get radius e + pad.
            float e = Math.Max(0f, radius - pad);
            var outside = new bool[w * h];
            for (int i = 0; i < inside.Length; i++) outside[i] = !inside[i];
            var depth = Distance(outside, w, h);
            var core = new bool[w * h];
            bool any = false;
            for (int i = 0; i < core.Length; i++)
            {
                core[i] = inside[i] && depth[i] > e;
                any |= core[i];
            }
            if (!any) Array.Copy(inside, core, inside.Length);
            var reach = Distance(core, w, h);
            var s = new BoardSilhouette { Width = w, Height = h, PixelsPerCell = ppc, Margin = margin, Fill = new byte[w * h], Stroke = new byte[w * h], Shadow = new byte[w * h] };
            float grow = (any ? e : 0f) + pad;
            float half = Math.Max(0.5f, stroke / 2f);
            for (int i = 0; i < reach.Length; i++)
            {
                // v: how far inside the panel's edge (negative outside).
                float v = grow - reach[i];
                s.Fill[i] = Alpha(v + 0.5f);
                s.Stroke[i] = Alpha(1f - Math.Abs(v - half) / (half + 0.5f));
                s.Shadow[i] = Alpha((v + blur) / Math.Max(1f, blur));
            }
            return s;
        }

        /// <summary>The panel of a layout's open, stone and piece cells (everything but gaps).</summary>
        public static BoardSilhouette ForLayout(LevelLayout l, int pixelsPerCell, float pad, float radius, float stroke, float blur)
        {
            return Build((c, r) => l.TypeAt(c, r) != CellType.Void, l.Width, l.Height, pixelsPerCell, pad, radius, stroke, blur);
        }

        public static BoardSilhouette ForBoard(Board b, int pixelsPerCell, float pad, float radius, float stroke, float blur)
        {
            return Build((c, r) => b[c, r].Type != CellType.Void, b.Width, b.Height, pixelsPerCell, pad, radius, stroke, blur);
        }

        /// <summary>The fill alpha (0-255) at a board position given in cells from the bottom-left of the cell area.</summary>
        public int FillAt(float col, float row)
        {
            int x = (int)Math.Floor(Margin + col * PixelsPerCell), y = (int)Math.Floor(Margin + row * PixelsPerCell);
            if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            return Fill[y * Width + x];
        }

        static byte Alpha(float a)
        {
            if (a <= 0f) return 0;
            if (a >= 1f) return 255;
            return (byte)(a * 255f + 0.5f);
        }

        /// <summary>Euclidean distance from every pixel to the nearest target pixel (0 on them; large if there are none).</summary>
        static float[] Distance(bool[] target, int w, int h)
        {
            const double Far = 1e20;
            var g = new double[w * h];
            for (int i = 0; i < g.Length; i++) g[i] = target[i] ? 0 : Far;
            int n = Math.Max(w, h);
            var f = new double[n];
            var d = new double[n];
            var v = new int[n];
            var z = new double[n + 1];
            // Columns, then rows: the exact squared distance transform (Felzenszwalb and Huttenlocher).
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) f[y] = g[y * w + x];
                Transform(f, h, d, v, z);
                for (int y = 0; y < h; y++) g[y * w + x] = d[y];
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) f[x] = g[y * w + x];
                Transform(f, w, d, v, z);
                for (int x = 0; x < w; x++) g[y * w + x] = d[x];
            }
            var result = new float[w * h];
            for (int i = 0; i < g.Length; i++) result[i] = (float)Math.Sqrt(g[i]);
            return result;
        }

        static void Transform(double[] f, int n, double[] d, int[] v, double[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }
    }
}
