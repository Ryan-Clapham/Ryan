using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using ColourCascade.Procedural;

/// <summary>Writes the generated sprites, atlas and backgrounds to PNG files for visual review.</summary>
static class ArtPreview
{
    public static void Export(string dir)
    {
        Directory.CreateDirectory(dir);
        var sw = Stopwatch.StartNew();
        var images = ArtBook.BuildAtlasImages();
        long genMs = sw.ElapsedMilliseconds;
        var atlas = AtlasPacker.Pack(images);
        Console.WriteLine($"Generated {images.Count} sprites in {genMs} ms; atlas {atlas.Image.Width}x{atlas.Image.Height} packed in {sw.ElapsedMilliseconds - genMs} ms");
        WritePng(Path.Combine(dir, "atlas.png"), atlas.Image, new byte[] { 34, 24, 80 });
        WritePng(Path.Combine(dir, "bg_main.png"), ArtBook.Background(-1), null);
        for (int t = 0; t < 6; t++) WritePng(Path.Combine(dir, $"bg_region{t}.png"), ArtBook.Background(t, 64, 256), null);
        foreach (var img in images) WritePng(Path.Combine(dir, img.Name + ".png"), img, new byte[] { 34, 24, 80 });
        var t0 = sw.ElapsedMilliseconds;
        var sfx = SoundSynth.BuildEffects();
        var music = SoundSynth.BuildMusic();
        Console.WriteLine($"Synthesised {sfx.Count} effects + {music.Samples.Length / (float)music.SampleRate:0.0}s music in {sw.ElapsedMilliseconds - t0} ms");
        foreach (var snd in sfx) foreach (var v in snd.Samples) if (float.IsNaN(v) || Math.Abs(v) > 1f) throw new Exception("Bad sample in " + snd.Name);
        WritePng(Path.Combine(dir, "mock_board.png"), BoardMock.Render(false), null);
        WritePng(Path.Combine(dir, "mock_board_symbols.png"), BoardMock.Render(true), null);
        WritePng(Path.Combine(dir, "mock_mechanics.png"), BoardMock.RenderMechanics(false), null);
        WritePng(Path.Combine(dir, "mock_mechanics_symbols.png"), BoardMock.RenderMechanics(true), null);
    }

    /// <summary>Writes RGBA (row 0 = bottom) as PNG, optionally composited over a background colour.</summary>
    public static void WritePng(string path, ImageData img, byte[] matte)
    {
        int w = img.Width, h = img.Height;
        var raw = new byte[(w * 4 + 1) * h];
        for (int y = 0; y < h; y++)
        {
            int srcRow = h - 1 - y;
            int o = y * (w * 4 + 1);
            raw[o] = 0;
            for (int x = 0; x < w; x++)
            {
                int si = (srcRow * w + x) * 4;
                int di = o + 1 + x * 4;
                byte a = img.Pixels[si + 3];
                if (matte != null)
                {
                    for (int c = 0; c < 3; c++) raw[di + c] = (byte)((img.Pixels[si + c] * a + matte[c] * (255 - a)) / 255);
                    raw[di + 3] = 255;
                }
                else
                {
                    for (int c = 0; c < 4; c++) raw[di + c] = img.Pixels[si + c];
                }
            }
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
        ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        Chunk(fs, "IHDR", ihdr);
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    static void WriteBE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(t); s.Write(data);
        uint crc = Crc(t, 0xFFFFFFFFu); crc = Crc(data, crc) ^ 0xFFFFFFFFu;
        var c = new byte[4]; WriteBE(c, 0, (int)crc); s.Write(c);
    }

    static uint[] table;
    static uint Crc(byte[] d, uint c)
    {
        if (table == null)
        {
            table = new uint[256];
            for (uint n = 0; n < 256; n++) { uint k = n; for (int i = 0; i < 8; i++) k = (k & 1) != 0 ? 0xEDB88320u ^ (k >> 1) : k >> 1; table[n] = k; }
        }
        foreach (var b in d) c = table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c;
    }
}
