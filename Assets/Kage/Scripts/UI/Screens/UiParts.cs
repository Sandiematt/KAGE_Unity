using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The parts that come back on several JS screens (wonder.css): the back diamond, the coin and jade pills, key hints, the red
    /// brush button, segmented controls and the chips. Each interactive part is a <see cref="NavItem"/> in a <see cref="NavGroup"/>,
    /// so keyboard, gamepad and mouse all drive the same thing.
    /// </summary>
    public static class UiParts
    {
        public static NavItem Make(RectTransform node, NavGroup nav, string key, Action onSubmit, Action<bool> onFocus = null, bool interactable = true)
        {
            var item = node.gameObject.AddComponent<NavItem>();
            item.key = key;
            item.onSubmit = onSubmit;
            item.onFocus = onFocus;
            item.interactable = interactable;
            nav.Add(item);
            return item;
        }

        public static string Num(int n) { return n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Width of a one-line label including its letter spacing.</summary>
        public static float Width(Text t, float tracking = 0f)
        {
            return t.preferredWidth + tracking * Mathf.Max(0, t.text.Length - 1);
        }

        // ---------------- top bar ----------------

        /// <summary>JS .w-back: a 58 px box holding a 40 px diamond and a chevron.</summary>
        public static NavItem Back(Transform p, NavGroup nav, Action onSubmit, float x = 40f, float y = 15f)
        {
            var rt = Ui.Node(p, "Back", x, y, 58, 58);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + 29f, -(y + 29f));
            var dia = Ui.Diamond(rt, 29, 29, 40, Ui.PanelA(0.88f), Ui.GoldDim, 1.5f, true);
            var chevron = Ui.Line(rt, new[] { new Vector2(32.2f, 36.5f), new Vector2(25.8f, 29f), new Vector2(32.2f, 21.5f) }, 2f, Ui.Bone);
            chevron.rectTransform.sizeDelta = new Vector2(58, 58);
            return Make(rt, nav, "back", onSubmit, f =>
            {
                dia.SetColors(f ? Ui.Mix(Ui.Acc, Ui.Panel, 0.34f) : Ui.PanelA(0.88f), f ? Ui.Gold : Ui.GoldDim);
                rt.localScale = Vector3.one * (f ? 1.07f : 1f);
            });
        }

        /// <summary>JS wPills: the coin and jade counts at the top right.</summary>
        public static void Pills(Transform p, Rules.SaveData s, float rightEdge = 1560f, float y = 22f)
        {
            float gemW = 168f, coinW = 192f;
            float gemX = rightEdge - gemW, coinX = gemX - 16f - coinW;
            Pill(p, coinX, y, coinW, true, UiParts.Num(s.coins));
            Pill(p, gemX, y, gemW, false, UiParts.Num(s.gems));
        }

        static void Pill(Transform p, float x, float y, float w, bool coin, string value)
        {
            var rt = Ui.Node(p, coin ? "Coins" : "Gems", x, y, w, 44);
            Ui.Box(rt, 0, 0, w, 44, Ui.PanelA(0.88f));
            Ui.Box(rt, 0, 0, w, 1.5f, Ui.A(Ui.Gold, 0.32f));
            Ui.Frame(rt, 0, 0, w, 44, 1, Ui.A(Ui.GoldDim, 0.85f));
            if (coin)
            {
                Ui.Disc(rt, 24, 22, 10, Ui.Gold);
                Ui.Disc(rt, 24, 22, 6, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                Ui.Disc(rt, 24, 22, 3.2f, Ui.Gold);
            }
            else Ui.Diamond(rt, 24, 22, 14, Ui.Ruby, Ui.Ruby, 0);
            Ui.Label(rt, value, UiFont.Title, 20, Ui.Bone, 48, 6, w - 88, 32, TextAnchor.MiddleLeft);
            float px = w - 34f;
            Ui.Box(rt, px - 4, 10, 1, 24, Ui.Hair);
            Ui.Box(rt, px + 6, 21, 14, 2, Ui.Gold);
            Ui.Box(rt, px + 12, 15, 2, 14, Ui.Gold);
        }

        /// <summary>JS wKeys: key cap and label pairs.</summary>
        public static void Keys(Transform p, float x, float y, params string[][] keys)
        {
            var rt = Ui.Node(p, "Keys", x, y, 740, 48);
            float cx = 0f;
            foreach (var k in keys)
            {
                var cap = Ui.Label(rt, k[0], UiFont.Cond, 13, Ui.Gold, 0, 0, 200, 26, TextAnchor.MiddleLeft, 1.56f);
                float kw = Width(cap, 1.56f) + 18f;
                cap.rectTransform.anchoredPosition = new Vector2(cx + 9f, -11f);
                Ui.Box(rt, cx, 11, kw, 26, Ui.PanelA(0.88f)).transform.SetAsFirstSibling();
                Ui.Box(rt, cx, 35, kw, 2, Ui.A(Ui.GoldDim, 0.65f));
                Ui.Frame(rt, cx, 11, kw, 26, 1, Ui.A(Ui.GoldDim, 0.85f));
                var lb = Ui.Label(rt, k[1], UiFont.Title, 14, Ui.Muted, cx + kw + 9f, 11, 200, 26, TextAnchor.MiddleLeft);
                cx += kw + 9f + Width(lb) + 22f;
            }
        }

        // ---------------- primary battle button (clean chamfered crimson plate) ----------------

        /// <summary>
        /// The crimson battle button (Fight): kanji and a tracked label on a crisp chamfered lacquer plate. Locked shows it muted.
        /// <paramref name="kanji"/> may be empty.
        /// </summary>
        public static NavItem Brush(Transform p, NavGroup nav, string key, float x, float y, float w, float h, string kanji, string label,
            int labelSize, int kanjiSize, float tracking, bool locked, Action onSubmit)
        {
            var rt = Ui.Node(p, "Brush_" + key, x, y, w, h);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            float bevel = Mathf.Min(11f, h * 0.18f);
            var plate = Ui.Chamfer(0, 0, w, h, bevel);
            var shape = Ui.Shape(rt, 0, 0, w, h, plate,
                locked ? Ui.Hex("#26211e") : Ui.Hex("#8e2224"),
                locked ? Ui.Hex("#322c27") : Ui.Acc,
                locked ? Ui.Hex("#221d1a") : Ui.Hex("#7d1c1f"), true, "Stroke");
            // Crisp beveled metallic rim and subtle top inner specular hairline (zero blurry glow or torn notches)
            var rim = Ui.Stroke(rt, 0, 0, w, h, plate, 1.5f, locked ? Ui.Hair : Ui.A(Ui.Gold, 0.72f), -1.5f, "Rim");
            if (!locked)
            {
                Ui.Box(rt, bevel + 4f, 2f, w - (bevel + 4f) * 2f, 1f, Ui.A(Ui.Bone, 0.24f), "TopEdge");
                if (w >= 260f)
                {
                    Ui.Diamond(rt, 22f, h * 0.5f, 6f, Ui.A(Ui.Gold, 0.7f), Ui.A(Ui.Gold, 0.7f), 0f);
                    Ui.Diamond(rt, w - 22f, h * 0.5f, 6f, Ui.A(Ui.Gold, 0.7f), Ui.A(Ui.Gold, 0.7f), 0f);
                }
            }
            var focusRing = Ui.Ring(rt, w, h, plate, 2.5f, 2f);
            Color text = locked ? Ui.Muted : Ui.Bone;
            var lb = Ui.Label(rt, label, UiFont.Title, labelSize, text, 0, 0, w, h, TextAnchor.MiddleCenter, tracking);
            var lbSh = lb.gameObject.AddComponent<Shadow>();
            lbSh.effectColor = new Color(0f, 0f, 0f, 0.65f);
            lbSh.effectDistance = new Vector2(0f, -1.5f);
            float lw = Width(lb, tracking);
            float kw = 0f;
            Text kj = null;
            if (!string.IsNullOrEmpty(kanji))
            {
                kj = Ui.Label(rt, kanji, UiFont.Brush, kanjiSize, text, 0, 0, kanjiSize + 8, h, TextAnchor.MiddleCenter);
                var kjSh = kj.gameObject.AddComponent<Shadow>();
                kjSh.effectColor = new Color(0f, 0f, 0f, 0.65f);
                kjSh.effectDistance = new Vector2(0f, -1.5f);
                kw = kanjiSize + 8;
            }
            float gap = 14f;
            float total = kw + (kw > 0 ? gap : 0) + lw;
            float start = (w - total) / 2f;
            if (kj != null) kj.rectTransform.anchoredPosition = new Vector2(start, 0);
            lb.rectTransform.anchoredPosition = new Vector2(start + kw + (kw > 0 ? gap : 0), 0);
            lb.rectTransform.sizeDelta = new Vector2(lw + 40, h);
            lb.alignment = TextAnchor.MiddleLeft;
            var sheen = ButtonSheen.Add(rt, w, h);
            Color d0 = Ui.Hex("#8e2224"), d1 = Ui.Acc, d2 = Ui.Hex("#7d1c1f");
            Color h0 = Ui.Acc, h1 = Ui.Hot, h2 = Ui.Acc;
            var tween = UiFocusTween.On(rt, 0.14f, v =>
            {
                if (locked) return;
                rt.localScale = Vector3.one * (1f + 0.025f * v);
                shape.SetColors(Color.Lerp(d0, h0, v), Color.Lerp(d1, h1, v), Color.Lerp(d2, h2, v));
                rim.color = Color.Lerp(Ui.A(Ui.Gold, 0.72f), Ui.Hex("#f5d996"), v);
                focusRing.color = Ui.A(Ui.Gold, 0.92f * v);
            });
            var brushItem = Make(rt, nav, key, () => { sheen.Play(0.32f); onSubmit(); }, f =>
            {
                tween.Set(f && !locked);
                if (f && !locked) sheen.Play(0.45f);
            }, !locked);
            brushItem.press = 0.97f;
            return brushItem;
        }

        // ---------------- overlay and menu plates (clean chamfered gold / lacquer buttons) ----------------

        /// <summary>
        /// Overlay and results buttons: crisp chamfered plate, burnished gold for the primary choice and dark crimson-obsidian
        /// lacquer for secondary choices, with a clean mitered border and outer gold focus ring.
        /// </summary>
        public static NavItem Plate(Transform p, NavGroup nav, string key, float x, float y, float w, float h, string label, bool primary, Action onSubmit)
        {
            var rt = Ui.Node(p, "Plate_" + key, x, y, w, h);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
            float bevel = Mathf.Min(8f, h * 0.15f);
            var plate = Ui.Chamfer(0, 0, w, h, bevel);
            Color p0 = Ui.Hex("#f1d69c"), p1 = Ui.Hex("#d4ab60"), p2 = Ui.Hex("#ab813b");
            Color ph0 = Ui.Hex("#fae4b5"), ph1 = Ui.Hex("#e3ba70"), ph2 = Ui.Hex("#bc9146");
            Color r0 = Ui.Hex("#211210"), r1 = Ui.Hex("#180d0b"), r2 = Ui.Hex("#120908");
            Color f0 = Ui.Hex("#381815"), f1 = Ui.Hex("#28110f"), f2 = Ui.Hex("#1a0b09");
            var shape = Ui.Shape(rt, 0, 0, w, h, plate, primary ? p0 : r0, primary ? p1 : r1, primary ? p2 : r2, false, "Plate");
            var border = Ui.Stroke(rt, 0, 0, w, h, plate, 1.4f, primary ? Ui.A(Ui.Hex("#fff0c8"), 0.55f) : Ui.A(Ui.GoldDim, 0.8f), -1.4f, "Border");
            Ui.Box(rt, bevel + 2f, 1.5f, w - (bevel + 2f) * 2f, 1f, primary ? Ui.A(Color.white, 0.35f) : Ui.A(Ui.Bone, 0.08f), "TopEdge");
            var edge = Ui.Ring(rt, w, h, plate, 2.5f, 2f);
            Ui.Label(rt, label.ToUpperInvariant(), UiFont.Cond, primary ? 23 : 21, primary ? Ui.Hex("#160a07") : Ui.Bone, 0, 0, w, h, TextAnchor.MiddleCenter, primary ? 3.4f : 2.8f);
            var sheen = primary ? ButtonSheen.Add(rt, w, h) : null;
            var tween = UiFocusTween.On(rt, 0.14f, v =>
            {
                rt.localScale = Vector3.one * (1f + 0.018f * v);
                edge.color = Ui.A(Ui.Gold, 0.92f * v);
                if (primary)
                {
                    shape.SetColors(Color.Lerp(p0, ph0, v), Color.Lerp(p1, ph1, v), Color.Lerp(p2, ph2, v));
                }
                else
                {
                    shape.SetColors(Color.Lerp(r0, f0, v), Color.Lerp(r1, f1, v), Color.Lerp(r2, f2, v));
                    border.color = Color.Lerp(Ui.A(Ui.GoldDim, 0.8f), Ui.Gold, v);
                }
            });
            var item = Make(rt, nav, key, () => { if (sheen != null) sheen.Play(0.32f); onSubmit(); }, f =>
            {
                tween.Set(f);
                if (f && sheen != null) sheen.Play(0.45f);
            });
            item.press = 0.96f;
            return item;
        }

        // ---------------- segmented control ----------------

        /// <summary>JS .w-seg: bordered options, the active one gold. Returns the items in order.</summary>
        public static NavItem[] Seg(Transform p, NavGroup nav, string keyPrefix, float rightEdge, float y, string[] labels, int active, Action<int> onPick, bool[] enabled = null)
        {
            var widths = new float[labels.Length];
            float total = 2f;
            for (int i = 0; i < labels.Length; i++)
            {
                var probe = Ui.Label(p, labels[i].ToUpperInvariant(), UiFont.Title, 14, Ui.Muted, 0, 0, 300, 42, TextAnchor.MiddleLeft, 1.96f);
                widths[i] = Width(probe, 1.96f) + 40f;
                UnityEngine.Object.Destroy(probe.gameObject);
                total += widths[i];
            }
            float x = rightEdge - total;
            var rt = Ui.Node(p, "Seg", x, y, total, 42);
            Ui.Box(rt, 0, 0, total, 42, Ui.PanelA(0.75f));
            Ui.Frame(rt, 0, 0, total, 42, 1, Ui.A(Ui.GoldDim, 0.9f));
            var items = new NavItem[labels.Length];
            float cx = 1f;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                bool on = i == active;
                bool usable = enabled == null || enabled[i];
                var cell = Ui.Node(rt, "Opt" + i, cx, 1, widths[i], 40);
                if (i > 0) Ui.Box(rt, cx, 6, 1, 30, Ui.Hair);
                var bg = Ui.Box(cell, 0, 0, widths[i], 40, on ? Ui.Gold : Color.clear);
                var txt = Ui.Label(cell, labels[i].ToUpperInvariant(), UiFont.Title, 14, on ? Ui.InkOnGold : (usable ? Ui.Muted : Ui.A(Ui.Muted, 0.4f)), 0, 0, widths[i], 40, TextAnchor.MiddleCenter, 1.96f);
                var ring = Ui.Frame2(cell, widths[i], 40);
                items[i] = Make(cell, nav, keyPrefix + ":" + i, () => onPick(idx), f =>
                {
                    if (on) { bg.color = f ? Ui.Hex("#dfc07a") : Ui.Gold; }
                    else
                    {
                        bg.color = f ? Ui.A(Ui.Gold, 0.14f) : Color.clear;
                        txt.color = f ? Ui.Bone : Ui.Muted;
                    }
                    ring.color = f ? Ui.Hex("#f7e0a8") : Color.clear;
                }, usable);
                cx += widths[i];
            }
            return items;
        }

        // ---------------- chips ----------------

        /// <summary>The width <see cref="Chip"/> will have, so chips can be laid out from the right.</summary>
        public static float ChipWidth(string key, string value, bool dot)
        {
            var probe = Ui.Node(null, "Probe", 0, 0, 10, 10);
            var v = Ui.Label(probe, value, UiFont.Title, 15, Color.white, 0, 0, 300, 38, TextAnchor.MiddleLeft);
            float w = 14f + (dot ? 26f : 0f) + Width(v) + 14f;
            if (!string.IsNullOrEmpty(key))
            {
                var k = Ui.Label(probe, key.ToUpperInvariant(), UiFont.Cond, 14, Color.white, 0, 0, 200, 38, TextAnchor.MiddleLeft, 2.8f);
                w += Width(k, 2.8f) + 10f;
            }
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            return w;
        }

        /// <summary>JS .w-chip: a small bordered tag. Returns its width.</summary>
        public static float Chip(Transform p, float x, float y, string key, string value, bool gold = false, bool dot = false)
        {
            var v = Ui.Label(p, value, UiFont.Title, 15, gold ? Ui.Gold : Ui.Bone, 0, 0, 300, 38, TextAnchor.MiddleLeft);
            float vw = Width(v);
            Text k = null;
            float kw = 0f;
            if (!string.IsNullOrEmpty(key))
            {
                k = Ui.Label(p, key.ToUpperInvariant(), UiFont.Cond, 14, Ui.Muted, 0, 0, 200, 38, TextAnchor.MiddleLeft, 2.8f);
                kw = Width(k, 2.8f) + 10f;
            }
            float icon = dot ? 26f : 0f;
            float w = 14f + icon + kw + vw + 14f;
            var rt = Ui.Node(p, "Chip", x, y, w, 38);
            Ui.Box(rt, 0, 0, w, 38, Ui.PanelA(0.72f)).transform.SetAsFirstSibling();
            Ui.Frame(rt, 0, 0, w, 38, 1, gold ? Ui.GoldDim : Ui.Hair);
            v.transform.SetParent(rt, false);
            v.rectTransform.anchoredPosition = new Vector2(14 + icon + kw, 0);
            if (k != null) { k.transform.SetParent(rt, false); k.rectTransform.anchoredPosition = new Vector2(14 + icon, 0); }
            if (dot) Ui.Diamond(rt, 14 + 8, 19, 9, gold ? Ui.Gold : Ui.Acc, gold ? Ui.Gold : Ui.Acc, 0);
            return w;
        }

        /// <summary>JS .w-pips: three thin bars, <paramref name="lit"/> of them red.</summary>
        public static void Pips(Transform p, float x, float y, int lit, float gap = 5f)
        {
            for (int i = 0; i < 3; i++) Ui.Box(p, x + i * (22 + gap), y, 22, 4, i < lit ? Ui.Acc : Ui.Hair, "Pip");
        }

        /// <summary>JS .w-sec: a section title with a red diamond.</summary>
        public static void Section(Transform p, float x, float y, string title, string kanji)
        {
            Ui.Diamond(p, x + 4.5f, y + 11, 6.4f, Ui.Acc, Ui.Acc, 0);
            var t = Ui.Label(p, title.ToUpperInvariant(), UiFont.Title, 15, Ui.Gold, x + 21, y, 400, 22, TextAnchor.MiddleLeft, 4.5f);
            float tw = Width(t, 4.5f);
            if (!string.IsNullOrEmpty(kanji)) Ui.Label(p, kanji, UiFont.Mincho, 14, Ui.Muted, x + 21 + tw + 16, y, 100, 22, TextAnchor.MiddleLeft);
        }
    }
}
