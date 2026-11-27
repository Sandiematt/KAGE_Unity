using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// World-anchored Japanese brush-calligraphy HUD callouts (PARRY, PERFECT PARRY, DODGE, COUNTER,
    /// IAI COUNTER, CLASH, PUNISH, GUARD BREAK, PARRIED, GUARD BROKEN) matching KAGE's Combat Callout UI design.
    /// Renders hand-brushed RGBA wordmark textures (<c>Resources/UI/Img/callout_*</c>) right above the blade
    /// contact / clash / dodge point with minimal 3-dot timing indicators beneath defensive/evasive callouts
    /// and a crisp 3-phase animation (Pop-in 0.08–0.12 s → Hold 0.24–0.30 s → Fade-out 0.46–0.52 s).
    /// </summary>
    public partial class Hud
    {
        RectTransform _calloutsRoot;
        CalloutSlotView[] _calloutSlots;

        const float CalloutStackStep = 46f;
        const float CalloutDotRadius = 3.2f;
        const float CalloutDotSpacing = 11.5f;

        sealed class CalloutSlotView
        {
            public RectTransform root;
            public CanvasGroup group;
            public RadialGlow darkHalo;
            public RadialGlow accentGlow;
            public RawImage shadowImg;
            public RawImage mainImg;
            public Text fallbackText;
            public RectTransform dotsRow;
            public readonly RingDotGraphic[] dots = new RingDotGraphic[3];
            public readonly RadialGlow[] dotGlows = new RadialGlow[3];
            public int boundId = -1;
            public float width;
            public float height;
            public bool hasDots;
        }

        /// <summary>Pushes a key combat HUD callout at <paramref name="worldPos"/> (the strike/parry/clash point).</summary>
        public void ShowCallout(CombatCalloutKind kind, Vector2 worldPos)
        {
            _model.Callouts.Push(kind, worldPos, true);
            if (_calloutSlots != null) ApplyCallouts(_model.Callouts);
        }

        /// <summary>Pushes a key combat HUD callout at the upper-centre fallback when no world contact point is given.</summary>
        public void ShowCallout(CombatCalloutKind kind)
        {
            _model.Callouts.Push(kind);
            if (_calloutSlots != null) ApplyCallouts(_model.Callouts);
        }

        /// <summary>Active combat callout count on the HUD right now.</summary>
        public int ActiveCalloutCount => _model.Callouts.Count;

        /// <summary>The newest combat callout label currently on screen, or null.</summary>
        public string LatestCalloutLabel => _model.Callouts.LatestLabel;

        void BuildCallouts()
        {
            _calloutsRoot = Ui.Fill(_js, "Callouts");
            _calloutSlots = new CalloutSlotView[HudCalloutQueue.MaxActive];
            for (int i = 0; i < _calloutSlots.Length; i++)
                _calloutSlots[i] = BuildCalloutSlot(i);
        }

        CalloutSlotView BuildCalloutSlot(int index)
        {
            var v = new CalloutSlotView();
            var rt = Ui.Node(_calloutsRoot, "Callout_" + index, 0f, 0f, 150f, 44f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            v.root = rt;

            v.group = rt.gameObject.AddComponent<CanvasGroup>();
            v.group.blocksRaycasts = false;
            v.group.interactable = false;

            // Soft radial ink contrast behind the brush lettering (no hard box edges, keeps lettering sharp over sparks)
            var darkRt = CenteredChild(rt, "InkHalo", 0f, 0f, 180f, 76f);
            v.darkHalo = darkRt.gameObject.AddComponent<RadialGlow>();
            v.darkHalo.raycastTarget = false;
            v.darkHalo.power = 2.0f;
            v.darkHalo.color = new Color(0.02f, 0.01f, 0.015f, 0.52f);

            // Warm / accent glow aura (brightest on PERFECT PARRY)
            var glowRt = CenteredChild(rt, "AccentGlow", 0f, 0f, 190f, 84f);
            v.accentGlow = glowRt.gameObject.AddComponent<RadialGlow>();
            v.accentGlow.raycastTarget = false;
            v.accentGlow.power = 2.1f;
            v.accentGlow.color = Color.clear;

            // Subtle crisp dark drop-shadow copy of the hand-brushed sprite
            var shRt = CenteredChild(rt, "BrushShadow", 1.5f, -2.0f, 150f, 44f);
            v.shadowImg = shRt.gameObject.AddComponent<RawImage>();
            v.shadowImg.raycastTarget = false;
            v.shadowImg.color = new Color(0.02f, 0.01f, 0.012f, 0.78f);

            // Primary hand-brushed calligraphy sprite
            var mainRt = CenteredChild(rt, "BrushWordmark", 0f, 0f, 150f, 44f);
            v.mainImg = mainRt.gameObject.AddComponent<RawImage>();
            v.mainImg.raycastTarget = false;
            v.mainImg.color = Color.white;

            // Clean fallback text if texture is absent
            var fbRt = CenteredChild(rt, "FallbackLabel", 0f, 0f, 240f, 40f);
            v.fallbackText = fbRt.gameObject.AddComponent<Text>();
            v.fallbackText.font = Ui.FontOf(UiFont.TitleHeavy);
            v.fallbackText.fontSize = 24;
            v.fallbackText.alignment = TextAnchor.MiddleCenter;
            v.fallbackText.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.fallbackText.verticalOverflow = VerticalWrapMode.Overflow;
            v.fallbackText.raycastTarget = false;
            fbRt.gameObject.SetActive(false);

            // Minimal 3-dot timing row directly beneath PARRY / PERFECT PARRY / DODGE
            v.dotsRow = CenteredChild(rt, "TimingDots", 0f, -24f, CalloutDotSpacing * 3f, 12f);
            for (int d = 0; d < 3; d++)
            {
                float dx = (d - 1) * CalloutDotSpacing;
                var dgRt = CenteredChild(v.dotsRow, "DotGlow" + d, dx, 0f, 16f, 16f);
                var dg = dgRt.gameObject.AddComponent<RadialGlow>();
                dg.raycastTarget = false;
                dg.power = 1.9f;
                dg.color = Color.clear;
                v.dotGlows[d] = dg;

                var dotRt = CenteredChild(v.dotsRow, "Dot" + d, dx, 0f, CalloutDotRadius * 2f + 2f, CalloutDotRadius * 2f + 2f);
                var dot = dotRt.gameObject.AddComponent<RingDotGraphic>();
                dot.raycastTarget = false;
                v.dots[d] = dot;
            }
            v.dotsRow.gameObject.SetActive(false);

            rt.gameObject.SetActive(false);
            return v;
        }

        static RectTransform CenteredChild(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        Vector2 WorldToJsPoint(Vector2 world)
        {
            float stageW = _js != null && _js.rect.width > 1f ? _js.rect.width : HudLayoutRects.StageW;
            float stageH = _js != null && _js.rect.height > 1f ? _js.rect.height : 720f;
            if (_cam == null || _js == null) return new Vector2(stageW * 0.5f, -stageH * 0.36f);

            Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector3 sp = _cam.WorldToScreenPoint(world);
            if (_cam.targetTexture != null && uiCam == null)
                sp = new Vector3(sp.x / _cam.targetTexture.width * Screen.width, sp.y / _cam.targetTexture.height * Screen.height, sp.z);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_js, sp, uiCam, out var lp);
            if (!float.IsFinite(lp.x) || !float.IsFinite(lp.y)) return new Vector2(stageW * 0.5f, -stageH * 0.36f);
            return lp;
        }

        void ApplyCallouts(HudCalloutQueue queue)
        {
            if (_calloutSlots == null) return;
            var entries = queue.Entries;
            float stageW = _js != null && _js.rect.width > 1f ? _js.rect.width : HudLayoutRects.StageW;
            float stageH = _js != null && _js.rect.height > 1f ? _js.rect.height : 720f;

            for (int i = 0; i < _calloutSlots.Length; i++)
            {
                var v = _calloutSlots[i];
                bool on = i < entries.Count;
                if (v.root.gameObject.activeSelf != on) v.root.gameObject.SetActive(on);
                if (!on) { v.boundId = -1; continue; }

                var e = entries[i];
                var spec = CombatCalloutCatalog.Get(e.Kind);
                if (v.boundId != e.Id) BindCallout(v, e, spec);

                float alpha = e.Alpha;
                float scale = e.Scale;
                float pulse = e.GlowPulse;

                v.group.alpha = alpha;

                // Project right to where the parry / clash / dodge happened
                Vector2 lp = e.HasWorldPos
                    ? WorldToJsPoint(e.WorldPos + Vector2.up * e.WorldRiseY)
                    : new Vector2(stageW * 0.5f, -stageH * 0.36f + e.WorldRiseY * 32f);

                // Subtle brush sweep along X during pop-in and clean upward lift when stacked
                lp.x += e.SweepX;
                lp.y += e.VisualStack * CalloutStackStep;

                float halfW = v.width * 0.5f + 12f;
                lp.x = Mathf.Clamp(lp.x, halfW, Mathf.Max(halfW, stageW - halfW));
                lp.y = Mathf.Clamp(lp.y, -(stageH - 86f), -114f);

                v.root.anchoredPosition = lp;
                v.root.localScale = new Vector3(scale, scale, 1f);
                v.root.localRotation = Quaternion.identity;

                // Impact aura glow (strongest golden burst on PERFECT PARRY, subtle on other actions)
                float glowBase = e.Kind == CombatCalloutKind.PerfectParry ? 0.34f : 0.14f;
                float glowA = glowBase * (0.35f + 0.65f * pulse);
                v.accentGlow.color = A(spec.GlowColor, glowA);

                // Subtle impact brightness pulse on the callout dots
                if (v.hasDots && spec.GoldDots)
                {
                    float gA = 0.45f + 0.40f * pulse;
                    for (int d = 0; d < 3; d++)
                        v.dotGlows[d].color = d < spec.FilledDots ? A(spec.DotColor, gA) : Color.clear;
                }
            }
        }

        void BindCallout(CalloutSlotView v, in HudCalloutEntry e, in CombatCalloutSpec spec)
        {
            v.boundId = e.Id;

            Texture2D tex = Ui.Tex(spec.SpriteId);
            bool hasTex = tex != null && tex.width > 0 && tex.height > 0;
            v.mainImg.gameObject.SetActive(hasTex);
            v.shadowImg.gameObject.SetActive(hasTex);
            v.fallbackText.gameObject.SetActive(!hasTex);

            float h = spec.DisplayHeight;
            float w;
            if (hasTex)
            {
                w = h * (tex.width / (float)tex.height);
                v.mainImg.texture = tex;
                v.shadowImg.texture = tex;
                v.mainImg.rectTransform.sizeDelta = new Vector2(w, h);
                v.shadowImg.rectTransform.sizeDelta = new Vector2(w, h);
            }
            else
            {
                v.fallbackText.text = spec.Label;
                v.fallbackText.color = spec.GlowColor;
                w = Mathf.Max(90f, v.fallbackText.preferredWidth + 24f);
            }

            v.width = w;
            v.height = h;
            v.root.sizeDelta = new Vector2(w, h);
            v.darkHalo.rectTransform.sizeDelta = new Vector2(w * 1.28f, h * 1.55f);
            v.accentGlow.rectTransform.sizeDelta = new Vector2(w * 1.38f, h * 1.75f);

            // Minimal 3-dot timing indicator directly below defensive/evasive callouts
            v.hasDots = spec.FilledDots > 0;
            v.dotsRow.gameObject.SetActive(v.hasDots);
            if (v.hasDots)
            {
                v.dotsRow.anchoredPosition = new Vector2(0f, -(h * 0.5f + 5.5f));
                Color ringCol = spec.GoldDots ? A(spec.DotColor, 0.85f) : new Color(0.95f, 0.93f, 0.88f, 0.72f);
                for (int d = 0; d < 3; d++)
                {
                    bool filled = d < spec.FilledDots;
                    v.dots[d].Set(CalloutDotRadius, 1.15f, filled ? spec.DotColor : Color.clear, filled ? spec.DotColor : ringCol);
                    v.dotGlows[d].color = filled && spec.GoldDots ? A(spec.DotColor, 0.65f) : Color.clear;
                }
            }
        }
    }

    /// <summary>
    /// Crisp procedural circle dot that renders either as a clean hollow ring (Ready / Neutral),
    /// or as a solid filled circle (Active Window / Perfect Timing), with zero texture aliasing.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class RingDotGraphic : MaskableGraphic
    {
        float _radius = 3.5f;
        float _thickness = 1.2f;
        Color _fill = Color.clear;
        Color _ring = Color.white;

        public void Set(float radius, float thickness, Color fill, Color ring)
        {
            _radius = Mathf.Max(1f, radius);
            _thickness = Mathf.Clamp(thickness, 0.5f, _radius);
            _fill = fill;
            _ring = ring;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int segs = 20;
            Vector2 c = rectTransform.rect.center;
            float outer = _radius;
            float inner = Mathf.Max(0f, _radius - _thickness);

            // Solid core disc when filled
            Color fc = _fill * color;
            if (fc.a > 0.004f)
            {
                int b = vh.currentVertCount;
                vh.AddVert(c, fc, Vector2.zero);
                for (int i = 0; i < segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer, fc, Vector2.zero);
                }
                for (int i = 0; i < segs; i++)
                    vh.AddTriangle(b, b + 1 + i, b + 1 + (i + 1) % segs);
                return;
            }

            // Crisp hollow ring when neutral / unfilled
            Color rc = _ring * color;
            if (rc.a <= 0.004f) return;
            int rb = vh.currentVertCount;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + dir * outer, rc, Vector2.zero);
                vh.AddVert(c + dir * inner, rc, Vector2.zero);
            }
            for (int i = 0; i < segs; i++)
            {
                int i0 = rb + i * 2;
                int i1 = rb + ((i + 1) % segs) * 2;
                vh.AddTriangle(i0, i1, i1 + 1);
                vh.AddTriangle(i0, i1 + 1, i0 + 1);
            }
        }
    }
}
