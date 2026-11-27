using UnityEngine;
using Kage.Rules;

namespace Kage
{
    /// <summary>Shared look of the JS overlay card (.ov-card): charcoal, a gold line on top, a dimmed screen behind.</summary>
    static class OverlayKit
    {
        /// <summary>The card of an overlay; when the overlay is opening (<paramref name="enter"/>) it pops in like the JS <c>cardIn2</c> (.45 s, up and in with the pop curve).</summary>
        public static RectTransform Card(Transform root, float x, float y, float w, float h, bool enter = false)
        {
            Ui.Box(root, 0, 0, 1600, 900, new Color(0f, 0f, 0f, 0.70f), "Dim");
            var card = Ui.Node(root, "Card", x, y, w, h);
            if (enter) UiFx.In(card, UiAnims.CardIn(.45f));
            Ui.Box(card, -4, 4, w + 8, h + 4, new Color(0f, 0f, 0f, 0.45f), "Shadow");
            Ui.Box(card, 0, 0, w, h, Ui.Charcoal);
            Ui.Grad(card, 0, 0, w, 120, Ui.A(Ui.Acc, 0.08f), Color.clear, false);
            Ui.Box(card, 0, 0, w, 2.5f, Ui.Gold);
            Ui.Frame(card, 0, 0, w, h, 1.2f, Ui.A(Ui.GoldDim, 0.78f));
            Ui.Frame(card, 4, 4, w - 8, h - 8, 1f, Ui.A(Ui.Bone, 0.04f));
            return card;
        }
    }

    /// <summary>
    /// The pause menu (JS openPause): Resume, Retry and Quit to map, with the game's settings under them. It is the same menu in
    /// any fight; the fight is stopped (the stage world's pause) while it is open.
    /// </summary>
    public sealed class PauseOverlay : FlowPanel
    {
        public PauseOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Pause") { }

        protected override string DefaultFocus { get { return "resume"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back || cmd == MenuCommand.Pause) { C.Resume(); return true; }
            return base.Handle(cmd);
        }

        protected override void Build()
        {
            var s = C.Save;
            var stage = GameRules.Stage(C.StageId);
            string name = stage == null ? "" : (stage.Kind == "boss" || stage.Kind == "subboss") && !string.IsNullOrEmpty(stage.Foe) ? stage.Foe : stage.Name;
            const float x = 460, y = 40, w = 680, h = 772;
            var card = OverlayKit.Card(Root, x, y, w, h, Fresh);
            Ui.Label(card, "PAUSED · NORMAL", UiFont.Cond, 15, Ui.Gold, 48, 34, 560, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, name.ToUpperInvariant(), UiFont.TitleHeavy, 44, Ui.Bone, 48, 62, 600, 60, TextAnchor.MiddleLeft, 1f);

            float by = 148;
            UiParts.Plate(card, Nav, "resume", 48, by, 584, 62, "Resume", true, () => C.Resume());
            UiParts.Plate(card, Nav, "retry", 48, by + 76, 584, 62, "Retry", false, () => C.Retry());
            // the JS pause menu has a Controls button between Restart and Quit
            UiParts.Plate(card, Nav, "controls", 48, by + 152, 584, 62, "Controls", false, () => flow.OpenControls());
            UiParts.Plate(card, Nav, "quit", 48, by + 228, 584, 62, "Quit to map", false, () => C.QuitToMap());

            float ry = by + 228 + 62 + 30;
            Ui.Box(card, 48, ry - 8, 584, 1, Ui.Hair);
            ry += 4;
            SettingRow(card, ref ry, "Screen Shake", "Camera shake on heavy impacts", "shake", new[] { "Full", "Half", "Off" }, Idx(new[] { "full", "half", "off" }, s.settings.shake),
                i => C.UpdateSettings(v => v.shake = new[] { "full", "half", "off" }[i]));
            SettingRow(card, ref ry, "Reduced Flashing", "Soft dim instead of inverted frames", "flash", new[] { "Off", "On" }, s.settings.reduceFlash ? 1 : 0, i => C.UpdateSettings(v => v.reduceFlash = i == 1));
            SettingRow(card, ref ry, "Sound Effects", "", "sound", new[] { "On", "Off" }, s.settings.sound ? 0 : 1, i => C.UpdateSettings(v => v.sound = i == 0));
            SettingRow(card, ref ry, "Music", "", "music", new[] { "On", "Off" }, s.settings.music ? 0 : 1, i => C.UpdateSettings(v => v.music = i == 0));
        }

        static int Idx(string[] values, string v)
        {
            int i = System.Array.IndexOf(values, v);
            return i < 0 ? 0 : i;
        }

        void SettingRow(Transform card, ref float y, string label, string hint, string key, string[] opts, int active, System.Action<int> pick)
        {
            Ui.Label(card, label.ToUpperInvariant(), UiFont.Cond, 16, Ui.Bone, 48, y, 300, 22, TextAnchor.MiddleLeft, 2.4f);
            if (!string.IsNullOrEmpty(hint)) Ui.Label(card, hint, UiFont.Body, 12, Ui.Muted, 48, y + 22, 300, 18, TextAnchor.UpperLeft);
            UiParts.Seg(card, Nav, key, 632, y - 6, opts, active, pick);
            y += 56;
        }
    }

    /// <summary>The question before dev mode is turned off (ADR-0009): it replaces the save with a fresh one.</summary>
    public sealed class ConfirmOverlay : FlowPanel
    {
        public ConfirmOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Confirm") { }

        protected override string DefaultFocus { get { return "cancel"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { C.CancelDevOff(); return true; }
            return base.Handle(cmd);
        }

        protected override void Build()
        {
            const float x = 480, y = 286, w = 640, h = 328;
            var card = OverlayKit.Card(Root, x, y, w, h, Fresh);
            Ui.Label(card, "CONFIRM", UiFont.Cond, 15, Ui.Gold, 40, 28, 400, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, "Turn dev mode off?", UiFont.TitleHeavy, 36, Ui.Bone, 40, 54, 560, 48, TextAnchor.MiddleLeft);
            Ui.Box(card, 40, 118, 3, 66, Ui.Acc);
            Ui.Label(card, "The real economy starts from a fresh save: the dev save's progress, coins and unlocked items are lost.", UiFont.Body, 17, Ui.Muted, 56, 118, 544, 72, TextAnchor.UpperLeft, 0, true, 1.35f);
            UiParts.Plate(card, Nav, "yes", 40, 226, 270, 58, "Yes, start fresh", true, () => C.ConfirmDevOff());
            UiParts.Plate(card, Nav, "cancel", 330, 226, 270, 58, "Cancel", false, () => C.CancelDevOff());
        }
    }

    /// <summary>The controls guide (JS openHelp): what each key and gamepad button does in a fight and in the menus.</summary>
    public sealed class ControlsOverlay : FlowPanel
    {
        public ControlsOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Controls") { }

        protected override string DefaultFocus { get { return "close"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { flow.CloseControls(); return true; }
            return base.Handle(cmd);
        }

        protected override void Build()
        {
            const float x = 320, y = 44, w = 960, h = 812;
            var card = OverlayKit.Card(Root, x, y, w, h, Fresh);
            Ui.Label(card, "CONTROLS GUIDE", UiFont.Cond, 15, Ui.Gold, 48, 28, 400, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, "Every move in one place", UiFont.TitleHeavy, 34, Ui.Bone, 48, 52, 700, 46, TextAnchor.MiddleLeft);
            Ui.Box(card, 48, 108, w - 96, 1, Ui.Hair);

            // Top row: Keyboard fight (9 rows: y 124..437) and Gamepad fight (7 rows: y 124..375) side by side
            Column(card, 48, 124, 412, 136, "Keyboard · fight", new[]
            {
                new[] { "Move", "A  D  ·  ←  →" }, new[] { "Jump", "W  ·  Space  ·  ↑" }, new[] { "Crouch", "S  ·  ↓" },
                new[] { "Punch", "J" }, new[] { "Kick", "K" }, new[] { "Katana", "L" }, new[] { "Shadow art", "I   (full meter: ↓ + I)" },
                new[] { "Dash", "Shift" }, new[] { "Pause", "Esc  ·  P" },
            });
            Column(card, 500, 124, 412, 136, "Gamepad · fight", new[]
            {
                new[] { "Move", "Stick  ·  D-pad" }, new[] { "Punch", "West (X)" }, new[] { "Kick", "South (A)" }, new[] { "Katana", "North (Y)" },
                new[] { "Shadow art", "East (B)  ·  RB" }, new[] { "Dash", "LB  ·  LT  ·  RT" }, new[] { "Pause", "Start" },
            });
            // Bottom section cleanly separated below both fight columns (y 462..656, zero overlap with Keyboard Pause row)
            Column(card, 48, 462, w - 96, 180, "Menus", new[]
            {
                new[] { "Move", "Arrows  ·  W A S D  ·  D-pad  ·  Stick" }, new[] { "Choose", "Enter  ·  Space  ·  J  ·  A" },
                new[] { "Back", "Esc  ·  K  ·  B" }, new[] { "Chapter / Tab", "Q / E  ·  LB / RB" }, new[] { "Mouse", "Hover to focus, click to choose" },
            });
            UiParts.Plate(card, Nav, "close", 48, 722, w - 96, 58, "Got it", true, () => flow.CloseControls());
        }

        static void Column(Transform card, float x, float y, float width, float labelW, string head, string[][] rows)
        {
            Ui.Diamond(card, x + 4.5f, y + 11, 6.4f, Ui.Acc, Ui.Acc, 0);
            Ui.Label(card, head.ToUpperInvariant(), UiFont.Cond, 15, Ui.Gold, x + 20, y, width - 20, 22, TextAnchor.MiddleLeft, 3f);
            float ry = y + 34;
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                Ui.Label(card, r[0], UiFont.Body, 16, Ui.Muted, x, ry, labelW, 25, TextAnchor.MiddleLeft);
                Ui.Label(card, r[1], UiFont.Cond, 17, Ui.Bone, x + labelW, ry, width - labelW, 25, TextAnchor.MiddleLeft, 1f);
                if (i + 1 < rows.Length) Ui.Box(card, x, ry + 28, width, 1, Ui.Hair);
                ry += 31;
            }
        }
    }
}
