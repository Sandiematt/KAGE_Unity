using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// Plays a <see cref="UiAnim"/> on a node from <see cref="UiClock"/>: opacity through a CanvasGroup, a move added on top of whatever else
    /// moves the node (a focus lift), a scale and a turn about the node's centre. A one-shot animation removes itself when it ends and leaves
    /// the node in its end pose, so nothing keeps writing to it afterwards (the hover scale of a button is its own again).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class UiAnimator : MonoBehaviour
    {
        UiAnim anim;
        float start;
        CanvasGroup group;
        RectTransform rt;
        Vector3 baseScale;
        float baseRot;
        Action onDone;
        bool done, absolute;

        public bool Finished { get { return done; } }

        /// <summary>Plays <paramref name="anim"/> on <paramref name="target"/>. An <paramref name="absolute"/> animation runs on the clock itself (an ambient loop, so a redraw of the screen does not restart it).</summary>
        public static UiAnimator Play(Transform target, UiAnim anim, Action onDone = null, bool absolute = false)
        {
            var old = target.GetComponents<UiAnimator>();
            foreach (var o in old) if (o.anim != null && o.anim.Name == anim.Name) { o.Cancel(); }
            var a = target.gameObject.AddComponent<UiAnimator>();
            a.absolute = absolute;
            a.Begin(anim, onDone);
            return a;
        }

        void Begin(UiAnim a, Action finished)
        {
            anim = a;
            onDone = finished;
            rt = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            baseScale = rt.localScale;
            baseRot = rt.localEulerAngles.z;
            start = absolute ? 0f : UiClock.Now + UiClock.EntranceDelay;
            if (!UiClock.Animate)
            {
                // still screens: a loop shows its middle, everything else its end
                Apply(anim.Sample(anim.Loop ? anim.Delay + anim.Duration * 0.5f : anim.EndTime));
                Complete();
                return;
            }
            Apply(anim.Sample(0f));
        }

        void Update()
        {
            if (done || anim == null) return;
            if (!UiClock.Animate && anim.Loop) return;                 // reduced motion holds the ambient loops still
            float t = UiClock.Now - start;
            if (!UiClock.Animate) t = anim.EndTime;
            Apply(anim.Sample(t));
            if (anim.IsFinished(t)) Complete();
        }

        void Complete()
        {
            if (done) return;
            done = true;
            var cb = onDone;
            onDone = null;
            if (!anim.Loop) Destroy(this);
            if (cb != null) cb();
        }

        public void Cancel()
        {
            if (done) return;
            Apply(UiPose.Identity);
            done = true;
            Destroy(this);
        }

        void Apply(UiPose p)
        {
            if (rt == null) return;
            if (group != null) group.alpha = p.Alpha;
            rt.localScale = new Vector3(baseScale.x * p.SX, baseScale.y * p.SY, baseScale.z);
            rt.localRotation = Quaternion.Euler(0f, 0f, baseRot - p.Rot);
            // scaling and turning happen about the node's centre (CSS transform-origin 50% 50%), the node's pivot is its corner
            Vector2 c = rt.rect.center;
            float rad = -p.Rot * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            Vector2 sc = new Vector2(c.x * p.SX, c.y * p.SY);
            Vector2 moved = new Vector2(sc.x * cos - sc.y * sin, sc.x * sin + sc.y * cos);
            UiOffsets.Of(rt).SetAnim(new Vector2(p.X, -p.Y) + (c - moved));
        }
    }

    /// <summary>
    /// The position of a node as three parts that add up: where the screen built it (the base), a focus lift or slide, and what a running
    /// animation adds. Everything that moves an animated node goes through here, so a hover and a rise never overwrite each other.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class UiOffsets : MonoBehaviour
    {
        RectTransform rt;
        Vector2 basePos, focus, anim;

        public static UiOffsets Of(RectTransform rt)
        {
            var o = rt.GetComponent<UiOffsets>();
            if (o != null) return o;
            o = rt.gameObject.AddComponent<UiOffsets>();
            o.rt = rt;
            o.basePos = rt.anchoredPosition;
            return o;
        }

        /// <summary>Moves the node's base position (a screen that scrolls or lays the node out again).</summary>
        public void SetBase(Vector2 p) { basePos = p; Write(); }

        /// <summary>The focus offset in stage px (+x right, +y up).</summary>
        public void SetFocus(Vector2 v) { focus = v; Write(); }

        public void SetAnim(Vector2 v) { anim = v; Write(); }

        void Write() { if (rt != null) rt.anchoredPosition = basePos + focus + anim; }
    }

    /// <summary>
    /// A hover or focus share that eases toward 1 (focused) or 0 (resting) in the item's own time, like the JS <c>transition: ... .16s</c>:
    /// the screen's callback draws whatever that share means (a wedge growing, a label sliding). When the focus is set in the same frame the
    /// node was built (a rebuild restoring it) the share snaps instead of replaying the hover.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class UiFocusTween : MonoBehaviour
    {
        float duration = .2f, value;
        bool target;
        Action<float> apply;
        int born;

        public float Value { get { return value; } }

        public static UiFocusTween On(Component c, float seconds, Action<float> apply)
        {
            var tw = c.gameObject.AddComponent<UiFocusTween>();
            tw.duration = seconds;
            tw.apply = apply;
            tw.born = Time.frameCount;
            apply(0f);
            return tw;
        }

        public void Set(bool focused)
        {
            if (focused == target && (value == (focused ? 1f : 0f))) return;
            target = focused;
            if (!UiClock.Animate || Time.frameCount == born)
            {
                value = focused ? 1f : 0f;
                apply(value);
            }
        }

        void Update()
        {
            float goal = target ? 1f : 0f;
            if (value == goal) return;
            value = UiFocusCurve.Step(value, target, Time.unscaledDeltaTime, duration);
            apply(value);
        }
    }

    /// <summary>
    /// A crisp metallic edge glint that runs along the top border of a button on hover or press — keeps the button face and label
    /// razor-sharp with no hazy wash across the text. Never plays on the build frame or when motion is off.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ButtonSheen : MonoBehaviour
    {
        RectTransform bar;
        float width, start = -99f, seconds = .45f, playAt = -1f, afterSeconds = 1.4f;
        int born;

        public static ButtonSheen Add(RectTransform button, float w, float h)
        {
            var clip = Ui.Node(button, "Sheen", 0, 0, w, 3f);
            clip.gameObject.AddComponent<RectMask2D>();
            var s = clip.gameObject.AddComponent<ButtonSheen>();
            s.width = w;
            s.born = Time.frameCount;
            float bw = Mathf.Clamp(w * 0.24f, 44f, 96f);
            s.bar = Ui.Node(clip, "Bar", -bw, 0f, bw, 2f);
            s.bar.pivot = new Vector2(0.5f, 0.5f);
            var g = s.bar.gameObject.AddComponent<GradRect>();
            g.raycastTarget = false;
            g.Set(true, new[] { new Color(1f, 0.94f, 0.76f, 0f), new Color(1f, 0.94f, 0.76f, 0.65f), new Color(1f, 0.94f, 0.76f, 0f) });
            s.bar.gameObject.SetActive(false);
            return s;
        }

        public void Play(float duration)
        {
            if (!UiClock.Animate || Time.frameCount == born) return;
            seconds = duration;
            start = UiClock.Now;
            bar.gameObject.SetActive(true);
        }

        /// <summary>Plays the edge glint once, <paramref name="delay"/> seconds from now.</summary>
        public void PlayAfter(float delay, float duration)
        {
            playAt = UiClock.Now + delay;
            afterSeconds = duration;
        }

        void Update()
        {
            if (bar == null) return;            // a copy of a screen (the wipe's ghost) has no sweep
            if (playAt >= 0f && UiClock.Now >= playAt)
            {
                playAt = -1f;
                seconds = afterSeconds;
                start = UiClock.Now;
                if (UiClock.Animate) bar.gameObject.SetActive(true);
            }
            if (!bar.gameObject.activeSelf) return;
            float k = (UiClock.Now - start) / seconds;
            if (k >= 1f) { bar.gameObject.SetActive(false); return; }
            float e = UiEase.Ease.Evaluate(Mathf.Clamp01(k));
            float x = Mathf.Lerp(-bar.rect.width * 0.5f, width + bar.rect.width * 0.5f, e);
            bar.anchoredPosition = new Vector2(x, -1f);
        }
    }

    /// <summary>Marches the dashes of an unvisited road along it (JS <c>cpDash</c>: stroke-dashoffset to -32 in 1.6 s, linear, forever).</summary>
    [AddComponentMenu("")]
    public sealed class UiDashFlow : MonoBehaviour
    {
        PolyLine line;

        public static UiDashFlow On(PolyLine l)
        {
            var d = l.gameObject.AddComponent<UiDashFlow>();
            d.line = l;
            return d;
        }

        /// <summary>The pattern's offset at a time: 32 px every 1.6 s, wrapped to the 16 px period.</summary>
        public static float PhaseAt(float time) { return (time * 20f) % 16f; }

        void Update()
        {
            if (line == null) return;
            line.SetPhase(UiClock.Animate ? PhaseAt(UiClock.Now) : 0f);
        }
    }

    /// <summary>
    /// A card that rises and is uncovered from the bottom upward (JS <c>cpBossCard</c>: up 26 px while its clip opens from 100% to 0). The clip is the
    /// node's own rect: its top edge rises while the bottom stays, and what is inside stays where it is (children sit on a bottom-anchored content node).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class UiRevealUp : MonoBehaviour
    {
        public const float Seconds = .7f, Rise = 26f;

        RectTransform node;
        float fullH, y0, born;

        /// <summary>The share of the card uncovered at a time (the JS --ease over .7 s).</summary>
        public static float Open(float time) { return UiEase.Ease.Evaluate(Mathf.Clamp01(time / Seconds)); }

        /// <summary>Makes <paramref name="n"/> (already holding its children, placed from its top-left corner) reveal itself.</summary>
        public static void On(RectTransform n, float fullHeight)
        {
            n.gameObject.AddComponent<RectMask2D>();
            var kids = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in n) kids.Add(c);
            var content = Ui.Node(n, "Content", 0, 0, n.sizeDelta.x, fullHeight);
            content.anchorMin = content.anchorMax = Vector2.zero;
            content.pivot = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            foreach (var c in kids) c.SetParent(content, false);
            var r = n.gameObject.AddComponent<UiRevealUp>();
            r.node = n; r.fullH = fullHeight; r.y0 = n.anchoredPosition.y; r.born = UiClock.Now;
            r.Tick();
        }

        void Update() { Tick(); }

        void Tick()
        {
            if (node == null) return;
            float k = UiClock.Animate ? Open(UiClock.Now - born) : 1f;
            node.sizeDelta = new Vector2(node.sizeDelta.x, Mathf.Max(0.01f, fullH * k));
            node.anchoredPosition = new Vector2(node.anchoredPosition.x, y0 - fullH * (1f - k) - Rise * (1f - k));
        }
    }

    /// <summary>Drives the leaf nodes of a <see cref="PetalField"/> from the UI clock.</summary>
    [AddComponentMenu("")]
    public sealed class UiPetalView : MonoBehaviour
    {
        PetalField field;
        RectTransform[] leaves;
        CanvasGroup[] groups;
        float baseW, baseH;

        public static UiPetalView Build(Transform parent, string name, float x, float y, float w, float h, PetalField field, float leafW, float leafH, Color hot, Color maple)
        {
            var rt = Ui.Node(parent, name, x, y, w, h);
            rt.gameObject.AddComponent<RectMask2D>();
            var v = rt.gameObject.AddComponent<UiPetalView>();
            v.field = field;
            v.baseW = leafW; v.baseH = leafH;
            v.leaves = new RectTransform[field.Petals.Length];
            v.groups = new CanvasGroup[field.Petals.Length];
            for (int i = 0; i < field.Petals.Length; i++)
            {
                float s = field.Petals[i].Size;
                var leaf = Ui.Node(rt, "Petal", 0, 0, leafW * s, leafH * s);
                leaf.pivot = new Vector2(0.5f, 0.5f);
                var g = leaf.gameObject.AddComponent<PetalGraphic>();
                g.raycastTarget = false;
                g.Set(hot, maple);
                v.groups[i] = leaf.gameObject.AddComponent<CanvasGroup>();
                v.leaves[i] = leaf;
            }
            v.Tick();
            return v;
        }

        void Update() { Tick(); }

        void Tick()
        {
            if (field == null) return;
            // reduced motion holds the petals where they are (the JS animation-play-state: paused)
            float t = UiClock.Reduced || UiClock.Skip ? 0f : UiClock.Now;
            for (int i = 0; i < leaves.Length; i++)
            {
                var st = field.Sample(i, t);
                var leaf = leaves[i];
                leaf.anchoredPosition = new Vector2(st.X + baseW * st.Size * 0.5f, -(st.Y + baseH * st.Size * 0.5f));
                leaf.localRotation = Quaternion.Euler(0f, 0f, -st.Rot);
                leaf.localScale = new Vector3(Mathf.Max(0.06f, st.TurnX), 1f, 1f);
                groups[i].alpha = st.Alpha;
            }
        }
    }

    /// <summary>Drives drifting cloud bands (JS wClouds) from the UI clock; each band is a soft cloud shape that slides across the stage.</summary>
    [AddComponentMenu("")]
    public sealed class UiCloudView : MonoBehaviour
    {
        CloudBand[] bands;
        RectTransform[] nodes;

        public static UiCloudView Build(Transform parent, string name, CloudBand[] bands, Color dark, Color lit)
        {
            var rt = Ui.Node(parent, name, 0, 0, 1600, 900);
            var v = rt.gameObject.AddComponent<UiCloudView>();
            v.bands = bands;
            v.nodes = new RectTransform[bands.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                var b = bands[i];
                float h = b.Width * 0.2f;
                var n = Ui.Node(rt, "Cloud", 0, b.Y, b.Width, h);
                Color c = b.Lit ? lit : dark;
                // Clean single stylized sumi-e cloud silhouette with gentle vertical fade (no muddy triple-stacked blur halo)
                var pts = CloudShape(b.Width, h);
                var s = Ui.Shape(n, 0f, 0f, b.Width, h, pts, Ui.A(c, b.Opacity * 0.58f), Ui.A(c, b.Opacity * 0.22f), null, false, "CloudShape");
                v.nodes[i] = n;
            }
            v.Tick();
            return v;
        }

        // JS: the cloud band's SVG path in its 300 x 60 box, stretched over width x height
        static readonly float[] Path =
        {
            0, 44, 22, 30, 54, 34, 70, 14, 104, 20, 126, 4, 160, 14, 190, 2, 214, 18, 244, 12, 260, 28, 290, 30, 300, 44,
            260, 52, 210, 50, 170, 58, 120, 52, 70, 58, 40, 52, 10, 52,
        };

        static Vector2[] CloudShape(float w, float h)
        {
            // pairs of (control, end): the path is a chain of quadratic curves
            var pts = new List<Vector2>();
            Vector2 cur = new Vector2(0, 44);
            pts.Add(cur);
            for (int i = 2; i + 3 < Path.Length + 2; i += 4)
            {
                if (i + 3 >= Path.Length) break;
                Vector2 c = new Vector2(Path[i], Path[i + 1]), e = new Vector2(Path[i + 2], Path[i + 3]);
                for (int s = 1; s <= 6; s++)
                {
                    float t = s / 6f, u = 1f - t;
                    pts.Add(u * u * cur + 2f * u * t * c + t * t * e);
                }
                cur = e;
            }
            // the closing curve back to the start: (10, 52) is the control point of the last segment into (0, 44)
            {
                Vector2 c = new Vector2(10, 52), e = new Vector2(0, 44);
                for (int s = 1; s <= 4; s++)
                {
                    float t = s / 4f, u = 1f - t;
                    pts.Add(u * u * cur + 2f * u * t * c + t * t * e);
                }
            }
            var o = new Vector2[pts.Count];
            for (int i = 0; i < o.Length; i++) o[i] = new Vector2(pts[i].x / 300f * w, pts[i].y / 60f * h);
            return o;
        }

        void Update() { Tick(); }

        void Tick()
        {
            if (bands == null) return;
            float t = UiClock.Reduced || UiClock.Skip ? 0f : UiClock.Now;
            for (int i = 0; i < nodes.Length; i++)
                nodes[i].anchoredPosition = new Vector2(bands[i].X(t), -bands[i].Y);
        }
    }

    /// <summary>A sakura petal: a leaf with one sharp corner (CSS border-radius 80% 0 55% 50% / 55% 0 80% 50%), shaded across its diagonal.</summary>
    [AddComponentMenu("")]
    public sealed class PetalGraphic : MaskableGraphic
    {
        [SerializeField] Color hot = Color.white, maple = Color.red;

        public void Set(Color hotColour, Color mapleColour) { hot = hotColour; maple = mapleColour; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            // corner radii (rx, ry): TL, TR, BR, BL, scaled down together where two on a side would overlap
            float[] rx = { .8f * w, 0, .55f * w, .5f * w }, ry = { .55f * h, 0, .8f * h, .5f * h };
            float f = Mathf.Min(1f, 1f / ((rx[0] + rx[1]) / w), 1f / ((rx[3] + rx[2]) / w), 1f / ((ry[0] + ry[3]) / h), 1f / ((ry[1] + ry[2]) / h));
            for (int i = 0; i < 4; i++) { rx[i] *= f; ry[i] *= f; }
            var pts = new List<Vector2>();
            // clockwise from the top-left, y down in this local list
            Arc(pts, rx[0], ry[0], rx[0], ry[0], 180f, 270f);                                       // TL
            if (rx[1] > 0) Arc(pts, w - rx[1], ry[1], rx[1], ry[1], 270f, 360f); else pts.Add(new Vector2(w, 0));       // TR
            Arc(pts, w - rx[2], h - ry[2], rx[2], ry[2], 0f, 90f);                                  // BR
            Arc(pts, rx[3], h - ry[3], rx[3], ry[3], 90f, 180f);                                    // BL
            Color c0 = Color.Lerp(maple, Color.white, 0.0f);
            Color topLeft = Color.Lerp(hot, Color.white, 0.3f);
            for (int i = 0; i < pts.Count; i++)
            {
                float k = Mathf.Clamp01((pts[i].x / w + pts[i].y / h) * 0.5f / 0.7f);
                Color col = Color.Lerp(topLeft, c0, k);
                col.a *= color.a;
                vh.AddVert(new Vector3(r.xMin + pts[i].x, r.yMax - pts[i].y, 0f), col, Vector2.zero);
            }
            for (int i = 1; i + 1 < pts.Count; i++) vh.AddTriangle(0, i, i + 1);
        }

        static void Arc(List<Vector2> pts, float cx, float cy, float rx, float ry, float a0, float a1)
        {
            const int n = 6;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
        }
    }

    /// <summary>Small helpers the screens call to play JS animations on the nodes they build.</summary>
    public static class UiFx
    {
        /// <summary>Plays a one-shot entrance on a node (or sets it to its end state when motion is off).</summary>
        public static UiAnimator In(Transform node, UiAnim anim) { return UiAnimator.Play(node, anim); }

        /// <summary>Plays an ambient loop on a node.</summary>
        public static UiAnimator Loop(Transform node, UiAnim anim) { return UiAnimator.Play(node, anim, null, true); }

        /// <summary>The press feedback of a button: a quick dip and back.</summary>
        public static void Press(Transform node, float low = .96f)
        {
            if (!UiClock.Animate || node == null) return;
            UiAnimator.Play(node, UiAnims.Press(low));
        }
    }
}
