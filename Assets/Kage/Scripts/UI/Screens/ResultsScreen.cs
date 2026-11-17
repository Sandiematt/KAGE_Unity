using System.Text;
using UnityEngine;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// A placeholder results step (SAN-67; the real results screen is SAN-73): win or defeat, the rating and what the stage paid.
    /// A win goes back to the map, a defeat offers a retry.
    /// </summary>
    public sealed class ResultsScreen : FlowPanel
    {
        public ResultsScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Results") { }

        protected override string DefaultFocus { get { return C.LastOutcome != null && C.LastOutcome.Win ? "continue" : "retry"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { C.ContinueFromResults(); return true; }
            return base.Handle(cmd);
        }

        protected override void Build()
        {
            var o = C.LastOutcome;
            if (o == null) return;
            var stage = GameRules.Stage(o.Stage);
            string name = stage == null ? "" : (stage.Kind != "stretch" && !string.IsNullOrEmpty(stage.Foe) ? stage.Foe : stage.Name);
            const float x = 450, y = 130, w = 700, h = 640;
            var card = OverlayKit.Card(Root, x, y, w, h);

            Ui.Label(card, (o.Win ? "STAGE CLEARED" : "DEFEATED"), UiFont.Cond, 15, o.Win ? Ui.Gold : Ui.Hot, 48, 32, 500, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, name, UiFont.Mincho, 44, Ui.Bone, 48, 56, 604, 60, TextAnchor.MiddleLeft);

            // the rating
            Ui.Label(card, o.Rating.ToString(), UiFont.TitleHeavy, 150, o.Win ? Ui.Gold : Ui.Muted, 48, 128, 200, 170, TextAnchor.MiddleCenter);
            Ui.Label(card, "RATING", UiFont.Cond, 14, Ui.Muted, 48, 296, 200, 20, TextAnchor.MiddleCenter, 3.4f);

            float ry = 140;
            Row(card, ref ry, "Coins", "+" + o.Coins);
            Row(card, ref ry, "Jade", "+" + o.Gems);
            Row(card, ref ry, "Experience", "+" + o.Xp);
            if (o.LevelUps > 0) Row(card, ref ry, "Level up", "Level " + C.Save.level);
            var r = C.LastResult;
            if (r != null) Row(card, ref ry, "Time", Mathf.FloorToInt((float)r.time / 60f) + ":" + Mathf.FloorToInt((float)r.time % 60f).ToString("00"));

            // what the clear opened
            var sb = new StringBuilder();
            foreach (var u in o.Unlocks)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append("Unlocked: ").Append(UnlockName(u));
            }
            if (o.Win && o.Next != null && !HasChapter(o)) sb.Append(sb.Length > 0 ? "\n" : "").Append("Next: ").Append(GameRules.Stage(o.Next).Name);
            if (sb.Length > 0) Ui.Label(card, sb.ToString(), UiFont.Body, 16, Ui.Muted, 48, 372, 604, 110, TextAnchor.UpperLeft, 0, true, 1.35f);

            if (o.Win)
            {
                UiParts.Plate(card, Nav, "continue", 48, 520, 296, 60, "Continue", true, () => C.ContinueFromResults());
                UiParts.Plate(card, Nav, "retry", 356, 520, 296, 60, "Retry", false, () => C.RetryFromResults());
            }
            else
            {
                UiParts.Plate(card, Nav, "retry", 48, 520, 296, 60, "Retry", true, () => C.RetryFromResults());
                UiParts.Plate(card, Nav, "continue", 356, 520, 296, 60, "Back to map", false, () => C.ContinueFromResults());
            }
        }

        static bool HasChapter(StageOutcome o)
        {
            foreach (var u in o.Unlocks) if (u.Kind == "chapter") return true;
            return false;
        }

        static string UnlockName(StageUnlock u)
        {
            if (u.Kind == "chapter") { var c = GameRules.Chapter(u.Id); return c != null ? c.Name + " · " + c.Title : u.Id; }
            var item = GameRules.Item(GameRules.KindFromName(u.Kind), u.Id);
            return item != null ? Clean(item.Name) : u.Id;
        }

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (c >= 32) sb.Append(c);
            return sb.ToString();
        }

        static void Row(Transform card, ref float y, string label, string value)
        {
            Ui.Label(card, label.ToUpperInvariant(), UiFont.Cond, 16, Ui.Muted, 300, y, 200, 28, TextAnchor.MiddleLeft, 2.6f);
            Ui.Label(card, value, UiFont.Title, 22, Ui.Bone, 452, y, 200, 28, TextAnchor.MiddleRight);
            Ui.Box(card, 300, y + 32, 352, 1, Ui.Hair);
            y += 40;
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
