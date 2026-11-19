using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// Fight HUD built in code (uGUI): slanted health bars with a lagging damage trail, shadow
    /// meters with the 35% (Crescent) and 100% (Thousand Shadows) marks, posture bars (SAN-86), round pips, timer,
    /// announcer, technique banner, combo counter, floating combat text, move list, screen flash.
    /// </summary>
    public partial class Hud : MonoBehaviour
    {
        class Side
        {
            public Text name, combo, meterLabel, postureLabel;
            public Image notch;
            public InkBar hp, trail, meter, hpBack, meterBack, posture, postureBack;
            public Image[] pips;
            public float trailVal = 1f, comboShow;
            public int lastCombo;
        }

        Canvas _canvas;
        RectTransform _root;
        Font _font;
        Camera _cam;
        Side _l, _r;
        Text _timer, _round, _announce, _banner, _mode, _hint, _moveList, _training;
        Image _flash;
        GameObject _moveListPanel;
        float _announceT = 99f, _announceDur = 1f, _bannerT = 99f, _bannerDur = 1f;
        Sprite _circle;
        // the rage art's cut-in band (SAN-82)
        RectTransform _cutBand;
        Text _cutName, _cutKanji, _cutSub;
        Image _cutEdgeTop, _cutEdgeBot;
        float _cutT = 99f, _cutDur = 1f;

        class Floater { public Text t; public Vector2 world; public float age, life, scale; public Color c; }
        readonly List<Floater> _floats = new();
        readonly Stack<Text> _pool = new();

        public void Build(Camera cam)
        {
            _cam = cam;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _circle = CircleSprite();
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)transform;

            _flash = MakeImage("Flash", _root, new Color(1, 1, 1, 0));
            Stretch(_flash.rectTransform);

            _l = MakeSide(false);
            _r = MakeSide(true);

            _timer = MakeText("Timer", _root, 70, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(200, 90), true);
            _round = MakeText("Round", _root, 22, TextAnchor.MiddleCenter, KMath.Hex("#E6B0A8"), new Vector2(0.5f, 1f), new Vector2(0, -118), new Vector2(400, 30), false);
            _mode = MakeText("Mode", _root, 18, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.55f), new Vector2(0.5f, 1f), new Vector2(0, -146), new Vector2(600, 26), false);
            _announce = MakeText("Announce", _root, 132, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1600, 200), true);
            _banner = MakeText("Banner", _root, 46, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(1400, 80), true);
            _hint = MakeText("Hint", _root, 19, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.62f), new Vector2(0f, 0f), new Vector2(380, 54), new Vector2(720, 90), false);
            _hint.text = "MOVE  A/D   JUMP  W   CROUCH  S   GUARD  " + KeyBindings.Label(GameAction.Guard, InputDeviceKind.Keyboard) + " (+S low)\nPUNCH  J   KICK  K   KATANA  L   SHADOW ART  I   (↓+I at full meter)\nTAB move list   F2 duel/training   F3 AI level   R restart";
            _training = MakeText("Training", _root, 24, TextAnchor.MiddleCenter, KMath.Hex("#F06F6E"), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(900, 34), true);
            _announce.text = _banner.text = "";

            BuildCutIn();
            BuildMoveList();
        }

        /// <summary>
        /// The cut-in band (JS <c>RageArts.drawCutIn</c>): a dark band slanted a little across the frame at a third of the way down, an
        /// edge line in the art's colour top and bottom, the kanji left of centre and the name and its caption on the right.
        /// </summary>
        void BuildCutIn()
        {
            var go = new GameObject("CutIn", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            _cutBand = (RectTransform)go.transform;
            Place(_cutBand, new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(2400, 150));
            _cutBand.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            var bg = MakeImage("Bg", _cutBand, new Color(0.024f, 0.016f, 0.016f, 0.92f));
            Stretch(bg.rectTransform);
            _cutEdgeTop = MakeImage("EdgeTop", _cutBand, Color.white);
            _cutEdgeTop.rectTransform.anchorMin = new Vector2(0f, 1f); _cutEdgeTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            _cutEdgeTop.rectTransform.pivot = new Vector2(0.5f, 1f); _cutEdgeTop.rectTransform.sizeDelta = new Vector2(0f, 5f); _cutEdgeTop.rectTransform.anchoredPosition = Vector2.zero;
            _cutEdgeBot = MakeImage("EdgeBot", _cutBand, Color.white);
            _cutEdgeBot.rectTransform.anchorMin = new Vector2(0f, 0f); _cutEdgeBot.rectTransform.anchorMax = new Vector2(1f, 0f);
            _cutEdgeBot.rectTransform.pivot = new Vector2(0.5f, 0f); _cutEdgeBot.rectTransform.sizeDelta = new Vector2(0f, 5f); _cutEdgeBot.rectTransform.anchoredPosition = Vector2.zero;
            _cutKanji = MakeText("Kanji", _cutBand, 118, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(-150, 2), new Vector2(520, 140), false);
            _cutName = MakeText("Name", _cutBand, 64, TextAnchor.MiddleRight, Color.white, new Vector2(1f, 0.5f), new Vector2(-270, 14), new Vector2(900, 80), true);
            _cutSub = MakeText("Sub", _cutBand, 24, TextAnchor.MiddleRight, Color.white, new Vector2(1f, 0.5f), new Vector2(-274, -36), new Vector2(900, 30), false);
            // the name and its caption hang from the band's right end (pivot on the right), clear of the screen's edge
            _cutName.rectTransform.pivot = _cutSub.rectTransform.pivot = new Vector2(1f, 0.5f);
            _cutName.rectTransform.anchoredPosition = new Vector2(-270f, 14f);
            _cutSub.rectTransform.anchoredPosition = new Vector2(-274f, -36f);
            // the kanji needs an OS font with Japanese glyphs when one exists (as the names, SetNames)
            var jp = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS", "Arial" }, 96);
            if (jp != null) _cutKanji.font = jp;
            go.SetActive(false);
        }

        Side MakeSide(bool right)
        {
            var s = new Side();
            float sx = right ? -1f : 1f;
            Vector2 anchor = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            var hpBack = MakeBar("HpBack", anchor, new Vector2(sx * 470, -66), new Vector2(760, 34), right, new Color(0.06f, 0.02f, 0.03f, 0.85f), new Color(0.06f, 0.02f, 0.03f, 0.85f));
            hpBack.skew = 16f;
            s.hpBack = hpBack;
            s.trail = MakeBar("HpTrail", anchor, new Vector2(sx * 470, -66), new Vector2(752, 26), right, new Color(1f, 0.92f, 0.86f, 0.9f), new Color(1f, 0.92f, 0.86f, 0.9f));
            s.hp = MakeBar("Hp", anchor, new Vector2(sx * 470, -66), new Vector2(752, 26), right,
                right ? Palette.Amber : KMath.Hex("#7E1A22"), right ? KMath.Hex("#FFD08A") : KMath.Hex("#F0605E"));
            s.meterBack = MakeBar("MeterBack", anchor, new Vector2(sx * 330, -104), new Vector2(480, 14), right, new Color(0.06f, 0.02f, 0.03f, 0.85f), new Color(0.06f, 0.02f, 0.03f, 0.85f));
            s.meter = MakeBar("Meter", anchor, new Vector2(sx * 330, -104), new Vector2(474, 10), right, KMath.Hex("#3A0A10"), right ? Palette.Amber : KMath.Hex("#FF4A50"));
            // 35% notch
            var notch = s.notch = MakeImage("Notch", _root, new Color(1, 1, 1, 0.5f));
            Place(notch.rectTransform, anchor, new Vector2(sx * (330 - 237 + 474 * 0.35f), -104), new Vector2(3, 18));
            s.meterLabel = MakeText("MeterLabel", _root, 15, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.6f), anchor, new Vector2(sx * 680, -104), new Vector2(200, 20), false);
            s.meterLabel.text = "SHADOW";
            // posture (SAN-86): fills as the fighter guards; full breaks the guard
            s.postureBack = MakeBar("PostureBack", anchor, new Vector2(sx * 330, -120), new Vector2(480, 11), right, new Color(0.06f, 0.02f, 0.03f, 0.85f), new Color(0.06f, 0.02f, 0.03f, 0.85f));
            s.posture = MakeBar("Posture", anchor, new Vector2(sx * 330, -120), new Vector2(474, 7), right, KMath.Hex("#8A7A5A"), KMath.Hex("#FFE7A8"));
            s.postureLabel = MakeText("PostureLabel", _root, 13, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.5f), anchor, new Vector2(sx * 680, -120), new Vector2(200, 18), false);
            s.postureLabel.text = "POSTURE";
            s.name = MakeText("Name", _root, 34, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Color.white, anchor, new Vector2(sx * 250, -26), new Vector2(400, 44), true);
            s.pips = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                s.pips[i] = MakeImage("Pip", _root, new Color(1, 1, 1, 0.15f));
                s.pips[i].sprite = _circle;
                Place(s.pips[i].rectTransform, anchor, new Vector2(sx * (110 + i * 30), -136), new Vector2(20, 20));
            }
            s.combo = MakeText("Combo", _root, 58, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Color.white, anchor, new Vector2(sx * 330, -330), new Vector2(560, 120), true);
            s.combo.text = "";
            return s;
        }

        InkBar MakeBar(string name, Vector2 anchor, Vector2 pos, Vector2 size, bool rtl, Color a, Color b)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(_root, false);
            var bar = go.AddComponent<InkBar>();
            bar.rightToLeft = rtl;
            bar.colorA = a; bar.colorB = b;
            bar.raycastTarget = false;
            Place(bar.rectTransform, anchor, pos, size);
            return bar;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        Image MakeImage(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Text MakeText(string name, Transform parent, int size, TextAnchor align, Color c, Vector2 anchor, Vector2 pos, Vector2 box, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = c;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = bold ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
            Place(t.rectTransform, anchor, pos, box);
            return t;
        }

        void BuildMoveList()
        {
            _moveListPanel = new GameObject("MoveList", typeof(RectTransform));
            _moveListPanel.transform.SetParent(_root, false);
            var bg = _moveListPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.01f, 0.02f, 0.86f);
            bg.raycastTarget = false;
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1180, 760));
            _moveList = MakeText("List", _moveListPanel.transform, 21, TextAnchor.UpperLeft, new Color(1f, 0.93f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1100, 720), false);
            _moveList.horizontalOverflow = HorizontalWrapMode.Wrap;
            _moveList.supportRichText = true;
            _moveList.text =
                "<size=34><b><color=#F06F6E>KAGE</color>  —  TECHNIQUES</b></size>\n\n" +
                "<b><color=#F06F6E>FISTS</color></b>\n" +
                "  J · J · J ............ Jab → Pommel Strike → Rising Fist (launcher)\n" +
                "  ↓ + J ................ Low Jab          → + J (far) .... Stepping Punch\n" +
                "  → + J (close) ........ Shoulder Throw   ↓ + J (opponent down) .. Ground Strike\n\n" +
                "<b><color=#F06F6E>KICKS</color></b>\n" +
                "  K · K · K ............ Roundhouse → Switch Kick → Axe Kick (overhead, knockdown)\n" +
                "  ↓ + K ................ Leg Sweep (low, knockdown)   → + K .... Push Kick\n" +
                "  ← + K ................ Axe Kick         air K .......... Flying Kick\n\n" +
                "<b><color=#F06F6E>KATANA</color></b>\n" +
                "  L · L · L ............ Iai Draw → Rising Moon (launcher) → Kage Cleave (overhead)\n" +
                "  → + L ................ Shadow Step (dash through)   ↓ + L .... Low Reap\n" +
                "  ← + L ................ Mirror Guard (parry → Kage Counter)   air L .. Crescent Spin\n\n" +
                "<b><color=#F06F6E>GUARD</color></b>  (O or B · gamepad RB / RT)\n" +
                "  hold O ............... Guard: blocks high and mid hits; overheads too   O + ↓ .. Low guard: blocks lows, not overheads\n" +
                "  Guarding fills POSTURE; a full bar breaks the guard (helpless, next hit is a CRITICAL). It drains after a second.\n" +
                "  Walking backward no longer guards. Attack, dash, roll or jump straight out of the guard.\n\n" +
                "<b><color=#F06F6E>SHADOW ARTS</color></b>\n" +
                "  I (35% meter) ........ Crimson Crescent (projectile)\n" +
                "  ↓ + I (100% meter) ... Thousand Shadows\n\n" +
                "<b><color=#F06F6E>MOVEMENT</color></b>\n" +
                "  hold → walk, keep holding to run   Shift dash (→ / ← / air)   dash + hold → sprint\n" +
                "  W / Space jump (tap = short hop), again in the air = double jump   ← walk back\n" +
                "  →→ Shadow Roll (passes through)   ←← Backflip\n" +
                "  When knocked down press any button for a quick kip-up.\n" +
                "  Mixing J / K / L chains is allowed: J→K, J→L, K→L, J·J→K …";
            _moveListPanel.SetActive(false);
        }

        public void ToggleMoveList() => _moveListPanel.SetActive(!_moveListPanel.activeSelf);

        /// <summary>The posture bar's fill (0..1) on the left (Kage's) or right (the opponent's) side, as last drawn.</summary>
        public float PostureBar(bool right) => (right ? _r : _l).posture.fill;
        public bool MoveListOpen => _moveListPanel.activeSelf;

        static Sprite CircleSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                float a = Mathf.Clamp01(n / 2f - 1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // =========================================================================================
        public void SetNames(string l, string r)
        {
            // names may carry a kanji (影): use an OS font with Japanese glyphs when one exists
            var jp = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS", "Arial" }, 48);
            if (jp != null) { _l.name.font = jp; _r.name.font = jp; }
            _l.name.text = l; _r.name.text = r;
        }
        public void SetMode(string mode) => _mode.text = mode;

        bool _clockShown = true, _roundsShown = true;

        /// <summary>
        /// Shows only what a fight format has (SAN-102): a format with no clock hides the timer, and one round hides the round count and the
        /// round pips (the boss format). The default shows everything, as a duel does.
        /// </summary>
        public void SetFormat(FightFormat f) { _clockShown = f.HasClock; _roundsShown = f.HasRounds; }
        public void SetTraining(string text) => _training.text = text;

        public void Tick(Fighter l, Fighter r, float timer, int round, bool showTimer)
        {
            float dt = Time.unscaledDeltaTime;
            UpdateSide(_l, l, dt);
            UpdateSide(_r, r, dt);
            _timer.text = showTimer ? Mathf.CeilToInt(Mathf.Max(0f, timer)).ToString() : "∞";
            _timer.color = showTimer && timer < 10f ? KMath.Hex("#FF6060") : Color.white;
            _round.text = "ROUND " + round;
            _timer.enabled = _clockShown;
            _round.enabled = _roundsShown;
            foreach (var p in _l.pips) p.enabled = _roundsShown;
            foreach (var p in _r.pips) p.enabled = _roundsShown;
            TickBoss(dt);
            TickStageCard(dt);

            // announcer: pop in big, settle, fade
            _announceT += dt;
            if (_announceT < _announceDur)
            {
                float u = _announceT / _announceDur;
                float pop = _announceT < 0.12f ? Mathf.Lerp(1.8f, 1f, KMath.Apply(Ease.OutBack, _announceT / 0.12f)) : 1f + (u * 0.08f);
                _announce.rectTransform.localScale = Vector3.one * pop;
                var c = _announce.color; c.a = u > 0.8f ? (1f - u) / 0.2f : 1f; _announce.color = c;
            }
            else _announce.text = "";

            UpdateCutIn(dt);

            _bannerT += dt;
            if (_bannerT < _bannerDur)
            {
                float u = _bannerT / _bannerDur;
                float slide = _bannerT < 0.15f ? Mathf.Lerp(-600f, 0f, KMath.Apply(Ease.OutExpo, _bannerT / 0.15f)) : (u > 0.8f ? Mathf.Lerp(0f, 600f, (u - 0.8f) / 0.2f) : 0f);
                _banner.rectTransform.anchoredPosition = new Vector2(slide, -150);
            }
            else _banner.text = "";

            var fc = GameFeel.flashColor; fc.a = GameFeel.flashAmount;
            _flash.color = fc;

            for (int i = _floats.Count - 1; i >= 0; i--)
            {
                var f = _floats[i];
                f.age += dt;
                if (f.age >= f.life) { f.t.gameObject.SetActive(false); _pool.Push(f.t); _floats.RemoveAt(i); continue; }
                float u = f.age / f.life;
                Vector3 sp = _cam.WorldToScreenPoint(f.world + Vector2.up * (KMath.Apply(Ease.OutCubic, u) * 0.45f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var lp);
                f.t.rectTransform.anchoredPosition = lp;
                float pop = f.age < 0.1f ? Mathf.Lerp(1.7f, 1f, f.age / 0.1f) : 1f;
                f.t.rectTransform.localScale = Vector3.one * pop * f.scale;
                var c = f.c; c.a = u > 0.7f ? (1f - u) / 0.3f : 1f; f.t.color = c;
            }
        }

        void UpdateSide(Side s, Fighter f, float dt)
        {
            if (f == null || !f.gameObject.activeInHierarchy)
            {
                s.hp.Set(1f); s.trail.Set(1f); s.meter.Set(0f); s.posture.Set(0f);
                return;
            }
            float hp = f.health / f.maxHealth;
            s.hp.Set(hp);
            s.trailVal = s.trailVal > hp ? Mathf.MoveTowards(s.trailVal, hp, dt * (f.Busy ? 0.08f : 0.6f)) : hp;
            s.trail.Set(s.trailVal);
            s.meter.Set(f.meter / 100f);
            bool full = f.meter >= 100f;
            s.meter.color = full ? Color.Lerp(Color.white, new Color(1.6f, 1.6f, 1.6f), Mathf.PingPong(Time.unscaledTime * 3f, 1f)) : Color.white;
            s.meterLabel.text = full ? "SHADOW  ·  READY" : (f.meter >= 35f ? "SHADOW  ·  CRESCENT" : "SHADOW");
            s.posture.Set(f.PostureFill);
            bool broken = f.GuardBroken;
            s.posture.color = broken ? Color.Lerp(Color.white, new Color(1.6f, 0.4f, 0.35f), Mathf.PingPong(Time.unscaledTime * 6f, 1f)) : Color.white;
            s.postureLabel.text = broken ? "POSTURE  ·  BROKEN" : "POSTURE";
            for (int i = 0; i < s.pips.Length; i++)
                s.pips[i].color = i < f.roundsWon ? (f == _lFighter ? KMath.Hex("#F06F6E") : Palette.Amber) : new Color(1, 1, 1, 0.15f);

            if (f.comboCount >= 2 && f.comboCount != s.lastCombo)
            {
                s.combo.text = f.comboCount + " HITS";
                s.comboShow = 1.4f;
                s.combo.rectTransform.localScale = Vector3.one * 1.35f;
            }
            s.lastCombo = f.comboCount;
            s.comboShow -= dt;
            s.combo.rectTransform.localScale = Vector3.Lerp(s.combo.rectTransform.localScale, Vector3.one, dt * 12f);
            var cc = s.combo.color; cc.a = Mathf.Clamp01(s.comboShow / 0.3f); s.combo.color = cc;
        }

        Fighter _lFighter;
        public void Bind(Fighter left) => _lFighter = left;

        public void Announce(string text, Color c, float dur)
        {
            _announce.text = text;
            _announce.color = c;
            _announceT = 0f;
            _announceDur = dur;
        }

        /// <summary>
        /// A rage art's cut-in band (SAN-82): slides in from the right with the art's <paramref name="name"/>, its <paramref name="kanji"/> and a
        /// caption, in the art's colours, stays for <paramref name="seconds"/> in all and slides out. On unscaled time, like the banner.
        /// </summary>
        public void CutIn(string name, string kanji, string caption, Color col, Color hot, float seconds)
        {
            _cutName.text = name; _cutName.color = col;
            _cutKanji.text = kanji; _cutKanji.color = new Color(hot.r, hot.g, hot.b, 0.85f);
            _cutSub.text = caption; _cutSub.color = hot;
            _cutEdgeTop.color = col; _cutEdgeBot.color = col;
            _cutT = 0f;
            _cutDur = Mathf.Max(0.3f, seconds);
            _cutBand.gameObject.SetActive(true);
            UpdateCutIn(0f);
        }

        /// <summary>The cut-in band is on screen.</summary>
        public bool CutInShowing => _cutT < _cutDur;
        public string CutInName => _cutName != null ? _cutName.text : null;
        public string CutInKanji => _cutKanji != null ? _cutKanji.text : null;

        void UpdateCutIn(float dt)
        {
            if (_cutBand == null) return;
            if (_cutT >= _cutDur) { if (_cutBand.gameObject.activeSelf) _cutBand.gameObject.SetActive(false); return; }
            _cutT += dt;
            // JS: in over 0.16 s (out cubic), out over the last 0.18 s; the band's height follows
            float inK = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(_cutT / 0.16f));
            float outK = Mathf.Clamp01((_cutT - (_cutDur - 0.23f)) / 0.18f);
            float k = inK * (1f - outK);
            _cutBand.localScale = new Vector3(1f, Mathf.Max(0.001f, k), 1f);
            _cutName.rectTransform.anchoredPosition = new Vector2(-270f + (1f - inK) * 300f, 14f);
            _cutSub.rectTransform.anchoredPosition = new Vector2(-274f + (1f - inK) * 300f, -36f);
            if (_cutT >= _cutDur) _cutBand.gameObject.SetActive(false);
        }

        public void Banner(string text, Color c, float dur)
        {
            _banner.text = text;
            _banner.color = c;
            _bannerT = 0f;
            _bannerDur = dur;
        }

        readonly List<(Text t, Vector2 w)> _labels = new();

        public void Label(Vector2 world, string text)
        {
            var t = MakeText("Label", _root, 17, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 30), false);
            t.text = text;
            _labels.Add((t, world));
        }

        public void ClearLabels()
        {
            foreach (var l in _labels) if (l.t != null) Destroy(l.t.gameObject);
            _labels.Clear();
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            foreach (var l in _labels)
            {
                Vector3 sp = _cam.WorldToScreenPoint(l.w);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var lp);
                l.t.rectTransform.anchoredPosition = lp;
            }
        }

        public void Float(Vector2 world, string text, Color c, float scale)
        {
            Text t = _pool.Count > 0 ? _pool.Pop() : MakeText("Float", _root, 30, TextAnchor.MiddleCenter, c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 50), true);
            t.gameObject.SetActive(true);
            t.text = text;
            t.color = c;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            _floats.Add(new Floater { t = t, world = world + new Vector2(Random.Range(-0.15f, 0.15f), 0f), life = 0.9f, scale = scale, c = c });
        }
    }
}
