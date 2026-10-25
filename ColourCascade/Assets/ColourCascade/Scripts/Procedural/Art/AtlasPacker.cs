using System;
using System.Collections.Generic;

namespace ColourCascade.Procedural
{
    public struct AtlasRect
    {
        public int X, Y, Width, Height;
    }

    /// <summary>Where a sprite lives in the atlas plus its 9-slice border and pivot.</summary>
    public struct SpriteMeta
    {
        public AtlasRect Rect;
        public int BorderLeft, BorderBottom, BorderRight, BorderTop;
        public float PivotX, PivotY;
    }

    public sealed class AtlasResult
    {
        public ImageData Image;
        public readonly Dictionary<string, SpriteMeta> Sprites = new Dictionary<string, SpriteMeta>();
    }

    /// <summary>
    /// Packs generated images into one texture (shelf packing, tallest first) so the UI batches into very few
    /// draw calls. Each image gets a 2-pixel padding filled by extruding its edge pixels, which prevents
    /// neighbouring sprites bleeding in when the texture is filtered.
    /// </summary>
    public static class AtlasPacker
    {
        public const int Padding = 2;

        public static AtlasResult Pack(List<ImageData> images, int maxSize = 4096)
        {
            var order = new List<ImageData>(images);
            order.Sort((a, b) => b.Height != a.Height ? b.Height.CompareTo(a.Height) : b.Width.CompareTo(a.Width));
            int[] widths = { 1024, 2048, 2048, 4096 };
            int[] heights = { 1024, 1024, 2048, 4096 };
            for (int attempt = 0; attempt < widths.Length; attempt++)
            {
                int w = widths[attempt], h = heights[attempt];
                if (w > maxSize || h > maxSize) break;
                var rects = TryPack(order, w, h);
                if (rects == null) continue;
                return Compose(order, rects, w, h);
            }
            throw new InvalidOperationException("Images do not fit in a " + maxSize + " atlas");
        }

        static AtlasRect[] TryPack(List<ImageData> order, int w, int h)
        {
            var rects = new AtlasRect[order.Count];
            int x = 0, y = 0, shelf = 0;
            for (int i = 0; i < order.Count; i++)
            {
                int iw = order[i].Width + Padding * 2, ih = order[i].Height + Padding * 2;
                if (iw > w) return null;
                if (x + iw > w)
                {
                    x = 0;
                    y += shelf;
                    shelf = 0;
                }
                if (y + ih > h) return null;
                rects[i] = new AtlasRect { X = x + Padding, Y = y + Padding, Width = order[i].Width, Height = order[i].Height };
                x += iw;
                shelf = Math.Max(shelf, ih);
            }
            return rects;
        }

        static AtlasResult Compose(List<ImageData> order, AtlasRect[] rects, int w, int h)
        {
            var pixels = new byte[w * h * 4];
            var result = new AtlasResult();
            for (int i = 0; i < order.Count; i++)
            {
                var img = order[i];
                var r = rects[i];
                for (int py = -Padding; py < img.Height + Padding; py++)
                {
                    int sy = py < 0 ? 0 : py >= img.Height ? img.Height - 1 : py;
                    int dy = r.Y + py;
                    for (int px = -Padding; px < img.Width + Padding; px++)
                    {
                        int sx = px < 0 ? 0 : px >= img.Width ? img.Width - 1 : px;
                        int dx = r.X + px;
                        int si = (sy * img.Width + sx) * 4;
                        int di = (dy * w + dx) * 4;
                        bool edge = px < 0 || py < 0 || px >= img.Width || py >= img.Height;
                        pixels[di] = img.Pixels[si];
                        pixels[di + 1] = img.Pixels[si + 1];
                        pixels[di + 2] = img.Pixels[si + 2];
                        // Extruded padding keeps colour but only keeps alpha for opaque edges (sliced sprites).
                        pixels[di + 3] = edge && img.Pixels[si + 3] < 250 ? (byte)0 : img.Pixels[si + 3];
                    }
                }
                result.Sprites[img.Name] = new SpriteMeta
                {
                    Rect = r,
                    BorderLeft = img.BorderLeft,
                    BorderBottom = img.BorderBottom,
                    BorderRight = img.BorderRight,
                    BorderTop = img.BorderTop,
                    PivotX = img.PivotX,
                    PivotY = img.PivotY,
                };
            }
            result.Image = new ImageData { Name = "atlas", Width = w, Height = h, Pixels = pixels };
            return result;
        }
    }
}
