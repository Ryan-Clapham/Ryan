using System;

namespace ColourCascade.Procedural
{
    /// <summary>Straight-alpha colour with float channels in 0..1.</summary>
    public struct Rgba
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Rgba Hex(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return new Rgba(1, 1, 1, alpha);
            if (hex[0] == '#') hex = hex.Substring(1);
            uint v = Convert.ToUInt32(hex, 16);
            if (hex.Length == 8) return new Rgba(((v >> 24) & 255) / 255f, ((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
            return new Rgba(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, alpha);
        }

        public static readonly Rgba White = new Rgba(1, 1, 1, 1);
        public static readonly Rgba Black = new Rgba(0, 0, 0, 1);
        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);
        public Rgba Scale(float k) => new Rgba(Clamp(R * k), Clamp(G * k), Clamp(B * k), A);
        public Rgba Lighten(float t) => Lerp(this, new Rgba(1, 1, 1, A), t);
        public Rgba Darken(float t) => Lerp(this, new Rgba(0, 0, 0, A), t);

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        static float Clamp(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>Colour as a function of position (design units).</summary>
    public abstract class Paint
    {
        public abstract Rgba At(float x, float y);

        public static implicit operator Paint(Rgba c) => new SolidPaint(c);
    }

    public sealed class SolidPaint : Paint
    {
        readonly Rgba c;
        public SolidPaint(Rgba c) { this.c = c; }
        public override Rgba At(float x, float y) => c;
    }

    public sealed class LinearPaint : Paint
    {
        readonly float x0, y0, dx, dy, len2;
        readonly Rgba c0, c1;

        public LinearPaint(float x0, float y0, Rgba c0, float x1, float y1, Rgba c1)
        {
            this.x0 = x0;
            this.y0 = y0;
            dx = x1 - x0;
            dy = y1 - y0;
            len2 = Math.Max(1e-6f, dx * dx + dy * dy);
            this.c0 = c0;
            this.c1 = c1;
        }

        /// <summary>Vertical gradient from bottom colour at y0 to top colour at y1.</summary>
        public static LinearPaint Vertical(float yBottom, Rgba bottom, float yTop, Rgba top) => new LinearPaint(0, yBottom, bottom, 0, yTop, top);

        public override Rgba At(float x, float y)
        {
            float t = ((x - x0) * dx + (y - y0) * dy) / len2;
            return Rgba.Lerp(c0, c1, t);
        }
    }

    public sealed class RadialPaint : Paint
    {
        readonly float cx, cy, r;
        readonly Rgba inner, outer;

        public RadialPaint(float cx, float cy, float r, Rgba inner, Rgba outer)
        {
            this.cx = cx;
            this.cy = cy;
            this.r = Math.Max(1e-3f, r);
            this.inner = inner;
            this.outer = outer;
        }

        public override Rgba At(float x, float y)
        {
            float dx = x - cx, dy = y - cy;
            return Rgba.Lerp(inner, outer, (float)Math.Sqrt(dx * dx + dy * dy) / r);
        }
    }

    /// <summary>
    /// Signed distance shape in design units (negative inside). Bounds let the rasteriser skip empty pixels.
    /// </summary>
    public abstract class Shape
    {
        public abstract float Dist(float x, float y);

        public virtual void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = y0 = -1e6f;
            x1 = y1 = 1e6f;
        }

        public Shape Union(params Shape[] others) => new UnionShape(Prepend(this, others));
        public Shape Minus(Shape other) => new SubtractShape(this, other);
        public Shape Intersect(Shape other) => new IntersectShape(this, other);
        public Shape Inflate(float r) => new InflateShape(this, r);
        public Shape Outline(float halfWidth) => new OutlineShape(this, halfWidth);
        public Shape Move(float dx, float dy) => new MoveShape(this, dx, dy);
        public Shape Rotate(float degrees, float cx, float cy) => new RotateShape(this, degrees, cx, cy);

        static Shape[] Prepend(Shape s, Shape[] rest)
        {
            var all = new Shape[rest.Length + 1];
            all[0] = s;
            Array.Copy(rest, 0, all, 1, rest.Length);
            return all;
        }

        protected static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        protected static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    public sealed class Circle : Shape
    {
        readonly float cx, cy, r;
        public Circle(float cx, float cy, float r) { this.cx = cx; this.cy = cy; this.r = r; }
        public override float Dist(float x, float y) => Len(x - cx, y - cy) - r;

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = cx - r; y0 = cy - r; x1 = cx + r; y1 = cy + r;
        }
    }

    public sealed class Ellipse : Shape
    {
        readonly float cx, cy, rx, ry;
        public Ellipse(float cx, float cy, float rx, float ry) { this.cx = cx; this.cy = cy; this.rx = rx; this.ry = ry; }

        public override float Dist(float x, float y)
        {
            float px = (x - cx), py = (y - cy);
            float k0 = Len(px / rx, py / ry);
            float k1 = Len(px / (rx * rx), py / (ry * ry));
            if (k1 < 1e-6f) return -Math.Min(rx, ry);
            return k0 * (k0 - 1f) / k1;
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = cx - rx; y0 = cy - ry; x1 = cx + rx; y1 = cy + ry;
        }
    }

    /// <summary>Axis-aligned box with rounded corners, given by centre and half extents.</summary>
    public sealed class RoundBox : Shape
    {
        readonly float cx, cy, hw, hh, r;

        public RoundBox(float cx, float cy, float halfW, float halfH, float radius)
        {
            this.cx = cx; this.cy = cy; hw = halfW; hh = halfH;
            r = Math.Max(0, Math.Min(radius, Math.Min(halfW, halfH)));
        }

        public static RoundBox FromRect(float x0, float y0, float x1, float y1, float radius)
            => new RoundBox((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2, radius);

        public override float Dist(float x, float y)
        {
            float qx = Math.Abs(x - cx) - (hw - r);
            float qy = Math.Abs(y - cy) - (hh - r);
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return Len(ox, oy) + Math.Min(Math.Max(qx, qy), 0) - r;
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = cx - hw; y0 = cy - hh; x1 = cx + hw; y1 = cy + hh;
        }
    }

    /// <summary>Line segment with round caps (a "stroke").</summary>
    public sealed class Capsule : Shape
    {
        readonly float ax, ay, bx, by, r;
        public Capsule(float ax, float ay, float bx, float by, float radius) { this.ax = ax; this.ay = ay; this.bx = bx; this.by = by; r = radius; }

        public override float Dist(float x, float y)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float dd = bax * bax + bay * bay;
            float h = dd > 0 ? Clamp01((pax * bax + pay * bay) / dd) : 0;
            return Len(pax - bax * h, pay - bay * h) - r;
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = Math.Min(ax, bx) - r; y0 = Math.Min(ay, by) - r; x1 = Math.Max(ax, bx) + r; y1 = Math.Max(ay, by) + r;
        }
    }

    /// <summary>Arbitrary (also concave) polygon. Vertices as x0,y0,x1,y1...</summary>
    public sealed class Polygon : Shape
    {
        readonly float[] v;

        public Polygon(params float[] xy)
        {
            if (xy.Length < 6 || xy.Length % 2 != 0) throw new ArgumentException("Polygon needs 3+ points");
            v = xy;
        }

        public static Polygon Regular(float cx, float cy, float r, int sides, float rotationDeg)
        {
            var pts = new float[sides * 2];
            for (int i = 0; i < sides; i++)
            {
                double a = (rotationDeg + 90 + 360.0 * i / sides) * Math.PI / 180;
                pts[i * 2] = cx + (float)(Math.Cos(a) * r);
                pts[i * 2 + 1] = cy + (float)(Math.Sin(a) * r);
            }
            return new Polygon(pts);
        }

        public static Polygon Star(float cx, float cy, float rOuter, float rInner, int points, float rotationDeg = 0)
        {
            var pts = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                double a = (rotationDeg + 90 + 180.0 * i / points) * Math.PI / 180;
                float r = i % 2 == 0 ? rOuter : rInner;
                pts[i * 2] = cx + (float)(Math.Cos(a) * r);
                pts[i * 2 + 1] = cy + (float)(Math.Sin(a) * r);
            }
            return new Polygon(pts);
        }

        public override float Dist(float px, float py)
        {
            int n = v.Length / 2;
            float d = (px - v[0]) * (px - v[0]) + (py - v[1]) * (py - v[1]);
            float s = 1;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float vix = v[i * 2], viy = v[i * 2 + 1], vjx = v[j * 2], vjy = v[j * 2 + 1];
                float ex = vjx - vix, ey = vjy - viy;
                float wx = px - vix, wy = py - viy;
                float ee = ex * ex + ey * ey;
                float t = ee > 0 ? Clamp01((wx * ex + wy * ey) / ee) : 0;
                float bx = wx - ex * t, by = wy - ey * t;
                d = Math.Min(d, bx * bx + by * by);
                bool c1 = py >= viy, c2 = py < vjy, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * (float)Math.Sqrt(d);
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = y0 = float.MaxValue;
            x1 = y1 = float.MinValue;
            for (int i = 0; i < v.Length; i += 2)
            {
                x0 = Math.Min(x0, v[i]); x1 = Math.Max(x1, v[i]);
                y0 = Math.Min(y0, v[i + 1]); y1 = Math.Max(y1, v[i + 1]);
            }
        }
    }

    /// <summary>Circular sector: centre, radius, facing direction (degrees, 0 = up, clockwise) and half opening angle.</summary>
    public sealed class Pie : Shape
    {
        readonly float cx, cy, r, sinA, cosA, cosR, sinR;

        public Pie(float cx, float cy, float radius, float directionDeg, float halfApertureDeg)
        {
            this.cx = cx; this.cy = cy; r = radius;
            double ap = halfApertureDeg * Math.PI / 180;
            sinA = (float)Math.Sin(ap);
            cosA = (float)Math.Cos(ap);
            double dir = directionDeg * Math.PI / 180;
            cosR = (float)Math.Cos(dir);
            sinR = (float)Math.Sin(dir);
        }

        public override float Dist(float x, float y)
        {
            float dx = x - cx, dy = y - cy;
            // rotate so the sector faces +y
            float px = dx * cosR - dy * sinR;
            float py = dx * sinR + dy * cosR;
            px = Math.Abs(px);
            float l = Len(px, py) - r;
            float dot = Math.Max(0, Math.Min(px * sinA + py * cosA, r));
            float m = Len(px - sinA * dot, py - cosA * dot);
            float side = cosA * px - sinA * py;
            return Math.Max(l, m * Math.Sign(side));
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = cx - r; y0 = cy - r; x1 = cx + r; y1 = cy + r;
        }
    }

    public sealed class HalfPlane : Shape
    {
        readonly float nx, ny, c;

        /// <summary>Inside where (x,y)·(nx,ny) &lt; c. Normal must be unit length.</summary>
        public HalfPlane(float nx, float ny, float c) { this.nx = nx; this.ny = ny; this.c = c; }
        public static HalfPlane Below(float y) => new HalfPlane(0, 1, y);
        public static HalfPlane Above(float y) => new HalfPlane(0, -1, -y);
        public override float Dist(float x, float y) => x * nx + y * ny - c;
    }

    sealed class UnionShape : Shape
    {
        readonly Shape[] parts;
        public UnionShape(Shape[] parts) { this.parts = parts; }

        public override float Dist(float x, float y)
        {
            float d = float.MaxValue;
            for (int i = 0; i < parts.Length; i++) d = Math.Min(d, parts[i].Dist(x, y));
            return d;
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            x0 = y0 = float.MaxValue;
            x1 = y1 = float.MinValue;
            foreach (var p in parts)
            {
                float a, b, c, d;
                p.Bounds(out a, out b, out c, out d);
                x0 = Math.Min(x0, a); y0 = Math.Min(y0, b); x1 = Math.Max(x1, c); y1 = Math.Max(y1, d);
            }
        }
    }

    sealed class SubtractShape : Shape
    {
        readonly Shape a, b;
        public SubtractShape(Shape a, Shape b) { this.a = a; this.b = b; }
        public override float Dist(float x, float y) => Math.Max(a.Dist(x, y), -b.Dist(x, y));
        public override void Bounds(out float x0, out float y0, out float x1, out float y1) => a.Bounds(out x0, out y0, out x1, out y1);
    }

    sealed class IntersectShape : Shape
    {
        readonly Shape a, b;
        public IntersectShape(Shape a, Shape b) { this.a = a; this.b = b; }
        public override float Dist(float x, float y) => Math.Max(a.Dist(x, y), b.Dist(x, y));
        public override void Bounds(out float x0, out float y0, out float x1, out float y1) => a.Bounds(out x0, out y0, out x1, out y1);
    }

    sealed class InflateShape : Shape
    {
        readonly Shape s;
        readonly float r;
        public InflateShape(Shape s, float r) { this.s = s; this.r = r; }
        public override float Dist(float x, float y) => s.Dist(x, y) - r;

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            s.Bounds(out x0, out y0, out x1, out y1);
            x0 -= r; y0 -= r; x1 += r; y1 += r;
        }
    }

    sealed class OutlineShape : Shape
    {
        readonly Shape s;
        readonly float w;
        public OutlineShape(Shape s, float halfWidth) { this.s = s; w = halfWidth; }
        public override float Dist(float x, float y) => Math.Abs(s.Dist(x, y)) - w;

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            s.Bounds(out x0, out y0, out x1, out y1);
            x0 -= w; y0 -= w; x1 += w; y1 += w;
        }
    }

    sealed class MoveShape : Shape
    {
        readonly Shape s;
        readonly float dx, dy;
        public MoveShape(Shape s, float dx, float dy) { this.s = s; this.dx = dx; this.dy = dy; }
        public override float Dist(float x, float y) => s.Dist(x - dx, y - dy);

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            s.Bounds(out x0, out y0, out x1, out y1);
            x0 += dx; x1 += dx; y0 += dy; y1 += dy;
        }
    }

    sealed class RotateShape : Shape
    {
        readonly Shape s;
        readonly float cx, cy, cos, sin;

        public RotateShape(Shape s, float degrees, float cx, float cy)
        {
            this.s = s; this.cx = cx; this.cy = cy;
            double a = -degrees * Math.PI / 180;
            cos = (float)Math.Cos(a);
            sin = (float)Math.Sin(a);
        }

        public override float Dist(float x, float y)
        {
            float dx = x - cx, dy = y - cy;
            return s.Dist(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
        }

        public override void Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            float a, b, c, d;
            s.Bounds(out a, out b, out c, out d);
            float r = Math.Max(Math.Max(Len(a - cx, b - cy), Len(c - cx, d - cy)), Math.Max(Len(a - cx, d - cy), Len(c - cx, b - cy)));
            x0 = cx - r; y0 = cy - r; x1 = cx + r; y1 = cy + r;
        }
    }
}
