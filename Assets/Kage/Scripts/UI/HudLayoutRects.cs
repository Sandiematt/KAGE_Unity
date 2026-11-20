using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Where the fight HUD's pieces sit, in the JS stage's own pixels (1280 x 720, y down from the top edge), as plain numbers the
    /// HUD draws from and tests read (SAN-108). The road strip's label used to start at the strip's left edge, which put "ENCOUNTERS 0/3"
    /// over Kage's health number; it is now centred under the strip, between the two health numbers.
    /// </summary>
    public static class HudLayoutRects
    {
        public const float StageW = 1280f;

        // the two side panels (JS .hud-side: top 2.1rem, left / right 1.8rem in the flat pass)
        public const float PanelInset = 28.8f, PanelTop = 22.4f;
        public const float Medal = 73.6f, MedalGap = 14.4f, BodyW = 384f, PanelW = Medal + MedalGap + BodyW;
        public const float HpH = 18.4f, ValW = 38.4f, ValGap = 9.6f, TrackW = BodyW - ValW - ValGap, HpY = 30.4f;
        /// <summary>The body's height beside the medal: Kage's holds the Rage row under the bar, the foe's only the name and the bar.</summary>
        public const float KageBodyH = 72.8f, FoeBodyH = 48.8f;

        // the road strip, centred at the top (JS .hud-road: top 1.5rem, 24rem wide)
        public const float RoadTop = 24f, RoadW = 384f, RoadLabelY = 17.2f, RoadLabelH = 20f;
        /// <summary>The widest the road's label can read ("FOES 12 · WAVE 3/3") with room to spare; the label is centred in a box this wide.</summary>
        public const float RoadLabelW = 260f;

        static float ValueY(float bodyH) { return PanelTop + (Medal - bodyH) / 2f + HpY - 3f; }

        /// <summary>Kage's health number, at the right end of his bar.</summary>
        public static Rect KageHealthValue
        {
            get { return new Rect(PanelInset + Medal + MedalGap + TrackW + ValGap, ValueY(KageBodyH), ValW, HpH + 6f); }
        }

        /// <summary>The foe's health number, at the left end of the foe's bar (it shows in a duel or a boss fight, not on the road).</summary>
        public static Rect FoeHealthValue
        {
            get { return new Rect(StageW - PanelInset - PanelW, ValueY(FoeBodyH), ValW, HpH + 6f); }
        }

        /// <summary>The road label's box: centred on the strip, under its track.</summary>
        public static Rect RoadLabel
        {
            get { return new Rect((StageW - RoadLabelW) / 2f, RoadTop + RoadLabelY, RoadLabelW, RoadLabelH); }
        }
    }
}
