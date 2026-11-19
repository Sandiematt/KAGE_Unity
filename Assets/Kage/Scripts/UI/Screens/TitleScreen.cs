using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The title screen (JS "Home Screen", wonder.css .hm-*): the key art over the lake, the side menu, the Continue card and
    /// the Armory tile. The painted hero and the wordmark are the JS images; the sky, hills, castle and water are drawn here from
    /// the JS SVG's shapes. Echo Trials and Boss Rush are out of scope and left out.
    /// </summary>
    public sealed class TitleScreen : FlowPanel
    {
        public TitleScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Title") { }

        protected override string DefaultFocus { get { return "continue"; } }

        protected override void Build()
        {
            var save = C.Save;
            Backdrop(Root);

            // top left: seal, name, level, xp
            var seal = Ui.Node(Root, "Seal", 48, 16, 56, 56);
            Ui.Box(seal, 0, 0, 56, 56, Ui.Acc);
            Ui.Label(seal, "影", UiFont.Brush, 36, Ui.Bone, 0, 0, 56, 56, TextAnchor.MiddleCenter);
            Ui.Label(Root, "KAGE", UiFont.Title, 26, Ui.Bone, 122, 14, 200, 34, TextAnchor.MiddleLeft, 1.56f);
            Ui.Label(Root, "Lv. " + save.level, UiFont.Title, 22, Ui.Gold, 232, 17, 120, 30, TextAnchor.MiddleLeft);
            float xp = Mathf.Clamp01(save.xp / (float)GameRules.XpForLevel(save.level));
            Ui.Box(Root, 122, 58, 310, 4, new Color(0.925f, 0.898f, 0.827f, 0.16f));
            if (xp > 0f) Ui.Grad(Root, 122, 58, 310 * xp, 4, Ui.Hex("#e2b56a"), Ui.Gold, true);
            UiParts.Pills(Root, save, 1560f);

            if (save.devMode)
            {
                var badge = Ui.Node(Root, "DevBadge", 690, 14, 220, 26);
                Ui.Box(badge, 0, 0, 220, 26, Ui.Acc);
                Ui.Frame(badge, 0, 0, 220, 26, 1, Ui.Bone);
                Ui.Label(badge, "DEV · EVERYTHING UNLOCKED", UiFont.Cond, 13, Ui.Bone, 0, 0, 220, 26, TextAnchor.MiddleCenter, 1.2f);
            }

            // brand
            Ui.Pic(Root, "kage_wordmark", 72, 104, 370, 144);
            Ui.Label(Root, "THE LAST BETRAYAL", UiFont.Title, 15, Ui.Bone, 100, 258, 400, 24, TextAnchor.MiddleLeft, 9.3f);

            // menu
            Row(0, "play", "影", "Home", true, true, () => C.Continue());
            Row(1, "campaign", "戦", "Campaign", false, true, () => C.OpenCampaign());
            Row(2, "armory", "鎧", "Armory", false, true, () => C.OpenArmory());
            Row(3, "settings", "設", "Settings", false, true, () => C.OpenSettings());

            ContinueCard(save);
            ArmoryTile();
        }

        // ---------------- backdrop ----------------

        static void Backdrop(Transform p)
        {
            Ui.Grad(p, 0, 0, 1600, 900, Ui.Sky1, Ui.Sky3, false, Ui.Mix(Ui.Sky1, Ui.Sky2, 0.55f));
            Ui.Glow(p, 760, 300, 460, Ui.A(Ui.Acc, 0.3f), 1.2f);
            // far hills
            Ui.Ground2(p, new float[] { 0, 560, 90, 500, 180, 530, 300, 440, 420, 520, 520, 480, 620, 540, 760, 470, 880, 530, 1000, 460, 1120, 520, 1240, 470, 1360, 520, 1480, 480, 1600, 510 },
                900, Ui.A(Ui.Mix(Ui.Acc, Ui.Sky1, 0.22f), 0.85f), Ui.A(Ui.Ground, 0.85f));
            // the cliff with the castle
            Ui.Ground2(p, new float[] { 1110, 560, 1160, 440, 1196, 252, 1260, 236, 1300, 228, 1340, 200, 1400, 188, 1440, 152, 1520, 140, 1600, 128 }, 900, Ui.Ground);
            Color castle = Ui.Mix(Ui.Ground, Color.black, 0.55f);
            foreach (var r in new[] { new Rect(1352, 158, 148, 34), new Rect(1376, 120, 100, 28), new Rect(1400, 88, 52, 22), new Rect(1262, 206, 46, 24), new Rect(1272, 186, 26, 14), new Rect(1424, 64, 4, 14) })
                Ui.Box(p, r.x, r.y, r.width, r.height, castle);
            foreach (var r in new[] { new Vector4(1334, 162, 1518, 162), new Vector4(1360, 124, 1492, 124), new Vector4(1386, 92, 1466, 92), new Vector4(1250, 210, 1320, 210) })
            {
                float inset = 18f;
                Ui.Shape(p, 0, 0, 1600, 900, new[] { new Vector2(r.x, r.y), new Vector2(r.z, r.w), new Vector2(r.z - inset, r.y - 16), new Vector2(r.x + inset, r.y - 16) }, castle);
            }
            foreach (var w in new[] { new Vector3(1372, 170, 8), new Vector3(1392, 170, 8), new Vector3(1452, 170, 8), new Vector3(1472, 170, 8), new Vector3(1398, 130, 7), new Vector3(1446, 130, 7), new Vector3(1420, 96, 6), new Vector3(1280, 214, 6) })
                Ui.Box(p, w.x, w.y, w.z, w.z, Ui.Hot);
            // the dark ground and the trees at the edges
            Ui.Ground2(p, new float[] { 0, 650, 150, 622, 300, 628, 420, 600, 540, 584, 640, 610, 760, 600, 900, 592, 1000, 620, 1120, 610, 1260, 602, 1400, 630, 1600, 610 }, 900, Ui.Mix(Ui.Ground, Color.black, 0.85f));
            Color trunk = Ui.Mix(Ui.Ground, Color.black, 0.65f);
            foreach (var t in new[] { new Vector2(18, 12), new Vector2(44, 9), new Vector2(74, 14), new Vector2(1556, 12), new Vector2(1580, 9) })
                Ui.Box(p, t.x, 0, t.y, 900, trunk);
            Ui.Box(p, 0, 792, 1600, 16, Ui.Mix(Ui.Ground, Color.black, 0.5f));
            // the lake
            Ui.Grad(p, 0, 804, 1600, 96, Ui.Mix(Ui.Sky1, Color.black, 0.6f), Ui.Mix(Ui.Sky3, Color.black, 0.8f));
            foreach (var s in new[] { new Vector4(660, 814, 240, 4), new Vector4(690, 828, 190, 3), new Vector4(640, 842, 260, 3), new Vector4(710, 858, 140, 3), new Vector4(680, 874, 200, 2) })
                Ui.Box(p, s.x, s.y, s.z, s.w, Ui.A(Ui.Acc, 0.35f));
            // the painted hero and the breathing moon behind his head
            Ui.Glow(p, 730, 300, 270, Ui.A(Ui.Hot, 0.34f), 1.4f);
            Ui.Pic(p, "ui_hero", 340, 20, 692, 880);
            // soft fade from the hero into the right column, and the dark side behind the menu
            Ui.Grad(p, 968, 700, 96, 200, Color.clear, Color.clear, true, Ui.A(Ui.Sky2, 0.8f));
            Ui.Grad(p, 0, 0, 620, 900, Ui.A(Ui.Sky3, 0.86f), Color.clear, true, Ui.A(Ui.Sky3, 0.55f));
        }

        // ---------------- menu rows ----------------

        void Row(int index, string key, string glyph, string label, bool on, bool usable, System.Action onSubmit)
        {
            var rt = Ui.Node(Root, "Row_" + key, 40, 300 + index * 82, 380, 64);
            var wedge = Ui.Shape(rt, 18, 8, 320, 48, Ui.Poly(320, 48, 0, 18, 6, 4, 62, 0, 96, 10, 84, 30, 100, 48, 88, 64, 98, 86, 58, 100, 4, 94, 0, 76),
                Ui.Acc, Ui.Deep, Ui.A(Ui.Deep, 0f), true, "Wedge");
            wedge.gameObject.SetActive(on);
            var dia = Ui.Diamond(rt, 47, 32, 40, Ui.PanelA(0.8f), Ui.GoldDim, 1.5f);
            var icon = Ui.Label(rt, glyph, UiFont.Mincho, 22, usable ? Ui.Bone : Ui.A(Ui.Bone, 0.45f), 18, 0, 58, 64, TextAnchor.MiddleCenter);
            var lb = Ui.Label(rt, label.ToUpperInvariant(), UiFont.Title, 22, usable ? Ui.Bone : Ui.A(Ui.Bone, 0.45f), 106, 0, 260, 64, TextAnchor.MiddleLeft, 0.88f);
            if (on) dia.SetColors(Ui.Acc, Ui.Bone);
            UiParts.Make(rt, Nav, key, onSubmit, f =>
            {
                bool lit = f || on;
                wedge.gameObject.SetActive(lit);
                dia.SetColors(lit ? Ui.Acc : Ui.PanelA(0.8f), lit ? Ui.Bone : Ui.GoldDim);
                lb.rectTransform.anchoredPosition = new Vector2(f ? 110f : 106f, 0);
            }, usable);
        }

        // ---------------- continue card ----------------

        void ContinueCard(SaveData save)
        {
            string next = GameRules.NextStage(save);
            string eyebrow, title;
            if (next != null)
            {
                var ch = GameRules.ChapterOf(next);
                var p = GameRules.ChapterProgress(save, ch.Id);
                eyebrow = ch.Name.ToUpperInvariant() + " · " + Mathf.Min(p.Total, p.Done + 1) + " / " + p.Total;
                title = GameRules.Stage(next).Name;
            }
            else { eyebrow = "THE STORY SO FAR"; title = "Replay any stage"; }

            var rt = Ui.Node(Root, "Continue", 1000, 300, 556, 214);
            Ui.Grad(rt, 0, 0, 556, 214, Ui.Mix(Ui.Acc, Ui.Panel, 0.16f), Ui.Mix(Ui.Acc, Ui.Sky1, 0.38f));
            Ui.Glow(rt, 411, 90, 210, Ui.A(Ui.Hot, 0.5f), 1.3f);
            Ui.Disc(rt, 470, 66, 38, Ui.A(Ui.Mix(Ui.Hot, Color.white, 0.7f), 0.85f));
            Ui.Ground2(rt, new float[] { 0, 140, 60, 112, 130, 122, 200, 100, 270, 116, 340, 92, 420, 110, 490, 96, 556, 108 }, 214, Ui.A(Ui.Mix(Ui.Acc, Ui.Sky1, 0.42f), 0.55f)).rectTransform.sizeDelta = new Vector2(556, 214);
            Ui.Ground2(rt, new float[] { 0, 166, 80, 140, 160, 152, 240, 134, 330, 146, 420, 132, 490, 140, 556, 138 }, 214, Ui.Mix(Ui.Acc, Ui.Ground, 0.28f)).rectTransform.sizeDelta = new Vector2(556, 214);
            Ui.Ground2(rt, new float[] { 0, 196, 110, 174, 230, 186, 350, 172, 450, 182, 556, 178 }, 214, Ui.Mix(Ui.Ground, Color.black, 0.62f)).rectTransform.sizeDelta = new Vector2(556, 214);
            Ui.Grad(rt, 0, 0, 417, 214, Ui.PanelA(0.9f), Ui.PanelA(0f), true, Ui.PanelA(0.55f));
            var eye = Ui.Label(rt, "CONTINUE", UiFont.Title, 16, Ui.Gold, 36, 24, 200, 24, TextAnchor.MiddleLeft, 1.92f);
            float ew = UiParts.Width(eye, 1.92f);
            Ui.Label(rt, "›", UiFont.Title, 16, Ui.Gold, 36 + ew + 14, 24, 20, 24, TextAnchor.MiddleLeft);
            Ui.Label(rt, eyebrow, UiFont.Title, 16, Ui.Gold, 36 + ew + 14 + 20 + 14, 24, 300, 24, TextAnchor.MiddleLeft, 1.92f);
            Ui.Label(rt, title, UiFont.Mincho, 38, Ui.Bone, 36, 52, 380, 100, TextAnchor.UpperLeft, 0, true, 0.95f);
            // play button
            Ui.Disc(rt, 504, 162, 30, Ui.A(Ui.Gold, 0.35f));
            Ui.Disc(rt, 504, 162, 30, Ui.Gold);
            Ui.Disc(rt, 504, 162, 27.5f, Ui.Mix(Ui.Panel, Color.black, 0.9f));
            Ui.Shape(rt, 0, 0, 556, 214, new[] { new Vector2(498, 150), new Vector2(516, 162), new Vector2(498, 174) }, Ui.Gold);
            Color edge = Ui.Mix(Ui.Hot, Ui.Gold, 0.55f);
            Ui.Frame(rt, 0, 0, 556, 214, 2, edge);
            UiParts.Make(rt, Nav, "continue", () => C.Continue(), f =>
            {
                rt.anchoredPosition = new Vector2(1000, -300 + (f ? 3 : 0));
            });
        }

        void ArmoryTile()
        {
            var rt = Ui.Node(Root, "ArmoryTile", 1000, 538, 176, 184);
            Ui.Box(rt, 0, 0, 176, 184, Ui.Mix(Ui.Sky1, Ui.Panel, 0.9f));
            Ui.Glow(rt, 88, 100, 90, Ui.A(Ui.Acc, 0.45f), 1.2f);
            var blade = Ui.Pic(rt, "ui_w_kagetsu", 8, 30, 160, 94, Ui.A(Color.white, 0.5f));
            var edge = Ui.Node(rt, "Edge", 0, 0, 176, 184);
            Ui.Frame(edge, 0, 0, 176, 184, 2, Ui.GoldDim);
            var label = Ui.Label(rt, "ARMORY", UiFont.Title, 17, Ui.A(Ui.Bone, 0.6f), 0, 150, 176, 24, TextAnchor.MiddleCenter, 2f);
            UiParts.Make(rt, Nav, "armory-tile", () => C.OpenArmory(), f =>
            {
                rt.anchoredPosition = new Vector2(1000, -(538 - (f ? 3 : 0)));
                blade.color = Ui.A(Color.white, f ? 0.9f : 0.5f);
                label.color = Ui.A(Ui.Bone, f ? 1f : 0.6f);
                foreach (var img in edge.GetComponentsInChildren<Image>()) img.color = f ? Ui.Gold : Ui.GoldDim;
            });
        }
    }
}
