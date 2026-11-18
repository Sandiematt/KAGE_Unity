using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The drawing kit for helmet visuals (SAN-72). Shapes are authored in the JS head frame: head radii, +u towards the
    /// face, +v down, origin at the head centre, so the numbers can be read straight off the JS helmet drawings
    /// (<c>21-characters/kage/gear</c> and the sub-boss heads). A shape string is the JS point list
    /// (<c>"[1.08, -0.34, 1], [0.86, -0.92]"</c>): a point flagged <c>1</c> is a sharp corner, any other is a control point
    /// of a smooth curve, exactly like the JS <c>orderShape</c>.
    /// Everything lands in a few layers between Kage's hair and his eye; one layer is one mesh of coloured polygons painted
    /// in order, each with a thin back-top rim like the coat's. Cloth tails are separate parts on a spring pivot.
    /// </summary>
    sealed class HelmetArt
    {
        /// <summary>Head centre and radius in the reference image's pixels (Kage's skull and face fit this circle).</summary>
        public const float R = 57f, CX = 862f, CY = 170f;

        /// <summary>First draw order of a helmet layer: just over the hair (21 * 4) and under the eye (24 * 4).</summary>
        public const int FirstOrder = 85;
        public const int Layers = 11;

        const float RimGrow = 0.0042f;
        static readonly Vector2 Light = new(-0.0055f, 0.0055f);

        /// <summary>Head-frame point to the rig's bind space.</summary>
        public static Vector2 At(float u, float v) => new((KageModel.OriginXRef - CX + u * R) / KageModel.RefScale, (KageModel.OriginYRef - CY - v * R) / KageModel.RefScale);

        /// <summary>Head radii to world units.</summary>
        public static float Len(float r) => r * R / KageModel.RefScale;

        readonly RigDef _d;
        readonly List<(Vector2[] pts, Color col)>[] _layers = new List<(Vector2[], Color)>[Layers];
        readonly List<PartDef> _tails = new();

        public HelmetArt(RigDef d) { _d = d; }

        public Color RimColor => _d.rimColor;

        // ---------------------------------------------------------------- shapes

        static readonly Regex Pt = new(@"\[\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*(?:,\s*(\d)\s*)?\]", RegexOptions.Compiled);
        static readonly Dictionary<string, (float u, float v, bool sharp)[]> Parsed = new();

        static (float u, float v, bool sharp)[] Parse(string s)
        {
            if (Parsed.TryGetValue(s, out var r)) return r;
            var list = new List<(float, float, bool)>();
            foreach (Match m in Pt.Matches(s))
                list.Add((float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), m.Groups[3].Success && m.Groups[3].Value == "1"));
            return Parsed[s] = list.ToArray();
        }

        /// <summary>The JS <c>orderShape</c>: sharp points are corners, the others control points of quadratic curves through the midpoints.</summary>
        public static List<Vector2> Outline(string shape, float k = 1f)
        {
            var p = Parse(shape);
            int n = p.Length;
            Vector2 P(int i) { var q = p[((i % n) + n) % n]; return new Vector2(q.u * k, q.v * k); }
            bool S(int i) => p[((i % n) + n) % n].sharp;
            var o = new List<Vector2>();
            Vector2 cur = S(0) ? P(0) : (P(-1) + P(0)) / 2f;
            o.Add(cur);
            for (int i = S(0) ? 1 : 0; i < n; i++)
            {
                if (S(i)) { cur = P(i); o.Add(cur); continue; }
                Vector2 c = P(i), e = (P(i) + P(i + 1)) / 2f;
                for (int t = 1; t <= 6; t++)
                {
                    float a = t / 6f;
                    o.Add((1 - a) * (1 - a) * cur + 2 * (1 - a) * a * c + a * a * e);
                }
                cur = e;
            }
            return o;
        }

        static Vector2[] ToBind(List<Vector2> uv, Vector2? shift = null)
        {
            var r = new List<Vector2>(uv.Count);
            foreach (var q in uv)
            {
                var b = At(q.x, q.y);
                if (r.Count > 0 && (r[r.Count - 1] - b).sqrMagnitude < 1e-10f) continue;
                r.Add(b);
            }
            if (r.Count > 1 && (r[0] - r[r.Count - 1]).sqrMagnitude < 1e-10f) r.RemoveAt(r.Count - 1);
            return r.ToArray();
        }

        public static List<Vector2> QuadPath(float x0, float y0, float cx, float cy, float x1, float y1, int n = 12)
        {
            var o = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n;
                o.Add(new Vector2((1 - a) * (1 - a) * x0 + 2 * (1 - a) * a * cx + a * a * x1, (1 - a) * (1 - a) * y0 + 2 * (1 - a) * a * cy + a * a * y1));
            }
            return o;
        }

        /// <summary>A canvas arc: centre, radius (head radii), angles in radians (y down, increasing angle = clockwise on screen).</summary>
        public static List<Vector2> ArcPath(float cx, float cy, float r, float a0, float a1, int n = 18)
        {
            var o = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n);
                o.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return o;
        }

        // ---------------------------------------------------------------- painting

        List<(Vector2[] pts, Color col)> L(int layer) => _layers[layer] ??= new List<(Vector2[], Color)>();

        /// <summary>A filled shape (a JS shape string), with the back-top rim unless <paramref name="rim"/> is false.</summary>
        public void Fill(int layer, string shape, Color col, bool rim = true, float k = 1f) => Fill(layer, Outline(shape, k), col, rim);

        public void Fill(int layer, List<Vector2> uvOutline, Color col, bool rim = true)
        {
            var pts = ToBind(uvOutline);
            if (pts.Length < 3) return;
            var list = L(layer);
            if (rim)
            {
                var g = MeshKit.Grow(pts, RimGrow);
                for (int i = 0; i < g.Length; i++) g[i] += Light;
                list.Add((g, _d.rimColor));
            }
            list.Add((pts, col));
        }

        /// <summary>A stroke of constant width (head radii) along a head-frame polyline.</summary>
        public void Stroke(int layer, Color col, float width, List<Vector2> path)
        {
            if (path.Count < 2) return;
            float h = Len(width) * 0.5f;
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            var b = new List<Vector2>();
            foreach (var q in path) b.Add(At(q.x, q.y));
            for (int i = 0; i < b.Count; i++)
            {
                Vector2 t = (b[Mathf.Min(b.Count - 1, i + 1)] - b[Mathf.Max(0, i - 1)]).normalized;
                Vector2 nrm = new(-t.y, t.x);
                left.Add(b[i] + nrm * h);
                right.Add(b[i] - nrm * h);
            }
            right.Reverse();
            left.AddRange(right);
            L(layer).Add((left.ToArray(), col));
        }

        public void Stroke(int layer, Color col, float width, params float[] uv)
        {
            var p = new List<Vector2>();
            for (int i = 0; i + 1 < uv.Length; i += 2) p.Add(new Vector2(uv[i], uv[i + 1]));
            Stroke(layer, col, width, p);
        }

        /// <summary>A dashed stroke: every other stretch of the path (the laced edges of a shikoro).</summary>
        public void Dashed(int layer, Color col, float width, List<Vector2> path)
        {
            for (int i = 0; i + 1 < path.Count; i += 2) Stroke(layer, col, width, new List<Vector2> { path[i], path[i + 1] });
        }

        public void Disc(int layer, Color col, float u, float v, float r)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                o.Add(new Vector2(u + Mathf.Cos(a) * r, v + Mathf.Sin(a) * r));
            }
            L(layer).Add((ToBind(o), col));
        }

        /// <summary>A tine or horn: a strip along a path whose width runs from <paramref name="w0"/> to <paramref name="w1"/> (head radii).</summary>
        public void Tine(int layer, Color col, float w0, float w1, bool rim, params float[] uv)
        {
            var path = new List<Vector2>();
            for (int i = 0; i + 1 < uv.Length; i += 2) path.Add(new Vector2(uv[i], uv[i + 1]));
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 p = path[i], q = path[Mathf.Min(path.Count - 1, i + 1)], o = path[Mathf.Max(0, i - 1)];
                Vector2 d = (q - o).normalized;
                float w = Mathf.Lerp(w0, w1, i / (float)(path.Count - 1)) * 0.5f;
                left.Add(p + new Vector2(-d.y, d.x) * w);
                right.Add(p - new Vector2(-d.y, d.x) * w);
            }
            right.Reverse();
            left.AddRange(right);
            Fill(layer, left, col, rim);
        }

        // ---------------------------------------------------------------- cloth tails

        /// <summary>
        /// A hanging cloth tail (hood drape, band tail, kabuto tassel) knotted at a head-frame point: a tapered strip that
        /// streams back and down, on a spring pivot so it lags and swings with the head and the body like Kage's own hair.
        /// </summary>
        public void Tail(string name, Color col, float rootU, float rootV, float length, float w0, float w1, float swing = 1.2f, float dirU = -0.84f, float dirV = 0.54f, Color? edge = null)
        {
            Vector2 root = At(rootU, rootV);
            var dir = new Vector2(dirU, dirV).normalized;
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float s = i / (float)(n - 1);
                // a slight droop: the cloth bends down along its length
                Vector2 c = new Vector2(rootU, rootV) + dir * (length * s) + new Vector2(0f, 0.16f * length * s * s);
                Vector2 step = (dir + new Vector2(0f, 0.32f * s)).normalized;
                float w = Mathf.Lerp(w0, w1, s) * 0.5f;
                left.Add(c + new Vector2(-step.y, step.x) * w);
                right.Add(c - new Vector2(-step.y, step.x) * w);
            }
            right.Reverse();
            left.AddRange(right);
            _tails.Add(new PartDef
            {
                name = name, bone = B.Head, order = FirstOrder, color = col, role = PartRole.Trim, pts = ToBind(left),
                rim = true, rimColor = edge, jigglePivot = root, jiggle = swing,
            });
        }

        // ---------------------------------------------------------------- done

        /// <summary>Adds the layers (and tails) to the rig as parts named <c>Helmet_&lt;look&gt;_n</c>.</summary>
        public int Commit(string look)
        {
            int n = 0;
            for (int i = 0; i < Layers; i++)
            {
                if (_layers[i] == null || _layers[i].Count == 0) continue;
                _d.parts.Add(new PartDef { name = $"Helmet_{look}_{i}", bone = B.Head, order = FirstOrder + i, role = PartRole.Trim, polys = _layers[i], soft = SoftKind.None, rim = false });
                n++;
            }
            foreach (var t in _tails) { t.name = $"Helmet_{look}_{t.name}"; _d.parts.Add(t); n++; }
            return n;
        }
    }
}
