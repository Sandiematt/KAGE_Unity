using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// A bar's fill for the JS-style HUD (SAN-108): a convex polygon (the track's clip-path, in px from the node's top-left, y down)
    /// cut at a fraction of its width, from the left or from the right, with a vertical colour gradient. The cut is a straight vertical
    /// line, so a slanted track end stays slanted when the bar is full and the fill meets it square when it is not (as CSS clip-path does).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class HudBar : MaskableGraphic
    {
        Vector2[] _shape = new Vector2[0];
        float _width = 1f, _fill = 1f;
        bool _fromRight;
        Color _top = Color.white, _bottom = Color.white;

        /// <summary>The polygon and the width of the whole track (the polygon's extent in x).</summary>
        public void SetShape(Vector2[] shape, float trackWidth, bool fromRight)
        {
            _shape = shape; _width = Mathf.Max(0.01f, trackWidth); _fromRight = fromRight;
            SetVerticesDirty();
        }

        public void SetColors(Color top, Color bottom)
        {
            if (top == _top && bottom == _bottom) return;
            _top = top; _bottom = bottom;
            SetVerticesDirty();
        }

        public float Fill => _fill;

        public void SetFill(float f)
        {
            f = Mathf.Clamp01(f);
            if (Mathf.Abs(f - _fill) < 0.0004f) return;
            _fill = f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_shape == null || _shape.Length < 3 || _fill <= 0.0005f) return;
            float cut = _fill * _width;
            var poly = new List<Vector2>(_shape);
            poly = Clip(poly, _fromRight ? _width - cut : cut, !_fromRight);
            if (poly.Count < 3) return;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in poly) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
            float span = Mathf.Max(0.001f, hi - lo);
            for (int i = 0; i < poly.Count; i++)
            {
                float t = (poly[i].y - lo) / span;
                vh.AddVert(new Vector3(poly[i].x, -poly[i].y, 0f), Color.Lerp(_top, _bottom, t) * color, Vector2.zero);
            }
            for (int i = 1; i + 1 < poly.Count; i++) vh.AddTriangle(0, i, i + 1);
        }

        /// <summary>Keeps the part of a convex polygon left of x (<paramref name="keepLeft"/>) or right of it.</summary>
        static List<Vector2> Clip(List<Vector2> poly, float x, bool keepLeft)
        {
            var res = new List<Vector2>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                bool ia = keepLeft ? a.x <= x : a.x >= x, ib = keepLeft ? b.x <= x : b.x >= x;
                if (ia) res.Add(a);
                if (ia != ib)
                {
                    float t = (x - a.x) / (b.x - a.x);
                    res.Add(new Vector2(x, Mathf.Lerp(a.y, b.y, t)));
                }
            }
            return res;
        }
    }
}
