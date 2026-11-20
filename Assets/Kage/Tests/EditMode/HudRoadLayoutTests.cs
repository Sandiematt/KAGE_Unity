using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The road HUD's layout (SAN-108): on a road stage "ENCOUNTERS 0/3" read over Kage's health number. The pieces are placed from
    /// <see cref="HudLayoutRects"/> (JS stage pixels, y down), so these tests read the same rectangles the HUD draws.
    /// </summary>
    public class HudRoadLayoutTests
    {
        [Test]
        public void The_encounters_label_does_not_overlap_Kages_health_number()
        {
            Assert.IsFalse(HudLayoutRects.RoadLabel.Overlaps(HudLayoutRects.KageHealthValue),
                HudLayoutRects.RoadLabel + " against " + HudLayoutRects.KageHealthValue);
        }

        [Test]
        public void The_encounters_label_does_not_overlap_the_foes_health_number_either()
        {
            Assert.IsFalse(HudLayoutRects.RoadLabel.Overlaps(HudLayoutRects.FoeHealthValue),
                HudLayoutRects.RoadLabel + " against " + HudLayoutRects.FoeHealthValue);
        }

        [Test]
        public void The_label_is_centred_under_the_strip_and_below_its_track()
        {
            var label = HudLayoutRects.RoadLabel;
            Assert.AreEqual(HudLayoutRects.StageW / 2f, label.center.x, 0.01f, "centred on the strip, which is centred on the stage");
            Assert.GreaterOrEqual(label.yMin, HudLayoutRects.RoadTop + 8f, "under the 8 px track");
        }

        [Test]
        public void The_label_box_clears_both_health_numbers_by_a_margin()
        {
            var label = HudLayoutRects.RoadLabel;
            Assert.GreaterOrEqual(label.xMin - HudLayoutRects.KageHealthValue.xMax, 4f);
            Assert.GreaterOrEqual(HudLayoutRects.FoeHealthValue.xMin - label.xMax, 4f);
        }

        [Test]
        public void Kages_health_number_is_where_the_panel_puts_it()
        {
            // a known place: the right end of the bar, 462.4 to 500.8 across (JS panel at 28.8, medal 73.6, gap 14.4, bar 336, gap 9.6)
            var v = HudLayoutRects.KageHealthValue;
            Assert.AreEqual(462.4f, v.xMin, 0.01f);
            Assert.AreEqual(500.8f, v.xMax, 0.01f);
        }
    }
}
