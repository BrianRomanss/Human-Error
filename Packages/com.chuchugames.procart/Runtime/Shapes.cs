using System;

namespace ChuchuGames.ProcArt
{
    /// <summary>Signed distance in pixels: negative inside, positive outside.</summary>
    public delegate float Sdf(float x, float y);

    /// <summary>Signed-distance primitives and boolean operations (after Inigo Quilez's formulas).</summary>
    public static class Shapes
    {
        public static Sdf Circle(float cx, float cy, float r) =>
            (x, y) => Len(x - cx, y - cy) - r;

        /// <summary>Approximate ellipse (good enough for eyes, mirrors and drops).</summary>
        public static Sdf Ellipse(float cx, float cy, float rx, float ry) => (x, y) =>
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            float k = Len(nx, ny);
            return (k - 1f) * Math.Min(rx, ry);
        };

        /// <summary>Axis-aligned box from its centre and half-size, with rounded corners.</summary>
        public static Sdf Box(float cx, float cy, float halfW, float halfH, float radius = 0f) => (x, y) =>
        {
            float qx = Math.Abs(x - cx) - halfW + radius;
            float qy = Math.Abs(y - cy) - halfH + radius;
            return Len(Math.Max(qx, 0), Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - radius;
        };

        /// <summary>Box given by its top-left corner and size.</summary>
        public static Sdf Rect(float left, float top, float width, float height, float radius = 0f) =>
            Box(left + width / 2f, top + height / 2f, width / 2f, height / 2f, radius);

        /// <summary>Line segment with round caps, <paramref name="thickness"/> wide.</summary>
        public static Sdf Capsule(float ax, float ay, float bx, float by, float thickness) => (x, y) =>
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Len(pax - bax * h, pay - bay * h) - thickness / 2f;
        };

        /// <summary>Arc of a circle (angles in degrees, 0 = right, clockwise because y is down).</summary>
        public static Sdf Arc(float cx, float cy, float r, float fromDeg, float toDeg, float thickness)
        {
            float a0 = fromDeg * (float)Math.PI / 180f, a1 = toDeg * (float)Math.PI / 180f;
            float ax = cx + r * (float)Math.Cos(a0), ay = cy + r * (float)Math.Sin(a0);
            float bx = cx + r * (float)Math.Cos(a1), by = cy + r * (float)Math.Sin(a1);
            return (x, y) =>
            {
                float ang = (float)Math.Atan2(y - cy, x - cx);
                if (InArc(ang, a0, a1)) return Math.Abs(Len(x - cx, y - cy) - r) - thickness / 2f;
                return Math.Min(Len(x - ax, y - ay), Len(x - bx, y - by)) - thickness / 2f;
            };
        }

        /// <summary>Convex or concave polygon (even-odd), points in order.</summary>
        public static Sdf Polygon(params (float x, float y)[] pts) => (x, y) =>
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                var a = pts[j];
                var b = pts[i];
                float ex = b.x - a.x, ey = b.y - a.y, wx = x - a.x, wy = y - a.y;
                float t = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                d = Math.Min(d, Len(wx - ex * t, wy - ey * t));
                if ((a.y > y) != (b.y > y) && x < a.x + (y - a.y) * ex / ey) inside = !inside;
            }
            return inside ? -d : d;
        };

        /// <summary>Regular star, first point straight up.</summary>
        public static Sdf Star(float cx, float cy, float outer, float inner, int points = 5)
        {
            var pts = new (float, float)[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                pts[i] = (cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a));
            }
            return Polygon(pts);
        }

        public static Sdf Union(params Sdf[] s) => (x, y) =>
        {
            float d = float.MaxValue;
            foreach (var f in s) d = Math.Min(d, f(x, y));
            return d;
        };

        /// <summary>Smooth union: blends shapes with a fillet of radius k (for soft, organic silhouettes).</summary>
        public static Sdf SmoothUnion(Sdf a, Sdf b, float k) => (x, y) =>
        {
            float da = a(x, y), db = b(x, y);
            float h = Clamp01(0.5f + 0.5f * (db - da) / k);
            return Lerp(db, da, h) - k * h * (1f - h);
        };

        public static Sdf Subtract(Sdf a, Sdf b) => (x, y) => Math.Max(a(x, y), -b(x, y));
        public static Sdf Intersect(Sdf a, Sdf b) => (x, y) => Math.Max(a(x, y), b(x, y));

        /// <summary>Hollow band of half-width w around the shape's edge.</summary>
        public static Sdf Ring(Sdf s, float halfWidth) => (x, y) => Math.Abs(s(x, y)) - halfWidth;

        public static Sdf Grow(Sdf s, float r) => (x, y) => s(x, y) - r;

        /// <summary>Wobbles a shape's edge with noise: ragged cloth, uneven plaster, organic silhouettes.</summary>
        public static Sdf Displace(Sdf s, float amount, float scale, int seed = 0) =>
            (x, y) => s(x, y) + (Noise.Fbm(x, y, scale, 3, seed) - 0.5f) * 2f * amount;

        /// <summary>Polyline as connected capsules (cracks, cobweb threads, hair strands).</summary>
        public static Sdf Path(float thickness, params (float x, float y)[] pts)
        {
            var parts = new Sdf[Math.Max(1, pts.Length - 1)];
            for (int i = 0; i < pts.Length - 1; i++)
                parts[i] = Capsule(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, thickness);
            return pts.Length < 2 ? Circle(pts[0].x, pts[0].y, thickness / 2) : Union(parts);
        }
        public static Sdf Move(Sdf s, float dx, float dy) => (x, y) => s(x - dx, y - dy);

        /// <summary>Uniform scale about a point.</summary>
        public static Sdf Scale(Sdf s, float k, float cx, float cy) => (x, y) => s(cx + (x - cx) / k, cy + (y - cy) / k) * k;

        /// <summary>Rotate about a point (degrees, clockwise on screen).</summary>
        public static Sdf Rotate(Sdf s, float deg, float cx, float cy)
        {
            float a = -deg * (float)Math.PI / 180f, c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
            return (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                return s(cx + dx * c - dy * sn, cy + dx * sn + dy * c);
            };
        }

        static bool InArc(float ang, float a0, float a1)
        {
            const float Tau = (float)(Math.PI * 2);
            float span = ((a1 - a0) % Tau + Tau) % Tau;
            float rel = ((ang - a0) % Tau + Tau) % Tau;
            return rel <= span;
        }

        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
