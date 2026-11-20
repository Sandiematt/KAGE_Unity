using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The timeline of the JS ink wipe (UIManager.go, styles.css <c>#wipe</c>) as plain data: the brush stroke slides in from the left in
    /// .225 s, the screen changes behind it 235 ms after the start, then it slides off to the right in .225 s. Positions are shares of the
    /// stroke's own width, the way the CSS <c>translateX(%)</c> gives them: -1.2 off the left edge, 0 covering the screen, +1.2 off the right.
    /// </summary>
    public static class UiWipe
    {
        public const float SlideSeconds = .225f;
        /// <summary>The moment the screen changes under the stroke (the JS setTimeout of 235 ms).</summary>
        public const float RevealAt = .235f;
        /// <summary>When the stroke has left the screen.</summary>
        public const float Total = RevealAt + SlideSeconds + .015f;

        public static float CurtainShare(float time)
        {
            if (time <= 0f) return -1.2f;
            if (time < SlideSeconds) return -1.2f + 1.2f * UiEase.Wipe.Evaluate(time / SlideSeconds);
            if (time < RevealAt) return 0f;
            float k = Math.Min(1f, (time - RevealAt) / SlideSeconds);
            return 1.2f * UiEase.Wipe.Evaluate(k);
        }

        public static bool Covering(float time) { return time >= SlideSeconds && time < RevealAt + 0.02f; }

        public static bool Finished(float time) { return time >= Total; }
    }

    /// <summary>
    /// The ink wipe over the whole screen: the black ragged brush stroke of the JS <c>#wipe</c> (a polygon with a red and an ash line), sliding
    /// across on <see cref="UiWipe"/>'s timeline. While it covers the screen the caller swaps what is under it (<see cref="Begin"/>'s callback).
    /// It never takes input: the player can already press things on the new screen.
    /// </summary>
    public sealed class ScreenWipe : MonoBehaviour
    {
        // the SVG's polygon (1700 x 900 viewBox, preserveAspectRatio none)
        static readonly float[] Stroke =
        {
            150, 0, 1560, 0, 1548, 60, 1590, 120, 1556, 210, 1600, 300, 1562, 380, 1610, 470, 1566, 560, 1604, 650, 1560, 740, 1596, 820, 1550, 900,
            120, 900, 150, 830, 96, 760, 140, 680, 100, 590, 146, 500, 92, 420, 138, 330, 100, 250, 150, 160, 104, 80,
        };

        RectTransform canvasRoot, node, art;
        float startedAt;
        bool running, revealed;
        Action onCovered;

        public bool Running { get { return running; } }

        public static ScreenWipe Create(RectTransform canvasRoot)
        {
            var go = new GameObject("Wipe", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var w = go.AddComponent<ScreenWipe>();
            w.canvasRoot = canvasRoot;
            w.node = rt;
            var art = Ui.Node(rt, "Art", 0, 0, 1700, 900);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = Vector2.zero;
            var pts = new List<Vector2>();
            for (int i = 0; i < Stroke.Length; i += 2) pts.Add(new Vector2(Stroke[i], Stroke[i + 1]));
            Ui.Shape(art, 0, 0, 1700, 900, pts.ToArray(), Ui.Hex("#030303"), null, null, false, "Ink");
            var red = Ui.Line(art, new[] { new Vector2(150, 60), new Vector2(1540, 40) }, 6f, Ui.A(Ui.Hex("#a3302a"), 0.55f));
            red.rectTransform.sizeDelta = new Vector2(1700, 900);
            var ash = Ui.Line(art, new[] { new Vector2(120, 860), new Vector2(1500, 880) }, 10f, Ui.A(Ui.Hex("#3a342b"), 0.7f));
            ash.rectTransform.sizeDelta = new Vector2(1700, 900);
            w.art = art;
            go.SetActive(false);
            return w;
        }

        /// <summary>Starts a wipe; <paramref name="covered"/> runs when the stroke covers the screen (the screen change goes there).</summary>
        public void Begin(Action covered)
        {
            if (running) Finish(true);
            onCovered = covered;
            startedAt = UiClock.Now;
            running = true;
            revealed = false;
            gameObject.SetActive(true);
            Place(UiWipe.CurtainShare(0f));
        }

        /// <summary>Ends the wipe at once (a new one starts, or the flow is torn down). The cover callback runs if it has not yet.</summary>
        public void Finish(bool runCallback)
        {
            if (!running) return;
            running = false;
            gameObject.SetActive(false);
            if (!revealed && runCallback) { revealed = true; var cb = onCovered; onCovered = null; if (cb != null) cb(); }
        }

        void Update()
        {
            if (!running) return;
            float t = UiClock.Now - startedAt;
            if (!revealed && t >= UiWipe.RevealAt - 0.005f)
            {
                revealed = true;
                var cb = onCovered; onCovered = null;
                if (cb != null) cb();
            }
            if (UiWipe.Finished(t)) { Finish(true); return; }
            Place(UiWipe.CurtainShare(t));
        }

        void Place(float share)
        {
            Rect r = canvasRoot.rect;
            float w = r.width * 1.2f, h = r.height * 1.04f;
            node.sizeDelta = new Vector2(w, h);
            node.anchoredPosition = new Vector2(share * w, 0f);
            art.localScale = new Vector3(w / 1700f, h / 900f, 1f);
        }
    }
}
