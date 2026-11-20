using NUnit.Framework;

namespace Kage.Tests
{
    // The JS UI's animation curves and timings as plain data (SAN-67 rework): a tween at time t gives a value. Expected numbers come from
    // the CSS (cubic-bezier values solved independently, the keyframes in styles.css and wonder.css) and from running the JS wPetals formulas,
    // not from the code under test.
    public class MotionTimingTests
    {
        const float Tol = 0.002f;

        [Test]
        public void The_JS_easing_curves_match_the_CSS_cubic_beziers()
        {
            Assert.AreEqual(0.72737f, UiEase.Ease.Evaluate(0.25f), Tol);
            Assert.AreEqual(0.92958f, UiEase.Ease.Evaluate(0.5f), Tol);
            Assert.AreEqual(0.98747f, UiEase.Ease.Evaluate(0.75f), Tol);
            Assert.AreEqual(0.91419f, UiEase.Pop.Evaluate(0.25f), Tol);
            Assert.AreEqual(1.11004f, UiEase.Pop.Evaluate(0.5f), Tol, "--pop overshoots");
            Assert.AreEqual(1.04412f, UiEase.Pop.Evaluate(0.75f), Tol);
            Assert.AreEqual(0.07080f, UiEase.Wipe.Evaluate(0.25f), Tol);
            Assert.AreEqual(0.5f, UiEase.Wipe.Evaluate(0.5f), Tol);
            Assert.AreEqual(0.92920f, UiEase.Wipe.Evaluate(0.75f), Tol);
            Assert.AreEqual(0f, UiEase.Pop.Evaluate(0f));
            Assert.AreEqual(1f, UiEase.Pop.Evaluate(1f));
        }

        [Test]
        public void A_rise_starts_18px_low_and_clear_and_ends_in_place_holding_before_its_delay_and_after_its_end()
        {
            var a = UiAnims.WRise(0.6f, 0.2f);
            var before = a.Sample(0f);
            Assert.AreEqual(0f, before.Alpha, 1e-5f, "CSS fill both: the first key holds through the delay");
            Assert.AreEqual(18f, before.Y, 1e-4f);
            var mid = a.Sample(0.2f + 0.3f);
            Assert.AreEqual(0.92958f, mid.Alpha, Tol);
            Assert.AreEqual(18f * (1f - 0.92958f), mid.Y, 0.05f);
            var end = a.Sample(5f);
            Assert.AreEqual(1f, end.Alpha, 1e-5f);
            Assert.AreEqual(0f, end.Y, 1e-4f);
            Assert.IsTrue(a.IsFinished(0.8f));
            Assert.IsFalse(a.IsFinished(0.79f));
        }

        [Test]
        public void A_card_pops_past_its_size_on_the_way_in()
        {
            var a = UiAnims.CardIn();
            Assert.AreEqual(0.95f, a.Sample(0f).Scale, 1e-4f);
            Assert.AreEqual(24f, a.Sample(0f).Y, 1e-4f);
            float max = 0f;
            for (int i = 0; i <= 50; i++) max = System.Math.Max(max, a.Sample(i * 0.01f).Scale);
            Assert.Greater(max, 1.003f, "the pop curve overshoots 1");
            Assert.AreEqual(1f, a.Sample(0.5f).Scale, 1e-4f);
        }

        [Test]
        public void The_VS_mark_drops_in_large_and_turned_and_settles()
        {
            var a = UiAnims.VsPop(.7f, .15f, 2f, -12f);
            var s = a.Sample(0f);
            Assert.AreEqual(2f, s.Scale, 1e-4f);
            Assert.AreEqual(-12f, s.Rot, 1e-4f);
            var e = a.Sample(1f);
            Assert.AreEqual(1f, e.Scale, 1e-4f);
            Assert.AreEqual(0f, e.Rot, 1e-4f);
        }

        [Test]
        public void A_glow_loop_goes_back_and_forth_between_80_and_100_percent()
        {
            var a = UiAnims.WGlow(5f);
            Assert.AreEqual(0.8f, a.Sample(0f).Alpha, 1e-4f);
            Assert.AreEqual(1f, a.Sample(2.5f).Alpha, 1e-4f);
            Assert.AreEqual(0.8f, a.Sample(5f).Alpha, 1e-4f);
            Assert.AreEqual(1f, a.Sample(7.5f).Alpha, 1e-4f);
            Assert.IsFalse(a.IsFinished(1000f), "a loop never finishes");
        }

        [Test]
        public void The_moon_breathes_between_55_and_100_percent_and_96_and_106_percent()
        {
            var a = UiAnims.HmMoon();
            Assert.AreEqual(0.55f, a.Sample(0f).Alpha, 1e-4f);
            Assert.AreEqual(0.96f, a.Sample(0f).Scale, 1e-4f);
            Assert.AreEqual(1f, a.Sample(3f).Alpha, 1e-4f);
            Assert.AreEqual(1.06f, a.Sample(3f).Scale, 1e-4f);
            Assert.AreEqual(0.55f, a.Sample(6f).Alpha, 1e-4f);
        }

        [Test]
        public void A_button_press_dips_to_96_percent_and_comes_back()
        {
            var a = UiAnims.Press();
            Assert.AreEqual(0.96f, a.Sample(0f).Scale, 1e-4f);
            Assert.AreEqual(1f, a.Sample(a.EndTime).Scale, 1e-4f);
            Assert.Less(a.EndTime, 0.25f, "a press never holds the player up");
        }

        [Test]
        public void The_ink_wipe_slides_in_in_225ms_changes_the_screen_at_235ms_and_leaves_in_225ms()
        {
            Assert.AreEqual(-1.2f, UiWipe.CurtainShare(0f), 1e-4f);
            Assert.AreEqual(-1.2f + 1.2f * 0.07080f, UiWipe.CurtainShare(0.225f * 0.25f), Tol);
            Assert.AreEqual(-0.6f, UiWipe.CurtainShare(0.225f * 0.5f), Tol);
            Assert.AreEqual(0f, UiWipe.CurtainShare(0.225f), 1e-4f, "covering the screen");
            Assert.AreEqual(0f, UiWipe.CurtainShare(0.23f), 1e-4f, "held for the 10 ms until the swap");
            Assert.AreEqual(0.6f, UiWipe.CurtainShare(0.235f + 0.225f * 0.5f), Tol);
            Assert.AreEqual(1.2f, UiWipe.CurtainShare(0.235f + 0.225f), 1e-4f, "off the right edge");
            Assert.IsTrue(UiWipe.Covering(0.23f));
            Assert.IsFalse(UiWipe.Covering(0.1f));
            Assert.IsFalse(UiWipe.Covering(0.3f));
            Assert.IsTrue(UiWipe.Finished(UiWipe.Total));
            Assert.Less(UiWipe.Total, 0.5f, "the whole wipe is under half a second");
        }

        [Test]
        public void The_petals_are_the_JS_petals_deterministic_per_index()
        {
            var f = new PetalField(26, 1600f, 900f, PetalField.Direction.Fall);
            Assert.AreEqual(26, f.Petals.Length);
            var p0 = f.Petals[0];
            Assert.AreEqual(1272f, p0.X0); Assert.AreEqual(-30f, p0.Y0); Assert.AreEqual(1468f, p0.X1); Assert.AreEqual(940f, p0.Y1);
            Assert.AreEqual(15.7f, p0.Duration, 1e-4f); Assert.AreEqual(-2.0f, p0.Delay, 1e-4f);
            Assert.AreEqual(1.25f, p0.Size, 1e-4f); Assert.AreEqual(2.4f, p0.SwayPeriod, 1e-4f); Assert.AreEqual(0.79f, p0.Opacity, 1e-4f);
            var p7 = f.Petals[7];
            Assert.AreEqual(602f, p7.X0); Assert.AreEqual(927f, p7.X1); Assert.AreEqual(18.9f, p7.Duration, 1e-4f); Assert.AreEqual(-17.4f, p7.Delay, 1e-4f);
            Assert.AreEqual(0.94f, p7.Opacity, 1e-4f);
            var p25 = f.Petals[25];
            Assert.AreEqual(652f, p25.X0); Assert.AreEqual(835f, p25.X1); Assert.AreEqual(19.6f, p25.Duration, 1e-4f); Assert.AreEqual(0.98f, p25.Opacity, 1e-4f);

            var w = new PetalField(16, 556f, 214f, PetalField.Direction.Wind);
            Assert.AreEqual(586f, w.Petals[0].X0); Assert.AreEqual(106f, w.Petals[0].Y0); Assert.AreEqual(-40f, w.Petals[0].X1); Assert.AreEqual(145f, w.Petals[0].Y1);
            Assert.AreEqual(6.7f, w.Petals[0].Duration, 1e-4f); Assert.AreEqual(0.61f, w.Petals[0].Opacity, 1e-4f);
            Assert.AreEqual(171f, w.Petals[9].Y0); Assert.AreEqual(262f, w.Petals[9].Y1); Assert.AreEqual(10.3f, w.Petals[9].Duration, 1e-4f);
        }

        [Test]
        public void A_petal_flies_in_a_straight_line_fading_in_and_out_and_sways_within_14px()
        {
            var f = new PetalField(26, 1600f, 900f, PetalField.Direction.Fall);
            // JS petal 0 at t = 3 s (the delay of -2 s puts it 5 s into its 15.7 s flight)
            var s = f.Sample(0, 3f);
            Assert.AreEqual(278.917f, s.Y, 0.05f);
            Assert.AreEqual(0.79f, s.Alpha, 1e-3f);
            Assert.AreEqual(1334.42f, s.X, 14.01f, "the leaf sways up to 14 px either side of its line");
            Assert.AreEqual(93.567f, f.Sample(0, 0f).Y, 0.05f);
            // petal 7 at 10 s
            Assert.AreEqual(406.243f, f.Sample(7, 10f).Y, 0.05f);
            Assert.AreEqual(0.94f, f.Sample(7, 10f).Alpha, 1e-3f);
            // every petal is invisible at the edges of its flight and never brighter than its own opacity
            for (int i = 0; i < f.Petals.Length; i++)
                for (int k = 0; k < 200; k++)
                {
                    var st = f.Sample(i, k * 0.37f);
                    Assert.LessOrEqual(st.Alpha, f.Petals[i].Opacity + 1e-4f);
                    Assert.GreaterOrEqual(st.Alpha, 0f);
                    Assert.LessOrEqual(st.TurnX, 1f + 1e-4f);
                }
        }

        [Test]
        public void A_cloud_drifts_from_720px_left_of_the_stage_to_1700px_right_forever()
        {
            var c = new CloudBand { Y = 150, Width = 560, Seconds = 140, Delay = -30, Opacity = 0.5f };
            Assert.AreEqual(-720f + 2420f * 30f / 140f, c.X(0f), 0.01f);
            Assert.AreEqual(-720f, c.X(110f), 0.01f, "the sweep restarts at the left edge");
            Assert.AreEqual(c.X(5f), c.X(145f), 0.01f, "periodic in its duration");
        }

        [Test]
        public void The_results_counters_count_up_with_an_ease_out_after_their_delay()
        {
            Assert.AreEqual(0, UiCount.Value(1000, 0.3f, 0.5f, 0.9f), "nothing before the delay");
            Assert.AreEqual(875, UiCount.Value(1000, 0.5f + 0.45f, 0.5f, 0.9f), "outCubic(0.5) = 0.875");
            Assert.AreEqual(1000, UiCount.Value(1000, 3f, 0.5f, 0.9f));
            Assert.AreEqual(0.875f, UiCount.Share(0.95f, 0.5f, 0.9f), 1e-4f);
        }

        [Test]
        public void A_focus_share_eases_toward_focused_and_back_in_its_own_time()
        {
            Assert.AreEqual(0.5f, UiFocusCurve.Step(0f, true, 0.1f, 0.2f), 1e-5f);
            Assert.AreEqual(1f, UiFocusCurve.Step(0.9f, true, 0.1f, 0.2f), 1e-5f, "never past 1");
            Assert.AreEqual(0.25f, UiFocusCurve.Step(0.75f, false, 0.1f, 0.2f), 1e-5f);
            Assert.AreEqual(0f, UiFocusCurve.Step(0.1f, false, 1f, 0.2f), 1e-5f);
            Assert.AreEqual(1f, UiFocusCurve.Step(0f, true, 0.001f, 0f), "zero duration snaps");
        }

        [Test]
        public void Skipping_or_reduced_motion_turns_every_animation_off()
        {
            try
            {
                UiClock.Reset();
                UiClock.ForceReduced = false;
                Assert.IsTrue(UiClock.Animate);
                UiClock.Skip = true;
                Assert.IsFalse(UiClock.Animate);
                UiClock.Skip = false;
                UiClock.ForceReduced = true;
                Assert.IsFalse(UiClock.Animate, "the OS reduced-motion setting is honoured like the JS prefers-reduced-motion");
            }
            finally { UiClock.Reset(); }
        }

        [Test]
        public void A_frozen_clock_is_stepped_by_hand_so_a_strip_of_frames_is_repeatable()
        {
            try
            {
                UiClock.Freeze(10f);
                Assert.AreEqual(10f, UiClock.Now);
                UiClock.Advance(0.25f);
                Assert.AreEqual(10.25f, UiClock.Now, 1e-5f);
            }
            finally { UiClock.Reset(); }
        }
    }
}
