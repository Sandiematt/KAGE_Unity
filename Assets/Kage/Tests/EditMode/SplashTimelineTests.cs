using NUnit.Framework;

namespace Kage.Tests
{
    // The JS opening splash (65-splash.js) as a timeline: K-A-G-E written stroke by stroke, each cut in by a slash, the final cut with its
    // shake, then the subtitle and the pulsing prompt. Expected numbers are the JS formulas evaluated for these times.
    public class SplashTimelineTests
    {
        const float Tol = 1e-3f;

        [Test]
        public void Each_letter_is_written_in_its_own_window_in_the_order_K_A_G_E()
        {
            Assert.AreEqual(4, SplashTimeline.Letters.Length);
            Assert.AreEqual("k", SplashTimeline.Letters[0].Key);
            Assert.AreEqual("e", SplashTimeline.Letters[3].Key);
            Assert.AreEqual(0f, SplashTimeline.LetterReveal(0, 0.3f), Tol, "nothing before its window");
            Assert.AreEqual(0.5f, SplashTimeline.LetterReveal(0, 0.68f), Tol);
            Assert.AreEqual(1f, SplashTimeline.LetterReveal(0, 0.86f), Tol, "the stroke takes 72% of the window");
            Assert.AreEqual(0f, SplashTimeline.LetterReveal(1, 0.9f), Tol, "A starts at 1.0 s");
            Assert.AreEqual(1f, SplashTimeline.LetterReveal(3, 2.2f), Tol);
        }

        [Test]
        public void The_dry_brush_front_travels_across_the_letter_along_its_stroke()
        {
            // K: stroke angle -1.05, its corners project to +-319.19 along the stroke
            Assert.AreEqual(269.395f, SplashTimeline.Front(0, 0.5f), 0.05f);
            Assert.Less(SplashTimeline.Front(0, 0.01f), -300f);
            Assert.Greater(SplashTimeline.Front(0, 1f), 355f, "past the far edge: the whole letter shows");
        }

        [Test]
        public void A_slash_runs_head_first_then_its_tail_catches_up_and_it_fades()
        {
            var at6 = SplashTimeline.Slash(0, 0.6f);
            Assert.AreEqual(0.875f, at6.Head, Tol);
            Assert.AreEqual(0.325f, at6.Tail, Tol);
            Assert.AreEqual(1f, at6.Alpha, Tol);
            var at8 = SplashTimeline.Slash(0, 0.8f);
            Assert.AreEqual(1f, at8.Head, Tol);
            Assert.AreEqual(0.7625f, at8.Tail, Tol);
            Assert.AreEqual(0.6875f, at8.Alpha, Tol);
            Assert.IsFalse(SplashTimeline.Slash(0, 0.3f).Visible, "not yet");
            Assert.IsFalse(SplashTimeline.Slash(0, 1.1f).Visible, "gone after its fade");
        }

        [Test]
        public void A_slash_has_23_points_on_its_line_between_tail_and_head()
        {
            var pts = SplashTimeline.SlashPoints(0, 0.875f, 0.325f);
            Assert.AreEqual(23, pts.Length);
            Assert.AreEqual(430f, pts[0].x, 1e-2f); Assert.AreEqual(654.5f, pts[0].y, 1e-2f);
            Assert.AreEqual(650f, pts[22].x, 1e-2f); Assert.AreEqual(357.5f, pts[22].y, 1e-2f);
        }

        [Test]
        public void The_final_cut_bumps_and_shakes_the_title_for_a_moment()
        {
            Assert.AreEqual(1f, SplashTimeline.Bump(2.1f), Tol);
            Assert.AreEqual(1.0225f, SplashTimeline.Bump(2.29f), Tol);
            Assert.AreEqual(1f, SplashTimeline.Bump(2.5f), Tol);
            Assert.AreEqual(7f, SplashTimeline.Shake(2.2f), Tol);
            Assert.AreEqual(3.5f, SplashTimeline.Shake(2.36f), Tol);
            Assert.AreEqual(0f, SplashTimeline.Shake(2.6f), Tol);
            Assert.AreEqual(0f, SplashTimeline.Shake(1f), Tol);
        }

        [Test]
        public void The_scene_fades_up_with_a_slow_push_in()
        {
            Assert.AreEqual(0.875f, SplashTimeline.SceneAlpha(0.3f), Tol);
            Assert.AreEqual(1f, SplashTimeline.SceneAlpha(2f), Tol);
            Assert.AreEqual(1.05f, SplashTimeline.SceneZoom(0f), Tol);
            Assert.AreEqual(1.00339f, SplashTimeline.SceneZoom(1f), Tol);
        }

        [Test]
        public void The_subtitle_and_the_prompt_come_after_the_cut_and_the_prompt_pulses()
        {
            Assert.AreEqual(0f, SplashTimeline.MarkAlpha(2.5f), Tol);
            Assert.AreEqual(0.875f, SplashTimeline.MarkAlpha(2.8f), Tol);
            Assert.AreEqual(1f, SplashTimeline.MarkAlpha(3.5f), Tol);
            Assert.AreEqual(0f, SplashTimeline.PromptAlpha(3.1f), Tol);
            Assert.AreEqual(0.6585f, SplashTimeline.PromptAlpha(3.65f), Tol);
            Assert.AreEqual(0.4491f, SplashTimeline.PromptAlpha(3.425f), Tol);
            for (float t = 3.65f; t < 20f; t += 0.13f)
            {
                float a = SplashTimeline.PromptAlpha(t);
                Assert.GreaterOrEqual(a, 0.24f - Tol);
                Assert.LessOrEqual(a, 1f + Tol);
            }
        }

        [Test]
        public void Reduced_motion_opens_straight_on_the_finished_card()
        {
            Assert.GreaterOrEqual(SplashTimeline.FinishedAt, SplashTimeline.PromptStart + 0.45f);
            Assert.AreEqual(1f, SplashTimeline.LetterReveal(3, SplashTimeline.FinishedAt), Tol);
            Assert.IsFalse(SplashTimeline.Slash(3, SplashTimeline.FinishedAt).Visible);
        }
    }
}
