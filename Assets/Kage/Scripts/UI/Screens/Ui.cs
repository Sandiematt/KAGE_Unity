using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>The JS game's menu type (src/fonts, converted to TTF under Resources/UI/Fonts).</summary>
    public enum UiFont
    {
        /// <summary>Cinzel 700: titles, labels, numbers (JS --f-title).</summary>
        Title,
        /// <summary>Cinzel 800: pause and results headings.</summary>
        TitleHeavy,
        /// <summary>Shippori Mincho 700: big names, Latin and the UI's kanji (--f-mincho).</summary>
        Mincho,
        /// <summary>Barlow 400: body text (--f-body).</summary>
        Body,
        /// <summary>Barlow Condensed 600: key caps, small tracked labels (--f-ui).</summary>
        Cond,
        /// <summary>Yuji Syuku (kanji only): the brush kanji 影 対 戦 (--f-brush).</summary>
        Brush,
        /// <summary>Rajdhani 700: HUD numbers.</summary>
        Num,
        /// <summary>Teko 600: the fight HUD's names, titles and labels (JS --font-ui, SAN-108).</summary>
        Teko,
        /// <summary>Permanent Marker: the combo count (JS --font-brush, SAN-108).</summary>
        Marker,
        /// <summary>Yuji Boku kanji (subset): the rage icon (JS --font-kanji, SAN-108).</summary>
        Kanji,
    }

    /// <summary>
    /// The shared theme and building blocks of the JS-style screens (SAN-67): the JS menu palette (the Raikon crimson the whole UI
    /// wears, 71-theme.js), its fonts and images, and helpers that place things the way the JS CSS does. Every screen is a
    /// 1600 x 900 stage: positions are the JS design pixels, x to the right and y down from the top-left corner.
    /// </summary>
    public static class Ui
    {
        // ---- palette (wonder.css :root and #ui, Raikon) ----
        public static readonly Color Void = Hex("#0a0908"), Charcoal = Hex("#14120f");
        public static readonly Color Bone = Hex("#ece5d3"), Muted = Hex("#a09785");
        public static readonly Color Gold = Hex("#c9a45c"), GoldDim = Hex("#7d6638");
        public static readonly Color Acc = Hex("#c8343a"), Deep = Hex("#a3302a"), Hot = Hex("#e05a4f");
        public static readonly Color Sky1 = Hex("#2c0f0e"), Sky2 = Hex("#170b0a"), Sky3 = Hex("#0e0707");
        public static readonly Color Ground = Hex("#1e0d0c"), Panel = Hex("#0e0807");
        public static readonly Color Sun1 = Hex("#d8352b"), Sun2 = Hex("#6e1512");
        public static readonly Color InkOnGold = Hex("#14100a"), Ruby = Hex("#c8343a");
        public static readonly Color Hair = new Color(0.925f, 0.898f, 0.827f, 0.14f), GoldHair = new Color(0.788f, 0.643f, 0.361f, 0.42f);

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        public static Color A(Color c, float a) { c.a = a; return c; }

        /// <summary>CSS color-mix(in srgb, a t, b): t of a, the rest b.</summary>
        public static Color Mix(Color a, Color b, float t)
        {
            return new Color(Mathf.Lerp(b.r, a.r, t), Mathf.Lerp(b.g, a.g, t), Mathf.Lerp(b.b, a.b, t), Mathf.Lerp(b.a, a.a, t));
        }

        /// <summary>The panel colour at an alpha: JS color-mix(in srgb, var(--th-panel) N%, transparent).</summary>
        public static Color PanelA(float a) { return A(Panel, a); }

        // ---- fonts and images ----
        static readonly Dictionary<UiFont, Font> fonts = new Dictionary<UiFont, Font>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static Font fallback;

        public static Font FontOf(UiFont f)
        {
            if (fonts.TryGetValue(f, out var font) && font != null) return font;
            string file = f switch
            {
                UiFont.Title => "cinzel_700",
                UiFont.TitleHeavy => "cinzel_800",
                UiFont.Mincho => "mincho_700",
                UiFont.Body => "barlow_400",
                UiFont.Cond => "barlowc_600",
                UiFont.Brush => "syuku_kanji",
                UiFont.Teko => "teko_600",
                UiFont.Marker => "marker_400",
                UiFont.Kanji => "yuji_kanji",
                _ => "rajdhani_700",
            };
            font = Resources.Load<Font>("UI/Fonts/" + file);
            if (font == null)
            {
                if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                font = fallback;
            }
            fonts[f] = font;
            return font;
        }

        /// <summary>A JS image converted to PNG under Resources/UI/Img (name without extension), or null.</summary>
        public static Texture2D Tex(string name)
        {
            if (textures.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("UI/Img/" + name);
            textures[name] = t;
            return t;
        }

        // ---- placement ----

        /// <summary>A child rectangle placed from its top-left corner (JS left/top/width/height).</summary>
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Stretches a node over its parent.</summary>
        public static RectTransform Fill(Transform parent, string name)
        {
            var rt = Node(parent, name, 0, 0, 0, 0);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // ---- plain graphics ----

        public static Image Box(Transform parent, float x, float y, float w, float h, Color c, string name = "Box")
        {
            var rt = Node(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A border drawn inside the rectangle, as four thin boxes.</summary>
        public static void Frame(Transform parent, float x, float y, float w, float h, float t, Color c)
        {
            Box(parent, x, y, w, t, c, "FrameT");
            Box(parent, x, y + h - t, w, t, c, "FrameB");
            Box(parent, x, y + t, t, h - 2 * t, c, "FrameL");
            Box(parent, x + w - t, y + t, t, h - 2 * t, c, "FrameR");
        }

        /// <summary>A hairline outline over a whole node that is shown only when its colour is set (focus ring).</summary>
        public static Outline2 Frame2(Transform parent, float w, float h)
        {
            var rt = Node(parent, "Ring", -2, -2, w + 4, h + 4);
            var g = rt.gameObject.AddComponent<Outline2>();
            g.raycastTarget = false;
            g.color = Color.clear;
            return g;
        }

        public static RawImage Pic(Transform parent, string tex, float x, float y, float w, float h, Color? tint = null)
        {
            var rt = Node(parent, "Pic", x, y, w, h);
            var ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = Tex(tex);
            ri.color = tint ?? Color.white;
            ri.raycastTarget = false;
            if (ri.texture == null) ri.color = new Color(0, 0, 0, 0);
            return ri;
        }

        /// <summary>CSS background-size: cover on a RawImage: crops the texture to the box, anchored by (ax, ay) in 0..1.</summary>
        public static void CoverUv(RawImage ri, float boxW, float boxH, float ax = 0.5f, float ay = 0.5f)
        {
            if (ri.texture == null) return;
            float tw = ri.texture.width, th = ri.texture.height;
            float k = Mathf.Max(boxW / tw, boxH / th);
            float uw = boxW / (tw * k), uh = boxH / (th * k);
            ri.uvRect = new Rect((1f - uw) * ax, (1f - uh) * ay, uw, uh);
        }

        public static Text Label(Transform parent, string text, UiFont font, int size, Color c, float x, float y, float w, float h,
            TextAnchor align = TextAnchor.UpperLeft, float tracking = 0f, bool wrap = false, float lineSpacing = 1f)
        {
            var rt = Node(parent, "Label", x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = FontOf(font);
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = lineSpacing;
            t.raycastTarget = false;
            t.supportRichText = false;
            if (tracking != 0f) rt.gameObject.AddComponent<Tracking>().px = tracking;
            return t;
        }

        /// <summary>A polygon (points in px from the node's top-left, y down) with an optional gradient.</summary>
        public static ShapeGraphic Shape(Transform parent, float x, float y, float w, float h, Vector2[] pts, Color c0, Color? c1 = null, Color? c2 = null, bool horizontal = false, string name = "Shape")
        {
            var rt = Node(parent, name, x, y, w, h);
            var g = rt.gameObject.AddComponent<ShapeGraphic>();
            g.raycastTarget = false;
            g.Set(pts, c0, c1 ?? c0, c2 ?? c1 ?? c0, horizontal);
            return g;
        }

        /// <summary>A ground silhouette: the polyline (JS "M x y L ..." points) filled down to <paramref name="bottom"/>.</summary>
        public static ShapeGraphic Ground2(Transform parent, float[] xy, float bottom, Color c, Color? cBottom = null)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            pts.Add(new Vector2(xy[xy.Length - 2], bottom));
            pts.Add(new Vector2(xy[0], bottom));
            return Shape(parent, 0, 0, 1600, 900, pts.ToArray(), c, cBottom ?? c);
        }

        public static GradRect Grad(Transform parent, float x, float y, float w, float h, Color a, Color b, bool horizontal = false, Color? mid = null)
        {
            var rt = Node(parent, "Grad", x, y, w, h);
            var g = rt.gameObject.AddComponent<GradRect>();
            g.raycastTarget = false;
            g.Set(horizontal, mid.HasValue ? new[] { a, mid.Value, b } : new[] { a, b });
            return g;
        }

        public static RadialGlow Glow(Transform parent, float cx, float cy, float r, Color center, float power = 1.6f)
        {
            var rt = Node(parent, "Glow", cx - r, cy - r, r * 2, r * 2);
            var g = rt.gameObject.AddComponent<RadialGlow>();
            g.raycastTarget = false;
            g.color = center;
            g.power = power;
            return g;
        }

        public static RadialGlow Disc(Transform parent, float cx, float cy, float r, Color c)
        {
            var g = Glow(parent, cx, cy, r, c, 0f);
            g.solid = true;
            return g;
        }

        /// <summary>A square turned 45 degrees: JS .w-dia::before. <paramref name="ds"/> is the square's side.</summary>
        public static DiamondGraphic Diamond(Transform parent, float cx, float cy, float ds, Color fill, Color border, float bw = 1.5f, bool centerPivot = false)
        {
            float half = ds * 0.7071f + 1f;
            var rt = Node(parent, "Diamond", cx - half, cy - half, half * 2, half * 2);
            // centerPivot (position unchanged): for diamonds that get scaled: the halo, the selection ring and the hover grow scale it, and a top-left pivot made them drift down and right
            if (centerPivot) { rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(cx, -cy); }
            var g = rt.gameObject.AddComponent<DiamondGraphic>();
            g.raycastTarget = false;
            g.Set(ds, bw, fill, border);
            return g;
        }

        public static PolyLine Line(Transform parent, Vector2[] pts, float width, Color c, float dash = 0f, float gap = 0f)
        {
            var rt = Node(parent, "Line", 0, 0, 1600, 900);
            var g = rt.gameObject.AddComponent<PolyLine>();
            g.raycastTarget = false;
            g.Set(pts, width, c, dash, gap);
            return g;
        }

        /// <summary>A star-shaped mark in the maple colour (JS wLeaves), n-pointed.</summary>
        public static Vector2[] Circle(float cx, float cy, float r, int n = 28)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                p[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return p;
        }

        /// <summary>CSS polygon() in percent of a w x h box to points.</summary>
        public static Vector2[] Poly(float w, float h, params float[] percentXY)
        {
            var p = new Vector2[percentXY.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(percentXY[i * 2] * 0.01f * w, percentXY[i * 2 + 1] * 0.01f * h);
            return p;
        }
    }

    /// <summary>CSS letter-spacing for a one-line legacy Text: moves each glyph quad right by px per character.</summary>
    [AddComponentMenu("")]
    public sealed class Tracking : BaseMeshEffect
    {
        public float px;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Mathf.Abs(px) < 0.01f) return;
            int count = vh.currentVertCount;
            int chars = count / 4;
            if (chars < 2) return;
            var text = graphic as Text;
            float shift = 0f;
            if (text != null)
            {
                var a = text.alignment;
                bool center = a == TextAnchor.UpperCenter || a == TextAnchor.MiddleCenter || a == TextAnchor.LowerCenter;
                bool right = a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight;
                if (center) shift = -px * (chars - 1) * 0.5f;
                else if (right) shift = -px * (chars - 1);
            }
            var v = new UIVertex();
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position.x += px * (i / 4) + shift;
                vh.SetUIVertex(v, i);
            }
        }
    }

    /// <summary>
    /// A rainbow across a Text or a shape (the legendary tier line): each vertex takes the hue of where it sits from the left to
    /// the right edge of the mesh, the hues drifting slowly sideways. Add it after <see cref="Tracking"/> so it colours the moved glyphs.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class Rainbow : BaseMeshEffect
    {
        public float saturation = 0.62f, value = 1f, speed = 0.12f;

        void Update() { if (speed != 0f && graphic != null) graphic.SetVerticesDirty(); }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            int count = vh.currentVertCount;
            if (count == 0) return;
            var v = new UIVertex();
            float x0 = float.MaxValue, x1 = float.MinValue;
            for (int i = 0; i < count; i++) { vh.PopulateUIVertex(ref v, i); x0 = Mathf.Min(x0, v.position.x); x1 = Mathf.Max(x1, v.position.x); }
            float span = Mathf.Max(1f, x1 - x0), shift = Time.unscaledTime * speed;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float h = Mathf.Repeat((v.position.x - x0) / span * 0.85f + shift, 1f);
                Color c = Color.HSVToRGB(h, saturation, value);
                c.a = v.color.a / 255f;
                v.color = c;
                vh.SetUIVertex(v, i);
            }
        }
    }

    /// <summary>A filled polygon (ear-clipped, so it may be concave) with a two or three stop gradient along x or y.</summary>
    [AddComponentMenu("")]
    public sealed class ShapeGraphic : MaskableGraphic
    {
        // serialized, so a copy of a screen (the wipe's ghost) draws the same shapes
        [SerializeField] Vector2[] pts = new Vector2[0];
        [SerializeField] Color c0, c1, c2;
        [SerializeField] bool horizontal;

        public void Set(Vector2[] points, Color a, Color b, Color c, bool horiz)
        {
            pts = points; c0 = a; c1 = b; c2 = c; horizontal = horiz;
            SetVerticesDirty();
        }

        public void SetColors(Color a, Color b, Color c) { c0 = a; c1 = b; c2 = c; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (pts == null || pts.Length < 3) return;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in pts) { float v = horizontal ? p.x : p.y; lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
            float span = Mathf.Max(0.001f, hi - lo);
            for (int i = 0; i < pts.Length; i++)
            {
                float t = ((horizontal ? pts[i].x : pts[i].y) - lo) / span;
                Color c = t < 0.5f ? Color.Lerp(c0, c1, t * 2f) : Color.Lerp(c1, c2, t * 2f - 1f);
                vh.AddVert(new Vector3(pts[i].x, -pts[i].y, 0f), c * color, Vector2.zero);
            }
            foreach (var tri in EarClip(pts)) vh.AddTriangle(tri.Item1, tri.Item2, tri.Item3);
        }

        static List<(int, int, int)> EarClip(Vector2[] p)
        {
            var res = new List<(int, int, int)>();
            int n = p.Length;
            var idx = new List<int>();
            float area = 0f;
            for (int i = 0; i < n; i++) { var a = p[i]; var b = p[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            for (int i = 0; i < n; i++) idx.Add(area > 0 ? i : n - 1 - i);
            int guard = n * n + 10;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f) continue;
                    bool inside = false;
                    foreach (int k in idx)
                    {
                        if (k == ia || k == ib || k == ic) continue;
                        if (PointInTri(p[k], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add((ia, ib, ic));
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) res.Add((idx[0], idx[1], idx[2]));
            return res;
        }

        static bool PointInTri(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (q.x - b.x) * (a.y - b.y) - (a.x - b.x) * (q.y - b.y);
            float d2 = (q.x - c.x) * (b.y - c.y) - (b.x - c.x) * (q.y - c.y);
            float d3 = (q.x - a.x) * (c.y - a.y) - (c.x - a.x) * (q.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }

    /// <summary>A rectangle with a gradient of two to three stops, horizontal or vertical.</summary>
    [AddComponentMenu("")]
    public sealed class GradRect : MaskableGraphic
    {
        [SerializeField] Color[] stops = { Color.white, Color.white };
        [SerializeField] bool horizontal;

        public void Set(bool horiz, Color[] cs) { horizontal = horiz; stops = cs; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            int n = stops.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Color c = stops[i] * color;
                if (horizontal)
                {
                    float x = Mathf.Lerp(r.xMin, r.xMax, t);
                    vh.AddVert(new Vector3(x, r.yMax), c, Vector2.zero);
                    vh.AddVert(new Vector3(x, r.yMin), c, Vector2.zero);
                }
                else
                {
                    float y = Mathf.Lerp(r.yMax, r.yMin, t);
                    vh.AddVert(new Vector3(r.xMin, y), c, Vector2.zero);
                    vh.AddVert(new Vector3(r.xMax, y), c, Vector2.zero);
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }
    }

    /// <summary>A round glow: the colour fades from the centre to nothing at the edge (alpha ^ power), or a solid disc.</summary>
    [AddComponentMenu("")]
    public sealed class RadialGlow : MaskableGraphic
    {
        public float power = 1.6f;
        public bool solid;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f;
            const int seg = 40, rings = 9;
            vh.AddVert(c, color, Vector2.zero);
            for (int k = 1; k <= rings; k++)
            {
                float u = k / (float)rings;
                float a = solid ? 1f : Mathf.Pow(1f - u, power);
                Color col = color; col.a *= a;
                for (int i = 0; i < seg; i++)
                {
                    float ang = i / (float)seg * Mathf.PI * 2f;
                    vh.AddVert(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius * u, col, Vector2.zero);
                }
            }
            for (int i = 0; i < seg; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % seg);
            for (int k = 1; k < rings; k++)
            {
                int b0 = 1 + (k - 1) * seg, b1 = 1 + k * seg;
                for (int i = 0; i < seg; i++)
                {
                    int j = (i + 1) % seg;
                    vh.AddTriangle(b0 + i, b1 + i, b1 + j);
                    vh.AddTriangle(b0 + i, b1 + j, b0 + j);
                }
            }
        }
    }

    /// <summary>JS .w-dia::before: a square turned 45 degrees with a border and a fill.</summary>
    [AddComponentMenu("")]
    public sealed class DiamondGraphic : MaskableGraphic
    {
        [SerializeField] float side, border;
        [SerializeField] Color fill, edge;

        public void Set(float ds, float bw, Color f, Color e) { side = ds; border = bw; fill = f; edge = e; SetVerticesDirty(); }

        public void SetColors(Color f, Color e) { fill = f; edge = e; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 c = rectTransform.rect.center;
            float outer = side * 0.7071f, inner = Mathf.Max(0f, (side - 2f * border) * 0.7071f);
            // the border is a ring between the two diamonds (so a clear fill leaves a hollow outline), the fill sits inside it
            if (border > 0f && edge.a > 0f)
            {
                int b = vh.currentVertCount;
                Color e = edge * color;
                Vector2[] o = { new Vector2(0, outer), new Vector2(outer, 0), new Vector2(0, -outer), new Vector2(-outer, 0) };
                Vector2[] n = { new Vector2(0, inner), new Vector2(inner, 0), new Vector2(0, -inner), new Vector2(-inner, 0) };
                for (int i = 0; i < 4; i++) vh.AddVert(c + o[i], e, Vector2.zero);
                for (int i = 0; i < 4; i++) vh.AddVert(c + n[i], e, Vector2.zero);
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    vh.AddTriangle(b + i, b + j, b + 4 + j);
                    vh.AddTriangle(b + i, b + 4 + j, b + 4 + i);
                }
            }
            float fillR = border > 0f ? inner : outer;
            if (fillR > 0f && fill.a > 0f) Quad(vh, c, fillR, fill * color);
        }

        static void Quad(VertexHelper vh, Vector2 c, float r, Color col)
        {
            int b = vh.currentVertCount;
            vh.AddVert(c + new Vector2(0, r), col, Vector2.zero);
            vh.AddVert(c + new Vector2(r, 0), col, Vector2.zero);
            vh.AddVert(c + new Vector2(0, -r), col, Vector2.zero);
            vh.AddVert(c + new Vector2(-r, 0), col, Vector2.zero);
            vh.AddTriangle(b, b + 1, b + 2);
            vh.AddTriangle(b, b + 2, b + 3);
        }
    }

    /// <summary>A polyline of fixed width, optionally dashed. Points are stage px (y down).</summary>
    [AddComponentMenu("")]
    public sealed class PolyLine : MaskableGraphic
    {
        [SerializeField] Vector2[] pts = new Vector2[0];
        [SerializeField] float width, dash, gap, phase;

        public void Set(Vector2[] p, float w, Color c, float d, float g)
        {
            pts = p; width = w; color = c; dash = d; gap = g;
            SetVerticesDirty();
        }

        /// <summary>How far, in px along the path, a dashed line's pattern has moved forward (the JS <c>stroke-dashoffset</c> animation).</summary>
        public void SetPhase(float px)
        {
            if (Mathf.Approximately(px, phase)) return;
            phase = px;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float cursor = 0f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                Vector2 dir = (b - a) / len;
                if (dash <= 0f) { Seg(vh, a, b); cursor += len; continue; }
                // dashes sit at [k period + phase, k period + phase + dash] along the whole path
                float period = dash + gap;
                int k = Mathf.FloorToInt((cursor - phase - dash) / period);
                for (int guard = 0; guard < 400; guard++, k++)
                {
                    float s0 = k * period + phase, s1 = s0 + dash;
                    if (s0 >= cursor + len) break;
                    float lo = Mathf.Max(s0, cursor), hi = Mathf.Min(s1, cursor + len);
                    if (hi > lo) Seg(vh, a + dir * (lo - cursor), a + dir * (hi - cursor));
                }
                cursor += len;
            }
        }

        void Seg(VertexHelper vh, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * width * 0.5f;
            a = new Vector2(a.x, -a.y); b = new Vector2(b.x, -b.y);
            n.y = -n.y;
            int i = vh.currentVertCount;
            vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(b - n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>A rectangular outline (2 px) drawn inside its rect; transparent until its colour is set.</summary>
    [AddComponentMenu("")]
    public sealed class Outline2 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            const float t = 2f;
            Add(vh, r.xMin, r.yMax - t, r.xMax, r.yMax);
            Add(vh, r.xMin, r.yMin, r.xMax, r.yMin + t);
            Add(vh, r.xMin, r.yMin + t, r.xMin + t, r.yMax - t);
            Add(vh, r.xMax - t, r.yMin + t, r.xMax, r.yMax - t);
        }

        void Add(VertexHelper vh, float x0, float y0, float x1, float y1)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), color, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), color, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), color, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
