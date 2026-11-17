using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>A slanted, brush-ended bar (health / shadow meter) drawn as a custom UI mesh.</summary>
    public class InkBar : Graphic
    {
        [Range(0, 1)] public float fill = 1f;
        public float skew = 14f;
        public bool rightToLeft;
        public Color colorA = Color.white, colorB = Color.white;
        public int notches;

        public void Set(float f)
        {
            f = Mathf.Clamp01(f);
            if (Mathf.Abs(f - fill) < 0.0005f) return;
            fill = f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (fill <= 0.0001f) return;
            float w = r.width * fill;
            float x0 = rightToLeft ? r.xMax - w : r.xMin;
            float x1 = rightToLeft ? r.xMax : r.xMin + w;
            float s = skew;
            Color ca = colorA * color, cb = colorB * color;
            Color left = rightToLeft ? Color.Lerp(cb, ca, 1f - fill) : ca;
            Color right = rightToLeft ? cb : Color.Lerp(ca, cb, fill);
            if (rightToLeft) { left = ca; right = cb; }
            vh.AddVert(new Vector3(x0, r.yMin), left, Vector2.zero);
            vh.AddVert(new Vector3(x0 + s, r.yMax), left, Vector2.zero);
            vh.AddVert(new Vector3(x1 + s, r.yMax), right, Vector2.zero);
            vh.AddVert(new Vector3(x1, r.yMin), right, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
            // brush highlight along the top edge
            Color hi = new Color(1f, 1f, 1f, 0.18f * color.a);
            int k = vh.currentVertCount;
            float hy = r.yMax - r.height * 0.28f;
            float hs = s * 0.72f;
            vh.AddVert(new Vector3(x0 + hs, hy), hi, Vector2.zero);
            vh.AddVert(new Vector3(x0 + s, r.yMax), hi, Vector2.zero);
            vh.AddVert(new Vector3(x1 + s, r.yMax), hi, Vector2.zero);
            vh.AddVert(new Vector3(x1 + hs, hy), hi, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2);
            vh.AddTriangle(k, k + 2, k + 3);
        }
    }
}
