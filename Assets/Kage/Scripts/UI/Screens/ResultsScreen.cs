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
            shownAt = Time.unscaledTime;
            var v = view;
            var accent = v.Win ? Hex("#f3d48f") : Hex("#e8443a");

            // a dark wash on the right so the column reads over the world
            for (int i = 0; i < 24; i++) Ui.Box(Root, 760 + i * 14, 0, 14, 900, Ui.A(Color.black, 0.7f * (i + 1) / 24f), "Wash");
            Ui.Box(Root, 1096, 0, 504, 900, Ui.A(Color.black, 0.7f), "Wash");

            float y = ColY;
            Ui.Label(Root, v.Eyebrow.ToUpperInvariant(), UiFont.Cond, 13, Ui.Muted, ColX, y, ColW, 18, TextAnchor.MiddleLeft, 3.1f);
            y += 18 + 6;
            Ui.Label(Root, v.Title, UiFont.Mincho, 100, Ui.A(Color.black, 0.5f), ColX + 0, y + 4, ColW, 108, TextAnchor.MiddleLeft);
            Ui.Label(Root, v.Title, UiFont.Mincho, 100, accent, ColX, y, ColW, 108, TextAnchor.MiddleLeft);
            y += 108 + 4;
            Ui.Label(Root, v.Line, UiFont.Mincho, 23, Ui.Bone, ColX, y, ColW, 60, TextAnchor.UpperLeft, 0, true, 1.1f);
            y += 62 + 24;

            if (v.ShowRewards)
            {
                // seals and rating
                for (int i = 0; i < 3; i++)
                {
                    float cx = ColX + 29 + i * 128;
                    bool on = v.Seals[i];
                    var d = Ui.Diamond(Root, cx, y + 32, 40, on ? Ui.Acc : Color.clear, on ? Hex("#e05a4f") : Ui.Hex("#3a342b"), 2f);
                    if (on) Ui.Diamond(Root, cx, y + 32, 12, Ui.Bone, Ui.Bone, 0);
                    Ui.Label(Root, v.SealNames[i].ToUpperInvariant(), UiFont.Cond, 13, on ? Ui.Bone : Ui.Muted, cx - 60, y + 74, 120, 18, TextAnchor.MiddleCenter, 2.6f);
                }
                Ui.Label(Root, v.Rating, UiFont.Mincho, 56, Ui.Gold, ColX + ColW - 110, y + 2, 110, 66, TextAnchor.MiddleCenter);
                Ui.Label(Root, "RATING", UiFont.Cond, 12, Ui.Muted, ColX + ColW - 110, y + 70, 110, 18, TextAnchor.MiddleCenter, 2.8f);
                y += 100 + 28;

                // rewards
                Ui.Box(Root, ColX, y, ColW, 1, Ui.Hair);
                float rx = ColX, ry = y + 20;
                Ui.Disc(Root, rx + 10, ry + 20, 10, Ui.Gold);
                Ui.Disc(Root, rx + 10, ry + 20, 6, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                Ui.Disc(Root, rx + 10, ry + 20, 3.2f, Ui.Gold);
                coinText = Ui.Label(Root, "+0", UiFont.Cond, 27, Ui.Bone, rx + 30, ry, 140, 40, TextAnchor.MiddleLeft);
                rx += 30 + 120;
                if (v.Gems > 0)
                {
                    Ui.Diamond(Root, rx + 9, ry + 20, 13, Ui.Ruby, Ui.Ruby, 0);
                    Ui.Label(Root, "+" + v.Gems, UiFont.Cond, 27, Ui.Bone, rx + 26, ry, 80, 40, TextAnchor.MiddleLeft);
                    rx += 26 + 62;
                }
                foreach (var name in v.Items)
                {
                    Ui.Box(Root, rx, ry - 2, 44, 44, Ui.Charcoal);
                    Ui.Frame(Root, rx, ry - 2, 44, 44, 1, Ui.Gold);
                    Ui.Diamond(Root, rx + 22, ry + 20, 14, Ui.Gold, Ui.Gold, 0);
                    var t = Ui.Label(Root, name.ToUpperInvariant(), UiFont.Cond, 16, Ui.Bone, rx + 56, ry, 220, 40, TextAnchor.MiddleLeft, 2.5f);
                    rx += 56 + UiParts.Width(t, 2.5f) + 24;
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
                y += 1 + 24;
                Ui.Diamond(Root, ColX + 9, y + 12, 8, Ui.Gold, Ui.Gold, 0);
                Ui.Label(Root, v.Tip, UiFont.Body, 15, Ui.Bone, ColX + 28, y, ColW - 28, 84, TextAnchor.UpperLeft, 0f, true, 1.15f);
                y += 84 + 20;
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

        /// <summary>The coin count-up and the experience bar fill (JS afterShow), from the moment the screen was built.</summary>
        public override void Tick()
        {
            if (view == null) return;
            float t = Time.unscaledTime - shownAt;
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
            Ui.Grad(c, 0, 0, 1600, 240, Ui.A(Color.black, 0f), Ui.A(Color.black, 0f), false, Ui.A(Color.black, 0.62f));
            Ui.Label(c, (ch.Name + " · " + CampaignView.KindName(stage.Kind)).ToUpperInvariant(), UiFont.Title, 16, Ui.Gold, 0, 60, 1600, 24, TextAnchor.MiddleCenter, 6f);
            Ui.Label(c, name, UiFont.Mincho, 64, Ui.Bone, 0, 88, 1600, 84, TextAnchor.MiddleCenter);
            Ui.Box(c, 740, 182, 120, 2, Ui.Gold);
            hint = Ui.Label(Root, "ESC / START  ·  PAUSE", UiFont.Cond, 13, Ui.A(Ui.Bone, 0.5f), 1100, 860, 460, 20, TextAnchor.MiddleRight, 2.4f);
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
