using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>A letter image revealed behind a ragged dry-brush front moving along its stroke (JS drawLetter's clip), as a mesh.</summary>
    [AddComponentMenu("")]
    public sealed class SplashLetterGraphic : MaskableGraphic
    {
        Texture tex;
        Rect frame;
        Vector2 centre;
        float angle, front;
        bool full;
        float[] jag = new float[0];

        public override Texture mainTexture { get { return tex != null ? tex : base.mainTexture; } }

        public void Set(Texture t, Rect letterFrame, Vector2 letterCentre, float strokeAngle, float[] jaggedness)
        {
            tex = t; frame = letterFrame; centre = letterCentre; angle = strokeAngle; jag = jaggedness;
        }

        public void SetReveal(float frontAlong, bool whole)
        {
            front = frontAlong; full = whole;
            SetVerticesDirty();
        }

        // art px (y down) to the node's local px (the node's pivot is its top-left corner)
        Vector3 P(Vector2 art) { return new Vector3(art.x, -art.y, 0f); }

        Vector2 Uv(Vector2 art) { return new Vector2((art.x - frame.x) / frame.width, 1f - (art.y - frame.y) / frame.height); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (tex == null) return;
            if (full)
            {
                Vert(vh, new Vector2(frame.xMin, frame.yMin)); Vert(vh, new Vector2(frame.xMax, frame.yMin));
                Vert(vh, new Vector2(frame.xMax, frame.yMax)); Vert(vh, new Vector2(frame.xMin, frame.yMax));
                vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
                return;
            }
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            int n = jag.Length;
            float step = 2400f / Mathf.Max(1, n - 1);
            for (int j = 0; j + 1 < n; j++)
            {
                float y0 = -1200f + j * step, y1 = y0 + step;
                Vector2[] q = { new Vector2(-2400f, y0), new Vector2(front + jag[j], y0), new Vector2(front + jag[j + 1], y1), new Vector2(-2400f, y1) };
                // the revealed strip, cut to the letter image's own rectangle (the JS draws the image inside its frame only)
                var poly = new List<Vector2>(8);
                for (int k = 0; k < 4; k++) poly.Add(centre + new Vector2(q[k].x * c - q[k].y * s, q[k].x * s + q[k].y * c));
                ClipToFrame(poly);
                if (poly.Count < 3) continue;
                int b = vh.currentVertCount;
                foreach (var pt in poly) Vert(vh, pt);
                for (int k = 1; k + 1 < poly.Count; k++) vh.AddTriangle(b, b + k, b + k + 1);
            }
        }

        void Vert(VertexHelper vh, Vector2 art) { vh.AddVert(P(art), color, Uv(art)); }

        // Sutherland-Hodgman against the four sides of the letter frame
        void ClipToFrame(List<Vector2> poly)
        {
            for (int side = 0; side < 4; side++)
            {
                var input = new List<Vector2>(poly);
                poly.Clear();
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 a = input[i], b = input[(i + 1) % input.Count];
                    bool ia = Inside(a, side), ib = Inside(b, side);
                    if (ia) poly.Add(a);
                    if (ia != ib) poly.Add(Cross(a, b, side));
                }
                if (poly.Count == 0) return;
            }
        }

        bool Inside(Vector2 p, int side)
        {
            switch (side)
            {
                case 0: return p.x >= frame.xMin;
                case 1: return p.x <= frame.xMax;
                case 2: return p.y >= frame.yMin;
                default: return p.y <= frame.yMax;
            }
        }

        Vector2 Cross(Vector2 a, Vector2 b, int side)
        {
            float edge = side == 0 ? frame.xMin : side == 1 ? frame.xMax : side == 2 ? frame.yMin : frame.yMax;
            bool vertical = side < 2;
            float t = vertical ? (edge - a.x) / (b.x - a.x) : (edge - a.y) / (b.y - a.y);
            return a + (b - a) * t;
        }
    }

    /// <summary>The red blade of light of a splash slash: a body thin at the tail and full at the head, a white core and a soft red glow (JS blade()).</summary>
    [AddComponentMenu("")]
    public sealed class SplashBladeGraphic : MaskableGraphic
    {
        Vector2[] pts = new Vector2[0];
        float width, alpha;

        public void Set(Vector2[] points, float w, float a) { pts = points; width = w; alpha = a; SetVerticesDirty(); }

        Vector2 Edge(int k, float side, float scale)
        {
            int n = pts.Length;
            Vector2 p = pts[k], q = pts[Mathf.Min(n - 1, k + 1)], o = pts[Mathf.Max(0, k - 1)];
            float dx = q.x - o.x, dy = q.y - o.y, len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f) len = 1f;
            float f = k / (float)(n - 1);
            float ww = width * scale * Mathf.Sin(Mathf.PI * Mathf.Min(1f, f * 1.15f));
            return new Vector2(p.x - dy / len * ww * side, p.y + dx / len * ww * side);
        }

        void Layer(VertexHelper vh, float scale, Color col)
        {
            int n = pts.Length;
            int b = vh.currentVertCount;
            for (int k = 0; k < n; k++)
            {
                Vector2 l = Edge(k, 1f, scale), r = Edge(k, -1f, scale);
                vh.AddVert(new Vector3(l.x, -l.y, 0f), col, Vector2.zero);
                vh.AddVert(new Vector3(r.x, -r.y, 0f), col, Vector2.zero);
            }
            for (int k = 0; k + 1 < n; k++)
            {
                int i = b + k * 2;
                vh.AddTriangle(i, i + 1, i + 3); vh.AddTriangle(i, i + 3, i + 2);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (pts.Length < 2 || alpha <= 0f) return;
            Layer(vh, 2.6f, new Color(1f, 0.16f, 0.16f, 0.16f * alpha));
            Layer(vh, 1.7f, new Color(1f, 0.16f, 0.16f, 0.26f * alpha));
            Layer(vh, 1f, new Color(0.89f, 0.098f, 0.12f, alpha));
            if (core) Layer(vh, 0.38f, new Color(1f, 0.953f, 0.925f, alpha));
        }

        /// <summary>The slashes have a white core; the final cut is plain red.</summary>
        public bool core = true;
    }

    /// <summary>Kage's scarf in the splash scene: the knot at his nape and two tails blown back to the left, waving (JS drawScarf).</summary>
    [AddComponentMenu("")]
    public sealed class SplashScarfGraphic : MaskableGraphic
    {
        static readonly Vector2 Knot = new Vector2(885f, 274f);
        static readonly Color Scarf = Ui.Hex("#a8262c"), ScarfDark = Ui.Hex("#6a1218"), Ink = new Color(3f / 255f, 2f / 255f, 2f / 255f, 1f);
        float time;

        public void SetTime(float t) { time = t; SetVerticesDirty(); }

        void Strip(VertexHelper vh, float len, int n, float w0, float w1, float ph, float lift, Color col, float grow)
        {
            var left = new Vector2[n + 1];
            var right = new Vector2[n + 1];
            System.Func<float, Vector2> pt = u => new Vector2(
                Knot.x - u * len,
                Knot.y + u * lift + (Mathf.Sin(time * 1.35f - u * 5.2f + ph) * 5.5f + Mathf.Sin(time * 2.20f - u * 8.4f + ph * 1.3f) * 1.1f) * u);
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                Vector2 p = pt(u), q = pt(u + 0.05f);
                Vector2 d = (q - p);
                float dl = d.magnitude; if (dl < 1e-4f) dl = 1f; d /= dl;
                float ww = Mathf.Lerp(w0, w1, u) / 2f + grow;
                left[i] = new Vector2(p.x - d.y * ww, p.y + d.x * ww);
                right[i] = new Vector2(p.x + d.y * ww, p.y - d.x * ww);
            }
            int b = vh.currentVertCount;
            for (int i = 0; i <= n; i++)
            {
                vh.AddVert(new Vector3(left[i].x, -left[i].y, 0f), col, Vector2.zero);
                vh.AddVert(new Vector3(right[i].x, -right[i].y, 0f), col, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int k = b + i * 2;
                vh.AddTriangle(k, k + 1, k + 3); vh.AddTriangle(k, k + 3, k + 2);
            }
        }

        void Knob(VertexHelper vh, float rx, float ry, Color col)
        {
            int b = vh.currentVertCount;
            const int seg = 16;
            float cr = Mathf.Cos(-0.5f), sr = Mathf.Sin(-0.5f);
            vh.AddVert(new Vector3(Knot.x, -Knot.y, 0f), col, Vector2.zero);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * rx, y = Mathf.Sin(a) * ry;
                vh.AddVert(new Vector3(Knot.x + x * cr - y * sr, -(Knot.y + x * sr + y * cr), 0f), col, Vector2.zero);
            }
            for (int i = 0; i < seg; i++) vh.AddTriangle(b, b + 1 + i, b + 1 + (i + 1) % seg);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // the ink outline is the same strip a pixel wider underneath
            Strip(vh, 62f, 10, 9f, 5f, 2f, 14f, Ink, 1f);
            Strip(vh, 96f, 12, 14f, 6f, 0f, -8f, Ink, 1f);
            Strip(vh, 62f, 10, 9f, 5f, 2f, 14f, ScarfDark, 0f);
            Strip(vh, 96f, 12, 14f, 6f, 0f, -8f, Scarf, 0f);
            Knob(vh, 10.2f, 8.2f, Ink);
            Knob(vh, 9f, 7f, Scarf);
        }
    }

    /// <summary>
    /// The opening splash (JS boot screen, 65-splash.js): the moon scene settles, K-A-G-E is written stroke by stroke, each letter cut in by a
    /// slash from outside the frame, then the subtitle, the tagline and the prompt. Any key, click or gamepad button opens the title (the JS boot
    /// screen fades away in .5 s). Reduced motion opens on the finished card. The timeline is <see cref="SplashTimeline"/>.
    /// </summary>
    public sealed class SplashPanel : MonoBehaviour
    {
        const float FadeOut = 0.5f;

        RectTransform root, art, sceneWrap, letters, subtitle, prompt, ruleL, ruleR;
        CanvasGroup rootGroup, sceneGroup, subtitleGroup, promptGroup;
        SplashScarfGraphic scarf;
        readonly SplashLetterGraphic[] letterGfx = new SplashLetterGraphic[4];
        readonly SplashBladeGraphic[] blades = new SplashBladeGraphic[4];
        SplashBladeGraphic cut;
        readonly List<RectTransform> cutLines = new List<RectTransform>();
        readonly float[][] jag = new float[4][];
        readonly List<Petal> petals = new List<Petal>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<RectTransform> petalNodes = new List<RectTransform>(), sparkNodes = new List<RectTransform>();
        readonly bool[] burst = new bool[5];
        System.Random rng;
        float startedAt, lastT, dismissedAt = -1f, manualAt = -1f;
        RectTransform canvasRoot;
        bool dismissed;
        Text sub, promptText;

        struct Petal { public float x, y, vx, vy, r, a, va, ph; }
        struct Spark { public float x, y, vx, vy, life, t; public bool white; }

        public bool Active { get { return gameObject.activeSelf && !dismissed; } }

        public bool Showing { get { return gameObject.activeSelf; } }

        public static SplashPanel Create(RectTransform canvasRoot)
        {
            var go = new GameObject("Splash", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var p = go.AddComponent<SplashPanel>();
            p.canvasRoot = canvasRoot;
            p.Build(rt);
            return p;
        }

        void Build(RectTransform rt)
        {
            root = rt;
            rootGroup = gameObject.AddComponent<CanvasGroup>();
            // the JS boot screen is its own background: the void
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.008f, 0.008f, 0.008f, 1f);
            bg.raycastTarget = true;
            rng = new System.Random(7);
            for (int i = 0; i < 4; i++)
            {
                jag[i] = new float[46];
                for (int j = 0; j < 46; j++) jag[i][j] = ((float)rng.NextDouble() - 0.5f) * 38f;
            }
            rng = new System.Random(1234);

            art = Ui.Node(root, "Art", 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
            art.pivot = new Vector2(0.5f, 0.5f);

            // the scene, scaled about (ART_W / 2, ART_H * 0.42): that point is the node's pivot, its children are placed from its corner
            sceneWrap = Ui.Node(art, "Scene", 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
            sceneWrap.pivot = new Vector2(0.5f, 1f - 0.42f);
            sceneWrap.anchoredPosition = new Vector2(SplashTimeline.ArtW / 2f, -SplashTimeline.ArtH * 0.42f);
            sceneGroup = sceneWrap.gameObject.AddComponent<CanvasGroup>();
            Ui.Pic(sceneWrap, "splash_scene", 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH).name = "SceneArt";
            var scarfNode = Ui.Node(sceneWrap, "Scarf", 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
            scarf = scarfNode.gameObject.AddComponent<SplashScarfGraphic>();
            scarf.raycastTarget = false;

            // petals drifting on the wind (a pool of leaves)
            for (int i = 0; i < 9; i++) SpawnPetal(true);
            for (int i = 0; i < 18; i++)
            {
                var n = Ui.Node(art, "Petal", 0, 0, 24, 24);
                n.pivot = new Vector2(0.5f, 0.5f);
                var g = n.gameObject.AddComponent<ShapeGraphic>();
                g.raycastTarget = false;
                g.Set(Ui.Circle(12, 12, 12, 16), Ui.Hex("#d42a2a"), Ui.Hex("#d42a2a"), Ui.Hex("#d42a2a"), false);
                n.gameObject.SetActive(false);
                petalNodes.Add(n);
            }

            // the letters and their slashes, scaled about the letter frame's centre (the node's pivot; its rect covers the art)
            letters = Ui.Node(art, "Letters", 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
            var lc = SplashTimeline.LetterCentre;
            letters.pivot = new Vector2(lc.x / SplashTimeline.ArtW, 1f - lc.y / SplashTimeline.ArtH);
            letters.anchoredPosition = new Vector2(lc.x, -lc.y);
            for (int i = 0; i < 4; i++)
            {
                var n = Ui.Node(letters, "Letter_" + SplashTimeline.Letters[i].Key, 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
                var g = n.gameObject.AddComponent<SplashLetterGraphic>();
                g.raycastTarget = false;
                g.Set(Ui.Tex("splash_" + SplashTimeline.Letters[i].Key), SplashTimeline.LetterFrame, SplashTimeline.LetterCentreOf(i), SplashTimeline.Letters[i].Angle, jag[i]);
                g.SetReveal(-9999f, false);
                letterGfx[i] = g;
            }
            for (int i = 0; i < 4; i++)
            {
                var n = Ui.Node(letters, "Slash" + i, 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
                blades[i] = n.gameObject.AddComponent<SplashBladeGraphic>();
                blades[i].raycastTarget = false;
            }
            var cn = Ui.Node(letters, "Cut", 0, 0, SplashTimeline.ArtW, SplashTimeline.ArtH);
            cut = cn.gameObject.AddComponent<SplashBladeGraphic>();
            cut.raycastTarget = false;
            cut.core = false;
            for (int i = 0; i < 12; i++)
            {
                var ln = Ui.Node(letters, "Impact", 0, 0, 1, 1);
                ln.pivot = new Vector2(0f, 0.5f);
                var img = ln.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(1f, 0.957f, 0.925f, 0f);
                ln.gameObject.SetActive(false);
                cutLines.Add(ln);
            }

            // sparks
            for (int i = 0; i < 60; i++)
            {
                var n = Ui.Node(art, "Spark", 0, 0, 5, 5);
                n.pivot = new Vector2(0.5f, 0.5f);
                var img = n.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                n.gameObject.SetActive(false);
                sparkNodes.Add(n);
            }

            // the subtitle between two rules, and the prompt
            ruleL = Ui.Box(art, 0, 712, 1, 2, new Color(0.91f, 0.871f, 0.808f, 0.55f), "RuleL").rectTransform;
            ruleR = Ui.Box(art, 0, 712, 1, 2, new Color(0.91f, 0.871f, 0.808f, 0.55f), "RuleR").rectTransform;
            subtitle = Ui.Node(art, "Subtitle", 887 - 500, 712 - 36, 1000, 72);
            subtitle.pivot = new Vector2(0.5f, 0.5f);
            subtitle.anchoredPosition = new Vector2(887, -712);
            subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            sub = Ui.Label(subtitle, "THE LAST BETRAYAL", UiFont.Title, 44, Ui.Hex("#f0dcc0"), 0, 0, 1000, 72, TextAnchor.MiddleCenter, 10f);
            prompt = Ui.Node(art, "Prompt", 887 - 500, 832 - 30, 1000, 60);
            prompt.pivot = new Vector2(0.5f, 0.5f);
            prompt.anchoredPosition = new Vector2(887, -832);
            promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptText = Ui.Label(prompt, "TAP  OR  PRESS  ANY  KEY", UiFont.Teko, 38, Ui.Hex("#efe4d2"), 0, 0, 1000, 60, TextAnchor.MiddleCenter, 9f);

            startedAt = UiClock.Now;
            Fit();
            Tick(Elapsed());
        }

        float Elapsed()
        {
            if (!UiClock.Animate) return SplashTimeline.FinishedAt;
            return UiClock.Now - startedAt;
        }

        void Fit()
        {
            Rect r = canvasRoot.rect;
            float s = Mathf.Min(r.width / SplashTimeline.ArtW, r.height / SplashTimeline.ArtH) * 0.8f;
            art.localScale = new Vector3(s, s, 1f);
            art.anchoredPosition = Vector2.zero;
        }

        void SpawnPetal(bool init)
        {
            petals.Add(new Petal
            {
                x = init ? (float)rng.NextDouble() * SplashTimeline.ArtW : -40f - (float)rng.NextDouble() * 200f,
                y = (float)rng.NextDouble() * SplashTimeline.ArtH * 0.8f,
                vx = 60f + (float)rng.NextDouble() * 70f, vy = 18f + (float)rng.NextDouble() * 30f,
                r = 6f + (float)rng.NextDouble() * 7f, a = (float)rng.NextDouble() * Mathf.PI * 2f,
                va = ((float)rng.NextDouble() - 0.5f) * 3f, ph = (float)rng.NextDouble() * Mathf.PI * 2f,
            });
        }

        void SparkBurst(float x, float y, int n, float dir)
        {
            for (int i = 0; i < n; i++)
            {
                float a = dir + ((float)rng.NextDouble() - 0.5f) * 1.6f, v = 240f + (float)rng.NextDouble() * 520f;
                sparks.Add(new Spark { x = x, y = y, vx = Mathf.Cos(a) * v, vy = Mathf.Sin(a) * v, life = 0.25f + (float)rng.NextDouble() * 0.3f, white = rng.NextDouble() < 0.4 });
            }
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            if (dismissed)
            {
                float k = (UiClock.Now - dismissedAt) / FadeOut;
                if (!UiClock.Animate || k >= 1f) { gameObject.SetActive(false); return; }
                rootGroup.alpha = 1f - k;
                return;
            }
            Fit();
            if (AnyPress()) { Dismiss(); return; }
            // a fixture drew a chosen moment (ShowAt): the splash stays on it
            if (manualAt >= 0f) return;
            float t = Elapsed();
            float dt = Mathf.Clamp(t - lastT, 0f, 0.05f);
            if (UiClock.Animate) dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            lastT = t;
            Tick(t, dt);
        }

        static bool AnyPress()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return true;
            var gp = Gamepad.current;
            if (gp != null)
            {
                foreach (var c in gp.allControls)
                    if (c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) return true;
            }
            return false;
        }

        /// <summary>Opens the title: the splash fades away in half a second (the JS boot screen's opacity transition).</summary>
        public void Dismiss()
        {
            if (dismissed) return;
            dismissed = true;
            dismissedAt = UiClock.Now;
            if (!UiClock.Animate) gameObject.SetActive(false);
        }

        /// <summary>Draws the splash at a time (the clock-free entry the fixtures use for a frame strip).</summary>
        public void ShowAt(float t)
        {
            gameObject.SetActive(true);
            dismissed = false;
            manualAt = t;
            rootGroup.alpha = 1f;
            Fit();
            ResetSim();
            // run the simulation forward to t in fixed steps, so sparks and petals are where a real run would have them
            float step = 1f / 60f, at = 0f;
            for (int i = 0; i < 1200 && at < t; i++) { at = Mathf.Min(t, at + step); Tick(at, step); }
            Tick(t, 0f);
        }

        void ResetSim()
        {
            petals.Clear(); sparks.Clear();
            for (int i = 0; i < burst.Length; i++) burst[i] = false;
            rng = new System.Random(1234);
            for (int i = 0; i < 9; i++) SpawnPetal(true);
        }

        void Tick(float t, float dt = 0.016f)
        {
            // sub-state: the shake moves the whole art a little
            float shake = SplashTimeline.Shake(t) * 2f;
            float S = art.localScale.x;
            art.anchoredPosition = new Vector2(((float)rng.NextDouble() - 0.5f) * shake * S, ((float)rng.NextDouble() - 0.5f) * shake * S);

            // the scene, fading up with a push-in; Kage's scarf waves
            float sk = SplashTimeline.SceneAlpha(t), zoom = SplashTimeline.SceneZoom(t);
            sceneGroup.alpha = sk;
            sceneWrap.localScale = new Vector3(zoom, zoom, 1f);
            scarf.SetTime(t);

            // petals
            if (petals.Count < 16 && rng.NextDouble() < dt * 3f) SpawnPetal(false);
            for (int i = petals.Count - 1; i >= 0; i--)
            {
                var p = petals[i];
                p.x += (p.vx + Mathf.Sin(t * 1.3f + p.ph) * 30f) * dt;
                p.y += (p.vy + Mathf.Sin(t * 2f + p.ph) * 14f) * dt;
                p.a += p.va * dt;
                if (p.x > SplashTimeline.ArtW + 60f || p.y > SplashTimeline.ArtH + 40f) { petals.RemoveAt(i); continue; }
                petals[i] = p;
            }
            for (int i = 0; i < petalNodes.Count; i++)
            {
                var n = petalNodes[i];
                if (i >= petals.Count) { if (n.gameObject.activeSelf) n.gameObject.SetActive(false); continue; }
                var p = petals[i];
                if (!n.gameObject.activeSelf) n.gameObject.SetActive(true);
                n.anchoredPosition = new Vector2(p.x, -p.y);
                n.localRotation = Quaternion.Euler(0f, 0f, -p.a * Mathf.Rad2Deg);
                n.localScale = new Vector3(p.r / 12f, p.r * 0.55f / 12f * (0.45f + 0.35f * Mathf.Abs(Mathf.Sin(t * 3f + p.ph))), 1f);
                var g = n.GetComponent<ShapeGraphic>();
                g.color = new Color(1f, 1f, 1f, 0.85f * sk);
            }

            // letters with their slashes
            float bump = SplashTimeline.Bump(t);
            letters.localScale = new Vector3(SplashTimeline.LetterScale * bump, SplashTimeline.LetterScale * bump, 1f);
            for (int i = 0; i < 4; i++)
            {
                float k = SplashTimeline.LetterReveal(i, t);
                var g = letterGfx[i];
                g.enabled = k > 0f;
                if (k > 0f)
                {
                    if (k >= 1f) g.SetReveal(0f, true);
                    else g.SetReveal(SplashTimeline.Front(i, k), false);
                }
            }
            var lc = SplashTimeline.LetterCentre;
            for (int i = 0; i < 4; i++)
            {
                var st = SplashTimeline.Slash(i, t);
                if (!st.Visible) { blades[i].Set(new Vector2[0], 0f, 0f); continue; }
                blades[i].Set(SplashTimeline.SlashPoints(i, st.Head, Mathf.Min(st.Head, st.Tail)), SplashTimeline.Slashes[i].Width, Mathf.Clamp01(st.Alpha));
                float u = Mathf.Clamp01((t - SplashTimeline.Slashes[i].T0) / (SplashTimeline.Slashes[i].T1 - SplashTimeline.Slashes[i].T0));
                if (!burst[i] && u > 0.6f)
                {
                    burst[i] = true;
                    var s = SplashTimeline.Slashes[i];
                    var p0 = SplashTimeline.SlashPoints(i, st.Head, st.Head)[0];
                    float dir = s.Line != null ? Mathf.Atan2(s.Line[3] - s.Line[1], s.Line[2] - s.Line[0]) : Mathf.Atan2(1f, 1f);
                    SparkBurst(p0.x, p0.y, 10, dir);
                }
            }
            // the final cut stays as a thin red stroke with a flash, and impact lines
            if (t >= SplashTimeline.CutStart)
            {
                float u = UiEase.OutCubic((t - SplashTimeline.CutStart) / (SplashTimeline.CutEnd - SplashTimeline.CutStart));
                var line = SplashTimeline.CutLine;
                var pts = new Vector2[21];
                for (int i = 0; i <= 20; i++)
                {
                    float v = u * i / 20f;
                    pts[i] = new Vector2(line[0] + (line[2] - line[0]) * v, line[1] + (line[3] - line[1]) * v);
                }
                float glow = Mathf.Clamp01(1f - (t - SplashTimeline.CutEnd) / 0.5f);
                cut.Set(pts, 5f + 5f * glow, 1f);
                if (!burst[4] && u >= 1f)
                {
                    burst[4] = true;
                    SparkBurst((line[0] + line[2]) / 2f, (line[1] + line[3]) / 2f, 26, Mathf.Atan2(line[3] - line[1], line[2] - line[0]));
                }
                float kk = (t - SplashTimeline.CutEnd) / 0.3f;
                float mx = (line[0] + line[2]) / 2f, my = (line[1] + line[3]) / 2f;
                for (int i = 0; i < cutLines.Count; i++)
                {
                    var n = cutLines[i];
                    bool on = kk > 0f && kk < 1f;
                    if (n.gameObject.activeSelf != on) n.gameObject.SetActive(on);
                    if (!on) continue;
                    float a = i / 12f * Mathf.PI * 2f + 0.2f, r0 = 330f + kk * 160f, r1 = r0 + 70f * (1f - kk);
                    Vector2 p0 = new Vector2(mx + Mathf.Cos(a) * r0, my + Mathf.Sin(a) * r0 * 0.55f);
                    Vector2 p1 = new Vector2(mx + Mathf.Cos(a) * r1, my + Mathf.Sin(a) * r1 * 0.55f);
                    n.anchoredPosition = new Vector2(p0.x, -p0.y);
                    float ang = Mathf.Atan2(-(p1.y - p0.y), p1.x - p0.x) * Mathf.Rad2Deg;
                    n.localRotation = Quaternion.Euler(0f, 0f, ang);
                    n.sizeDelta = new Vector2((p1 - p0).magnitude, 3f);
                    n.GetComponent<Image>().color = new Color(1f, 0.957f, 0.925f, 1f - kk);
                }
            }
            else
            {
                cut.Set(new Vector2[0], 0f, 0f);
            }

            // sparks (drawn outside the letters' scale: their positions are mapped through it as the JS does)
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var p = sparks[i];
                p.t += dt;
                if (p.t >= p.life) { sparks.RemoveAt(i); continue; }
                p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.93f; p.vy = p.vy * 0.93f + 300f * dt;
                sparks[i] = p;
            }
            for (int i = 0; i < sparkNodes.Count; i++)
            {
                var n = sparkNodes[i];
                if (i >= sparks.Count) { if (n.gameObject.activeSelf) n.gameObject.SetActive(false); continue; }
                var p = sparks[i];
                if (!n.gameObject.activeSelf) n.gameObject.SetActive(true);
                float sx = lc.x + (p.x - lc.x) * SplashTimeline.LetterScale, sy = lc.y + (p.y - lc.y) * SplashTimeline.LetterScale;
                n.anchoredPosition = new Vector2(sx, -sy);
                var img = n.GetComponent<Image>();
                var c = p.white ? new Color(1f, 0.95f, 0.91f) : new Color(1f, 0.231f, 0.184f);
                c.a = 1f - p.t / p.life;
                img.color = c;
            }

            // the subtitle between two rules
            float mk = SplashTimeline.MarkAlpha(t);
            subtitleGroup.alpha = mk;
            if (mk > 0f)
            {
                float half = 232f;
                float sc = 1.15f - 0.15f * mk;
                subtitle.localScale = new Vector3(sc, sc, 1f);
                float lw = 150f * mk;
                SetRule(ruleL, 887 - half - 40 - lw, lw, mk);
                SetRule(ruleR, 887 + half + 40, lw, mk);
            }
            else { SetRule(ruleL, 0, 0, 0); SetRule(ruleR, 0, 0, 0); }
            promptGroup.alpha = SplashTimeline.PromptAlpha(t);
        }

        static void SetRule(RectTransform r, float x, float w, float alpha)
        {
            r.anchoredPosition = new Vector2(x, -712f);
            r.sizeDelta = new Vector2(Mathf.Max(0f, w), 2f);
            var img = r.GetComponent<Image>();
            var c = img.color; c.a = 0.55f * alpha; img.color = c;
        }
    }
}
