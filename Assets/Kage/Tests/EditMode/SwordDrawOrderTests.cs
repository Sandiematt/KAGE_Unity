using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// SAN-114: two parts of the katana that overlap must never share a draw order. With the same sorting order Unity draws them in
    /// whatever order it picks, and that changed between renders in one session: the habaki showed over the tsuba by a pixel or two
    /// in <c>CharacterScreenshots</c> after a weapon family test class had run. Seam: <see cref="KageModel.Build"/>, the sword parts'
    /// polygons (bind space, rigid on the Sword bone) and their draw slot (<see cref="PartDef.order"/>, <see cref="PartDef.underSlot"/>).
    /// </summary>
    public class SwordDrawOrderTests
    {
        static List<PartDef> SwordParts(KageModel.Look look) =>
            KageModel.Build(look).parts.Where(p => p.bone == B.Sword && p.pts != null && p.pts.Length >= 3).ToList();

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static Rect Bounds(Vector2[] poly)
        {
            float x0 = poly.Min(v => v.x), x1 = poly.Max(v => v.x), y0 = poly.Min(v => v.y), y1 = poly.Max(v => v.y);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>Points of a 0.25 mm grid inside both polygons.</summary>
        static int SharedSamples(Vector2[] a, Vector2[] b)
        {
            Rect ra = Bounds(a), rb = Bounds(b);
            float x0 = Mathf.Max(ra.xMin, rb.xMin), x1 = Mathf.Min(ra.xMax, rb.xMax), y0 = Mathf.Max(ra.yMin, rb.yMin), y1 = Mathf.Min(ra.yMax, rb.yMax);
            if (x0 >= x1 || y0 >= y1) return 0;
            const float step = 0.00025f;
            int n = 0;
            for (int i = 0; i < 4000 && x0 + i * step < x1; i++)
                for (int j = 0; j < 4000 && y0 + j * step < y1; j++)
                {
                    var p = new Vector2(x0 + i * step, y0 + j * step);
                    if (Inside(a, p) && Inside(b, p)) n++;
                }
            return n;
        }

        [TestCase(KageModel.Look.Kage)]
        [TestCase(KageModel.Look.Ronin)]
        public void Overlapping_sword_parts_never_share_a_draw_order(KageModel.Look look)
        {
            var parts = SwordParts(look);
            Assert.Greater(parts.Count, 5, "the katana's parts");
            var ties = new List<string>();
            for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    PartDef a = parts[i], b = parts[j];
                    if (a.order != b.order || a.underSlot != b.underSlot) continue;
                    int shared = SharedSamples(a.pts, b.pts);
                    if (shared > 0) ties.Add($"{a.name} and {b.name} (order {a.order}, {shared} samples)");
                }
            CollectionAssert.IsEmpty(ties, "overlapping parts drawn at the same order");
        }

        [Test]
        public void The_habaki_is_drawn_under_the_tsuba_where_they_overlap()
        {
            var parts = SwordParts(KageModel.Look.Kage);
            var tsuba = parts.Single(p => p.name == "Tsuba");
            var habaki = parts.Single(p => p.name == "Habaki");
            Assert.Greater(SharedSamples(tsuba.pts, habaki.pts), 0, "they do overlap");
            Assert.AreEqual(tsuba.order, habaki.order, "one depth slot");
            Assert.Less(habaki.underSlot, tsuba.underSlot, "the tsuba on top, as in the SAN-66 before images");
        }
    }
}
