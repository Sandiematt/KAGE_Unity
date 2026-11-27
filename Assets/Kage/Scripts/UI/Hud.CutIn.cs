using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The rage art cut-in band (SAN-82): a slanted sumi-e lacquer banner across the upper third with dual weapon-tinted edges
    /// (prismatic on legendary weapons), a glowing brush kanji seal on the left, a Cinzel Heavy art title with crisp shadow/rim
    /// separation and dynamic fit on the right, a diamond-tipped accent rule, and a tracked condensed subtitle naming the weapon.
    /// Runs on unscaled/capture time (<see cref="UiDeltaTime"/>) so world-freeze and hit-stop never stall it.
    /// </summary>
    public partial class Hud
    {
        RectTransform _cutBand;
        CanvasGroup _cutGroup;
        GradRect _cutBg, _cutWash, _cutSheen;
        GradRect _cutEdgeTop, _cutEdgeBot, _cutHairTop, _cutHairBot;
        GradRect _cutDivider, _cutRule;
        DiamondGraphic _cutRuleGem;
        RadialGlow _cutKanjiGlow;
        Text _cutKanjiShadow, _cutKanji;
        Text _cutNameShadow, _cutNameRim, _cutName;
        Text _cutSubShadow, _cutSub;
        Tracking _cutNameShadowTrack, _cutNameRimTrack, _cutNameTrack;
        Tracking _cutSubShadowTrack, _cutSubTrack;

        float _cutT = 99f, _cutDur = 1f;
        float _cutRuleW = 460f;
        Color _cutTitleCol, _cutTitleHot, _cutGlowCol;
        bool _cutLegendary;

        const float CutBandW = 2300f, CutBandH = 176f;
        const float CutKanjiX = -520f, CutKanjiY = 2f;
        const float CutDividerX = -365f;
        const float CutRightX = 840f;
        const float CutNameY = 20f, CutRuleY = -18f, CutSubY = -44f;
        const float CutMaxTextW = 1100f;
        static readonly Color CutInkDark = new(0.016f, 0.011f, 0.015f, 1f);
        static readonly Color CutShadowCol = new(0.015f, 0.008f, 0.012f, 0.92f);
        static readonly Color CutLegendaryGold = new(1f, 0.925f, 0.761f, 1f);
        static readonly Color CutLegendaryKanji = new(1f, 0.965f, 0.875f, 1f);

        /// <summary>The cut-in band is on screen.</summary>
        public bool CutInShowing => _cutT < _cutDur;
        public string CutInName => _cutName != null ? _cutName.text : null;
        public string CutInKanji => _cutKanji != null ? _cutKanji.text : null;
        public string CutInCaption => _cutSub != null ? _cutSub.text : null;

        void BuildCutIn()
        {
            var go = new GameObject("CutIn", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(_root, false);
            _cutBand = (RectTransform)go.transform;
            Place(_cutBand, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(CutBandW, CutBandH));
            _cutBand.localRotation = Quaternion.Euler(0f, 0f, 2.0f);
            _cutGroup = go.AddComponent<CanvasGroup>();
            _cutGroup.blocksRaycasts = false;
            _cutGroup.interactable = false;

            // Layered sumi-e lacquer backdrop: deep weapon-tinted ink + left-center elemental wash + vertical sheen
            _cutBg = CutGrad("Bg", _cutBand, true);
            Stretch(_cutBg.rectTransform);
            _cutWash = CutGrad("Wash", _cutBand, true);
            Stretch(_cutWash.rectTransform);
            _cutSheen = CutGrad("Sheen", _cutBand, false);
            Stretch(_cutSheen.rectTransform);

            // Dual top & bottom framing lines (4px outer bar + 1.5px inner hot hairline inset 7px)
            _cutEdgeTop = CutBar("EdgeTop", _cutBand, top: true, inset: 0f, height: 4f);
            _cutEdgeBot = CutBar("EdgeBot", _cutBand, top: false, inset: 0f, height: 4f);
            _cutHairTop = CutBar("HairTop", _cutBand, top: true, inset: 7f, height: 1.5f);
            _cutHairBot = CutBar("HairBot", _cutBand, top: false, inset: 7f, height: 1.5f);

            // Left-zone kanji seal: soft oval aura + shadow/outline backing + crisp calligraphy foreground
            var glowRt = CutNode("KanjiGlow", _cutBand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CutKanjiX, 0f), new Vector2(210f, 210f));
            glowRt.localScale = new Vector3(1.65f, 0.92f, 1f);
            _cutKanjiGlow = glowRt.gameObject.AddComponent<RadialGlow>();
            _cutKanjiGlow.raycastTarget = false;
            _cutKanjiGlow.power = 1.85f;

            _cutKanjiShadow = CutText("KanjiShadow", _cutBand, UiFont.Kanji, 112, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(CutKanjiX + 4f, CutKanjiY - 5f), new Vector2(360f, 160f), CutShadowCol);
            var ko = _cutKanjiShadow.gameObject.AddComponent<Outline>();
            ko.effectColor = new Color(0.015f, 0.008f, 0.012f, 0.65f);
            ko.effectDistance = new Vector2(2f, -2f);

            _cutKanji = CutText("Kanji", _cutBand, UiFont.Kanji, 112, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(CutKanjiX, CutKanjiY), new Vector2(360f, 160f), Color.white);

            // Subtle slanted vertical blade hairline separating the kanji seal from the title block
            _cutDivider = CutGrad("Divider", _cutBand, false);
            Place(_cutDivider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(CutDividerX, 0f), new Vector2(2f, 98f));
            _cutDivider.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);

            // Right-aligned title block: separate shadow + dark rim (never rainbow-overwritten) + foreground Cinzel Heavy
            _cutNameShadow = CutText("NameShadow", _cutBand, UiFont.TitleHeavy, 72, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(CutRightX + 4f, CutNameY - 5f), new Vector2(CutMaxTextW, 96f), new Color(0.012f, 0.006f, 0.012f, 0.85f));
            _cutNameShadowTrack = _cutNameShadow.gameObject.AddComponent<Tracking>();

            _cutNameRim = CutText("NameRim", _cutBand, UiFont.TitleHeavy, 72, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(CutRightX, CutNameY), new Vector2(CutMaxTextW, 96f), CutShadowCol);
            _cutNameRimTrack = _cutNameRim.gameObject.AddComponent<Tracking>();
            // Outline added AFTER Tracking so glyphs are spaced first, then outlined cleanly
            var nrOutline = _cutNameRim.gameObject.AddComponent<Outline>();
            nrOutline.effectColor = CutShadowCol;
            nrOutline.effectDistance = new Vector2(2.2f, -2.2f);

            _cutName = CutText("Name", _cutBand, UiFont.TitleHeavy, 72, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(CutRightX, CutNameY), new Vector2(CutMaxTextW, 96f), Color.white);
            _cutNameTrack = _cutName.gameObject.AddComponent<Tracking>();

            // Accent horizontal rule fading left + crisp diamond terminator at the right edge
            _cutRule = CutGrad("Rule", _cutBand, true);
            var rr = _cutRule.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(1f, 0.5f);
            rr.anchoredPosition = new Vector2(CutRightX - 10f, CutRuleY);
            rr.sizeDelta = new Vector2(_cutRuleW, 2f);

            var gemRt = CutNode("RuleGem", _cutBand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CutRightX - 3f, CutRuleY), new Vector2(12f, 12f));
            _cutRuleGem = gemRt.gameObject.AddComponent<DiamondGraphic>();
            _cutRuleGem.raycastTarget = false;
            _cutRuleGem.Set(6f, 0f, Color.white, Color.clear);

            // Right-aligned subtitle in Barlow Condensed (full macron & ampersand coverage) with dark drop shadow
            _cutSubShadow = CutText("SubShadow", _cutBand, UiFont.Cond, 24, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(CutRightX + 2f, CutSubY - 2f), new Vector2(CutMaxTextW, 38f), CutShadowCol);
            _cutSubShadowTrack = _cutSubShadow.gameObject.AddComponent<Tracking>();
            _cutSubShadowTrack.px = 4.5f;

            _cutSub = CutText("Sub", _cutBand, UiFont.Cond, 24, TextAnchor.MiddleRight,
                new Vector2(1f, 0.5f), new Vector2(CutRightX, CutSubY), new Vector2(CutMaxTextW, 38f), Color.white);
            _cutSubTrack = _cutSub.gameObject.AddComponent<Tracking>();
            _cutSubTrack.px = 4.5f;

            go.SetActive(false);
        }

        /// <summary>
        /// A rage art's cut-in band (SAN-82): unfolds across the upper third with the art's <paramref name="name"/>, its
        /// <paramref name="kanji"/> and weapon <paramref name="caption"/> in the art's colours (optional <paramref name="deep"/>
        /// tint for the ink backdrop), holds for <paramref name="seconds"/> and collapses cleanly. Runs on unscaled/capture time.
        /// </summary>
        public void CutIn(string name, string kanji, string caption, Color col, Color hot, float seconds, bool legendary = false, Color? deep = null)
        {
            if (_cutBand == null) BuildCutIn();
            string title = name ?? "";
            string glyph = kanji ?? "";
            string sub = caption ?? "";
            Color ink = deep ?? Color.Lerp(col, Color.black, 0.85f);

            _cutLegendary = legendary;
            _cutTitleCol = legendary ? Color.white : Color.Lerp(col, hot, 0.35f);
            _cutTitleHot = Color.Lerp(hot, Color.white, 0.4f);
            Color subCol = legendary ? CutLegendaryGold : hot;
            Color kanjiCol = legendary ? CutLegendaryKanji : Color.Lerp(hot, Color.white, 0.25f);
            _cutGlowCol = col;

            // 1. Backdrop gradients (deep weapon-tinted ink + soft elemental wash + top lacquer sheen)
            Color inkEdge = Color.Lerp(ink, CutInkDark, 0.42f).WithA(0.95f);
            Color inkCenter = Color.Lerp(ink, CutInkDark, 0.22f).WithA(0.94f);
            _cutBg.Set(true, new[] { inkEdge, inkCenter, inkEdge });
            _cutWash.Set(true, new[] { col.WithA(0.03f), col.WithA(0.19f), col.WithA(0.12f), col.WithA(0.05f) });
            _cutSheen.Set(false, new[] { hot.WithA(0.09f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.26f) });

            // 2. Dual edges: 9-stop rainbow prism on legendary, glowing weapon bar otherwise
            if (legendary)
            {
                var prism = new[]
                {
                    new Color(1f, 1f, 1f, 0.35f), Color.white, Color.white, Color.white, Color.white,
                    Color.white, Color.white, Color.white, new Color(1f, 1f, 1f, 0.35f),
                };
                _cutEdgeTop.Set(true, prism);
                _cutEdgeBot.Set(true, prism);
            }
            else
            {
                Color midHi = Color.Lerp(col, hot, 0.45f);
                var edge = new[]
                {
                    col.WithA(0.32f), col, midHi, col, hot, col, midHi, col, col.WithA(0.32f),
                };
                _cutEdgeTop.Set(true, edge);
                _cutEdgeBot.Set(true, edge);
            }
            Legendary(_cutEdgeTop, legendary);
            Legendary(_cutEdgeBot, legendary);

            Color hairCol = legendary ? CutLegendaryGold : hot;
            var hair = new[] { hairCol.WithA(0.12f), hairCol.WithA(0.58f), hairCol.WithA(0.58f), hairCol.WithA(0.12f) };
            _cutHairTop.Set(true, hair);
            _cutHairBot.Set(true, hair);
            _cutDivider.Set(false, new[] { hairCol.WithA(0f), hairCol.WithA(0.62f), hairCol.WithA(0f) });

            // 3. Kanji seal with unified matched-pair glyph font
            int kSize = glyph.Length <= 2 ? 112 : glyph.Length == 3 ? 92 : 76;
            Font kFont = RageKanjiFont(glyph, kSize) ?? Ui.FontOf(UiFont.Kanji);
            _cutKanji.font = _cutKanjiShadow.font = kFont;
            _cutKanji.fontSize = _cutKanjiShadow.fontSize = kSize;
            _cutKanji.text = _cutKanjiShadow.text = glyph;
            _cutKanji.color = kanjiCol;
            _cutKanjiGlow.color = col.WithA(legendary ? 0.28f : 0.30f);

            // 4. Title: dynamic size & tracking with hard width clamp so it never crosses into the kanji zone
            int titleSize = title.Length <= 12 ? 72 : title.Length <= 16 ? 64 : 56;
            float titleTrack = title.Length <= 12 ? 5f : title.Length <= 16 ? 4f : 3f;
            _cutName.text = _cutNameRim.text = _cutNameShadow.text = title;
            _cutName.fontSize = titleSize;
            int visibleTitleChars = CountVisibleChars(title);
            float titleW = _cutName.preferredWidth + titleTrack * Mathf.Max(0, visibleTitleChars - 1);
            while (titleSize > 38 && titleW > CutMaxTextW)
            {
                titleSize -= 2;
                titleTrack = Mathf.Max(1.5f, titleTrack * 0.94f);
                _cutName.fontSize = titleSize;
                titleW = _cutName.preferredWidth + titleTrack * Mathf.Max(0, visibleTitleChars - 1);
            }
            _cutNameRim.fontSize = _cutNameShadow.fontSize = titleSize;
            _cutNameTrack.px = _cutNameRimTrack.px = _cutNameShadowTrack.px = titleTrack;
            // Apply Rainbow ONLY to the foreground _cutName (never to _cutNameRim or _cutNameShadow)
            Legendary(_cutName, legendary);
            _cutName.color = _cutTitleCol;

            // 5. Subtitle: Barlow Condensed with width clamp and zero rainbow smear
            int subSize = 24;
            float subTrack = 4.5f;
            _cutSub.text = _cutSubShadow.text = sub;
            _cutSub.fontSize = subSize;
            int visibleSubChars = CountVisibleChars(sub);
            float subW = _cutSub.preferredWidth + subTrack * Mathf.Max(0, visibleSubChars - 1);
            while (subSize > 18 && subW > CutMaxTextW - 20f)
            {
                subSize -= 1;
                subTrack = Mathf.Max(2f, subTrack * 0.92f);
                _cutSub.fontSize = subSize;
                subW = _cutSub.preferredWidth + subTrack * Mathf.Max(0, visibleSubChars - 1);
            }
            _cutSubShadow.fontSize = subSize;
            _cutSubTrack.px = _cutSubShadowTrack.px = subTrack;
            _cutSub.color = subCol;

            // 6. Accent rule matched cleanly to the title/subtitle visual span
            _cutRuleW = Mathf.Clamp(Mathf.Max(titleW, subW) + 28f, 340f, CutMaxTextW);
            Color ruleMid = legendary ? new Color(1f, 0.84f, 0.48f, 0.78f) : col.WithA(0.75f);
            Color ruleTip = legendary ? CutLegendaryGold.WithA(0.95f) : hot.WithA(0.95f);
            _cutRule.Set(true, new[] { ruleTip.WithA(0f), ruleMid, ruleTip });
            _cutRuleGem.SetColors(ruleTip, Color.clear);

            _cutT = 0f;
            _cutDur = Mathf.Max(0.3f, seconds);
            _cutBand.SetAsLastSibling();
            _cutBand.gameObject.SetActive(true);
            UpdateCutIn(0f);
        }

        void UpdateCutIn(float dt)
        {
            if (_cutBand == null) return;
            if (_cutT >= _cutDur) { if (_cutBand.gameObject.activeSelf) _cutBand.gameObject.SetActive(false); return; }
            _cutT += dt;
            float t = Mathf.Min(_cutT, _cutDur);
            float u = Mathf.Clamp01(t / _cutDur);

            float inBand = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(t / 0.14f));
            float inText = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(t / 0.18f));
            float inSub = KMath.Apply(Ease.OutCubic, Mathf.Clamp01((t - 0.03f) / 0.18f));
            float inRule = KMath.Apply(Ease.OutExpo, Mathf.Clamp01((t - 0.02f) / 0.20f));
            float outK = KMath.Apply(Ease.InCubic, Mathf.Clamp01((t - (_cutDur - 0.16f)) / 0.16f));

            float bandScaleY = Mathf.Max(0.001f, inBand * (1f - outK));
            _cutBand.localScale = new Vector3(1f, bandScaleY, 1f);
            _cutGroup.alpha = Mathf.Clamp01(inBand * 1.45f) * (1f - outK);

            // Staggered entrance + gentle opposing glide during hold
            float kanjiX = CutKanjiX - (1f - inText) * 80f + u * 14f;
            _cutKanji.rectTransform.anchoredPosition = new Vector2(kanjiX, CutKanjiY);
            _cutKanjiShadow.rectTransform.anchoredPosition = new Vector2(kanjiX + 4f, CutKanjiY - 5f);
            _cutKanjiGlow.rectTransform.anchoredPosition = new Vector2(kanjiX, 0f);
            _cutKanjiGlow.color = _cutGlowCol.WithA((_cutLegendary ? 0.24f : 0.26f) + 0.08f * Mathf.Sin(u * Mathf.PI));
            float kanjiPop = Mathf.Lerp(1.16f, 1f, inText);
            _cutKanji.rectTransform.localScale = _cutKanjiShadow.rectTransform.localScale = new Vector3(kanjiPop, kanjiPop, 1f);

            float nameX = CutRightX + (1f - inText) * 200f - u * 18f;
            _cutName.rectTransform.anchoredPosition = new Vector2(nameX, CutNameY);
            _cutNameRim.rectTransform.anchoredPosition = new Vector2(nameX, CutNameY);
            _cutNameShadow.rectTransform.anchoredPosition = new Vector2(nameX + 4f, CutNameY - 5f);
            if (!_cutLegendary)
                _cutName.color = Color.Lerp(_cutTitleHot, _cutTitleCol, Mathf.Clamp01(t / 0.14f));

            float subX = CutRightX + (1f - inSub) * 260f - u * 18f;
            _cutSub.rectTransform.anchoredPosition = new Vector2(subX, CutSubY);
            _cutSubShadow.rectTransform.anchoredPosition = new Vector2(subX + 2f, CutSubY - 2f);

            float ruleX = CutRightX - u * 18f;
            _cutRule.rectTransform.anchoredPosition = new Vector2(ruleX - 10f, CutRuleY);
            _cutRule.rectTransform.sizeDelta = new Vector2(Mathf.Max(2f, _cutRuleW * inRule), 2f);
            _cutRuleGem.rectTransform.anchoredPosition = new Vector2(ruleX - 3f, CutRuleY);
            float gemS = Mathf.Clamp01(inRule * 1.3f);
            _cutRuleGem.rectTransform.localScale = new Vector3(gemS, gemS, 1f);

            if (_cutT >= _cutDur) _cutBand.gameObject.SetActive(false);
        }

        static int CountVisibleChars(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) n++;
            return n;
        }

        static RectTransform CutNode(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static GradRect CutGrad(string name, Transform parent, bool horizontal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GradRect>();
            g.raycastTarget = false;
            g.Set(horizontal, new[] { Color.clear, Color.clear });
            return g;
        }

        static GradRect CutBar(string name, Transform parent, bool top, float inset, float height)
        {
            var g = CutGrad(name, parent, true);
            var rt = g.rectTransform;
            float ay = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, ay);
            rt.anchorMax = new Vector2(1f, ay);
            rt.pivot = new Vector2(0.5f, ay);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, top ? -inset : inset);
            return g;
        }

        static Text CutText(string name, Transform parent, UiFont font, int size, TextAnchor align, Vector2 pivot, Vector2 pos, Vector2 box, Color c)
        {
            var rt = CutNode(name, parent, new Vector2(0.5f, 0.5f), pivot, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Ui.FontOf(font);
            t.fontSize = size;
            t.alignment = align;
            t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            return t;
        }
    }
}
