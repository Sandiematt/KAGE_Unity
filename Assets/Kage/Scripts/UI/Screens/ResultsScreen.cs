using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The results screen (JS showResult, SAN-73): on the right the eyebrow, Victory / Defeated, one line, the three seals and the
    /// rating (a win) or the recap and tip (a loss), the rewards, the experience bar and the buttons; the stage world stands on the
    /// left in the victory or defeat pose. What it shows is <see cref="ResultsView"/>; the numbers come from the rules.
    /// </summary>
    public sealed class ResultsScreen : FlowPanel
    {
        const float ColX = 944f, ColY = 117f, ColW = 496f;
        const float CountDelay = 0.5f, CountSeconds = 0.9f, BarDelay = 0.6f, BarSeconds = 1.1f;

        ResultsView view;
        float shownAt;
        int toastsShown;
        /// <summary>The JS afterShow toasts: the first at .9 s, then one every .7 s (unlocks, a new chapter, a level up).</summary>
        public const float ToastFirst = 0.9f, ToastGap = 0.7f;
        Text coinText;
        RectTransform barGain;
        float barBase, barGainW;

        public ResultsScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Results") { }

        /// <summary>The data on screen (null until a stage has finished).</summary>
        public ResultsView View { get { return view; } }

        protected override string DefaultFocus { get { return view != null ? view.PrimaryKey : null; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { C.ContinueFromResults(); return true; }
            return base.Handle(cmd);
        }

        protected override void Build()
        {
            var o = C.LastOutcome;
            coinText = null;
            barGain = null;
            if (o == null || C.LastResult == null) { view = null; return; }
            view = ResultsView.Build(C.Save, o, C.LastResult);
            shownAt = UiClock.Now + UiClock.EntranceDelay;
            toastsShown = 0;
            var v = view;
            var accent = v.Win ? Hex("#f3d48f") : Hex("#e8443a");

            // Smooth obsidian gradient wash on the right so the results column reads cleanly with zero vertical banding
            Ui.Grad(Root, 700, 0, 380, 900, Ui.A(Ui.Void, 0f), Ui.A(Ui.Void, 0.80f), true);
            Ui.Box(Root, 1080, 0, 520, 900, Ui.A(Ui.Void, 0.80f), "Wash");
            Ui.Grad(Root, 916, 112, 1.5f, 676, Color.clear, Color.clear, false, Ui.A(accent, 0.35f));

            float y = ColY;
            Ui.Label(Root, v.Eyebrow.ToUpperInvariant(), UiFont.Cond, 13, Ui.Muted, ColX, y, ColW, 18, TextAnchor.MiddleLeft, 3.1f);
            y += 18 + 6;
            var titleNode = Ui.Node(Root, "Title", ColX, y, ColW, 112);
            Ui.Label(titleNode, v.Title, UiFont.Mincho, 100, Ui.A(Color.black, 0.5f), 0, 4, ColW, 108, TextAnchor.MiddleLeft);
            Ui.Label(titleNode, v.Title, UiFont.Mincho, 100, accent, 0, 0, ColW, 108, TextAnchor.MiddleLeft);
            Enter(titleNode, UiAnims.TitleRise());
            y += 108 + 4;
            Ui.Label(Root, v.Line, UiFont.Mincho, 23, Ui.Bone, ColX, y, ColW, 60, TextAnchor.UpperLeft, 0, true, 1.1f);
            y += 62 + 24;

            if (v.ShowRewards)
            {
                // seals and rating: each seal pops in 120 ms after the one before (cardIn2, .5 s), the rating drops in large and turned (.6 s after .6 s)
                for (int i = 0; i < 3; i++)
                {
                    float cx = ColX + 29 + i * 128;
                    bool on = v.Seals[i];
                    var sealNode = Ui.Node(Root, "Seal" + i, cx - 60, y, 120, 100);
                    Enter(sealNode, UiAnims.CardIn(.5f, .25f + i * .12f));
                    Ui.Diamond(sealNode, 60, 32, 40, on ? Ui.Acc : Color.clear, on ? Hex("#e05a4f") : Ui.Hex("#3a342b"), 2f);
                    if (on) Ui.Diamond(sealNode, 60, 32, 12, Ui.Bone, Ui.Bone, 0);
                    Ui.Label(sealNode, v.SealNames[i].ToUpperInvariant(), UiFont.Cond, 13, on ? Ui.Bone : Ui.Muted, 0, 74, 120, 18, TextAnchor.MiddleCenter, 2.6f);
                }
                var ratingNode = Ui.Node(Root, "Rating", ColX + ColW - 110, y + 2, 110, 90);
                Enter(ratingNode, UiAnims.VsPop(.6f, .6f, 2.4f, -8f));
                Ui.Label(ratingNode, v.Rating, UiFont.Mincho, 56, Ui.Gold, 0, 0, 110, 66, TextAnchor.MiddleCenter);
                Ui.Label(ratingNode, "RATING", UiFont.Cond, 12, Ui.Muted, 0, 68, 110, 18, TextAnchor.MiddleCenter, 2.8f);
                y += 100 + 28;

                // rewards: the coins, the gems and each item pop in (cardIn2, .45 s; the first two after .5 and .62 s)
                Ui.Box(Root, ColX, y, ColW, 1, Ui.Hair);
                float rx = ColX, ry = y + 20;
                var coinNode = Ui.Node(Root, "Coins", rx, ry - 2, 150, 44);
                Enter(coinNode, UiAnims.CardIn(.45f, .5f));
                Ui.Disc(coinNode, 10, 22, 10, Ui.Gold);
                Ui.Disc(coinNode, 10, 22, 6, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                Ui.Disc(coinNode, 10, 22, 3.2f, Ui.Gold);
                coinText = Ui.Label(coinNode, "+0", UiFont.Cond, 27, Ui.Bone, 30, 2, 120, 40, TextAnchor.MiddleLeft);
                rx += 30 + 120;
                if (v.Gems > 0)
                {
                    var gemNode = Ui.Node(Root, "Gems", rx, ry - 2, 90, 44);
                    Enter(gemNode, UiAnims.CardIn(.45f, .62f));
                    Ui.Diamond(gemNode, 9, 22, 13, Ui.Ruby, Ui.Ruby, 0);
                    Ui.Label(gemNode, "+" + v.Gems, UiFont.Cond, 27, Ui.Bone, 26, 2, 60, 40, TextAnchor.MiddleLeft);
                    rx += 26 + 62;
                }
                for (int n = 0; n < v.Items.Count; n++)
                {
                    string name = v.Items[n];
                    var label = Ui.Label(Root, name.ToUpperInvariant(), UiFont.Cond, 16, Ui.Bone, 0, 0, 220, 40, TextAnchor.MiddleLeft, 2.5f);
                    float lw = UiParts.Width(label, 2.5f);
                    var chip = Ui.Node(Root, "Item" + n, rx, ry - 2, 56 + lw + 8, 44);
                    Enter(chip, UiAnims.CardIn(.45f));
                    Ui.Box(chip, 0, 0, 44, 44, Ui.Charcoal);
                    // the item itself, drawn (JS paintIcon into the 44 px canvas)
                    var art = n < v.ItemArt.Count ? v.ItemArt[n] : null;
                    if (art != null && art.Texture != null)
                    {
                        var pic = Ui.Pic(chip, art.Key, 2, 2, 40, 40);
                        FitPic(pic, 40, 40);
                    }
                    else Ui.Diamond(chip, 22, 22, 14, Ui.Gold, Ui.Gold, 0);
                    Ui.Frame(chip, 0, 0, 44, 44, 1, Ui.Gold);
                    label.transform.SetParent(chip, false);
                    label.rectTransform.anchoredPosition = new Vector2(56, -2);
                    rx += 56 + lw + 24;
                }
                y += 80;
                Ui.Box(Root, ColX, y, ColW, 1, Ui.Hair);
                y += 1 + 32;
            }
            else
            {
                Ui.Box(Root, ColX, y, ColW, 1, Ui.Hair);
                Ui.Label(Root, v.EnemyDamagePct + "%", UiFont.Cond, 27, Ui.Bone, ColX, y + 20, 180, 36, TextAnchor.MiddleLeft);
                Ui.Label(Root, (JsTables.Enemy(GameRules.Stage(v.StageId).Enemy) != null && JsTables.Enemy(GameRules.Stage(v.StageId).Enemy).Type == "regular" ? "ENEMY" : "BOSS") + " HEALTH TAKEN", UiFont.Cond, 12, Ui.Muted, ColX, y + 56, 220, 18, TextAnchor.MiddleLeft, 2.6f);
                Ui.Disc(Root, ColX + 270 + 10, y + 38, 10, Ui.Gold);
                Ui.Disc(Root, ColX + 270 + 10, y + 38, 6, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                Ui.Disc(Root, ColX + 270 + 10, y + 38, 3.2f, Ui.Gold);
                Ui.Label(Root, "+" + v.Coins, UiFont.Cond, 27, Ui.Bone, ColX + 270 + 30, y + 20, 140, 36, TextAnchor.MiddleLeft);
                Ui.Label(Root, "KEPT", UiFont.Cond, 12, Ui.Muted, ColX + 270, y + 56, 120, 18, TextAnchor.MiddleLeft, 2.6f);
                y += 96;
                Ui.Box(Root, ColX, y, ColW, 1, Ui.Hair);
                y += 1 + 20;
                var tipCard = Ui.Node(Root, "TipCard", ColX, y, ColW, 88);
                Ui.Box(tipCard, 0, 0, ColW, 88, Ui.PanelA(0.82f));
                Ui.Box(tipCard, 0, 0, 3, 88, Ui.Gold);
                Ui.Frame(tipCard, 0, 0, ColW, 88, 1, Ui.Hair);
                Ui.Diamond(tipCard, 18, 22, 7, Ui.Gold, Ui.Gold, 0);
                Ui.Label(tipCard, v.Tip, UiFont.Body, 15, Ui.Bone, 32, 12, ColW - 46, 68, TextAnchor.UpperLeft, 0f, true, 1.2f);
                y += 88 + 20;
            }

            // experience
            string lvl = ("Level " + v.Level + (v.LevelUp ? " · Level up" : "")).ToUpperInvariant();
            Ui.Label(Root, lvl, UiFont.Cond, 15, Ui.Bone, ColX, y, 260, 22, TextAnchor.MiddleLeft, 3f);
            string xp = UiParts.Num(v.Xp) + " / " + UiParts.Num(v.XpNeed) + " · ";
            Ui.Label(Root, xp + "+" + v.XpGain, UiFont.Body, 15, Ui.Muted, ColX + 200, y, ColW - 200, 22, TextAnchor.MiddleRight);
            y += 22 + 10;
            Ui.Box(Root, ColX, y, ColW, 6, Ui.Hex("#3a342b"));
            barBase = (float)(v.XpBasePct / 100.0) * ColW;
            barGainW = (float)(v.XpGainPct / 100.0) * ColW;
            Ui.Box(Root, ColX, y, barBase, 6, Ui.GoldDim);
            var gainNode = Ui.Box(Root, ColX + barBase, y, 0, 6, Ui.Gold, "XpGain");
            barGain = gainNode.rectTransform;
            y += 6 + 32;

            // buttons
            float bx = ColX;
            for (int i = 0; i < v.Buttons.Length; i++)
            {
                var b = v.Buttons[i];
                bool primary = i == 0;
                // a loss has three buttons (JS: Retry, Upgrade, Map) and the column is only 496 wide
                float bw = v.Buttons.Length > 2 ? (i == 0 ? 210 : i == 1 ? 170 : 96) : primary ? 256 : 190;
                string key = b.Key;
                UiParts.Plate(Root, Nav, key, bx, y, bw, 60, b.Label, primary, () =>
                {
                    if (key == "continue") C.ContinueFromResults();
                    else if (key == "armory") C.OpenArmory();
                    else C.RetryFromResults();
                });
                bx += bw + 16;
            }
            Tick();
        }

        static Color Hex(string h) { return Ui.Hex(h); }

        static void FitPic(UnityEngine.UI.RawImage pic, float w, float h)
        {
            var t = pic.texture;
            if (t == null) return;
            float k = Mathf.Min(w / t.width, h / t.height);
            float iw = t.width * k, ih = t.height * k;
            pic.rectTransform.anchoredPosition += new Vector2((w - iw) / 2f, -(h - ih) / 2f);
            pic.rectTransform.sizeDelta = new Vector2(iw, ih);
        }

        /// <summary>The coin count-up and the experience bar fill (JS afterShow), from the moment the screen was built.</summary>
        public override void Tick()
        {
            if (view == null) return;
            float t = UiClock.Now - shownAt;
            while (toastsShown < view.Toasts.Count && t >= ToastFirst + toastsShown * ToastGap)
            {
                var toast = view.Toasts[toastsShown++];
                C.Toast(toast.Title, toast.Text);
            }
            if (coinText != null)
            {
                float k = Mathf.Clamp01((t - CountDelay) / CountSeconds);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                coinText.text = "+" + UiParts.Num(Mathf.RoundToInt(view.Coins * e));
            }
            if (barGain != null)
            {
                float k = Mathf.Clamp01((t - BarDelay) / BarSeconds);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                var sd = barGain.sizeDelta;
                barGain.sizeDelta = new Vector2(Mathf.Max(0f, barGainW - barBase) * e, sd.y);
            }
        }
    }

    /// <summary>
    /// The stage's own screen layer: the plain title card at the start of a stage (spec: no story scenes, straight to playing)
    /// and a small pause hint during the fight. The health bars and the rest of the fight HUD are the stage world's HUD.
    /// </summary>
    public sealed class StageHudPanel : FlowPanel
    {
        CanvasGroup card;
        UnityEngine.UI.Text hint;

        public StageHudPanel(GameFlow flow, RectTransform stage) : base(flow, stage, "StageHud") { }

        protected override void Build()
        {
            var stage = GameRules.Stage(C.StageId);
            if (stage == null) return;
            var ch = GameRules.ChapterOf(stage.Id);
            string name = stage.Kind != "stretch" && !string.IsNullOrEmpty(stage.Foe) ? stage.Foe : stage.Name;
            var c = Ui.Node(Root, "TitleCard", 0, 330, 1600, 240);
            card = c.gameObject.AddComponent<CanvasGroup>();
            Ui.Grad(c, 0, 0, 1600, 240, Ui.A(Color.black, 0f), Ui.A(Color.black, 0f), false, Ui.A(Color.black, 0.72f));
            Ui.Grad(c, 220, 40, 1160, 1, Color.clear, Color.clear, true, Ui.A(Ui.Gold, 0.45f));
            Ui.Grad(c, 220, 200, 1160, 1, Color.clear, Color.clear, true, Ui.A(Ui.Gold, 0.45f));
            Ui.Label(c, (ch.Name + " · " + CampaignView.KindName(stage.Kind)).ToUpperInvariant(), UiFont.Title, 16, Ui.Gold, 0, 60, 1600, 24, TextAnchor.MiddleCenter, 6f);
            var nm = Ui.Label(c, name, UiFont.Mincho, 64, Ui.Bone, 0, 88, 1600, 84, TextAnchor.MiddleCenter);
            var nmSh = nm.gameObject.AddComponent<Shadow>();
            nmSh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            nmSh.effectDistance = new Vector2(0f, -2.5f);
            Ui.Box(c, 740, 182, 120, 2, Ui.Gold);
            hint = Ui.Label(Root, "ESC / START  ·  PAUSE", UiFont.Cond, 13, Ui.A(Ui.Bone, 0.55f), 1100, 860, 460, 20, TextAnchor.MiddleRight, 2.4f);
        }

        public override void Tick()
        {
            var w = flow.World;
            if (card == null || w == null) return;
            float a = 0f;
            if (w.phase == StageWorld.Phase.Intro)
            {
                float t = w.PhaseTime, d = StageWorld.IntroSeconds;
                a = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((d - t) / 0.35f);
            }
            card.alpha = a;
            if (hint != null) hint.enabled = w.phase == StageWorld.Phase.Fight;
        }
    }
}
