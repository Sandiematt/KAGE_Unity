using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The JS-style fight HUD (SAN-108): Kage's panel, the foe's panel, the clock and round pips, the road strip, the pause button, the combo
    /// counter and the foes' small bars, drawn from the <see cref="FightHudModel"/>'s values. Everything is laid out in the JS game's own
    /// pixels (1 rem = 16 px, on a 1280 x 720 stage; <c>styles.css</c> "combat HUD" and its "flat 2D pass") under one scaled root, with the
    /// JS fonts and colours. The model decides what shows and what the bars read; this file only places and colours.
    /// </summary>
    public partial class Hud
    {
        // ---- the model and what feeds it ----
        readonly FightHudModel _model = new();
        CombatEvents _events;
        StretchSession _stretch;
        FightFormat _format = FightFormat.Duel;
        string _foeName, _playerName = "KAGE";
        int _playerLevel = 1;
        readonly List<HudMiniBar> _miniBars = new();

        /// <summary>The HUD's logic: what is shown and the values drawn. Read-only for everything but the HUD.</summary>
        public FightHudModel Model => _model;

        /// <summary>Raised when the pause button is clicked (the keyboard and the gamepad pause through their own keys).</summary>
        public event System.Action PauseRequested;
        /// <summary>The pause button is on screen (JS <c>.hud-pause</c>).</summary>
        public bool PauseButton { get; set; } = true;
        /// <summary>The dev arena's own text (mode line, key hints, training readout) is shown. Review shots and the campaign turn it off.</summary>
        public bool DevText
        {
            get => _devText;
            set { _devText = value; }
        }
        bool _devText = true;

        // ---- scale root ----
        const float JsScale = 1.5f;      // the canvas is 1920 x 1080, the JS stage 1280 x 720
        RectTransform _js, _left, _right, _center, _bottomBar;
        Vector2 _jsSize;

        // ---- fonts and colours (styles.css) ----
        static readonly Color Bone = Hex("#f1ebe0"), Cream = Hex("#f6e6c8"), NameCream = Hex("#f4e6cc"), Gold = Hex("#c9a45c"),
            Crimson = Hex("#c8232f"), HpRed = Hex("#d8202c"), TrackInk = Hex("#120808"), ValueCream = Hex("#f6e7cc"), LineAsh = Hex("#3a342b");

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Color A(Color c, float a) { c.a = a; return c; }

        // ---- panels ----
        sealed class JsPanel
        {
            public bool right;
            public RectTransform root;
            public Vector2 basePos;
            public Text name, sub, hpValue, rageIcon, rageReady, kunai;
            public HudBar chip, fill, posture;
            public Image postureTrack;
            public RawImage portrait;
            public RectTransform rageBarRoot, readyRoot, kunaiRoot;
            public HudBar rageFill;
            public GameObject rageRow;
            public float rageBarW;
            public ShapeGraphic readyShape;
            public string shownName, shownSub;
            public HudPortrait cam;
            public int portraitTicks;
            public Fighter shownFighter;
            public bool portraitDone;
        }

        JsPanel _pl, _pr;

        const float Medal = 73.6f, MedalGap = 14.4f, BodyW = 384f, PanelW = Medal + MedalGap + BodyW;
        const float HpH = 18.4f, ValW = 38.4f, ValGap = 9.6f;
        const float TrackW = BodyW - ValW - ValGap;      // 336

        static Vector2[] Slant(float w, float h, float slant, bool rightEnd, float inset = 0f)
        {
            // CSS clip-path trapezoid: the slanted end loses `slant` along the bottom edge; inset shrinks it all round
            float i = inset, k = h > 0f ? slant / h : 0f;
            if (rightEnd) return new[] { new Vector2(i, i), new Vector2(w - i - k * i, i), new Vector2(w - i - k * (h - i), h - i), new Vector2(i, h - i) };
            return new[] { new Vector2(i + k * i, i), new Vector2(w - i, i), new Vector2(w - i, h - i), new Vector2(i + k * (h - i), h - i) };
        }

        Text Lbl(Transform parent, string text, UiFont font, int size, Color c, float x, float y, float w, float h, TextAnchor align, float tracking = 0f, bool shadow = true)
        {
            var t = Ui.Label(parent, text, font, size, c, x, y, w, h, align, tracking);
            if (shadow)
            {
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.85f);
                s.effectDistance = new Vector2(0f, -2f);
            }
            return t;
        }

        RectTransform Container(string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_js, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        void BuildJs()
        {
            var go = new GameObject("JsHud", typeof(RectTransform));
            go.layer = 5;
            _js = (RectTransform)go.transform;
            _js.SetParent(_root, false);
            _js.anchorMin = _js.anchorMax = new Vector2(0f, 1f);
            _js.pivot = new Vector2(0f, 1f);
            _js.anchoredPosition = Vector2.zero;
            _js.localScale = new Vector3(JsScale, JsScale, 1f);
            ResizeJs();

            _left = Container("Left", new Vector2(0f, 1f), new Vector2(28.8f, -22.4f), new Vector2(PanelW, Medal));
            _right = Container("Right", new Vector2(1f, 1f), new Vector2(-28.8f, -22.4f), new Vector2(PanelW, Medal));
            _pl = BuildPanel(_left, false);
            _pr = BuildPanel(_right, true);
            BuildClock();
            BuildRoad();
            BuildPauseButton();
            BuildCombo();
            BuildBossBar();
        }

        /// <summary>Keeps the scaled root exactly over the canvas, whatever its aspect.</summary>
        void ResizeJs()
        {
            if (_js == null) return;
            var size = _root.rect.size / JsScale;
            if (size == _jsSize || size.x < 1f) return;
            _jsSize = size;
            _js.sizeDelta = size;
        }

        // =====================================================================================================
        // a fighter's panel
        // =====================================================================================================

        JsPanel BuildPanel(RectTransform root, bool right)
        {
            var p = new JsPanel { right = right, root = root, basePos = root.anchoredPosition };
            float medalX = right ? PanelW - Medal : 0f, bodyX = right ? 0f : Medal + MedalGap;

            // medal: ring, dark face, glow, portrait, diamond
            var medal = Ui.Node(root, "Medal", medalX, 0, Medal, Medal);
            Ui.Disc(medal, Medal / 2, Medal / 2, Medal / 2, right ? Hex("#d84a3c") : Hex("#e8c886"));
            Ui.Disc(medal, Medal / 2, Medal / 2, Medal / 2 - 3f, Hex("#0a0707"));
            Ui.Glow(medal, Medal / 2, Medal * 0.45f, Medal * 0.5f, right ? Hex("#6a1d16") : Hex("#4a2a1a"), 1.2f);
            var maskGo = Ui.Node(medal, "PortraitMask", 3f, 3f, Medal - 6f, Medal - 6f);
            var maskImg = maskGo.gameObject.AddComponent<Image>();
            maskImg.sprite = CircleSprite();
            maskImg.raycastTarget = false;
            maskGo.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var ri = Ui.Node(maskGo, "Portrait", 0, 0, Medal - 6f, Medal - 6f).gameObject.AddComponent<RawImage>();
            ri.raycastTarget = false;
            ri.color = Color.clear;
            p.portrait = ri;
            Ui.Diamond(medal, right ? 3.2f : 70.4f, 63.2f, 14.4f, Crimson, Crimson, 0f);

            // body
            float bodyH = right ? 48.8f : 72.8f, by = (Medal - bodyH) / 2f;
            var body = Ui.Node(root, "Body", bodyX, by, BodyW, bodyH);
            var al = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            p.name = Lbl(body, "", UiFont.Teko, 25, NameCream, right ? 0f : 0f, 0f, BodyW, 24.8f, al, 3.5f);
            p.sub = Lbl(body, "", UiFont.Teko, 15, Gold, 0f, 0f, 200f, 24.8f, al, 2.7f);

            // health: value beside a slanted track with its bone-coloured trail
            float trackX = right ? ValW + ValGap : 0f, valX = right ? 0f : TrackW + ValGap;
            float hy = 30.4f;
            var track = Ui.Node(body, "HpTrack", trackX, hy, TrackW, HpH);
            Ui.Shape(track, 0, 0, TrackW, HpH, Slant(TrackW, HpH, 11.2f, !right), Bone, null, null, false, "Frame");
            Ui.Shape(track, 0, 0, TrackW, HpH, Slant(TrackW, HpH, 11.2f, !right, 2f), TrackInk, null, null, false, "Ink");
            p.chip = MakeBar(track, TrackW, HpH, Slant(TrackW, HpH, 11.2f, !right, 2f), right);
            p.chip.SetColors(Bone, Bone);
            p.fill = MakeBar(track, TrackW, HpH, Slant(TrackW, HpH, 11.2f, !right, 2f), right);
            p.fill.SetColors(HpRed, HpRed);
            p.hpValue = Lbl(body, "", UiFont.Num, 18, ValueCream, valX, hy - 3f, ValW, HpH + 6f, TextAnchor.MiddleLeft);
            // posture: a thin strip under the bar, only while it means something
            var pt = Ui.Node(track, "Posture", 0, HpH + 1.2f, TrackW, 3f);
            p.postureTrack = Ui.Box(pt, 0, 0, TrackW, 3f, A(Color.black, 0.55f), "PostureBack");
            p.posture = MakeBar(pt, TrackW, 3f, new[] { new Vector2(0, 0), new Vector2(TrackW, 0), new Vector2(TrackW, 3), new Vector2(0, 3) }, right);
            p.posture.SetColors(Hex("#FFE7A8"), Hex("#C9A45C"));
            pt.gameObject.SetActive(true);

            if (!right) BuildRageRow(body, p);
            return p;
        }

        HudBar MakeBar(Transform parent, float w, float h, Vector2[] shape, bool fromRight)
        {
            var rt = Ui.Node(parent, "Bar", 0, 0, w, h);
            var b = rt.gameObject.AddComponent<HudBar>();
            b.raycastTarget = false;
            b.SetShape(shape, w, fromRight);
            return b;
        }

        void BuildRageRow(Transform body, JsPanel p)
        {
            float y = 54.4f;
            var row = Ui.Node(body, "RageRow", 0, y, BodyW * 0.82f, 18.4f);
            p.rageRow = row.gameObject;
            p.rageIcon = Lbl(row, "怒", UiFont.Kanji, 18, Gold, 0f, -1f, 19.2f, 20f, TextAnchor.MiddleCenter, 0f, false);
            float barX = 19.2f + 8.8f, barW = 251f, barH = 8.8f, barY = (18.4f - barH) / 2f;
            p.rageBarW = barW;
            var bar = Ui.Node(row, "RageBar", barX, barY, barW, barH);
            p.rageBarRoot = bar;
            Ui.Shape(bar, 0, 0, barW, barH, Slant(barW, barH, 6.4f, true, 0f), A(Gold, 0.35f), null, null, false, "Frame");
            Ui.Shape(bar, 0, 0, barW, barH, Slant(barW, barH, 6.4f, true, 1f), Hex("#140a08"), null, null, false, "Ink");
            p.rageFill = MakeBar(bar, barW, barH, Slant(barW, barH, 6.4f, true, 1f), false);
            p.rageFill.SetColors(Hex("#e8b45a"), Hex("#e8b45a"));
            // five segments: 2 px black dividers at every fifth
            for (int i = 1; i <= 4; i++) Ui.Box(bar, barW * i * 0.2f - 1f, 0f, 2f, barH, A(Color.black, 0.75f), "Seg");
            // ready pill
            var pill = Ui.Node(row, "Ready", 0, 0, 100f, 18.4f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            p.readyRoot = pill;
            p.readyShape = Ui.Shape(pill, 0, 0, 100f, 18.4f, Slant(100f, 18.4f, 4.8f, true, 0f), Hex("#f3d48f"), Hex("#ffeec8"), null, true, "Pill");
            p.rageReady = Lbl(pill, "RAGE READY", UiFont.Teko, 15, Hex("#1a0907"), 0f, 0f, 100f, 18.4f, TextAnchor.MiddleCenter, 2.7f, false);
            // kunai: icon and count at the row's right end
            var k = Ui.Node(row, "Kunai", BodyW * 0.82f - 28f, 0, 28f, 18.4f);
            p.kunaiRoot = k;
            var icon = Ui.Node(k, "Icon", 0, 1.6f, 15.2f, 15.2f);
            float u = 15.2f / 24f;
            Ui.Line(icon, new[] { new Vector2(3 * u, 21 * u), new Vector2(14 * u, 10 * u) }, 1.6f, Hex("#d8c6a8"));
            Ui.Shape(icon, 0, 0, 15.2f, 15.2f, new[] { new Vector2(14 * u, 10 * u), new Vector2(21 * u, 3 * u), new Vector2(18 * u, 11 * u) }, Hex("#d8c6a8"));
            p.kunai = Lbl(k, "", UiFont.Num, 15, Hex("#d8c6a8"), 18.4f, -2f, 12f, 22f, TextAnchor.MiddleLeft, 0f, false);
            p.kunaiRoot = k;
        }

        // portrait capture is retried for a few ticks after a fighter is bound, then once more now and then (its pose settles)
        void UpdatePortrait(JsPanel p, Fighter f, bool visible)
        {
            if (!visible || f == null) { if (p.portrait != null && p.shownFighter != null) { p.portrait.color = Color.clear; p.shownFighter = null; p.portraitDone = false; p.portraitTicks = 0; } return; }
            if (p.shownFighter != f) { p.shownFighter = f; p.portraitDone = false; p.portraitTicks = 0; }
            if (p.portraitDone) return;
            if (++p.portraitTicks < 4) return;       // the rig poses in its first frames
            p.cam ??= new HudPortrait();
            if (p.cam.Capture(f))
            {
                p.portrait.texture = p.cam.Texture;
                p.portrait.color = Color.white;
                p.portraitDone = true;
            }
        }

        /// <summary>Draws the portraits again (a fighter's gear or look changed).</summary>
        public void RefreshPortraits()
        {
            if (_pl != null) _pl.portraitDone = false;
            if (_pr != null) _pr.portraitDone = false;
        }

        static Vector2 HurtOffset(float hurt)
        {
            // JS hudHurt: 0% 0, 25% (-4, 2), 50% (3, -1), 100% 0 — hurt runs 1 -> 0
            float p = 1f - Mathf.Clamp01(hurt);
            if (hurt <= 0f) return Vector2.zero;
            if (p < 0.25f) return Vector2.Lerp(Vector2.zero, new Vector2(-4f, -2f), p / 0.25f);
            if (p < 0.5f) return Vector2.Lerp(new Vector2(-4f, -2f), new Vector2(3f, 1f), (p - 0.25f) / 0.25f);
            return Vector2.Lerp(new Vector2(3f, 1f), Vector2.zero, (p - 0.5f) / 0.5f);
        }

        static Color Brighten(Color c, float k) => new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);

        float _lowPhase;

        void ApplyPanel(JsPanel p, HudPanelView v, Fighter f, float dt)
        {
            bool vis = v.Visible;
            if (p.root.gameObject.activeSelf != vis) p.root.gameObject.SetActive(vis);
            if (!vis) return;
            UpdatePortrait(p, f, true);
            var off = HurtOffset(v.Hurt);
            p.root.anchoredPosition = p.basePos + new Vector2(p.right ? -off.x : off.x, off.y);

            // name and the small gold word beside it (level for Kage, the foe's title)
            string sub = p.right ? v.Title : v.LevelText;
            if (p.shownName != v.Name || p.shownSub != sub)
            {
                p.shownName = v.Name; p.shownSub = sub;
                p.name.text = (v.Name ?? "").ToUpperInvariant();
                p.sub.text = (sub ?? "").ToUpperInvariant();
                float nameW = p.name.preferredWidth + 3.5f * (p.name.text.Length) + 2f;
                float subW = p.sub.preferredWidth + 2.7f * p.sub.text.Length + 2f;
                var nr = p.name.rectTransform;
                var sr = p.sub.rectTransform;
                // baseline alignment: the small word sits a touch lower than the name's centre
                if (p.right)
                {
                    sr.sizeDelta = new Vector2(subW, 24.8f);
                    sr.anchoredPosition = new Vector2(BodyW - nameW - 11.2f - subW, 0f);
                    sr.GetComponent<Text>().alignment = TextAnchor.LowerRight;
                    p.name.alignment = TextAnchor.LowerRight;
                }
                else
                {
                    sr.sizeDelta = new Vector2(subW, 24.8f);
                    sr.anchoredPosition = new Vector2(nameW + 11.2f, 0f);
                    sr.GetComponent<Text>().alignment = TextAnchor.LowerLeft;
                    p.name.alignment = TextAnchor.LowerLeft;
                }
            }

            // health
            float low = v.Low ? 1f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI / 0.7f)) : 1f;
            float flash = 1f + v.Hurt * 1.4f;
            p.fill.SetFill(v.Hp);
            p.chip.SetFill(v.Chip);
            p.fill.SetColors(Brighten(HpRed, low * flash), Brighten(HpRed, low * flash));
            string hv = v.HpValue.ToString();
            if (p.hpValue.text != hv) p.hpValue.text = hv;

            // posture strip: shown while it is up or broken
            bool pShow = v.Posture > 0.02f || v.PostureBroken;
            if (p.posture.gameObject.activeSelf != pShow) { p.posture.gameObject.SetActive(pShow); p.postureTrack.gameObject.SetActive(pShow); }
            if (pShow)
            {
                p.posture.SetFill(v.PostureBroken ? 1f : v.Posture);
                if (v.PostureBroken)
                {
                    float pulse = Mathf.PingPong(Time.unscaledTime * 6f, 1f);
                    p.posture.SetColors(Color.Lerp(Hex("#FF7A4B"), Hex("#FFD0B0"), pulse), Color.Lerp(Hex("#C8232F"), Hex("#FF4A50"), pulse));
                }
                else p.posture.SetColors(Hex("#FFE7A8"), Hex("#C9A45C"));
            }

            if (p.right) return;
            // rage
            bool ready = v.RageReady;
            float pillW = Mathf.Max(70f, p.rageReady.preferredWidth + 2.7f * 10 + 16f);
            float barW = p.rageBarW - (ready ? pillW + 8.8f : 0f);
            // the bar's own shapes were built for the full width: scale them with the node
            p.rageBarRoot.localScale = new Vector3(barW / p.rageBarW, 1f, 1f);
            p.rageFill.SetFill(v.RageFill);
            p.rageFill.SetColors(ready ? Hex("#ffe9b0") : Hex("#e8b45a"), ready ? Hex("#ffe9b0") : Hex("#e8b45a"));
            p.rageIcon.color = ready ? Color.Lerp(Gold, Hex("#ffd98f"), Mathf.PingPong(Time.unscaledTime * 1.25f, 1f)) : Gold;
            if (p.readyRoot.gameObject.activeSelf != ready) p.readyRoot.gameObject.SetActive(ready);
            if (ready)
            {
                p.readyRoot.anchoredPosition = new Vector2(19.2f + 8.8f + barW + 8.8f + pillW / 2f, -9.2f);
                p.readyRoot.sizeDelta = new Vector2(pillW, 18.4f);
                p.readyShape.Set(Slant(pillW, 18.4f, 4.8f, true, 0f), Hex("#f3d48f"), Hex("#ffeec8"), Hex("#ffeec8"), true);
                p.rageReady.rectTransform.sizeDelta = new Vector2(pillW, 18.4f);
                float s = Mathf.Lerp(1f, 0.6f, v.RageReadyPop);
                p.readyRoot.localScale = new Vector3(s, s, 1f);
            }
            if (p.kunaiRoot.gameObject.activeSelf != v.KunaiShown) p.kunaiRoot.gameObject.SetActive(v.KunaiShown);
            if (v.KunaiShown) { string ks = v.Kunai.ToString(); if (p.kunai.text != ks) p.kunai.text = ks; }
        }

        // =====================================================================================================
        // the clock and the round pips
        // =====================================================================================================

        RectTransform _clock;
        Text _clockText;
        readonly List<DiamondGraphic> _pips = new();
        const float ClockSize = 89.6f;
        RectTransform _pause;
        Image _pauseDisc;
        Font _fallbackFont;

        void BuildClock()
        {
            _center = Container("Clock", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(ClockSize, ClockSize));
            _clock = _center;
            float c = ClockSize / 2f, k = ClockSize / 100f;
            Ui.Disc(_clock, c, c, 44f * k, A(Hex("#060303"), 0.72f));
            Ui.Line(_clock, Arc(c, c, 41f * k, -90f, 219f), 5f * k, A(Gold, 0.85f));
            Ui.Line(_clock, Arc(c, c, 41f * k, 227f, 256f), 3f * k, Crimson);
            _clockText = Lbl(_clock, "", UiFont.Num, 34, Cream, 0f, 22f, ClockSize, 33.6f, TextAnchor.MiddleCenter);
        }

        static Vector2[] Arc(float cx, float cy, float r, float fromDeg, float toDeg)
        {
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 6f));
            var p = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)n) * Mathf.Deg2Rad;
                p[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return p;
        }

        void ApplyClock(HudClockView v)
        {
            if (_clock.gameObject.activeSelf != v.Visible) _clock.gameObject.SetActive(v.Visible);
            if (!v.Visible) return;
            bool inf = v.Text == "∞";
            if (inf && _fallbackFont == null) _fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var want = inf ? _fallbackFont : Ui.FontOf(UiFont.Num);
            if (_clockText.font != want) _clockText.font = want;
            if (_clockText.text != v.Text) _clockText.text = v.Text;
            _clockText.color = v.Urgent ? Color.Lerp(Hex("#ff5a4a"), Hex("#7a2a24"), Mathf.PingPong(Time.unscaledTime / 0.6f, 1f)) : Cream;
            // pips
            int n = v.PipsVisible ? v.Pips.Length : 0;
            while (_pips.Count < n)
            {
                var d = Ui.Diamond(_clock, 0, 0, 8f, Color.clear, LineAsh, 1f);
                _pips.Add(d);
            }
            float step = 18f, x0 = ClockSize / 2f - (n - 1) * step / 2f;
            for (int i = 0; i < _pips.Count; i++)
            {
                var d = _pips[i];
                bool on = i < n;
                if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                if (!on) continue;
                var pip = v.Pips[i];
                Color fill = pip == HudPip.Kage ? Gold : pip == HudPip.Foe ? Hex("#d24a3c") : Color.clear;
                Color edge = pip == HudPip.Empty ? LineAsh : fill;
                d.Set(8f, 1f, fill, edge);
                var rt = d.rectTransform;
                float half = 8f * 0.7071f + 1f;
                rt.anchoredPosition = new Vector2(x0 + i * step - half, -(22f + 33.6f + 4f + 4f - half));
            }
        }

        // =====================================================================================================
        // the pause button
        // =====================================================================================================

        void BuildPauseButton()
        {
            _pause = Container("Pause", new Vector2(0.5f, 1f), new Vector2(57.6f + 17.6f, -25.6f), new Vector2(35.2f, 35.2f));
            var disc = Ui.Disc(_pause, 17.6f, 17.6f, 17.6f, A(Gold, 0.45f));
            Ui.Disc(_pause, 17.6f, 17.6f, 16.6f, A(Hex("#060303"), 0.88f));
            Ui.Box(_pause, 17.6f - 5f, 17.6f - 5.75f, 3.4f, 11.5f, Gold, "Bar");
            Ui.Box(_pause, 17.6f + 1.6f, 17.6f - 5.75f, 3.4f, 11.5f, Gold, "Bar");
            _pauseDisc = null;
        }

        void ApplyPauseButton(bool stretch)
        {
            bool on = PauseButton;
            if (_pause.gameObject.activeSelf != on) _pause.gameObject.SetActive(on);
            if (!on) return;
            // JS: beside the clock; beside the road strip in a stretch
            var pos = _pause.anchoredPosition;
            float x = (stretch ? 208f : 57.6f) + 17.6f;
            if (!Mathf.Approximately(pos.x, x)) _pause.anchoredPosition = new Vector2(x, pos.y);
        }

        void PollPauseClick()
        {
            if (!PauseButton || _pause == null || !_pause.gameObject.activeInHierarchy) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(_pause, mouse.position.ReadValue(), null)) PauseRequested?.Invoke();
        }

        /// <summary>The pause button's rectangle on screen (px), for tests and tooling.</summary>
        public Rect PauseButtonScreenRect
        {
            get
            {
                var c = new Vector3[4];
                _pause.GetWorldCorners(c);
                return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
            }
        }

        /// <summary>Presses the pause button (what a click does).</summary>
        public void PressPause() => PauseRequested?.Invoke();

        // =====================================================================================================
        // the road strip
        // =====================================================================================================

        RectTransform _road;
        const float RoadW = 384f;
        GradRect _roadFill;
        RectTransform _roadKage;
        Text _roadLabel;
        readonly List<DiamondGraphic> _roadMarks = new();
        Image _roadWave;

        void BuildRoad()
        {
            _road = Container("Road", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(RoadW, 40f));
            var track = Ui.Node(_road, "Track", 0, 0, RoadW, 8f);
            Ui.Box(track, 0, 0, RoadW, 8f, A(Gold, 0.55f), "Frame");
            Ui.Box(track, 1, 1, RoadW - 2f, 6f, A(Hex("#060303"), 0.85f), "Ink");
            _roadFill = Ui.Grad(track, 1, 1, RoadW - 2f, 6f, A(Crimson, 0.55f), Crimson, true);
            var kage = Ui.Node(track, "Kage", 0, 4f - 7.2f, 14.4f, 14.4f);
            _roadKage = kage;
            Ui.Disc(kage, 7.2f, 7.2f, 9f, A(Hex("#f6e6c8"), 0.25f));
            Ui.Disc(kage, 7.2f, 7.2f, 7.2f, Cream);
            _roadLabel = Lbl(_road, "", UiFont.Teko, 16, Gold, 0f, 17.2f, RoadW, 20f, TextAnchor.UpperLeft, 3.2f);
        }

        void ApplyRoad(HudRoadView v)
        {
            if (_road.gameObject.activeSelf != v.Visible) _road.gameObject.SetActive(v.Visible);
            if (!v.Visible) return;
            float k = v.Fill;
            _roadFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, (RoadW - 2f) * k), 6f);
            _roadKage.anchoredPosition = new Vector2(k * RoadW - 7.2f, -(4f - 7.2f));
            var track = _roadFill.transform.parent;
            while (_roadMarks.Count < v.Marks.Count)
            {
                var d = Ui.Diamond(track, 0, 0, 8.8f, Crimson, Cream, 1f);
                _roadMarks.Add(d);
                d.transform.SetSiblingIndex(_roadFill.transform.GetSiblingIndex() + 1);
            }
            for (int i = 0; i < _roadMarks.Count; i++)
            {
                var d = _roadMarks[i];
                bool on = i < v.Marks.Count;
                if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                if (!on) continue;
                float half = 8.8f * 0.7071f + 1f;
                d.rectTransform.anchoredPosition = new Vector2(v.Marks[i].At * RoadW - half, -(4f - half));
                d.SetColors(A(Crimson, v.Marks[i].Done ? 0.35f : 1f), A(Cream, v.Marks[i].Done ? 0.35f : 1f));
            }
            if (_roadLabel.text != v.Label) _roadLabel.text = v.Label;
            _roadLabel.color = Color.Lerp(Gold, Cream, v.WavePulse);
        }

        // =====================================================================================================
        // the combo counter
        // =====================================================================================================

        RectTransform _combo, _comboInkRoot;
        CanvasGroup _comboGroup;
        Text _comboNum, _comboKanji, _comboHits;
        ShapeGraphic _comboInk;
        int _comboShown = -1;
        Font _comboJp;
        static readonly Color BrushRedColor = new Color(0.639f, 0.188f, 0.165f, 0.92f);

        void BuildCombo()
        {
            _combo = Ui.Node(_js, "Combo", 150.4f, 248f, 200f, 70f);
            _comboGroup = _combo.gameObject.AddComponent<CanvasGroup>();
            _comboGroup.blocksRaycasts = false;
            _comboGroup.alpha = 0f;
            _comboInkRoot = Ui.Node(_combo, "InkRoot", -22.4f, 3.2f, 220f, 80f);
            _comboInkRoot.pivot = new Vector2(0.5f, 0.5f);
            _comboInk = Ui.Shape(_comboInkRoot, 0, 0, 220f, 80f, BrushRed(220f, 80f), BrushRedColor, null, null, false, "Brush");
            _comboNum = Lbl(_combo, "", UiFont.Marker, 58, Hex("#ece5d3"), 19.2f, 4f, 80f, 64f, TextAnchor.MiddleLeft, 0f, false);
            var sh = _comboNum.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.012f, 0.012f, 0.012f, 0.6f);
            sh.effectDistance = new Vector2(2f, -3f);
            _comboJp = Font.CreateDynamicFontFromOSFont(new[] { "Yu Mincho", "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Mincho", "MS Gothic", "Noto Serif CJK JP", "Arial Unicode MS" }, 20);
            _comboKanji = Lbl(_combo, "連撃", UiFont.Mincho, 18, Hex("#ece5d3"), 100f, 30f, 44f, 30f, TextAnchor.MiddleLeft, 1.8f, true);
            if (_comboJp != null) _comboKanji.font = _comboJp;
            _comboHits = Lbl(_combo, "HITS", UiFont.Teko, 18, Hex("#ece5d3"), 144f, 30f, 70f, 30f, TextAnchor.MiddleLeft, 4.7f, true);
            _combo.gameObject.SetActive(false);
        }

        /// <summary>The JS brush-red stroke (<c>--brush-red</c> in styles.css), a path of curves sampled into a polygon, in a w x h box.</summary>
        static Vector2[] BrushRed(float w, float h)
        {
            float sx = w / 300f, sy = h / 90f;
            var pts = new List<Vector2>();
            void Bez(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f, u = 1f - t;
                    pts.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
                }
            }
            var p0 = new Vector2(6, 58);
            pts.Add(p0);
            Bez(p0, new Vector2(30, 30), new Vector2(90, 22), new Vector2(160, 18));
            Bez(new Vector2(160, 18), new Vector2(220, 15), new Vector2(270, 16), new Vector2(294, 26));
            pts.Add(new Vector2(288, 34)); pts.Add(new Vector2(296, 40));
            Bez(new Vector2(296, 40), new Vector2(262, 54), new Vector2(200, 62), new Vector2(140, 66));
            Bez(new Vector2(140, 66), new Vector2(80, 70), new Vector2(30, 76), new Vector2(6, 58));
            for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(pts[i].x * sx, pts[i].y * sy);
            return pts.ToArray();
        }

        void ApplyCombo(HudComboView v)
        {
            bool show = v.Opacity > 0.002f;
            if (_combo.gameObject.activeSelf != show) _combo.gameObject.SetActive(show);
            if (!show) return;
            _comboGroup.alpha = v.Opacity;
            if (_comboShown != v.Count)
            {
                _comboShown = v.Count;
                _comboNum.text = v.Count.ToString();
                float nw = _comboNum.preferredWidth + 2f;
                _comboNum.rectTransform.sizeDelta = new Vector2(nw, 64f);
                float hx = 19.2f + nw + 8f;
                _comboKanji.rectTransform.anchoredPosition = new Vector2(hx, -33f);
                _comboHits.rectTransform.anchoredPosition = new Vector2(hx + 44f, -33f);
                // the stroke: the padded box grown by the JS insets (-1.4rem left, -1rem right, -.2rem top, -.4rem bottom), tilted four degrees
                float boxW = hx + 44f + 56f + 25.6f, w = boxW + 22.4f + 16f, h = 70f + 3.2f + 6.4f;
                _comboInkRoot.sizeDelta = new Vector2(w, h);
                _comboInkRoot.anchoredPosition = new Vector2(-22.4f + w / 2f, -(-3.2f + h / 2f));
                _comboInk.rectTransform.sizeDelta = new Vector2(w, h);
                _comboInk.Set(BrushRed(w, h), BrushRedColor, BrushRedColor, BrushRedColor, false);
                _comboInkRoot.localRotation = Quaternion.Euler(0f, 0f, 4f);
            }
            // the bump: 1.6 -> 1 over 0.22 s
            float s = 1f + 0.6f * v.Bump;
            _comboNum.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        // =====================================================================================================
        // the foes' small bars (JS drawMiniBar), in the stretch
        // =====================================================================================================

        readonly List<(RectTransform root, Image fill)> _mini = new();

        void ApplyMiniBars(IReadOnlyList<HudMiniBar> bars)
        {
            while (_mini.Count < bars.Count)
            {
                var bg = MakeImage("MiniBar", _root, new Color(0.012f, 0.012f, 0.012f, 0.8f));
                Place(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(94f, 9f));
                var fill = MakeImage("Fill", bg.transform, Hex("#d24a3c"));
                fill.rectTransform.anchorMin = new Vector2(0f, 0f); fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.offsetMin = new Vector2(1.5f, 1.5f); fill.rectTransform.offsetMax = new Vector2(1.5f, -1.5f);
                _mini.Add((bg.rectTransform, fill));
            }
            for (int i = 0; i < _mini.Count; i++)
            {
                var (rt, fill) = _mini[i];
                bool on = i < bars.Count;
                if (rt.gameObject.activeSelf != on) rt.gameObject.SetActive(on);
                if (!on) continue;
                Vector3 sp = _cam.WorldToScreenPoint(bars[i].World);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var lp);
                rt.anchoredPosition = lp;
                fill.rectTransform.sizeDelta = new Vector2(91f * Mathf.Clamp01(bars[i].Ratio), fill.rectTransform.sizeDelta.y);
            }
        }

        // =====================================================================================================
        // feeding the model
        // =====================================================================================================

        /// <summary>The model hears the fight's events (hits, phases, waves, the stage ending). Binding again replaces the old stream.</summary>
        public void BindEvents(CombatEvents events)
        {
            if (_events == events) return;
            if (_events != null) _events.Published -= OnCombatEvent;
            _events = events;
            if (_events != null) _events.Published += OnCombatEvent;
        }

        /// <summary>The road a stretch walks: the strip, its marks and its label read it. Null goes back to a duel's layout.</summary>
        public void BindStretch(StretchSession session)
        {
            _stretch = session;
            if (session != null) _format = FightFormat.Stretch;
        }

        /// <summary>Kage's name beside the level on the left panel (the arena passes "KAGE  影"; only the first word is shown).</summary>
        public void SetPlayerLevel(int level) { _playerLevel = Mathf.Max(0, level); }

        void OnCombatEvent(CombatEvent e)
        {
            _model.Handle(e);
            if (e.Type == CombatEventType.BossPhaseChanged && _boss != null && e.TargetId == _model.Boss.Id && e.Index > 0)
                Banner("PHASE " + FightHudModel.Roman(e.Index + 1) + "  ·  " + (e.Name ?? "").ToUpperInvariant(), Hex("#FFD08A"), 1.6f);
        }

        HudLayout CurrentLayout => _boss != null ? HudLayout.Boss : (_stretch != null || _format == FightFormat.Stretch) ? HudLayout.Stretch : HudLayout.Duel;

        HudFrame ReadFrame(Fighter l, Fighter r, float timer, bool showTimer)
        {
            var f = new HudFrame
            {
                Layout = CurrentLayout,
                Player = HudReader.Fighter(l, _playerName, _playerLevel),
                HasFoe = r != null && r.gameObject.activeInHierarchy,
                TimeLeft = timer,
                ClockShown = _clockShown,
                ClockRuns = showTimer,
                RoundsShown = _roundsShown,
                RoundsToWin = _format.RoundsToWin,
                Road = HudReader.Road(_stretch),
            };
            if (f.HasFoe) f.Foe = HudReader.Fighter(r, _foeName);
            if (f.Layout == HudLayout.Stretch) { HudReader.MiniBars(_stretch, _miniBars); f.MiniBars = _miniBars; }
            return f;
        }

        void Update() => PollPauseClick();
    }
}
