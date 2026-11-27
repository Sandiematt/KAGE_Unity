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
            float gy = 144;
            for (int i = 0; i < cells.Length; i++)
            {
                float cx = 44 + (i % 2) * 304f, cy = gy + (i / 2) * 96f;
                var tile = Ui.Node(card, "Stat_" + i, cx, cy, 288, 82);
                Ui.Box(tile, 0, 0, 288, 82, Ui.PanelA(0.75f));
                Ui.Box(tile, 0, 0, 2.5f, 82, Ui.A(Ui.GoldDim, 0.7f));
                Ui.Frame(tile, 0, 0, 288, 82, 1, Ui.Hair);
                Ui.Label(tile, cells[i][0], UiFont.Cond, 30, Ui.Bone, 18, 10, 256, 36, TextAnchor.MiddleLeft);
                Ui.Label(tile, cells[i][1].ToUpperInvariant(), UiFont.Cond, 13, Ui.Muted, 18, 48, 256, 20, TextAnchor.MiddleLeft, 2.6f);
            }
            XpBar(card, 44, gy + 3 * 96f + 16f, 592f, s);
            UiParts.Plate(card, Nav, "close", 44, h - 40 - 62, 592, 62, "Close", true, () => flow.CloseInfo());
        }

        public static void XpBar(Transform card, float x, float y, float w, SaveData s)
        {
            int need = GameRules.XpForLevel(s.level);
            Ui.Label(card, "EXPERIENCE", UiFont.Cond, 16, Ui.Bone, x, y, 300, 24, TextAnchor.MiddleLeft, 3.2f);
            Ui.Label(card, UiParts.Num(s.xp) + " / " + UiParts.Num(need), UiFont.Body, 16, Ui.Muted, x, y, w, 24, TextAnchor.MiddleRight);
            Ui.Box(card, x, y + 34, w, 7f, Ui.Hex("#2e2922"));
            float f = Mathf.Clamp01(s.xp / (float)need);
            if (f > 0f) Ui.Grad(card, x, y + 34, w * f, 7f, Ui.GoldDim, Ui.Gold, true);
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
                var alpha = got ? 1f : 0.52f;
                Ui.Box(rt, 0, 0, colW, 122, Ui.PanelA(0.78f));
                Ui.Box(rt, 0, 0, 74, 122, got ? Ui.A(Ui.Acc, 0.14f) : Ui.PanelA(0.55f));
                Ui.Box(rt, 74, 0, 1, 122, Ui.Hair);
                Ui.Frame(rt, 0, 0, colW, 122, 1, got ? Ui.GoldDim : Ui.Hair);
                if (got) Ui.Box(rt, 0, 0, colW, 2, Ui.Gold);
                string foe = st.Foe ?? st.Name;
                string glyph;
                if (!Glyph.TryGetValue(foe, out glyph)) glyph = "影";
                Ui.Label(rt, glyph, UiFont.Kanji, 42, Ui.A(got ? Ui.Hot : Ui.Muted, alpha), 0, 0, 74, 122, TextAnchor.MiddleCenter);
                Ui.Label(rt, foe, UiFont.Mincho, 23, Ui.A(Ui.Bone, alpha), 90, 20, colW - 102, 30, TextAnchor.MiddleLeft);
                string place = GameRules.ChapterOf(st.Id).Name + " · " + (st.FoeTitle ?? "");
                Ui.Label(rt, place, UiFont.Body, 16, Ui.A(Ui.Muted, alpha), 90, 52, colW - 102, 24, TextAnchor.MiddleLeft);
                Ui.Label(rt, got ? "◆ DEFEATED" : "NOT YET DEFEATED", UiFont.Cond, 13, got ? Ui.Gold : Ui.A(Ui.Muted, 0.72f), 90, 82, colW - 102, 20, TextAnchor.MiddleLeft, 2.2f);
            }
            UiParts.Plate(card, Nav, "close", 44, h - 40 - 62, w - 88, 62, "Close", true, () => flow.CloseInfo());
        }
    }

    /// <summary>
    /// The title's Profile diamond (JS openLoadout): Kage himself, live, in what he wears (<see cref="ProfileKage"/>), his weapon,
    /// helmet and armour beside him (each with its Armory tile art), his health and defence, his level and experience, and a way
    /// into the Armory.
    /// </summary>
    public sealed class ProfileOverlay : FlowPanel
    {
        public ProfileOverlay(GameFlow flow, RectTransform stage) : base(flow, stage, "Profile") { }

        protected override string DefaultFocus { get { return "armory"; } }

        ProfileKage kage;

        /// <summary>The live Kage on show, for tests and screenshots.</summary>
        public ProfileKage Kage { get { return kage; } }

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

        static readonly Color Line = Ui.Hex("#3a342b");

        // card 880 x 680: portrait left, gear rows right, stats and experience under the rows, the buttons along the bottom
        const float W = 880f, H = 680f, Pad = 44f, TopY = 140f, PortW = 300f, PortH = 400f, ColGap = 28f, RowH = 78f, RowGap = 12f;

        protected override void Build()
        {
            var s = C.Save;
            var card = OverlayKit.Card(Root, (1600 - W) / 2f, (900 - H) / 2f, W, H, Fresh);
            Ui.Label(card, "PROFILE", UiFont.Cond, 15, Ui.Gold, Pad, 40, 400, 22, TextAnchor.MiddleLeft, 3.9f);
            Ui.Label(card, "Kage · Level " + s.level, UiFont.TitleHeavy, 40, Ui.Bone, Pad, 66, 600, 56, TextAnchor.MiddleLeft);

            Portrait(card, s);

            float cx = Pad + PortW + ColGap, cw = W - Pad - cx;
            string[] ids = { s.equipped.weapon, s.equipped.helmet, s.equipped.armor };
            GearKind[] kinds = { GearKind.Weapons, GearKind.Helmets, GearKind.Armors };
            string[] kindNames = { "Weapon", "Helm", "Armor" };
            for (int i = 0; i < 3; i++)
            {
                var item = GameRules.Item(kinds[i], ids[i]);
                int lv = GearLevel(s, kinds[i], ids[i]);
                GearRow(card, cx, TopY + i * (RowH + RowGap), cw, kinds[i], ids[i], item != null ? Clean(item.Name) : "—", kindNames[i], lv);
            }

            // health and defence, then the experience bar, under the rows and level with the portrait's foot
            float sy = TopY + 3 * (RowH + RowGap) + 6f;
            var stats = GameRules.PlayerCombatStats(s);
            Stat(card, cx, sy, cw / 2f - 10f, GameRules.PlayerMaxHp(s).ToString(), "Health");
            Stat(card, cx + cw / 2f + 10f, sy, cw / 2f - 10f, Mathf.RoundToInt((float)stats.Defense * 100f) + "%", "Defense");
            RecordsOverlay.XpBar(card, cx, TopY + PortH - 44f, cw, s);

            float by = H - 40 - 62;
            UiParts.Plate(card, Nav, "armory", Pad, by, 520, 62, "Open Armory", true, () => { flow.CloseInfo(); C.OpenArmory(); });
            UiParts.Plate(card, Nav, "close", Pad + 520 + 16, by, W - Pad * 2 - 536, 62, "Close", false, () => flow.CloseInfo());
        }

        static int GearLevel(SaveData s, GearKind kind, string id)
        {
            var owned = kind == GearKind.Weapons ? s.owned.weapons : kind == GearKind.Helmets ? s.owned.helmets : s.owned.armors;
            int lv;
            return id != null && owned != null && owned.TryGetValue(id, out lv) && lv > 0 ? lv : 1;
        }

        /// <summary>Kage, live, in front of the red sun: the glow is clipped to the frame so it never spills over the card.</summary>
        void Portrait(RectTransform card, SaveData s)
        {
            var port = Ui.Node(card, "Portrait", Pad, TopY, PortW, PortH);
            port.gameObject.AddComponent<RectMask2D>();
            Ui.Box(port, 0, 0, PortW, PortH, Ui.Sky3);
            // the sun, the ground and Kage are one picture, drawn like the pre-fight screen's Kage (ProfileKage)
            var k = ProfileKage.Ensure(ref kage, flow.transform, Root, s, PortW, PortH);
            var pic = Ui.Node(port, "Kage", 0, 0, PortW, PortH).gameObject.AddComponent<RawImage>();
            pic.raycastTarget = false;
            pic.texture = k.Texture;

            Ui.Label(port, "影", UiFont.Kanji, 40, Ui.A(Ui.Bone, 0.16f), 14, 10, 60, 60, TextAnchor.UpperLeft);
            Ui.Frame(card, Pad, TopY, PortW, PortH, 1.5f, Ui.A(Ui.GoldDim, 0.85f));
            Ui.Frame(card, Pad + 4, TopY + 4, PortW - 8, PortH - 8, 1f, Ui.A(Ui.Gold, 0.18f));
        }

        static void GearRow(RectTransform card, float x, float y, float w, GearKind kind, string id, string name, string kindName, int lv)
        {
            var row = Ui.Node(card, "Gear_" + kindName, x, y, w, RowH);
            Ui.Box(row, 0, 0, w, RowH, Ui.PanelA(0.76f));
            Ui.Frame(row, 0, 0, w, RowH, 1, Ui.A(Ui.GoldDim, 0.55f));
            // the item's art in a square well on the left
            var well = Ui.Node(row, "Well", 0, 0, RowH, RowH);
            well.gameObject.AddComponent<RectMask2D>();
            Ui.Grad(well, 0, 0, RowH, RowH, Ui.A(Ui.Acc, 0.24f), Ui.PanelA(0.92f));
            var a = GearArt.For(kind, id);
            if (a.Texture != null)
            {
                float pad = 8f, inner = RowH - pad * 2f;
                var pic = Ui.Pic(well, a.Key, pad, pad, inner, inner);
                Fit(pic, inner, inner);
            }
            Ui.Box(row, RowH, 0, 1, RowH, Ui.A(Ui.GoldDim, 0.45f));
            float tx = RowH + 18f;
            Ui.Label(row, name, UiFont.Cond, 25, Ui.Bone, tx, 12, w - tx - 70, 32, TextAnchor.MiddleLeft);
            Ui.Label(row, kindName.ToUpperInvariant(), UiFont.Cond, 13, Ui.Muted, tx, 46, 200, 18, TextAnchor.MiddleLeft, 2.8f);
            Ui.Label(row, "LV " + lv, UiFont.Cond, 16, Ui.Gold, w - 76, 0, 60, RowH, TextAnchor.MiddleRight, 2f);
        }

        static void Stat(RectTransform card, float x, float y, float w, string value, string label)
        {
            var tile = Ui.Node(card, "PStat_" + label, x, y, w, 66);
            Ui.Box(tile, 0, 0, w, 66, Ui.PanelA(0.72f));
            Ui.Box(tile, 0, 0, 2.5f, 66, Ui.GoldDim);
            Ui.Frame(tile, 0, 0, w, 66, 1, Ui.Hair);
            Ui.Label(tile, value, UiFont.Cond, 28, Ui.Bone, 14, 6, w - 20, 34, TextAnchor.MiddleLeft);
            Ui.Label(tile, label.ToUpperInvariant(), UiFont.Cond, 12, Ui.Muted, 14, 40, w - 20, 18, TextAnchor.MiddleLeft, 2.6f);
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
