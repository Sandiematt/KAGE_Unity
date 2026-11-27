using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The fight HUD built in code (uGUI). The JS-style panels (Kage's and the foe's, the clock and round pips, the road strip, the boss
    /// bar, the combo counter, the pause button; SAN-108) are in <c>Hud.Panels.cs</c> and <c>Hud.Boss.cs</c>, drawn from the view model
    /// (<see cref="FightHudModel"/>); this file keeps the rest: the announcer, the technique banner, the rage art's cut-in, the move
    /// list, floating combat text, the screen flash and the dev arena's text (<c>Hud.StageCard.cs</c> has the title card and the prompt slip).
    /// </summary>
    public partial class Hud : MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _root;
        Font _font;
        Camera _cam;
        Text _announce, _banner, _mode, _hint, _moveList, _training;
        Image _flash;
        GameObject _moveListPanel;
        float _announceT = 99f, _announceDur = 1f, _bannerT = 99f, _bannerDur = 1f;
        Sprite _circle;

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
            gameObject.AddComponent<HudRectWatcher>().hud = this;

            _flash = MakeImage("Flash", _root, new Color(1, 1, 1, 0));
            Stretch(_flash.rectTransform);

            BuildJs();

            _mode = MakeText("Mode", _root, 18, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.55f), new Vector2(0f, 0f), new Vector2(380, 132), new Vector2(720, 26), false);
            _announce = MakeText("Announce", _root, 132, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1600, 200), true);
            _banner = MakeText("Banner", _root, 46, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(1400, 80), true);
            _hint = MakeText("Hint", _root, 19, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.62f), new Vector2(0f, 0f), new Vector2(380, 54), new Vector2(720, 90), false);
            _hint.text = "MOVE  A/D   JUMP  W   CROUCH  S   GUARD  " + KeyBindings.Label(GameAction.Guard, InputDeviceKind.Keyboard) + " (+S low)\nPUNCH  J   KICK  K   KATANA  L   SHADOW ART  I   (↓+I at full meter)\nTAB move list   F2 duel/training   F3 AI level   R restart";
            _training = MakeText("Training", _root, 24, TextAnchor.MiddleCenter, KMath.Hex("#F06F6E"), new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(900, 34), true);
            _announce.text = _banner.text = "";

            BuildCutIn();
            BuildMoveList();
        }

        void OnDestroy()
        {
            if (_events != null) _events.Published -= OnCombatEvent;
            _events = null;
            _pl?.cam?.Dispose();
            _pr?.cam?.Dispose();
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
                "  → + L ................ Shadow Step (dash through)   S / ↓ + L .... Crown Wheel (low → mid)\n" +
                "  ← + L ................ Mirror Guard (parry → Kage Counter)   ← + J .. Poised Fang (held overhead)\n" +
                "  air L ................ Crescent Cut                 air ↓ + L .... Kage Cleave from a jump\n\n" +
                "<b><color=#F06F6E>GUARD</color></b>  (O or B · gamepad RB / RT)\n" +
                "  hold O ............... Guard: blocks high and mid hits; overheads too   O + ↓ .. Low guard: blocks lows, not overheads\n" +
                "  Guarding fills POSTURE; a full bar breaks the guard (helpless, next hit is a CRITICAL). It drains after a second.\n" +
                "  Walking backward no longer guards. Attack, dash, slide or jump straight out of the guard.\n" +
                "  tap O just before a blow: PARRY, then a COUNTER.   PARRIED by a foe: hold O or dash to recover early\n" +
                "  A blow that connects can dash out of its recovery. A whiff can't: PUNISH a foe's whiff\n\n" +
                "<b><color=#F06F6E>SHADOW ARTS</color></b>\n" +
                "  I (35% meter) ........ Crimson Crescent (projectile)\n" +
                "  ↓ + I (100% meter) ... Thousand Shadows\n\n" +
                "<b><color=#F06F6E>MOVEMENT</color></b>\n" +
                "  hold → walk, keep holding to run   Shift dash (→ / ← / air)   dash + hold → sprint\n" +
                "  W / Space jump (tap = short hop), again in the air = double jump   ← walk back\n" +
                "  →→ Shadow Slide (passes through)   ←← Back Step\n" +
                "  When knocked down press any button for a quick kip-up.\n" +
                "  Mixing J / K / L chains is allowed: J→K, J→L, K→L, J·J→K …";
            _moveListPanel.SetActive(false);
        }

        public void ToggleMoveList() => _moveListPanel.SetActive(!_moveListPanel.activeSelf);

        /// <summary>The posture bar's fill (0..1) on the left (Kage's) or right (the opponent's) side, as last drawn.</summary>
        public float PostureBar(bool right) => (right ? _model.Foe : _model.Player).Posture;
        public bool MoveListOpen => _moveListPanel.activeSelf;

        static Sprite CircleSprite()
        {
            const int n = 128;
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
        /// <summary>
        /// The names. Kage's (the arena passes "KAGE  影": only the first word is shown, the JS panel has none of the kanji) and the foe's
        /// (the group fight adds "× 4").
        /// </summary>
        public void SetNames(string l, string r)
        {
            _playerName = FirstWord(l);
            _foeName = r;
        }

        static string FirstWord(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf("  ", System.StringComparison.Ordinal);
            return i > 0 ? s.Substring(0, i) : s;
        }

        public void SetMode(string mode) => _mode.text = mode;

        bool _clockShown = true, _roundsShown = true;

        /// <summary>
        /// Shows only what a fight format has (SAN-102): a format with no clock hides the clock, and one round hides the round pips (the boss
        /// format); a stretch's format puts the road strip where the clock was. The default shows everything, as a duel does.
        /// </summary>
        public void SetFormat(FightFormat f) { _format = f; _clockShown = f.HasClock; _roundsShown = f.HasRounds; }
        public void SetTraining(string text) => _training.text = text;

        Fighter _lFighter, _rFighter;
        public void Bind(Fighter left) => _lFighter = left;

        /// <summary>
        /// The HUD's frame time: real (unscaled) time, capped, or the fixed capture step when frames are being captured
        /// (<see cref="Time.captureDeltaTime"/>, the review harness: unscaled time ignores it, so the HUD barely moved in a render).
        /// </summary>
        internal static float UiDeltaTime => Mathf.Min(0.1f, Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime);

        public void Tick(Fighter l, Fighter r, float timer, int round, bool showTimer)
        {
            float dt = UiDeltaTime;
            ResizeJs();
            _rFighter = r;
            _model.Tick(dt, ReadFrame(l, r, timer, showTimer));
            ApplyPanel(_pl, _model.Player, l, dt);
            ApplyPanel(_pr, _model.Foe, r, dt);
            ApplyBoss(_model.Boss, dt);
            ApplyClock(_model.Clock);
            ApplyRoad(_model.Road);
            ApplyPauseButton(_model.Layout == HudLayout.Stretch);
            ApplyCombo(_model.Combo);
            ApplyCallouts(_model.Callouts);
            ApplyRageCue(_model.RageCue);
            ApplyMiniBars(_model.MiniBars);
            _mode.enabled = _hint.enabled = _training.enabled = _devText;
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
            TickInk(dt);
            TickArtStamp(dt);       // a rage art's finisher word and its grade (Hud.RageArt.cs)

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

        public void Announce(string text, Color c, float dur)
        {
            _announce.text = text;
            _announce.color = c;
            _announceT = 0f;
            _announceDur = dur;
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
