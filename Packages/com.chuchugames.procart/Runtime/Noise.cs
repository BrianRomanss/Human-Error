using System;

namespace ChuchuGames.ProcArt
{
    /// <summary>
    /// Seeded value noise and fractal (fBm) noise in 0–1, for grime, stains, fog and wobbly edges.
    /// Deterministic: the same seed always gives the same image.
    /// </summary>
    public static class Noise
    {
        /// <summary>Smooth value noise at (x, y) / scale.</summary>
        public static float Value(float x, float y, float scale, int seed = 0)
        {
            x /= scale;
            y /= scale;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sy);
        }

        /// <summary>Fractal noise: several octaves of <see cref="Value"/>, each half the size and strength.</summary>
        public static float Fbm(float x, float y, float scale, int octaves = 4, int seed = 0)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, scale, seed + i * 101) * amp;
                norm += amp;
                amp *= 0.5f;
                scale *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Paint whose alpha follows noise: only the parts above <paramref name="threshold"/> show (stains, grime, mist).</summary>
        public static Paint Mottled(Rgba color, float scale, float threshold = 0.5f, float softness = 0.15f, int seed = 0, int octaves = 4) =>
            (x, y) =>
            {
                float n = Fbm(x, y, scale, octaves, seed);
                float t = (n - threshold) / Math.Max(0.0001f, softness);
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                return color.WithAlpha(color.A * t);
            };

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
