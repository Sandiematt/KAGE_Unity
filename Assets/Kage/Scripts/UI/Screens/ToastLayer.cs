using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The JS toasts (UIManager.toast, <c>#toasts</c>): small charcoal cards with a gold (or red) line on top that drop in from above at the top
    /// centre with a pop, stay 2.6 s and slip away upward in .3 s, stacked with a gap. Purchases, level-ups, unlocks and the results' rewards use them.
    /// </summary>
    public sealed class ToastLayer : MonoBehaviour
    {
        const float Stay = 2.6f, Gap = 12f, Top = 28f;

        sealed class Toast
        {
            public RectTransform node;
            public float height, x, bornAt;
            public bool leaving;
        }

        RectTransform stage;
        readonly List<Toast> toasts = new List<Toast>();

        public int Count { get { return toasts.Count; } }

        public static ToastLayer Create(RectTransform stage)
        {
            var go = new GameObject("Toasts", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(stage, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<ToastLayer>();
            t.stage = rt;
            return t;
        }

        /// <summary>Shows a toast: a small eyebrow over a title; <paramref name="red"/> for a refusal.</summary>
        public void Show(string eyebrow, string title, bool red = false)
        {
            var t = new Toast();
            var title_ = Ui.Label(stage, title, UiFont.Mincho, 27, Ui.Bone, 0, 0, 600, 36, TextAnchor.MiddleLeft);
            var eye = Ui.Label(stage, eyebrow.ToUpperInvariant(), UiFont.Title, 12, Ui.Gold, 0, 0, 600, 20, TextAnchor.MiddleLeft, 2.9f);
            float w = Mathf.Max(UiParts.Width(title_), UiParts.Width(eye, 2.9f)) + 52f;
            float h = 17f + 20f + 3f + 36f + 17f;
            var rt = Ui.Node(stage, "Toast", 800f - w / 2f, Top, w, h);
            Ui.Box(rt, 0, 0, w, h, Ui.Charcoal).transform.SetAsFirstSibling();
            Ui.Box(rt, 0, 0, w, 2, red ? Ui.Hot : Ui.Gold);
            Ui.Frame(rt, 0, 0, w, h, 1, Ui.A(Color.black, 0.35f));
            title_.transform.SetParent(rt, false);
            title_.rectTransform.anchoredPosition = new Vector2(26, -(17 + 20 + 3));
            eye.transform.SetParent(rt, false);
            eye.rectTransform.anchoredPosition = new Vector2(26, -17);
            if (red) eye.color = Ui.Hot;
            t.node = rt; t.height = h; t.x = 800f - w / 2f; t.bornAt = UiClock.Now;
            toasts.Add(t);
            Layout();
            UiFx.In(rt, UiAnims.ToastIn());
        }

        void Layout()
        {
            float y = Top;
            foreach (var t in toasts)
            {
                if (t.node == null) continue;
                var o = UiOffsets.Of(t.node);
                o.SetBase(new Vector2(t.x, -y));
                y += t.height + Gap;
            }
        }

        void Update()
        {
            bool dirty = false;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                if (t.node == null) { toasts.RemoveAt(i); dirty = true; continue; }
                if (!t.leaving && UiClock.Now - t.bornAt >= Stay)
                {
                    t.leaving = true;
                    var node = t.node;
                    var tt = t;
                    UiAnimator.Play(node, UiAnims.ToastOut(), () => { if (node != null) Destroy(node.gameObject); tt.node = null; });
                }
            }
            if (dirty) Layout();
        }
    }
}
