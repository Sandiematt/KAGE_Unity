using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// What a rage art puts on the screen besides its cut-in band (docs/weapon-styles.md, the JS <c>RageArts.finish</c> stamp and
    /// <c>drawSky</c>/<c>drawScreen</c> grade):
    /// <list type="bullet">
    /// <item><b>The finisher's word</b> (<see cref="ArtStamp"/>): the art's call ("MIZUCHI", "SILENCE", "RAIJIN", "HELL SPLIT",
    /// "REQUIEM", "MOONFALL", "SHADOWFALL", "EARTHSPLITTER") slammed onto the screen at the finisher in the brush font, its kanji
    /// watermark behind it, a tapered sumi-e brush underline beneath it and its tracked art/weapon caption underneath, anchored by a
    /// soft dark-ink backing so every weapon's palette and legendary rainbow pop crisply over busy VFX.</item>
    /// <item><b>The grade</b> (<see cref="ArtGrade"/>): a full-screen veil in the art's deep colour under every HUD panel while the art
    /// plays (the JS arena change: the blood moon's dark, the violet silence, the hell sky, the flood's blue), eased in and out.</item>
    /// </list>
    /// Both run on unscaled time, like the banners, so hit-stop and slow motion never stall them.
    /// </summary>
    public partial class Hud
    {
        RectTransform _stamp;
        RadialGlow _stampInkGlow, _stampColGlow;
        ShapeGraphic _stampBrushShadow, _stampBrush;
        Text _stampKanjiShadow, _stampKanji;
        Text _stampWordShadow, _stampWordOutline, _stampWord;
        Text _stampSubShadow, _stampSub;
        Outline _stampWordShadowFx, _stampWordOutlineFx, _stampSubShadowFx;
        float _stampT = 99f, _stampDur = 1.25f;
        Color _stampCol, _stampHot, _stampDeep;
        bool _stampLegendary;
        Image _grade;
        Color _gradeCol;
        float _gradeNow, _gradeTarget;

        /// <summary>The word sits in the upper third, over the fighters' heads, clear of the top HUD bars.</summary>
        const float StampY = 225f;
        const float StampW = 1500f, StampH = 420f;
        /// <summary>Brush underline vertical centre in y-down Ui coordinates inside the 1500 x 420 stamp container (62 px below centre).</summary>
        const float BrushYDown = StampH * 0.5f + 62f;

        static readonly Dictionary<int, Font> _osKanjiFonts = new();
        static readonly string[] _osKanjiFamilies =
        {
            "Yu Mincho", "MS PMincho", "MS Mincho", "BIZ UDMincho", "Noto Serif CJK JP", "Yu Gothic UI", "Meiryo", "MS Gothic"
        };

        /// <summary>The finisher's word is on screen.</summary>
        public bool ArtStampShowing => _stampT < _stampDur;
        public string ArtStampWord => _stampWord != null ? _stampWord.text : null;
        public string ArtStampCaption => _stampSub != null ? _stampSub.text : null;
        public string ArtStampKanji => _stampKanji != null ? _stampKanji.text : null;
        /// <summary>How strong the art's grade is now, 0..1.</summary>
        public float ArtGradeAmount => _gradeNow;

        /// <summary>
        /// Picks a single coherent kanji font that contains every character in <paramref name="kanji"/> so pairs never mix
        /// brush and gothic fallback glyphs: prefers Yuji Boku (<see cref="UiFont.Kanji"/>), then Yuji Syuku (<see cref="UiFont.Brush"/>),
        /// then a cached OS Mincho serif font.
        /// </summary>
        public static Font RageKanjiFont(string kanji, int size)
        {
            var boku = Ui.FontOf(UiFont.Kanji);
            if (string.IsNullOrEmpty(kanji) || FontHasAll(boku, kanji)) return boku;
            var syuku = Ui.FontOf(UiFont.Brush);
            if (FontHasAll(syuku, kanji)) return syuku;
            int key = Mathf.Max(16, size);
            if (!_osKanjiFonts.TryGetValue(key, out var os) || os == null)
            {
                os = Font.CreateDynamicFontFromOSFont(_osKanjiFamilies, key);
                _osKanjiFonts[key] = os;
            }
            return os != null ? os : Ui.FontOf(UiFont.Mincho);
        }

        static bool FontHasAll(Font f, string s)
        {
            if (f == null || string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (!char.IsWhiteSpace(c) && !f.HasCharacter(c)) return false;
            return true;
        }

        static void PlaceCentered(RectTransform rt, float dx, float dyUp, float w, float h) =>
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(dx, dyUp), new Vector2(w, h));

        void EnsureArtStamp()
        {
            if (_stamp != null) return;
            var go = new GameObject("ArtStamp", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(_root, false);
            _stamp = (RectTransform)go.transform;
            Place(_stamp, new Vector2(0.5f, 0.5f), new Vector2(0f, StampY), new Vector2(StampW, StampH));

            // 1. Soft dark-ink elliptical backing + subtle elemental halo (scaled square RadialGlow -> wide ellipse)
            _stampInkGlow = Ui.Glow(_stamp, 0, 0, 175f, new Color(0.015f, 0.008f, 0.015f, 0.70f), 1.45f);
            PlaceCentered(_stampInkGlow.rectTransform, 0f, -14f, 350f, 350f);
            _stampInkGlow.rectTransform.localScale = new Vector3(1320f / 350f, 1f, 1f);

            _stampColGlow = Ui.Glow(_stamp, 0, 0, 150f, Color.white, 1.8f);
            PlaceCentered(_stampColGlow.rectTransform, 0f, 8f, 300f, 300f);
            _stampColGlow.rectTransform.localScale = new Vector3(920f / 300f, 1f, 1f);

            // 2. Kanji watermark behind the finisher word (separate dark shadow so it stays legible)
            _stampKanjiShadow = Ui.Label(_stamp, "", UiFont.Kanji, 240, Color.black, 0, 0, StampW, 340, TextAnchor.MiddleCenter);
            PlaceCentered(_stampKanjiShadow.rectTransform, 4f, 11f, StampW, 340f);
            _stampKanji = Ui.Label(_stamp, "", UiFont.Kanji, 240, Color.white, 0, 0, StampW, 340, TextAnchor.MiddleCenter);
            PlaceCentered(_stampKanji.rectTransform, 0f, 16f, StampW, 340f);

            // 3. Tapered sumi-e brush underline beneath the word (never cutting through letters)
            _stampBrushShadow = Ui.Shape(_stamp, 0, 0, 100, 40, new Vector2[0], Color.black, horizontal: true, name: "BrushShadow");
            _stampBrush = Ui.Shape(_stamp, 0, 0, 100, 40, new Vector2[0], Color.white, horizontal: true, name: "Brush");

            // 4. Finisher word: separate drop-shadow + ink outline siblings BEHIND the foreground text so Rainbow never smears the outline
            _stampWordShadow = Ui.Label(_stamp, "", UiFont.Marker, 128, Color.black, 0, 0, StampW, 240, TextAnchor.MiddleCenter);
            PlaceCentered(_stampWordShadow.rectTransform, 6f, -1f, StampW, 240f);
            _stampWordShadowFx = _stampWordShadow.gameObject.AddComponent<Outline>();
            _stampWordShadowFx.effectDistance = new Vector2(2f, -2f);

            _stampWordOutline = Ui.Label(_stamp, "", UiFont.Marker, 128, Color.black, 0, 0, StampW, 240, TextAnchor.MiddleCenter);
            PlaceCentered(_stampWordOutline.rectTransform, 0f, 6f, StampW, 240f);
            _stampWordOutlineFx = _stampWordOutline.gameObject.AddComponent<Outline>();
            _stampWordOutlineFx.effectDistance = new Vector2(3.5f, -3.5f);

            _stampWord = Ui.Label(_stamp, "", UiFont.Marker, 128, Color.white, 0, 0, StampW, 240, TextAnchor.MiddleCenter);
            PlaceCentered(_stampWord.rectTransform, 0f, 6f, StampW, 240f);

            // 5. Centered tracked caption in Barlow Condensed (full macron ōūŌŪ + · + & glyph support) with separate dark shadow
            _stampSubShadow = Ui.Label(_stamp, "", UiFont.Cond, 28, Color.black, 0, 0, StampW, 60, TextAnchor.MiddleCenter, tracking: 5.5f);
            PlaceCentered(_stampSubShadow.rectTransform, 2f, -100f, StampW, 60f);
            _stampSubShadowFx = _stampSubShadow.gameObject.AddComponent<Outline>();
            _stampSubShadowFx.effectDistance = new Vector2(1.5f, -1.5f);

            _stampSub = Ui.Label(_stamp, "", UiFont.Cond, 28, Color.white, 0, 0, StampW, 60, TextAnchor.MiddleCenter, tracking: 5.5f);
            PlaceCentered(_stampSub.rectTransform, 0f, -98f, StampW, 60f);

            go.SetActive(false);
        }

        /// <summary>
        /// Slams the art's <paramref name="word"/> onto the screen (its <paramref name="kanji"/> large behind it, a sumi-e brush
        /// underline and <paramref name="caption"/> under it) in <paramref name="col"/> with a <paramref name="hot"/> core, for
        /// <paramref name="seconds"/>. A new stamp replaces the old.
        /// </summary>
        public void ArtStamp(string word, string kanji, string caption, Color col, Color hot, float seconds = 1.25f, bool legendary = false, Color? deep = null)
        {
            EnsureArtStamp();
            string w = word ?? "";
            string k = kanji ?? "";
            string cap = caption ?? "";
            int len = w.Length;
            int wordSize = len <= 8 ? 128 : len <= 11 ? 114 : 100;

            _stampWord.fontSize = _stampWordOutline.fontSize = _stampWordShadow.fontSize = wordSize;
            _stampWord.text = _stampWordOutline.text = _stampWordShadow.text = w;

            var kFont = RageKanjiFont(k, 240);
            _stampKanji.font = _stampKanjiShadow.font = kFont;
            _stampKanji.text = _stampKanjiShadow.text = k;

            _stampSub.text = _stampSubShadow.text = cap;

            // Only the foreground word and brush accent get Rainbow; outlines, shadows and small captions stay razor-sharp
            Legendary(_stampWord, legendary);
            Legendary(_stampBrush, legendary);

            // Tailored tapered brush underline spanning slightly wider than the finisher word (centred on local y = 0 so scaling thins symmetrically)
            float bw = Mathf.Clamp(Mathf.Max(len, 6) * 76f + 160f, 560f, 1180f);
            float bx = (StampW - bw) * 0.5f;
            var pts = InkBanner.Stroke(0f, 2f, bw, -2f, 10f, InkBanner.SeedOf(w) + 17);
            var ink = new Color(0.015f, 0.008f, 0.012f, 0.88f);
            Ui.Place(_stampBrushShadow.rectTransform, bx + 3f, BrushYDown + 4f, bw, 40f);
            Ui.Place(_stampBrush.rectTransform, bx, BrushYDown, bw, 40f);
            _stampBrushShadow.Set(pts, ink, ink, ink, true);
            _stampBrush.Set(pts, col, Color.Lerp(hot, Color.white, 0.35f), col, true);

            _stampCol = col;
            _stampHot = hot;
            _stampDeep = deep ?? Color.Lerp(col, Color.black, 0.82f);
            _stampLegendary = legendary;
            _stampT = 0f;
            _stampDur = Mathf.Max(0.5f, seconds);
            _stamp.SetAsLastSibling();
            _stamp.gameObject.SetActive(true);
            TickArtStamp(0f);
        }

        /// <summary>A legendary art's text or accent drifts in the tier's rainbow (<see cref="Rainbow"/>); attach only to foreground elements without an Outline/Shadow on the same GameObject.</summary>
        static void Legendary(Graphic g, bool on)
        {
            if (g == null) return;
            var r = g.GetComponent<Rainbow>();
            if (on && r == null) { r = g.gameObject.AddComponent<Rainbow>(); r.saturation = 0.68f; r.value = 1f; r.speed = 0.32f; }
            if (r != null) r.enabled = on;
            g.SetVerticesDirty();
        }

        /// <summary>Holds the art's grade at <paramref name="amount"/> (0..1) of <paramref name="col"/>; it eases there, and back to 0 when the art lets go.</summary>
        public void ArtGrade(Color col, float amount)
        {
            if (_grade == null)
            {
                _grade = MakeImage("ArtGrade", _root, new Color(0f, 0f, 0f, 0f));
                Stretch(_grade.rectTransform);
                _grade.rectTransform.SetAsFirstSibling();          // under every panel, the flash and the stamp
            }
            if (amount > 0f) _gradeCol = col;            // letting go fades out in the art's own colour
            _gradeTarget = Mathf.Clamp01(amount);
        }

        void TickArtStamp(float dt)
        {
            if (_grade != null)
            {
                float rate = _gradeTarget > _gradeNow ? 6f : 3.5f;          // in fast, out slower (JS damp 6 / 3.5)
                _gradeNow = Mathf.MoveTowards(_gradeNow, _gradeTarget, rate * dt);
                _grade.color = _gradeCol.WithA(0.18f * _gradeNow);      // a light veil over everything; the dark itself is the sky behind the fighters (Vfx.SkyGrade)
                _grade.enabled = _gradeNow > 0.002f;
            }
            if (_stamp == null) return;
            if (_stampT >= _stampDur) { if (_stamp.gameObject.activeSelf) _stamp.gameObject.SetActive(false); return; }
            _stampT += dt;
            float t = _stampT, u = t / _stampDur;

            // the slam: in big and gently tilted over 0.12 s (out back), a tight settle shake, a clean hold, then thin and fade
            float pop = t < 0.12f ? Mathf.Lerp(1.48f, 1f, KMath.Apply(Ease.OutBack, t / 0.12f)) : 1f + 0.035f * u;
            float shake = t > 0.12f && t < 0.30f ? Mathf.Sin(t * 120f) * 5f * (0.30f - t) / 0.18f : 0f;
            float fade = u > 0.72f ? 1f - (u - 0.72f) / 0.28f : 1f;
            _stamp.localScale = new Vector3(pop, pop * (u > 0.72f ? Mathf.Lerp(1f, 0.84f, (u - 0.72f) / 0.28f) : 1f), 1f);
            _stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-5f, -2.5f, Mathf.Clamp01(t / 0.12f)));
            _stamp.anchoredPosition = new Vector2(shake, StampY);

            var ink = new Color(0.015f, 0.008f, 0.012f, 1f);
            _stampInkGlow.color = Color.Lerp(ink, _stampDeep, 0.35f).WithA(0.70f * fade);
            float haloA = (t < 0.14f ? Mathf.Lerp(0.36f, 0.24f, t / 0.14f) : 0.24f) * fade;
            _stampColGlow.color = _stampCol.WithA(haloA);

            _stampKanjiShadow.color = ink.WithA(0.42f * fade);
            _stampKanji.color = Color.Lerp(_stampCol, _stampHot, 0.28f).WithA(0.30f * fade);

            _stampWordShadow.color = ink.WithA(0.86f * fade);
            _stampWordShadowFx.effectColor = ink.WithA(0.62f * fade);
            _stampWordOutline.color = ink.WithA(0.96f * fade);
            _stampWordOutlineFx.effectColor = ink.WithA(0.96f * fade);
            Color body = Color.Lerp(_stampCol, _stampHot, 0.22f);
            _stampWord.color = _stampLegendary
                ? Color.white.WithA(fade)
                : Color.Lerp(_stampHot, body, Mathf.Clamp01(t / 0.18f)).WithA(fade);

            // the sumi-e brush underline sweeps left-to-right beneath the word on impact
            float sweep = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(0.35f + (t / 0.09f) * 0.65f));
            float brushY = 1f - 0.30f * u;
            _stampBrush.rectTransform.localScale = new Vector3(sweep, brushY, 1f);
            _stampBrushShadow.rectTransform.localScale = new Vector3(sweep, brushY, 1f);
            float brushA = fade * Mathf.Clamp01(1.35f - u * 0.55f);
            _stampBrush.color = Color.white.WithA(brushA);
            _stampBrushShadow.color = Color.white.WithA(0.85f * brushA);

            float subFade = fade * Mathf.Lerp(0.80f, 1f, Mathf.Clamp01(t / 0.08f));
            _stampSubShadow.color = ink.WithA(0.92f * subFade);
            _stampSubShadowFx.effectColor = ink.WithA(0.82f * subFade);
            Color subCol = _stampLegendary ? KMath.Hex("#fff1c4") : Color.Lerp(_stampHot, Color.white, 0.25f);
            _stampSub.color = subCol.WithA(subFade);

            if (_stampT >= _stampDur) _stamp.gameObject.SetActive(false);
        }
    }
}
