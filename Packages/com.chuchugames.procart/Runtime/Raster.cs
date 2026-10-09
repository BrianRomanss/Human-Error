using System;

namespace ChuchuGames.ProcArt
{
    /// <summary>Straight (non-premultiplied) RGBA colour, 0–1 floats.</summary>
    public readonly struct Rgba
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }

        public static Rgba Hex(string hex, float alpha = 1f)
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6) throw new ArgumentException($"Expected #RRGGBB, got {hex}");
            int v = Convert.ToInt32(hex, 16);
            return new Rgba(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, alpha);
        }

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);

        /// <summary>Mix towards white (t &gt; 0) or black (t &lt; 0).</summary>
        public Rgba Shade(float t) =>
            t >= 0 ? Lerp(this, new Rgba(1, 1, 1, A), t) : Lerp(this, new Rgba(0, 0, 0, A), -t);

        public static Rgba Lerp(Rgba a, Rgba b, float t) =>
            new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

        public static readonly Rgba Transparent = new Rgba(0, 0, 0, 0);
        public static readonly Rgba White = new Rgba(1, 1, 1);
    }

    /// <summary>Colour as a function of position, e.g. a gradient.</summary>
    public delegate Rgba Paint(float x, float y);

    /// <summary>
    /// An RGBA image you draw on with signed-distance shapes. Coordinates are pixels, origin
    /// top-left, y down (like most image editors). Every shape is anti-aliased.
    /// </summary>
    public sealed class Raster
    {
        readonly float[] _px; // straight RGBA

        public int Width { get; }
        public int Height { get; }

        public Raster(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            Width = width;
            Height = height;
            _px = new float[width * height * 4];
        }

        public Rgba Get(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return new Rgba(_px[i], _px[i + 1], _px[i + 2], _px[i + 3]);
        }

        public void Set(int x, int y, Rgba c)
        {
            int i = (y * Width + x) * 4;
            _px[i] = c.R; _px[i + 1] = c.G; _px[i + 2] = c.B; _px[i + 3] = c.A;
        }

        /// <summary>Box-filtered copy at a smaller size (for thumbnails and contact sheets).</summary>
        public Raster Downscale(int width, int height)
        {
            var dst = new Raster(width, height);
            float kx = (float)Width / width, ky = (float)Height / height;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                int n = 0;
                for (int sy = (int)(y * ky); sy < Math.Max((int)((y + 1) * ky), (int)(y * ky) + 1); sy++)
                for (int sx = (int)(x * kx); sx < Math.Max((int)((x + 1) * kx), (int)(x * kx) + 1); sx++)
                {
                    var c = Get(sx, sy);
                    r += c.R * c.A; g += c.G * c.A; b += c.B * c.A; a += c.A; n++;
                }
                if (a > 0) dst.Set(x, y, new Rgba(r / a, g / a, b / a, a / n));
            }
            return dst;
        }

        public void Clear(Rgba c)
        {
            for (int i = 0; i < _px.Length; i += 4)
            {
                _px[i] = c.R; _px[i + 1] = c.G; _px[i + 2] = c.B; _px[i + 3] = c.A;
            }
        }

        /// <summary>Fills the inside (distance &lt; 0) of a shape with a solid colour.</summary>
        public void Fill(Sdf shape, Rgba color, float feather = 1f) => Fill(shape, (x, y) => color, feather);

        /// <summary>
        /// Fills a shape with a paint. <paramref name="feather"/> is the edge softness in pixels:
        /// 1 gives a crisp anti-aliased edge, large values give glows and soft shadows.
        /// </summary>
        public void Fill(Sdf shape, Paint paint, float feather = 1f)
        {
            float half = Math.Max(feather, 0.0001f) * 0.5f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = shape(px, py);
                if (d > half) continue;
                float cover = d < -half ? 1f : 0.5f - d / (2f * half);
                Blend(x, y, paint(px, py), cover);
            }
        }

        /// <summary>Draws only the outline of a shape, <paramref name="width"/> pixels thick, centred on its edge.</summary>
        public void Stroke(Sdf shape, float width, Rgba color, float feather = 1f) =>
            Fill(Shapes.Ring(shape, width * 0.5f), color, feather);

        /// <summary>Soft glow around a shape (GDD ghosts: "soft outer glow").</summary>
        public void Glow(Sdf shape, Rgba color, float radius) =>
            Fill(Shapes.Grow(shape, radius * 0.5f), color, radius);

        void Blend(int x, int y, Rgba src, float cover)
        {
            float sa = src.A * cover;
            if (sa <= 0f) return;
            int i = (y * Width + x) * 4;
            float da = _px[i + 3];
            float oa = sa + da * (1f - sa);
            if (oa <= 0f) return;
            _px[i] = (src.R * sa + _px[i] * da * (1f - sa)) / oa;
            _px[i + 1] = (src.G * sa + _px[i + 1] * da * (1f - sa)) / oa;
            _px[i + 2] = (src.B * sa + _px[i + 2] * da * (1f - sa)) / oa;
            _px[i + 3] = oa;
        }

        /// <summary>Draws another raster on top at an offset (straight alpha "over").</summary>
        public void Draw(Raster src, int offsetX, int offsetY, float opacity = 1f)
        {
            for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
            {
                int tx = x + offsetX, ty = y + offsetY;
                if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) continue;
                var c = src.Get(x, y);
                Blend(tx, ty, new Rgba(c.R, c.G, c.B, 1f), c.A * opacity);
            }
        }

        /// <summary>Keeps only what lies inside the shape (anti-aliased), e.g. to clip a portrait to a circle.</summary>
        public void Mask(Sdf shape, float feather = 1f)
        {
            float half = Math.Max(feather, 0.0001f) * 0.5f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float d = shape(x + 0.5f, y + 0.5f);
                float cover = d < -half ? 1f : d > half ? 0f : 0.5f - d / (2f * half);
                _px[(y * Width + x) * 4 + 3] *= cover;
            }
        }

        /// <summary>8-bit RGBA bytes, top row first.</summary>
        public byte[] ToBytes()
        {
            var bytes = new byte[_px.Length];
            for (int i = 0; i < _px.Length; i++)
            {
                float v = _px[i] < 0 ? 0 : _px[i] > 1 ? 1 : _px[i];
                bytes[i] = (byte)(v * 255f + 0.5f);
            }
            return bytes;
        }

        public static Paint VerticalGradient(float y0, Rgba top, float y1, Rgba bottom) => (x, y) =>
        {
            float t = (y - y0) / (y1 - y0);
            return Rgba.Lerp(top, bottom, t < 0 ? 0 : t > 1 ? 1 : t);
        };

        public static Paint RadialGradient(float cx, float cy, float r, Rgba inner, Rgba outer) => (x, y) =>
        {
            float t = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
            return Rgba.Lerp(inner, outer, t < 0 ? 0 : t > 1 ? 1 : t);
        };
    }
}
