using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The Armory (JS "Armory", wonder.css .ar-* and .al-*, SAN-90): tabs for weapons, helmets and armour; on the first two a live
    /// Kage in front of the sun, the selected item's details on the right and a row of tiles under it; on the Armor tab a row of
    /// tall cards, one Kage in each armour, with the details under the row. What it shows is <see cref="ArmoryView"/> and what its
    /// buttons do is <see cref="ArmoryController"/>; this class only draws them. The backdrop is drawn into the world's camera-space
    /// canvas (behind the fighters, <see cref="ArmoryWorld"/>), everything else here.
    /// </summary>
    public sealed class ArmoryScreen : FlowPanel
    {
        const float TileW = 127f, TileGap = 10f, GridX = 40f, GridY = 748f, GridW = 950f, GridH = 112f;

        float scroll, cardScroll, cardMax;
        // what the last build showed: the entrances (tiles rising, the details fading in) play when the tab, the row or the item changes, like the JS render
        ArmoryTab? builtTab;
        string builtSelection;
        bool enterTiles, enterDetail;
        RectTransform tileContent, cardsFront, cardsBack;
        float tileMax;
        int tileIndex;
        string pendingBurstId;
        static readonly Color Violet = Ui.Hex("#A855F7"), Blue = Ui.Hex("#38BDF8"), Lit = Ui.Hex("#EAB308");
        static readonly Color SteelGray = Ui.Hex("#9CA3AF"), FlameOrange = Ui.Hex("#F97316"), LabelLight = Ui.Hex("#F3F4F6");

        public ArmoryScreen(GameFlow flow, RectTransform stage) : base(flow, stage, "Armory") { }

        ArmoryController A { get { return C.Armory; } }

        protected override string DefaultFocus { get { return A != null ? "tile:" + A.SelectedId : null; } }

        bool focusTile;

        public override bool Handle(MenuCommand cmd)
        {
            // Q / E and the shoulder buttons turn the tabs; the focus follows to the new tab's selected item
            if (A != null && (cmd == MenuCommand.Prev || cmd == MenuCommand.Next))
            {
                focusTile = true;
                A.StepTab(cmd == MenuCommand.Prev ? -1 : 1);
                focusTile = false;
                return true;
            }
            return base.Handle(cmd);
        }

        protected override string FocusAfterBuild(string key)
        {
            return focusTile && A != null ? "tile:" + A.SelectedId : key;
        }

        // ---------------- build ----------------

        protected override void Build()
        {
            var a = A;
            if (a == null) return;
            var v = a.View;
            var save = C.Save;
            bool armor = v.Tab == ArmoryTab.Armor;
            bool helmets = v.Tab == ArmoryTab.Helmets;
            var world = flow.ArmoryWorld;
            RectTransform back = world != null ? world.BackRoot : null;
            if (back != null)
            {
                for (int i = back.childCount - 1; i >= 0; i--)
                {
                    back.GetChild(i).gameObject.SetActive(false);
                    Object.Destroy(back.GetChild(i).gameObject);
                }
                if (armor) ArmorBackdrop(back);
                else if (helmets) HelmetBackdrop(back, v.TierId);
                else Backdrop(back);
            }

            enterTiles = Fresh || builtTab != v.Tab;
            enterDetail = enterTiles || builtSelection != v.SelectedId;
            builtTab = v.Tab; builtSelection = v.SelectedId;
            if (!helmets)
                Ui.Label(Root, v.Kanji, UiFont.Brush, armor ? 46 : 64, Ui.A(Ui.Bone, 0.2f), armor ? 44 : 64, armor ? 98 : 150, 400, armor ? 60 : 80, TextAnchor.UpperLeft);
            if (v.Preview == ArmoryPreview.Illustration) Preview(v);
            if (armor) ArmorBody(v, save, back);
            else if (helmets) HelmetBody(v, save);
            else
            {
                Detail(v);
                Actions(v, Root, 1020, 752, 540);
                Tiles(v, save);
            }
            Header(v);
            if (world != null) Fighters(world, v, save);
            if (back != null) SetLayer(back, 0);
        }

        static void SetLayer(Transform t, int layer)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
        }

        void Fighters(ArmoryWorld world, ArmoryView v, SaveData save)
        {
            if (v.Preview == ArmoryPreview.Illustration)
            {
                world.ShowNone();
                return;
            }
            if (v.Tab != ArmoryTab.Armor)
            {
                world.ShowSingle(ArmoryView.PreviewSave(save, v.Tab, v.SelectedId), v.Tab == ArmoryTab.Helmets);
                return;
            }
            var saves = new List<SaveData>();
            var xs = new List<float>();
            var shown = new List<bool>();
            for (int i = 0; i < v.Tiles.Length; i++)
            {
                saves.Add(ArmoryView.PreviewSave(save, ArmoryTab.Armor, v.Tiles[i].Id));
                // Armor cards display unarmed Kage centered symmetrically inside each card with zero weapon overlap
                xs.Add(CardX(i) + CardW * 0.50f);
                shown.Add(CardInWindow(i));
            }
            string key = "armor|unarmed:" + KageCharacter.LoadoutKey(save).Split('|')[0] + "|" + v.Tiles.Length;
            // the row scrolls by the camera panning with it, so each Kage stays in his card
            world.ShowRow(saves, xs, key, cardScroll, shown);
        }

        // ---------------- the Weapons tab's preview: the JS illustration of the blade ----------------

        void Preview(ArmoryView v)
        {
            var holder = Ui.Node(Root, "Preview", 0, 96, ArmoryPreviewMotion.Width, ArmoryPreviewMotion.Height);
            holder.gameObject.AddComponent<RectMask2D>();
            // the faint warrior behind the blade, then the blade turning slowly in front of the sun
            Ui.Pic(holder, GearArt.GhostKey, 0, 0, ArmoryPreviewMotion.Width, ArmoryPreviewMotion.Height, new Color(1f, 1f, 1f, 0.32f)).name = "Ghost";
            var w = JsTables.Weapon(v.SelectedId);
            var art = Ui.Tex(v.PreviewKey);
            ArmoryBladeSway.Build(holder, art, w != null ? w.Len : 0);
            if (enterDetail) UiFx.In(holder, UiAnims.WFade(.35f));
        }

        // ---------------- the top bar ----------------

        void Header(ArmoryView v)
        {
            // Unified dark obsidian header band across all Armory tabs
            Ui.Box(Root, 0, 0, 1600, 96, Ui.PanelA(0.92f), "HeaderBand");
            Ui.Box(Root, 0, 95, 1600, 1, Ui.A(Ui.GoldDim, 0.45f), "HeaderRule");
            UiParts.Back(Root, Nav, () => C.Back(), 40, 19);
            var title = Ui.Label(Root, "ARMORY", UiFont.Title, 36, Ui.Bone, 118, 0, 300, 96, TextAnchor.MiddleLeft, 0f);
            float titleW = UiParts.Width(title);
            string[] names = { "Weapons", "Helmets", "Armor" };
            var widths = new float[3];
            float total = 0f;
            for (int i = 0; i < 3; i++)
            {
                var probe = Ui.Label(Root, names[i].ToUpperInvariant(), UiFont.Title, 20, Color.white, 0, 0, 300, 48, TextAnchor.MiddleLeft, 2.4f);
                widths[i] = UiParts.Width(probe, 2.4f) + 2.4f + 44f;
                Object.Destroy(probe.gameObject);
                total += widths[i];
            }
            total += 2 * 32f;
            float grpW = 58f + 20f + titleW;
            float pillsW = 376f;
            float free = 1520f - grpW - total - pillsW;
            float x = 40f + grpW + free / 2f;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                bool on = (int)v.Tab == i;
                var rt = Ui.Node(Root, "Tab_" + names[i], x, 24, widths[i], 48);
                var tabPlate = Ui.Chamfer(0, 4, widths[i], 40, 6f);
                if (on)
                {
                    Ui.Shape(rt, 0, 0, widths[i], 48, tabPlate, Ui.A(Ui.Acc, 0.34f), Ui.A(Ui.Deep, 0.16f), null, false, "TabBg");
                    Ui.Stroke(rt, 0, 0, widths[i], 48, tabPlate, 1.2f, Ui.A(Ui.Gold, 0.65f), -1.2f, "TabBorder");
                    Ui.Box(rt, 12f, 42f, widths[i] - 24f, 2f, Ui.Gold, "TabRule");
                }
                var hoverBar = Ui.Box(rt, 14f, 42f, widths[i] - 28f, 2f, Color.clear, "TabHover");
                var t = Ui.Label(rt, names[i].ToUpperInvariant(), UiFont.Title, 20, on ? Ui.Bone : Ui.Muted, 0, 0, widths[i], 48, TextAnchor.MiddleCenter, 2.4f);
                var tabTween = UiFocusTween.On(rt, .16f, k =>
                {
                    t.color = on ? Ui.Bone : Color.Lerp(Ui.Muted, Ui.Bone, k);
                    if (!on) hoverBar.color = Ui.A(Ui.Gold, 0.65f * k);
                });
                UiParts.Make(rt, Nav, "tab:" + i, () => A.SelectTab((ArmoryTab)idx), f => tabTween.Set(f));
                if (on && enterTiles) UiFx.In(rt, UiAnims.WFade(.25f));
                x += widths[i] + 32f;
            }
            UiParts.Pills(Root, C.Save, 1560f, 26f);
        }

        // ---------------- weapons and helmets: the detail panel ----------------

        static Color TierColor(string tier)
        {
            switch (tier)
            {
                case "rare": return Blue;
                case "epic": return Violet;
                case "trophy": return Lit;
                case "boss": return Ui.Hot;
                case "legendary": return FlameOrange;
                case "common":
                case "starter": return SteelGray;
                default: return Ui.Gold;
            }
        }

        // a wrapped paragraph with a CSS-like line height; returns its height
        static float Para(Transform p, string text, UiFont font, int size, Color c, float x, float y, float w, float lineH, int maxLines = 0)
        {
            var t = Ui.Label(p, text, font, size, c, x, y, w, lineH, TextAnchor.UpperLeft, 0f, true, 1f);
            float ph = t.preferredHeight;
            int lines = Mathf.Max(1, t.cachedTextGeneratorForLayout.lineCount);
            float natural = ph / lines;
            t.lineSpacing = natural > 0.01f ? lineH / natural : 1f;
            if (maxLines > 0 && lines > maxLines)
            {
                t.verticalOverflow = VerticalWrapMode.Truncate;
                lines = maxLines;
            }
            t.rectTransform.sizeDelta = new Vector2(w, lines * lineH);
            return lines * lineH;
        }

        float Rarity(Transform p, float x, float y, ArmoryView v)
        {
            var c = TierColor(v.TierId);
            if (v.TierId == JsTables.Legendary.Id)
            {
                if (v.Tab == ArmoryTab.Helmets || v.Tab == ArmoryTab.Armor)
                {
                    var st = HelmetTierStyle.Legendary;
                    Ui.Diamond(p, x + 6f, y + 11f, 10f, st.Highlight, st.Secondary, 1.4f);
                    Ui.Label(p, v.TierLine.ToUpperInvariant(), UiFont.TitleHeavy, 17, st.Primary, x + 22f, y, 460f, 22f, TextAnchor.MiddleLeft, 4.4f);
                    return 22f;
                }
                var gem = Ui.Diamond(p, x + 6f, y + 11f, 11f, Color.white, Color.white, 0f);
                gem.gameObject.AddComponent<Rainbow>();
                var t = Ui.Label(p, v.TierLine.ToUpperInvariant(), UiFont.Title, 19, Color.white, x + 24f, y, 460f, 22f, TextAnchor.MiddleLeft, 5.2f);
                t.gameObject.AddComponent<Rainbow>();
                return 22f;
            }
            Ui.Diamond(p, x + 4.5f, y + 10f, 9f, c, c, 0f);
            Ui.Label(p, v.TierLine.ToUpperInvariant(), UiFont.Title, 14, c, x + 19f, y, 460f, 20f, TextAnchor.MiddleLeft, 4.0f);
            return 20f;
        }

        float NameRow(Transform p, float x, float y, ArmoryView v, int size, bool kanji)
        {
            var n = Ui.Label(p, v.Name, UiFont.Mincho, size, Ui.Bone, x, y, 460f, size, TextAnchor.MiddleLeft);
            float w = UiParts.Width(n);
            string k = v.Tab == ArmoryTab.Weapons && kanji ? ArmoryText.NameKanji(v.SelectedId) : null;
            if (k != null) Ui.Label(p, k, UiFont.Mincho, 22, Ui.Gold, x + w + 16f, y + size * 0.62f - 14f, 200f, 28f, TextAnchor.MiddleLeft);
            return size;
        }

        float Level(Transform p, float x, float y, float w, ArmoryView v)
        {
            Ui.Label(p, "LEVEL", UiFont.Title, 14, Ui.Muted, x, y, 120f, 22f, TextAnchor.MiddleLeft, 4.2f);
            if (v.Equipped) Ui.Label(p, "◆ EQUIPPED", UiFont.Title, 12, Ui.Gold, x + 94f, y, 200f, 22f, TextAnchor.MiddleLeft, 2.4f);
            Ui.Label(p, v.LevelText, UiFont.Title, 18, Ui.Bone, x, y, w, 22f, TextAnchor.MiddleRight);
            float sy = y + 22f + 10f;
            int n = Mathf.Max(1, v.MaxLevel);
            float sw = (w - 6f * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float sx = x + i * (sw + 6f);
                if (i < v.Level) Ui.Box(p, sx, sy, sw, 5f, Ui.Gold, "Seg");
                else if (i == v.Level && v.Owned) Ui.Frame(p, sx, sy, sw, 5f, 1f, Ui.Gold);
                else Ui.Box(p, sx, sy, sw, 5f, Ui.Hair, "Seg");
            }
            return 22f + 10f + 5f;
        }

        float Stats(Transform p, float x, float y, float w, ArmoryView v)
        {
            float cy = y;
            for (int i = 0; i < v.Stats.Length; i++)
            {
                var s = v.Stats[i];
                Ui.Label(p, s.Label.ToUpperInvariant(), UiFont.Title, 15, Ui.Bone, x, cy, 300f, 22f, TextAnchor.MiddleLeft, 3f);
                var val = Ui.Label(p, s.Value, UiFont.Title, 18, Ui.Bone, x, cy, w, 22f, TextAnchor.MiddleRight);
                if (!string.IsNullOrEmpty(s.Preview))
                {
                    float vw = UiParts.Width(val);
                    Ui.Label(p, s.Preview, UiFont.Title, 14, Ui.Gold, x, cy, w - vw - 8f, 22f, TextAnchor.MiddleRight);
                }
                float by = cy + 22f + 8f;
                Ui.Box(p, x, by, w, 4f, Ui.A(Ui.Bone, 0.12f), "Track");
                float fill = (float)s.Pct / 100f * w;
                if (fill > 0.5f) Ui.Grad(p, x, by, fill, 4f, Ui.Deep, Ui.Hot, true);
                if (s.PreviewPct > 0) Ui.Box(p, x + fill, by, (float)s.PreviewPct / 100f * w, 4f, Ui.Gold, "Preview");
                cy += 34f + 14f;
            }
            return v.Stats.Length * 34f + (v.Stats.Length - 1) * 14f;
        }

        // the gold perks / rage-art box and the red locked box
        float Box(Transform p, float x, float y, float w, ArmoryBox b, float pad, bool compact = false)
        {
            float icon = compact ? 36f : 40f, titleH = compact ? 18f : 22f, lineH = compact ? 19f : 21f, gap = compact ? 2f : 4f;
            var edge = b.Warn ? Ui.A(Ui.Acc, 0.45f) : Ui.A(Ui.Gold, 0.3f);
            float textW = w - 2f * pad - icon - 14f;
            float tx = x + pad + icon + 14f;
            // measure first, then draw behind
            var head = Ui.Label(p, b.Title, UiFont.Title, 15, b.Warn ? Ui.Hot : Ui.Gold, tx, y + pad, textW, titleH, TextAnchor.MiddleLeft, 1.2f);
            float bodyH = Para(p, b.Text, UiFont.Body, 14, Ui.Muted, tx, y + pad + titleH + gap, textW, lineH);
            float inner = Mathf.Max(icon, titleH + gap + bodyH);
            float h = pad * 2f + inner;
            var bg = Ui.Grad(p, x, y, w, h, b.Warn ? Ui.A(Ui.Acc, 0.14f) : Ui.A(Ui.Gold, 0.1f), Color.clear, true);
            bg.transform.SetAsFirstSibling();
            Ui.Frame(p, x, y, w, h, 1f, edge);
            var ic = Ui.Node(p, "Icon", x + pad, y + pad, icon, icon);
            Ui.Box(ic, 0, 0, icon, icon, b.Warn ? Ui.Acc : Ui.Gold);
            string glyph = b.Warn ? "鎖" : b.Title.StartsWith("RAGE") ? "怒" : "護";
            Ui.Label(ic, glyph, UiFont.Mincho, compact ? 20 : 22, b.Warn ? Ui.Bone : Ui.InkOnGold, 0, 0, icon, icon, TextAnchor.MiddleCenter);
            return h;
        }

        void Detail(ArmoryView v)
        {
            const float PanelX = 1020f, PanelY = 108f, PanelW = 540f, Max = 634f;
            var det = Ui.Node(Root, "Detail", PanelX, PanelY, PanelW, Max);
            if (enterDetail) UiFx.In(det, UiAnims.WFade(.3f));
            var bg = Ui.Box(det, 0, 0, PanelW, Max, Ui.PanelA(0.90f));
            Ui.Grad(det, 0, 0, PanelW, 110f, Ui.A(TierColor(v.TierId), 0.08f), Color.clear, false);
            var clip = Ui.Node(det, "Clip", 0, 0, PanelW, Max);
            clip.gameObject.AddComponent<RectMask2D>();
            bool multiBox = v.Boxes.Length > 1;
            float x = 32f, w = PanelW - 64f, y = 22f;
            y += Rarity(clip, x, y, v) + 8f;
            NameRow(clip, x, y, v, multiBox ? 44 : 50, true); y += (multiBox ? 44f : 50f) + 10f;
            if (!string.IsNullOrEmpty(v.Lore)) y += Para(clip, v.Lore, UiFont.Mincho, multiBox ? 14 : 15, Ui.Muted, x, y, w, multiBox ? 20f : 23f, multiBox ? 2 : 3) + 10f;
            if (!string.IsNullOrEmpty(v.Desc)) y += Para(clip, v.Desc, UiFont.Body, multiBox ? 15 : 16, Ui.Bone, x, y, w, multiBox ? 21f : 23f, multiBox ? 3 : 4) + 12f;
            y += Level(clip, x, y, w, v) + 12f;
            y += multiBox ? StatsCompact(clip, x, y, w, v) : Stats(clip, x, y, w, v);
            for (int i = 0; i < v.Boxes.Length; i++)
            {
                y += multiBox ? 10f : 14f;
                y += Box(clip, x, y, w, v.Boxes[i], multiBox ? 11f : 14f, multiBox);
            }
            y += 22f;
            float h = Mathf.Min(Max, y);
            det.sizeDelta = new Vector2(PanelW, h);
            bg.rectTransform.sizeDelta = new Vector2(PanelW, h);
            clip.sizeDelta = new Vector2(PanelW, h);
            Ui.Box(det, 0, 0, PanelW, 2f, TierColor(v.TierId));
            Ui.Frame(det, 0, 0, PanelW, h, 1.5f, Ui.A(Ui.GoldDim, 0.85f));
        }

        // ---------------- buttons ----------------

        void Actions(ArmoryView v, Transform p, float x, float y, float w)
        {
            float bw = (w - 14f) / 2f;
            Button(p, "primary", x, y, bw, v.Primary, v.Primary.Action == ArmoryAction.None ? ButtonStyle.Plain : ButtonStyle.Buy, () =>
            {
                if (v.Primary.Action == ArmoryAction.Buy) A.Buy();
                else if (v.Primary.Action == ArmoryAction.Upgrade) A.Upgrade();
            });
            Button(p, "equip", x + bw + 14f, y, bw, v.Equip, v.Equipped ? ButtonStyle.Gold : ButtonStyle.Equip, () => A.Equip());
        }

        enum ButtonStyle { Plain, Buy, Gold, Equip }

        void Button(Transform p, string key, float x, float y, float w, ArmoryButton b, ButtonStyle style, System.Action onSubmit)
        {
            const float H = 62f;
            var rt = Ui.Node(p, "Btn_" + key, x, y, w, H);
            bool buy = style == ButtonStyle.Buy;
            bool on = b.Enabled;
            Color text, edge;
            float edgeW = 1f;
            if (buy)
            {
                var g0 = on ? Ui.Deep : Ui.Mix(Ui.Deep, Ui.Charcoal, 0.45f);
                var g1 = on ? Ui.Acc : Ui.Mix(Ui.Acc, Ui.Charcoal, 0.45f);
                Ui.Grad(rt, 0, 0, w, H, g0, g1, true);
                text = on ? Ui.Bone : Ui.A(Ui.Bone, 0.55f);
                edge = on ? Ui.Hot : Ui.Mix(Ui.Hot, Ui.Charcoal, 0.4f);
                edgeW = 1.5f;
            }
            else if (style == ButtonStyle.Gold || style == ButtonStyle.Equip)
            {
                Ui.Box(rt, 0, 0, w, H, style == ButtonStyle.Gold ? Ui.A(Ui.Gold, 0.12f) : Ui.A(Ui.Gold, on ? 0.04f : 0f));
                text = on || style == ButtonStyle.Gold ? Ui.Gold : Ui.A(Ui.Gold, 0.55f);
                edge = on || style == ButtonStyle.Gold ? Ui.Gold : Ui.A(Ui.Gold, 0.45f);
                edgeW = 1.5f;
            }
            else
            {
                Ui.Box(rt, 0, 0, w, H, Ui.PanelA(0.8f));
                text = Ui.Muted;
                edge = Ui.Hair;
            }
            var hl = Ui.Box(rt, 0, 0, w, H, Color.clear, "Light");
            Ui.Frame(rt, 0, 0, w, H, edgeW, edge);
            var focusRing = Ui.Frame2(rt, w, H);

            string label = b.Label.ToUpperInvariant();
            float tracking = buy ? 3f : style == ButtonStyle.Equip ? 4.4f : 5.4f;
            var lb = Ui.Label(rt, label, UiFont.Title, 18, text, 0, 0, w, H, TextAnchor.MiddleLeft, tracking);
            float lw = UiParts.Width(lb, tracking);
            // the price: a coin or a jade and its number
            var price = new List<KeyValuePair<bool, int>>();
            if (buy && b.Coins > 0) price.Add(new KeyValuePair<bool, int>(true, b.Coins));
            if (buy && b.Gems > 0) price.Add(new KeyValuePair<bool, int>(false, b.Gems));
            if (b.Action == ArmoryAction.Upgrade && b.Coins > 0 && price.Count == 0) price.Add(new KeyValuePair<bool, int>(true, b.Coins));
            float group = lw + (style == ButtonStyle.Gold ? 18f : 0f);
            var numLabels = new List<Text>();
            foreach (var pr in price)
            {
                var n = Ui.Label(rt, UiParts.Num(pr.Value), UiFont.Title, 18, text, 0, 0, 160f, H, TextAnchor.MiddleLeft, 1f);
                numLabels.Add(n);
                group += 10f + 18f + 6f + UiParts.Width(n, 1f);
            }
            float cx = (w - group) / 2f;
            if (style == ButtonStyle.Gold)
            {
                Ui.Diamond(rt, cx + 4f, H / 2f, 8f, Ui.Gold, Ui.Gold, 0f);
                cx += 18f;
            }
            lb.rectTransform.anchoredPosition = new Vector2(cx, 0);
            cx += lw;
            for (int i = 0; i < price.Count; i++)
            {
                cx += 10f;
                // the coin and the jade are drawn in the button's text colour (JS currentColor)
                if (price[i].Key)
                {
                    Ui.Disc(rt, cx + 9f, H / 2f, 8f, text);
                    Ui.Disc(rt, cx + 9f, H / 2f, 4.6f, Ui.Mix(Ui.Deep, Ui.Acc, 0.5f));
                    Ui.Disc(rt, cx + 9f, H / 2f, 2.4f, text);
                }
                else Ui.Diamond(rt, cx + 9f, H / 2f, 10f, text, text, 0f);
                cx += 18f + 6f;
                numLabels[i].rectTransform.anchoredPosition = new Vector2(cx, 0);
                cx += UiParts.Width(numLabels[i], 1f);
            }
            var item = UiParts.Make(rt, Nav, key, onSubmit, f =>
            {
                rt.anchoredPosition = new Vector2(x, -(y - (f ? 2f : 0f)));
                hl.color = f ? Ui.A(Ui.Gold, 0.08f) : Color.clear;
                focusRing.color = f ? Ui.Gold : Color.clear;
            }, b.Enabled);
            item.press = 0.97f;
        }

        // ---------------- weapons and helmets: the row of tiles ----------------

        void Tiles(ArmoryView v, SaveData save)
        {
            tileIndex = 0;
            int n = v.Tiles.Length;
            var viewport = Ui.Node(Root, "Tiles", GridX, GridY - 6f, GridW, GridH + 6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            float contentW = n * (TileW + TileGap) - TileGap;
            tileContent = Ui.Node(viewport, "Content", 0, 0, contentW, GridH + 6f);
            tileMax = Mathf.Max(0f, contentW - GridW);
            scroll = Mathf.Clamp(scroll, 0f, tileMax);
            for (int i = 0; i < n; i++)
                if (v.Tiles[i].Selected) KeepVisible(i * (TileW + TileGap));
            tileContent.anchoredPosition = new Vector2(-scroll, 0);
            for (int i = 0; i < n; i++) Tile(v, v.Tiles[i], i * (TileW + TileGap));
            if (tileMax > 2f)
            {
                if (scroll > 2f) ScrollIndicator(Root, GridX - 24f, GridY + 40f, false);
                if (scroll < tileMax - 2f) ScrollIndicator(Root, GridX + GridW + 4f, GridY + 40f, true);
            }
        }

        static void ScrollIndicator(Transform p, float x, float y, bool right)
        {
            var rt = Ui.Node(p, right ? "ScrollHintR" : "ScrollHintL", x, y, 20f, 32f);
            float s = right ? -1f : 1f;
            var chev = Ui.Line(rt, new[] { new Vector2(10f + 4.5f * s, 8f), new Vector2(10f - 4.5f * s, 16f), new Vector2(10f + 4.5f * s, 24f) }, 2f, Ui.A(Ui.Gold, 0.85f));
            chev.rectTransform.sizeDelta = new Vector2(20f, 32f);
        }

        void Tile(ArmoryView v, ArmoryTile t, float x)
        {
            const float Y = 6f, H = 110f;
            var rt = Ui.Node(tileContent, "Tile_" + t.Id, x, Y, TileW, H);
            rt.gameObject.AddComponent<RectMask2D>();
            if (enterTiles) UiFx.In(rt, UiAnims.WRise(.45f, tileIndex * 0.025f));
            tileIndex++;
            Ui.Box(rt, 0, 0, TileW, H, Ui.PanelA(0.92f));
            if (t.Selected)
            {
                Ui.Grad(rt, 0, 0, TileW, H, Ui.A(Ui.Acc, 0.24f), Ui.PanelA(0.94f), false);
                Ui.Box(rt, 0, 0, TileW, 2.5f, Ui.Gold);
                Ui.Frame(rt, 0, 0, TileW, H, 1.5f, Ui.Gold);
            }
            else Ui.Frame(rt, 0, 0, TileW, H, 1f, Ui.A(Ui.GoldDim, 0.8f));
            // hover and focus only highlight: a faint gold wash and the lift; the selection changes when the tile is pressed
            var wash = Ui.Box(rt, 0, 0, TileW, H, Color.clear, "Focus");

            // the picture: the item drawn the way the JS draws it (the sword, the helmet), dim when it is not owned (JS .locked .art)
            // the JS drawings of helmets and armour are dark on a dark tile, so a locked one is dimmed less than a weapon render
            bool drawn = t.Art.Source != ArtSource.WeaponRender;
            var tint = t.Owned ? Color.white : drawn ? new Color(0.8f, 0.8f, 0.8f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.45f);
            var picture = t.Art.Texture;
            if (picture != null)
            {
                bool render = t.Art.Source == ArtSource.WeaponRender;
                float aw = render ? 96f : 100f, ah = 58f;
                var pic = Ui.Pic(rt, t.Art.Key, (TileW - aw) / 2f, 10f, aw, ah, tint);
                FitPic(pic, aw, ah);
            }
            if (t.Equipped) Ui.Diamond(rt, 14f, 14f, 8f, Ui.Gold, Ui.Gold, 0f);
            if (!t.Owned) PadLock(rt, TileW - 8f - 14f, 8f, Ui.Muted);

            // the name, the price or the lock label
            if (t.Owned)
            {
                Ui.Label(rt, Ellipsis(t.Name.ToUpperInvariant(), 14, 0.84f, 120f), UiFont.Title, 14, t.Selected ? Ui.Gold : Ui.Bone, 0, H - 10f - 20f, TileW, 20f, TextAnchor.MiddleCenter, 0.84f);
            }
            else if (t.LockLabel != null)
            {
                var lockText = Ui.Label(rt, t.LockLabel.ToUpperInvariant(), UiFont.Title, 11, Ui.Hot, 0, H - 10f - 18f, TileW, 18f, TextAnchor.MiddleCenter, 1.6f);
                if (t.LockLabel == JsTables.Legendary.Label) lockText.gameObject.AddComponent<Rainbow>();
            }
            else
            {
                bool gem = t.Gems > 0;
                string num = gem ? t.Gems.ToString() : UiParts.Num(t.Coins);
                var nl = Ui.Label(rt, num, UiFont.Title, 13, Ui.Muted, 0, H - 10f - 18f, 120f, 18f, TextAnchor.MiddleLeft);
                float w = 14f + 6f + UiParts.Width(nl);
                float sx = (TileW - w) / 2f;
                if (gem) Ui.Diamond(rt, sx + 7f, H - 10f - 9f, 9f, Ui.Ruby, Ui.Ruby, 0f);
                else
                {
                    Ui.Disc(rt, sx + 7f, H - 10f - 9f, 6.5f, Ui.Gold);
                    Ui.Disc(rt, sx + 7f, H - 10f - 9f, 3.8f, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                    Ui.Disc(rt, sx + 7f, H - 10f - 9f, 2f, Ui.Gold);
                }
                nl.rectTransform.anchoredPosition = new Vector2(sx + 20f, -(H - 10f - 18f));
            }

            string id = t.Id;
            float tx = x;
            var lift = UiFocusTween.On(rt, .16f, k =>
            {
                UiOffsets.Of(rt).SetFocus(new Vector2(0f, 3f * k));
                wash.color = Ui.A(Ui.Gold, 0.12f * k);
            });
            var item = UiParts.Make(rt, Nav, "tile:" + id, null, f =>
            {
                lift.Set(f);
                if (f) KeepVisible(tx);
            });
            ArmoryTilePick.Bind(item, id, A.Select);
        }

        void KeepVisible(float tileX)
        {
            if (tileContent == null) return;
            float lo = tileX - 24f, hi = tileX + TileW + 24f;
            if (lo < scroll) scroll = Mathf.Clamp(lo, 0f, tileMax);
            else if (hi > scroll + GridW) scroll = Mathf.Clamp(hi - GridW, 0f, tileMax);
            tileContent.anchoredPosition = new Vector2(-scroll, 0);
        }

        static void FitPic(RawImage pic, float w, float h)
        {
            var t = pic.texture;
            if (t == null) return;
            float k = Mathf.Min(w / t.width, h / t.height);
            float iw = t.width * k, ih = t.height * k;
            pic.rectTransform.anchoredPosition += new Vector2((w - iw) / 2f, -(h - ih) / 2f);
            pic.rectTransform.sizeDelta = new Vector2(iw, ih);
        }

        // the text cut with an ellipsis to fit a width (CSS text-overflow)
        static string Ellipsis(string s, int size, float tracking, float maxW)
        {
            var probe = Ui.Label(null, s, UiFont.Title, size, Color.white, 0, 0, 600f, 24f, TextAnchor.MiddleLeft, tracking);
            string r = s;
            try
            {
                int guard = 40;
                while (r.Length > 1 && UiParts.Width(probe, tracking) > maxW && guard-- > 0)
                {
                    r = r.Substring(0, r.Length - 1);
                    probe.text = r.TrimEnd() + "…";
                }
                return r == s ? s : r.TrimEnd() + "…";
            }
            finally { Object.DestroyImmediate(probe.gameObject); }
        }

        static void PadLock(Transform p, float x, float y, Color c)
        {
            var rt = Ui.Node(p, "Lock", x, y, 14f, 14f);
            Ui.Box(rt, 2.5f, 6.5f, 9f, 7f, c);
            Ui.Line(rt, new[] { new Vector2(4.5f, 6.5f), new Vector2(4.5f, 4f), new Vector2(5.5f, 2.5f), new Vector2(8.5f, 2.5f), new Vector2(9.5f, 4f), new Vector2(9.5f, 6.5f) }, 1.4f, c);
        }

        // ---------------- the Helmets tab: 5-tier rarity gallery + live Kage preview + inspector ----------------

        void HelmetBody(ArmoryView v, SaveData save)
        {
            // Cohesive warm obsidian backdrop matching the rest of KAGE, framing the top-right live Kage preview window (x 1030..1560, y 106..312)
            var leftBg = Ui.Node(Root, "HelmetGalleryBg", 0, 96, 1024, 804);
            Ui.Grad(leftBg, 0, 0, 1024, 804, Ui.Sky2, Ui.Sky3, false);
            Ui.Box(Root, 1024, 96, 6, 804, Ui.Sky3, "MaskSplit");
            Ui.Box(Root, 1560, 96, 40, 804, Ui.Sky3, "MaskRight");
            Ui.Box(Root, 1024, 96, 576, 10, Ui.Sky3, "MaskTop");
            Ui.Box(Root, 1024, 312, 576, 588, Ui.Sky3, "MaskBelow");
            Ui.Box(Root, 1023, 96, 1, 804, Ui.A(Ui.GoldDim, 0.25f), "SplitRule");

            // Crisp frame around the transparent live Kage showcase window
            var selStyle = HelmetTierStyle.For(v.TierId);
            var previewFrame = Ui.Node(Root, "LivePreviewFrame", 1030, 106, 530, 206);
            Ui.Frame(previewFrame, 0, 0, 530, 206, 1.5f, selStyle.Primary);
            Ui.Frame(previewFrame, 4, 4, 522, 198, 1f, Ui.A(selStyle.Highlight, 0.18f));
            var tag = Ui.Node(previewFrame, "LiveTag", 12, 10, 108, 22);
            Ui.Box(tag, 0, 0, 108, 22, Ui.PanelA(0.86f));
            Ui.Frame(tag, 0, 0, 108, 22, 1f, Ui.A(selStyle.Primary, 0.55f));
            Ui.Label(tag, "LIVE PREVIEW", UiFont.Cond, 11, selStyle.Primary, 0, 0, 108, 22, TextAnchor.MiddleCenter, 2.0f);
            Ui.Label(previewFrame, v.Kanji, UiFont.Brush, 36, Ui.A(Ui.Bone, 0.16f), 472, 8, 46, 46, TextAnchor.MiddleCenter);

            // Overlay for one-shot selection particle bursts (sits above cards so bursts never clip)
            var gallery = Ui.Node(Root, "HelmetGallery", 0, 0, 1024, 900);
            var burstLayer = Ui.Node(Root, "HelmetBurstLayer", 0, 0, 1024, 900);

            var byTier = new Dictionary<string, List<ArmoryTile>>();
            foreach (var st in HelmetTierStyle.All) byTier[st.Id] = new List<ArmoryTile>();
            foreach (var t in v.Tiles)
            {
                string tid = HelmetTierStyle.For(t.TierId).Id;
                byTier[tid].Add(t);
            }

            tileIndex = 0;
            const float CardSz = 132f, CardGap = 16f, LegSz = 144f, LegGap = 26f;

            // Row 1: COMMON (2) + RARE (3), centered across x = 24..1000
            var common = byTier["common"];
            var rare = byTier["rare"];
            float wCommon = GroupWidth(common.Count, CardSz, CardGap);
            float wRare = GroupWidth(rare.Count, CardSz, CardGap);
            float row1Gap = 52f;
            float row1X = 24f + (976f - (wCommon + row1Gap + wRare)) * 0.5f;
            HelmetTierGroup(gallery, burstLayer, HelmetTierStyle.Common, common, row1X, 106f, 142f, CardSz, CardGap);
            HelmetTierGroup(gallery, burstLayer, HelmetTierStyle.Rare, rare, row1X + wCommon + row1Gap, 106f, 142f, CardSz, CardGap);

            // Row 2: EPIC (3) + TROPHY (3), centered across x = 24..1000
            var epic = byTier["epic"];
            var trophy = byTier["trophy"];
            float wEpic = GroupWidth(epic.Count, CardSz, CardGap);
            float wTrophy = GroupWidth(trophy.Count, CardSz, CardGap);
            float row2Gap = 40f;
            float row2X = 24f + (976f - (wEpic + row2Gap + wTrophy)) * 0.5f;
            HelmetTierGroup(gallery, burstLayer, HelmetTierStyle.Epic, epic, row2X, 342f, 382f, CardSz, CardGap);
            HelmetTierGroup(gallery, burstLayer, HelmetTierStyle.Trophy, trophy, row2X + wEpic + row2Gap, 342f, 382f, CardSz, CardGap);

            // Row 3: LEGENDARY (3), slightly larger ornate notched cards centered across x = 24..1000
            var legendary = byTier["legendary"];
            float wLeg = GroupWidth(legendary.Count, LegSz, LegGap);
            float row3X = 24f + (976f - wLeg) * 0.5f;
            HelmetTierGroup(gallery, burstLayer, HelmetTierStyle.Legendary, legendary, row3X, 586f, 632f, LegSz, LegGap);

            HelmetDetail(v);
            Actions(v, Root, 1030, 804, 530);
        }

        static float GroupWidth(int count, float size, float gap)
        {
            return count <= 0 ? 0f : count * size + (count - 1) * gap;
        }

        void HelmetTierGroup(Transform gallery, Transform burstLayer, HelmetTierStyle style, List<ArmoryTile> items,
            float startX, float headerY, float cardY, float cardSize, float gap)
        {
            if (items.Count == 0) return;
            float groupW = GroupWidth(items.Count, cardSize, gap);
            float centerX = startX + groupW * 0.5f;

            // Crisp structured tier header flanked by subtle hairlines (zero radial blur smudge)
            int fontSize = style.Tier == HelmetTier.Legendary ? 20 : 18;
            var heading = Ui.Label(gallery, style.Label, UiFont.TitleHeavy, fontSize,
                style.Primary, startX - 10f, headerY, groupW + 20f, 26f, TextAnchor.MiddleCenter, 3.2f);
            float textW = UiParts.Width(heading, 3.2f) + 20f;
            float ruleW = Mathf.Max(14f, (groupW - textW) * 0.5f - 4f);
            if (ruleW > 14f)
            {
                Ui.Grad(gallery, startX, headerY + 12.5f, ruleW, 1f, Color.clear, Ui.A(style.Primary, 0.55f), true);
                Ui.Grad(gallery, centerX + textW * 0.5f + 4f, headerY + 12.5f, ruleW, 1f, Ui.A(style.Primary, 0.55f), Color.clear, true);
            }
            var hShadow = heading.gameObject.AddComponent<Shadow>();
            hShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            hShadow.effectDistance = new Vector2(0f, -1.5f);

            for (int i = 0; i < items.Count; i++)
                HelmetCard(gallery, burstLayer, style, items[i], startX + i * (cardSize + gap), cardY, cardSize);
        }

        void HelmetCard(Transform gallery, Transform burstLayer, HelmetTierStyle style, ArmoryTile t, float x, float y, float size)
        {
            int idx = tileIndex++;
            // Centered pivot so smooth hover scaling (scale(1.04)) expands evenly around the card center
            var rt = Ui.Node(gallery, "Tile_" + t.Id, x, y, size, size);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + size * 0.5f, -(y + size * 0.5f));
            if (enterTiles) UiFx.In(rt, UiAnims.WRise(.42f, idx * 0.025f));

            // 1. Contained card interior background & subtle clipped studio lighting (no blurry outer glow stack)
            var bgClip = Ui.Node(rt, "BgClip", 2f, 2f, size - 4f, size - 4f);
            bgClip.gameObject.AddComponent<RectMask2D>();
            float iw = size - 4f;
            if (style.HasNotchedFrame)
                Ui.Shape(bgClip, 0f, 0f, iw, iw, HelmetTierStyle.NotchedFramePoints(0f, 0f, iw, iw, 0f, 10f), style.BgTop, style.BgBottom, null, false, "CardFill");
            else
                Ui.Grad(bgClip, 0f, 0f, iw, iw, style.BgTop, style.BgBottom, false);

            if (style.HasRadialBackGlow)
            {
                float backAlpha = style.Tier == HelmetTier.Legendary ? 0.24f : style.Tier == HelmetTier.Trophy ? 0.20f : 0.16f;
                Ui.Glow(bgClip, iw * 0.5f, iw * 0.48f, iw * 0.46f, Ui.A(style.Primary, backAlpha), 1.6f);
            }
            else if (style.Tier == HelmetTier.Rare)
            {
                Ui.Glow(bgClip, iw * 0.5f, iw * 0.46f, iw * 0.44f, Ui.A(style.Primary, 0.12f), 1.6f);
            }

            // 2. Restrained crisp ambient embers inside Epic, Trophy & Legendary cards (zero crackle bolts)
            HelmetCardAmbient.Attach(rt, style, size, size, idx + 1);

            // 3. Crisp mitered border frame (drawn under top-breakout portraits so horns/antlers clear the top edge)
            if (style.HasNotchedFrame)
            {
                var outerPts = HelmetTierStyle.NotchedFramePoints(0f, 0f, size, size, 0f, 11f);
                var innerPts = HelmetTierStyle.NotchedFramePoints(4.5f, 4.5f, size - 9f, size - 9f, 0f, 8f);
                Ui.Stroke(rt, 0f, 0f, size, size, outerPts, 2.2f, style.Primary, -2.2f, "OuterFrame");
                Ui.Stroke(rt, 0f, 0f, size, size, innerPts, 1.0f, Ui.A(style.Highlight, 0.45f), -1.0f, "InnerFrame");
                foreach (var pt in new[] { new Vector2(9f, 9f), new Vector2(size - 9f, 9f), new Vector2(9f, size - 9f), new Vector2(size - 9f, size - 9f) })
                    Ui.Diamond(rt, pt.x, pt.y, 5f, style.Highlight, style.Primary, 0.8f);
            }
            else
            {
                Ui.Frame(rt, 0f, 0f, size, size, 2f, style.Primary);
                Ui.Frame(rt, 4f, 4f, size - 8f, size - 8f, 1f, style.InnerBorder);
                if (style.Tier == HelmetTier.Trophy)
                {
                    foreach (var pt in new[] { new Vector2(9f, 9f), new Vector2(size - 9f, 9f), new Vector2(9f, size - 9f), new Vector2(size - 9f, size - 9f) })
                        Ui.Diamond(rt, pt.x, pt.y, 4.5f, style.Highlight, style.Primary, 0.6f);
                }
            }

            // 4. Character silhouette + helmet portrait (clipped cleanly inside bottom/sides; Trophy antlers & Legendary horns break over top)
            float topExtra = style.AllowTopBreakout ? 24f : 0f;
            float inset = style.AllowTopBreakout ? 5f : 5.5f;
            var portClip = Ui.Node(rt, "PortraitClip", inset, inset - topExtra, size - inset * 2f, size - inset * 2f + topExtra);
            portClip.gameObject.AddComponent<RectMask2D>();
            var picture = t.Art.Texture;
            if (picture != null)
            {
                Color tint = t.Owned ? Color.white : new Color(0.76f, 0.76f, 0.80f, 0.88f);
                float pw = size - inset * 2f;
                float ph = style.AllowTopBreakout ? size + 16f : size - inset * 2f;
                float py = style.AllowTopBreakout ? (topExtra - 18f) : 0f;
                var pic = Ui.Pic(portClip, t.Art.Key, 0f, py, pw, ph, tint);
                FitPic(pic, pw, ph);
            }

            // 5. Crisp mitered selection highlight ring
            Graphic selectHighlight;
            if (style.HasNotchedFrame)
            {
                var ring = Ui.Ring(rt, size, size, HelmetTierStyle.NotchedFramePoints(0f, 0f, size, size, 0f, 11f), 2f, 2f);
                ring.color = t.Selected ? style.Highlight : Color.clear;
                selectHighlight = ring;
            }
            else
            {
                var ring = Ui.Frame2(rt, size, size);
                ring.color = t.Selected ? style.Highlight : Color.clear;
                selectHighlight = ring;
            }
            HelmetCardPulse legendPulse = style.Tier == HelmetTier.Legendary
                ? HelmetCardPulse.Attach(selectHighlight, style.Highlight, 0.92f, t.Selected)
                : null;

            // Hover wash
            var wash = Ui.Box(rt, 3f, 3f, size - 6f, size - 6f, Color.clear, "HoverWash");

            // 6. Distinct "EQUIPPED" badge/border state
            if (t.Equipped)
            {
                if (!style.HasNotchedFrame) Ui.Frame(rt, -1.5f, -1.5f, size + 3f, size + 3f, 1.5f, Ui.Hex("#FDE047"));
                var badge = Ui.Node(rt, "EquippedBadge", 5f, 5f, 76f, 18f);
                Ui.Box(badge, 0f, 0f, 76f, 18f, Ui.PanelA(0.94f));
                Ui.Frame(badge, 0f, 0f, 76f, 18f, 1f, Ui.Hex("#EAB308"));
                Ui.Label(badge, "◆ EQUIPPED", UiFont.Cond, 10, Ui.Hex("#FDE047"), 0f, 0f, 76f, 18f, TextAnchor.MiddleCenter, 0.9f);
            }

            // 7. Compact price / lock tag in top-right when not owned
            if (!t.Owned)
            {
                if (t.LockLabel != null)
                {
                    var lk = Ui.Node(rt, "LockPill", size - 24f, 6f, 18f, 18f);
                    Ui.Box(lk, 0f, 0f, 18f, 18f, Ui.PanelA(0.90f));
                    Ui.Frame(lk, 0f, 0f, 18f, 18f, 1f, Ui.A(style.Primary, 0.65f));
                    PadLock(lk, 2f, 2f, style.Primary);
                }
                else
                {
                    bool gem = t.Gems > 0;
                    string num = gem ? t.Gems.ToString() : UiParts.Num(t.Coins);
                    var pill = Ui.Node(rt, "PricePill", size - 56f, 6f, 50f, 18f);
                    Ui.Box(pill, 0f, 0f, 50f, 18f, Ui.PanelA(0.92f));
                    Ui.Frame(pill, 0f, 0f, 50f, 18f, 1f, Ui.A(style.Primary, 0.55f));
                    if (gem) Ui.Diamond(pill, 10f, 9f, 7f, Ui.Ruby, Ui.Ruby, 0f);
                    else Ui.Disc(pill, 10f, 9f, 4.5f, Ui.Hex("#EAB308"));
                    Ui.Label(pill, num, UiFont.Cond, 11, LabelLight, 17f, 0f, 31f, 18f, TextAnchor.MiddleLeft, 0.4f);
                }
            }

            // 9. Centered helmet name below the card in bold serif (#F3F4F6), NEVER truncated with "..."
            var nameLbl = Ui.Label(gallery, t.Name.ToUpperInvariant(), UiFont.Title, style.Tier == HelmetTier.Legendary ? 13 : 12,
                t.Selected ? style.Highlight : LabelLight, x - 9f, y + size + 8f, size + 18f, 22f, TextAnchor.MiddleCenter, 0.45f);
            var nShadow = nameLbl.gameObject.AddComponent<Shadow>();
            nShadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            nShadow.effectDistance = new Vector2(0f, -1.5f);

            // Spawn one-shot selection particle burst if this card was just clicked
            if (pendingBurstId == t.Id)
            {
                pendingBurstId = null;
                HelmetSelectBurst.Spawn(burstLayer, new Vector2(x + size * 0.5f, y + size * 0.5f), style);
            }

            // 10. Smooth hover scaling (scale(1.05)) + click-to-select with sound & burst
            string id = t.Id;
            bool isSelected = t.Selected;
            var hoverTween = UiFocusTween.On(rt, 0.14f, k =>
            {
                rt.localScale = Vector3.one * (1f + 0.05f * k);
                wash.color = Ui.A(style.Primary, 0.10f * k);
                if (legendPulse == null)
                    selectHighlight.color = isSelected ? style.Highlight : Ui.A(style.Highlight, 0.72f * k);
            });
            var item = UiParts.Make(rt, Nav, "tile:" + id, null, f =>
            {
                hoverTween.Set(f);
                if (legendPulse != null) legendPulse.SetActive(isSelected || f);
            });
            ArmoryTilePick.Bind(item, id, pickedId =>
            {
                pendingBurstId = pickedId;
                HelmetSelectSfx.Play(style.Tier);
                A.Select(pickedId);
            });
        }

        void HelmetDetail(ArmoryView v)
        {
            const float PanelX = 1030f, PanelY = 324f, PanelW = 530f, PanelH = 468f;
            var style = HelmetTierStyle.For(v.TierId);
            var det = Ui.Node(Root, "Detail", PanelX, PanelY, PanelW, PanelH);
            if (enterDetail) UiFx.In(det, UiAnims.WFade(.3f));
            Ui.Box(det, 0, 0, PanelW, PanelH, Ui.PanelA(0.94f));
            Ui.Grad(det, 0, 0, PanelW, 110f, Ui.A(style.Primary, style.GlowAlpha > 0f ? 0.10f : 0.05f), Color.clear, false);
            var clip = Ui.Node(det, "Clip", 0, 0, PanelW, PanelH);
            clip.gameObject.AddComponent<RectMask2D>();
            float x = 28f, w = PanelW - 56f, y = 20f;
            y += Rarity(clip, x, y, v) + 6f;
            NameRow(clip, x, y, v, 36, false); y += 38f + 8f;
            if (!string.IsNullOrEmpty(v.Lore)) y += Para(clip, v.Lore, UiFont.Mincho, 13, Ui.Muted, x, y, w, 19.5f, 3) + 10f;
            y += Level(clip, x, y, w, v) + 12f;
            y += StatsCompact(clip, x, y, w, v);
            for (int i = 0; i < v.Boxes.Length; i++)
            {
                y += 10f;
                y += Box(clip, x, y, w, v.Boxes[i], 10f, true);
            }
            Ui.Box(det, 0, 0, PanelW, 2f, style.Primary);
            Ui.Frame(det, 0, 0, PanelW, PanelH, 1.5f, Ui.A(style.Primary, 0.72f));
        }

        float StatsCompact(Transform p, float x, float y, float w, ArmoryView v)
        {
            float cy = y;
            for (int i = 0; i < v.Stats.Length; i++)
            {
                var s = v.Stats[i];
                Ui.Label(p, s.Label.ToUpperInvariant(), UiFont.Title, 13, Ui.Bone, x, cy, 260f, 19f, TextAnchor.MiddleLeft, 2.4f);
                var val = Ui.Label(p, s.Value, UiFont.Title, 16, Ui.Bone, x, cy, w, 19f, TextAnchor.MiddleRight);
                if (!string.IsNullOrEmpty(s.Preview))
                {
                    float vw = UiParts.Width(val);
                    Ui.Label(p, s.Preview, UiFont.Title, 13, Ui.Gold, x, cy, w - vw - 8f, 19f, TextAnchor.MiddleRight);
                }
                float by = cy + 19f + 6f;
                Ui.Box(p, x, by, w, 4f, Ui.A(Ui.Bone, 0.12f), "Track");
                float fill = (float)s.Pct / 100f * w;
                if (fill > 0.5f) Ui.Grad(p, x, by, fill, 4f, Ui.Deep, Ui.Hot, true);
                if (s.PreviewPct > 0) Ui.Box(p, x + fill, by, (float)s.PreviewPct / 100f * w, 4f, Ui.Gold, "Preview");
                cy += 29f + 10f;
            }
            return v.Stats.Length * 29f + (v.Stats.Length - 1) * 10f;
        }

        // Backdrop behind the live Kage on the Helmets tab: obsidian crimson matching the rest of the Armory
        static void HelmetBackdrop(Transform p, string tierId)
        {
            var st = HelmetTierStyle.For(tierId);
            Ui.Grad(p, 0, 0, 1600, 900, Ui.Sky2, Ui.Sky3, false);
            // Focused crimson sun right behind the top-right Live Preview window (center 1295, 210)
            Ui.Glow(p, 1295, 212, 200, Ui.A(Ui.Acc, 0.34f), 1.35f);
            Ui.Disc(p, 1295, 212, 114, Ui.Mix(Ui.Deep, Ui.Sun2, 0.55f));
            Ui.Glow(p, 1272, 186, 80, Ui.A(Ui.Sun1, 0.65f), 1.4f);
            UiFx.Loop(Ui.Line(p, Ui.Circle(1295, 212, 130, 60).Close(), 1.2f, Ui.A(st.Primary, 0.38f)).transform, UiAnims.WGlow(4f));
        }

        // ---------------- the Armor tab ----------------

        // JS .al-row: cards 177 wide, 14 apart, from x = 42; the row (x 40 to 1560) scrolls when there are more than eight
        const float CardW = 177f, CardPitch = 191f, RowX = 40f, RowW = 1520f;

        static float CardX(int i) { return 42f + i * CardPitch; }

        /// <summary>True when armour card i lies inside the row's clipped window at the current scroll.</summary>
        bool CardInWindow(int i)
        {
            float left = CardX(i) - cardScroll, right = left + CardW;
            return left >= RowX - 2f && right <= RowX + RowW + 2f;
        }

        // returns true when the row scrolled
        bool KeepCardVisible(int i)
        {
            float before = cardScroll;
            float lo = CardX(i) - RowX - 4f, hi = CardX(i) - RowX + CardW + 4f;
            if (lo < cardScroll) cardScroll = Mathf.Clamp(lo, 0f, cardMax);
            else if (hi > cardScroll + RowW) cardScroll = Mathf.Clamp(hi - RowW, 0f, cardMax);
            return Mathf.Abs(cardScroll - before) > 0.01f;
        }

        int scrollRebuilds;
        RectTransform armorBurstLayer;

        void ArmorBody(ArmoryView v, SaveData save, RectTransform back)
        {
            // the cards, in a row that is clipped to its window and scrolls
            int n = v.Tiles.Length;
            cardMax = Mathf.Max(0f, CardX(n - 1) + CardW + 4f - RowX - RowW);
            cardScroll = Mathf.Clamp(cardScroll, 0f, cardMax);
            // the row follows the selection when it changes; a redraw for the focus scrolling the row must not pull it back to the selected card
            if (enterDetail)
                for (int i = 0; i < n; i++)
                    if (v.Tiles[i].Selected) KeepCardVisible(i);
            const float MaskPad = 5f;
            cardsFront = Ui.Node(Root, "Cards", RowX - MaskPad, 0, RowW + MaskPad * 2f, 900);
            cardsFront.gameObject.AddComponent<RectMask2D>();
            armorBurstLayer = Ui.Node(Root, "ArmorBurstLayer", RowX - MaskPad, 0, RowW + MaskPad * 2f, 900);
            cardsBack = null;
            if (back != null)
            {
                cardsBack = Ui.Node(back, "Cards", RowX - MaskPad, 0, RowW + MaskPad * 2f, 900);
                cardsBack.gameObject.AddComponent<RectMask2D>();
            }
            for (int i = 0; i < n; i++) Card(v, v.Tiles[i], i, CardX(i) - (RowX - MaskPad) - cardScroll, CardW);
            if (cardMax > 2f)
            {
                if (cardScroll > 2f) ScrollIndicator(Root, RowX - 24f, 348f, false);
                if (cardScroll < cardMax - 2f) ScrollIndicator(Root, RowX + RowW + 4f, 348f, true);
            }

            ArmorDetail(v);
        }

        static Color HueOf(string id)
        {
            var g = JsTables.Armor(id);
            if (g != null && !string.IsNullOrEmpty(g.Hue)) return Ui.Hex(g.Hue);
            return g != null && g.Tier == "trophy" ? Lit : Ui.Gold;
        }

        static Vector2[] Octagon(float x, float y, float w, float h, float c)
        {
            return Ui.Chamfer(x, y, w, h, c);
        }

        void Card(ArmoryView v, ArmoryTile t, int index, float x, float w)
        {
            const float Y = 154f, H = 420f;
            var style = HelmetTierStyle.For(t.TierId);
            var hue = HueOf(t.Id);
            bool leg = style.Tier == HelmetTier.Legendary;
            bool sel = t.Selected;
            float bevel = leg ? 11f : 9f;
            var outerPoly = Octagon(0f, 0f, w, H, bevel);
            var innerPoly = Octagon(5f, 5f, w - 10f, H - 10f, bevel - 2.5f);

            // 1. Behind the live Kage: tier-styled dark obsidian backdrop, clipped subtle studio spotlight & crisp ambient motes
            if (cardsBack != null)
            {
                var bg = Ui.Node(cardsBack, "Card_" + t.Id, x, Y, w, H);
                bg.gameObject.AddComponent<RectMask2D>();
                Ui.Shape(bg, 0, 0, w, H, outerPoly, Ui.A(style.BgTop, 0.94f), Ui.A(style.BgBottom, 0.98f), null, false);
                float gr = 0.50f * w;
                if (style.Tier == HelmetTier.Common)
                {
                    var glow = Ui.Glow(bg, 0, 0, gr, Ui.A(Ui.Acc, 0.16f), 1.6f);
                    glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    glow.rectTransform.anchoredPosition = new Vector2(w * 0.50f, -H * 0.42f);
                    glow.rectTransform.localScale = new Vector3(1f, 0.38f * H / gr, 1f);
                }
                else
                {
                    float ga = leg ? 0.30f : style.Tier == HelmetTier.Trophy ? 0.24f : style.Tier == HelmetTier.Epic ? 0.22f : 0.16f;
                    var glow = Ui.Glow(bg, 0, 0, gr, Ui.A(style.GlowColor, ga), 1.6f);
                    glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    glow.rectTransform.anchoredPosition = new Vector2(w * 0.50f, -H * 0.42f);
                    glow.rectTransform.localScale = new Vector3(1f, 0.40f * H / gr, 1f);
                }
                // Grounded floor tint band inside the card behind Kage's boots and footer
                Ui.Grad(bg, 2f, 444f - Y, w - 4f, H - (444f - Y) - 2f, Color.clear, Ui.PanelA(0.88f), false);
                HelmetCardAmbient.Attach(bg, style, w, H, index + 101);
            }

            // 2. Front card frame: crisp mitered PolyRing borders (zero corner cracks or stacked blurry glow halos)
            var rt = Ui.Node(cardsFront, "Card_" + t.Id, x, Y, w, H);
            if (enterTiles) UiFx.In(rt, UiAnims.WRise(.5f, index * 0.04f));

            var edge = sel ? style.Highlight : style.Primary;
            Ui.Stroke(rt, 0f, 0f, w, H, outerPoly, sel ? 2.2f : 1.8f, edge, sel ? -2.2f : -1.8f, "OuterBorder");
            Ui.Stroke(rt, 0f, 0f, w, H, innerPoly, 1.0f, sel ? Ui.A(style.Highlight, 0.55f) : style.InnerBorder, -1.0f, "InnerBorder");
            if (leg || style.Tier == HelmetTier.Trophy)
            {
                foreach (var pt in new[] { new Vector2(10f, 10f), new Vector2(w - 10f, 10f), new Vector2(10f, H - 10f), new Vector2(w - 10f, H - 10f) })
                    Ui.Diamond(rt, pt.x, pt.y, leg ? 5.2f : 4.4f, style.Highlight, style.Primary, 0.8f);
            }

            // 3. Top status tags (Equipped badge / Level / Price / Lock)
            if (t.Equipped)
            {
                var badge = Ui.Node(rt, "EquippedBadge", 10f, 10f, 80f, 20f);
                Ui.Box(badge, 0f, 0f, 80f, 20f, Ui.PanelA(0.94f));
                Ui.Frame(badge, 0f, 0f, 80f, 20f, 1f, Ui.Hex("#EAB308"));
                Ui.Label(badge, "◆ EQUIPPED", UiFont.Cond, 10, Ui.Hex("#FDE047"), 0f, 0f, 80f, 20f, TextAnchor.MiddleCenter, 0.9f);
            }
            else if (t.Owned)
            {
                var lvPill = Ui.Node(rt, "LvPill", w - 52f, 10f, 42f, 20f);
                Ui.Box(lvPill, 0f, 0f, 42f, 20f, Ui.PanelA(0.90f));
                Ui.Frame(lvPill, 0f, 0f, 42f, 20f, 1f, Ui.A(style.Primary, 0.65f));
                Ui.Label(lvPill, "Lv " + t.Level, UiFont.Cond, 11, LabelLight, 0f, 0f, 42f, 20f, TextAnchor.MiddleCenter, 0.6f);
            }
            else if (t.LockLabel != null)
            {
                var lk = Ui.Node(rt, "LockPill", w - 30f, 10f, 20f, 20f);
                Ui.Box(lk, 0f, 0f, 20f, 20f, Ui.PanelA(0.90f));
                Ui.Frame(lk, 0f, 0f, 20f, 20f, 1f, Ui.A(style.Primary, 0.65f));
                PadLock(lk, 3f, 3f, style.Primary);
            }
            else
            {
                // Structured dark price pill so costs stay legible over every armor backdrop
                float pillW = 10f;
                if (t.Gems > 0) pillW += 22f + t.Gems.ToString().Length * 8f;
                if (t.Coins > 0) pillW += 22f + UiParts.Num(t.Coins).Length * 7.5f;
                if (t.Gems > 0 && t.Coins > 0) pillW += 6f;
                pillW = Mathf.Clamp(pillW, 56f, w - 20f);
                var pill = Ui.Node(rt, "PricePill", w - 10f - pillW, 10f, pillW, 22f);
                Ui.Box(pill, 0f, 0f, pillW, 22f, Ui.PanelA(0.90f));
                Ui.Frame(pill, 0f, 0f, pillW, 22f, 1f, Ui.A(style.Primary, 0.55f));
                float rx = pillW - 7f;
                if (t.Gems > 0)
                {
                    var gl = Ui.Label(pill, t.Gems.ToString(), UiFont.Title, 11, LabelLight, 0, 1f, pillW, 20f, TextAnchor.MiddleLeft, 0.6f);
                    float gw = UiParts.Width(gl, 0.6f);
                    gl.rectTransform.anchoredPosition = new Vector2(rx - gw, -1f);
                    Ui.Diamond(pill, rx - gw - 9f, 11f, 7.5f, Ui.Ruby, Ui.Ruby, 0f);
                    rx -= gw + 19f;
                }
                if (t.Coins > 0)
                {
                    var cl = Ui.Label(pill, UiParts.Num(t.Coins), UiFont.Title, 11, LabelLight, 0, 1f, pillW, 20f, TextAnchor.MiddleLeft, 0.6f);
                    float cw = UiParts.Width(cl, 0.6f);
                    cl.rectTransform.anchoredPosition = new Vector2(rx - cw, -1f);
                    Ui.Disc(pill, rx - cw - 8.5f, 11f, 5.2f, Ui.Gold);
                    Ui.Disc(pill, rx - cw - 8.5f, 11f, 2.8f, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                    Ui.Disc(pill, rx - cw - 8.5f, 11f, 1.5f, Ui.Gold);
                }
            }

            // 4. Structured footer: crisp tier tag, divider, Armor name & refined diamond ornament
            float divY = 492f - Y;
            string tierTag = t.TierId == "starter" ? "STARTER" : style.Label;
            var tierLbl = Ui.Label(rt, tierTag, UiFont.TitleHeavy, leg ? 11 : 10, style.Primary, 0f, divY - 20f, w, 17f, TextAnchor.MiddleCenter, leg ? 2.4f : 2.0f);
            var tShadow = tierLbl.gameObject.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            tShadow.effectDistance = new Vector2(0f, -1.2f);

            Ui.Box(rt, 14f, divY, w - 28f, 1f, Ui.A(style.Primary, 0.55f));
            var nm = Ui.Label(rt, Ellipsis(t.Name.ToUpperInvariant(), 14, 0f, w - 16f), UiFont.Mincho, 14, sel ? style.Highlight : LabelLight, 0, 502f - Y, w, 26f, TextAnchor.MiddleCenter);
            var nShadow = nm.gameObject.AddComponent<Shadow>();
            nShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            nShadow.effectDistance = new Vector2(0f, -1.5f);

            float oy = 548f - Y;
            Color orn = style.Tier == HelmetTier.Common ? hue : style.Primary;
            Ui.Grad(rt, w / 2f - 58f, oy - 0.5f, 48f, 1f, Color.clear, Ui.A(orn, 0.85f), true);
            Ui.Grad(rt, w / 2f + 10f, oy - 0.5f, 48f, 1f, Ui.A(orn, 0.85f), Color.clear, true);
            Ui.Diamond(rt, w / 2f, oy, 11f, Color.clear, orn, 1.4f);
            Ui.Diamond(rt, w / 2f, oy, 4.5f, style.Highlight, style.Highlight, 0f);
            Ui.Diamond(rt, w / 2f - 17f, oy, 3.6f, orn, orn, 0f);
            Ui.Diamond(rt, w / 2f + 17f, oy, 3.6f, orn, orn, 0f);

            // 5. Crisp mitered outer selection ring + Legendary pulse
            var selectRing = Ui.Ring(rt, w, H, outerPoly, 2f, 2f);
            selectRing.color = sel ? style.Highlight : Color.clear;
            HelmetCardPulse legendPulse = leg ? HelmetCardPulse.Attach(selectRing, style.Highlight, 0.92f, sel) : null;

            if (pendingBurstId == t.Id && armorBurstLayer != null)
            {
                pendingBurstId = null;
                HelmetSelectBurst.Spawn(armorBurstLayer, new Vector2(x + w * 0.5f, Y + H * 0.5f), style);
            }

            string id = t.Id;
            int idx = index;
            var lift = UiFocusTween.On(rt, .18f, k =>
            {
                UiOffsets.Of(rt).SetFocus(new Vector2(0f, 4f * k));
                if (legendPulse == null)
                    selectRing.color = sel ? style.Highlight : Ui.A(style.Highlight, 0.72f * k);
            });
            var item = UiParts.Make(rt, Nav, "tile:" + id, null, f =>
            {
                lift.Set(f);
                if (legendPulse != null) legendPulse.SetActive(sel || f);
                // moving the focus along a row of more than eight cards scrolls it; the Kages in the cards follow when the screen is drawn
                // again (it used to happen because focusing selected, which redraws). One redraw at most per focus move.
                if (f && KeepCardVisible(idx) && scrollRebuilds == 0)
                {
                    scrollRebuilds++;
                    try { Rebuild(); }
                    finally { scrollRebuilds--; }
                }
            });
            ArmoryTilePick.Bind(item, id, pickedId =>
            {
                pendingBurstId = pickedId;
                HelmetSelectSfx.Play(style.Tier);
                A.Select(pickedId);
            });
        }

        void ArmorDetail(ArmoryView v)
        {
            var style = HelmetTierStyle.For(v.TierId);
            var tint = style.Tier == HelmetTier.Common ? HueOf(v.SelectedId) : style.Primary;
            Color frameCol = style.Tier == HelmetTier.Common ? Ui.A(Ui.GoldDim, 0.85f) : Ui.A(style.Primary, 0.78f);
            // the details under the row
            var det = Ui.Node(Root, "Detail", 40f, 600f, 1040f, 262f);
            if (enterDetail) UiFx.In(det, UiAnims.WFade(.3f));
            Ui.Box(det, 0, 0, 1040f, 262f, Ui.PanelA(0.92f));
            Ui.Grad(det, 0, 0, 440f, 262f, Ui.A(tint, style.Tier == HelmetTier.Legendary ? 0.12f : 0.08f), Color.clear, true);
            Ui.Box(det, 0, 0, 1040f, 2f, tint);
            Ui.Box(det, 484f, 24f, 1f, 214f, Ui.Hair);
            Ui.Frame(det, 0, 0, 1040f, 262f, 1.5f, frameCol);
            float x = 34f, y = 24f;
            y += Rarity(det, x, y, v) + 8f;
            NameRow(det, x, y, v, 42, false);
            y += 44f + 10f;
            if (!string.IsNullOrEmpty(v.Lore)) Para(det, v.Lore, UiFont.Mincho, 14, Ui.Muted, x, y, 426f, 21.7f, 4);
            // the stats, centred on the right
            float statsH = v.Stats.Length * 34f + (v.Stats.Length - 1) * 14f;
            Stats(det, 514f, (262f - statsH) / 2f, 492f, v);

            // Structured side panel: matching obsidian top card for Level + Perks above the action buttons
            var side = Ui.Node(Root, "Side", 1100f, 600f, 460f, 262f);
            var sideTop = Ui.Node(side, "SideCard", 0, 0, 460f, 188f);
            Ui.Box(sideTop, 0, 0, 460f, 188f, Ui.PanelA(0.92f));
            Ui.Box(sideTop, 0, 0, 460f, 2f, frameCol);
            Ui.Frame(sideTop, 0, 0, 460f, 188f, 1f, Ui.A(Ui.GoldDim, 0.7f));
            float sy = 18f;
            sy += Level(sideTop, 20f, sy, 420f, v) + 14f;
            foreach (var b in v.Boxes) sy += Box(sideTop, 20f, sy, 420f, b, 10f, true) + 10f;
            Actions(v, side, 0, 262f - 62f, 460f);
        }

        // ---------------- backdrops (drawn behind the fighters) ----------------

        static Color Ground(float k) { return Ui.Mix(Ui.Ground, Color.black, k); }

        static Vector2[] Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n = 16)
        {
            var r = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                r[i] = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
            }
            return r;
        }

        static Vector2[] Quad(Vector2 a, Vector2 b, Vector2 c, int n = 10)
        {
            var r = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                r[i] = u * u * a + 2 * u * t * b + t * t * c;
            }
            return r;
        }

        static Vector2[] Poly(params float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        static readonly float[] Leaf =
        {
            0, -10, 2, -4, 7, -7, 5, -1, 10, 0, 5, 2, 7, 7, 1, 4, 0.6f, 10, -0.6f, 10, -1, 4, -7, 7, -5, 2, -10, 0, -5, -1, -7, -7, -2, -4,
        };

        internal static void Leaves(Transform p, float[][] pts)
        {
            var c = Ui.A(Ui.Mix(Ui.Hot, Ui.Acc, 0.5f), 0.9f);
            int n = 0;
            foreach (var l in pts)
            {
                float s = l[2], r = l[3] * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
                var poly = new Vector2[Leaf.Length / 2];
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                for (int i = 0; i < poly.Length; i++)
                {
                    float px = Leaf[i * 2] * s, py = Leaf[i * 2 + 1] * s;
                    poly[i] = new Vector2(l[0] + px * cs - py * sn, l[1] + px * sn + py * cs);
                    x0 = Mathf.Min(x0, poly[i].x); y0 = Mathf.Min(y0, poly[i].y); x1 = Mathf.Max(x1, poly[i].x); y1 = Mathf.Max(y1, poly[i].y);
                }
                // a node of its own, so the leaf turns about its centre
                for (int i = 0; i < poly.Length; i++) poly[i] -= new Vector2(x0, y0);
                var shape = Ui.Shape(p, x0, y0, x1 - x0, y1 - y0, poly, c, null, null, false, "Leaf");
                // JS wLeaf: 5.5 s, every second leaf 7 s and every third 6.2 s, back and forth by (6, 10) px and 14 degrees
                float period = n % 3 == 2 ? 6.2f : n % 2 == 1 ? 7f : 5.5f;
                UiFx.Loop(shape.transform, new UiAnim("wLeaf", period / 2f).Eased(UiEase.EaseInOut).FromTo(UiProp.X, 0, 6).FromTo(UiProp.Y, 0, 10).FromTo(UiProp.Rot, 0, 14).Repeat(true));
                n++;
            }
        }

        // the Weapons backdrop: crimson sky, sun, hills, trees and branch (clean without milky beam or stray floor ring bleeding through tiles)
        static void Backdrop(Transform p)
        {
            Ui.Grad(p, 0, 0, 1600, 540, Ui.Sky1, Ui.Sky2, false);
            Ui.Grad(p, 0, 540, 1600, 360, Ui.Sky2, Ui.Sky3, false);
            Ui.Glow(p, 500, 430, 380, Ui.A(Ui.Acc, 0.34f), 1.25f);
            Ui.Disc(p, 500, 430, 264, Ui.Mix(Ui.Deep, Ui.Sun2, 0.55f));
            Ui.Glow(p, 457, 365, 180, Ui.A(Ui.Sun1, 0.72f), 1.4f);
            UiFx.Loop(Ui.Line(p, Ui.Circle(500, 430, 294, 72).Close(), 1.5f, Ui.A(Ui.Gold, 0.28f)).transform, UiAnims.WGlow(4f));
            Ui.Shape(p, 0, 0, 1600, 900, Poly(0, 700, 160, 650, 320, 680, 480, 630, 640, 670, 820, 640, 1000, 670, 1000, 900, 0, 900), Ground(0.85f), null, null, false, "Hills");
            Ui.Box(p, 20, 96, 12, 804, Ground(0.65f), "Trunk");
            Ui.Box(p, 46, 96, 9, 804, Ground(0.65f), "Trunk");
            Ui.Line(p, Bezier(new Vector2(1000, 110), new Vector2(940, 130), new Vector2(900, 150), new Vector2(850, 190)), 5f, Ground(0.85f));
            Ui.Box(p, 0, 800, 1000, 100, Ground(0.45f), "Floor");
            Leaves(p, new[] { new[] { 960f, 132f, 1.5f, 10f }, new[] { 930f, 154f, 1.2f, -25f }, new[] { 890f, 168f, 1.3f, 35f }, new[] { 856f, 196f, 1.1f, -15f }, new[] { 984f, 116f, 1f, 50f } });
        }

        static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int n = 64)
        {
            var r = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                r[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return r;
        }

        // the Armor tab backdrop (JS armorScreenHTML): a moonlit torii over hills
        static void ArmorBackdrop(Transform p)
        {
            Ui.Grad(p, 0, 0, 1600, 540, Ui.Sky1, Ui.Sky2, false);
            Ui.Grad(p, 0, 540, 1600, 360, Ui.Sky2, Ui.Sky3, false);
            Ui.Glow(p, 800, 250, 380, Ui.A(Ui.Acc, 0.5f), 1f);
            Ui.Disc(p, 800, 250, 230, Ui.A(Ui.Mix(Ui.Acc, Ui.Deep, 0.6f), 0.92f));
            Ui.Glow(p, 760, 215, 150, Ui.A(Ui.Sun1, 0.8f), 1.3f);
            UiFx.Loop(Ui.Line(p, Ellipse(800, 250, 262, 262, 72), 1.5f, Ui.A(Ui.Bone, 0.12f)).transform, UiAnims.WGlow(4f));
            Ui.Shape(p, 0, 0, 1600, 900, Poly(0, 330, 130, 270, 250, 300, 420, 220, 560, 300, 700, 280, 800, 330, 930, 270, 1080, 300, 1220, 230, 1380, 290, 1600, 250, 1600, 900, 0, 900), Ui.A(Ground(0.55f), 0.8f), null, null, false, "Mountains");
            var torii = Ground(0.72f);
            var beam = new List<Vector2>();
            beam.AddRange(Quad(new Vector2(702, 112), new Vector2(800, 96), new Vector2(898, 112)));
            beam.Add(new Vector2(903, 128));
            var low = Quad(new Vector2(903, 128), new Vector2(800, 114), new Vector2(697, 128));
            beam.AddRange(low);
            Ui.Shape(p, 0, 0, 1600, 900, beam.ToArray(), torii, null, null, false, "Torii");
            Ui.Box(p, 712, 134, 176, 11, torii, "Torii");
            Ui.Box(p, 736, 124, 13, 196, torii, "Torii");
            Ui.Box(p, 851, 124, 13, 196, torii, "Torii");
            Ui.Box(p, 794, 134, 12, 24, torii, "Torii");
            var small = new List<Vector2>();
            small.AddRange(Quad(new Vector2(890, 238), new Vector2(922, 232), new Vector2(954, 238)));
            small.Add(new Vector2(956, 244));
            small.AddRange(Quad(new Vector2(956, 244), new Vector2(922, 239), new Vector2(888, 244)));
            Ui.Shape(p, 0, 0, 1600, 900, small.ToArray(), torii, null, null, false, "Torii");
            Ui.Box(p, 898, 244, 6, 66, torii, "Torii");
            Ui.Box(p, 940, 244, 6, 66, torii, "Torii");
            var field = new List<Vector2>();
            field.AddRange(Bezier(new Vector2(0, 312), new Vector2(260, 292), new Vector2(520, 336), new Vector2(800, 318)));
            field.AddRange(Bezier(new Vector2(800, 318), new Vector2(1080, 300), new Vector2(1340, 330), new Vector2(1600, 306)));
            field.Add(new Vector2(1600, 900));
            field.Add(new Vector2(0, 900));
            Ui.Shape(p, 0, 0, 1600, 900, field.ToArray(), Ground(0.45f), null, null, false, "Field");
            Ui.Line(p, Bezier(new Vector2(1600, 92), new Vector2(1540, 102), new Vector2(1500, 126), new Vector2(1440, 140)), 5f, Ground(0.85f));
            Ui.Line(p, Bezier(new Vector2(1440, 140), new Vector2(1400, 150), new Vector2(1370, 166), new Vector2(1330, 190)), 5f, Ground(0.85f));
            Ui.Line(p, Bezier(new Vector2(1504, 116), new Vector2(1484, 136), new Vector2(1474, 148), new Vector2(1456, 166)), 3f, Ground(0.85f));
            Leaves(p, new[]
            {
                new[] { 1560f, 100f, 1.5f, 10f }, new[] { 1520f, 114f, 1.2f, -25f }, new[] { 1478f, 130f, 1.4f, 35f }, new[] { 1440f, 144f, 1.1f, -15f },
                new[] { 1404f, 158f, 1.3f, 50f }, new[] { 1366f, 176f, 1f, 20f }, new[] { 1458f, 168f, 1f, -40f }, new[] { 1338f, 192f, 1.2f, 5f },
            });
        }
    }

    static class Vec2Extensions
    {
        /// <summary>The same points with the first repeated at the end, so a line closes.</summary>
        public static Vector2[] Close(this Vector2[] pts)
        {
            var r = new Vector2[pts.Length + 1];
            pts.CopyTo(r, 0);
            r[pts.Length] = pts[0];
            return r;
        }
    }
}
