using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The campaign map (JS "Campaign Screen", wonder.css .cp-*): the chapter rail, the open chapter's stages on the serpentine
    /// path with locked / open / cleared taken from the save, the stage card and, for the act boss, its art card. What it shows
    /// comes from <see cref="CampaignView"/>.
    /// </summary>
    public sealed class CampaignScreen : FlowPanel
    {
        // the JS node positions on the map (panel px), used in stage order
        static readonly Vector2[] Slots =
        {
            new Vector2(80, 250), new Vector2(200, 330), new Vector2(320, 250), new Vector2(440, 330), new Vector2(560, 250),
            new Vector2(440, 490), new Vector2(320, 570), new Vector2(200, 490), new Vector2(100, 600),
        };

        const float MapX = 410, MapY = 124;

        public CampaignScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Campaign") { }

        protected override string DefaultFocus { get { return "node:" + C.SelectedStage; } }

        string ChapterOfSelected()
        {
            var st = GameRules.Stage(C.SelectedStage);
            return st != null ? st.Chapter : GameRules.Chapters[0].Id;
        }

        public override bool Handle(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Prev || cmd == MenuCommand.Next)
            {
                var chapters = GameRules.Chapters;
                int i = System.Array.FindIndex(chapters, c => c.Id == ChapterOfSelected());
                int j = Mathf.Clamp(i + (cmd == MenuCommand.Next ? 1 : -1), 0, chapters.Length - 1);
                if (j != i) PickChapter(chapters[j].Id);
                return true;
            }
            return base.Handle(cmd);
        }

        void PickChapter(string id)
        {
            var v = CampaignView.Build(C.Save, id, null);
            if (v.Chapter.State == ChapterState.Locked) return;
            C.SelectStage(v.Card.StageId);
            Nav.SetFocus("chapter:" + id);
        }

        protected override void Build()
        {
            var save = C.Save;
            string chId = ChapterOfSelected();
            var view = CampaignView.Build(save, chId, C.SelectedStage);
            var ch = view.Chapter;

            Backdrop(Root);

            // ---- top bar ----
            UiParts.Back(Root, Nav, () => C.Back(), 40, 19);
            Ui.Label(Root, "CAMPAIGN", UiFont.Title, 14, Ui.Gold, 118, 18, 400, 20, TextAnchor.MiddleLeft, 4.76f);
            Ui.Label(Root, "The Last Betrayal", UiFont.Title, 34, Ui.Bone, 118, 38, 600, 42, TextAnchor.MiddleLeft);
            int cleared = 0;
            foreach (var c in view.Chapters) if (c.State == ChapterState.Cleared) cleared++;
            float pipsW = view.Chapters.Length * 14 + (view.Chapters.Length - 1) * 8 + 14 + 70;
            float px = 800 - pipsW / 2;
            for (int i = 0; i < view.Chapters.Length; i++)
            {
                var c = view.Chapters[i];
                float cx = px + 7 + i * 22;
                if (c.State == ChapterState.Cleared) Ui.Diamond(Root, cx, 48, 14, Ui.Acc, Ui.Acc, 0);
                else Ui.Diamond(Root, cx, 48, 14, Color.clear, c.Selected ? Ui.Gold : Ui.GoldDim, 1.5f);
            }
            Ui.Label(Root, cleared + " / " + view.Chapters.Length, UiFont.Title, 18, Ui.Bone, px + view.Chapters.Length * 22 + 14, 32, 100, 32, TextAnchor.MiddleLeft);
            UiParts.Pills(Root, save, 1560f);

            // ---- chapter rail ----
            for (int i = 0; i < view.Chapters.Length; i++) ChapterRow(view.Chapters[i], 40, 124 + i * 88);
            UiParts.Keys(Root, 40, 812, new[] { "ENTER", "Fight" }, new[] { "ESC", "Back" }, new[] { "Q / E", "Chapter" });

            // ---- the map ----
            MapPanel(view, save);

            // ---- the card column ----
            bool withBoss = view.Card.BossArt != null && Ui.Tex("reveal_" + view.Card.BossArt) != null;
            StageCardPanel(view, withBoss);
        }

        // ---------------- backdrop ----------------

        static void Backdrop(Transform p)
        {
            Ui.Grad(p, 0, 0, 1600, 900, Ui.Sky1, Ui.Sky3, false, Ui.Sky2);
            Ui.Glow(p, 1320, 180, 420, Ui.A(Ui.Acc, 0.35f), 1.2f);
            Ui.Disc(p, 1320, 180, 130, Ui.A(Ui.Deep, 0.55f));
            Ui.Ground2(p, new float[] { 0, 720, 140, 660, 280, 690, 420, 620, 560, 670, 720, 630, 880, 680, 1040, 620, 1200, 670, 1360, 630, 1600, 660 }, 900, Ui.Ground);
        }

        // ---------------- chapter rail ----------------

        void ChapterRow(ChapterView c, float x, float y)
        {
            var rt = Ui.Node(Root, "Chapter_" + c.Id, x, y, 340, 80);
            bool locked = c.State == ChapterState.Locked;
            Ui.Box(rt, 0, 0, 340, 80, Ui.PanelA(0.7f));
            if (c.Selected)
            {
                Ui.Grad(rt, 0, 0, 340, 80, Ui.A(Ui.Acc, 0.42f), Ui.PanelA(0.85f), true);
                Ui.Frame(rt, 0, 0, 340, 80, 1.5f, Ui.Gold);
            }
            else Ui.Frame(rt, 0, 0, 340, 80, 1, Ui.Hair);
            float a = locked ? 0.5f : 1f;
            Ui.Label(rt, c.Name.ToUpperInvariant(), UiFont.Title, 12, Ui.A(Ui.Gold, a), 20, 16, 240, 16, TextAnchor.MiddleLeft, 3.6f);
            Ui.Label(rt, c.Title, UiFont.Mincho, 20, Ui.A(Ui.Bone, a), 20, 36, 250, 30, TextAnchor.MiddleLeft);
            if (locked) LockIcon(rt, 316, 40, Ui.Muted);
            else if (c.State == ChapterState.Cleared) Ui.Label(rt, "CLEARED", UiFont.Title, 12, Ui.Hot, 200, 0, 120, 80, TextAnchor.MiddleRight, 2.4f);
            else Ui.Label(rt, c.Done + " / " + c.Total, UiFont.Title, 16, c.Selected ? Ui.Bone : Ui.Muted, 200, 0, 120, 80, TextAnchor.MiddleRight);
            var ring = Ui.Box(rt, 0, 0, 340, 80, Color.clear);
            string id = c.Id;
            UiParts.Make(rt, Nav, "chapter:" + id, () => PickChapter(id), f =>
            {
                rt.anchoredPosition = new Vector2(x + (f && !c.Selected ? 4f : 0f), -y);
                ring.color = f && !c.Selected ? Ui.A(Ui.Gold, 0.10f) : Color.clear;
            }, !locked);
        }

        public static void LockIcon(Transform p, float cx, float cy, Color c)
        {
            Ui.Box(p, cx - 6, cy - 1, 12, 9, c, "LockBody");
            var pts = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.PI * (i / 10f);
                pts.Add(new Vector2(cx - Mathf.Cos(a) * 3.8f, cy - 1 - Mathf.Sin(a) * 4.5f));
            }
            Ui.Line(p, pts.ToArray(), 1.8f, c);
        }

        // ---------------- map ----------------

        void MapPanel(CampaignView view, SaveData save)
        {
            var map = Ui.Node(Root, "Map", MapX, MapY, 680, 736);
            Ui.Grad(map, 0, 0, 680, 736, Ui.Mix(Ui.Acc, Ui.Sky1, 0.14f), Ui.Panel);
            Ui.Glow(map, 520, 150, 220, Ui.A(Ui.Hot, 0.35f), 1.2f);
            Ui.Disc(map, 520, 150, 64, Ui.A(Ui.Mix(Ui.Hot, Ui.Panel, 0.55f), 0.45f));
            // the village skyline: roofs with glowing windows
            var sky = new float[] { 0, 470, 40, 470, 40, 430, 20, 430, 60, 404, 100, 430, 80, 430, 80, 470, 140, 470, 140, 410, 120, 410, 170, 376, 220, 410, 200, 410, 200, 470,
                260, 470, 260, 440, 240, 440, 280, 414, 320, 440, 300, 440, 300, 470, 380, 470, 380, 390, 356, 390, 410, 350, 464, 390, 440, 390, 440, 470, 520, 470, 520, 432, 500, 432,
                540, 406, 580, 432, 560, 432, 560, 470, 680, 470 };
            var pts = new List<Vector2>();
            for (int i = 0; i < sky.Length; i += 2) pts.Add(new Vector2(sky[i], sky[i + 1]));
            pts.Add(new Vector2(680, 736)); pts.Add(new Vector2(0, 736));
            Ui.Shape(map, 0, 0, 680, 736, pts.ToArray(), Ui.Mix(Ui.Ground, Ui.Panel, 0.85f), Ui.Mix(Ui.Ground, Ui.Panel, 0.85f), null, false, "Skyline");
            foreach (var w in new[] { new Vector2(60, 452), new Vector2(170, 440), new Vector2(410, 420), new Vector2(540, 452) })
            {
                Ui.Disc(map, w.x, w.y, 14, Ui.A(Ui.Hot, 0.12f));
                Ui.Disc(map, w.x, w.y, 4, Ui.Hot);
            }
            Ui.Box(map, 0, 0, 680, 736, Ui.PanelA(0.35f));

            // title block
            Ui.Label(map, (view.Chapter.Name + " · " + view.Chapter.Place).ToUpperInvariant(), UiFont.Title, 14, Ui.Gold, 36, 30, 620, 20, TextAnchor.MiddleLeft, 4.2f);
            Ui.Label(map, view.Chapter.Title, UiFont.Mincho, 46, Ui.Bone, 36, 52, 620, 56, TextAnchor.MiddleLeft);
            float frac = view.Chapter.Total == 0 ? 0 : view.Chapter.Done / (float)view.Chapter.Total;
            Ui.Box(map, 36, 124, 420, 4, new Color(0.925f, 0.898f, 0.827f, 0.16f));
            if (frac > 0f) Ui.Grad(map, 36, 124, 420 * frac, 4, Ui.Hex("#e2b56a"), Ui.Gold, true);
            Ui.Label(map, view.Chapter.Done + " / " + view.Chapter.Total, UiFont.Title, 16, Ui.Bone, 470, 114, 80, 24, TextAnchor.MiddleLeft);
            Ui.Frame(map, 0, 0, 680, 736, 1.5f, Ui.GoldDim);

            // layout of the nodes
            int n = view.Stages.Length;
            var pos = new Vector2[n];
            for (int i = 0; i < n; i++)
                pos[i] = n == 9 ? Slots[i] : n == 1 ? new Vector2(340, 420) : Slots[Mathf.RoundToInt(i * 8f / (n - 1))];

            // the road between the nodes
            for (int i = 0; i + 1 < n; i++)
            {
                var a = pos[i]; var b = pos[i + 1];
                bool ok = view.Stages[i].State == StageState.Cleared && (view.Stages[i + 1].State == StageState.Cleared || view.Stages[i + 1].IsNext);
                var path = new List<Vector2> { a };
                if (Mathf.Approximately(a.x, 560) && b.y > 400) path.Add(new Vector2(560, 410));
                path.Add(b);
                var line = Ui.Line(map, path.ToArray(), ok ? 3f : 2f, ok ? Ui.Acc : Ui.A(Ui.Muted, 0.5f), ok ? 0f : 8f, 8f);
                line.rectTransform.sizeDelta = new Vector2(680, 736);
            }
            for (int i = 0; i < n; i++) Node(map, view.Stages[i], pos[i]);
        }

        void Node(Transform map, StageView st, Vector2 p)
        {
            bool big = st.Kind == "stretch";
            float box = st.IsNext ? 92f : st.Kind == "boss" ? 88f : st.Kind == "subboss" ? 56f : 68f;
            float ds = st.IsNext ? 56f : st.Kind == "boss" ? 56f : st.Kind == "subboss" ? 36f : 44f;
            bool locked = st.State == StageState.Locked, done = st.State == StageState.Cleared;
            var rt = Ui.Node(map, "Node_" + st.Id, p.x - 70, p.y - box / 2, 140, box + 60);
            Color fill = Ui.PanelA(0.9f), edge = Ui.Hair;
            float bw = 1.5f;
            if (done) { fill = Ui.Acc; edge = Ui.Hot; }
            if (st.Kind == "boss" && !locked) { fill = Ui.Acc; edge = Ui.Bone; bw = 2f; Ui.Glow(rt, 70, box / 2, 60, Ui.A(Ui.Acc, 0.5f), 1.6f); }
            if (st.IsNext) { fill = Ui.InkOnGold; edge = Ui.Gold; bw = 2f; Ui.Glow(rt, 70, box / 2, 56, Ui.A(Ui.Gold, 0.45f), 1.6f); }
            float cx = 70, cy = box / 2;
            if (st.IsNext) Ui.Diamond(rt, cx, cy, 56 * 1.35f, Color.clear, Ui.GoldHair, 1f);
            var dia = Ui.Diamond(rt, cx, cy, ds, fill, edge, bw);
            if (st.Selected && !st.IsNext) Ui.Diamond(rt, cx, cy, ds * 1.32f, Color.clear, Ui.Gold, 1.5f);

            // the glyph
            if (locked && !big && st.Kind != "boss") LockIcon(rt, cx, cy - 2, Ui.Muted);
            else if (st.Kind == "boss") Ui.Label(rt, "戦", UiFont.Mincho, 26, Ui.Bone, cx - 30, cy - 30, 60, 60, TextAnchor.MiddleCenter);
            else if (st.Kind == "subboss") Ui.Label(rt, "鬼", UiFont.Mincho, 20, locked ? Ui.Muted : (done ? Ui.Bone : Ui.Bone), cx - 30, cy - 30, 60, 60, TextAnchor.MiddleCenter);
            else
            {
                Color nc = st.IsNext ? Ui.Gold : done ? Ui.Bone : Ui.Muted;
                Ui.Label(rt, CampaignView.RomanOf(st.Number), UiFont.Title, st.IsNext ? 24 : 20, nc, cx - 30, cy - 30, 60, 60, TextAnchor.MiddleCenter);
            }

            // name and type under the diamond
            Color nameC = st.Selected || st.IsNext ? Ui.Gold : locked ? Ui.Muted : Ui.Bone;
            int nameSize = st.Kind == "boss" ? 16 : st.IsNext ? 15 : 14;
            var nm = Ui.Label(rt, st.Label, UiFont.Title, nameSize, nameC, 5, box + 6, 130, 36, TextAnchor.UpperCenter, 0, true, 0.9f);
            nm.horizontalOverflow = HorizontalWrapMode.Overflow;
            float oneLine = nm.preferredWidth;
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            int lines = Mathf.Clamp(Mathf.CeilToInt(oneLine / 116f), 1, 3);
            float nh = lines * nameSize * 1.21f;
            if (!string.IsNullOrEmpty(st.TypeLine))
                Ui.Label(rt, st.TypeLine.ToUpperInvariant(), UiFont.Cond, 12, st.Kind == "boss" ? Ui.Hot : Ui.Muted, -10, box + 6 + nh + 4, 160, 16, TextAnchor.UpperCenter, 2.88f);

            string id = st.Id;
            // the whole node is the target
            rt.sizeDelta = new Vector2(140, box + 14 + nh + 18);
            var item = UiParts.Make(rt, Nav, "node:" + id, () =>
            {
                if (C.SelectedStage == id) C.BeginLoadout();
                else C.SelectStage(id);
            });
            // moving onto a node selects it, so the card follows the cursor (a rebuild restoring the focus does not)
            item.onUserFocus = () => { if (C.SelectedStage != id) C.SelectStage(id); };
        }

        // ---------------- stage card ----------------

        void StageCardPanel(CampaignView view, bool withBoss)
        {
            var card = view.Card;
            float x = 1120, y = 124, w = 440;
            float cardH = withBoss ? 425f : 736f;
            var rt = Ui.Node(Root, "StageCard", x, y, w, cardH);
            Ui.Box(rt, 0, 0, w, cardH, Ui.PanelA(0.88f));

            // texts first, to know the height of the body
            var eye = Ui.Label(rt, card.KindLine.ToUpperInvariant(), UiFont.Title, 14, Ui.Gold, 28, 0, 390, 20, TextAnchor.MiddleLeft, 4.2f);
            var ttl = Ui.Label(rt, card.Title, UiFont.Mincho, 34, Ui.Bone, 28, 0, 390, 40, TextAnchor.UpperLeft, 0, true, 0.95f);
            var desc = Ui.Label(rt, card.Description, UiFont.Body, 15, Ui.Muted, 28, 0, 384, 80, TextAnchor.UpperLeft, 0, true, 1.5f);
            float ttlH = Mathf.Max(40f, ttl.preferredHeight);
            float descH = desc.preferredHeight;
            float bodyH = 8 + 20 + 14 + ttlH + 14 + descH + 14 + 28 + 4 + (card.Cleared ? 32 : 0) + 8 + 66 + 28;
            float top = cardH - bodyH;
            float cy = top + 8;
            eye.rectTransform.anchoredPosition = new Vector2(28, -cy); cy += 20 + 14;
            ttl.rectTransform.anchoredPosition = new Vector2(28, -cy); ttl.rectTransform.sizeDelta = new Vector2(390, ttlH); cy += ttlH + 14;
            desc.rectTransform.anchoredPosition = new Vector2(28, -cy); desc.rectTransform.sizeDelta = new Vector2(384, descH); cy += descH + 14;

            // threat and reward
            UiParts.Pips(rt, 28, cy + 12, card.Threat, 10f);
            Ui.Label(rt, card.ThreatLabel.ToUpperInvariant(), UiFont.Title, 14, Ui.Bone, 28 + 3 * 22 + 2 * 10 + 14, cy, 140, 28, TextAnchor.MiddleLeft, 2.8f);
            float rx = 412;
            if (card.Gems > 0)
            {
                var g = Ui.Label(rt, "+" + card.Gems, UiFont.Title, 18, Ui.Gold, rx - 80, cy, 80, 28, TextAnchor.MiddleRight);
                float gw = UiParts.Width(g);
                Ui.Diamond(rt, rx - gw - 13, cy + 14, 11, Ui.Ruby, Ui.Ruby, 0);
                rx = rx - gw - 13 - 22;
            }
            var coins = Ui.Label(rt, "+" + card.Coins, UiFont.Title, 18, Ui.Gold, rx - 100, cy, 100, 28, TextAnchor.MiddleRight);
            float cw = UiParts.Width(coins);
            Ui.Disc(rt, rx - cw - 15, cy + 14, 9, Ui.Gold);
            Ui.Disc(rt, rx - cw - 15, cy + 14, 5, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
            cy += 28 + 4;
            if (card.Cleared)
            {
                Ui.Label(rt, "CLEARED · REPLAY ANY TIME", UiFont.Cond, 13, Ui.Hot, 28, cy, 380, 24, TextAnchor.MiddleLeft, 2.4f);
                cy += 32;
            }
            cy += 8;

            // the strip above the body (a sunset over the bridge)
            Strip(rt, w, top);
            Ui.Frame(rt, 0, 0, w, cardH, 1.5f, Ui.Gold);

            // the Fight button
            var brush = UiParts.Brush(rt, Nav, "fight", 28, cy, 384, 66, card.CanStart ? "戦" : "", card.Action.ToUpperInvariant(), 26, 28, 8.84f, !card.CanStart, () => C.BeginLoadout());
            // a stage card is a button too (keyboard users reach the Fight button by moving right from the map)

            if (withBoss)
            {
                float ry = 124 + cardH + 18;
                var reveal = Ui.Node(Root, "BossReveal", x, ry, 440, 293);
                var img = Ui.Pic(reveal, "reveal_" + card.BossArt, 0, 0, 440, 293);
                Ui.Glow(reveal, 220, 146, 330, Ui.A(Ui.Acc, 0.0f), 1f);
                Ui.Frame(reveal, 0, 0, 440, 293, 1.5f, Ui.Gold);
            }
        }

        static void Strip(Transform card, float w, float h)
        {
            if (h < 20f) return;
            var rt = Ui.Node(card, "Strip", 0, 0, w, h);
            Ui.Grad(rt, 0, 0, w, h, Ui.Mix(Ui.Acc, Ui.Sky1, 0.12f), Ui.Mix(Ui.Acc, Ui.Sky1, 0.30f));
            float sunY = Mathf.Min(h * 0.45f, 120f);
            Ui.Glow(rt, 300, sunY, 90, Ui.A(Ui.Sun1, 0.25f), 1.5f);
            Ui.Disc(rt, 300, sunY, 48, Ui.Sun1);
            float by = h - 8f;
            Ui.Ground2(rt, new float[] { 0, by - 34, 80, by - 54, 160, by - 42, 240, by - 60, 340, by - 46, 440, by - 58 }, h, Ui.Mix(Ui.Acc, Ui.Sky1, 0.12f)).rectTransform.sizeDelta = new Vector2(w, h);
            // the bridge
            var arc = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                float x = Mathf.Lerp(20f, 420f, t);
                float y = Mathf.Lerp(Mathf.Lerp(by - 10f, by - 80f, t), Mathf.Lerp(by - 80f, by - 14f, t), t);
                arc.Add(new Vector2(x, y));
            }
            Ui.Line(rt, arc.ToArray(), 7f, Ui.Hex("#0c0605"));
            Ui.Disc(rt, 226, by - 66, 3.2f, Ui.Hex("#0c0605"));
            Ui.Grad(rt, 0, h - 60, w, 60, Ui.PanelA(0f), Ui.PanelA(0.95f));
        }
    }
}
