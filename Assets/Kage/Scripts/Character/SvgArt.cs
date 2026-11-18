using System.Collections.Generic;
using System.IO;
using Unity.VectorGraphics;
using UnityEngine;

namespace Kage
{
    /// <summary>One colour of a cut: triangles in SVG pixels (x right, y down).</summary>
    public sealed class SvgPiece
    {
        public Color color;
        public int layer;              // index into <see cref="SvgArt.Layers"/> (paint order)
        public Vector2[] vertices;
        public int[] triangles;
    }

    /// <summary>
    /// A colour-trace SVG (SAN-71): one compound path per colour, painted in file order, as the owner's Raikon trace
    /// is. <see cref="Parse"/> reads it with Unity's vector graphics module; <see cref="Cut"/> clips every colour to a
    /// region polygon and tessellates what is inside, so a part set can cut the trace into rig parts by region. The
    /// trace's background is its first colour, so a region has to follow the figure's outline (see the regions of
    /// <see cref="RaikonPartSet"/>).
    /// </summary>
    public sealed class SvgArt
    {
        sealed class Layer
        {
            public Shape shape;
            public BezierContour[] contours;
            public Rect[] bounds;
        }

        readonly List<Layer> _layers = new();

        /// <summary>The canvas size in SVG pixels.</summary>
        public Vector2 Size { get; private set; }

        /// <summary>Each colour, in paint order.</summary>
        public List<Color> Layers { get; } = new();

        /// <summary>Curve flattening: the largest distance (SVG px) between a curve and its polyline.</summary>
        public float maxCordDeviation = 0.5f;

        /// <summary>
        /// The layer that is the trace's canvas (-1: none). In a cutout trace that colour is exactly where no other colour
        /// is, so a cut paints the whole region in it first and the other colours over it, instead of cutting the
        /// canvas's large, many-holed shape (same picture, far less work).
        /// </summary>
        public int backgroundLayer = -1;

        public static SvgArt Parse(string svg)
        {
            var info = SVGParser.ImportSVG(new StringReader(svg), ViewportOptions.DontPreserve, 0f, 1f, 0, 0);
            var art = new SvgArt { Size = info.SceneViewport.size };
            art.Collect(info.Scene.Root, Matrix2D.identity);
            return art;
        }

        void Collect(SceneNode node, Matrix2D parent)
        {
            if (node == null) return;
            var m = parent * node.Transform;
            if (node.Shapes != null)
            {
                foreach (var s in node.Shapes)
                {
                    if (s.Fill is not SolidFill fill || s.Contours == null) continue;
                    var contours = new BezierContour[s.Contours.Length];
                    var bounds = new Rect[contours.Length];
                    for (int i = 0; i < contours.Length; i++)
                    {
                        var src = s.Contours[i].Segments;
                        var seg = new BezierPathSegment[src.Length];
                        for (int k = 0; k < seg.Length; k++)
                            seg[k] = new BezierPathSegment { P0 = m.MultiplyPoint(src[k].P0), P1 = m.MultiplyPoint(src[k].P1), P2 = m.MultiplyPoint(src[k].P2) };
                        contours[i] = new BezierContour { Segments = seg, Closed = true };
                        bounds[i] = SegBounds(seg);
                    }
                    var shape = new Shape { Contours = contours, Fill = new SolidFill { Color = fill.Color, Opacity = 1f, Mode = fill.Mode } };
                    _layers.Add(new Layer { shape = shape, contours = contours, bounds = bounds });
                    Layers.Add(fill.Color);
                }
            }
            if (node.Children != null)
                foreach (var c in node.Children) Collect(c, m);
        }

        static Rect SegBounds(BezierPathSegment[] seg)
        {
            Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
            foreach (var s in seg)
            {
                lo = Vector2.Min(lo, Vector2.Min(s.P0, Vector2.Min(s.P1, s.P2)));
                hi = Vector2.Max(hi, Vector2.Max(s.P0, Vector2.Max(s.P1, s.P2)));
            }
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }

        /// <summary>
        /// Every colour inside <paramref name="region"/> (polygons in SVG pixels, combined even-odd, so a polygon inside
        /// another is a hole), as triangles in SVG pixels, in paint order.
        /// </summary>
        public List<SvgPiece> Cut(IList<Vector2[]> region, float cell = 0f)
        {
            var result = new List<SvgPiece>();
            if (region == null || region.Count == 0) return result;
            if (cell <= 0f) { CutOne(region, result); return result; }

            // cut cell by cell: the region's pieces then have no triangle bigger than a cell (soft parts bend smoothly)
            Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
            foreach (var poly in region) foreach (var p in poly) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
            var sub = new List<Vector2[]>();
            for (float y = Mathf.Floor(lo.y / cell) * cell; y < hi.y; y += cell)
                for (float x = Mathf.Floor(lo.x / cell) * cell; x < hi.x; x += cell)
                {
                    sub.Clear();
                    foreach (var poly in region)
                    {
                        var c = ClipRect(poly, x, y, x + cell, y + cell);
                        if (c.Length >= 3 && Mathf.Abs(Area(c)) > 0.01f) sub.Add(c);
                    }
                    if (sub.Count > 0) CutOne(sub, result);
                }
            return result;
        }

        void CutOne(IList<Vector2[]> region, List<SvgPiece> result)
        {
            var clipContours = new BezierContour[region.Count];
            Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
            for (int i = 0; i < region.Count; i++)
            {
                clipContours[i] = Polygon(region[i]);
                foreach (var p in region[i]) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
            }
            var box = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
            var opts = new VectorUtils.TessellationOptions
            {
                StepDistance = 10000f,
                MaxCordDeviation = maxCordDeviation,
                MaxTanAngleDeviation = 1.5f,          // a trace's curves are short: the cord deviation alone decides
                SamplingStepSize = 0.1f,
            };
            var regionShape = new Shape { Contours = clipContours, Fill = new SolidFill { Color = Color.white, Mode = FillMode.OddEven } };
            if (HasHole(region))
            {
                // the module's clipper unions its contours (a hole would be filled), so a region with holes clips by
                // its own even-odd triangulation: the union of those triangles is exactly the region
                var tris = new List<BezierContour>();
                foreach (var g in VectorUtils.TessellateScene(new Scene { Root = new SceneNode { Shapes = new List<Shape> { regionShape } } }, opts))
                    for (int i = 0; i + 2 < g.Indices.Length; i += 3)
                        tris.Add(Polygon(new[]
                        {
                            g.WorldTransform.MultiplyPoint(g.Vertices[g.Indices[i]]),
                            g.WorldTransform.MultiplyPoint(g.Vertices[g.Indices[i + 1]]),
                            g.WorldTransform.MultiplyPoint(g.Vertices[g.Indices[i + 2]]),
                        }));
                regionShape = new Shape { Contours = tris.ToArray(), Fill = new SolidFill { Color = Color.white } };
            }
            var clipper = new SceneNode { Shapes = new List<Shape> { regionShape } };

            for (int li = 0; li < _layers.Count; li++)
            {
                if (li == backgroundLayer)
                {
                    // the background colour is wherever no other colour is: paint the whole region in it, under the rest
                    var fill = new Shape { Contours = regionShape.Contours, Fill = new SolidFill { Color = Layers[li], Mode = ((SolidFill)regionShape.Fill).Mode } };
                    Emit(VectorUtils.TessellateScene(new Scene { Root = new SceneNode { Shapes = new List<Shape> { fill } } }, opts), li, result);
                    continue;
                }
                var layer = _layers[li];
                // a contour that misses the region adds no winding inside it (even-odd), so it can be left out
                var keep = new List<BezierContour>();
                for (int i = 0; i < layer.contours.Length; i++)
                    if (layer.bounds[i].Overlaps(box)) keep.Add(layer.contours[i]);
                if (keep.Count == 0) continue;

                var shape = new Shape { Contours = keep.ToArray(), Fill = layer.shape.Fill };
                var node = new SceneNode { Shapes = new List<Shape> { shape }, Clipper = clipper };
                Emit(VectorUtils.TessellateScene(new Scene { Root = node }, opts), li, result);
            }
        }

        /// <summary>Polygons (even-odd) filled with one colour, as triangles: for fills drawn in a trace's colours.</summary>
        public static SvgPiece Tessellate(IList<Vector2[]> polygons, Color color)
        {
            var contours = new BezierContour[polygons.Count];
            for (int i = 0; i < contours.Length; i++) contours[i] = Polygon(polygons[i]);
            var shape = new Shape { Contours = contours, Fill = new SolidFill { Color = color, Mode = FillMode.OddEven } };
            var opts = new VectorUtils.TessellationOptions { StepDistance = 10000f, MaxCordDeviation = 0.5f, MaxTanAngleDeviation = 1.5f, SamplingStepSize = 0.1f };
            var v = new List<Vector2>();
            var t = new List<int>();
            foreach (var g in VectorUtils.TessellateScene(new Scene { Root = new SceneNode { Shapes = new List<Shape> { shape } } }, opts))
            {
                int b = v.Count;
                foreach (var p in g.Vertices) v.Add(g.WorldTransform.MultiplyPoint(p));
                foreach (var i in g.Indices) t.Add(b + i);
            }
            return new SvgPiece { color = color, layer = -1, vertices = v.ToArray(), triangles = t.ToArray() };
        }

        void Emit(List<VectorUtils.Geometry> geoms, int li, List<SvgPiece> result)
        {
            foreach (var g in geoms)
            {
                if (g.Indices == null || g.Indices.Length < 3) continue;
                var v = new Vector2[g.Vertices.Length];
                for (int i = 0; i < v.Length; i++) v[i] = g.WorldTransform.MultiplyPoint(g.Vertices[i]);
                var t = new int[g.Indices.Length];
                for (int i = 0; i < t.Length; i++) t[i] = g.Indices[i];
                result.Add(new SvgPiece { color = Layers[li], layer = li, vertices = v, triangles = t });
            }
        }

        static float Area(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) a += p[j].x * p[i].y - p[i].x * p[j].y;
            return a * 0.5f;
        }

        /// <summary>A polygon clipped to an axis-aligned rectangle (Sutherland-Hodgman).</summary>
        static Vector2[] ClipRect(Vector2[] poly, float x0, float y0, float x1, float y1)
        {
            var a = new List<Vector2>(poly);
            var b = new List<Vector2>(poly.Length + 4);
            for (int edge = 0; edge < 4 && a.Count > 0; edge++)
            {
                b.Clear();
                for (int i = 0; i < a.Count; i++)
                {
                    Vector2 p = a[i], q = a[(i + 1) % a.Count];
                    float dp = Side(edge, p, x0, y0, x1, y1), dq = Side(edge, q, x0, y0, x1, y1);
                    if (dp >= 0f) b.Add(p);
                    if ((dp >= 0f) != (dq >= 0f)) b.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
                }
                (a, b) = (b, a);
            }
            return a.ToArray();
        }

        static float Side(int edge, Vector2 p, float x0, float y0, float x1, float y1) => edge switch
        {
            0 => p.x - x0, 1 => x1 - p.x, 2 => p.y - y0, _ => y1 - p.y,
        };

        /// <summary>True when one polygon lies inside another (a hole, under even-odd); separate islands are not.</summary>
        static bool HasHole(IList<Vector2[]> region)
        {
            for (int i = 0; i < region.Count; i++)
                for (int j = 0; j < region.Count; j++)
                    if (i != j && Inside(region[j], region[i][0])) return true;
            return false;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    c = !c;
            return c;
        }

        static BezierContour Polygon(Vector2[] pts)
        {
            var seg = new BezierPathSegment[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                seg[i] = new BezierPathSegment { P0 = a, P1 = a + (b - a) / 3f, P2 = a + (b - a) * (2f / 3f) };
            }
            return new BezierContour { Segments = seg, Closed = true };
        }
    }
}
