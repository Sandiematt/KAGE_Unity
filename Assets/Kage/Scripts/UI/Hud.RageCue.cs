using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The rage art's cue over a boss (HudRageCue.cs; the rule is Fighter.RageArtCounter.cs): a small gold diamond with 怒 in it, the RAGE READY
    /// pill's colours, a crimson beat behind it and a little chevron pointing down at the boss. It lights while Kage's Rage is full and the boss
    /// would take the art now, pops in at each opening and breathes while it lasts.
    /// </summary>
    public partial class Hud
    {
        RectTransform _rageCueRoot, _rageCueChevron;
        CanvasGroup _rageCueGroup;
        RadialGlow _rageCueBeat, _rageCueHalo;

        static readonly Color RageCueGoldHi = new(1f, 0.933f, 0.784f), RageCueGold = new(0.953f, 0.831f, 0.561f), RageCueGoldLo = new(0.788f, 0.643f, 0.361f);
        static readonly Color RageCueInk = new(0.102f, 0.035f, 0.027f), RageCueCrimson = new(1f, 0.25f, 0.2f);

        /// <summary>A diamond of half-width <paramref name="r"/> round the node's centre (ShapeGraphic points are y-down from the pivot).</summary>
        static Vector2[] Diamond(float r) => new[] { new Vector2(0f, -r), new Vector2(r, 0f), new Vector2(0f, r), new Vector2(-r, 0f) };

        void BuildRageCue()
        {
            // anchored at the stage's top-left like the call-outs, so the projected point (WorldToJsPoint) lands where it says
            var root = Ui.Node(_js, "RageCue", 0f, 0f, 46f, 46f);
            root.pivot = new Vector2(0.5f, 0.5f);
            _rageCueRoot = root;
            _rageCueGroup = root.gameObject.AddComponent<CanvasGroup>();
            _rageCueGroup.blocksRaycasts = false;
            _rageCueGroup.interactable = false;

            _rageCueBeat = CenteredChild(root, "Beat", 0f, 0f, 96f, 96f).gameObject.AddComponent<RadialGlow>();
            _rageCueBeat.raycastTarget = false;
            _rageCueBeat.power = 2.2f;
            _rageCueHalo = CenteredChild(root, "Halo", 0f, 0f, 58f, 58f).gameObject.AddComponent<RadialGlow>();
            _rageCueHalo.raycastTarget = false;
            _rageCueHalo.power = 1.8f;

            var rim = CenteredChild(root, "Rim", 0f, 0f, 40f, 40f).gameObject.AddComponent<ShapeGraphic>();
            rim.raycastTarget = false;
            rim.Set(Diamond(20f), RageCueInk, RageCueInk, RageCueInk, false);
            var face = CenteredChild(root, "Diamond", 0f, 0f, 36f, 36f).gameObject.AddComponent<ShapeGraphic>();
            face.raycastTarget = false;
            face.Set(Diamond(17.5f), RageCueGoldHi, RageCueGold, RageCueGoldLo, false);

            var kanji = CenteredChild(root, "Ikari", 0f, -0.5f, 30f, 30f).gameObject.AddComponent<Text>();
            kanji.font = Ui.FontOf(UiFont.Kanji);
            kanji.fontSize = 20;
            kanji.text = "怒";
            kanji.color = RageCueInk;
            kanji.alignment = TextAnchor.MiddleCenter;
            kanji.horizontalOverflow = HorizontalWrapMode.Overflow;
            kanji.verticalOverflow = VerticalWrapMode.Overflow;
            kanji.raycastTarget = false;

            _rageCueChevron = CenteredChild(root, "Chevron", 0f, -29f, 16f, 9f);
            var chev = _rageCueChevron.gameObject.AddComponent<ShapeGraphic>();
            chev.raycastTarget = false;
            chev.Set(new[] { new Vector2(-8f, -4.5f), new Vector2(8f, -4.5f), new Vector2(0f, 4.5f) }, RageCueGoldHi, RageCueGold, RageCueGoldLo, false);

            root.gameObject.SetActive(false);
        }

        void ApplyRageCue(HudRageCueView v)
        {
            if (_js == null) return;
            if (!v.Visible)
            {
                if (_rageCueRoot != null && _rageCueRoot.gameObject.activeSelf) _rageCueRoot.gameObject.SetActive(false);
                return;
            }
            if (_rageCueRoot == null) BuildRageCue();
            if (!_rageCueRoot.gameObject.activeSelf) _rageCueRoot.gameObject.SetActive(true);

            float stageW = _js.rect.width > 1f ? _js.rect.width : HudLayoutRects.StageW;
            float stageH = _js.rect.height > 1f ? _js.rect.height : 720f;
            Vector2 lp = WorldToJsPoint(v.World);
            lp.y += 3f * Mathf.Sin(v.Age * Mathf.PI * 2f * 1.1f);                 // a slow bob
            lp.x = Mathf.Clamp(lp.x, 30f, Mathf.Max(30f, stageW - 30f));
            lp.y = Mathf.Clamp(lp.y, -(stageH - 40f), -96f);                     // below the panels, above the floor
            _rageCueRoot.anchoredPosition = lp;
            float s = v.Scale;
            _rageCueRoot.localScale = new Vector3(s, s, 1f);
            _rageCueGroup.alpha = v.Alpha;

            float beat = v.Beat;
            _rageCueBeat.color = A(RageCueCrimson, 0.18f + 0.3f * beat);
            _rageCueHalo.color = A(RageCueGoldHi, 0.25f + 0.35f * beat);
            _rageCueChevron.anchoredPosition = new Vector2(0f, -29f - 3f * beat);    // it nods at the boss on the beat
        }
    }
}
