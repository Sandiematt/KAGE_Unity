using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The brush banner of the JS game (SAN-67; <see cref="InkBanner"/> has its numbers): the stage-result banner (Stretch cleared, Fallen)
    /// drawn the way the JS canvas draws it, in the JS fonts: the ink band painted from the left with ragged edges and two brush strokes,
    /// the calligraphy glyph in Yuji Boku, the title in Permanent Marker and the caption in Teko, tracked. It is built on first use and
    /// runs on unscaled time, like the other banners.
    /// </summary>
    public partial class Hud
    {
        const float InkScale = 1.5f;     // the JS canvas is 1280 x 720, the HUD's reference 1920 x 1080
        RectTransform _ink;
        ShapeGraphic _inkBand, _inkTop, _inkBottom;
        Text _inkGlyph, _inkGlyphShadow, _inkTitle, _inkSub;
        float _inkT = 99f, _inkDur = InkBanner.Duration;
        int _inkSeed;
        Color _inkColor;
        string _inkTitleText = "";
        bool _inkHold;

        /// <summary>The brush banner is on screen.</summary>
        public bool InkBannerShowing => _inkT < _inkDur;
        public string InkBannerTitle => _inkTitleText;
        public string InkBannerCaption => _inkSub != null ? _inkSub.text : null;
        public string InkBannerGlyph => _inkGlyph != null ? _inkGlyph.text : null;
        /// <summary>The banner's opacity now, 0..1.</summary>
        public float InkBannerAlpha => InkBannerShowing ? 1f - InkBanner.FadeOut(_inkT, _inkDur) : 0f;

        void EnsureInk()
        {
            if (_ink != null) return;
            var go = new GameObject("InkBanner", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(_root, false);
            _ink = (RectTransform)go.transform;
            Place(_ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(InkBanner.Width * InkScale, InkBanner.Height * InkScale));
            var clear = Color.white;
            _inkBand = Ui.Shape(_ink, 0, 0, InkBanner.Width * InkScale, InkBanner.Height * InkScale, new Vector2[0], clear, name: "Band");
            _inkTop = Ui.Shape(_ink, 0, 0, InkBanner.Width * InkScale, InkBanner.Height * InkScale, new Vector2[0], clear, name: "TopStroke");
            _inkBottom = Ui.Shape(_ink, 0, 0, InkBanner.Width * InkScale, InkBanner.Height * InkScale, new Vector2[0], clear, name: "BottomStroke");
            // JS: 900 112px KageKanji (the browser thickens the one weight), 58px KageBrush, 600 14px KageUI with 5px of letter spacing
            _inkGlyphShadow = Ui.Label(_ink, "", UiFont.Kanji, Mathf.RoundToInt(112 * InkScale), Color.black, 0, 0, 420, 260, TextAnchor.MiddleCenter);
            _inkGlyph = Ui.Label(_ink, "", UiFont.Kanji, Mathf.RoundToInt(112 * InkScale), Color.white, 0, 0, 420, 260, TextAnchor.MiddleCenter);
            _inkGlyphShadow.fontStyle = _inkGlyph.fontStyle = FontStyle.Bold;
            _inkTitle = Ui.Label(_ink, "", UiFont.Marker, Mathf.RoundToInt(58 * InkScale), Ui.Bone, 0, 0, 900, 140, TextAnchor.MiddleLeft);
            _inkSub = Ui.Label(_ink, "", UiFont.Teko, Mathf.RoundToInt(14 * InkScale), Color.white, 0, 0, 900, 40, TextAnchor.MiddleLeft);
            _inkSub.gameObject.AddComponent<InkTracking>().px = 5f * InkScale;
            go.SetActive(false);
        }

        /// <summary>
        /// The JS brush banner (<c>fx.banner</c>): <paramref name="title"/> in the brush font with a small <paramref name="caption"/> under it,
        /// in <paramref name="color"/>, on its ink band; <paramref name="glyph"/> is the calligraphy (null: the JS pick for the title).
        /// Shown for <paramref name="seconds"/> (the JS 1.55); a new banner replaces the one on screen.
        /// </summary>
        public void ShowInkBanner(string title, string caption, Color color, string glyph = null, float seconds = InkBanner.Duration)
        {
            EnsureInk();
            _inkTitleText = title ?? "";
            _inkTitle.text = _inkTitleText;
            _inkSub.text = (caption ?? "").ToUpperInvariant();
            string g = glyph ?? InkBanner.GlyphFor(title);
            _inkGlyph.text = _inkGlyphShadow.text = g;
            _inkColor = color;
            _inkSeed = InkBanner.SeedOf(title);
            _inkT = 0f;
            _inkDur = Mathf.Max(0.5f, seconds);
            _ink.gameObject.SetActive(true);
            TickInk(0f);
        }

        /// <summary>Tooling hook (review captures): holds the banner at <paramref name="seconds"/> into its run until <see cref="ReleaseInkBanner"/>.</summary>
        public void HoldInkBanner(float seconds)
        {
            if (_ink == null) return;
            _inkHold = true;
            _inkT = Mathf.Clamp(seconds, 0f, _inkDur - 0.001f);
            _ink.gameObject.SetActive(true);
            TickInk(0f);
        }

        public void ReleaseInkBanner() { _inkHold = false; }

        /// <summary>Takes the brush banner off at once.</summary>
        public void HideInkBanner()
        {
            _inkT = 99f;
            _inkHold = false;
            if (_ink != null) _ink.gameObject.SetActive(false);
        }

        void TickInk(float dt)
        {
            if (_ink == null || !_ink.gameObject.activeSelf) return;
            if (_inkT >= _inkDur) { _ink.gameObject.SetActive(false); return; }
            if (!_inkHold) _inkT += dt;
            float t = Mathf.Min(_inkT, _inkDur);
            float painted = InkBanner.Painted(t);
            float alpha = 1f - InkBanner.FadeOut(t, _inkDur);
            float slide = InkBanner.Slide(painted);
            float W = InkBanner.Width, cy = InkBanner.CenterY, hh = InkBanner.HalfHeight, K = InkScale;

            // a screen wider than 16:9 shows more of the road: the band starts at the screen's edge
            float ext = Mathf.Max(0f, (_root.rect.width / K - W) * 0.5f);
            float xs = -ext;
            float x1 = xs + (W + 2f * ext) * painted;

            bool any = painted > 0.01f;
            _inkBand.gameObject.SetActive(any);
            _inkTop.gameObject.SetActive(any);
            _inkBottom.gameObject.SetActive(any);
            if (any)
            {
                var band = InkBanner.Band(x1, _inkSeed, xs);
                _inkBand.Set(Scale(band), new Color(3f / 255f, 3f / 255f, 3f / 255f, 0.84f * alpha), new Color(3f / 255f, 3f / 255f, 3f / 255f, 0.84f * alpha), new Color(3f / 255f, 3f / 255f, 3f / 255f, 0.84f * alpha), false);

                float a0 = xs + 40f, a1 = Mathf.Max(a0 + 1f, x1 - 40f);
                var top = InkBanner.Stroke(a0, cy - hh + 4f, a1, cy - hh - 2f, 5f, _inkSeed + 1);
                var c = _inkColor; c.a = alpha * 0.85f;
                _inkTop.Set(Scale(top), c, c, c, false);
                var bot = InkBanner.Stroke(a1, cy + hh - 2f, xs + 60f, cy + hh + 3f, 3f, _inkSeed + 2);
                var pale = new Color(236f / 255f, 229f / 255f, 211f / 255f, 0.35f * alpha * 0.85f);
                _inkBottom.Set(Scale(bot), pale, pale, pale, false);
            }

            // the glyph, title and caption slide in with the band (JS: the glyph the full slide, the title 60 percent, the caption 40 percent)
            const float gw = 420f, gh = 260f;
            PlaceInk(_inkGlyphShadow.rectTransform, W / 2f - 190f + slide + 5f - gw / 2f / K, cy + 6f - gh / 2f / K, gw, gh);
            PlaceInk(_inkGlyph.rectTransform, W / 2f - 190f + slide - gw / 2f / K, cy - gh / 2f / K, gw, gh);
            PlaceInk(_inkTitle.rectTransform, W / 2f - 110f + slide * 0.6f, cy - 10f - 70f / K, 900f, 140f);
            PlaceInk(_inkSub.rectTransform, W / 2f - 108f + slide * 0.4f, cy + 30f - 20f / K, 900f, 40f);
            var sh = Color.black; sh.a = 0.6f * alpha; _inkGlyphShadow.color = sh;
            var gc = _inkColor; gc.a = alpha; _inkGlyph.color = gc;
            var tc = Ui.Bone; tc.a = alpha; _inkTitle.color = tc;
            var sc = _inkColor; sc.a = alpha; _inkSub.color = sc;
        }

        // JS canvas pixels to the container's (centred, so a wider screen's extra is negative x on the left)
        static Vector2[] Scale(Vector2[] pts)
        {
            var o = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = new Vector2(pts[i].x * InkScale, pts[i].y * InkScale);
            return o;
        }

        // x, y are the box's top-left corner in JS pixels (the size is in container pixels)
        static void PlaceInk(RectTransform rt, float x, float y, float w, float h) { Ui.Place(rt, x * InkScale, y * InkScale, w, h); }
    }
    /// <summary>
    /// CSS letter-spacing for the banner's caption: like <see cref="Tracking"/>, but a space has no glyph quad in a legacy Text, so the
    /// quads are matched to the string's visible characters and each is moved right by px for every character (spaces included) before it.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class InkTracking : BaseMeshEffect
    {
        public float px;

        public override void ModifyMesh(VertexHelper vh)
        {
            var text = graphic as Text;
            if (!IsActive() || text == null || Mathf.Abs(px) < 0.01f) return;
            string s = text.text ?? "";
            int quads = vh.currentVertCount / 4;
            var index = new System.Collections.Generic.List<int>();
            for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) index.Add(i);
            if (index.Count != quads) return;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position.x += px * index[i / 4];
                vh.SetUIVertex(v, i);
            }
        }
    }
}
