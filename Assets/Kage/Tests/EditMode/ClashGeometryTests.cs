using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The pure maths of a weapon clash (SAN-93): segment intersection and whether two blades' swept paths cross in one
    /// frame (<see cref="ClashGeometry"/>). Expected values are worked out by hand in metres.
    /// </summary>
    public class ClashGeometryTests
    {
        // ---------------------------------------------------------------- segments

        [Test]
        public void Two_segments_in_an_X_cross_in_the_middle()
        {
            Assert.IsTrue(ClashGeometry.SegmentsCross(new Vector2(0, 0), new Vector2(2, 2), new Vector2(0, 2), new Vector2(2, 0), out var p));
            Assert.AreEqual(1f, p.x, 1e-5f);
            Assert.AreEqual(1f, p.y, 1e-5f);
        }

        [Test]
        public void Parallel_segments_apart_do_not_cross()
        {
            Assert.IsFalse(ClashGeometry.SegmentsCross(new Vector2(0, 0), new Vector2(2, 0), new Vector2(0, 0.01f), new Vector2(2, 0.01f), out _));
        }

        [Test]
        public void Segments_whose_lines_cross_beyond_an_end_do_not_cross()
        {
            // the lines meet at (1, 0), 1 cm past the end of the first
            Assert.IsFalse(ClashGeometry.SegmentsCross(new Vector2(0, 0), new Vector2(0.99f, 0), new Vector2(1, -1), new Vector2(1, 1), out _));
        }

        [Test]
        public void Touching_ends_count_as_crossing()
        {
            Assert.IsTrue(ClashGeometry.SegmentsCross(new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 1), out var p));
            Assert.AreEqual(1f, p.x, 1e-5f);
            Assert.AreEqual(0f, p.y, 1e-5f);
        }

        [Test]
        public void Overlapping_collinear_segments_cross_at_the_middle_of_the_overlap()
        {
            // two spears thrust at each other along y = 1: [0, 2] and [1, 3] share [1, 2]
            Assert.IsTrue(ClashGeometry.SegmentsCross(new Vector2(0, 1), new Vector2(2, 1), new Vector2(3, 1), new Vector2(1, 1), out var p));
            Assert.AreEqual(1.5f, p.x, 1e-5f);
            Assert.AreEqual(1f, p.y, 1e-5f);
        }

        [Test]
        public void Collinear_segments_that_do_not_overlap_do_not_cross()
        {
            Assert.IsFalse(ClashGeometry.SegmentsCross(new Vector2(0, 1), new Vector2(1, 1), new Vector2(1.5f, 1), new Vector2(3, 1), out _));
        }

        [Test]
        public void A_point_is_not_a_segment()
        {
            Assert.IsFalse(ClashGeometry.SegmentsCross(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(2, 0), out _));
        }

        // ---------------------------------------------------------------- blades

        /// <summary>A blade of <paramref name="n"/> samples up a vertical line from y0 to y1; the line moves from x0 (start of the frame) to x1 (end).</summary>
        static void VerticalBlade(List<HitSweep> into, float x0, float x1, float y0, float y1, int n = 5)
        {
            for (int k = 0; k < n; k++)
            {
                float y = Mathf.Lerp(y0, y1, (k + 1) / (float)n);
                into.Add(new HitSweep { point = HitPoint.Blade, sample = k, from = new Vector2(x0, y), to = new Vector2(x1, y), radius = 0.09f });
            }
        }

        /// <summary>A still blade lying along y from x0 to x1.</summary>
        static void HorizontalBlade(List<HitSweep> into, float y, float x0, float x1, int n = 5)
        {
            for (int k = 0; k < n; k++)
            {
                float x = Mathf.Lerp(x0, x1, (k + 1) / (float)n);
                into.Add(new HitSweep { point = HitPoint.Blade, sample = k, from = new Vector2(x, y), to = new Vector2(x, y), radius = 0.09f });
            }
        }

        [Test]
        public void Two_still_blades_that_cross_clash()
        {
            // A upright at x = 1; B lies across it, from x 0.5 to 1.5 at y = 0.6
            var s = new List<HitSweep>();
            VerticalBlade(s, 1f, 1f, 0.2f, 1f);
            HorizontalBlade(s, 0.6f, 0.5f, 1.5f);
            Assert.IsTrue(ClashGeometry.BladesCross(s, 0, 5, 5, 10, out var c));
            Assert.AreEqual(1f, c.x, 1e-4f);
            Assert.AreEqual(0.6f, c.y, 1e-4f);
        }

        [Test]
        public void A_blade_swung_through_a_still_one_clashes_though_neither_line_crosses_the_other_at_the_ends_of_the_frame()
        {
            // A stands at x = 1. B is parallel to it, at x = 0.5 on the last frame and x = 1.5 on this one: it jumped over A.
            var s = new List<HitSweep>();
            VerticalBlade(s, 1f, 1f, 0.2f, 1f);
            VerticalBlade(s, 0.5f, 1.5f, 0.2f, 1f);
            Assert.IsTrue(ClashGeometry.BladesCross(s, 0, 5, 5, 10, out var c));
            Assert.AreEqual(1f, c.x, 1e-4f, "where B's path passes A's line");
        }

        [Test]
        public void A_near_miss_is_not_a_clash()
        {
            // B stops 10 cm short of A's line (x = 0.9 against x = 1)
            var s = new List<HitSweep>();
            VerticalBlade(s, 1f, 1f, 0.2f, 1f);
            VerticalBlade(s, 0.5f, 0.9f, 0.2f, 1f);
            Assert.IsFalse(ClashGeometry.BladesCross(s, 0, 5, 5, 10, out _));
        }

        [Test]
        public void Blades_at_different_heights_do_not_clash()
        {
            // A reaches from 0.5 to 1.0 m; B is a low sweep at 0.1 m crossing A's x
            var s = new List<HitSweep>();
            VerticalBlade(s, 1f, 1f, 0.5f, 1f);
            HorizontalBlade(s, 0.1f, 0.5f, 1.5f);
            Assert.IsFalse(ClashGeometry.BladesCross(s, 0, 5, 5, 10, out _));
        }

        // ---------------------------------------------------------------- which samples are blades

        static HitSweep Sample(HitPoint p, int k, HitWindow w) => new() { point = p, sample = k, window = w, from = Vector2.zero, to = Vector2.zero };

        [Test]
        public void A_twin_window_gives_one_blade_for_the_far_sword_and_one_for_the_near_sword()
        {
            var w = new HitWindow { points = new[] { HitPoint.Blade, HitPoint.BladeN } };
            var s = new List<HitSweep>();
            for (int k = 0; k < 5; k++) s.Add(Sample(HitPoint.Blade, k, w));
            for (int k = 5; k < 10; k++) s.Add(Sample(HitPoint.BladeN, k, w));
            var runs = new List<ClashGeometry.BladeRun>();
            ClashGeometry.CollectBlades(s, runs);
            Assert.AreEqual(2, runs.Count);
            Assert.AreEqual((0, 5), (runs[0].start, runs[0].end), "the far blade");
            Assert.AreEqual((5, 10), (runs[1].start, runs[1].end), "the near blade (BladeN) clashes like the far one");
        }

        [Test]
        public void Fist_hilt_foot_shin_and_knee_samples_are_never_blades()
        {
            var w = new HitWindow();
            var s = new List<HitSweep>();
            foreach (var p in new[] { HitPoint.Fist, HitPoint.Hilt, HitPoint.FistF, HitPoint.FootN, HitPoint.FootF, HitPoint.ShinN, HitPoint.ShinF, HitPoint.KneeN })
                s.Add(Sample(p, s.Count, w));
            var runs = new List<ClashGeometry.BladeRun>();
            ClashGeometry.CollectBlades(s, runs);
            Assert.IsEmpty(runs);
        }

        [Test]
        public void A_window_that_mixes_a_fist_and_a_blade_only_clashes_with_the_blade()
        {
            var w = new HitWindow();
            var s = new List<HitSweep> { Sample(HitPoint.Fist, 0, w), Sample(HitPoint.Blade, 1, w), Sample(HitPoint.Blade, 2, w) };
            var runs = new List<ClashGeometry.BladeRun>();
            ClashGeometry.CollectBlades(s, runs);
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual((1, 3), (runs[0].start, runs[0].end));
        }

        [Test]
        public void Two_windows_of_one_move_are_two_blades()
        {
            var w1 = new HitWindow();
            var w2 = new HitWindow();
            var s = new List<HitSweep> { Sample(HitPoint.Blade, 0, w1), Sample(HitPoint.Blade, 1, w1), Sample(HitPoint.Blade, 0, w2), Sample(HitPoint.Blade, 1, w2) };
            var runs = new List<ClashGeometry.BladeRun>();
            ClashGeometry.CollectBlades(s, runs);
            Assert.AreEqual(2, runs.Count);
        }

        [Test]
        public void The_result_does_not_depend_on_which_blade_is_asked_first()
        {
            var s = new List<HitSweep>();
            VerticalBlade(s, 1f, 1f, 0.2f, 1f);
            VerticalBlade(s, 0.5f, 1.5f, 0.2f, 1f);
            Assert.AreEqual(ClashGeometry.BladesCross(s, 0, 5, 5, 10, out _), ClashGeometry.BladesCross(s, 5, 10, 0, 5, out _));
        }

        [Test]
        public void The_same_sweeps_always_give_the_same_contact()
        {
            var s = new List<HitSweep>();
            VerticalBlade(s, 1f, 1f, 0.2f, 1f);
            VerticalBlade(s, 0.5f, 1.5f, 0.2f, 1f);
            ClashGeometry.BladesCross(s, 0, 5, 5, 10, out var c1);
            ClashGeometry.BladesCross(s, 0, 5, 5, 10, out var c2);
            Assert.AreEqual(c1, c2);
        }
    }
}
