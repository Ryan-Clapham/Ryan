using System;
using System.IO;
using System.Text;

namespace ColourCascade.Procedural
{
    /// <summary>
    /// Binary cache of a generated atlas, so the art is only drawn on the first launch of each app version.
    /// Any mismatch (key, truncation, corruption) makes Read return null and the atlas is regenerated.
    /// </summary>
    public static class AtlasCache
    {
        const uint Magic = 0x54414343; // "CCAT"

        public static void Write(Stream stream, AtlasResult atlas, string key)
        {
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(Magic);
                w.Write(key ?? "");
                w.Write(atlas.Image.Width);
                w.Write(atlas.Image.Height);
                w.Write(atlas.Sprites.Count);
                foreach (var kv in atlas.Sprites)
                {
                    var m = kv.Value;
                    w.Write(kv.Key);
                    w.Write(m.Rect.X); w.Write(m.Rect.Y); w.Write(m.Rect.Width); w.Write(m.Rect.Height);
                    w.Write(m.BorderLeft); w.Write(m.BorderBottom); w.Write(m.BorderRight); w.Write(m.BorderTop);
                    w.Write(m.PivotX); w.Write(m.PivotY);
                }
                w.Write(atlas.Image.Pixels.Length);
                w.Write(atlas.Image.Pixels);
                w.Write(Magic);
            }
        }

        public static AtlasResult Read(Stream stream, string key)
        {
            try
            {
                using (var r = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (r.ReadUInt32() != Magic) return null;
                    if (r.ReadString() != (key ?? "")) return null;
                    int w = r.ReadInt32(), h = r.ReadInt32(), count = r.ReadInt32();
                    if (w <= 0 || h <= 0 || w > 8192 || h > 8192 || count < 0 || count > 10000) return null;
                    var result = new AtlasResult();
                    for (int i = 0; i < count; i++)
                    {
                        string name = r.ReadString();
                        var m = new SpriteMeta
                        {
                            Rect = new AtlasRect { X = r.ReadInt32(), Y = r.ReadInt32(), Width = r.ReadInt32(), Height = r.ReadInt32() },
                            BorderLeft = r.ReadInt32(), BorderBottom = r.ReadInt32(), BorderRight = r.ReadInt32(), BorderTop = r.ReadInt32(),
                            PivotX = r.ReadSingle(), PivotY = r.ReadSingle(),
                        };
                        result.Sprites[name] = m;
                    }
                    int len = r.ReadInt32();
                    if (len != w * h * 4) return null;
                    var pixels = r.ReadBytes(len);
                    if (pixels.Length != len || r.ReadUInt32() != Magic) return null;
                    result.Image = new ImageData { Name = "atlas", Width = w, Height = h, Pixels = pixels };
                    return result;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
