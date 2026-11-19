using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>What a tutorial prompt says (SAN-96): the verb, the keys or buttons, the hint and how it is drawn.</summary>
    public struct PromptView
    {
        public string verb, hint;
        public string[] keys;
        /// <summary>0..1: fades in over the first frames; fades out after being done.</summary>
        public float alpha;
        /// <summary>The prompt is done: gold, crossed out and floating up by <see cref="rise"/> pixels.</summary>
        public bool done;
        public float rise;
    }

    /// <summary>
    /// The stage title card and the tutorial prompt slip (SAN-96), two small pieces of HUD a stretch needs. Both are built on first use, so
    /// a fight that never shows one carries nothing extra.
    /// <para><b>Title card</b> (<see cref="TitleCard"/>): the plain card that opens a stage in place of story scenes: a dark band across the
    /// middle of the screen with the chapter and kind in small gold capitals, the stage name below and a gold rule, fading in and out
    /// (the flow's <c>StageHudPanel</c> draws the same card for the campaign).</para>
    /// <para><b>Prompt slip</b> (<see cref="SetPrompt"/>): the JS tutorial prompt: a paper slip with a red seal, the verb, the keys as
    /// key caps and the hint, drifting above Kage; a done prompt turns gold, is crossed out and floats away.</para>
    /// </summary>
    public partial class Hud
    {
        // ---------------- title card ----------------
        RectTransform _card;
        CanvasGroup _cardGroup;
        Text _cardKicker, _cardName, _cardSub;
        float _cardT = 99f, _cardDur = 1f;

        /// <summary>The title card is on screen.</summary>
        public bool TitleCardShowing => _cardT < _cardDur;
        public string TitleCardName => _cardName != null ? _cardName.text : null;
        public string TitleCardKicker => _cardKicker != null ? _cardKicker.text : null;
        /// <summary>The card's opacity now, 0..1.</summary>
        public float TitleCardAlpha => _cardGroup != null && TitleCardShowing ? _cardGroup.alpha : 0f;

        void EnsureCard()
        {
            if (_card != null) return;
            var go = new GameObject("TitleCard", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_root, false);
            _card = (RectTransform)go.transform;
            _cardGroup = go.GetComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false;
            Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(2400f, 300f));
            var bg = MakeImage("Bg", _card, new Color(0f, 0f, 0f, 0.62f));
            Stretch(bg.rectTransform);
            _cardKicker = MakeText("Kicker", _card, 26, TextAnchor.MiddleCenter, KMath.Hex("#C9A45C"), new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(1400f, 36f), true);
            _cardName = MakeText("Name", _card, 96, TextAnchor.MiddleCenter, KMath.Hex("#ECE5D3"), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(1800f, 120f), true);
            _cardSub = MakeText("Sub", _card, 28, TextAnchor.MiddleCenter, new Color(0.93f, 0.9f, 0.83f, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(1400f, 40f), false);
            var rule = MakeImage("Rule", _card, KMath.Hex("#C9A45C"));
            Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -108f), new Vector2(220f, 3f));
            go.SetActive(false);
        }

        /// <summary>
        /// The plain title card at the start of a stage: <paramref name="kicker"/> (the chapter and kind, shown in capitals), the stage
        /// <paramref name="name"/> and an optional <paramref name="subtitle"/>, on screen for <paramref name="seconds"/> in all, fading
        /// in over a quarter of a second and out over the last third. On unscaled time.
        /// </summary>
        public void TitleCard(string kicker, string name, string subtitle, float seconds)
        {
            EnsureCard();
            _cardKicker.text = (kicker ?? "").ToUpperInvariant();
            _cardName.text = name ?? "";
            _cardSub.text = subtitle ?? "";
            _cardT = 0f;
            _cardDur = Mathf.Max(0.5f, seconds);
            _card.gameObject.SetActive(true);
            TickTitleCard(0f);
        }

        /// <summary>Takes the title card off at once.</summary>
        public void HideTitleCard()
        {
            _cardT = _cardDur;
            if (_card != null) _card.gameObject.SetActive(false);
        }

        void TickTitleCard(float dt)
        {
            if (_card == null) return;
            if (_cardT >= _cardDur) { if (_card.gameObject.activeSelf) _card.gameObject.SetActive(false); return; }
            _cardT += dt;
            float a = Mathf.Clamp01(_cardT / 0.25f) * Mathf.Clamp01((_cardDur - _cardT) / 0.35f);
            _cardGroup.alpha = a;
            if (_cardT >= _cardDur) _card.gameObject.SetActive(false);
        }

        // ---------------- tutorial prompt slip ----------------
        sealed class Slip
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image paper, strike;
            public Text verb, hint;
            public readonly Image[] capBg = new Image[2];
            public readonly Text[] capText = new Text[2];
            public Text slash;
        }

        Slip _slipNow, _slipDone;
        Vector2 _slipWorld;
        float _slipX = float.NaN;
        PromptView _viewNow, _viewDone;
        bool _hasNow, _hasDone;

        /// <summary>The current prompt's slip is on screen, with this verb (null when none).</summary>
        public string PromptVerb => _hasNow && _slipNow != null && _slipNow.root.gameObject.activeSelf ? _slipNow.verb.text : null;
        public string PromptHint => _hasNow && _slipNow != null ? _slipNow.hint.text : null;
        /// <summary>The key caps of the current prompt, in order.</summary>
        public string[] PromptKeys
        {
            get
            {
                if (!_hasNow || _slipNow == null) return new string[0];
                int n = _viewNow.keys != null ? Mathf.Min(2, _viewNow.keys.Length) : 0;
                var k = new string[n];
                for (int i = 0; i < n; i++) k[i] = _slipNow.capText[i].text;
                return k;
            }
        }
        /// <summary>The prompt just done is on screen, crossed out (its verb), or null.</summary>
        public string PromptDoneVerb => _hasDone && _slipDone != null && _slipDone.root.gameObject.activeSelf ? _slipDone.verb.text : null;

        Slip BuildSlip(string name)
        {
            var s = new Slip();
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_root, false);
            s.root = (RectTransform)go.transform;
            s.group = go.GetComponent<CanvasGroup>();
            s.group.blocksRaycasts = false;
            Place(s.root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 150f));
            s.paper = MakeImage("Paper", s.root, new Color(0.925f, 0.898f, 0.827f, 0.94f));
            Stretch(s.paper.rectTransform);
            var seal = MakeImage("Seal", s.root, KMath.Hex("#C0392B"));
            Place(seal.rectTransform, new Vector2(0f, 0.5f), new Vector2(-6f, 0f), new Vector2(12f, 120f));
            s.verb = MakeText("Verb", s.root, 54, TextAnchor.MiddleLeft, KMath.Hex("#1A1512"), new Vector2(0f, 1f), new Vector2(40f + 100f, -48f), new Vector2(240f, 70f), true);
            s.verb.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < 2; i++)
            {
                s.capBg[i] = MakeImage("Cap" + i, s.root, KMath.Hex("#1A1512"));
                s.capText[i] = MakeText("CapText" + i, s.capBg[i].transform, 34, TextAnchor.MiddleCenter, KMath.Hex("#ECE5D3"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 56f), true);
                var o = s.capText[i].GetComponent<Outline>();
                if (o != null) Destroy(o);
                s.capText[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            s.slash = MakeText("Slash", s.root, 34, TextAnchor.MiddleCenter, KMath.Hex("#1A1512"), new Vector2(0f, 1f), Vector2.zero, new Vector2(30f, 50f), true);
            var so = s.slash.GetComponent<Outline>();
            if (so != null) Destroy(so);
            s.hint = MakeText("Hint", s.root, 25, TextAnchor.MiddleLeft, new Color(0.012f, 0.012f, 0.012f, 0.72f), new Vector2(0f, 0f), new Vector2(40f + 270f, 30f), new Vector2(540f, 36f), false);
            var ho = s.hint.GetComponent<Outline>();
            if (ho != null) Destroy(ho);
            s.strike = MakeImage("Strike", s.root, KMath.Hex("#C0392B"));
            go.SetActive(false);
            return s;
        }

        void LayoutSlip(Slip s, in PromptView v)
        {
            s.verb.text = v.verb ?? "";
            s.hint.text = v.hint ?? "";
            float verbW = Mathf.Max(120f, s.verb.preferredWidth);
            float x = 40f;
            s.verb.rectTransform.sizeDelta = new Vector2(verbW, 70f);
            s.verb.rectTransform.anchoredPosition = new Vector2(x + verbW / 2f, -48f);
            x += verbW + 28f;
            int n = v.keys != null ? Mathf.Min(2, v.keys.Length) : 0;
            for (int i = 0; i < 2; i++)
            {
                bool on = i < n;
                s.capBg[i].gameObject.SetActive(on);
                if (!on) continue;
                s.capText[i].text = v.keys[i];
                float w = Mathf.Max(52f, s.capText[i].preferredWidth + 28f);
                var r = s.capBg[i].rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x, -48f);
                r.sizeDelta = new Vector2(w, 56f);
                x += w;
                if (i < n - 1)
                {
                    s.slash.gameObject.SetActive(true);
                    s.slash.text = "/";
                    s.slash.rectTransform.anchoredPosition = new Vector2(x + 17f, -48f);
                    x += 34f;
                }
            }
            if (n < 2) s.slash.gameObject.SetActive(false);
            float hw = Mathf.Max(s.hint.preferredWidth, 200f);
            s.hint.rectTransform.sizeDelta = new Vector2(hw, 36f);
            s.hint.rectTransform.anchoredPosition = new Vector2(40f + hw / 2f, 30f);
            float width = Mathf.Max(x, 40f + hw) + 34f;
            s.root.sizeDelta = new Vector2(width, 118f);
            s.paper.color = v.done ? new Color(0.788f, 0.643f, 0.361f, 0.94f) : new Color(0.925f, 0.898f, 0.827f, 0.94f);
            s.strike.gameObject.SetActive(v.done);
            if (v.done)
            {
                var r = s.strike.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(34f, -46f);
                r.sizeDelta = new Vector2(verbW + 10f, 5f);
                r.localRotation = Quaternion.Euler(0f, 0f, 3f);
            }
        }

        /// <summary>
        /// Shows the tutorial prompt <paramref name="v"/> on a slip above <paramref name="world"/> (Kage's head); call it every frame it is
        /// on. <paramref name="done"/>, when given, is the prompt just cleared, drawn crossed out above the current one. Call
        /// <see cref="ClearPrompts"/> when nothing shows.
        /// </summary>
        public void SetPrompt(PromptView? now, PromptView? done, Vector2 world)
        {
            _slipWorld = world;
            _hasNow = now.HasValue;
            _hasDone = done.HasValue;
            if (now.HasValue)
            {
                _slipNow ??= BuildSlip("PromptSlip");
                _viewNow = now.Value;
                if (!_slipNow.root.gameObject.activeSelf) _slipNow.root.gameObject.SetActive(true);
                LayoutSlip(_slipNow, _viewNow);
                _slipNow.group.alpha = _viewNow.alpha;
            }
            else if (_slipNow != null && _slipNow.root.gameObject.activeSelf) _slipNow.root.gameObject.SetActive(false);
            if (done.HasValue)
            {
                _slipDone ??= BuildSlip("PromptSlipDone");
                _viewDone = done.Value;
                if (!_slipDone.root.gameObject.activeSelf) _slipDone.root.gameObject.SetActive(true);
                LayoutSlip(_slipDone, _viewDone);
                _slipDone.group.alpha = _viewDone.alpha;
            }
            else if (_slipDone != null && _slipDone.root.gameObject.activeSelf) _slipDone.root.gameObject.SetActive(false);
            PlaceSlips();
        }

        public void ClearPrompts() => SetPrompt(null, null, _slipWorld);

        void PlaceSlips()
        {
            if (_cam == null) return;
            Vector3 sp = _cam.WorldToScreenPoint(_slipWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var lp);
            // the slip drifts after Kage (JS damp 5), and stays on screen
            _slipX = float.IsNaN(_slipX) ? lp.x : Mathf.Lerp(_slipX, lp.x, 1f - Mathf.Exp(-5f * Mathf.Max(Time.unscaledDeltaTime, 0.0001f)));
            var rect = _root.rect;
            void Put(Slip s, bool on, float rise)
            {
                if (s == null || !on) return;
                float w = s.root.sizeDelta.x;
                float x = Mathf.Clamp(_slipX, rect.xMin + 24f + w / 2f, rect.xMax - 24f - w / 2f);
                s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 0.5f);
                s.root.anchoredPosition = new Vector2(x, lp.y + 40f + rise);
            }
            Put(_slipNow, _hasNow, 0f);
            Put(_slipDone, _hasDone, _viewDone.rise);
        }

        void TickStageCard(float dt)
        {
            TickTitleCard(dt);
            if (_hasNow || _hasDone) PlaceSlips();
        }
    }
}
