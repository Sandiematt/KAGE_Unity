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
        public float maxCordDeviation = 0.2f;

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
        public List<SvgPiece> Cut(IList<Vector2[]> region)
        {
            var result = new List<SvgPiece>();
            if (region == null || region.Count == 0) return result;
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
                MaxTanAngleDeviation = 0.1f,
                SamplingStepSize = 0.02f,
            };
            var regionShape = new Shape { Contours = clipContours, Fill = new SolidFill { Color = Color.white, Mode = FillMode.OddEven } };
            if (region.Count > 1)
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
                var layer = _layers[li];
                // a contour that misses the region adds no winding inside it (even-odd), so it can be left out
                var keep = new List<BezierContour>();
                for (int i = 0; i < layer.contours.Length; i++)
                    if (layer.bounds[i].Overlaps(box)) keep.Add(layer.contours[i]);
                if (keep.Count == 0) continue;

                var shape = new Shape { Contours = keep.ToArray(), Fill = layer.shape.Fill };
                var node = new SceneNode { Shapes = new List<Shape> { shape }, Clipper = clipper };
                var geoms = VectorUtils.TessellateScene(new Scene { Root = node }, opts);
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
            return result;
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
