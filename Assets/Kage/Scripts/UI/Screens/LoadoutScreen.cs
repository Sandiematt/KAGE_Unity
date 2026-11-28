using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The pre-fight screen (JS "Pre Fight Screen", wonder.css .pf-*): Kage's loadout on the left, the foe on the right, the weapon
    /// row and the Fight button in the dock. The two fighters themselves are the live stage world behind this screen
    /// (<see cref="StageWorld"/> in its tableau phase). The hairstyle and armour are the equipped ones; Change gear opens the
    /// Armory (SAN-90).
    /// </summary>
    public sealed class LoadoutScreen : FlowPanel
    {
        const float RowX = 98f, RowW = 980f, CardW = 136f, CardGap = 10f;

        float scroll;
        RectTransform content;
        int cellIndex;

        static readonly Dictionary<string, string> WeaponKanji = new Dictionary<string, string>
        {
            { "mumei", "無銘" }, { "kagetsu", "影月" }, { "tsubame", "燕" }, { "shizuka", "静" }, { "onikiri", "鬼斬" }, { "mizuchi", "蛟" },
            { "yamigama", "闇鎌" }, { "iwakudaki", "岩砕き" }, { "shinsen", "神閃" }, { "raiko_no_tachi", "雷" },
        };

        public LoadoutScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Loadout") { }

        protected override string DefaultFocus { get { return "fight"; } }

        protected override void Build()
        {
            var save = C.Save;
            var stage = GameRules.Stage(C.StageId);
            if (stage == null) return;
            var ch = GameRules.Chapter(stage.Chapter);
            var foe = JsTables.Enemy(stage.Enemy);
            bool stretch = stage.Kind == "stretch";

            // atmosphere over the live tableau
            Ui.Grad(Root, 0, 0, 1600, 320, Ui.A(Ui.Sky3, 0.86f), Ui.A(Ui.Sky3, 0f));
            // Subtle top-right scrim behind the foe title block so text stays crisp even over the bright moon
            Ui.Grad(Root, 920, 88, 680, 212, Color.clear, Ui.PanelA(0.68f), true);
            Ui.Grad(Root, 799, 120, 1.5f, 640, Color.clear, Color.clear, false, Ui.A(Ui.Bone, 0.30f));
            // the versus mark (grounded 3D contact shadows already anchor both fighters in StageWorld; no wireframe ripple crossing legs)
            var vs = Ui.Node(Root, "Vs", 720, 400, 160, 160);
            Enter(vs, UiAnims.VsPop(.7f, .15f, 2f, -12f));
            Ui.Glow(vs, 80, 80, 68, Ui.A(Ui.Acc, 0.22f), 1.6f);
            Ui.Diamond(vs, 80, 80, 92, Ui.PanelA(0.90f), Ui.Gold, 2f);
            Ui.Diamond(vs, 80, 80, 80, Color.clear, Ui.A(Ui.Gold, 0.32f), 1f);
            Ui.Label(vs, "対", UiFont.Brush, 64, Ui.Acc, 0, 0, 160, 160, TextAnchor.MiddleCenter);

            // top bar
            UiParts.Back(Root, Nav, () => C.Back(), 40, 15);
            Ui.Label(Root, "PREPARE FOR BATTLE", UiFont.Title, 16, Ui.Gold, 118, 24, 500, 40, TextAnchor.MiddleLeft, 4.8f);
            UiParts.Pills(Root, save, 1560f);

            // Kage
            Enter(Ui.Label(Root, "KAGE", UiFont.Title, 60, Ui.Bone, 64, 104, 320, 70, TextAnchor.MiddleLeft, 2.4f).transform, UiAnims.WRise(.6f));
            var stats = Ui.Node(Root, "Stats", 64 + 210, 112, 220, 52);
            Enter(stats, UiAnims.WRise(.6f));
            Ui.Box(stats, 0, 0, 220, 52, Ui.PanelA(0.82f));
            Ui.Frame(stats, 0, 0, 220, 52, 1, Ui.GoldDim);
            Ui.Label(stats, "LV", UiFont.Title, 14, Ui.Gold, 18, 0, 40, 52, TextAnchor.MiddleLeft, 2f);
            Ui.Label(stats, save.level.ToString(), UiFont.Title, 26, Ui.Bone, 48, 0, 50, 52, TextAnchor.MiddleLeft);
            Ui.Box(stats, 92, 12, 1, 28, Ui.GoldHair);
            Ui.Label(stats, "HP", UiFont.Title, 14, Ui.Gold, 110, 0, 40, 52, TextAnchor.MiddleLeft, 2f);
            Ui.Label(stats, GameRules.PlayerMaxHp(save).ToString(), UiFont.Title, 26, Ui.Mix(Ui.Hot, Ui.Bone, 0.7f), 140, 0, 80, 52, TextAnchor.MiddleLeft);

            string hair = Hairstyles.Name(save.equipped.hairstyle);
            var armor = JsTables.Armor(save.equipped.armor);
            float cx = 64;
            cellIndex = 0;
            cx += Cell(cx, 182, "Hairstyle", hair) + 10;
            cx += Cell(cx, 182, "Armor", armor != null ? armor.Name : "-") + 10;
            cx += Cell(cx, 182, "Mode", "Normal") + 10;
            ChangeGear(cx, 182);

            // the foe
            int dl = (int)Mathf.Clamp(ch.Diff + (foe != null && foe.Type == "boss" ? 1 : 0), 0, 4);
            int lit = Mathf.Clamp(dl, 1, 3);
            string[] labels = { "Calm", "Tense", "Fierce", "Brutal", "Mythic" };
            string kind = stretch ? "Stretch" : stage.Kind == "boss" ? "Boss" : "Sub-boss";
            string eyeText = (ch.Name + " · " + kind + " · " + labels[dl]).ToUpperInvariant();
            var eye = Ui.Label(Root, eyeText, UiFont.Title, 15, Ui.Hot, 956, 108, 580, 24, TextAnchor.MiddleRight, 3.6f);
            Soften(eye);
            Enter(eye.transform, UiAnims.WRise(.6f, .08f));
            var pipsHolder = Ui.Node(Root, "PipsRow", 0, 0, 1600, 900);
            UiParts.Pips(pipsHolder, 1536 - UiParts.Width(eye, 3.6f) - 14 - 76, 118, lit);
            Enter(pipsHolder, UiAnims.WRise(.6f, .08f));
            string title = stretch ? stage.Name : (stage.Foe ?? (foe != null ? foe.Name : stage.Name));
            string sub = stretch ? ch.Place : (stage.FoeTitle ?? (foe != null ? foe.Title : ""));
            var titleLabel = Ui.Label(Root, title, UiFont.Mincho, 50, Ui.Bone, 856, 136, 680, 66, TextAnchor.MiddleRight);
            Soften(titleLabel);
            Enter(titleLabel.transform, UiAnims.WRise(.6f, .08f));
            var subLabel = Ui.Label(Root, sub, UiFont.Title, 16, Ui.Bone, 956, 206, 580, 24, TextAnchor.MiddleRight, 1.28f);
            Soften(subLabel);
            Enter(subLabel.transform, UiAnims.WRise(.6f, .08f));
            // chips, right aligned
            var reward = GameRules.StageRewardPreview(save, stage.Id);
            string kindChip = stretch ? (stage.Stretch.Encounters.Length + " encounter" + (stage.Stretch.Encounters.Length == 1 ? "" : "s")) : kind;
            var chipRow = Ui.Node(Root, "Chips", 956, 244, 580, 38);
            Enter(chipRow, UiAnims.WRise(.6f, .08f));
            string coinChip = "+" + reward.Coins;
            float w2 = UiParts.ChipWidth(null, coinChip, true);
            float w1 = UiParts.ChipWidth(null, kindChip, true);
            UiParts.Chip(chipRow, 580 - w2, 0, null, coinChip, true, true);
            UiParts.Chip(chipRow, 580 - w2 - 10 - w1, 0, null, kindChip, false, true);

            Dock(save, stage);
        }

        // JS .pf-gear: a gold-edged button that opens the Armory (a Change gear tile after the Mode cell)
        void ChangeGear(float x, float y)
        {
            var label = Ui.Label(Root, "CHANGE GEAR", UiFont.Title, 14, Ui.Gold, 0, 0, 300, 58, TextAnchor.MiddleLeft, 1.68f);
            float w = 18f + 20f + 10f + UiParts.Width(label, 1.68f) + 1.68f + 18f;
            var rt = Ui.Node(Root, "ChangeGear", x, y, w, 58);
            Enter(rt, UiAnims.WRise(.6f));
            Ui.Box(rt, 0, 0, w, 58, Ui.PanelA(0.88f)).transform.SetAsFirstSibling();
            var hl = Ui.Box(rt, 0, 0, w, 58, Color.clear, "Light");
            Ui.Frame(rt, 0, 0, w, 58, 1.2f, Ui.Gold);
            var ring = Ui.Frame2(rt, w, 58);
            // a small breastplate for the icon
            Ui.Shape(rt, 18, 19, 20, 20, Ui.Poly(20, 20, 0, 0, 30, 14, 50, 0, 70, 14, 100, 0, 100, 60, 70, 100, 30, 100, 0, 60), Ui.Gold, null, null, false, "Icon");
            label.transform.SetParent(rt, false);
            label.rectTransform.anchoredPosition = new Vector2(18 + 20 + 10, 0);
            UiParts.Make(rt, Nav, "gear", () => C.OpenArmory(), f =>
            {
                hl.color = f ? Ui.A(Ui.Gold, 0.14f) : Color.clear;
                ring.color = f ? Ui.Gold : Color.clear;
            });
        }

        // Clean single tight drop shadow over the dark scrim (eliminates muddy double-shadow blur)
        static void Soften(Text t)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(0f, -2f);
        }

        float Cell(float x, float y, string k, string v)
        {
            var val = Ui.Label(Root, v, UiFont.Title, 17, Ui.Bone, 0, 0, 400, 26, TextAnchor.MiddleLeft);
            float w = Mathf.Max(128f, UiParts.Width(val) + 16 + 16 + 3);
            var rt = Ui.Node(Root, "Cell_" + k, x, y + 0, w, 58);
            Enter(rt, UiAnims.WRise(.6f));
            cellIndex++;
            Ui.Box(rt, 0, 0, w, 58, Ui.PanelA(0.82f)).transform.SetAsFirstSibling();
            Ui.Frame(rt, 0, 0, w, 58, 1, Ui.Hair);
            Ui.Box(rt, 0, 0, 3, 58, Ui.Acc);
            Ui.Label(rt, k.ToUpperInvariant(), UiFont.Title, 12, Ui.Muted, 19, 8, 200, 18, TextAnchor.MiddleLeft, 2.4f);
            val.transform.SetParent(rt, false);
            val.rectTransform.anchoredPosition = new Vector2(19, -28);
            val.rectTransform.sizeDelta = new Vector2(w - 24, 26);
            return w;
        }

        // ---------------- dock ----------------

        void Dock(SaveData save, JsStage stage)
        {
            Ui.Grad(Root, 0, 780, 1600, 120, Ui.A(Ui.Sky3, 0f), Ui.A(Ui.Sky3, 0.88f), false);
            Ui.Box(Root, 0, 836, 1600, 64, Ui.A(Ui.Sky3, 0.5f));

            var owned = new List<JsWeapon>();
            foreach (var w in JsTables.AllWeapons)
                if (!w.Enemy && !GameRules.IsWithheld(GearKind.Weapons, w.Id) && save.owned.weapons.ContainsKey(w.Id)) owned.Add(w);
            // the starter weapon leads, as it does on the JS screen
            int starter = owned.FindIndex(w => w.Id == GameRules.StarterWeaponId);
            if (starter > 0) { var k = owned[starter]; owned.RemoveAt(starter); owned.Insert(0, k); }

            var head = Ui.Label(Root, "CHOOSE WEAPON", UiFont.Title, 13, Ui.Muted, 56, 788, 300, 18, TextAnchor.MiddleLeft, 4.42f);
            float hw = UiParts.Width(head, 4.42f);
            var badge = Ui.Node(Root, "Count", 56 + hw + 8, 788, 40, 18);
            Ui.Box(badge, 0, 0, 40, 18, Ui.A(Ui.Acc, 0.3f));
            Ui.Label(badge, owned.Count.ToString(), UiFont.Title, 13, Ui.Bone, 0, 0, 40, 18, TextAnchor.MiddleCenter, 1f);

            float rowY = 812f;
            float contentW = owned.Count * (CardW + CardGap) - CardGap + 4;
            float maxScroll = Mathf.Max(0f, contentW - RowW);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);
            var left = Arrow(56, rowY, false, scroll > 2f);
            var viewport = Ui.Node(Root, "Weapons", RowX, rowY - 6, RowW, 82);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Ui.Node(viewport, "Content", 0, 0, owned.Count * (CardW + CardGap), 82);
            var right = Arrow(RowX + RowW + 8, rowY, true, scroll < maxScroll - 2f);
            content.anchoredPosition = new Vector2(-scroll, 0);
            for (int i = 0; i < owned.Count; i++) Card(content, owned[i], 2 + i * (CardW + CardGap), 6, save.equipped.weapon == owned[i].Id, maxScroll);

            left.onSubmit = () => ScrollBy(-RowW * 0.7f, maxScroll);
            right.onSubmit = () => ScrollBy(RowW * 0.7f, maxScroll);

            UiParts.Brush(Root, Nav, "fight", 1204, 801, 340, 78, "戦", "FIGHT", 30, 34, 10.2f, false, () => C.StartStage());
        }

        void ScrollBy(float dx, float maxScroll)
        {
            scroll = Mathf.Clamp(scroll + dx, 0f, maxScroll);
            Rebuild();
        }

        NavItem Arrow(float x, float y, bool rightSide, bool enabled)
        {
            var rt = Ui.Node(Root, rightSide ? "ArrowR" : "ArrowL", x, y, 34, 70);
            Ui.Box(rt, 0, 0, 34, 70, Ui.PanelA(enabled ? 0.90f : 0.55f));
            var hl = Ui.Box(rt, 0, 0, 34, 70, Color.clear, "Focus");
            Ui.Frame(rt, 0, 0, 34, 70, 1, enabled ? Ui.GoldDim : Ui.Hair);
            float s = rightSide ? -1f : 1f;
            var chev = Ui.Line(rt, new[] { new Vector2(17 + 4 * s, 28), new Vector2(17 - 4 * s, 35), new Vector2(17 + 4 * s, 42) }, 2f, enabled ? Ui.Gold : Ui.A(Ui.Muted, 0.35f));
            chev.rectTransform.sizeDelta = new Vector2(34, 70);
            return UiParts.Make(rt, Nav, rightSide ? "arrow-r" : "arrow-l", null, f => hl.color = f && enabled ? Ui.A(Ui.Gold, 0.14f) : Color.clear, enabled);
        }

        void Card(Transform parent, JsWeapon w, float x, float y, bool selected, float maxScroll)
        {
            var rt = Ui.Node(parent, "Weapon_" + w.Id, x, y, CardW, 70);
            rt.gameObject.AddComponent<RectMask2D>();
            Ui.Box(rt, 0, 0, CardW, 70, Ui.PanelA(0.92f));
            if (selected)
            {
                Ui.Grad(rt, 0, 0, CardW, 70, Ui.A(Ui.Acc, 0.26f), Ui.PanelA(0.94f), false);
                Ui.Box(rt, 0, 0, CardW, 2.5f, Ui.Gold);
                Ui.Frame(rt, 0, 0, CardW, 70, 1.5f, Ui.Gold);
            }
            else Ui.Frame(rt, 0, 0, CardW, 70, 1, Ui.A(Ui.GoldDim, 0.8f));
            var wash = Ui.Box(rt, 0, 0, CardW, 70, Color.clear, "Focus");
            // the Armory's own picture of the weapon: its render, or the JS drawing for the weapons without one (Shinsen, Raikō no Tachi, ...)
            string tex = GearArt.For(GearKind.Weapons, w.Id).Key;
            if (Ui.Tex(tex) != null)
            {
                var pic = Ui.Pic(rt, tex, 30, 4, 76, 40);
                Fit(pic, 76, 40);
            }
            else
            {
                string k;
                if (!WeaponKanji.TryGetValue(w.Id, out k)) k = w.Name.Length > 0 ? w.Name.Substring(0, 1) : "?";
                Ui.Label(rt, k, k.Length > 0 && k[0] > 0x2E80 ? UiFont.Mincho : UiFont.Title, 26, Ui.Gold, 0, 2, CardW, 44, TextAnchor.MiddleCenter);
            }
            Ui.Label(rt, Clean(w.Name).ToUpperInvariant(), UiFont.Title, 13, selected ? Ui.Gold : Ui.Bone, 4, 48, CardW - 8, 18, TextAnchor.MiddleCenter, 0.78f);
            string id = w.Id;
            float cardX = x;
            var lift = UiFocusTween.On(rt, .16f, v =>
            {
                UiOffsets.Of(rt).SetFocus(new Vector2(0f, 3f * v));
                wash.color = Ui.A(Ui.Gold, 0.10f * v);
            });
            UiParts.Make(rt, Nav, "weapon:" + id, () => C.EquipWeapon(id), f =>
            {
                lift.Set(f);
                if (f) KeepVisible(cardX, maxScroll);
            });
        }

        // the row scrolls so the focused card is always inside the window
        void KeepVisible(float cardX, float maxScroll)
        {
            if (content == null) return;
            float lo = cardX - 24f, hi = cardX + CardW + 24f;
            if (lo < scroll) scroll = Mathf.Clamp(lo, 0f, maxScroll);
            else if (hi > scroll + RowW) scroll = Mathf.Clamp(hi - RowW, 0f, maxScroll);
            content.anchoredPosition = new Vector2(-scroll, 0);
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

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (c >= 32) sb.Append(c);
            return sb.ToString();
        }
    }
}
