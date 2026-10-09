using System;
using System.Collections.Generic;
using ChuchuGames.ProcArt;
using static ChuchuGames.ProcArt.Shapes;

namespace HumanError.EditorTools.ArtGen
{
    /// <summary>
    /// Placeholder art in the GDD §9 style: flat vector, thick ink outline, two flat tones per shape,
    /// teal bio-glow accents. Visitor parts all share one 512×512 anchor grid (neck, eye line, crown) so
    /// any combination fits, and skin parts are drawn in greyscale so the view tints them per clan.
    /// Final art replaces these files 1:1 by keeping the same names. Engine-free: runs from the Unity
    /// menu and from Tools/ArtGen (plain .NET).
    /// </summary>
    public static class HumanErrorArt
    {
        // GDD §9 palette
        static readonly Rgba MossNight = Rgba.Hex("#0E1A1F"), BoothGlass = Rgba.Hex("#173B44"), BioGlow = Rgba.Hex("#3FE0C5"),
            Coral = Rgba.Hex("#FF6B57"), PaperSand = Rgba.Hex("#E9DFC7"), Ink = Rgba.Hex("#111A1E");

        // Greyscale tones for tinted parts: white → skin colour, grey → its shade (two flat tones).
        static readonly Rgba Skin = Rgba.White, SkinShade = new Rgba(0.80f, 0.80f, 0.82f), SkinMark = new Rgba(0.70f, 0.70f, 0.73f);
        static readonly Rgba HumanSkin = Rgba.Hex("#E7B596"), HumanSkinShade = Rgba.Hex("#C98E70");
        static readonly Rgba Sclera = Rgba.Hex("#F3EDDC"), Iris = Rgba.Hex("#6B4A2E");

        const int S = 512;     // visitor layer canvas
        const float Line = 10; // ink outline at 512 px (≈4 px at display size)

        public static readonly string[] ClanNames = { "Vessi", "Ammaru", "Plo", "Ruu" };

        /// <summary>Writes every placeholder into <paramref name="root"/> (…/Art/Generated). Returns the files written.</summary>
        public static List<string> GenerateAll(string root, Action<string> log = null)
        {
            var files = new List<string>();
            void Save(string rel, Raster r)
            {
                var path = System.IO.Path.Combine(root, rel);
                Png.Write(path, r);
                files.Add(path);
                log?.Invoke(rel);
            }

            for (int h = 0; h < 4; h++)
            {
                Save($"Visitor/head_{h}.png", Head(h));
                Save($"Visitor/ear_{h}.png", Ear(h));
                for (int p = 0; p < 4; p++) Save($"Visitor/pattern_{p}_{h}.png", Pattern(p, h));
                for (int c = 0; c < 4; c++)
                {
                    Save($"Visitor/crest_{ClanNames[c]}_{h}.png", Crest(c, h));
                    Save($"Visitor/glow_{ClanNames[c]}_{h}.png", GlowSpots(c, h));
                    Save($"Visitor/eyes_{ClanNames[c]}_{h}_slit.png", Eyes(c, h, round: false, closed: false));
                    Save($"Visitor/eyes_{ClanNames[c]}_{h}_round.png", Eyes(c, h, round: true, closed: false));
                    Save($"Visitor/eyelids_{ClanNames[c]}_{h}.png", Eyes(c, h, round: false, closed: true));
                }
            }
            for (int m = 0; m < 4; m++) Save($"Visitor/mouth_{m}.png", Mouth(m));
            for (int o = 0; o < 4; o++) Save($"Visitor/outfit_{o}.png", Outfit(o));
            Save("Visitor/neck.png", Neck());
            Save("Visitor/gills.png", Gills());
            Save("Visitor/seam.png", Seam());
            Save("UI/fog.png", Fog());
            Save("UI/glyph.png", Glyph());
            Save("Visitor/sweat.png", Sweat());
            Save("Visitor/hand_4.png", Hand(4));
            Save("Visitor/hand_5.png", Hand(5));

            Save("UI/panel_9s.png", RoundedSquare(64, 14));
            Save("UI/button_9s.png", RoundedSquare(64, 18));
            Save("UI/circle.png", CircleSprite(128));
            Save("UI/scanline.png", ScanLine());
            Save("UI/moon_kesh.png", Moon(160, false));
            Save("UI/moon_aun.png", Moon(320, true));
            Save("Backgrounds/booth.png", Booth());
            return files;
        }

        // ------------------------------------------------------------------ anchor grid

        static Sdf HeadShape(int h)
        {
            switch (h)
            {
                case 1: return Ellipse(256, 235, 122, 190);
                case 2: return Ellipse(256, 265, 185, 140);
                case 3:
                    return SmoothUnion(Ellipse(256, 270, 150, 140),
                        Union(Circle(172, 160, 62), Circle(256, 128, 68), Circle(340, 160, 62)), 30);
                default: return Ellipse(256, 245, 150, 170);
            }
        }

        /// <summary>Top of the head at the centre line (the crown anchor for crests).</summary>
        static float Crown(int h) => h == 1 ? 45 : h == 2 ? 125 : h == 3 ? 62 : 75;

        /// <summary>Left edge of the head at the ear line.</summary>
        static float SideX(int h) => h == 1 ? 136 : h == 2 ? 74 : 108;

        /// <summary>Eye centres for the clan on this head (GDD kit row "Eyes").</summary>
        static List<(float x, float y, float rx, float ry)> EyeSpots(int clan, int h)
        {
            var e = new List<(float, float, float, float)>();
            switch (clan)
            {
                case 1: // Ammaru: two eyes + a third above
                    e.Add((205, 262, 32, 26)); e.Add((307, 262, 32, 26));
                    e.Add((256, Math.Max(Crown(h) + 70, 190), 22, 18));
                    break;
                case 2: // Plo: one great eye
                    e.Add((256, 252, 62, 44));
                    break;
                case 3: // Ruu: stalk eyes above the crown
                    float y = Math.Max(Crown(h) - 38, 34);
                    e.Add((208, y, 26, 26)); e.Add((304, y, 26, 26));
                    break;
                default: // Vessi
                    e.Add((205, 255, 34, 27)); e.Add((307, 255, 34, 27));
                    break;
            }
            return e;
        }

        // ------------------------------------------------------------------ visitor parts

        static void Part(Raster r, Sdf shape, Rgba fill, Rgba shade, float shadeDx = -26, float shadeDy = -14)
        {
            r.Fill(shape, fill);
            r.Fill(Subtract(shape, Move(shape, shadeDx, shadeDy)), shade);
            r.Stroke(shape, Line, Ink);
        }

        static Raster Head(int h)
        {
            var r = new Raster(S, S);
            Part(r, HeadShape(h), Skin, SkinShade);
            return r;
        }

        /// <summary>A human ear peeking out from behind the suit's head (drawn behind the head layer).</summary>
        static Raster Ear(int h)
        {
            var r = new Raster(S, S);
            float x = SideX(h) - 2, y = 275;
            var ear = Ellipse(x, y, 26, 40);
            r.Fill(ear, HumanSkin);
            r.Fill(Arc(x - 4, y, 16, 110, 250, 7), HumanSkinShade);
            r.Stroke(ear, Line * 0.8f, Ink);
            return r;
        }

        static Raster Pattern(int p, int h)
        {
            var r = new Raster(S, S);
            var head = HeadShape(h);
            switch (p)
            {
                case 0: // mottled spots
                    foreach (var (x, y, rad) in new[] { (180f, 180f, 20f), (340f, 200f, 14f), (330f, 330f, 24f), (175f, 340f, 12f), (300f, 140f, 10f) })
                        r.Fill(Circle(x, y, rad), SkinMark);
                    break;
                case 1: // stripes
                    for (int i = 0; i < 4; i++)
                        r.Fill(Arc(256, 470 - i * 70, 300, 235, 305, 14), SkinMark);
                    break;
                case 2: // freckle dots
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i * 2.39996f, d = 40 + 9 * i;
                        r.Fill(Circle(256 + (float)Math.Cos(a) * d, 270 + (float)Math.Sin(a) * d * 0.8f, 6), SkinMark);
                    }
                    break;
                default: // constellation
                    var pts = new[] { (170f, 190f), (215f, 150f), (300f, 175f), (340f, 250f), (300f, 340f), (190f, 320f) };
                    r.Fill(Path(5, pts), SkinMark);
                    foreach (var pt in pts) r.Fill(Circle(pt.Item1, pt.Item2, 9), SkinMark);
                    break;
            }
            r.Mask(Grow(head, -Line * 0.5f));
            return r;
        }

        static Raster Crest(int clan, int h)
        {
            var r = new Raster(S, S);
            float top = Crown(h);
            switch (clan)
            {
                case 0: // Vessi fins
                    foreach (int side in new[] { -1, 1 })
                    {
                        var fin = Polygon((256 + side * 40, top + 40), (256 + side * 92, top - 62), (256 + side * 104, top + 30));
                        Part(r, fin, Skin, SkinShade, side * 12, 0);
                    }
                    break;
                case 1: // Ammaru antennae
                    foreach (int side in new[] { -1, 1 })
                    {
                        var stalk = Capsule(256 + side * 34, top + 30, 256 + side * 62, top - 48, 16);
                        Part(r, stalk, Skin, SkinShade, 0, 0);
                        Part(r, Circle(256 + side * 62, top - 56, 17), Skin, SkinShade);
                    }
                    break;
                case 2: // Plo frill fan
                    var fan = Intersect(Ellipse(256, top + 70, 190, 120), Rect(0, 0, S, top + 70));
                    r.Fill(fan, SkinShade);
                    for (int i = 0; i <= 6; i++)
                    {
                        double a = Math.PI + Math.PI * i / 6.0;
                        r.Fill(Capsule(256, top + 70, 256 + (float)Math.Cos(a) * 180, top + 70 + (float)Math.Sin(a) * 112, 7), SkinMark);
                    }
                    r.Stroke(fan, Line, Ink);
                    break;
                default: // Ruu eye stalks
                    float y = Math.Max(top - 38, 34);
                    foreach (int side in new[] { -1, 1 })
                        Part(r, Capsule(256 + side * 30, top + 30, 256 + side * 48, y + 10, 18), Skin, SkinShade, 0, 0);
                    break;
            }
            return r;
        }

        /// <summary>Teal bio-glow spots (GDD: "teal glow = alien"). Fake parts in M2 skip these.</summary>
        static Raster GlowSpots(int clan, int h)
        {
            var r = new Raster(S, S);
            float top = Crown(h);
            var spots = new List<(float x, float y, float rad)>();
            switch (clan)
            {
                case 0: spots.Add((226, top + 62, 9)); spots.Add((256, top + 50, 11)); spots.Add((286, top + 62, 9)); break;
                case 1: spots.Add((194, top - 56, 9)); spots.Add((318, top - 56, 9)); break;
                case 2: spots.Add((256, 330, 8)); break;
                default: spots.Add((190, 280, 7)); spots.Add((322, 280, 7)); spots.Add((256, 200, 8)); break;
            }
            foreach (var (x, y, rad) in spots)
            {
                r.Glow(Circle(x, y, rad), BioGlow.WithAlpha(0.45f), 18);
                r.Fill(Circle(x, y, rad), BioGlow);
            }
            return r;
        }

        static Raster Eyes(int clan, int h, bool round, bool closed)
        {
            var r = new Raster(S, S);
            foreach (var (x, y, rx, ry) in EyeSpots(clan, h))
            {
                var eye = Ellipse(x, y, rx, ry);
                if (closed)
                {
                    // Blink: skin-coloured lid (tinted) with a lash line.
                    r.Fill(Grow(eye, 2), Skin);
                    r.Stroke(eye, Line * 0.8f, Ink);
                    r.Fill(Arc(x, y - ry * 0.6f, rx * 1.05f, 20, 160, 6), Ink);
                    continue;
                }
                r.Fill(eye, Sclera);
                if (round)
                {
                    r.Fill(Circle(x, y, ry * 0.62f), Iris);
                    r.Fill(Circle(x, y, ry * 0.32f), Ink);
                    r.Fill(Circle(x - ry * 0.18f, y - ry * 0.2f, ry * 0.12f), Rgba.White);
                }
                else
                {
                    if (clan == 2) r.Fill(Circle(x, y, ry * 0.7f), BioGlow);
                    r.Fill(Capsule(x, y - ry * 0.78f, x, y + ry * 0.78f, Math.Max(7, rx * 0.26f)), Ink);
                }
                r.Stroke(eye, Line * 0.8f, Ink);
            }
            return r;
        }

        static Raster Mouth(int m)
        {
            var r = new Raster(S, S);
            switch (m)
            {
                case 0: r.Fill(Arc(256, 300, 42, 30, 150, 9), Ink); break;
                case 1:
                    var o = Ellipse(256, 345, 15, 20);
                    r.Fill(o, Rgba.Hex("#3A2638"));
                    r.Stroke(o, 7, Ink);
                    break;
                case 2: // mandibles
                    foreach (int side in new[] { -1, 1 })
                    {
                        var fang = Polygon((256 + side * 8, 322), (256 + side * 34, 326), (256 + side * 16, 372));
                        r.Fill(fang, Sclera);
                        r.Stroke(fang, 6, Ink);
                    }
                    break;
                default:
                    r.Fill(Capsule(224, 340, 288, 340, 8), Ink);
                    r.Fill(Capsule(240, 352, 246, 362, 5), Ink);
                    r.Fill(Capsule(272, 352, 266, 362, 5), Ink);
                    break;
            }
            return r;
        }

        static Raster Neck()
        {
            var r = new Raster(S, S);
            var neck = Rect(212, 370, 88, 110);
            Part(r, neck, Skin, SkinShade, -18, 0);
            return r;
        }

        /// <summary>Gill slits, a separate layer so the view can pulse them (citizens) or hold them still (humans).</summary>
        static Raster Gills()
        {
            var r = new Raster(S, S);
            for (int i = 0; i < 3; i++)
            {
                r.Fill(Capsule(224, 404 + i * 15, 240, 409 + i * 15, 6), Ink.WithAlpha(0.55f));
                r.Fill(Capsule(288, 404 + i * 15, 272, 409 + i * 15, 6), Ink.WithAlpha(0.55f));
            }
            return r;
        }

        /// <summary>The bio-suit's seam, shown for a frame when the suit flickers (GDD §5.4).</summary>
        static Raster Seam()
        {
            var r = new Raster(S, S);
            r.Glow(Capsule(256, 90, 256, 470, 8), Coral.WithAlpha(0.5f), 18);
            r.Fill(Capsule(256, 90, 256, 470, 8), Ink);
            for (int y = 100; y < 470; y += 22)
            {
                r.Fill(Capsule(244, y, 268, y + 6, 6), Rgba.Hex("#B9C2CF"));
            }
            r.Fill(Rect(244, 300, 24, 40, 5), Rgba.Hex("#B9C2CF"));
            r.Stroke(Rect(244, 300, 24, 40, 5), 4, Ink);
            return r;
        }

        static Raster Fog()
        {
            var r = new Raster(256, 160);
            r.Fill(Ellipse(128, 80, 100, 56), Rgba.White.WithAlpha(0.55f), 60);
            r.Fill(Ellipse(110, 74, 50, 26), Rgba.White.WithAlpha(0.35f), 30);
            return r;
        }

        /// <summary>The Lattice glyph: a downward chevron over a bar. Forgeries print it upside down (GDD §5.1).</summary>
        static Raster Glyph()
        {
            var r = new Raster(64, 64);
            r.Fill(Path(7, (12, 16), (32, 44), (52, 16)), Rgba.White);
            r.Fill(Capsule(14, 54, 50, 54, 6), Rgba.White);
            return r;
        }

        static Raster Outfit(int o)
        {
            var r = new Raster(S, S);
            var body = Ellipse(256, 612, 250, 172);
            Rgba main, accent;
            switch (o)
            {
                case 1: main = Rgba.Hex("#E8833A"); accent = PaperSand; break;
                case 2: main = PaperSand; accent = Coral; break;
                case 3: main = Rgba.Hex("#8C7B5E"); accent = Rgba.Hex("#3E7A66"); break;
                default: main = Rgba.Hex("#2F3A6B"); accent = PaperSand; break;
            }
            Part(r, body, main, main.Shade(-0.18f), -40, -10);
            switch (o)
            {
                case 0: // V-neck collar
                    var collar = Polygon((196, 452), (256, 520), (316, 452), (290, 452), (256, 492), (222, 452));
                    r.Fill(collar, accent);
                    r.Stroke(collar, 6, Ink);
                    break;
                case 1: // vest stripe and zip
                    r.Fill(Intersect(body, Rect(0, 500, S, 16)), accent);
                    r.Fill(Capsule(256, 455, 256, 520, 6), Ink);
                    break;
                case 2: // badge
                    r.Stroke(Circle(320, 492, 14), 6, accent);
                    r.Fill(Circle(320, 492, 5), accent);
                    break;
                default: // apron bib with straps
                    var bib = Rect(196, 470, 120, 60, 6);
                    r.Fill(bib, accent);
                    r.Stroke(bib, 6, Ink);
                    r.Fill(Capsule(206, 470, 190, 446, 8), accent);
                    r.Fill(Capsule(306, 470, 322, 446, 8), accent);
                    break;
            }
            return r;
        }

        static Raster Sweat()
        {
            var r = new Raster(64, 96);
            var drop = SmoothUnion(Circle(32, 62, 22), Polygon((32, 6), (16, 52), (48, 52)), 6);
            r.Fill(drop, Rgba.Hex("#8FD3FF"));
            r.Fill(Circle(25, 58, 6), Rgba.Hex("#E8F7FF"));
            r.Stroke(drop, 5, Ink);
            return r;
        }

        /// <summary>A raised palm (Action question). Four fingers is a citizen; five is a human in a suit glove.</summary>
        static Raster Hand(int fingers)
        {
            var r = new Raster(256, 256);
            var palm = Box(128, 168, 58, 56, 26);
            var parts = new List<Sdf> { palm };
            float span = fingers == 5 ? 96 : 84;
            int upright = fingers == 5 ? 4 : 3;
            for (int i = 0; i < upright; i++)
            {
                float x = 128 - span / 2 + span * i / (upright - 1);
                parts.Add(Capsule(x, 150, x, i == 0 || i == upright - 1 ? 62 : 44, 24));
            }
            parts.Add(Capsule(80, 182, 36, 128, 24)); // thumb
            var hand = Union(parts.ToArray());
            Part(r, hand, Skin, SkinShade, -14, 0);
            return r;
        }

        // ------------------------------------------------------------------ UI and backgrounds

        static Raster RoundedSquare(int size, float radius)
        {
            var r = new Raster(size, size);
            r.Fill(Box(size / 2f, size / 2f, size / 2f - 1, size / 2f - 1, radius), Rgba.White);
            return r;
        }

        static Raster CircleSprite(int size)
        {
            var r = new Raster(size, size);
            r.Fill(Circle(size / 2f, size / 2f, size / 2f - 1), Rgba.White);
            return r;
        }

        static Raster ScanLine()
        {
            var r = new Raster(256, 48);
            r.Fill(Rect(0, 0, 256, 48), Raster.VerticalGradient(0, BioGlow.WithAlpha(0f), 40, BioGlow.WithAlpha(0.35f)));
            r.Fill(Rect(0, 40, 256, 8), BioGlow);
            return r;
        }

        static Raster Moon(int size, bool craters)
        {
            var r = new Raster(size, size);
            float c = size / 2f, rad = size * 0.36f;
            r.Glow(Circle(c, c, rad), Rgba.White.WithAlpha(0.25f), size * 0.12f);
            r.Fill(Circle(c, c, rad), craters ? Rgba.Hex("#E9DFC7") : Rgba.White);
            if (craters)
            {
                r.Fill(Circle(c - rad * 0.35f, c - rad * 0.2f, rad * 0.2f), Rgba.Hex("#CFC2A2"));
                r.Fill(Circle(c + rad * 0.4f, c + rad * 0.35f, rad * 0.12f), Rgba.Hex("#CFC2A2"));
            }
            return r;
        }

        /// <summary>The view through the booth glass: Vel'Tara's bioluminescent city at night (moons are separate sprites).</summary>
        static Raster Booth()
        {
            const int W = 1280, H = 720;
            var r = new Raster(W, H);
            r.Fill(Rect(0, 0, W, H), Raster.VerticalGradient(0, Rgba.Hex("#1D4A54"), H, BoothGlass));
            var rng = new Random(7);
            var city = Rgba.Hex("#10292F");
            float x = -20;
            while (x < W)
            {
                float w = 70 + rng.Next(90), top = 300 + rng.Next(200);
                r.Fill(Rect(x, top, w, H - top), city);
                if (rng.Next(3) == 0) r.Fill(Polygon((x + w * 0.3f, top), (x + w * 0.5f, top - 120 - rng.Next(80)), (x + w * 0.7f, top)), city);
                for (int i = 0; i < 4; i++)
                {
                    if (rng.Next(2) == 0) continue;
                    var col = rng.Next(5) == 0 ? Coral : BioGlow;
                    r.Fill(Rect(x + 12 + rng.Next((int)Math.Max(1, w - 30)), top + 30 + rng.Next(160), 12, 14), col.WithAlpha(0.85f));
                }
                x += w + 10 + rng.Next(30);
            }
            // Ground haze and the teal guide line across the glass (GDD cover art).
            r.Fill(Rect(0, 470, W, 250), BoothGlass.WithAlpha(0.55f));
            r.Fill(Rect(0, 478, W, 6), BioGlow.WithAlpha(0.8f));
            // Glass streaks.
            r.Fill(Capsule(90, 120, 380, 20, 10), Rgba.White.WithAlpha(0.06f));
            r.Fill(Capsule(160, 190, 560, 40, 8), Rgba.White.WithAlpha(0.05f));
            return r;
        }
    }
}
