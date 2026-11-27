using UnityEngine;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The settings screen (JS "Settings Screen", wonder.css .sg-*): the settings rows as segmented controls in one panel and the
    /// help card beside it. The challenge row (difficulty) is replaced by the dev-mode switch, because the Unity game is fixed
    /// to Normal. Turning dev mode off asks first (the flow's confirm overlay) because it replaces the save with a fresh one.
    /// </summary>
    public sealed class SettingsScreen : FlowPanel
    {
        Transform panelParent;

        public SettingsScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Settings") { }

        protected override string DefaultFocus { get { return "devmode:" + (C.Save.devMode ? 0 : 1); } }

        protected override void Build()
        {
            var s = C.Save;
            // backdrop: sunset over the torii
            Ui.Grad(Root, 0, 0, 1600, 900, Ui.Sky1, Ui.Sky3, false, Ui.Sky2);
            Ui.Glow(Root, 800, 880, 420, Ui.A(Ui.Acc, 0.4f), 1.2f);
            Ui.Disc(Root, 800, 880, 150, Ui.Deep);
            Ui.Ground2(Root, new float[] { 0, 850, 140, 812, 300, 836, 460, 806, 620, 838, 800, 820, 980, 840, 1140, 808, 1300, 836, 1460, 810, 1600, 830 }, 900, Ui.Mix(Ui.Ground, Color.black, 0.85f));
            Color torii = Ui.Mix(Ui.Ground, Color.black, 0.55f);
            Ui.Shape(Root, 0, 0, 1600, 900, new[] { new Vector2(724, 804), new Vector2(800, 812), new Vector2(876, 804), new Vector2(873, 814), new Vector2(800, 820), new Vector2(727, 814) }, torii);
            Ui.Box(Root, 740, 830, 120, 8, torii);
            Ui.Box(Root, 754, 814, 12, 86, torii);
            Ui.Box(Root, 834, 814, 12, 86, torii);
            Ui.Box(Root, 795, 815, 10, 16, torii);
            Ui.Box(Root, 0, 872, 1600, 28, Ui.Mix(Ui.Ground, Color.black, 0.45f));
            ArmoryScreen.Leaves(Root, new[] { new[] { 1000f, 826f, 1.3f, 20f }, new[] { 600f, 850f, 1.1f, -30f }, new[] { 1500f, 860f, 1.4f, 40f }, new[] { 990f, 500f, 1f, -10f } });

            // top bar
            UiParts.Back(Root, Nav, () => C.Back(), 40, 19);
            var title = Ui.Label(Root, "SETTINGS", UiFont.Title, 36, Ui.Bone, 118, 24, 400, 48, TextAnchor.MiddleLeft);
            Ui.Label(Root, "設定", UiFont.Mincho, 22, Ui.Gold, 118 + UiParts.Width(title) + 18, 24, 100, 48, TextAnchor.MiddleLeft);
            UiParts.Pills(Root, s, 1560f);

            // the panel rises as one (JS .sg-panel: wRise .5 s)
            var panel = Ui.Node(Root, "PanelLayer", 0, 0, 1600, 900);
            panelParent = panel;
            Enter(panel, UiAnims.WRise(.5f));
            float px = 40, py = 112, pw = 940;
            float y = py + 28;
            float ph = 560f;
            Ui.Box(panel, px, py, pw, ph, Ui.PanelA(0.92f), "PanelBg");
            Ui.Box(panel, px, py, pw, 2, Ui.Gold, "PanelAccent");
            var frame = Ui.Node(panel, "PanelFrame", px, py, pw, ph);
            Ui.Frame(frame, 0, 0, pw, ph, 1.5f, Ui.A(Ui.GoldDim, 0.85f));
            Ui.Frame(frame, 4, 4, pw - 8, ph - 8, 1f, Ui.A(Ui.Bone, 0.04f));

            UiParts.Section(panel, px + 40, y, "Development", "開発");
            y += 28;
            Row(px, ref y, pw, "Dev Mode", "On unlocks every stage and item and grants coins. Off starts the real economy from a fresh save.",
                "devmode", new[] { "On", "Off" }, s.devMode ? 0 : 1, i => C.SetDevMode(i == 0), true);
            y += 22;
            UiParts.Section(panel, px + 40, y, "Feel & Sound", "音");
            y += 28;
            Row(px, ref y, pw, "Screen Shake", "Camera shake on heavy impacts", "shake", new[] { "Full", "Half", "Off" }, IndexOf(new[] { "full", "half", "off" }, s.settings.shake),
                i => C.UpdateSettings(x => x.shake = new[] { "full", "half", "off" }[i]), false);
            Row(px, ref y, pw, "Reduced Flashing", "Replaces inverted impact frames with a soft dim", "flash", new[] { "Off", "On" }, s.settings.reduceFlash ? 1 : 0,
                i => C.UpdateSettings(x => x.reduceFlash = i == 1), false);
            Row(px, ref y, pw, "Sound Effects", "", "sound", new[] { "On", "Off" }, s.settings.sound ? 0 : 1, i => C.UpdateSettings(x => x.sound = i == 0), false);
            Row(px, ref y, pw, "Music", "", "music", new[] { "On", "Off" }, s.settings.music ? 0 : 1, i => C.UpdateSettings(x => x.music = i == 0), false, last: true);

            // help card
            var help = Ui.Node(Root, "Help", 1010, 112, 550, 316);
            Enter(help, UiAnims.WRise(.5f, .16f));
            Ui.Box(help, 0, 0, 550, 316, Ui.PanelA(0.92f));
            Ui.Box(help, 0, 0, 550, 2, Ui.Gold);
            Ui.Frame(help, 0, 0, 550, 316, 1.5f, Ui.A(Ui.GoldDim, 0.85f));
            Ui.Frame(help, 4, 4, 542, 308, 1f, Ui.A(Ui.Bone, 0.04f));
            UiParts.Section(help, 32, 28, "Help & Data", null);
            var guide = Ui.Node(help, "Guide", 32, 72, 486, 56);
            Ui.Box(guide, 0, 0, 486, 56, Ui.PanelA(0.85f));
            var ring = Ui.Box(guide, 0, 0, 486, 56, Color.clear);
            Ui.Frame(guide, 0, 0, 486, 56, 1.5f, Ui.Gold);
            var guideFocus = Ui.Frame2(guide, 486, 56);
            Ui.Label(guide, "CONTROLS GUIDE", UiFont.Title, 17, Ui.Gold, 0, 0, 486, 56, TextAnchor.MiddleCenter, 3.74f);
            UiParts.Make(guide, Nav, "guide", () => flow.OpenControls(), f =>
            {
                ring.color = f ? Ui.A(Ui.Gold, 0.14f) : Color.clear;
                guideFocus.color = f ? Ui.Gold : Color.clear;
            });
            Ui.Label(help, "Keyboard, gamepad and mouse all work in the menus: arrows or d-pad to move, Enter or A to choose, Esc or B to go back.", UiFont.Body, 14, Ui.Muted, 32, 150, 486, 62, TextAnchor.UpperLeft, 0, true, 1.4f);
            Ui.Box(help, 32, 224, 486, 1, Ui.Hair);
            Ui.Label(help, "Progress saves automatically to " + SaveStore.FileName + ".", UiFont.Body, 14, Ui.Muted, 32, 240, 486, 50, TextAnchor.UpperLeft, 0, true, 1.4f);

            UiParts.Keys(Root, 40, 800, new[] { "ENTER", "Select" }, new[] { "ESC", "Back" }, new[] { "← / →", "Change" });
        }

        static int IndexOf(string[] values, string v)
        {
            int i = System.Array.IndexOf(values, v);
            return i < 0 ? 0 : i;
        }

        void Row(float px, ref float y, float pw, string label, string hint, string key, string[] options, int active, System.Action<int> onPick, bool first, bool last = false)
        {
            float h = string.IsNullOrEmpty(hint) ? 78f : hint.Length > 70 ? 104f : 90f;
            float top = y + (first ? 20 : 18);
            var t = Ui.Label(panelParent, label, UiFont.Title, 19, Ui.Bone, px + 40, top, 420, 26, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(hint)) Ui.Label(panelParent, hint, UiFont.Body, 14, Ui.Muted, px + 40, top + 28, 420, 44, TextAnchor.UpperLeft, 0, true, 1.3f);
            float segY = y + (h - 42f) / 2f;
            UiParts.Seg(panelParent, Nav, key, px + pw - 40, segY, options, active, onPick);
            y += h;
            if (!last) Ui.Box(panelParent, px + 40, y - 1, pw - 80, 1, Ui.Hair);
        }
    }
}
