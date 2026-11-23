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
            var dia = Ui.Diamond(rt, 29, 29, 40, Ui.PanelA(0.8f), Ui.GoldDim, 1.5f, true);
            var chevron = Ui.Line(rt, new[] { new Vector2(32.2f, 37f), new Vector2(26.7f, 29f), new Vector2(32.2f, 21f) }, 2f, Ui.Bone);
            chevron.rectTransform.sizeDelta = new Vector2(58, 58);
            return Make(rt, nav, "back", onSubmit, f =>
            {
                dia.SetColors(f ? Ui.Mix(Ui.Acc, Ui.Panel, 0.3f) : Ui.PanelA(0.8f), f ? Ui.Gold : Ui.GoldDim);
                rt.localScale = Vector3.one * (f ? 1.08f : 1f);
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
            Ui.Box(rt, 0, 0, w, 44, Ui.PanelA(0.78f));
            Ui.Frame(rt, 0, 0, w, 44, 1, Ui.GoldDim);
            if (coin)
            {
                Ui.Disc(rt, 25, 22, 10, Ui.Gold);
                Ui.Disc(rt, 25, 22, 6, Ui.Mix(Ui.Gold, Ui.Panel, 0.45f));
                Ui.Disc(rt, 25, 22, 3.2f, Ui.Gold);
            }
            else Ui.Diamond(rt, 25, 22, 14, Ui.Ruby, Ui.Ruby, 0);
            Ui.Label(rt, value, UiFont.Title, 20, Ui.Bone, 50, 6, w - 90, 32, TextAnchor.MiddleLeft);
            // the + button
            float px = w - 36f;
            Ui.Box(rt, px + 14, 22 - 1, 2, 2, Color.clear);
            Ui.Box(rt, px + 7, 21, 16, 2, Ui.Gold);
            Ui.Box(rt, px + 14, 14, 2, 16, Ui.Gold);
        }

        /// <summary>JS wKeys: key cap and label pairs.</summary>
        public static void Keys(Transform p, float x, float y, params string[][] keys)
        {
            var rt = Ui.Node(p, "Keys", x, y, 700, 48);
            float cx = 0f;
            foreach (var k in keys)
            {
                var cap = Ui.Label(rt, k[0], UiFont.Cond, 13, Ui.Gold, 0, 0, 200, 26, TextAnchor.MiddleLeft, 1.56f);
                float kw = Width(cap, 1.56f) + 16f + 2f;
                cap.rectTransform.anchoredPosition = new Vector2(cx + 9f, -11f);
                Ui.Box(rt, cx, 11, kw, 26, Ui.PanelA(0.8f)).transform.SetAsFirstSibling();
                Ui.Frame(rt, cx, 11, kw, 26, 1, Ui.GoldDim);
                var lb = Ui.Label(rt, k[1], UiFont.Title, 14, Ui.Muted, cx + kw + 8f, 11, 200, 26, TextAnchor.MiddleLeft);
                cx += kw + 8f + Width(lb) + 22f;
            }
        }

        // ---------------- brush button ----------------

        static readonly float[] BrushClip = { 2, 16, 10, 4, 60, 0, 94, 6, 100, 28, 95, 50, 100, 74, 92, 96, 40, 100, 6, 94, 0, 70, 4, 48 };

        /// <summary>
        /// The red brush button (Fight): kanji and a tracked label on a torn brush stroke. Locked shows it grey.
        /// <paramref name="kanji"/> may be empty.
        /// </summary>
        public static NavItem Brush(Transform p, NavGroup nav, string key, float x, float y, float w, float h, string kanji, string label,
            int labelSize, int kanjiSize, float tracking, bool locked, Action onSubmit)
        {
            var rt = Ui.Node(p, "Brush_" + key, x, y, w, h);
            var glow = Ui.Glow(rt, w / 2, h / 2, w * 0.62f, Ui.A(Ui.Acc, locked ? 0f : 0.16f), 2f);
            glow.rectTransform.localScale = new Vector3(1f, h / w * 1.9f, 1f);
            var shape = Ui.Shape(rt, 0, 0, w, h, Ui.Poly(w, h, BrushClip),
                locked ? Ui.Hex("#2a2420") : Ui.Deep, locked ? Ui.Hex("#3a332c") : Ui.Acc, locked ? Ui.Hex("#2a2420") : Ui.Deep, true, "Stroke");
            Color text = locked ? Ui.Muted : Ui.Bone;
            var lb = Ui.Label(rt, label, UiFont.Title, labelSize, text, 0, 0, w, h, TextAnchor.MiddleCenter, tracking);
            float lw = Width(lb, tracking);
            float kw = 0f;
            Text kj = null;
            if (!string.IsNullOrEmpty(kanji))
            {
                kj = Ui.Label(rt, kanji, UiFont.Brush, kanjiSize, text, 0, 0, kanjiSize + 8, h, TextAnchor.MiddleCenter);
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
            Color d0 = Ui.Deep, d1 = Ui.Acc, d2 = Ui.Deep, h0 = Ui.Acc, h1 = Ui.Hot, h2 = Ui.Acc;
            var tween = UiFocusTween.On(rt, 0.14f, v =>
            {
                if (locked) return;
                rt.localScale = Vector3.one * (1f + 0.03f * v);
                shape.SetColors(Color.Lerp(d0, h0, v), Color.Lerp(d1, h1, v), Color.Lerp(d2, h2, v));
                glow.color = Ui.A(Ui.Acc, Mathf.Lerp(0.16f, 0.4f, v));
                glow.SetVerticesDirty();
            });
            var brushItem = Make(rt, nav, key, () => { sheen.Play(0.32f); onSubmit(); }, f =>
            {
                tween.Set(f && !locked);
                if (f && !locked) sheen.Play(0.55f);
            }, !locked);
            brushItem.press = 0.97f;
            return brushItem;
        }

        // ---------------- overlay buttons ----------------

        static readonly float[] StrokeClip = { 0, 22, 3, 6, 40, 0, 97, 8, 100, 40, 98, 70, 100, 92, 60, 100, 5, 94, 0, 66 };

        /// <summary>
        /// The pause and confirm buttons (JS .btn-primary / .btn-ghost under wonder.css): a brush stroke, gold for the main choice,
        /// dark red for the others.
        /// </summary>
        public static NavItem Plate(Transform p, NavGroup nav, string key, float x, float y, float w, float h, string label, bool primary, Action onSubmit)
        {
            var rt = Ui.Node(p, "Plate_" + key, x, y, w, h);
            var shape = Ui.Shape(rt, 0, 0, w, h, Ui.Poly(w, h, StrokeClip),
                primary ? Ui.Hex("#fbe9c4") : Ui.Hex("#2a1210"), primary ? Ui.Hex("#e1b974") : Ui.Hex("#1c0c0a"), primary ? Ui.Hex("#b98c45") : Ui.Hex("#120807"), false);
            var edge = Ui.Frame2(rt, w, h);
            var t = Ui.Label(rt, label.ToUpperInvariant(), UiFont.Cond, primary ? 23 : 21, primary ? Ui.Hex("#1a0907") : Ui.Hex("#f3d9a8"), 0, 0, w, h, TextAnchor.MiddleCenter, primary ? 3.6f : 3f);
            var sheen = primary ? ButtonSheen.Add(rt, w, h) : null;
            Color r0 = Ui.Hex("#2a1210"), r1 = Ui.Hex("#1c0c0a"), r2 = Ui.Hex("#120807");
            Color f0 = Ui.Mix(Ui.Acc, Ui.Hex("#2a1210"), 0.35f), f1 = Ui.Hex("#3a1612"), f2 = Ui.Hex("#1c0c0a");
            var tween = UiFocusTween.On(rt, 0.16f, v =>
            {
                rt.localScale = Vector3.one * (1f + 0.02f * v);
                edge.color = Ui.A(Ui.Gold, 0.9f * v);
                if (!primary) shape.SetColors(Color.Lerp(r0, f0, v), Color.Lerp(r1, f1, v), Color.Lerp(r2, f2, v));
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
            Ui.Frame(rt, 0, 0, total, 42, 1, Ui.GoldDim);
            var items = new NavItem[labels.Length];
            float cx = 1f;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                bool on = i == active;
                bool usable = enabled == null || enabled[i];
                var cell = Ui.Node(rt, "Opt" + i, cx, 1, widths[i], 40);
                var bg = Ui.Box(cell, 0, 0, widths[i], 40, on ? Ui.Gold : Color.clear);
                var txt = Ui.Label(cell, labels[i].ToUpperInvariant(), UiFont.Title, 14, on ? Ui.InkOnGold : (usable ? Ui.Muted : Ui.A(Ui.Muted, 0.4f)), 0, 0, widths[i], 40, TextAnchor.MiddleCenter, 1.96f);
                var ring = Ui.Box(cell, 3, 3, widths[i] - 6, 34, Color.clear);
                items[i] = Make(cell, nav, keyPrefix + ":" + i, () => onPick(idx), f =>
                {
                    if (on) { bg.color = f ? Ui.Hex("#d9b46c") : Ui.Gold; }
                    else
                    {
                        bg.color = f ? Ui.A(Ui.Gold, 0.12f) : Color.clear;
                        txt.color = f ? Ui.Bone : Ui.Muted;
                    }
                    ring.color = on && f ? Ui.A(Ui.InkOnGold, 0.25f) : Color.clear;
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
