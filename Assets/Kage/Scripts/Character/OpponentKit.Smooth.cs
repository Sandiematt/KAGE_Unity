using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The smooth-shape half of the opponent art kit (SAN-112): curved outlines in place of hand-placed corner points, so a foe
    /// reads as a figure of rounded, tapering cloth and limbs at Kage's level of finish rather than as stacked boxes. It ports
    /// the JS canvas drawing the foes were designed in (<c>21-characters/_shared/</c>): <c>orderShape</c> (a closed path that
    /// rounds every corner with a quadratic through the edge midpoints, sharp only where flagged), <c>animeCapsule</c> (a
    /// tapered limb with round ends and a swelling), quadratic and cubic bezier paths, and <c>celFill</c> (a base tone, a
    /// shadow strip on the side away from the light, a lit edge, a dark seam). Everything is tessellated here into simple,
    /// closed polygons in bind space, which the rig paints as it paints any part; nothing here touches the skeleton.
    /// <para>
    /// <b>Frames.</b> Shapes are written in a <see cref="Frame"/>: a JS drawing's own coordinates (head units with +x the face
    /// side, a spine frame of u along the spine and w across it, or plain reference pixels) mapped into Kage's reference pixels
    /// (x to the back, y down, 400 px a unit). A JS shape can then be copied number for number.
    /// </para>
    /// <para>
    /// <b>Cel shading.</b> <see cref="Cel"/> turns one outline into its painted polygons (seam, shadow, lit edge, base) by
    /// insetting the outline towards the light (<see cref="ShadeDir"/>): what the JS strip of a shifted copy leaves, without
    /// polygon clipping. <see cref="AddCel"/> adds that as one part, with the rig's world-fixed back-light rim on its outline
    /// (<see cref="PartDef.rimPts"/>); <see cref="AddCloth"/> adds it as cloth bound to spring chains (<see cref="SoftChains"/>),
    /// with the rim painted into the mesh so it bends with the cloth.
    /// </para>
    /// </summary>
    public static partial class OpponentKit
    {
        /// <summary>Reference pixels per JS design unit on the smooth-shape foes, at Kage's bind size (SAN-112: 2.14 JS px per unit on the
        /// Husk, 2.08 reference px per JS px once its 1.16 body scale is taken out). A part set may use its own.</summary>
        public const float JsUnit = 4.45f;

        /// <summary>The direction (bind space, facing right) the cel shadow falls: to the back and down, away from a key light at the
        /// front-top, on the same side as the rig's violet or crimson back-light rim (the JS celFill's light).</summary>
        public static readonly Vector2 ShadeDir = new Vector2(-0.55f, -0.835f).normalized;

        /// <summary>The dark seam round every cel part (JS <c>ANIME.outline</c>).</summary>
        public static readonly Color Seam = KMath.Hex("#060509");

        // ------------------------------------------------------------------------------------ frames

        /// <summary>
        /// A drawing's own coordinates mapped into reference pixels: <c>origin + ax * a + ay * b</c>. <see cref="Px"/> maps one point,
        /// <see cref="Local"/> maps back.
        /// </summary>
        public readonly struct Frame
        {
            public readonly Vector2 origin, ax, ay;
            public Frame(Vector2 origin, Vector2 ax, Vector2 ay) { this.origin = origin; this.ax = ax; this.ay = ay; }

            /// <summary>Plain reference pixels.</summary>
            public static Frame Ref => new(Vector2.zero, Vector2.right, Vector2.up);

            /// <summary>A JS head frame: centred on (cx, cy) reference px, r px a head unit, +x the face side (the front, which is -x in
            /// reference pixels), +y down.</summary>
            public static Frame Head(float cx, float cy, float r) => new(new Vector2(cx, cy), new Vector2(-r, 0f), new Vector2(0f, r));

            /// <summary>
            /// A JS spine frame (<c>spX</c>/<c>spY</c>): u = 0 at the hip point (hx, hy), u = 1 at the neck (nx, ny); w px across it
            /// towards the front, <paramref name="unit"/> reference px per w unit.
            /// </summary>
            public static Frame Spine(float hx, float hy, float nx, float ny, float unit)
            {
                Vector2 d = new(nx - hx, ny - hy);
                Vector2 n = new Vector2(d.y, -d.x).normalized;      // perpendicular to the spine...
                if (n.x > 0f) n = -n;                               // ...on the front side (low x)
                return new Frame(new Vector2(hx, hy), d, n * unit);
            }

            /// <summary>A frame along a limb segment (ax, ay) to (bx, by): +a along it (1 at b), +b across it to the front, <paramref name="unit"/> px per b unit.</summary>
            public static Frame Along(float ax, float ay, float bx, float by, float unit)
            {
                Vector2 d = new(bx - ax, by - ay);
                Vector2 n = new Vector2(d.y, -d.x).normalized;
                if (n.x > 0f) n = -n;
                return new Frame(new Vector2(ax, ay), d, n * unit);
            }

            public Vector2 Px(float a, float b) => origin + ax * a + ay * b;

            public Vector2 Local(Vector2 px)
            {
                Vector2 q = px - origin;
                float det = ax.x * ay.y - ax.y * ay.x;
                return new Vector2((q.x * ay.y - q.y * ay.x) / det, (ax.x * q.y - ax.y * q.x) / det);
            }

            /// <summary>The points (a, b pairs) in reference pixels, as x, y pairs (for <see cref="Band"/>, <see cref="V"/>).</summary>
            public float[] Xy(params float[] ab)
            {
                var r = new float[ab.Length];
                for (int i = 0; i + 1 < ab.Length; i += 2) { var p = Px(ab[i], ab[i + 1]); r[i] = p.x; r[i + 1] = p.y; }
                return r;
            }
        }

        // ------------------------------------------------------------------------------------ the path builder

        /// <summary>
        /// A closed outline built like a canvas path, in a <see cref="Frame"/>'s coordinates, flattened to a polygon: line, quadratic
        /// and cubic bezier segments and circular arcs, about one point per <see cref="Step"/> reference px of curve. <see cref="Bind"/>
        /// returns it in bind space with repeated and straight-through points dropped, ready to triangulate.
        /// </summary>
        public sealed class Outline
        {
            /// <summary>Reference px per tessellated segment along curves.</summary>
            public const float Step = 5f;

            readonly Frame _f;
            readonly List<Vector2> _px = new();
            Vector2 _cur;

            public Outline(Frame f) { _f = f; }
            public Outline() : this(Frame.Ref) { }

            Vector2 F(float a, float b) => _f.Px(a, b);

            public Outline MoveTo(float a, float b) { _cur = F(a, b); _px.Add(_cur); return this; }

            public Outline LineTo(float a, float b) { _cur = F(a, b); _px.Add(_cur); return this; }

            public Outline QuadTo(float ca, float cb, float a, float b)
            {
                Vector2 p0 = _cur, c = F(ca, cb), p1 = F(a, b);
                int n = Steps((c - p0).magnitude + (p1 - c).magnitude);
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n, u = 1f - t;
                    _px.Add(u * u * p0 + 2f * u * t * c + t * t * p1);
                }
                _cur = p1;
                return this;
            }

            public Outline CubicTo(float c1a, float c1b, float c2a, float c2b, float a, float b)
            {
                Vector2 p0 = _cur, c1 = F(c1a, c1b), c2 = F(c2a, c2b), p1 = F(a, b);
                int n = Steps((c1 - p0).magnitude + (c2 - c1).magnitude + (p1 - c2).magnitude);
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n, u = 1f - t;
                    _px.Add(u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p1);
                }
                _cur = p1;
                return this;
            }

            /// <summary>A circular arc about (ca, cb) of radius r (frame units) from angle a0 to a1 (radians, in the frame), drawn as
            /// canvas <c>arc</c> would (a line to its start first).</summary>
            public Outline Arc(float ca, float cb, float r, float a0, float a1)
            {
                Vector2 c = F(ca, cb);
                float len = Mathf.Abs(a1 - a0) * (F(ca + r, cb) - c).magnitude;
                int n = Steps(len);
                for (int i = 0; i <= n; i++)
                {
                    float t = Mathf.Lerp(a0, a1, i / (float)n);
                    _px.Add(F(ca + Mathf.Cos(t) * r, cb + Mathf.Sin(t) * r));
                }
                _cur = _px[_px.Count - 1];
                return this;
            }

            static int Steps(float lenPx) => Mathf.Clamp(Mathf.CeilToInt(lenPx / Step), 2, 64);

            /// <summary>The outline in reference pixels (closed: the last point joins the first).</summary>
            public List<Vector2> Px => _px;

            public Vector2[] Bind() => Clean(ToBindList(_px));
        }

        static List<Vector2> ToBindList(List<Vector2> px)
        {
            var r = new List<Vector2>(px.Count);
            foreach (var p in px) r.Add(P(p.x, p.y));
            return r;
        }

        /// <summary>Drops repeated points (closer than a tenth of a reference pixel) and points where the outline runs straight on.</summary>
        public static Vector2[] Clean(IList<Vector2> pts)
        {
            const float eps = 0.1f / KageModel.RefScale;
            var a = new List<Vector2>(pts.Count);
            foreach (var p in pts)
                if (a.Count == 0 || (p - a[a.Count - 1]).sqrMagnitude > eps * eps) a.Add(p);
            while (a.Count > 1 && (a[0] - a[a.Count - 1]).sqrMagnitude <= eps * eps) a.RemoveAt(a.Count - 1);
            bool changed = true;
            while (changed && a.Count > 3)
            {
                changed = false;
                for (int i = 0; i < a.Count && a.Count > 3; i++)
                {
                    Vector2 p0 = a[(i + a.Count - 1) % a.Count], p1 = a[i], p2 = a[(i + 1) % a.Count];
                    Vector2 e0 = p1 - p0, e1 = p2 - p1;
                    float cross = e0.x * e1.y - e0.y * e1.x;
                    if (Mathf.Abs(cross) <= 1e-7f * e0.magnitude * e1.magnitude * 1000f && Vector2.Dot(e0, e1) > 0f)
                    {
                        a.RemoveAt(i);
                        changed = true;
                        i--;
                    }
                }
            }
            return a.ToArray();
        }

        // ------------------------------------------------------------------------------------ shapes

        /// <summary>
        /// The JS <c>orderShape</c>: a closed outline through points given as (x, y, sharp) triples in the frame. A point with sharp 0
        /// is a rounded corner (a quadratic through the midpoints of its two edges, the point its control); a point with sharp 1 is a
        /// hard corner the outline passes through (torn cloth, a horn's tip, a paper's zigzag).
        /// </summary>
        public static Vector2[] Rounded(Frame f, params float[] xys)
        {
            int n = xys.Length / 3;
            float X(int i) => xys[((i % n) + n) % n * 3];
            float Y(int i) => xys[((i % n) + n) % n * 3 + 1];
            bool Sharp(int i) => xys[((i % n) + n) % n * 3 + 2] > 0.5f;
            var o = new Outline(f);
            if (Sharp(0)) o.MoveTo(X(0), Y(0));
            else o.MoveTo((X(-1) + X(0)) * 0.5f, (Y(-1) + Y(0)) * 0.5f);
            for (int i = Sharp(0) ? 1 : 0; i < n; i++)
            {
                if (Sharp(i)) o.LineTo(X(i), Y(i));
                else o.QuadTo(X(i), Y(i), (X(i) + X(i + 1)) * 0.5f, (Y(i) + Y(i + 1)) * 0.5f);
            }
            return o.Bind();
        }

        /// <summary><see cref="Rounded"/> with every corner rounded: points as (x, y) pairs (the JS <c>animeTorso</c> and an all-smooth <c>orderShape</c>).</summary>
        public static Vector2[] RoundedAll(Frame f, params float[] xy)
        {
            var t = new float[xy.Length / 2 * 3];
            for (int i = 0; i < xy.Length / 2; i++) { t[i * 3] = xy[i * 2]; t[i * 3 + 1] = xy[i * 2 + 1]; }
            return Rounded(f, t);
        }

        /// <summary>
        /// The JS <c>animeCapsule</c> in reference px: a limb piece from a to b, radius <paramref name="ra"/> at a and <paramref name="rb"/>
        /// at b, its sides curving out by <paramref name="bulge"/> at <paramref name="pos"/> (0 at a, 1 at b), round ends. Sleeves, hakama,
        /// wrapped forearms and shins.
        /// </summary>
        public static Vector2[] Capsule(float ax, float ay, float bx, float by, float ra, float rb, float bulge = 0f, float pos = 0.5f)
        {
            Vector2 a = new(ax, ay), b = new(bx, by), d = b - a;
            float L = Mathf.Max(1e-3f, d.magnitude);
            Vector2 u = d / L, n = new(-u.y, u.x);
            float rm = ra + (rb - ra) * pos + bulge;
            Vector2 m = a + d * pos;
            Vector2 p0 = a + n * ra, p2 = b + n * rb, q0 = b - n * rb, q2 = a - n * ra;
            Vector2 c1 = 2f * (m + n * rm) - (p0 + p2) * 0.5f, c2 = 2f * (m - n * rm) - (q0 + q2) * 0.5f;
            float an = Mathf.Atan2(n.y, n.x);
            var o = new Outline();
            o.MoveTo(p0.x, p0.y).QuadTo(c1.x, c1.y, p2.x, p2.y);
            o.Arc(b.x, b.y, rb, an, an - Mathf.PI);
            o.QuadTo(c2.x, c2.y, q2.x, q2.y);
            o.Arc(a.x, a.y, ra, an + Mathf.PI, an);
            return o.Bind();
        }

        /// <summary>A smooth ellipse in reference px (centre, radii, turn in degrees), about one point per <see cref="Outline.Step"/> px.</summary>
        public static Vector2[] Oval(float cx, float cy, float rx, float ry, float deg = 0f)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * (rx + ry) / Outline.Step), 12, 96);
            return Ellipse(cx, cy, rx, ry, deg, n);
        }

        /// <summary>
        /// A torn cloth panel (the JS <c>tornPanelPath</c>): smooth sides through the <paramref name="left"/> and <paramref name="right"/>
        /// edge points (reference px, top first, as x, y pairs), the hem between their last points torn into <paramref name="teeth"/>
        /// uneven points <paramref name="rag"/> px deep (<see cref="Teeth"/>, starting at <paramref name="seed"/>). Robe panels, capes, tatters.
        /// </summary>
        public static Vector2[] TornPanel(float[] left, float[] right, int teeth, float rag, int seed = 0)
        {
            int n = left.Length / 2;
            Vector2 Lp(int i) => new(left[i * 2], left[i * 2 + 1]);
            Vector2 Rp(int i) => new(right[i * 2], right[i * 2 + 1]);
            var o = new Outline();
            o.MoveTo(Lp(0).x, Lp(0).y);
            for (int i = 1; i < n - 1; i++) { var m = (Lp(i) + Lp(i + 1)) * 0.5f; o.QuadTo(Lp(i).x, Lp(i).y, m.x, m.y); }
            Vector2 le = Lp(n - 1), re = Rp(n - 1);
            o.LineTo(le.x, le.y);
            Vector2 dir = ((le + re) - (Lp(n - 2) + Rp(n - 2))).normalized;
            for (int k = 0; k < teeth; k++)
            {
                float t0 = (k + 0.5f) / teeth, t1 = (k + 1f) / teeth, dd = Teeth[(k + seed) % Teeth.Length] * rag;
                var a = Vector2.Lerp(le, re, t0) + dir * dd;
                o.LineTo(a.x, a.y);
                if (k < teeth - 1) { var b = Vector2.Lerp(le, re, t1) - dir * rag * 0.22f; o.LineTo(b.x, b.y); }
            }
            o.LineTo(re.x, re.y);
            for (int i = n - 2; i > 0; i--) { var m = (Rp(i) + Rp(i - 1)) * 0.5f; o.QuadTo(Rp(i).x, Rp(i).y, m.x, m.y); }
            o.LineTo(Rp(0).x, Rp(0).y);
            return o.Bind();
        }

        /// <summary>The uneven depths of torn points (JS <c>HOLLOW_TEETH</c>).</summary>
        public static readonly float[] Teeth = { 1f, 0.34f, 0.82f, 0.16f, 1.18f, 0.5f, 0.92f, 0.26f, 0.7f };

        /// <summary>A quadratic curve in a frame sampled to x, y pairs in reference px (for a <see cref="Band"/> along a curve).</summary>
        public static float[] QuadXy(Frame f, float a0, float b0, float ca, float cb, float a1, float b1, int n = 12)
        {
            var r = new float[(n + 1) * 2];
            Vector2 p0 = f.Px(a0, b0), c = f.Px(ca, cb), p1 = f.Px(a1, b1);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                Vector2 p = u * u * p0 + 2f * u * t * c + t * t * p1;
                r[i * 2] = p.x; r[i * 2 + 1] = p.y;
            }
            return r;
        }

        /// <summary>
        /// The JS <c>hollowCrack</c>: a break along a polyline (x, y pairs in reference px): a dark split, a soft bloom in
        /// <paramref name="glow"/> (its alpha kept) and a thin bright core, <paramref name="wPx"/> the core's width.
        /// </summary>
        public static IEnumerable<(Vector2[] pts, Color col)> Crack(Color glow, Color core, float wPx, params float[] xy)
        {
            yield return (Band(wPx * 2.6f, xy), Seam);
            yield return (Band(wPx * 3.2f, xy), glow);
            yield return (Band(wPx, xy), core);
        }

        /// <summary>A dashed line along a polyline (x, y pairs in reference px): dashes <paramref name="dash"/> px long with <paramref name="gap"/> px between.</summary>
        public static IEnumerable<(Vector2[] pts, Color col)> Dashes(Color c, float wPx, float dash, float gap, params float[] xy)
        {
            // cumulative length along the polyline, then one dash per period: a fixed count, so no float step can stall
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            var cum = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            float total = cum[cum.Length - 1], period = Mathf.Max(0.5f, dash + gap);
            int count = Mathf.CeilToInt(total / period);
            for (int k = 0; k < count; k++)
            {
                float s0 = k * period, s1 = Mathf.Min(total, s0 + dash);
                if (s1 - s0 < 0.5f) continue;
                var line = new List<float>();
                Vector2 a0 = At(pts, cum, s0);
                line.Add(a0.x); line.Add(a0.y);
                for (int i = 1; i < pts.Count - 1; i++) if (cum[i] > s0 && cum[i] < s1) { line.Add(pts[i].x); line.Add(pts[i].y); }
                Vector2 a1 = At(pts, cum, s1);
                line.Add(a1.x); line.Add(a1.y);
                yield return (Band(wPx, line.ToArray()), c);
            }
        }

        static Vector2 At(List<Vector2> pts, float[] cum, float s)
        {
            for (int i = 1; i < pts.Count; i++)
                if (cum[i] >= s) return Vector2.Lerp(pts[i - 1], pts[i], (s - cum[i - 1]) / Mathf.Max(1e-6f, cum[i] - cum[i - 1]));
            return pts[pts.Count - 1];
        }

        /// <summary>Detail polygons cut to a convex outline (bind space): wraps round a limb, lines across a fist.</summary>
        public static IEnumerable<(Vector2[] pts, Color col)> Inside(Vector2[] convex, IEnumerable<(Vector2[] pts, Color col)> polys)
        {
            foreach (var (pts, col) in polys)
            {
                var c = ClipConvex(pts, convex);
                if (c.Length >= 3) yield return (c, col);
            }
        }

        // ------------------------------------------------------------------------------------ clipping

        /// <summary>
        /// The part of <paramref name="subject"/> inside <paramref name="convexClip"/> (Sutherland-Hodgman; the subject may be concave,
        /// the clip must be convex): a mask cut along its break, an opening cut out of a robe. Both in the same space. Empty when they
        /// do not meet.
        /// </summary>
        public static Vector2[] ClipConvex(IList<Vector2> subject, IList<Vector2> convexClip)
        {
            var output = new List<Vector2>(subject);
            float s = MeshKit.SignedArea(convexClip) > 0f ? 1f : -1f;
            for (int e = 0; e < convexClip.Count && output.Count > 0; e++)
            {
                Vector2 c0 = convexClip[e], c1 = convexClip[(e + 1) % convexClip.Count];
                float Side(Vector2 p) => s * ((c1.x - c0.x) * (p.y - c0.y) - (c1.y - c0.y) * (p.x - c0.x));
                var input = output;
                output = new List<Vector2>(input.Count + 4);
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 cur = input[i], prev = input[(i + input.Count - 1) % input.Count];
                    float sc = Side(cur), sp = Side(prev);
                    if (sc >= 0f)
                    {
                        if (sp < 0f) output.Add(Vector2.Lerp(prev, cur, sp / (sp - sc)));
                        output.Add(cur);
                    }
                    else if (sp >= 0f) output.Add(Vector2.Lerp(prev, cur, sp / (sp - sc)));
                }
            }
            return output.Count >= 3 ? Clean(output) : new Vector2[0];
        }

        /// <summary>
        /// The overlap of two shapes that may both be concave, as long as each is seen whole from <paramref name="centre"/> (every ray
        /// from it leaves the shape once): a mask sculpted on a face (the face's bridge notch) and broken along a zigzag crack, where
        /// <see cref="ClipConvex"/> needs one of them convex (SAN-76 rework). Each shape is cast along rays from the centre, a fixed
        /// number round the circle plus one through every corner of either, and the nearer edge is kept, so both shapes' corners
        /// stay sharp. All in one space (bind space for a part); empty when the centre is outside either shape.
        /// </summary>
        public static Vector2[] ClipStar(IList<Vector2> a, IList<Vector2> b, Vector2 centre, int rays = 360)
        {
            var angles = new List<float>(rays + a.Count + b.Count);
            for (int i = 0; i < rays; i++) angles.Add(i * (2f * Mathf.PI / rays));
            foreach (var p in a) angles.Add(AngleOf(p - centre));
            foreach (var p in b) angles.Add(AngleOf(p - centre));
            angles.Sort();
            var r = new List<Vector2>(angles.Count);
            float last = -1f;
            foreach (float t in angles)
            {
                if (t - last < 1e-6f) continue;
                last = t;
                var d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                float ra = RayOut(a, centre, d), rb = RayOut(b, centre, d);
                if (ra <= 0f || rb <= 0f) return new Vector2[0];
                r.Add(centre + d * Mathf.Min(ra, rb));
            }
            return Clean(r);
        }

        static float AngleOf(Vector2 v) { float t = Mathf.Atan2(v.y, v.x); return t < 0f ? t + 2f * Mathf.PI : t; }

        /// <summary>How far a ray from <paramref name="o"/> along <paramref name="d"/> runs before it first meets the outline (0 if it never does).</summary>
        static float RayOut(IList<Vector2> pts, Vector2 o, Vector2 d)
        {
            int n = pts.Count;
            float best = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                Vector2 a = pts[k], e = pts[(k + 1) % n] - a;
                float den = d.x * e.y - d.y * e.x;
                if (Mathf.Abs(den) < 1e-12f) continue;
                Vector2 w = a - o;
                float t = (w.x * e.y - w.y * e.x) / den, s = (w.x * d.y - w.y * d.x) / den;
                if (t > 1e-7f && s >= -1e-6f && s <= 1f + 1e-6f && t < best) best = t;
            }
            return best == float.MaxValue ? 0f : best;
        }

        // ------------------------------------------------------------------------------------ cel shading

        /// <summary>A cel tone: the shadow, the base and the lit edge (the JS <c>tone</c> triple <c>[shadow, base, highlight]</c>).</summary>
        public readonly struct Tone
        {
            public readonly Color shade, mid, lit;
            public Tone(Color shade, Color mid, Color lit) { this.shade = shade; this.mid = mid; this.lit = lit; }
        }

        /// <summary>
        /// The outline moved in along its normals: by up to <paramref name="toShade"/> (bind units) where it faces <paramref name="dir"/>,
        /// and up to <paramref name="toLight"/> where it faces away; each point by no more than 0.45 of the shape's thickness behind it,
        /// so thin tips and teeth stay whole. What a copy of the shape shifted towards the light covers (the JS celFill strip).
        /// </summary>
        public static Vector2[] Inset(IList<Vector2> pts, Vector2 dir, float toShade, float toLight)
        {
            int n = pts.Count;
            var r = new Vector2[n];
            float sign = MeshKit.SignedArea(pts) > 0f ? 1f : -1f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = pts[(i + n - 1) % n], p1 = pts[i], p2 = pts[(i + 1) % n];
                Vector2 e0 = (p1 - p0).normalized, e1 = (p2 - p1).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x) * sign, n1 = new Vector2(e1.y, -e1.x) * sign;
                Vector2 m = n0 + n1;
                m = m.sqrMagnitude < 1e-10f ? n0 : m.normalized;
                float k = Vector2.Dot(m, dir);
                float amount = toShade * Mathf.Max(0f, k) + toLight * Mathf.Max(0f, -k);
                if (amount <= 0f) { r[i] = p1; continue; }
                amount = Mathf.Min(amount, 0.45f * Depth(pts, i, -m));
                r[i] = p1 - m * amount;
            }
            return r;
        }

        /// <summary>How far a ray from point i along <paramref name="d"/> runs inside the shape before it meets an edge.</summary>
        static float Depth(IList<Vector2> pts, int i, Vector2 d)
        {
            int n = pts.Count;
            Vector2 o = pts[i];
            float best = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n;
                if (k == i || k1 == i) continue;
                Vector2 a = pts[k], e = pts[k1] - a;
                float den = d.x * e.y - d.y * e.x;
                if (Mathf.Abs(den) < 1e-12f) continue;
                Vector2 w = a - o;
                float t = (w.x * e.y - w.y * e.x) / den;      // along the ray
                float s = (w.x * d.y - w.y * d.x) / den;      // along the edge
                if (t > 1e-6f && s >= 0f && s <= 1f && t < best) best = t;
            }
            return best == float.MaxValue ? 0f : best;
        }

        /// <summary>
        /// One outline painted as the JS <c>celFill</c>: a dark seam just outside it, the shadow tone over the whole shape, the lit
        /// tone where the shadow strip (<paramref name="sdPx"/> deep, on the <see cref="ShadeDir"/> side) is not, and the base tone
        /// inside a lit edge <paramref name="hdPx"/> deep on the light side. Depths in reference px; <paramref name="seamPx"/> 0 for no seam.
        /// </summary>
        public static List<(Vector2[] pts, Color col)> Cel(Vector2[] shape, Tone t, float sdPx, float hdPx, float seamPx = 1.6f)
        {
            float u = 1f / KageModel.RefScale;
            var r = new List<(Vector2[] pts, Color col)>();
            if (seamPx > 0f) r.Add((MeshKit.Grow(shape, seamPx * u), Seam));
            r.Add((shape, t.shade));
            if (hdPx > 0f)
            {
                r.Add((Inset(shape, ShadeDir, sdPx * u, 0f), t.lit));
                r.Add((Inset(shape, ShadeDir, sdPx * u, hdPx * u), t.mid));
            }
            else r.Add((Inset(shape, ShadeDir, sdPx * u, 0f), t.mid));
            return r;
        }

        /// <summary>
        /// The JS <c>animeFold</c>: a crease along a polyline (x, y pairs in reference px): a lit line a little towards the light, the
        /// dark crease over it.
        /// </summary>
        public static IEnumerable<(Vector2[] pts, Color col)> Fold(Color lit, float widthPx, params float[] xy)
        {
            var shifted = new float[xy.Length];
            Vector2 off = -ShadeDir * 2.4f;   // the shadow direction in reference px (bind space mirrors x and flips y); the lit line goes the other way
            for (int i = 0; i + 1 < xy.Length; i += 2) { shifted[i] = xy[i] - off.x; shifted[i + 1] = xy[i + 1] - off.y; }
            yield return (Band(widthPx * 0.75f, shifted), lit);
            yield return (Band(widthPx, xy), Seam);
        }

        // ------------------------------------------------------------------------------------ adding cel parts

        /// <summary>
        /// Adds one cel-shaded part (see <see cref="Cel"/>) on a bone, with any <paramref name="detail"/> polygons painted over it
        /// (folds, cracks, wraps: keep them inside the outline). With <paramref name="rim"/> the outline also gets the rig's
        /// back-light rim, held towards the back-top in world space like every flat part's.
        /// </summary>
        public static PartDef AddCel(RigDef d, string name, B bone, int slot, Vector2[] shape, Tone t, float sdPx, float hdPx, int sub = 0,
            bool rim = true, IEnumerable<(Vector2[] pts, Color col)> detail = null, PartRole role = PartRole.Body, bool swordLayer = false,
            float seamPx = 1.6f, Color? rimColor = null)
        {
            var polys = Cel(shape, t, sdPx, hdPx, seamPx);
            if (detail != null) polys.AddRange(detail);
            var p = new PartDef
            {
                name = name, bone = bone, order = slot * 4 + sub, role = role, polys = polys, rim = false, swordLayer = swordLayer,
                rimPts = rim ? shape : null, rimColor = rimColor,
            };
            d.parts.Add(p);
            return p;
        }

        /// <summary>
        /// Adds a cel-shaded piece of cloth that moves: the mesh is bound to spring <paramref name="chainsPx"/> (each an x, y polyline in
        /// reference px, root first, the root pinned to the bone; <see cref="SoftChains"/>), so a robe panel, a mantle's hanging
        /// points or a loose bandage swing, trail and settle with the body. The rim is painted into the mesh so it bends with it.
        /// </summary>
        public static PartDef AddCloth(RigDef d, string name, B bone, int slot, Vector2[] shape, Tone t, float sdPx, float hdPx,
            ChainFeel feel, float[][] chainsPx, int sub = 0, bool rim = true, IEnumerable<(Vector2[] pts, Color col)> detail = null,
            bool swordLayer = false, float seamPx = 1.6f)
        {
            var polys = new List<(Vector2[] pts, Color col)>();
            if (rim) polys.Add((ShiftBy(MeshKit.Grow(shape, 0.0045f), new Vector2(-0.0055f, 0.0055f)), d.rimColor));
            polys.AddRange(Cel(shape, t, sdPx, hdPx, seamPx));
            if (detail != null) polys.AddRange(detail);
            var chains = new List<Vector2[]>();
            foreach (var c in chainsPx) chains.Add(V(c));
            var p = new PartDef
            {
                name = name, bone = bone, order = slot * 4 + sub, role = PartRole.Body, polys = polys, rim = false, swordLayer = swordLayer,
                soft = SoftKind.Chain, chains = chains, chainFeel = feel,
            };
            d.parts.Add(p);
            return p;
        }

        /// <summary>
        /// Hangs a part (a cel plate from <see cref="AddCel"/>, or a flat one) from its own pivot at (<paramref name="pivotX"/>,
        /// <paramref name="pivotY"/>) reference px, swung by the fighter's spring motion like <see cref="AddSwinging"/>: a kusazuri plate
        /// from the belt. <paramref name="swing"/> is how much (0.3 a little, 1 a lot). The part and its rim swing together.
        /// </summary>
        public static PartDef Swing(PartDef p, float pivotX, float pivotY, float swing)
        {
            p.jigglePivot = P(pivotX, pivotY);
            p.jiggle = swing;
            return p;
        }

        static Vector2[] ShiftBy(Vector2[] pts, Vector2 by)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < r.Length; i++) r[i] = pts[i] + by;
            return r;
        }
    }
}
