using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The title screen (JS "Home Screen", wonder.css .hm-*): the key art over the lake, the side menu, the Continue card, the Armory tile, the
    /// four utility diamonds and the Codex button. The painted hero and the wordmark are the JS images; the sky, hills, castle and water are
    /// drawn here from the JS SVG's shapes. It moves like the JS screen: the key art fades in, the brand and the menu rows rise one after another,
    /// the red moon breathes, clouds and sakura petals drift, the Continue card's hills slide and its petals blow by, hovering a row grows its
    /// wedge, a tile lifts. Echo Trials and Boss Rush are out of scope (spec) and left out.
    /// </summary>
    public sealed class TitleScreen : FlowPanel
    {
        sealed class MenuRow
        {
            public UiFocusTween tween;
            public bool focused, home;
        }

        readonly List<MenuRow> rows = new List<MenuRow>();

        public TitleScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Title") { }

        protected override string DefaultFocus { get { return "continue"; } }

        protected override void Build()
        {
            var save = C.Save;
            rows.Clear();
            Backdrop(Root, Fresh);

            // top left: seal, name, level, xp
            var seal = Ui.Node(Root, "Seal", 48, 16, 56, 56);
            Ui.Box(seal, 0, 0, 56, 56, Ui.Acc);
            Ui.Label(seal, "影", UiFont.Brush, 36, Ui.Bone, 0, 0, 56, 56, TextAnchor.MiddleCenter);
            Ui.Label(Root, "KAGE", UiFont.Title, 26, Ui.Bone, 122, 14, 200, 34, TextAnchor.MiddleLeft, 1.56f);
            Ui.Label(Root, "Lv. " + save.level, UiFont.Title, 22, Ui.Gold, 232, 17, 120, 30, TextAnchor.MiddleLeft);
            float xp = Mathf.Clamp01(save.xp / (float)GameRules.XpForLevel(save.level));
            Ui.Box(Root, 122, 58, 310, 4, new Color(0.925f, 0.898f, 0.827f, 0.16f));
            if (xp > 0f) Ui.Grad(Root, 122, 58, 310 * xp, 4, Ui.Hex("#e2b56a"), Ui.Gold, true);
            UiParts.Pills(Root, save, 1500f);
            CodexButton(1516, 22);

            if (save.devMode)
            {
                var badge = Ui.Node(Root, "DevBadge", 690, 14, 220, 26);
                Ui.Box(badge, 0, 0, 220, 26, Ui.Acc);
                Ui.Frame(badge, 0, 0, 220, 26, 1, Ui.Bone);
                Ui.Label(badge, "DEV · EVERYTHING UNLOCKED", UiFont.Cond, 13, Ui.Bone, 0, 0, 220, 26, TextAnchor.MiddleCenter, 1.2f);
            }

            // brand: rises as one
            var wordmark = Ui.Pic(Root, "kage_wordmark", 72, 104, 370, 144);
            var tagline = Ui.Label(Root, "THE LAST BETRAYAL", UiFont.Title, 15, Ui.Bone, 100, 258, 400, 24, TextAnchor.MiddleLeft, 9.3f);
            Enter(wordmark.transform, UiAnims.WRise(.8f));
            Enter(tagline.transform, UiAnims.WRise(.8f));

            // menu (the JS rows' stagger by their place in the six-row menu: Echo Trials and Boss Rush are not built)
            Row(0, 0f, "play", "影", "Home", true, () => C.Continue());
            Row(1, .05f, "campaign", "戦", "Campaign", false, () => C.OpenCampaign());
            Row(2, .1f, "armory", "鎧", "Armory", false, () => C.OpenArmory());
            Row(3, .25f, "settings", "設", "Settings", false, () => C.OpenSettings());

            ContinueCard(save);
            ArmoryTile();
            Utilities(save);
        }

        // ---------------- backdrop ----------------

        static void Backdrop(Transform p, bool fresh)
        {
            Ui.Grad(p, 0, 0, 1600, 900, Ui.Sky1, Ui.Sky3, false, Ui.Mix(Ui.Sky1, Ui.Sky2, 0.55f));
            // the sun's glow breathes (wGlow, 5 s)
            UiFx.Loop(Ui.Glow(p, 760, 300, 460, Ui.A(Ui.Acc, 0.3f), 1.2f).transform, UiAnims.WGlow(5f));
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
            // the castle's windows glow (wGlow, 3 s)
            var windows = Ui.Node(p, "Windows", 0, 0, 1600, 900);
            foreach (var w in new[] { new Vector3(1372, 170, 8), new Vector3(1392, 170, 8), new Vector3(1452, 170, 8), new Vector3(1472, 170, 8), new Vector3(1398, 130, 7), new Vector3(1446, 130, 7), new Vector3(1420, 96, 6), new Vector3(1280, 214, 6) })
                Ui.Box(windows, w.x, w.y, w.z, w.z, Ui.Hot);
            UiFx.Loop(windows, UiAnims.WGlow(3f));
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

            // far clouds, behind the hero
            Color dark = Ui.Mix(Ui.Ground, Color.black, 0.7f), lit = Ui.Mix(Ui.Hot, Ui.Hex("#d8b4aa"), 0.45f);
            UiCloudView.Build(p, "CloudsFar", new[]
            {
                Cloud(150, 560, 140, -30, 0.5f, false), Cloud(420, 700, 170, -110, 0.42f, false), Cloud(96, 420, 120, -85, 0.35f, false),
            }, dark, lit);

            // the painted hero fades in; the red moon behind his head breathes over him
            var hero = Ui.Pic(p, "ui_hero", 340, 20, 692, 880);
            if (fresh) UiFx.In(hero.transform, UiAnims.WFade(.9f));
            UiFx.Loop(Ui.Glow(p, 730, 300, 262, Ui.A(Ui.Hot, 0.34f), 1.4f).transform, UiAnims.HmMoon());

            // near clouds, then the soft fade from the hero into the right column
            UiCloudView.Build(p, "CloudsNear", new[]
            {
                Cloud(236, 520, 95, -20, 0.55f, false), Cloud(318, 380, 75, -60, 0.32f, true), Cloud(170, 300, 85, -12, 0.22f, true), Cloud(470, 640, 110, -70, 0.45f, false),
            }, dark, lit);
            Ui.Grad(p, 968, 700, 96, 200, Color.clear, Color.clear, true, Ui.A(Ui.Sky2, 0.8f));

            // sakura petals falling across the whole stage
            UiPetalView.Build(p, "Petals", 0, 0, 1600, 900, new PetalField(26, 1600f, 900f, PetalField.Direction.Fall), 14f, 10f, Ui.Hot, Ui.Hex("#d9302a"));
            // the dark side behind the menu
            Ui.Grad(p, 0, 0, 620, 900, Ui.A(Ui.Sky3, 0.86f), Color.clear, true, Ui.A(Ui.Sky3, 0.55f));
        }

        static CloudBand Cloud(float y, float w, float seconds, float delay, float opacity, bool lit)
        {
            return new CloudBand { Y = y, Width = w, Seconds = seconds, Delay = delay, Opacity = opacity, Lit = lit };
        }

        // ---------------- menu rows ----------------

        void Row(int index, float delay, string key, string glyph, string label, bool on, System.Action onSubmit)
        {
            var rt = Ui.Node(Root, "Row_" + key, 40, 300 + index * 82, 380, 64);
            Enter(rt, UiAnims.WRise(.6f, delay));
            var wedge = Ui.Shape(rt, 18, 8, 320, 48, Ui.Poly(320, 48, 0, 18, 6, 4, 62, 0, 96, 10, 84, 30, 100, 48, 88, 64, 98, 86, 58, 100, 4, 94, 0, 76),
                Ui.Acc, Ui.Deep, Ui.A(Ui.Deep, 0f), true, "Wedge");
            var dia = Ui.Diamond(rt, 47, 32, 40, Ui.PanelA(0.8f), Ui.GoldDim, 1.5f);
            Ui.Label(rt, glyph, UiFont.Mincho, 22, Ui.Bone, 18, 0, 58, 64, TextAnchor.MiddleCenter);
            var lb = Ui.Label(rt, label.ToUpperInvariant(), UiFont.Title, 22, Ui.Bone, 106, 0, 260, 64, TextAnchor.MiddleLeft, 0.88f);
            var row = new MenuRow { home = on };
            // JS: the wedge grows from the left in .28 s, the label slides 4 px in .2 s, the diamond lights
            row.tween = UiFocusTween.On(rt, .28f, v =>
            {
                wedge.rectTransform.localScale = new Vector3(Mathf.Max(0.001f, UiEase.Ease.Evaluate(v)), 1f, 1f);
                lb.rectTransform.anchoredPosition = new Vector2(106f + 4f * UiEase.Ease.Evaluate(v), 0f);
                dia.SetColors(Color.Lerp(Ui.PanelA(0.8f), Ui.Acc, v), Color.Lerp(Ui.GoldDim, Ui.Bone, v));
            });
            rows.Add(row);
            UiParts.Make(rt, Nav, key, onSubmit, f =>
            {
                row.focused = f;
                RefreshRows();
            });
            RefreshRows();
        }

        // Home is lit while no other row is: the JS hover rule that gives the wedge to the row under the pointer
        void RefreshRows()
        {
            bool other = false;
            foreach (var r in rows) if (!r.home && r.focused) other = true;
            foreach (var r in rows) r.tween.Set(r.focused || (r.home && !other));
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
            rt.gameObject.AddComponent<RectMask2D>();        // the hills slide past its edges (JS overflow hidden)
            Enter(rt, UiAnims.WRise(.7f, .1f));
            Ui.Grad(rt, 0, 0, 556, 214, Ui.Mix(Ui.Acc, Ui.Panel, 0.16f), Ui.Mix(Ui.Acc, Ui.Sky1, 0.38f));
            Ui.Glow(rt, 411, 90, 210, Ui.A(Ui.Hot, 0.5f), 1.3f);
            // the moon breathes (hmCMoon, 5 s)
            UiFx.Loop(Ui.Disc(rt, 470, 66, 38, Ui.A(Ui.Mix(Ui.Hot, Color.white, 0.7f), 0.85f)).transform, UiAnims.HmCMoon());
            // two hills slide 10 px either way, out of step (hmHill)
            var h1 = Ui.Ground2(rt, new float[] { 0, 140, 60, 112, 130, 122, 200, 100, 270, 116, 340, 92, 420, 110, 490, 96, 556, 108 }, 214, Ui.A(Ui.Mix(Ui.Acc, Ui.Sky1, 0.42f), 0.55f));
            h1.rectTransform.sizeDelta = new Vector2(556, 214);
            UiFx.Loop(h1.transform, UiAnims.HmHill(14f, 0f));
            var h2 = Ui.Ground2(rt, new float[] { 0, 166, 80, 140, 160, 152, 240, 134, 330, 146, 420, 132, 490, 140, 556, 138 }, 214, Ui.Mix(Ui.Acc, Ui.Ground, 0.28f));
            h2.rectTransform.sizeDelta = new Vector2(556, 214);
            UiFx.Loop(h2.transform, new UiAnim("hmHill2", 10f).Eased(UiEase.EaseInOut).FromTo(UiProp.X, 10, -10).Repeat(true).Delayed(-4f));
            Ui.Ground2(rt, new float[] { 0, 196, 110, 174, 230, 186, 350, 172, 450, 182, 556, 178 }, 214, Ui.Mix(Ui.Ground, Color.black, 0.62f)).rectTransform.sizeDelta = new Vector2(556, 214);
            // petals blowing from the right (the wind variant)
            UiPetalView.Build(rt, "Petals", 0, 0, 556, 214, new PetalField(16, 556f, 214f, PetalField.Direction.Wind), 18f, 13f, Ui.Hot, Ui.Hex("#d9302a"));
            Ui.Grad(rt, 0, 0, 417, 214, Ui.PanelA(0.9f), Ui.PanelA(0f), true, Ui.PanelA(0.55f));
            var eye = Ui.Label(rt, "CONTINUE", UiFont.Title, 16, Ui.Gold, 36, 24, 200, 24, TextAnchor.MiddleLeft, 1.92f);
            float ew = UiParts.Width(eye, 1.92f);
            Ui.Label(rt, "›", UiFont.Title, 16, Ui.Gold, 36 + ew + 14, 24, 20, 24, TextAnchor.MiddleLeft);
            Ui.Label(rt, eyebrow, UiFont.Title, 16, Ui.Gold, 36 + ew + 14 + 20 + 14, 24, 300, 24, TextAnchor.MiddleLeft, 1.92f);
            Ui.Label(rt, title, UiFont.Mincho, 38, Ui.Bone, 36, 52, 380, 100, TextAnchor.UpperLeft, 0, true, 0.95f);
            // the play button's glow pulses (wGlow, 2.4 s)
            UiFx.Loop(Ui.Disc(rt, 504, 162, 30, Ui.A(Ui.Gold, 0.35f)).transform, UiAnims.WGlow(2.4f));
            Ui.Disc(rt, 504, 162, 30, Ui.Gold);
            Ui.Disc(rt, 504, 162, 27.5f, Ui.Mix(Ui.Panel, Color.black, 0.9f));
            Ui.Shape(rt, 0, 0, 556, 214, new[] { new Vector2(498, 150), new Vector2(516, 162), new Vector2(498, 174) }, Ui.Gold);
            Color edge = Ui.Mix(Ui.Hot, Ui.Gold, 0.55f);
            Ui.Frame(rt, 0, 0, 556, 214, 2, edge);
            // hover: the card lifts 3 px in .2 s
            var lift = UiFocusTween.On(rt, .2f, v => UiOffsets.Of(rt).SetFocus(new Vector2(0f, 3f * v)));
            UiParts.Make(rt, Nav, "continue", () => C.Continue(), f => lift.Set(f));
        }

        void ArmoryTile()
        {
            var rt = Ui.Node(Root, "ArmoryTile", 1000, 538, 176, 184);
            Enter(rt, UiAnims.WRise(.6f, 0f));
            Ui.Box(rt, 0, 0, 176, 184, Ui.Mix(Ui.Sky1, Ui.Panel, 0.9f));
            Ui.Glow(rt, 88, 100, 90, Ui.A(Ui.Acc, 0.45f), 1.2f);
            var bladeNode = Ui.Node(rt, "BladeBox", 8, 30, 160, 94);
            bladeNode.pivot = new Vector2(0.5f, 0.5f);
            bladeNode.anchoredPosition = new Vector2(8 + 80, -(30 + 47));
            var blade = bladeNode.gameObject.AddComponent<RawImage>();
            blade.texture = Ui.Tex("ui_w_kagetsu");
            blade.raycastTarget = false;
            var edge = Ui.Node(rt, "Edge", 0, 0, 176, 184);
            Ui.Frame(edge, 0, 0, 176, 184, 2, Ui.GoldDim);
            var label = Ui.Label(rt, "ARMORY", UiFont.Title, 17, Ui.A(Ui.Bone, 0.6f), 0, 150, 176, 24, TextAnchor.MiddleCenter, 2f);
            // hover: the tile lifts 4 px, the blade turns 6 degrees more (JS .hm-tile:hover .blade), the frame and label light, all in .2 to .3 s
            var tween = UiFocusTween.On(rt, .25f, v =>
            {
                UiOffsets.Of(rt).SetFocus(new Vector2(0f, 4f * v));
                bladeNode.localRotation = Quaternion.Euler(0f, 0f, 6f * v);
                blade.color = Ui.A(Color.white, Mathf.Lerp(0.5f, 0.9f, v));
                label.color = Ui.A(Ui.Bone, Mathf.Lerp(0.6f, 1f, v));
                foreach (var img in edge.GetComponentsInChildren<Image>()) img.color = Color.Lerp(Ui.GoldDim, Ui.Gold, v);
            });
            UiParts.Make(rt, Nav, "armory-tile", () => C.OpenArmory(), f => tween.Set(f));
        }

        // ---------------- the Codex button and the four diamonds ----------------

        void CodexButton(float x, float y)
        {
            var rt = Ui.Node(Root, "Codex", x, y, 44, 44);
            Ui.Box(rt, 0, 0, 44, 44, Ui.PanelA(0.78f));
            var frame = Ui.Node(rt, "Frame", 0, 0, 44, 44);
            Ui.Frame(frame, 0, 0, 44, 44, 1, Ui.GoldDim);
            // a lotus: three petals and a stem
            var c = Ui.Bone;
            Ui.Shape(rt, 0, 0, 44, 44, Petal(22, 20, 4.4f, 9f, 0f), c, null, null, false, "Petal");
            Ui.Shape(rt, 0, 0, 44, 44, Petal(14.5f, 22, 4f, 8f, -0.8f), c, null, null, false, "Petal");
            Ui.Shape(rt, 0, 0, 44, 44, Petal(29.5f, 22, 4f, 8f, 0.8f), c, null, null, false, "Petal");
            Ui.Box(rt, 21.2f, 27, 1.6f, 8, c, "Stem");
            var tween = UiFocusTween.On(rt, .16f, v => { foreach (var img in frame.GetComponentsInChildren<Image>()) img.color = Color.Lerp(Ui.GoldDim, Ui.Gold, v); });
            UiParts.Make(rt, Nav, "codex", () => flow.OpenInfo(InfoView.Controls), f => tween.Set(f));
        }

        static Vector2[] Petal(float cx, float cy, float rx, float ry, float tilt)
        {
            var pts = new Vector2[14];
            float c = Mathf.Cos(tilt), s = Mathf.Sin(tilt);
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2f;
                float px = Mathf.Cos(a) * rx, py = Mathf.Sin(a) * ry;
                pts[i] = new Vector2(cx + px * c - py * s, cy + px * s + py * c);
            }
            return pts;
        }

        void Utilities(SaveData save)
        {
            // JS .hm-util: four diamonds 78 px apart by 22, right-aligned to 1556 on the bottom row
            string[] keys = { "records", "trophies", "profile", "sound" };
            for (int i = 0; i < 4; i++)
            {
                float cx = 1217 + i * 100f, cy = 856f;
                string key = keys[i];
                var rt = Ui.Node(Root, "Util_" + key, cx - 39, cy - 39, 78, 78);
                var dia = Ui.Diamond(rt, 39, 39, 54, Ui.PanelA(0.82f), Ui.GoldDim, 1.5f);
                Color ink = Ui.Bone;
                switch (i)
                {
                    case 0: Bars(rt, ink); break;
                    case 1: Cup(rt, ink); break;
                    case 2: Person(rt, ink); break;
                    default: Speaker(rt, ink, save.settings.sound); break;
                }
                if (i == 1)
                {
                    int broken = 0;
                    foreach (var st in GameRules.Stages) if ((st.Kind == "boss" || st.Kind == "subboss" || st.Kind == "echo") && GameRules.DefeatedCount(save, st.Id) > 0) broken++;
                    if (broken > 0)
                    {
                        var b = Ui.Node(rt, "Badge", 78 - 2 - 22, 4, 22, 18);
                        Ui.Box(b, 0, 0, 22, 18, Ui.Acc);
                        Ui.Frame(b, 0, 0, 22, 18, 2, Ui.Bone);
                        Ui.Label(b, broken.ToString(), UiFont.Cond, 11, Ui.Bone, 0, 0, 22, 18, TextAnchor.MiddleCenter);
                    }
                }
                var tween = UiFocusTween.On(rt, .2f, v =>
                {
                    dia.SetColors(Ui.PanelA(0.82f), Color.Lerp(Ui.GoldDim, Ui.Gold, v));
                    rt.localScale = Vector3.one * (1f + 0.08f * v);
                });
                System.Action act = null;
                if (i == 0) act = () => flow.OpenInfo(InfoView.Records);
                else if (i == 1) act = () => flow.OpenInfo(InfoView.Trophies);
                else if (i == 2) act = () => flow.OpenInfo(InfoView.Profile);
                else act = () => C.UpdateSettings(s => { s.sound = !s.sound; s.music = s.sound; });
                UiParts.Make(rt, Nav, key, act, f => tween.Set(f));
            }
        }

        static void Bars(Transform p, Color c)
        {
            Ui.Box(p, 39 - 8, 39 + 2, 3, 8, c, "Bar");
            Ui.Box(p, 39 - 1.5f, 39 - 9, 3, 19, c, "Bar");
            Ui.Box(p, 39 + 5, 39 - 3, 3, 13, c, "Bar");
        }

        static void Cup(Transform p, Color c)
        {
            Ui.Shape(p, 0, 0, 78, 78, new[] { new Vector2(30, 28), new Vector2(48, 28), new Vector2(48, 36), new Vector2(43, 43), new Vector2(35, 43), new Vector2(30, 36) }, c, null, null, false, "Cup");
            Ui.Box(p, 37.5f, 43, 3, 7, c, "Stem");
            Ui.Box(p, 31, 50, 16, 3, c, "Base");
            Ui.Line(p, new[] { new Vector2(30, 31), new Vector2(26, 31), new Vector2(27, 37), new Vector2(31, 38) }, 1.5f, c).rectTransform.sizeDelta = new Vector2(78, 78);
            Ui.Line(p, new[] { new Vector2(48, 31), new Vector2(52, 31), new Vector2(51, 37), new Vector2(47, 38) }, 1.5f, c).rectTransform.sizeDelta = new Vector2(78, 78);
        }

        static void Person(Transform p, Color c)
        {
            var head = new Vector2[16];
            for (int i = 0; i < head.Length; i++) { float a = i / 16f * Mathf.PI * 2f; head[i] = new Vector2(39 + Mathf.Cos(a) * 6f, 33 + Mathf.Sin(a) * 6f); }
            Ui.Line(p, Close(head), 2f, c).rectTransform.sizeDelta = new Vector2(78, 78);
            Ui.Line(p, new[] { new Vector2(28, 52), new Vector2(28, 49), new Vector2(31, 45), new Vector2(35, 45), new Vector2(43, 45), new Vector2(47, 45), new Vector2(50, 49), new Vector2(50, 52) }, 2f, c).rectTransform.sizeDelta = new Vector2(78, 78);
        }

        static void Speaker(Transform p, Color c, bool on)
        {
            Ui.Shape(p, 0, 0, 78, 78, new[] { new Vector2(28, 35), new Vector2(33, 35), new Vector2(39, 29), new Vector2(39, 49), new Vector2(33, 43), new Vector2(28, 43) }, c, null, null, false, "Speaker");
            if (on)
            {
                Ui.Line(p, new[] { new Vector2(43, 35), new Vector2(45.5f, 39), new Vector2(43, 43) }, 2f, c).rectTransform.sizeDelta = new Vector2(78, 78);
                Ui.Line(p, new[] { new Vector2(46, 31), new Vector2(50, 39), new Vector2(46, 47) }, 2f, c).rectTransform.sizeDelta = new Vector2(78, 78);
            }
            else
            {
                Ui.Line(p, new[] { new Vector2(44, 34), new Vector2(51, 44) }, 2f, c).rectTransform.sizeDelta = new Vector2(78, 78);
                Ui.Line(p, new[] { new Vector2(51, 34), new Vector2(44, 44) }, 2f, c).rectTransform.sizeDelta = new Vector2(78, 78);
            }
        }

        static Vector2[] Close(Vector2[] pts)
        {
            var r = new Vector2[pts.Length + 1];
            pts.CopyTo(r, 0);
            r[pts.Length] = pts[0];
            return r;
        }
    }
}
