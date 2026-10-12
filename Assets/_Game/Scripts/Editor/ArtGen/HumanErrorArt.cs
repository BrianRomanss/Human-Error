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

        /// <summary>Options per free kit slot (head, mouth, pattern, outfit); matches Look.Options.</summary>
        public const int Options = 8;

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

            for (int h = 0; h < Options; h++)
            {
                Save($"Visitor/head_{h}.png", Head(h));
                Save($"Visitor/ear_{h}.png", Ear(h));
                for (int p = 0; p < Options; p++) Save($"Visitor/pattern_{p}_{h}.png", Pattern(p, h));
                for (int c = 0; c < 4; c++)
                {
                    Save($"Visitor/crest_{ClanNames[c]}_{h}.png", Crest(c, h));
                    Save($"Visitor/glow_{ClanNames[c]}_{h}.png", GlowSpots(c, h));
                    Save($"Visitor/eyes_{ClanNames[c]}_{h}_slit.png", Eyes(c, h, round: false, closed: false));
                    Save($"Visitor/eyes_{ClanNames[c]}_{h}_round.png", Eyes(c, h, round: true, closed: false));
                    Save($"Visitor/eyelids_{ClanNames[c]}_{h}.png", Eyes(c, h, round: false, closed: true));
                }
            }
            for (int m = 0; m < Options; m++) Save($"Visitor/mouth_{m}.png", Mouth(m));
            for (int o = 0; o < Options; o++) Save($"Visitor/outfit_{o}.png", Outfit(o));
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
            Save("Backgrounds/title.png", TitleTower());
            Save("Backgrounds/home.png", HomeRoom());
            Save("UI/shade_left.png", ShadeLeft());
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
                case 4: // pear: narrow brow, wide jowls
                    return SmoothUnion(Ellipse(256, 205, 118, 112), Ellipse(256, 300, 172, 118), 40);
                case 5: // rounded block
                    return Box(256, 250, 152, 165, 72);
                case 6: // onion: a pointed crown
                    return SmoothUnion(Ellipse(256, 285, 150, 135), Polygon((170, 200), (256, 62), (342, 200)), 40);
                case 7: // round with cheek lobes
                    return SmoothUnion(Ellipse(256, 255, 160, 160), Union(Circle(118, 270, 52), Circle(394, 270, 52)), 30);
                default: return Ellipse(256, 245, 150, 170);
            }
        }

        /// <summary>Top of the head at the centre line (the crown anchor for crests).</summary>
        static float Crown(int h)
        {
            switch (h)
            {
                case 1: return 45;
                case 2: return 125;
                case 3: return 62;
                case 4: return 93;
                case 5: return 85;
                case 6: return 62;
                case 7: return 95;
                default: return 75;
            }
        }

        /// <summary>Left edge of the head at the ear line.</summary>
        static float SideX(int h)
        {
            switch (h)
            {
                case 1: return 136;
                case 2: return 74;
                case 4: return 88;
                case 5: return 104;
                case 6: return 106;
                case 7: return 68;
                default: return 108;
            }
        }

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
                case 3: // constellation
                    var pts = new[] { (170f, 190f), (215f, 150f), (300f, 175f), (340f, 250f), (300f, 340f), (190f, 320f) };
                    r.Fill(Path(5, pts), SkinMark);
                    foreach (var pt in pts) r.Fill(Circle(pt.Item1, pt.Item2, 9), SkinMark);
                    break;
                case 4: // brow chevrons
                    float top = Math.Max(Crown(h) + 46, 150);
                    for (int i = 0; i < 3; i++)
                        r.Fill(Path(9, (206, top + i * 22), (256, top + 24 + i * 22), (306, top + i * 22)), SkinMark);
                    break;
                case 5: // cheek patches
                    r.Fill(Ellipse(170, 312, 36, 24), SkinMark);
                    r.Fill(Ellipse(342, 312, 36, 24), SkinMark);
                    break;
                case 6: // centre stripe
                    r.Fill(Capsule(256, Crown(h) + 8, 256, 236, 15), SkinMark);
                    r.Fill(Capsule(256, 284, 256, 420, 15), SkinMark);
                    break;
                default: // rings
                    foreach (var (x, y, rad) in new[] { (176f, 210f, 18f), (336f, 196f, 14f), (346f, 318f, 20f), (180f, 336f, 13f) })
                        r.Fill(Ring(Circle(x, y, rad), 4), SkinMark);
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
                case 3:
                    r.Fill(Capsule(224, 340, 288, 340, 8), Ink);
                    r.Fill(Capsule(240, 352, 246, 362, 5), Ink);
                    r.Fill(Capsule(272, 352, 266, 362, 5), Ink);
                    break;
                case 4: // flat grin with two small tusks
                    foreach (int side in new[] { -1, 1 })
                    {
                        var tusk = Polygon((256 + side * 22, 334), (256 + side * 40, 334), (256 + side * 30, 308));
                        r.Fill(tusk, Sclera);
                        r.Stroke(tusk, 5, Ink);
                    }
                    r.Fill(Capsule(210, 336, 302, 336, 8), Ink);
                    break;
                case 5: // "w" mouth
                    r.Fill(Path(8, (224, 324), (240, 340), (256, 328), (272, 340), (288, 324)), Ink);
                    break;
                case 6: // small beak
                    var beak = Polygon((232, 318), (280, 318), (256, 356));
                    r.Fill(beak, Rgba.Hex("#E8B64A"));
                    r.Fill(Polygon((232, 318), (280, 318), (256, 330)), Rgba.Hex("#C9902F"));
                    r.Stroke(beak, 6, Ink);
                    break;
                default: // feeding tendrils
                    foreach (int i in new[] { -1, 0, 1 })
                    {
                        float x = 256 + i * 24;
                        var t = Path(11, (x, 326), (x + 8, 346), (x - 6, 366), (x + 4, 388));
                        r.Fill(Grow(t, 3), Ink);
                        r.Fill(t, Rgba.Hex("#7A5E96"));
                    }
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
                case 4: main = Rgba.Hex("#2E6B6A"); accent = PaperSand; break;
                case 5: main = Rgba.Hex("#3B4048"); accent = BioGlow; break;
                case 6: main = Rgba.Hex("#A64B5A"); accent = PaperSand; break;
                case 7: main = Rgba.Hex("#5B4A8A"); accent = Rgba.Hex("#E8B64A"); break;
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
                case 3: // apron bib with straps
                    var bib = Rect(196, 470, 120, 60, 6);
                    r.Fill(bib, accent);
                    r.Stroke(bib, 6, Ink);
                    r.Fill(Capsule(206, 470, 190, 446, 8), accent);
                    r.Fill(Capsule(306, 470, 322, 446, 8), accent);
                    break;
                case 4: // high-collar coat with buttons
                    foreach (int side in new[] { -1, 1 })
                    {
                        var flap = Polygon((256 + side * 8, 512), (256 + side * 58, 446), (256 + side * 70, 470));
                        r.Fill(flap, main.Shade(0.12f));
                        r.Stroke(flap, 6, Ink);
                    }
                    for (int i = 0; i < 2; i++) r.Fill(Circle(256, 482 + i * 22, 6), accent);
                    break;
                case 5: // Lattice uniform: glowing shoulder bars and a chevron badge
                    r.Fill(Intersect(body, Rect(90, 452, 70, 12, 4)), accent);
                    r.Fill(Intersect(body, Rect(352, 452, 70, 12, 4)), accent);
                    r.Fill(Path(5, (306, 480), (320, 496), (334, 480)), accent);
                    break;
                case 6: // striped sweater
                    for (int i = 0; i < 3; i++) r.Fill(Intersect(body, Rect(0, 470 + i * 22, S, 9)), accent);
                    break;
                default: // scarf
                    var scarf = Union(Capsule(196, 458, 316, 458, 20), Capsule(290, 462, 302, 512, 14));
                    r.Fill(scarf, accent);
                    r.Stroke(scarf, 5, Ink);
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

        /// <summary>Alpha-blends an axis-aligned block without anti-aliasing: cheap for hundreds of lit windows.</summary>
        static void Block(Raster r, float x, float y, float w, float h, Rgba c)
        {
            int x0 = Math.Max(0, (int)x), y0 = Math.Max(0, (int)y);
            int x1 = Math.Min(r.Width, (int)(x + w)), y1 = Math.Min(r.Height, (int)(y + h));
            for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
            {
                var d = r.Get(xx, yy);
                float a = c.A;
                r.Set(xx, yy, new Rgba(d.R + (c.R - d.R) * a, d.G + (c.G - d.G) * a, d.B + (c.B - d.B) * a, Math.Max(d.A, a)));
            }
        }

        /// <summary>A row of city blocks with a few lit windows, between <paramref name="minTop"/> and <paramref name="maxTop"/>.</summary>
        static void Skyline(Raster r, Random rng, Rgba colour, int minTop, int maxTop, float lit, Sdf clip = null)
        {
            float x = -30;
            while (x < r.Width)
            {
                float w = 60 + rng.Next(110), top = minTop + rng.Next(Math.Max(1, maxTop - minTop));
                Sdf block = Rect(x, top, w, r.Height - top);
                if (rng.Next(3) == 0) block = Union(block, Polygon((x + w * 0.3f, top), (x + w * 0.5f, top - 60 - rng.Next(70)), (x + w * 0.7f, top)));
                r.Fill(clip == null ? block : Intersect(block, clip), colour);
                for (float wy = top + 18; wy < r.Height - 20; wy += 26)
                for (float wx = x + 10; wx < x + w - 14; wx += 22)
                {
                    if (rng.NextDouble() > lit) continue;
                    if (clip != null && clip(wx, wy) > 0) continue;
                    var c = rng.Next(6) == 0 ? Coral : BioGlow;
                    Block(r, wx, wy, 9, 12, c.WithAlpha(0.35f + (float)rng.NextDouble() * 0.4f));
                }
                x += w + 6 + rng.Next(24);
            }
        }

        static void MoonAt(Raster r, float x, float y, float rad, bool aun)
        {
            r.Glow(Circle(x, y, rad), (aun ? PaperSand : Coral).WithAlpha(0.3f), rad * 0.6f);
            r.Fill(Circle(x, y, rad), aun ? PaperSand : Coral);
            if (!aun) return;
            r.Fill(Circle(x - rad * 0.35f, y - rad * 0.2f, rad * 0.2f), Rgba.Hex("#CFC2A2"));
            r.Fill(Circle(x + rad * 0.4f, y + rad * 0.35f, rad * 0.12f), Rgba.Hex("#CFC2A2"));
        }

        /// <summary>Title (GDD §7.2): the Lattice Tower at night with Night Window 3 lit at its foot.</summary>
        static Raster TitleTower()
        {
            const int W = 1600, H = 900;
            var r = new Raster(W, H);
            r.Fill(Rect(0, 0, W, H), Raster.VerticalGradient(0, Rgba.Hex("#071014"), H, Rgba.Hex("#1A4650")));
            var rng = new Random(11);
            for (int i = 0; i < 160; i++)
                Block(r, rng.Next(W), rng.Next(H * 6 / 10), 2, 2, Rgba.White.WithAlpha(0.25f + (float)rng.NextDouble() * 0.5f));
            MoonAt(r, 1390, 170, 92, true);
            MoonAt(r, 1215, 112, 24, false);

            Skyline(r, rng, Rgba.Hex("#12323B"), 520, 640, 0.10f);
            Skyline(r, rng, Rgba.Hex("#0C232A"), 640, 760, 0.07f);

            // The tower: a tapering block with a spire, glow-edged, rows of dim windows.
            const float cx = 1150;
            var tower = Union(Polygon((cx - 120, H), (cx - 48, 210), (cx + 48, 210), (cx + 120, H)), Polygon((cx - 30, 214), (cx, 50), (cx + 30, 214)));
            r.Glow(tower, BioGlow.WithAlpha(0.18f), 30);
            r.Fill(tower, Rgba.Hex("#0D252C"));
            r.Stroke(tower, 3, BioGlow.WithAlpha(0.55f));
            for (float y = 270; y < 740; y += 30)
            {
                float half = 48 + (120 - 48) * (y - 210) / (H - 210) - 22;
                for (float x = cx - half; x < cx + half - 8; x += 20)
                    Block(r, x, y, 9, 14, BioGlow.WithAlpha(rng.Next(9) == 0 ? 0.6f : 0.14f));
            }
            r.Fill(Path(6, (cx - 22, 236), (cx, 258), (cx + 22, 236)), BioGlow);

            // Night Window 3: the one lit window, with you in it.
            var window = Rect(cx - 52, 764, 104, 70, 6);
            r.Glow(window, BioGlow.WithAlpha(0.5f), 40);
            r.Fill(window, Rgba.Hex("#7FF2DE"));
            r.Fill(Ellipse(cx, 796, 16, 19), Rgba.Hex("#0D252C"));
            r.Fill(Intersect(Ellipse(cx, 846, 40, 30), window), Rgba.Hex("#0D252C"));

            r.Fill(Rect(0, 836, W, H - 836), BoothGlass.WithAlpha(0.75f));
            r.Fill(Rect(0, 836, W, 4), BioGlow.WithAlpha(0.8f));
            return r;
        }

        /// <summary>Home (GDD §7.2): a small room in Block 22. The window sits where the Home screen leaves a gap for it.</summary>
        static Raster HomeRoom()
        {
            const int W = 1600, H = 900;
            var r = new Raster(W, H);
            r.Fill(Rect(0, 0, W, H), Raster.VerticalGradient(0, Rgba.Hex("#14232A"), H, Rgba.Hex("#1E3238")));
            for (int x = 120; x < W; x += 170) Block(r, x, 0, 3, 760, Rgba.Hex("#101D22").WithAlpha(0.8f));
            r.Fill(Rect(0, 760, W, H - 760), Rgba.Hex("#2A211C"));
            for (int y = 790; y < H; y += 34) Block(r, 0, y, W, 2, Rgba.Hex("#1F1814"));
            Block(r, 0, 756, W, 6, Rgba.Hex("#3A3029"));

            // The window (screen 660–1220 × 150–530 at 1920×1080 = art 550–1017 × 125–442).
            var frame = Rect(548, 116, 470, 332, 12);
            r.Fill(frame, Rgba.Hex("#2B3F44"));
            r.Stroke(frame, 6, Ink);
            var glass = Rect(566, 134, 434, 296, 4);
            r.Fill(glass, Raster.VerticalGradient(134, Rgba.Hex("#0A171C"), 430, Rgba.Hex("#1D4A54")));
            var rng = new Random(23);
            for (int i = 0; i < 40; i++)
                Block(r, 570 + rng.Next(426), 138 + rng.Next(120), 2, 2, Rgba.White.WithAlpha(0.4f));
            MoonAt(r, 905, 205, 46, true);
            MoonAt(r, 805, 176, 13, false);
            Skyline(r, rng, Rgba.Hex("#0E262D"), 300, 360, 0.18f, glass);
            for (int i = 0; i < 80; i++)
            {
                // Rain on the glass.
                float x = 570 + rng.Next(430), y = 136 + rng.Next(290), len = 14 + rng.Next(22);
                r.Fill(Intersect(Capsule(x, y, x - 4, y + len, 1.2f), glass), Rgba.White.WithAlpha(0.16f));
            }
            r.Fill(Rect(779, 134, 8, 296), Rgba.Hex("#2B3F44"));
            r.Fill(Rect(566, 278, 434, 8), Rgba.Hex("#2B3F44"));
            var sill = Rect(532, 440, 502, 20, 4);
            r.Fill(sill, Rgba.Hex("#3A3029"));
            r.Stroke(sill, 4, Ink);
            // Sel's plant on the sill.
            var pot = Polygon((600, 440), (640, 440), (634, 404), (606, 404));
            foreach (var (lx, ly) in new[] { (604f, 360f), (622f, 350f), (640f, 366f) })
            {
                var leaf = Capsule(620, 404, lx, ly, 9);
                r.Fill(leaf, Rgba.Hex("#9EE6B8"));
                r.Stroke(leaf, 3, Ink);
            }
            r.Fill(pot, Coral);
            r.Stroke(pot, 4, Ink);

            // Sel's drawing pinned to the wall: you at your window, Graav as a grumpy cloud.
            var paper = Rect(1160, 22, 120, 86, 3);
            r.Fill(paper, PaperSand);
            r.Stroke(paper, 3, Ink);
            r.Stroke(Rect(1176, 40, 44, 50, 4), 3, BioGlow);
            r.Fill(Circle(1198, 60, 10), Rgba.Hex("#9EE6B8"));
            r.Fill(Union(Circle(1244, 50, 11), Circle(1258, 46, 13), Circle(1270, 54, 9)), Rgba.Hex("#8FA7A8"));
            r.Fill(Circle(1220, 26, 4), Coral);
            return r;
        }

        /// <summary>Left-to-right fade from the night colour, laid over art behind menus.</summary>
        static Raster ShadeLeft()
        {
            var r = new Raster(256, 16);
            r.Fill(Rect(0, 0, 256, 16), (x, y) => MossNight.WithAlpha(0.94f * (float)Math.Pow(1 - x / 256f, 1.3)));
            return r;
        }
    }
}
