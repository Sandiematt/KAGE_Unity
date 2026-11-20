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
        ArmorLine? builtLine;
        string builtSelection;
        bool enterTiles, enterDetail;
        RectTransform tileContent, cardsFront, cardsBack;
        float tileMax;
        int tileIndex;
        static readonly Color Violet = Ui.Hex("#a98bff"), Blue = Ui.Hex("#7fb8ff"), Lit = Ui.Hex("#e9c46a");

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
            var world = flow.ArmoryWorld;
            RectTransform back = world != null ? world.BackRoot : null;
            if (back != null)
            {
                for (int i = back.childCount - 1; i >= 0; i--)
                {
                    back.GetChild(i).gameObject.SetActive(false);
                    Object.Destroy(back.GetChild(i).gameObject);
                }
                if (armor) ArmorBackdrop(back); else Backdrop(back);
            }

            enterTiles = Fresh || builtTab != v.Tab || builtLine != v.Line;
            enterDetail = enterTiles || builtSelection != v.SelectedId;
            builtTab = v.Tab; builtLine = v.Line; builtSelection = v.SelectedId;
            Ui.Label(Root, v.Kanji, UiFont.Brush, armor ? 46 : 64, Ui.A(Ui.Bone, 0.2f), armor ? 44 : 64, armor ? 98 : 150, 400, armor ? 60 : 80, TextAnchor.UpperLeft);
            if (v.Preview == ArmoryPreview.Illustration) Preview(v);
            Header(v);
            if (armor) ArmorBody(v, save, back);
            else
            {
                Detail(v);
                Actions(v, Root, 1020, 752, 540);
                Tiles(v, save);
            }
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
            for (int i = 0; i < v.Tiles.Length; i++)
            {
                saves.Add(ArmoryView.PreviewSave(save, ArmoryTab.Armor, v.Tiles[i].Id));
                xs.Add(CardX(i) + 3f + 0.54f * (CardW - 6f));
            }
            string key = v.Line + "|" + save.equipped.weapon + ":" + KageCharacter.LoadoutKey(save).Split('|')[0] + "|" + v.Tiles.Length;
            // the row scrolls by the camera panning with it, so each Kage stays in his card
            world.ShowRow(saves, xs, key, cardScroll);
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
            UiParts.Back(Root, Nav, () => C.Back(), 40, 19);
            var title = Ui.Label(Root, "ARMORY", UiFont.Title, 36, Ui.Bone, 118, 0, 300, 96, TextAnchor.MiddleLeft, 0f);
            float titleW = UiParts.Width(title);
            string[] names = { "Weapons", "Helmets", "Armor" };
            var widths = new float[3];
            float total = 0f;
            for (int i = 0; i < 3; i++)
            {
                var probe = Ui.Label(Root, names[i].ToUpperInvariant(), UiFont.Title, 20, Color.white, 0, 0, 300, 48, TextAnchor.MiddleLeft, 2.4f);
                widths[i] = UiParts.Width(probe, 2.4f) + 2.4f + 36f;
                Object.Destroy(probe.gameObject);
                total += widths[i];
            }
            total += 2 * 44f;
            float grpW = 58f + 20f + titleW;
            float pillsW = 376f;
            float free = 1520f - grpW - total - pillsW;
            float x = 40f + grpW + free / 2f;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                bool on = (int)v.Tab == i;
                var rt = Ui.Node(Root, "Tab_" + names[i], x, 24, widths[i], 48);
                if (on)
                {
                    var brush = Ui.Shape(rt, 0, 8, 150, 34, Ui.Poly(150, 34, 0, 20, 8, 4, 70, 0, 98, 12, 88, 40, 100, 62, 86, 96, 6, 92, 0, 70),
                        Ui.Acc, Ui.Deep, Ui.A(Ui.Deep, 0f), true, "Brush");
                }
                var t = Ui.Label(rt, names[i].ToUpperInvariant(), UiFont.Title, 20, on ? Ui.Bone : Ui.Muted, 18, 0, widths[i] - 18, 48, TextAnchor.MiddleLeft, 2.4f);
                var tabTween = UiFocusTween.On(rt, .16f, k => t.color = on ? Ui.Bone : Color.Lerp(Ui.Muted, Ui.Bone, k));
                UiParts.Make(rt, Nav, "tab:" + i, () => A.SelectTab((ArmoryTab)idx), f => tabTween.Set(f));
                if (on && enterTiles) UiFx.In(rt, UiAnims.WFade(.3f));
                x += widths[i] + 44f;
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
                case "trophy":
                case "boss": return Ui.Hot;
                case "common":
                case "starter": return Ui.Muted;
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
            Ui.Diamond(p, x + 4.5f, y + 10f, 9f, c, c, 0f);
            Ui.Label(p, v.TierLine.ToUpperInvariant(), UiFont.Title, 14, c, x + 19f, y, 460f, 20f, TextAnchor.MiddleLeft, 4.2f);
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
            const float PanelX = 1020f, PanelY = 112f, PanelW = 540f, Max = 628f;
            var det = Ui.Node(Root, "Detail", PanelX, PanelY, PanelW, Max);
            if (enterDetail) UiFx.In(det, UiAnims.WFade(.35f));
            var bg = Ui.Box(det, 0, 0, PanelW, Max, Ui.PanelA(0.86f));
            var clip = Ui.Node(det, "Clip", 0, 0, PanelW, Max);
            clip.gameObject.AddComponent<RectMask2D>();
            float x = 36f, w = PanelW - 72f, y = 28f;
            Rarity(clip, x, y, v); y += 20f + 14f;
            NameRow(clip, x, y, v, 52, true); y += 52f + 14f;
            if (!string.IsNullOrEmpty(v.Lore)) y += Para(clip, v.Lore, UiFont.Mincho, 15, Ui.Muted, x, y, w, 24f) + 14f;
            if (!string.IsNullOrEmpty(v.Desc)) y += Para(clip, v.Desc, UiFont.Body, 16, Ui.Bone, x, y, w, 24f) + 14f;
            y += Level(clip, x, y, w, v) + 14f;
            y += Stats(clip, x, y, w, v);
            for (int i = 0; i < v.Boxes.Length; i++)
            {
                y += 14f + (i == 0 ? 4f : 0f);
                y += Box(clip, x, y, w, v.Boxes[i], 16f);
            }
            y += 26f;
            float h = Mathf.Min(Max, y);
            det.sizeDelta = new Vector2(PanelW, h);
            bg.rectTransform.sizeDelta = new Vector2(PanelW, h);
            clip.sizeDelta = new Vector2(PanelW, h);
            Ui.Frame(det, 0, 0, PanelW, h, 1.5f, Ui.GoldDim);
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
            UiParts.Make(rt, Nav, key, onSubmit, f =>
            {
                rt.anchoredPosition = new Vector2(x, -(y - (f ? 2f : 0f)));
                hl.color = f ? Ui.A(Color.white, 0.07f) : Color.clear;
            }, b.Enabled);
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
        }

        void Tile(ArmoryView v, ArmoryTile t, float x)
        {
            const float Y = 6f, H = 110f;
            var rt = Ui.Node(tileContent, "Tile_" + t.Id, x, Y, TileW, H);
            if (enterTiles) UiFx.In(rt, UiAnims.WRise(.45f, tileIndex * 0.025f));
            tileIndex++;
            Ui.Box(rt, 0, 0, TileW, H, Ui.PanelA(0.9f));
            if (t.Selected)
            {
                Ui.Glow(rt, TileW / 2f, H * 0.4f, 74f, Ui.A(Ui.Acc, 0.45f), 1.2f);
                Ui.Frame(rt, 0, 0, TileW, H, 1.5f, Ui.Gold);
            }
            else Ui.Frame(rt, 0, 0, TileW, H, 1f, Ui.GoldDim);

            // the picture: the item drawn the way the JS draws it (the sword, the helmet), dim when it is not owned (JS .locked .art)
            var tint = t.Owned ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.45f);
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
                Ui.Label(rt, t.LockLabel.ToUpperInvariant(), UiFont.Title, 11, Ui.Hot, 0, H - 10f - 18f, TileW, 18f, TextAnchor.MiddleCenter, 1.6f);
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
            var lift = UiFocusTween.On(rt, .16f, k => UiOffsets.Of(rt).SetFocus(new Vector2(0f, 3f * k)));
            var item = UiParts.Make(rt, Nav, "tile:" + id, () => A.Select(id), f =>
            {
                lift.Set(f);
                if (f) KeepVisible(tx);
            });
            item.onUserFocus = () => A.Select(id);
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

        // ---------------- the Armor tab ----------------

        // JS .al-row: cards 177 wide, 14 apart, from x = 42; the row (x 40 to 1560) scrolls when there are more than eight
        const float CardW = 177f, CardPitch = 191f, RowX = 40f, RowW = 1520f;

        static float CardX(int i) { return 42f + i * CardPitch; }

        void KeepCardVisible(int i)
        {
            float lo = CardX(i) - RowX - 4f, hi = CardX(i) - RowX + CardW + 4f;
            if (lo < cardScroll) cardScroll = Mathf.Clamp(lo, 0f, cardMax);
            else if (hi > cardScroll + RowW) cardScroll = Mathf.Clamp(hi - RowW, 0f, cardMax);
        }

        void ArmorBody(ArmoryView v, SaveData save, RectTransform back)
        {
            // the two rows
            string[] chips = { "Armor line", "Trophies" };
            float cx = 112f;
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                bool on = (int)v.Line == i;
                string text = chips[i].ToUpperInvariant();
                var tl = Ui.Label(Root, text, UiFont.Title, 13, on ? Ui.Bone : Ui.Muted, 0, 0, 300f, 34f, TextAnchor.MiddleLeft, 2.6f);
                float tw = UiParts.Width(tl, 2.6f) + 2.6f;
                Text count = null;
                float cw = 0f;
                if (i == 1)
                {
                    count = Ui.Label(Root, v.TrophyCount.ToString(), UiFont.Title, 13, Ui.Gold, 0, 0, 60f, 34f, TextAnchor.MiddleLeft);
                    cw = 8f + UiParts.Width(count);
                }
                float w = 32f + tw + cw;
                var rt = Ui.Node(Root, "Chip" + i, cx, 104f, w, 34f);
                Ui.Box(rt, 0, 0, w, 34f, on ? Ui.A(Ui.Gold, 0.14f) : Ui.PanelA(0.7f)).transform.SetAsFirstSibling();
                Ui.Frame(rt, 0, 0, w, 34f, 1f, on ? Ui.Gold : Ui.Hair);
                tl.transform.SetParent(rt, false);
                tl.rectTransform.anchoredPosition = new Vector2(16f, 0);
                if (count != null) { count.transform.SetParent(rt, false); count.rectTransform.anchoredPosition = new Vector2(16f + tw + 8f, 0); }
                var tlRef = tl;
                UiParts.Make(rt, Nav, "line:" + i, () => A.SetLine((ArmorLine)idx), f => tlRef.color = on || f ? Ui.Bone : Ui.Muted);
                cx += w + 10f;
            }

            // the cards, in a row that is clipped to its window and scrolls
            int n = v.Tiles.Length;
            cardMax = Mathf.Max(0f, CardX(n - 1) + CardW + 4f - RowX - RowW);
            cardScroll = Mathf.Clamp(cardScroll, 0f, cardMax);
            for (int i = 0; i < n; i++)
                if (v.Tiles[i].Selected) KeepCardVisible(i);
            cardsFront = Ui.Node(Root, "Cards", RowX, 0, RowW, 900);
            cardsFront.gameObject.AddComponent<RectMask2D>();
            cardsBack = null;
            if (back != null)
            {
                cardsBack = Ui.Node(back, "Cards", RowX, 0, RowW, 900);
                cardsBack.gameObject.AddComponent<RectMask2D>();
            }
            for (int i = 0; i < n; i++) Card(v, v.Tiles[i], i, CardX(i) - RowX - cardScroll, CardW);

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
            return new[]
            {
                new Vector2(x + c, y), new Vector2(x + w - c, y), new Vector2(x + w, y + c), new Vector2(x + w, y + h - c),
                new Vector2(x + w - c, y + h), new Vector2(x + c, y + h), new Vector2(x, y + h - c), new Vector2(x, y + c),
            };
        }

        static Vector2[] Closed(Vector2[] pts)
        {
            var r = new Vector2[pts.Length + 1];
            pts.CopyTo(r, 0);
            r[pts.Length] = pts[0];
            return r;
        }

        void Card(ArmoryView v, ArmoryTile t, int index, float x, float w)
        {
            const float Y = 158f, H = 412f;
            var hue = HueOf(t.Id);
            // behind the fighter: the card's dark ground and the glow
            if (cardsBack != null)
            {
                var bg = Ui.Node(cardsBack, "Card_" + t.Id, x, Y, w, H);
                Ui.Shape(bg, 0, 0, w, H, Octagon(0, 0, w, H, 10f), Ui.PanelA(0.82f), Ui.Mix(Ui.Panel, Color.black, 0.96f), null, false);
                // CSS ellipse 90% 60% at 52% 42%, clear at 72%: an oval glow, scaled about its own centre
                float gr = 0.65f * w;
                var glow = Ui.Glow(bg, 0, 0, gr, Ui.A(Ui.Acc, 0.42f), 1.4f);
                glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                glow.rectTransform.anchoredPosition = new Vector2(w * 0.52f, -H * 0.42f);
                glow.rectTransform.localScale = new Vector3(1f, 0.43f * H / gr, 1f);
            }
            var rt = Ui.Node(cardsFront, "Card_" + t.Id, x, Y, w, H);
            if (enterTiles) UiFx.In(rt, UiAnims.WRise(.5f, index * 0.05f));
            bool sel = t.Selected;
            if (sel)
            {
                // CSS drop-shadow 0 0 14px: a few fading outlines round the card
                for (int k = 4; k >= 1; k--) Ui.Line(rt, Closed(Octagon(1f - 3f * k, 1f - 3f * k, w - 2f + 6f * k, H - 2f + 6f * k, 10f + 3f * k)), 3f, Ui.A(Lit, 0.11f / k * 2f));
            }
            var edge = sel ? Lit : Ui.Mix(hue, Ui.A(Ui.Bone, 0.15f), 0.3f);
            Ui.Line(rt, Closed(Octagon(1f, 1f, w - 2f, H - 2f, 10f)), sel ? 2f : 1f, edge);
            Ui.Line(rt, Closed(Octagon(6f, 6f, w - 12f, H - 12f, 8f)), 1f, sel ? Ui.A(Lit, 0.55f) : Ui.Mix(hue, Ui.A(Ui.Bone, 0.1f), 0.22f));

            // the tag in the top corners
            if (t.Equipped) Ui.Label(rt, "◆ EQUIPPED", UiFont.Title, 11, Ui.Gold, 16f, 12f, 150f, 20f, TextAnchor.MiddleLeft, 1.6f);
            else if (t.Owned) Ui.Label(rt, "Lv " + t.Level, UiFont.Title, 12, Ui.Bone, 0, 12f, w - 16f, 20f, TextAnchor.MiddleRight, 1f);
            else if (t.LockLabel != null) PadLock(rt, w - 16f - 14f, 16f, Ui.Muted);
            else
            {
                float rx = w - 16f;
                if (t.Gems > 0)
                {
                    var gl = Ui.Label(rt, t.Gems.ToString(), UiFont.Title, 12, Ui.Muted, 0, 12f, 200f, 20f, TextAnchor.MiddleLeft, 1f);
                    float gw = UiParts.Width(gl, 1f);
                    gl.rectTransform.anchoredPosition = new Vector2(rx - gw, -12f);
                    Ui.Diamond(rt, rx - gw - 12f, 22f, 9f, Ui.Ruby, Ui.Ruby, 0f);
                    rx -= gw + 24f;
                }
                if (t.Coins > 0)
                {
                    var cl = Ui.Label(rt, UiParts.Num(t.Coins), UiFont.Title, 12, Ui.Muted, 0, 12f, 200f, 20f, TextAnchor.MiddleLeft, 1f);
                    float cw = UiParts.Width(cl, 1f);
                    cl.rectTransform.anchoredPosition = new Vector2(rx - cw, -12f);
                    Ui.Disc(rt, rx - cw - 11f, 22f, 6.5f, Ui.Gold);
                    Ui.Disc(rt, rx - cw - 11f, 22f, 3.8f, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                    Ui.Disc(rt, rx - cw - 11f, 22f, 2f, Ui.Gold);
                }
            }

            // the name and the ornament
            Ui.Box(rt, 8f, 496f - Y, w - 16f, 1f, Ui.Mix(hue, Ui.A(Ui.Bone, 0.12f), 0.28f));
            var nm = Ui.Label(rt, Ellipsis(t.Name.ToUpperInvariant(), 14, 0f, w - 16f), UiFont.Mincho, 14, sel ? Ui.Hex("#f3dc9a") : Ui.Bone, 0, 504f - Y, w, 28f, TextAnchor.MiddleCenter);
            float oy = 549f - Y;
            Ui.Grad(rt, w / 2f - 60f, oy - 0.75f, 52f, 1.5f, Color.clear, Ui.A(hue, 0.9f), true);
            Ui.Grad(rt, w / 2f + 8f, oy - 0.75f, 52f, 1.5f, Ui.A(hue, 0.9f), Color.clear, true);
            Ui.Diamond(rt, w / 2f, oy, 13f, Color.clear, hue, 1.5f);
            Ui.Diamond(rt, w / 2f, oy, 5f, hue, hue, 0f);
            Ui.Diamond(rt, w / 2f - 18f, oy, 4f, hue, hue, 0f);
            Ui.Diamond(rt, w / 2f + 18f, oy, 4f, hue, hue, 0f);

            string id = t.Id;
            float cardX = x;
            int idx = index;
            var lift = UiFocusTween.On(rt, .18f, k => UiOffsets.Of(rt).SetFocus(new Vector2(0f, 4f * k)));
            var item = UiParts.Make(rt, Nav, "tile:" + id, () => A.Select(id), f =>
            {
                lift.Set(f);
                if (f) KeepCardVisible(idx);
            });
            item.onUserFocus = () => A.Select(id);
        }

        void ArmorDetail(ArmoryView v)
        {
            var hue = HueOf(v.SelectedId);
            // the details under the row
            var det = Ui.Node(Root, "Detail", 40f, 600f, 1040f, 262f);
            if (enterDetail) UiFx.In(det, UiAnims.WFade(.35f));
            Ui.Box(det, 0, 0, 1040f, 262f, Ui.PanelA(0.88f));
            Ui.Grad(det, 0, 0, 416f, 262f, Ui.A(hue, 0.1f), Color.clear, true);
            Ui.Frame(det, 0, 0, 1040f, 262f, 1.5f, Ui.GoldDim);
            float x = 34f, y = 24f;
            Rarity(det, x, y, v);
            y += 20f + 10f;
            NameRow(det, x, y, v, 44, false);
            y += 44f + 10f;
            if (!string.IsNullOrEmpty(v.Lore)) Para(det, v.Lore, UiFont.Mincho, 14, Ui.Muted, x, y, 430f, 21.7f, 4);
            // the stats, centred on the right
            float statsH = v.Stats.Length * 34f + (v.Stats.Length - 1) * 14f;
            Stats(det, 34f + 430f + 40f, (262f - statsH) / 2f, 502f, v);

            // the level, the box and the buttons
            var side = Ui.Node(Root, "Side", 1100f, 600f, 460f, 262f);
            float sy = 0f;
            sy += Level(side, 0, sy, 460f, v) + 14f;
            foreach (var b in v.Boxes) sy += Box(side, 0, sy, 460f, b, 10f, true) + 14f;
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

        static void Leaves(Transform p, float[][] pts)
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

        // the Weapons and Helmets backdrop (JS renderArmory .w-bd): sky, the big sun, the beam, hills, trees and a branch
        static void Backdrop(Transform p)
        {
            Ui.Grad(p, 0, 0, 1600, 540, Ui.Sky1, Ui.Sky2, false);
            Ui.Grad(p, 0, 540, 1600, 360, Ui.Sky2, Ui.Sky3, false);
            Ui.Glow(p, 500, 430, 420, Ui.A(Ui.Acc, 0.45f), 1f);
            Ui.Disc(p, 500, 430, 270, Ui.Mix(Ui.Deep, Ui.Sun2, 0.55f));
            Ui.Glow(p, 457, 365, 192, Ui.A(Ui.Sun1, 0.85f), 1.3f);
            UiFx.Loop(Ui.Line(p, Ui.Circle(500, 430, 300, 72).Close(), 1.5f, Ui.A(Ui.Gold, 0.3f)).transform, UiAnims.WGlow(4f));
            Ui.Shape(p, 0, 0, 1600, 900, Poly(0, 640, 1000, 200, 1000, 330, 0, 770), Color.clear, Ui.A(Ui.Bone, 0.07f), Color.clear, true, "Beam");
            Ui.Shape(p, 0, 0, 1600, 900, Poly(0, 700, 160, 650, 320, 680, 480, 630, 640, 670, 820, 640, 1000, 670, 1000, 900, 0, 900), Ground(0.85f), null, null, false, "Hills");
            Ui.Box(p, 20, 96, 12, 804, Ground(0.65f), "Trunk");
            Ui.Box(p, 46, 96, 9, 804, Ground(0.65f), "Trunk");
            Ui.Line(p, Bezier(new Vector2(1000, 110), new Vector2(940, 130), new Vector2(900, 150), new Vector2(850, 190)), 5f, Ground(0.85f));
            Ui.Box(p, 0, 800, 1000, 100, Ground(0.45f), "Floor");
            Ui.Line(p, Ellipse(500, 770, 300, 20), 1.5f, Ui.A(Ui.Bone, 0.15f));
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
