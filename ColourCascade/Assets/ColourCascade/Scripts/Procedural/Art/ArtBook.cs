using System;
using System.Collections.Generic;

namespace ColourCascade.Procedural
{
    public enum Icon
    {
        Home, Back, Forward, Close, Gear, Play, Pause, Restart, Map, Shop, Help, Lock, Check, Plus, Minus,
        Calendar, Bolt, Infinity, Trophy, SoundOn, SoundOff, Music, Vibrate, Eye, Sparkle, Gift, Star, Timer,
        Target, Info, Crown,
    }

    public enum Decor
    {
        Cloud, Hill, Tree, Flower, Wave, Bubble, Coral, Crystal, Mesa, Mountain, Snowflake, Planet, Sparkle,
    }

    /// <summary>Names of every generated sprite.</summary>
    public static class ArtNames
    {
        public const string Round = "ui.round";
        public const string RoundShadow = "ui.shadow";
        public const string RoundStroke = "ui.stroke";
        public const string Gloss = "ui.gloss";
        public const string Circle = "ui.circle";
        public const string CircleGloss = "ui.circlegloss";
        public const string CircleShadow = "ui.circleshadow";
        public const string Ring = "ui.ring";
        public const string Glow = "ui.glow";
        public const string Gradient = "ui.gradient";
        public const string Dot = "ui.dot";
        public const string Ribbon = "ui.ribbon";
        public const string Gem = "ui.gem";
        public const string Coin = "art.coin";
        public const string StarGold = "art.star";
        public const string StarEmpty = "art.starempty";
        public const string Hand = "art.hand";
        public const string Stone = "tile.stone";
        public const string TileGlow = "tile.glow";
        public const string TileFlash = "tile.flash";
        public const string ParticleSoft = "fx.soft";
        public const string ParticleSpark = "fx.spark";
        public const string ParticleShard = "fx.shard";
        public const string ParticleRing = "fx.ring";

        /// <summary>Corner radius (in pixels) baked into the Round/Gloss/Stroke sprites.</summary>
        public const float RoundRadiusPx = 64f;
        /// <summary>Corner radius baked into the shadow sprite.</summary>
        public const float ShadowRadiusPx = 48f;

        public static string Tile(int colour) => "tile." + colour;
        public static string Symbol(int colour) => "sym." + colour;
        public static string IconName(Icon icon) => "icon." + icon;
        public static string PowerUp(int id) => "pu." + id;
        public static string DecorName(Decor d) => "decor." + d;
    }

    /// <summary>
    /// Recipes for every sprite in the game. Everything is drawn from signed-distance shapes, so the art is
    /// resolution independent, has no external asset dependencies and can be previewed outside Unity.
    /// </summary>
    public static class ArtBook
    {
        /// <summary>Bump whenever any recipe changes, so cached atlases on devices are regenerated.</summary>
        public const int Version = 3;

        static readonly Rgba W = Rgba.White;
        static Rgba WA(float a) => new Rgba(1, 1, 1, a);
        static Rgba Ink(float a) => new Rgba(0.05f, 0.03f, 0.16f, a);

        public static List<ImageData> BuildAtlasImages()
        {
            var list = new List<ImageData>();
            BuildUi(list);
            for (int c = 0; c < Palette.Tiles.Length; c++)
            {
                list.Add(Tile(ArtNames.Tile(c), Palette.Tiles[c]));
                list.Add(Symbol(c));
            }
            list.Add(StoneTile());
            list.Add(TileGlow());
            list.Add(TileFlash());
            foreach (Icon icon in Enum.GetValues(typeof(Icon))) list.Add(IconImage(icon));
            for (int id = 0; id < 6; id++) list.Add(PowerUpIcon(id));
            foreach (Decor d in Enum.GetValues(typeof(Decor))) list.Add(DecorImage(d));
            list.Add(Coin());
            list.Add(StarGold());
            list.Add(StarEmpty());
            list.Add(Hand());
            BuildParticles(list);
            return list;
        }

        // ------------------------------------------------------------------ UI primitives

        static void BuildUi(List<ImageData> list)
        {
            // Rounded rectangle, 9-sliced. Tinted in code; corner radius controlled via pixelsPerUnitMultiplier.
            {
                var cv = new PixelCanvas(160, 160, 160);
                cv.Fill(new RoundBox(80, 80, 79, 79, ArtNames.RoundRadiusPx), W);
                list.Add(Sliced(cv.ToImage(ArtNames.Round), 70));
            }
            {
                var cv = new PixelCanvas(160, 160, 160);
                cv.Fill(new RoundBox(80, 80, 79, 79, ArtNames.RoundRadiusPx).Outline(3.5f).Intersect(new RoundBox(80, 80, 79, 79, ArtNames.RoundRadiusPx)), W);
                list.Add(Sliced(cv.ToImage(ArtNames.RoundStroke), 70));
            }
            {
                // Top sheen for buttons and cards.
                var cv = new PixelCanvas(160, 160, 160);
                var box = new RoundBox(80, 80, 79, 79, ArtNames.RoundRadiusPx);
                cv.Fill(box.Intersect(HalfPlane.Above(84)), LinearPaint.Vertical(84, WA(0f), 158, WA(0.42f)));
                cv.Fill(box.Outline(2.2f).Intersect(box).Intersect(HalfPlane.Above(100)), WA(0.25f));
                list.Add(Sliced(cv.ToImage(ArtNames.Gloss), 70));
            }
            {
                // Soft shadow: box half 64 + radius 48 + blur 22, 9-sliced.
                var cv = new PixelCanvas(176, 176, 176);
                cv.Fill(new RoundBox(88, 88, 64, 64, ArtNames.ShadowRadiusPx), Rgba.Black, 1f, 22f);
                list.Add(Sliced(cv.ToImage(ArtNames.RoundShadow), 80));
            }
            {
                var cv = new PixelCanvas(128, 128);
                cv.Fill(new Circle(50, 50, 49), W);
                list.Add(cv.ToImage(ArtNames.Circle));
            }
            {
                var cv = new PixelCanvas(128, 128);
                var c = new Circle(50, 50, 49);
                cv.Fill(c.Intersect(HalfPlane.Above(48)), LinearPaint.Vertical(48, WA(0f), 99, WA(0.45f)));
                cv.Fill(c.Outline(1.4f).Intersect(c).Intersect(HalfPlane.Above(62)), WA(0.3f));
                list.Add(cv.ToImage(ArtNames.CircleGloss));
            }
            {
                var cv = new PixelCanvas(128, 128);
                cv.Fill(new Circle(50, 50, 36), Rgba.Black, 1f, 12f);
                list.Add(cv.ToImage(ArtNames.CircleShadow));
            }
            {
                var cv = new PixelCanvas(128, 128);
                cv.Fill(new Circle(50, 50, 45).Outline(3.2f), W);
                list.Add(cv.ToImage(ArtNames.Ring));
            }
            {
                var cv = new PixelCanvas(128, 128);
                cv.Fill(new Circle(50, 50, 22), W, 1f, 26f);
                list.Add(cv.ToImage(ArtNames.Glow));
            }
            {
                var cv = new PixelCanvas(8, 256, 8);
                cv.Fill(RoundBox.FromRect(-10, -10, 18, 266, 0), LinearPaint.Vertical(0, WA(0f), 256, WA(1f)));
                list.Add(cv.ToImage(ArtNames.Gradient));
            }
            {
                var cv = new PixelCanvas(32, 32);
                cv.Fill(new Circle(50, 50, 46), W);
                list.Add(cv.ToImage(ArtNames.Dot));
            }
            {
                // Ribbon banner: white band with forked grey tails; 9-sliced horizontally.
                var cv = new PixelCanvas(256, 96, 256);
                cv.Fill(new Polygon(4, 22, 52, 22, 52, 66, 4, 66, 20, 44), new Rgba(0.68f, 0.68f, 0.72f));
                cv.Fill(new Polygon(252, 22, 204, 22, 204, 66, 252, 66, 236, 44), new Rgba(0.68f, 0.68f, 0.72f));
                cv.Fill(new Polygon(36, 22, 52, 12, 52, 22), new Rgba(0.45f, 0.45f, 0.5f));
                cv.Fill(new Polygon(220, 22, 204, 12, 204, 22), new Rgba(0.45f, 0.45f, 0.5f));
                var band = new RoundBox(128, 50, 92, 32, 6);
                cv.Fill(band, LinearPaint.Vertical(18, new Rgba(0.88f, 0.88f, 0.9f), 82, W));
                cv.Fill(band.Outline(1.5f).Intersect(HalfPlane.Above(66)), WA(0.6f));
                var img = cv.ToImage(ArtNames.Ribbon);
                img.BorderLeft = img.BorderRight = 60;
                list.Add(img);
            }
            {
                // Gem used for milestone nodes: shaded hexagon (tinted in code).
                var cv = new PixelCanvas(128, 128);
                var hex = Polygon.Regular(50, 50, 46, 6, 0).Inflate(2f);
                cv.Fill(hex, new Rgba(0.72f, 0.72f, 0.76f));
                var inner = Polygon.Regular(50, 50, 30, 6, 0);
                cv.Fill(new Polygon(50, 98, 92, 74, 76, 65, 50, 80), new Rgba(0.92f, 0.92f, 0.95f));
                cv.Fill(new Polygon(50, 98, 8, 74, 24, 65, 50, 80), W);
                cv.Fill(new Polygon(8, 74, 8, 26, 24, 35, 24, 65), new Rgba(0.86f, 0.86f, 0.9f));
                cv.Fill(new Polygon(92, 74, 92, 26, 76, 35, 76, 65), new Rgba(0.6f, 0.6f, 0.66f));
                cv.Fill(new Polygon(8, 26, 50, 2, 50, 20, 24, 35), new Rgba(0.62f, 0.62f, 0.68f));
                cv.Fill(new Polygon(92, 26, 50, 2, 50, 20, 76, 35), new Rgba(0.5f, 0.5f, 0.56f));
                cv.Fill(inner, LinearPaint.Vertical(20, new Rgba(0.78f, 0.78f, 0.82f), 80, W));
                cv.Fill(new Ellipse(42, 62, 10, 5).Rotate(-25, 42, 62), WA(0.7f));
                list.Add(cv.ToImage(ArtNames.Gem));
            }
        }

        static ImageData Sliced(ImageData img, int border)
        {
            img.BorderLeft = img.BorderBottom = img.BorderRight = img.BorderTop = border;
            return img;
        }

        // ------------------------------------------------------------------ Tiles

        public static ImageData Tile(string name, Rgba baseColour)
        {
            var cv = new PixelCanvas(192, 192);
            var outer = new RoundBox(50, 50, 48.5f, 48.5f, 21);
            cv.Fill(outer, LinearPaint.Vertical(0, baseColour.Darken(0.45f), 100, baseColour.Darken(0.16f)));
            var face = new RoundBox(50, 53.2f, 44.8f, 44.2f, 18);
            cv.Fill(face, LinearPaint.Vertical(9, baseColour.Darken(0.06f), 97, baseColour.Lighten(0.24f)));
            cv.Fill(new Ellipse(50, 79, 33, 13).Intersect(face), LinearPaint.Vertical(66, WA(0f), 93, WA(0.42f)));
            cv.Fill(face.Outline(0.9f).Intersect(face).Intersect(HalfPlane.Above(62)), WA(0.28f));
            cv.Fill(new Circle(22.5f, 82, 3.8f), WA(0.6f));
            cv.Fill(new Circle(29.5f, 85.5f, 1.8f), WA(0.45f));
            return cv.ToImage(name);
        }

        static ImageData StoneTile()
        {
            var cv = new PixelCanvas(192, 192);
            var outer = new RoundBox(50, 50, 48.5f, 48.5f, 16);
            cv.Fill(outer, LinearPaint.Vertical(0, Palette.StoneDark.Darken(0.35f), 100, Palette.StoneDark));
            var face = new RoundBox(50, 53, 44.5f, 44, 13);
            cv.Fill(face, LinearPaint.Vertical(9, Palette.Stone.Darken(0.1f), 97, Palette.Stone.Lighten(0.2f)));
            float[] speck = { 24, 30, 3.2f, 70, 26, 2.6f, 76, 70, 3.6f, 36, 74, 2.2f, 58, 44, 2.8f, 18, 58, 2f, 84, 46, 2.2f, 48, 18, 2.4f };
            for (int i = 0; i < speck.Length; i += 3)
                cv.Fill(new Circle(speck[i], speck[i + 1], speck[i + 2]), Palette.StoneDark.WithAlpha(0.35f));
            var crack = Ink(0.55f);
            cv.Fill(new Capsule(30, 72, 42, 60, 1.5f).Union(new Capsule(42, 60, 38, 47, 1.5f), new Capsule(42, 60, 57, 55, 1.4f), new Capsule(57, 55, 64, 40, 1.2f)), crack);
            cv.Fill(face.Outline(1f).Intersect(face).Intersect(HalfPlane.Above(64)), WA(0.3f));
            cv.Fill(new Ellipse(36, 82, 16, 5), WA(0.18f));
            return cv.ToImage(ArtNames.Stone);
        }

        static ImageData TileGlow()
        {
            var cv = new PixelCanvas(192, 192);
            cv.Fill(new RoundBox(50, 50, 36, 36, 16), W, 1f, 12f);
            return cv.ToImage(ArtNames.TileGlow);
        }

        static ImageData TileFlash()
        {
            var cv = new PixelCanvas(192, 192);
            cv.Fill(new RoundBox(50, 50, 48.5f, 48.5f, 21), W);
            return cv.ToImage(ArtNames.TileFlash);
        }

        /// <summary>The accessibility glyph for each tile colour.</summary>
        public static Shape SymbolShape(int colour)
        {
            switch (colour)
            {
                case 0: // heart
                    return new Polygon(50, 25, 70, 45, 50, 65, 30, 45).Union(new Circle(40.2f, 55, 14.1f), new Circle(59.8f, 55, 14.1f)).Move(0, -1);
                case 1: // star
                    return Polygon.Star(50, 50, 22, 9.5f, 5).Inflate(3f);
                case 2: // ring (a bold "O", clearly unlike the solid heart and diamond)
                    return new Circle(50, 50, 21f).Minus(new Circle(50, 50, 9f));
                case 3: // triangle
                    return Polygon.Regular(50, 45.5f, 20.5f, 3, 0).Inflate(3.5f);
                case 4: // crescent moon (chosen as the shape least like the others, measured by Jaccard distance)
                    return new Circle(48, 50, 23f).Minus(new Circle(62, 57, 15f));
                default: // cross
                    return new RoundBox(50, 50, 7.5f, 21, 3.5f).Union(new RoundBox(50, 50, 21, 7.5f, 3.5f));
            }
        }

        static ImageData Symbol(int colour)
        {
            var cv = new PixelCanvas(128, 128);
            var glyph = SymbolShape(colour);
            cv.Fill(glyph.Inflate(3f).Move(0, -2.5f), Ink(0.32f), 1f, 3f);
            cv.Fill(glyph.Inflate(2.4f), Ink(0.42f));
            cv.Fill(glyph, LinearPaint.Vertical(28, new Rgba(0.93f, 0.93f, 0.97f), 72, W));
            return cv.ToImage(ArtNames.Symbol(colour));
        }

        // ------------------------------------------------------------------ Icons (white, tinted in code)

        public static Shape IconShape(Icon icon)
        {
            switch (icon)
            {
                case Icon.Home:
                    return new Polygon(50, 80, 22, 54, 78, 54).Inflate(5)
                        .Union(new RoundBox(50, 37, 23, 20, 5))
                        .Minus(new RoundBox(50, 27, 7.5f, 11, 3.5f));
                case Icon.Back:
                    return new Capsule(74, 50, 30, 50, 7).Union(new Capsule(30, 50, 50, 70, 7), new Capsule(30, 50, 50, 30, 7));
                case Icon.Forward:
                    return new Capsule(26, 50, 70, 50, 7).Union(new Capsule(70, 50, 50, 70, 7), new Capsule(70, 50, 50, 30, 7));
                case Icon.Close:
                    return new Capsule(30, 30, 70, 70, 7.5f).Union(new Capsule(30, 70, 70, 30, 7.5f));
                case Icon.Gear:
                    {
                        Shape gear = new Circle(50, 50, 27);
                        for (int i = 0; i < 8; i++) gear = gear.Union(new RoundBox(50, 79, 7.5f, 9, 2.5f).Rotate(i * 45, 50, 50));
                        return gear.Minus(new Circle(50, 50, 11));
                    }
                case Icon.Play:
                    return new Polygon(38, 26, 38, 74, 74, 50).Inflate(5);
                case Icon.Pause:
                    return new RoundBox(37, 50, 8, 25, 4).Union(new RoundBox(63, 50, 8, 25, 4));
                case Icon.Restart:
                    {
                        var ring = new Circle(50, 50, 26).Outline(6.5f).Minus(new Pie(50, 50, 40, 55, 32));
                        var head = new Polygon(62, 86, 80, 64, 56, 60).Inflate(2);
                        return ring.Union(head);
                    }
                case Icon.Map:
                    {
                        var map = new Polygon(16, 24, 38, 16, 62, 24, 84, 16, 84, 74, 62, 82, 38, 74, 16, 82).Inflate(2.5f);
                        return map.Minus(new Capsule(38, 20, 38, 70, 2.2f)).Minus(new Capsule(62, 28, 62, 78, 2.2f));
                    }
                case Icon.Shop:
                    {
                        var bag = new RoundBox(50, 40, 28, 25, 8);
                        var handle = new Circle(50, 64, 14).Outline(4.5f).Intersect(HalfPlane.Above(62));
                        return bag.Union(handle, new Capsule(36, 64, 36, 58, 4.5f), new Capsule(64, 64, 64, 58, 4.5f))
                            .Minus(new Circle(38, 52, 3)).Minus(new Circle(62, 52, 3));
                    }
                case Icon.Help:
                    {
                        var hook = new Circle(50, 62, 15).Outline(6).Intersect(new Pie(50, 62, 30, 40, 140));
                        return hook.Union(new Capsule(50, 47, 50, 39, 6), new Circle(50, 22, 6.8f));
                    }
                case Icon.Lock:
                    {
                        var body = new RoundBox(50, 37, 25, 21, 6);
                        var shackle = new Circle(50, 60, 15).Outline(5).Intersect(HalfPlane.Above(58))
                            .Union(new Capsule(35, 60, 35, 50, 5), new Capsule(65, 60, 65, 50, 5));
                        return body.Union(shackle).Minus(new Circle(50, 40, 5.5f)).Minus(new Capsule(50, 40, 50, 28, 2.6f));
                    }
                case Icon.Check:
                    return new Capsule(24, 52, 42, 32, 8).Union(new Capsule(42, 32, 78, 70, 8));
                case Icon.Plus:
                    return new Capsule(50, 22, 50, 78, 8).Union(new Capsule(22, 50, 78, 50, 8));
                case Icon.Minus:
                    return new Capsule(22, 50, 78, 50, 8);
                case Icon.Calendar:
                    {
                        Shape cal = new RoundBox(50, 44, 33, 31, 8);
                        for (int r = 0; r < 2; r++)
                            for (int c = 0; c < 3; c++)
                                cal = cal.Minus(new RoundBox(33 + c * 17, 42 - r * 15, 5.5f, 5, 1.8f));
                        cal = cal.Minus(new RoundBox(50, 61, 26, 1.6f, 0.8f));
                        return cal.Union(new Capsule(35, 82, 35, 70, 4.5f), new Capsule(65, 82, 65, 70, 4.5f));
                    }
                case Icon.Bolt:
                    return new Polygon(62, 90, 28, 46, 48, 46, 40, 10, 74, 56, 54, 56).Inflate(2.5f);
                case Icon.Infinity:
                    return new Circle(33, 50, 14).Outline(6).Union(new Circle(67, 50, 14).Outline(6));
                case Icon.Trophy:
                    {
                        var cup = new Circle(50, 64, 24).Intersect(HalfPlane.Below(82)).Union(new RoundBox(50, 80, 24, 3, 1.5f));
                        var handles = new Circle(25, 66, 10).Outline(4).Union(new Circle(75, 66, 10).Outline(4)).Minus(new RoundBox(50, 66, 24, 30, 0));
                        return cup.Union(handles, new RoundBox(50, 36, 5, 9, 2), new RoundBox(50, 22, 18, 6, 3));
                    }
                case Icon.SoundOn:
                    {
                        var speaker = new Polygon(18, 40, 32, 40, 50, 24, 50, 76, 32, 60, 18, 60).Inflate(3);
                        var waves = new Circle(52, 50, 16).Outline(4).Union(new Circle(52, 50, 29).Outline(4)).Intersect(new Pie(52, 50, 40, 90, 46));
                        return speaker.Union(waves);
                    }
                case Icon.SoundOff:
                    {
                        var speaker = new Polygon(18, 40, 32, 40, 50, 24, 50, 76, 32, 60, 18, 60).Inflate(3);
                        return speaker.Union(new Capsule(62, 38, 84, 62, 5), new Capsule(62, 62, 84, 38, 5));
                    }
                case Icon.Music:
                    return new Ellipse(34, 28, 12, 10).Union(new Ellipse(70, 36, 12, 10), new Capsule(44, 30, 44, 76, 4.5f), new Capsule(80, 38, 80, 84, 4.5f),
                        new Polygon(40, 66, 84, 76, 84, 90, 40, 80));
                case Icon.Vibrate:
                    return new RoundBox(50, 50, 15, 28, 6).Minus(new RoundBox(50, 53, 9.5f, 18, 2))
                        .Union(new Capsule(24, 38, 24, 62, 3.5f), new Capsule(76, 38, 76, 62, 3.5f), new Capsule(12, 44, 12, 56, 3.5f), new Capsule(88, 44, 88, 56, 3.5f));
                case Icon.Eye:
                    {
                        var almond = new Circle(50, 24, 40).Intersect(new Circle(50, 76, 40));
                        return almond.Minus(new Circle(50, 50, 15)).Union(new Circle(50, 50, 9));
                    }
                case Icon.Sparkle:
                    return Polygon.Star(42, 56, 28, 7, 4).Union(Polygon.Star(72, 28, 14, 4, 4), Polygon.Star(76, 76, 9, 3, 4));
                case Icon.Gift:
                    {
                        var box = new RoundBox(50, 37, 28, 22, 4).Union(new RoundBox(50, 64, 33, 8, 3));
                        box = box.Minus(new RoundBox(50, 46, 4, 40, 0)).Minus(new RoundBox(50, 55.5f, 40, 1.8f, 0));
                        var bow = new Ellipse(39, 79, 11, 7).Rotate(-25, 39, 79).Union(new Ellipse(61, 79, 11, 7).Rotate(25, 61, 79));
                        return box.Union(bow.Minus(new Ellipse(39, 79, 4, 2.5f).Rotate(-25, 39, 79)).Minus(new Ellipse(61, 79, 4, 2.5f).Rotate(25, 61, 79)));
                    }
                case Icon.Star:
                    return Polygon.Star(50, 48, 36, 15, 5).Inflate(4f);
                case Icon.Timer:
                    return new Circle(50, 44, 29).Outline(5.5f).Union(new Capsule(50, 44, 50, 62, 4.5f), new Capsule(50, 44, 63, 37, 4.5f),
                        new RoundBox(50, 86, 9, 4.5f, 2), new Capsule(50, 74, 50, 82, 4), new Capsule(74, 70, 79, 75, 4));
                case Icon.Target:
                    return new Circle(50, 50, 33).Outline(5.5f).Union(new Circle(50, 50, 18).Outline(5.5f), new Circle(50, 50, 6.5f));
                case Icon.Info:
                    return new Circle(50, 50, 38).Minus(new Circle(50, 69, 6.5f)).Minus(new Capsule(50, 54, 50, 30, 6));
                case Icon.Crown:
                    return new Polygon(18, 28, 16, 72, 35, 52, 50, 80, 65, 52, 84, 72, 82, 28).Inflate(2.5f)
                        .Union(new Circle(16, 74, 5), new Circle(50, 82, 5), new Circle(84, 74, 5));
                default:
                    return new Circle(50, 50, 30);
            }
        }

        static ImageData IconImage(Icon icon)
        {
            var cv = new PixelCanvas(128, 128);
            cv.Fill(IconShape(icon), W);
            return cv.ToImage(ArtNames.IconName(icon));
        }

        // ------------------------------------------------------------------ Coloured art

        static ImageData Coin()
        {
            var cv = new PixelCanvas(128, 128);
            var gold = Palette.Gold;
            cv.Fill(new Circle(50, 50, 47), LinearPaint.Vertical(3, Palette.GoldLip.Darken(0.15f), 97, Palette.GoldLip));
            var face = new Circle(50, 53, 41);
            cv.Fill(face, LinearPaint.Vertical(12, gold.Darken(0.05f), 94, gold.Lighten(0.3f)));
            cv.Fill(new Circle(50, 53, 31).Outline(2.4f), Palette.GoldLip.WithAlpha(0.55f));
            cv.Fill(Polygon.Star(50, 52, 17, 7, 5).Inflate(1.5f).Move(0, -1.2f), Palette.GoldLip.WithAlpha(0.6f));
            cv.Fill(Polygon.Star(50, 52, 17, 7, 5).Inflate(1.5f), gold.Lighten(0.45f));
            cv.Fill(new Ellipse(36, 74, 13, 6).Rotate(-30, 36, 74), WA(0.55f));
            return cv.ToImage(ArtNames.Coin);
        }

        static ImageData StarGold()
        {
            var cv = new PixelCanvas(128, 128);
            var star = Polygon.Star(50, 49, 43, 19, 5).Inflate(4f);
            cv.Fill(star.Move(0, -3), Palette.GoldLip.Darken(0.25f));
            cv.Fill(star, LinearPaint.Vertical(8, Palette.GoldLip, 95, Palette.Gold.Lighten(0.15f)));
            var inner = Polygon.Star(50, 51, 33, 15, 5).Inflate(3f);
            cv.Fill(inner, LinearPaint.Vertical(18, Palette.Gold, 85, Palette.Gold.Lighten(0.45f)));
            cv.Fill(new Ellipse(40, 66, 9, 5).Rotate(-30, 40, 66), WA(0.6f));
            return cv.ToImage(ArtNames.StarGold);
        }

        static ImageData StarEmpty()
        {
            var cv = new PixelCanvas(128, 128);
            var star = Polygon.Star(50, 49, 43, 19, 5).Inflate(4f);
            cv.Fill(star, Ink(0.45f));
            cv.Fill(star.Outline(1.6f).Intersect(star), WA(0.22f));
            return cv.ToImage(ArtNames.StarEmpty);
        }

        static ImageData Hand()
        {
            var cv = new PixelCanvas(128, 128);
            var hand = new Capsule(40, 50, 40, 86, 8.5f)
                .Union(new RoundBox(54, 36, 21, 22, 11), new Capsule(56, 52, 56, 44, 7.5f), new Capsule(69, 49, 69, 40, 7), new Capsule(33, 38, 23, 52, 7));
            cv.Fill(hand.Inflate(3.5f).Move(1.5f, -2.5f), Ink(0.35f), 1f, 3f);
            cv.Fill(hand.Inflate(3f), Palette.TextDark);
            cv.Fill(hand, LinearPaint.Vertical(14, new Rgba(0.9f, 0.9f, 0.95f), 90, W));
            cv.Fill(new Capsule(36, 80, 36, 66, 2), WA(0.8f));
            return cv.ToImage(ArtNames.Hand);
        }

        /// <summary>Power-up artwork, drawn to sit on a circle of the power-up's accent colour.</summary>
        static ImageData PowerUpIcon(int id)
        {
            var cv = new PixelCanvas(160, 160);
            var outline = Ink(0.35f);
            switch (id)
            {
                case 0: // Blast: bomb
                    {
                        var body = new Circle(45, 43, 27);
                        cv.Fill(body.Move(0, -2.5f), outline, 1f, 2f);
                        cv.Fill(body, LinearPaint.Vertical(16, Rgba.Hex("#1A1438"), 70, Rgba.Hex("#4A3F86")));
                        cv.Fill(new Ellipse(35, 56, 9, 5.5f).Rotate(-35, 35, 56), WA(0.5f));
                        cv.Fill(new RoundBox(64, 64, 9, 6.5f, 2).Rotate(45, 64, 64), Rgba.Hex("#6B63A8"));
                        cv.Fill(new Capsule(68, 69, 74, 77, 2.6f).Union(new Capsule(74, 77, 80, 80, 2.6f)), Rgba.Hex("#C9A27A"));
                        cv.Fill(Polygon.Star(82, 82, 13, 5, 6), Rgba.Hex("#FFD54A"));
                        cv.Fill(Polygon.Star(82, 82, 7, 3, 6, 30), W);
                        break;
                    }
                case 1: // Swap: two arrows
                    {
                        var top = new Capsule(22, 62, 66, 62, 6.5f).Union(new Polygon(64, 78, 84, 62, 64, 46).Inflate(2));
                        var bottom = new Capsule(78, 38, 34, 38, 6.5f).Union(new Polygon(36, 54, 16, 38, 36, 22).Inflate(2));
                        cv.Fill(top.Union(bottom).Move(0, -2.5f), outline, 1f, 2f);
                        cv.Fill(top, W);
                        cv.Fill(bottom, Rgba.Hex("#E9FFFB"));
                        break;
                    }
                case 2: // Colour Shift: palette
                    {
                        var palette = new Ellipse(50, 50, 38, 32).Minus(new Circle(64, 32, 8)).Minus(new Ellipse(86, 42, 10, 7));
                        cv.Fill(palette.Move(0, -2.5f), outline, 1f, 2f);
                        cv.Fill(palette, LinearPaint.Vertical(18, new Rgba(0.9f, 0.88f, 0.96f), 82, W));
                        cv.Fill(new Circle(32, 56, 7.5f), Palette.Tiles[0]);
                        cv.Fill(new Circle(48, 68, 7.5f), Palette.Tiles[1]);
                        cv.Fill(new Circle(66, 64, 7.5f), Palette.Tiles[2]);
                        cv.Fill(new Circle(32, 38, 7.5f), Palette.Tiles[3]);
                        break;
                    }
                case 3: // Tile Breaker: hammer
                    {
                        var handle = new Capsule(28, 22, 58, 56, 5.5f);
                        var head = new RoundBox(62, 62, 24, 11, 4).Rotate(-45, 62, 62);
                        cv.Fill(handle.Union(head).Move(0, -2.5f), outline, 1f, 2f);
                        cv.Fill(handle, LinearPaint.Vertical(20, Rgba.Hex("#9A5F35"), 56, Rgba.Hex("#D99A60")));
                        cv.Fill(head, LinearPaint.Vertical(40, Rgba.Hex("#8D96BD"), 84, Rgba.Hex("#F0F3FF")));
                        cv.Fill(new RoundBox(62, 62, 24, 11, 4).Rotate(-45, 62, 62).Outline(1.2f), Ink(0.25f));
                        cv.Fill(new RoundBox(20, 70, 4, 4, 1).Rotate(20, 20, 70), WA(0.9f));
                        cv.Fill(new RoundBox(28, 82, 3, 3, 1).Rotate(40, 28, 82), WA(0.8f));
                        cv.Fill(new RoundBox(84, 26, 3.5f, 3.5f, 1).Rotate(10, 84, 26), WA(0.8f));
                        break;
                    }
                case 4: // Extra Moves: circular arrow with plus
                    {
                        var ring = new Circle(50, 50, 30).Outline(6.5f).Minus(new Pie(50, 50, 45, 40, 26));
                        var head = new Polygon(60, 90, 80, 70, 56, 62).Inflate(2);
                        var plus = new Capsule(50, 38, 50, 62, 6).Union(new Capsule(38, 50, 62, 50, 6));
                        cv.Fill(ring.Union(head, plus).Move(0, -2.5f), outline, 1f, 2f);
                        cv.Fill(ring.Union(head), W);
                        cv.Fill(plus, W);
                        break;
                    }
                default: // Colour Burst: petals of every colour around a star
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            double a = (90 + i * 60) * Math.PI / 180;
                            float x = 50 + (float)Math.Cos(a) * 27, y = 50 + (float)Math.Sin(a) * 27;
                            cv.Fill(new Circle(x, y - 2, 12), outline, 1f, 2f);
                            cv.Fill(new Circle(x, y, 11.5f), LinearPaint.Vertical(y - 11, Palette.Tiles[i].Darken(0.12f), y + 11, Palette.Tiles[i].Lighten(0.2f)));
                        }
                        cv.Fill(Polygon.Star(50, 50, 18, 8, 4).Inflate(1.5f), W);
                        cv.Fill(Polygon.Star(50, 50, 13, 5, 4, 45), W);
                        break;
                    }
            }
            return cv.ToImage(ArtNames.PowerUp(id));
        }

        // ------------------------------------------------------------------ Map decorations (white silhouettes, tinted)

        static ImageData DecorImage(Decor d)
        {
            var cv = new PixelCanvas(128, 128);
            Shape s;
            switch (d)
            {
                case Decor.Cloud:
                    s = new Circle(36, 46, 16).Union(new Circle(56, 54, 21), new Circle(74, 44, 14), new RoundBox(54, 36, 34, 8, 8));
                    cv.Fill(s, W);
                    cv.Fill(s.Intersect(HalfPlane.Below(40)), new Rgba(0.85f, 0.87f, 0.95f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Hill:
                    s = new Ellipse(50, 18, 50, 44).Intersect(HalfPlane.Above(16));
                    cv.Fill(s, LinearPaint.Vertical(16, new Rgba(0.82f, 0.82f, 0.86f), 62, W));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Tree:
                    cv.Fill(new RoundBox(50, 24, 5, 14, 2), new Rgba(0.55f, 0.55f, 0.6f));
                    s = new Circle(50, 58, 24).Union(new Circle(36, 48, 15), new Circle(64, 48, 15));
                    cv.Fill(s, LinearPaint.Vertical(34, new Rgba(0.78f, 0.78f, 0.82f), 82, W));
                    cv.Fill(new Ellipse(42, 68, 8, 5).Rotate(-30, 42, 68), WA(0.5f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Flower:
                    cv.Fill(new Capsule(50, 10, 50, 46, 2.5f), new Rgba(0.6f, 0.6f, 0.65f));
                    for (int i = 0; i < 5; i++)
                    {
                        double a = (90 + i * 72) * Math.PI / 180;
                        cv.Fill(new Circle(50 + (float)Math.Cos(a) * 13, 56 + (float)Math.Sin(a) * 13, 10), W);
                    }
                    cv.Fill(new Circle(50, 56, 8), new Rgba(0.75f, 0.75f, 0.78f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Wave:
                    s = new Circle(20, 30, 14).Union(new Circle(50, 30, 14), new Circle(80, 30, 14)).Outline(3.2f).Intersect(HalfPlane.Above(30));
                    cv.Fill(s, W);
                    cv.Fill(s.Move(15, 26), WA(0.7f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Bubble:
                    cv.Fill(new Circle(50, 50, 30).Outline(3f), W);
                    cv.Fill(new Circle(50, 50, 28.5f), WA(0.15f));
                    cv.Fill(new Ellipse(40, 62, 8, 5).Rotate(-35, 40, 62), WA(0.8f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Coral:
                    s = new Capsule(50, 10, 50, 50, 5).Union(new Capsule(50, 36, 30, 58, 4.5f), new Capsule(30, 58, 28, 76, 4),
                        new Capsule(50, 46, 70, 66, 4.5f), new Capsule(70, 66, 66, 84, 4), new Capsule(70, 66, 82, 74, 3.5f), new Capsule(50, 50, 52, 82, 4));
                    cv.Fill(s, W);
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Crystal:
                    cv.Fill(new Polygon(50, 92, 66, 70, 62, 12, 38, 12, 34, 70), W);
                    cv.Fill(new Polygon(50, 92, 66, 70, 62, 12, 50, 12), new Rgba(0.78f, 0.78f, 0.84f));
                    cv.Fill(new Polygon(26, 60, 36, 46, 34, 12, 18, 12, 16, 46), new Rgba(0.88f, 0.88f, 0.92f));
                    cv.Fill(new Polygon(76, 52, 86, 40, 84, 12, 68, 12, 66, 40), new Rgba(0.7f, 0.7f, 0.76f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Mesa:
                    s = new Polygon(8, 12, 22, 58, 34, 66, 66, 66, 78, 54, 92, 12).Inflate(2);
                    cv.Fill(s, LinearPaint.Vertical(12, new Rgba(0.78f, 0.78f, 0.82f), 66, W));
                    cv.Fill(new RoundBox(50, 44, 40, 1.5f, 1).Intersect(s), new Rgba(0.7f, 0.7f, 0.75f));
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Mountain:
                    s = new Polygon(4, 12, 40, 80, 58, 52, 70, 66, 96, 12);
                    cv.Fill(s, new Rgba(0.78f, 0.8f, 0.88f));
                    cv.Fill(new Polygon(40, 80, 30, 61, 36, 64, 42, 58, 48, 64, 51, 61), W);
                    return cv.ToImage(ArtNames.DecorName(d));
                case Decor.Snowflake:
                    {
                        Shape flake = new Capsule(50, 18, 50, 82, 3.2f);
                        flake = flake.Union(flake.Rotate(60, 50, 50), flake.Rotate(120, 50, 50));
                        Shape twig = new Capsule(50, 70, 42, 78, 2.5f).Union(new Capsule(50, 70, 58, 78, 2.5f));
                        for (int i = 0; i < 6; i++) flake = flake.Union(twig.Rotate(i * 60, 50, 50));
                        cv.Fill(flake, W);
                        return cv.ToImage(ArtNames.DecorName(d));
                    }
                case Decor.Planet:
                    cv.Fill(new Circle(50, 50, 22), LinearPaint.Vertical(28, new Rgba(0.75f, 0.75f, 0.8f), 72, W));
                    cv.Fill(new Ellipse(50, 50, 40, 10).Rotate(-18, 50, 50).Outline(2.4f).Minus(new Circle(50, 50, 22).Intersect(HalfPlane.Above(48))), W);
                    return cv.ToImage(ArtNames.DecorName(d));
                default:
                    cv.Fill(Polygon.Star(50, 50, 30, 7, 4), W);
                    return cv.ToImage(ArtNames.DecorName(d));
            }
        }

        static void BuildParticles(List<ImageData> list)
        {
            {
                var cv = new PixelCanvas(64, 64);
                cv.Fill(new Circle(50, 50, 22), W, 1f, 20f);
                list.Add(cv.ToImage(ArtNames.ParticleSoft));
            }
            {
                var cv = new PixelCanvas(64, 64);
                cv.Fill(Polygon.Star(50, 50, 46, 9, 4), W);
                cv.Fill(new Circle(50, 50, 12), W, 1f, 8f);
                list.Add(cv.ToImage(ArtNames.ParticleSpark));
            }
            {
                var cv = new PixelCanvas(32, 32);
                cv.Fill(new RoundBox(50, 50, 40, 40, 14), W);
                list.Add(cv.ToImage(ArtNames.ParticleShard));
            }
            {
                var cv = new PixelCanvas(128, 128);
                cv.Fill(new Circle(50, 50, 44).Outline(3.5f), W, 1f, 1.5f);
                list.Add(cv.ToImage(ArtNames.ParticleRing));
            }
        }

        // ------------------------------------------------------------------ Backgrounds (separate textures)

        /// <summary>Full-screen background. theme -1 = the main menu/game night sky; 0-5 = map regions.</summary>
        public static ImageData Background(int theme, int width = 180, int height = 320)
        {
            var cv = new PixelCanvas(width, height, 100);
            float h = cv.UnitsH;
            Rgba top, bottom, accentA, accentB;
            if (theme < 0)
            {
                top = Rgba.Hex("#2C1B70");
                bottom = Rgba.Hex("#0F0A2B");
                accentA = Rgba.Hex("#FF4FA3");
                accentB = Rgba.Hex("#3D8BFF");
            }
            else
            {
                var r = Palette.Regions[theme % Palette.Regions.Length];
                top = r[0];
                bottom = r[1];
                accentA = r[2];
                accentB = r[3];
            }
            cv.Fill(RoundBox.FromRect(-5, -5, 105, h + 5, 0), LinearPaint.Vertical(0, bottom, h, top));
            cv.Fill(new Circle(12, h * 0.78f, 40), accentA.WithAlpha(theme < 0 ? 0.22f : 0.18f), 1f, 34f);
            cv.Fill(new Circle(92, h * 0.5f, 38), accentB.WithAlpha(theme < 0 ? 0.26f : 0.2f), 1f, 34f);
            cv.Fill(new Circle(60, h * 0.1f, 40), (theme < 0 ? Palette.Gold : accentA).WithAlpha(0.12f), 1f, 34f);
            if (theme < 0 || theme == 5)
            {
                uint seed = 12345;
                for (int i = 0; i < 70; i++)
                {
                    seed = seed * 1664525u + 1013904223u;
                    float x = (seed >> 8) % 1000 / 10f;
                    seed = seed * 1664525u + 1013904223u;
                    float y = (seed >> 8) % 1000 / 1000f * h;
                    seed = seed * 1664525u + 1013904223u;
                    float s = 0.25f + (seed >> 8) % 100 / 160f;
                    cv.Fill(new Circle(x, y, s), WA(0.25f + (seed >> 4) % 50 / 100f));
                }
            }
            var img = cv.ToImage(theme < 0 ? "bg.main" : "bg.region." + theme);
            return img;
        }
    }
}
