using System;
using System.Collections.Generic;
using ColourCascade.Procedural;

/// <summary>Composites a mock game board from the generated sprites, for judging the look at play size.</summary>
static class BoardMock
{
    public static ImageData Render(bool symbols, int cols = 8, int rows = 9, int cell = 116)
    {
        var images = new Dictionary<string, ImageData>();
        foreach (var i in ArtBook.BuildAtlasImages()) images[i.Name] = i;
        int pad = 40, W = cols * cell + pad * 2, H = rows * cell + pad * 2;
        var bg = ArtBook.Background(-1, 180, 320);
        var outImg = new ImageData { Name = "mock", Width = W, Height = H, Pixels = new byte[W * H * 4] };
        Blit(outImg, bg, 0, 0, W, H, 1f, Rgba.White);
        var rng = new ColourCascade.Core.Rng(42);
        Blit(outImg, images[ArtNames.Round], pad - 16, pad - 16, cols * cell + 32, rows * cell + 32, 0.55f, Palette.BoardBack);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int x = pad + c * cell, y = pad + r * cell;
                bool stone = (c == 1 || c == cols - 2) && (r == 4 || r == 3);
                Blit(outImg, images[ArtNames.Round], x + 4, y + 4, cell - 8, cell - 8, 0.6f, Palette.Slot);
                if (stone) { Blit(outImg, images[ArtNames.Stone], x + 3, y + 3, cell - 6, cell - 6, 1f, Rgba.White); continue; }
                int col = rng.Next(5);
                Blit(outImg, images[ArtNames.Tile(col)], x + 3, y + 3, cell - 6, cell - 6, 1f, Rgba.White);
                if (symbols) { int s = (int)(cell * 0.5f); Blit(outImg, images[ArtNames.Symbol(col)], x + (cell - s) / 2, y + (cell - s) / 2 + 2, s, s, 1f, Rgba.White); }
            }
        return outImg;
    }

    /// <summary>Every new mechanic on one board: ice (1 and 2 layers), chains (1-3 links), moss, specials and treasure.</summary>
    public static ImageData RenderMechanics(bool symbols, int cell = 140)
    {
        var board = ColourCascade.Core.Board.FromRows(
            new[] { "0-1|2*3@", "1$203$41", "40123401", "23401234", "12340123" },
            new[] { "........", "........", "iI.123..", ".mm..i..", "........" });
        var images = new Dictionary<string, ImageData>();
        foreach (var i in ArtBook.BuildAtlasImages()) images[i.Name] = i;
        int cols = board.Width, rows = board.Height, pad = 40, W = cols * cell + pad * 2, H = rows * cell + pad * 2;
        var outImg = new ImageData { Name = "mock_mechanics", Width = W, Height = H, Pixels = new byte[W * H * 4] };
        Blit(outImg, ArtBook.Background(-1, 180, 320), 0, 0, W, H, 1f, Rgba.White);
        Blit(outImg, images[ArtNames.Round], pad - 16, pad - 16, cols * cell + 32, rows * cell + 32, 0.55f, Palette.BoardBack);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int x = pad + c * cell, y = pad + r * cell;
                var t = board[c, r];
                Blit(outImg, images[ArtNames.Round], x + 4, y + 4, cell - 8, cell - 8, 0.6f, Palette.Slot);
                switch (t.Type)
                {
                    case ColourCascade.Core.CellType.Special:
                        Blit(outImg, images[ArtNames.Special(t.Special)], x + 3, y + 3, cell - 6, cell - 6, 1f, Rgba.White);
                        break;
                    case ColourCascade.Core.CellType.Treasure:
                        Blit(outImg, images[ArtNames.Treasure], x + 3, y + 3, cell - 6, cell - 6, 1f, Rgba.White);
                        break;
                    case ColourCascade.Core.CellType.Colour:
                        Blit(outImg, images[ArtNames.Tile(t.Colour)], x + 3, y + 3, cell - 6, cell - 6, 1f, Rgba.White);
                        var overlay = ArtNames.Obstacle(t.Obstacle, t.Strength);
                        if (overlay != null) Blit(outImg, images[overlay], x + 3, y + 3, cell - 6, cell - 6, 1f, Rgba.White);
                        if (symbols) { int sz = (int)(cell * 0.5f); Blit(outImg, images[ArtNames.Symbol(t.Colour)], x + (cell - sz) / 2, y + (cell - sz) / 2 + 2, sz, sz, 1f, Rgba.White); }
                        break;
                }
            }
        return outImg;
    }

    // Bilinear scaled alpha-over with tint. Destination rows are bottom-up like the sources.
    static void Blit(ImageData dst, ImageData src, int dx, int dy, int dw, int dh, float opacity, Rgba tint)
    {
        for (int y = 0; y < dh; y++)
        {
            int ty = dy + y; if (ty < 0 || ty >= dst.Height) continue;
            float sy = (y + 0.5f) / dh * src.Height - 0.5f;
            for (int x = 0; x < dw; x++)
            {
                int tx = dx + x; if (tx < 0 || tx >= dst.Width) continue;
                float sx = (x + 0.5f) / dw * src.Width - 0.5f;
                Sample(src, sx, sy, out float r, out float g, out float b, out float a);
                a *= opacity * tint.A; r *= tint.R; g *= tint.G; b *= tint.B;
                int di = (ty * dst.Width + tx) * 4;
                float da = dst.Pixels[di + 3] / 255f;
                float oa = a + da * (1 - a);
                for (int c = 0; c < 3; c++)
                {
                    float sc = c == 0 ? r : c == 1 ? g : b;
                    float dc = dst.Pixels[di + c] / 255f;
                    float o = oa > 0 ? (sc * a + dc * da * (1 - a)) / oa : 0;
                    dst.Pixels[di + c] = (byte)Math.Clamp((int)(o * 255 + 0.5f), 0, 255);
                }
                dst.Pixels[di + 3] = (byte)Math.Clamp((int)(oa * 255 + 0.5f), 0, 255);
            }
        }
    }

    static void Sample(ImageData s, float x, float y, out float r, out float g, out float b, out float a)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        float fx = x - x0, fy = y - y0;
        r = g = b = a = 0;
        for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++)
        {
            int xx = Math.Clamp(x0 + i, 0, s.Width - 1), yy = Math.Clamp(y0 + j, 0, s.Height - 1);
            float w = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy);
            int k = (yy * s.Width + xx) * 4;
            float pa = s.Pixels[k + 3] / 255f;
            r += s.Pixels[k] / 255f * pa * w; g += s.Pixels[k + 1] / 255f * pa * w; b += s.Pixels[k + 2] / 255f * pa * w; a += pa * w;
        }
        if (a > 0) { r /= a; g /= a; b /= a; }
    }
}
