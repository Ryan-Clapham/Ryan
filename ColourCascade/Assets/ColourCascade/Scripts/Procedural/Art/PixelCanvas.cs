using System;

namespace ColourCascade.Procedural
{
    /// <summary>A generated image: RGBA32 bytes (straight alpha), row 0 at the BOTTOM (Unity texture order).
    /// Border is the 9-slice border in pixels (left, bottom, right, top); all zero for simple sprites.</summary>
    public sealed class ImageData
    {
        public string Name = "";
        public int Width;
        public int Height;
        public byte[] Pixels;
        public int BorderLeft, BorderBottom, BorderRight, BorderTop;
        /// <summary>Pivot in 0..1 (default centre).</summary>
        public float PivotX = 0.5f, PivotY = 0.5f;
    }

    /// <summary>
    /// Anti-aliased SDF rasteriser. Shapes are described in "design units" (default 100 × 100 regardless of
    /// pixel size), so every sprite can be regenerated at any resolution.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        /// <summary>Pixels per design unit.</summary>
        public readonly float Scale;
        /// <summary>Design-unit extents.</summary>
        public readonly float UnitsW, UnitsH;
        // Premultiplied colour accumulation.
        readonly float[] pr, pg, pb, pa;

        public PixelCanvas(int width, int height, float unitsWide = 100f)
        {
            Width = width;
            Height = height;
            Scale = width / unitsWide;
            UnitsW = unitsWide;
            UnitsH = height / Scale;
            int n = width * height;
            pr = new float[n];
            pg = new float[n];
            pb = new float[n];
            pa = new float[n];
        }

        /// <summary>Fills a shape. softness widens the edge (in design units) for glows/shadows.</summary>
        public void Fill(Shape shape, Paint paint, float opacity = 1f, float softness = 0f)
        {
            float aaUnits = 1f / Scale;
            float edge = Math.Max(aaUnits, softness);
            float bx0, by0, bx1, by1;
            shape.Bounds(out bx0, out by0, out bx1, out by1);
            int x0 = Math.Max(0, (int)Math.Floor((bx0 - edge) * Scale) - 1);
            int y0 = Math.Max(0, (int)Math.Floor((by0 - edge) * Scale) - 1);
            int x1 = Math.Min(Width - 1, (int)Math.Ceiling((bx1 + edge) * Scale) + 1);
            int y1 = Math.Min(Height - 1, (int)Math.Ceiling((by1 + edge) * Scale) + 1);
            for (int py = y0; py <= y1; py++)
            {
                float uy = (py + 0.5f) / Scale;
                int row = py * Width;
                for (int px = x0; px <= x1; px++)
                {
                    float ux = (px + 0.5f) / Scale;
                    float d = shape.Dist(ux, uy);
                    float cov;
                    if (softness > 0)
                    {
                        float t = 0.5f - d / (2f * edge);
                        cov = t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
                    }
                    else
                    {
                        float t = 0.5f - d / edge;
                        cov = t <= 0 ? 0 : t >= 1 ? 1 : t;
                    }
                    if (cov <= 0) continue;
                    var c = paint.At(ux, uy);
                    float a = c.A * cov * opacity;
                    if (a <= 0) continue;
                    int i = row + px;
                    float inv = 1f - a;
                    pr[i] = c.R * a + pr[i] * inv;
                    pg[i] = c.G * a + pg[i] * inv;
                    pb[i] = c.B * a + pb[i] * inv;
                    pa[i] = a + pa[i] * inv;
                }
            }
        }

        /// <summary>Soft drop shadow / glow of a shape.</summary>
        public void Shadow(Shape shape, Rgba colour, float blur, float dx = 0, float dy = 0)
        {
            Fill(shape.Move(dx, dy), colour, 1f, blur);
        }

        /// <summary>Multiplies existing alpha by a shape mask (used to clip decorations to a tile).</summary>
        public void ClipTo(Shape shape)
        {
            float edge = 1f / Scale;
            for (int py = 0; py < Height; py++)
            {
                float uy = (py + 0.5f) / Scale;
                for (int px = 0; px < Width; px++)
                {
                    int i = py * Width + px;
                    if (pa[i] <= 0) continue;
                    float d = shape.Dist((px + 0.5f) / Scale, uy);
                    float t = 0.5f - d / edge;
                    float cov = t <= 0 ? 0 : t >= 1 ? 1 : t;
                    pr[i] *= cov; pg[i] *= cov; pb[i] *= cov; pa[i] *= cov;
                }
            }
        }

        /// <summary>Converts to straight-alpha RGBA32. Fully transparent pixels take the colour of nearby
        /// opaque ones ("alpha bleeding") so bilinear filtering never shows dark fringes.</summary>
        public ImageData ToImage(string name, bool bleed = true)
        {
            int n = Width * Height;
            var bytes = new byte[n * 4];
            var r = new float[n];
            var g = new float[n];
            var b = new float[n];
            var known = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float a = pa[i];
                if (a > 1e-4f)
                {
                    r[i] = pr[i] / a; g[i] = pg[i] / a; b[i] = pb[i] / a;
                    known[i] = true;
                }
            }
            if (bleed) Bleed(r, g, b, known, 3);
            for (int i = 0; i < n; i++)
            {
                bytes[i * 4] = ToByte(r[i]);
                bytes[i * 4 + 1] = ToByte(g[i]);
                bytes[i * 4 + 2] = ToByte(b[i]);
                bytes[i * 4 + 3] = ToByte(pa[i]);
            }
            return new ImageData { Name = name, Width = Width, Height = Height, Pixels = bytes };
        }

        void Bleed(float[] r, float[] g, float[] b, bool[] known, int passes)
        {
            var next = new bool[known.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                Array.Copy(known, next, known.Length);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int i = y * Width + x;
                        if (known[i]) continue;
                        float sr = 0, sg = 0, sb = 0;
                        int count = 0;
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            int yy = y + oy;
                            if (yy < 0 || yy >= Height) continue;
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int xx = x + ox;
                                if (xx < 0 || xx >= Width) continue;
                                int j = yy * Width + xx;
                                if (!known[j]) continue;
                                sr += r[j]; sg += g[j]; sb += b[j];
                                count++;
                            }
                        }
                        if (count == 0) continue;
                        r[i] = sr / count; g[i] = sg / count; b[i] = sb / count;
                        next[i] = true;
                    }
                }
                Array.Copy(next, known, known.Length);
            }
        }

        static byte ToByte(float v)
        {
            int i = (int)(v * 255f + 0.5f);
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }
    }
}
