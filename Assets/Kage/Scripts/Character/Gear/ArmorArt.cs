using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The drawing kit for armour visuals (SAN-85). Shapes are authored in the reference image's pixels (the same numbers
    /// as <see cref="KageModel"/>: x right, y down, Kage facing left) so they can be read straight against his body.
    /// Pieces that dress a bone (chest plates, shoulder guards, a halo) become parts of that bone; pieces that belong to
    /// the coat (hem teeth, hem art, kusazuri) are appended to the coat's own cloth meshes so they ride the cloth.
    /// Every filled piece can carry the thin back-top rim light like the rest of Kage's body.
    /// </summary>
    sealed class ArmorArt
    {
        const float RimGrow = 0.0045f;
        static readonly Vector2 Light = new(-0.0055f, 0.0055f);

        readonly RigDef _d;
        readonly string _look;
        readonly Dictionary<(B bone, int order, float glow), List<(Vector2[] pts, Color col)>> _layers = new();
        readonly List<(B bone, int order, float glow)> _keys = new();

        public ArmorArt(RigDef d, string look) { _d = d; _look = look; }

        public RigDef Rig => _d;
        public Color RimColor => _d.rimColor;

        public static Vector2 P(float x, float y) => KageModel.P(x, y);

        public static Vector2 P(Vector2 px) => KageModel.P(px.x, px.y);

        // ---------------------------------------------------------------- shape helpers (reference pixels)

        public static List<Vector2> Pts(params float[] xy)
        {
            var o = new List<Vector2>(xy.Length / 2);
            for (int i = 0; i + 1 < xy.Length; i += 2) o.Add(new Vector2(xy[i], xy[i + 1]));
            return o;
        }

        /// <summary>A strip along an open polyline whose width runs from <paramref name="w0"/> to <paramref name="w1"/> (pixels).</summary>
        public static List<Vector2> Strip(IList<Vector2> path, float w0, float w1)
        {
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 t = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector2 n = new(-t.y, t.x);
                float w = Mathf.Lerp(w0, w1, path.Count > 1 ? i / (float)(path.Count - 1) : 0f) * 0.5f;
                left.Add(path[i] + n * w);
                right.Add(path[i] - n * w);
            }
            right.Reverse();
            left.AddRange(right);
            return left;
        }

        public static List<Vector2> Strip(float width, params float[] xy) => Strip(Pts(xy), width, width);

        public static List<Vector2> Circle(float cx, float cy, float r, int n = 12)
        {
            var o = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                o.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return o;
        }

        public static List<Vector2> Quad(float x0, float y0, float cx, float cy, float x1, float y1, int n = 8)
        {
            var o = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n;
                o.Add(new Vector2((1 - a) * (1 - a) * x0 + 2 * (1 - a) * a * cx + a * a * x1, (1 - a) * (1 - a) * y0 + 2 * (1 - a) * a * cy + a * a * y1));
            }
            return o;
        }

        // ---------------------------------------------------------------- bone parts

        List<(Vector2[] pts, Color col)> L(B bone, int order, float glow)
        {
            var key = (bone, order, glow);
            if (!_layers.TryGetValue(key, out var list))
            {
                list = new List<(Vector2[], Color)>();
                _layers[key] = list;
                _keys.Add(key);
            }
            return list;
        }

        static Vector2[] ToBind(IList<Vector2> px)
        {
            var r = new List<Vector2>(px.Count);
            foreach (var q in px)
            {
                var b = P(q);
                if (r.Count > 0 && (r[r.Count - 1] - b).sqrMagnitude < 1e-10f) continue;
                r.Add(b);
            }
            if (r.Count > 1 && (r[0] - r[r.Count - 1]).sqrMagnitude < 1e-10f) r.RemoveAt(r.Count - 1);
            return r.ToArray();
        }

        /// <summary>A filled polygon of reference pixels on a bone, at a draw order (the same scale as <see cref="PartDef.order"/>).</summary>
        public void Fill(B bone, int order, Color col, bool rim, IList<Vector2> px, float glow = 1f)
        {
            var pts = ToBind(px);
            if (pts.Length < 3) return;
            var list = L(bone, order, glow);
            if (rim)
            {
                var g = MeshKit.Grow(pts, RimGrow);
                for (int i = 0; i < g.Length; i++) g[i] += Light;
                list.Add((g, _d.rimColor));
            }
            list.Add((pts, col));
        }

        public void Fill(B bone, int order, Color col, bool rim, params float[] xy) => Fill(bone, order, col, rim, Pts(xy));

        public void Line(B bone, int order, Color col, float width, IList<Vector2> path, float glow = 1f)
        {
            if (path.Count < 2) return;
            var pts = ToBind(Strip(path, width, width));
            if (pts.Length >= 3) L(bone, order, glow).Add((pts, col));
        }

        public void Line(B bone, int order, Color col, float width, params float[] xy) => Line(bone, order, col, width, Pts(xy));

        public void Disc(B bone, int order, Color col, float cx, float cy, float r, float glow = 1f)
        {
            var pts = ToBind(Circle(cx, cy, r, 10));
            if (pts.Length >= 3) L(bone, order, glow).Add((pts, col));
        }

        // ---------------------------------------------------------------- the coat's own cloth

        /// <summary>Subdivides a closed polygon so no edge is longer than <paramref name="maxLen"/> units: cloth bends smoothly.</summary>
        public static Vector2[] Dense(Vector2[] pts, float maxLen = 0.03f)
        {
            var r = new List<Vector2>();
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / maxLen));
                for (int k = 0; k < n; k++) r.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            return r.ToArray();
        }

        PartDef Coat(string part) => _d.parts.Find(p => p.name == part);

        /// <summary>
        /// Adds a polygon to one of the coat's cloth meshes (<c>Coat</c>, <c>Coat_Side</c>, <c>Coat_Back</c>). Pieces under the
        /// cloth (<paramref name="atFront"/>, the hem teeth) go first so the coat's own rim and fill overlap their roots.
        /// </summary>
        public void CoatFill(string part, Color col, bool rim, IList<Vector2> px, bool atFront = false)
        {
            var p = Coat(part);
            var pts = ToBind(px);
            if (p == null || pts.Length < 3) return;
            var add = new List<(Vector2[] pts, Color col)>();
            if (rim)
            {
                var g = MeshKit.Grow(pts, RimGrow);
                for (int i = 0; i < g.Length; i++) g[i] += Light;
                add.Add((Dense(g), _d.rimColor));
            }
            add.Add((Dense(pts), col));
            if (atFront) p.polys.InsertRange(0, add); else p.polys.AddRange(add);
        }

        public void CoatLine(string part, Color col, float width, IList<Vector2> path, bool atFront = false)
        {
            if (path.Count < 2) return;
            var p = Coat(part);
            var pts = ToBind(Strip(path, width, width));
            if (p == null || pts.Length < 3) return;
            var item = (Dense(pts), col);
            if (atFront) p.polys.Insert(0, item); else p.polys.Add(item);
        }

        public void CoatLine(string part, Color col, float width, params float[] xy) => CoatLine(part, col, width, Pts(xy));

        // ---------------------------------------------------------------- done

        /// <summary>Adds the bone layers to the rig as parts named <c>Armor_&lt;look&gt;_&lt;bone&gt;_&lt;order&gt;</c>. The coat pieces are already in the coat.</summary>
        public int Commit()
        {
            int n = 0;
            foreach (var key in _keys)
            {
                var polys = _layers[key];
                if (polys.Count == 0) continue;
                _d.parts.Add(new PartDef
                {
                    name = $"Armor_{_look}_{key.bone}_{key.order}", bone = key.bone, order = key.order, role = PartRole.Trim,
                    polys = polys, soft = SoftKind.None, rim = false, glow = key.glow,
                    swordLayer = false,
                });
                n++;
            }
            return n;
        }
    }
}
