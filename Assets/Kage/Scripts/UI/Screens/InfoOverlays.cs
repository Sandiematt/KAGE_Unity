using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// What the title's Records diamond shows (JS openProfile): Kage's level, duels won and lost, bosses broken, best Echo Trials wave, flawless
    /// wins and the current New Game+ cycle, and the experience bar.
    /// </summary>
    public sealed class RecordsOverlay : FlowPanel
    {
        public RecordsOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Records") { }

        protected override string DefaultFocus { get { return "close"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { flow.CloseInfo(); return true; }
            return base.Handle(cmd);
        }

        /// <summary>The cells of the grid, label and value, in the JS order.</summary>
        public static string[][] Cells(SaveData s)
        {
            int total = 0, broken = 0;
            foreach (var st in GameRules.Stages)
                if (st.Kind == "boss" || st.Kind == "subboss" || st.Kind == "echo") { total++; if (GameRules.DefeatedCount(s, st.Id) > 0) broken++; }
            return new[]
            {
                new[] { s.stats.wins.ToString(), "Duels won" }, new[] { s.stats.losses.ToString(), "Defeats" },
                new[] { broken + " / " + total, "Bosses broken" }, new[] { s.trialsBest.ToString(), "Best Echo Trials wave" },
                new[] { s.stats.perfects.ToString(), "Flawless wins" }, new[] { s.ng > 0 ? "NG+" + s.ng : "—", "Current cycle" },
            };
        }

        protected override void Build()
        {
            var s = C.Save;
            const float w = 680f, h = 640f;
            var card = OverlayKit.Card(Root, 460, (900 - h) / 2f, w, h, Fresh);
            Ui.Label(card, "RECORDS", UiFont.Cond, 15, Ui.Gold, 44, 40, 400, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, "Kage · Level " + s.level, UiFont.TitleHeavy, 40, Ui.Bone, 44, 66, 600, 56, TextAnchor.MiddleLeft);
            var cells = Cells(s);
            float gy = 148;
            for (int i = 0; i < cells.Length; i++)
            {
                float cx = 44 + (i % 2) * 312f, cy = gy + (i / 2) * 100f;
                Ui.Label(card, cells[i][0], UiFont.Cond, 31, Ui.Bone, cx, cy + 12, 280, 38, TextAnchor.MiddleLeft);
                Ui.Label(card, cells[i][1].ToUpperInvariant(), UiFont.Cond, 14, Ui.Muted, cx, cy + 50, 290, 20, TextAnchor.MiddleLeft, 2.8f);
                Ui.Box(card, cx, cy + 80, 280, 1, Ui.A(Ui.Hex("#3a342b"), 1f));
            }
            XpBar(card, 44, gy + 3 * 100f + 6f, 592f, s);
            UiParts.Plate(card, Nav, "close", 44, h - 40 - 62, 592, 62, "Close", true, () => flow.CloseInfo());
        }

        public static void XpBar(Transform card, float x, float y, float w, SaveData s)
        {
            int need = GameRules.XpForLevel(s.level);
            Ui.Label(card, "EXPERIENCE", UiFont.Cond, 17, Ui.Bone, x, y, 300, 24, TextAnchor.MiddleLeft, 3.2f);
            Ui.Label(card, UiParts.Num(s.xp) + " / " + UiParts.Num(need), UiFont.Body, 17, Ui.Muted, x, y, w, 24, TextAnchor.MiddleRight);
            Ui.Box(card, x, y + 36, w, 7.5f, Ui.Hex("#3a342b"));
            float f = Mathf.Clamp01(s.xp / (float)need);
            if (f > 0f) Ui.Box(card, x, y + 36, w * f, 7.5f, Ui.GoldDim);
        }
    }

    /// <summary>The title's Trophies diamond (JS openTrophies): every boss stage with its kanji, whether it has fallen and where.</summary>
    public sealed class TrophiesOverlay : FlowPanel
    {
        static readonly System.Collections.Generic.Dictionary<string, string> Glyph = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Raikon", "雷" }, { "Akari", "霧" }, { "Takashi", "父" }, { "Rem", "弟" }, { "The Witness", "鏡" },
        };

        public TrophiesOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Trophies") { }

        protected override string DefaultFocus { get { return "close"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { flow.CloseInfo(); return true; }
            return base.Handle(cmd);
        }

        protected override void Build()
        {
            var s = C.Save;
            var rows = new System.Collections.Generic.List<JsStage>();
            foreach (var st in GameRules.Stages) if (st.Kind == "boss" || st.Kind == "subboss" || st.Kind == "echo") rows.Add(st);
            const float w = 1160f;
            int lines = Mathf.Max(1, (rows.Count + 2) / 3);
            float h = 40 + 22 + 6 + 52 + 24 + lines * 122 + (lines - 1) * 18 + 24 + 62 + 40;
            var card = OverlayKit.Card(Root, 220, (900 - h) / 2f, w, h, Fresh);
            Ui.Label(card, "TROPHIES", UiFont.Cond, 15, Ui.Gold, 44, 40, 400, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, "Fallen Foes", UiFont.TitleHeavy, 40, Ui.Bone, 44, 66, 600, 56, TextAnchor.MiddleLeft);
            float colW = (w - 88 - 36) / 3f;
            for (int i = 0; i < rows.Count; i++)
            {
                var st = rows[i];
                bool got = GameRules.DefeatedCount(s, st.Id) > 0;
                float x = 44 + (i % 3) * (colW + 18), y = 148 + (i / 3) * 140;
                var rt = Ui.Node(card, "Tro_" + st.Id, x, y, colW, 122);
                var alpha = got ? 1f : 0.55f;
                Ui.Box(rt, 0, 0, colW, 122, new Color(10f / 255f, 6f / 255f, 6f / 255f, 0.6f));
                Ui.Frame(rt, 0, 0, colW, 122, 1, got ? Ui.Hex("#8a6a35") : Ui.Hex("#3a2a22"));
                string foe = st.Foe ?? st.Name;
                string glyph;
                if (!Glyph.TryGetValue(foe, out glyph)) glyph = "影";
                Ui.Label(rt, glyph, UiFont.Kanji, 44, Ui.A(Ui.Acc, alpha), 16, 0, 52, 122, TextAnchor.MiddleCenter);
                Ui.Label(rt, foe, UiFont.Mincho, 23, Ui.A(Ui.Bone, alpha), 86, 22, colW - 96, 30, TextAnchor.MiddleLeft);
                string place = GameRules.ChapterOf(st.Id).Name + " · " + (st.FoeTitle ?? "");
                Ui.Label(rt, place, UiFont.Body, 17, Ui.A(Ui.Muted, alpha), 86, 52, colW - 96, 24, TextAnchor.MiddleLeft);
                Ui.Label(rt, got ? "Defeated" : "Not yet defeated", UiFont.Body, 17, Ui.A(Ui.Gold, alpha), 86, 78, colW - 96, 24, TextAnchor.MiddleLeft);
            }
            UiParts.Plate(card, Nav, "close", 44, h - 40 - 62, w - 88, 62, "Close", true, () => flow.CloseInfo());
        }
    }

    /// <summary>
    /// The title's Profile diamond (JS openLoadout): Kage's level and experience, his weapon, helmet and armour with their levels (each drawn,
    /// like the Armory's tiles), his health, and a way into the Armory. The JS draws Kage himself in a canvas here; this shows the gear instead.
    /// </summary>
    public sealed class ProfileOverlay : FlowPanel
    {
        public ProfileOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Profile") { }

        protected override string DefaultFocus { get { return "armory"; } }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Back) { flow.CloseInfo(); return true; }
            return base.Handle(cmd);
        }

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (c >= 32) sb.Append(c);
            return sb.ToString();
        }

        protected override void Build()
        {
            var s = C.Save;
            const float w = 680f, h = 780f;
            var card = OverlayKit.Card(Root, 460, (900 - h) / 2f, w, h, Fresh);
            Ui.Label(card, "PROFILE", UiFont.Cond, 15, Ui.Gold, 44, 40, 400, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, "Kage · Level " + s.level, UiFont.TitleHeavy, 40, Ui.Bone, 44, 66, 600, 56, TextAnchor.MiddleLeft);

            // the gear, drawn: weapon, helmet, armour
            float ay = 140;
            var art = Ui.Node(card, "Gear", 44, ay, 592, 220);
            Ui.Grad(art, 0, 0, 592, 220, Ui.A(Ui.Acc, 0.22f), Ui.PanelA(0.9f));
            Ui.Glow(art, 296, 110, 210, Ui.A(Ui.Acc, 0.55f), 1.3f);
            string[] ids = { s.equipped.weapon, s.equipped.helmet, s.equipped.armor };
            GearKind[] kinds = { GearKind.Weapons, GearKind.Helmets, GearKind.Armors };
            string[] names =
            {
                Clean(GameRules.Item(GearKind.Weapons, ids[0]).Name), Clean(GameRules.Item(GearKind.Helmets, ids[1]).Name), Clean(GameRules.Item(GearKind.Armors, ids[2]).Name),
            };
            for (int i = 0; i < 3; i++)
            {
                var a = GearArt.For(kinds[i], ids[i]);
                var tex = a.Texture;
                float cx = 20 + i * 188, cw = 168f;
                if (tex != null)
                {
                    var pic = Ui.Pic(art, a.Key, cx, 12, cw, i == 2 ? 148f : 110f);
                    Fit(pic, cw, i == 2 ? 148f : 110f);
                }
                Ui.Label(art, names[i].ToUpperInvariant(), UiFont.Title, 13, Ui.Bone, cx, 176, cw, 20, TextAnchor.MiddleCenter, 1.2f);
            }

            RecordsOverlay.XpBar(card, 44, ay + 220 + 24, 592, s);

            // the four records
            float gy = ay + 220 + 24 + 64;
            int lv = 1;
            s.owned.weapons.TryGetValue(s.equipped.weapon, out lv);
            string[][] cells =
            {
                new[] { names[0], "Weapon · Lv " + Mathf.Max(1, lv) }, new[] { names[1], "Helm" }, new[] { names[2], "Armor" },
                new[] { GameRules.PlayerMaxHp(s).ToString(), "Health" },
            };
            for (int i = 0; i < cells.Length; i++)
            {
                float cx = 44 + (i % 2) * 312f, cy = gy + (i / 2) * 84f;
                Ui.Label(card, cells[i][0], UiFont.Cond, 26, Ui.Bone, cx, cy + 6, 290, 34, TextAnchor.MiddleLeft);
                Ui.Label(card, cells[i][1].ToUpperInvariant(), UiFont.Cond, 14, Ui.Muted, cx, cy + 42, 290, 20, TextAnchor.MiddleLeft, 2.8f);
                Ui.Box(card, cx, cy + 68, 280, 1, Ui.Hex("#3a342b"));
            }
            float by = h - 40 - 62;
            UiParts.Plate(card, Nav, "armory", 44, by, 390, 62, "Open Armory", true, () => { flow.CloseInfo(); C.OpenArmory(); });
            UiParts.Plate(card, Nav, "close", 450, by, 186, 62, "Close", false, () => flow.CloseInfo());
        }

        static void Fit(RawImage pic, float w, float h)
        {
            var t = pic.texture;
            if (t == null) return;
            float k = Mathf.Min(w / t.width, h / t.height);
            float iw = t.width * k, ih = t.height * k;
            pic.rectTransform.anchoredPosition += new Vector2((w - iw) / 2f, -(h - ih) / 2f);
            pic.rectTransform.sizeDelta = new Vector2(iw, ih);
        }
    }
}
