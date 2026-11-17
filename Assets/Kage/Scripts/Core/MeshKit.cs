using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kage
{
    /// <summary>
    /// Procedural geometry: polygon triangulation (ear clipping), outline growth, rings, discs,
    /// and the shared unlit materials. Everything visible in KAGE is built from these meshes.
    /// </summary>
    public static class MeshKit
    {
        static Shader _shader;
        static Material _alpha, _additive, _softAdd, _softAlpha;
        static Texture2D _softDot;

        static int _linear = -1;
        /// <summary>Colours are authored in sRGB; vertex colours must be linear in a linear-space project.</summary>
        public static Color L(Color c)
        {
            if (_linear < 0) _linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0;
            return _linear == 1 ? c.linear : c;
        }

        public static Shader Shader
        {
            get
            {
                if (_shader == null) _shader = Shader.Find("Kage/Unlit");
                if (_shader == null) _shader = Shader.Find("Sprites/Default");
                return _shader;
            }
        }

        public static Material Alpha => _alpha != null ? _alpha : (_alpha = MakeMat(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, "Kage Alpha", null));
        public static Material Additive => _additive != null ? _additive : (_additive = MakeMat(BlendMode.SrcAlpha, BlendMode.One, "Kage Additive", null));
        public static Material SoftAdditive => _softAdd != null ? _softAdd : (_softAdd = MakeMat(BlendMode.SrcAlpha, BlendMode.One, "Kage Soft Additive", SoftDot));
        public static Material SoftAlpha => _softAlpha != null ? _softAlpha : (_softAlpha = MakeMat(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, "Kage Soft Alpha", SoftDot));

        public static Material MakeMat(BlendMode src, BlendMode dst, string name, Texture tex)
        {
            var m = new Material(Shader) { name = name, renderQueue = 3000 };
            if (m.HasProperty("_SrcBlend"))
            {
                m.SetFloat("_SrcBlend", (float)src);
                m.SetFloat("_DstBlend", (float)dst);
            }
            if (tex != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            return m;
        }

        /// <summary>Radial soft dot used by particles, glows and fog.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                const int n = 64;
                _softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Kage SoftDot", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                _softDot.SetPixels32(px);
                _softDot.Apply();
                return _softDot;
            }
        }

        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += (p[j].x * p[i].y - p[i].x * p[j].y);
            return a * 0.5f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Ear-clipping triangulation for simple (possibly concave) polygons.</summary>
        public static List<int> Triangulate(IList<Vector2> pts)
        {
            var tris = new List<int>();
            int n = pts.Count;
            if (n < 3) return tris;
            var idx = new List<int>(n);
            bool ccw = SignedArea(pts) > 0f;
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 4000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ia], b = pts[ib], c = pts[ic];
                    if (Cross(b - a, c - b) <= 1e-9f) continue;
                    bool inside = false;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int p = idx[j];
                        if (p == ia || p == ib || p == ic) continue;
                        if (InTri(pts[p], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            for (int i = 1; i < idx.Count - 1; i++) { tris.Add(idx[0]); tris.Add(idx[i]); tris.Add(idx[i + 1]); }
            return tris;
        }

        /// <summary>Offsets a polygon outward by d along mitred vertex normals (used for rim/outline meshes).</summary>
        public static Vector2[] Grow(IList<Vector2> pts, float d)
        {
            int n = pts.Count;
            var res = new Vector2[n];
            float sign = SignedArea(pts) > 0f ? 1f : -1f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = pts[(i + n - 1) % n], p1 = pts[i], p2 = pts[(i + 1) % n];
                Vector2 e0 = (p1 - p0).normalized, e1 = (p2 - p1).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x) * sign, n1 = new Vector2(e1.y, -e1.x) * sign;
                Vector2 m = n0 + n1;
                float ml = m.magnitude;
                m = ml < 1e-5f ? n0 : m / ml;
                float cos = Mathf.Max(0.35f, Vector2.Dot(m, n0));
                res[i] = p1 + m * (d / cos);
            }
            return res;
        }

        public static Mesh Poly(IList<Vector2> pts, Color col, string name = "poly")
        {
            var m = new Mesh { name = name };
            var v = new Vector3[pts.Count];
            var c = new Color[pts.Count];
            Color lc = L(col);
            for (int i = 0; i < pts.Count; i++) { v[i] = pts[i]; c[i] = lc; }
            m.vertices = v;
            m.colors = c;
            m.SetTriangles(Triangulate(pts), 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Several polygons painted in list order into a single mesh (one draw call, stable overlap).</summary>
        public static Mesh PolyMulti(IList<(Vector2[] pts, Color col)> polys, Vector2 offset, string name = "multi")
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            foreach (var (pts, col) in polys)
            {
                if (pts == null || pts.Length < 3) continue;
                int b = v.Count;
                Color lc = L(col);
                for (int i = 0; i < pts.Length; i++) { v.Add(pts[i] - offset); c.Add(lc); }
                var tri = Triangulate(pts);
                for (int i = 0; i < tri.Count; i++) t.Add(b + tri[i]);
            }
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Polygon with a vertical colour gradient (bottom → top) in mesh space.</summary>
        public static Mesh PolyGradient(IList<Vector2> pts, Color bottom, Color top, string name = "polyGrad")
        {
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var p in pts) { y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
            var m = new Mesh { name = name };
            var v = new Vector3[pts.Count];
            var c = new Color[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                v[i] = pts[i];
                c[i] = Color.Lerp(L(bottom), L(top), Mathf.InverseLerp(y0, y1, pts[i].y));
            }
            m.vertices = v;
            m.colors = c;
            m.SetTriangles(Triangulate(pts), 0);
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Ring(Vector2 c, float rOuter, float rInner, Color col, int seg = 28, float a0 = 0f, float a1 = 360f)
        {
            col = L(col);
            var m = new Mesh { name = "ring" };
            var v = new List<Vector3>();
            var cs = new List<Color>();
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                v.Add(c + d * rOuter); v.Add(c + d * rInner);
                cs.Add(col); cs.Add(col);
                if (i < seg)
                {
                    int k = i * 2;
                    t.Add(k); t.Add(k + 1); t.Add(k + 2);
                    t.Add(k + 1); t.Add(k + 3); t.Add(k + 2);
                }
            }
            m.SetVertices(v); m.SetColors(cs); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Disc(Vector2 c, float r, Color inner, Color outer, int seg = 32, float sx = 1f, float sy = 1f)
        {
            inner = L(inner); outer = L(outer);
            var m = new Mesh { name = "disc" };
            var v = new List<Vector3> { c };
            var cs = new List<Color> { inner };
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(c + new Vector2(Mathf.Cos(a) * r * sx, Mathf.Sin(a) * r * sy));
                cs.Add(outer);
                if (i < seg) { t.Add(0); t.Add(i + 1); t.Add(i + 2); }
            }
            m.SetVertices(v); m.SetColors(cs); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Quad(float w, float h, Color col, bool uv = true)
        {
            col = L(col);
            var m = new Mesh { name = "quad" };
            m.vertices = new Vector3[] { new(-w / 2, -h / 2), new(w / 2, -h / 2), new(w / 2, h / 2), new(-w / 2, h / 2) };
            m.colors = new[] { col, col, col, col };
            if (uv) m.uv = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A star burst used for contact flashes.</summary>
        public static Mesh Star(int points, float rOuter, float rInner, Color core, Color tip)
        {
            core = L(core); tip = L(tip);
            var m = new Mesh { name = "star" };
            var v = new List<Vector3> { Vector3.zero };
            var cs = new List<Color> { core };
            var t = new List<int>();
            int n = points * 2;
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float r = (i % 2 == 0) ? rOuter : rInner;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                cs.Add(i % 2 == 0 ? tip : Color.Lerp(core, tip, 0.5f));
                if (i < n) { t.Add(0); t.Add(i + 1); t.Add(i + 2); }
            }
            m.SetVertices(v); m.SetColors(cs); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public static MeshRenderer MakeRenderer(string name, Transform parent, Mesh mesh, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        public static void SetColor(Mesh m, Color c)
        {
            var cs = new Color[m.vertexCount];
            c = L(c);
            for (int i = 0; i < cs.Length; i++) cs[i] = c;
            m.colors = cs;
        }
    }

    /// <summary>
    /// A dynamic triangle-strip ribbon rebuilt every frame (scarf, weapon trails, limb smears).
    /// </summary>
    public class Ribbon
    {
        public readonly Mesh mesh;
        public readonly MeshRenderer renderer;
        readonly List<Vector3> _v = new();
        readonly List<Color> _c = new();
        readonly List<Vector2> _uv = new();
        readonly List<int> _t = new();
        int _stripStart;

        public Ribbon(string name, Transform parent, Material mat, int order)
        {
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            renderer = MeshKit.MakeRenderer(name, parent, mesh, mat, order);
        }

        public void Begin() { _v.Clear(); _c.Clear(); _uv.Clear(); _t.Clear(); _stripStart = 0; }

        /// <summary>Add a cross-section (left and right edge). Consecutive sections are joined.</summary>
        public void Add(Vector3 left, Vector3 right, Color cl, Color cr, float u = 0f)
        {
            int k = _v.Count;
            _v.Add(left); _v.Add(right);
            _c.Add(MeshKit.L(cl)); _c.Add(MeshKit.L(cr));
            _uv.Add(new Vector2(u, 0f)); _uv.Add(new Vector2(u, 1f));
            if (k - 2 >= _stripStart)
            {
                _t.Add(k - 2); _t.Add(k - 1); _t.Add(k);
                _t.Add(k - 1); _t.Add(k + 1); _t.Add(k);
            }
        }

        /// <summary>Break the strip so the next Add starts a new, unconnected strip.</summary>
        public void Break() { _stripStart = _v.Count; }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            int k = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c);
            _c.Add(MeshKit.L(ca)); _c.Add(MeshKit.L(cb)); _c.Add(MeshKit.L(cc));
            _uv.Add(Vector2.zero); _uv.Add(Vector2.zero); _uv.Add(Vector2.zero);
            _t.Add(k); _t.Add(k + 1); _t.Add(k + 2);
            _stripStart = _v.Count;
        }

        public void End()
        {
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetColors(_c);
            mesh.SetUVs(0, _uv);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();
        }

        public int Count => _v.Count;
    }
}
