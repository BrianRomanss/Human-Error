using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ChuchuGames.ProcArt;
using HumanError.EditorTools.ArtGen;

// Usage: dotnet run --project Tools/ArtGen [-- --sheet <contact-sheet.png>] [--no-audio]
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var art = Path.Combine(root, "Assets", "_Game", "Art", "Generated");
var audio = Path.Combine(root, "Assets", "_Game", "Audio", "Generated");
var sw = Stopwatch.StartNew();
var files = HumanErrorArt.GenerateAll(art);
Console.WriteLine($"{files.Count} images → {art} ({sw.ElapsedMilliseconds} ms)");
if (Array.IndexOf(args, "--no-audio") < 0)
    Console.WriteLine($"{HumanErrorAudio.GenerateAll(audio).Count} sounds → {audio}");

int sheetArg = Array.IndexOf(args, "--sheet");
if (sheetArg >= 0)
{
    // Contact sheet: a composited visitor per clan × head (tinted like the game does), plus tells and parts.
    var tints = new[] { "#C7B8F5", "#9EE6B8", "#F6B48F", "#8FC1EE" };
    var sheet = new Raster(1700, 1100);
    sheet.Clear(Rgba.Hex("#0E1A1F"));
    Raster Load(string rel) => PngRead.Read(Path.Combine(art, rel));
    Raster Tint(Raster src, Rgba t)
    {
        var o = new Raster(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
        for (int x = 0; x < src.Width; x++)
        {
            var c = src.Get(x, y);
            o.Set(x, y, new Rgba(c.R * t.R, c.G * t.G, c.B * t.B, c.A));
        }
        return o;
    }
    for (int c = 0; c < 4; c++)
    for (int h = 0; h < 4; h++)
    {
        string clan = HumanErrorArt.ClanNames[c];
        var t = Rgba.Hex(tints[c]);
        bool human = (c + h) % 3 == 0;
        var v = new Raster(512, 512);
        v.Draw(Load($"Visitor/outfit_{(c + h) % 4}.png"), 0, 0);
        v.Draw(Tint(Load("Visitor/neck.png"), t), 0, 0);
        if (human) v.Draw(Load($"Visitor/ear_{h}.png"), 0, 0);
        v.Draw(Tint(Load($"Visitor/crest_{clan}_{h}.png"), t), 0, 0);
        v.Draw(Tint(Load($"Visitor/head_{h}.png"), t), 0, 0);
        v.Draw(Tint(Load($"Visitor/pattern_{(c * 3 + h) % 4}_{h}.png"), t), 0, 0);
        v.Draw(Load($"Visitor/glow_{clan}_{h}.png"), 0, 0);
        v.Draw(Load($"Visitor/eyes_{clan}_{h}_{(human ? "round" : "slit")}.png"), 0, 0);
        v.Draw(Load($"Visitor/mouth_{(c + 2 * h) % 4}.png"), 0, 0);
        if (human) v.Draw(Load("Visitor/sweat.png"), 330, 160);
        sheet.Draw(v.Downscale(256, 256), 10 + h * 266, 10 + c * 266);
    }
    int px = 1080, py = 10;
    foreach (var rel in new[] { "Visitor/hand_4.png", "Visitor/hand_5.png", "UI/moon_aun.png", "UI/moon_kesh.png" })
    {
        var img = Load(rel);
        sheet.Draw(img.Width > 256 ? img.Downscale(256, 256) : img, px, py);
        px += 270; if (px > 1500) { px = 1080; py += 270; }
    }
    sheet.Draw(Load("Visitor/eyelids_Ammaru_0.png").Downscale(256, 256), 1080, 560);
    sheet.Draw(Load("Backgrounds/booth.png").Downscale(600, 338), 1080, 760);
    Png.Write(args[sheetArg + 1], sheet);
    Console.WriteLine($"sheet → {args[sheetArg + 1]}");
}

/// <summary>Minimal PNG reader for the contact sheet (8-bit RGBA, non-interlaced: what Png.Write produces).</summary>
static class PngRead
{
    public static Raster Read(string path)
    {
        var data = File.ReadAllBytes(path);
        int pos = 8, w = 0, h = 0;
        using var idat = new MemoryStream();
        while (pos < data.Length)
        {
            int len = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            if (type == "IHDR")
            {
                w = (data[pos + 8] << 24) | (data[pos + 9] << 16) | (data[pos + 10] << 8) | data[pos + 11];
                h = (data[pos + 12] << 24) | (data[pos + 13] << 16) | (data[pos + 14] << 8) | data[pos + 15];
            }
            else if (type == "IDAT") idat.Write(data, pos + 8, len);
            pos += 12 + len;
        }
        idat.Position = 0;
        using var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        using var raw = new MemoryStream();
        z.CopyTo(raw);
        var bytes = raw.ToArray();
        var r = new Raster(w, h);
        int stride = w * 4;
        var prev = new byte[stride];
        var line = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int f = bytes[y * (stride + 1)];
            Array.Copy(bytes, y * (stride + 1) + 1, line, 0, stride);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= 4 ? line[i - 4] : 0, b = prev[i], c = i >= 4 ? prev[i - 4] : 0;
                int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                int pred = f == 1 ? a : f == 2 ? b : f == 3 ? (a + b) / 2 : f == 4 ? (pa <= pb && pa <= pc ? a : pb <= pc ? b : c) : 0;
                line[i] = (byte)(line[i] + pred);
            }
            for (int x = 0; x < w; x++)
                r.Set(x, y, new Rgba(line[x * 4] / 255f, line[x * 4 + 1] / 255f, line[x * 4 + 2] / 255f, line[x * 4 + 3] / 255f));
            (prev, line) = (line, prev);
        }
        return r;
    }
}
