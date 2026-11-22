using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    // The JS brush banner (Effects.banner / drawBanner, 30-combat.js) as data (SAN-67 stage-result banner). The expected numbers come from
    // running the JS prng, band and brushStroke formulas in node, not from the code under test.
    public class InkBannerTests
    {
        const float Tol = 0.01f;

        [Test]
        public void The_prng_matches_the_JS_prng()
        {
            var r = new InkBanner.Prng(5);
            Assert.AreEqual(0.7497818277914924, r.Next(), 1e-12);
            Assert.AreEqual(0.5831796916123385, r.Next(), 1e-12);
            Assert.AreEqual(0.5010769285732307, r.Next(), 1e-12);
        }

        [Test]
        public void The_ink_band_is_the_JS_ragged_polygon()
        {
            var b = InkBanner.Band(640f, 5);
            Assert.AreEqual(45, b.Length);
            Assert.AreEqual(new Vector2(0f, 226f), b[0], "it starts on the top edge at the left");
            Assert.AreEqual(0f, b[1].x, Tol); Assert.AreEqual(228.9974f, b[1].y, Tol);
            Assert.AreEqual(128f, b[5].x, Tol); Assert.AreEqual(227.7506f, b[5].y, Tol);
            Assert.AreEqual(0f, b[b.Length - 1].x, Tol); Assert.AreEqual(346.9553f, b[b.Length - 1].y, Tol);
            Assert.AreEqual(224f, b[b.Length - 8].x, Tol); Assert.AreEqual(342.4429f, b[b.Length - 8].y, Tol);
            // the pointed end: out past the painted edge at the top, back at the edge near the bottom
            Assert.AreEqual(new Vector2(670f, 288f - 62f * 0.2f), b[22]);
            Assert.AreEqual(new Vector2(640f, 288f + 62f * 0.9f), b[23]);
        }

        [Test]
        public void A_wider_screen_starts_the_band_further_left_and_it_never_runs_unbounded()
        {
            var b = InkBanner.Band(2000f, 5, -300f);
            Assert.AreEqual(-300f, b[0].x);
            Assert.Less(b.Length, 410);
            var tiny = InkBanner.Band(0f, 5);
            Assert.Greater(tiny.Length, 3);
        }

        [Test]
        public void A_brush_stroke_tapers_like_the_JS_brushStroke()
        {
            var s = InkBanner.Stroke(40f, 226f, 600f, 220f, 5f, 6);
            Assert.AreEqual(34, s.Length);
            Assert.AreEqual(40.014f, s[0].x, Tol); Assert.AreEqual(227.1659f, s[0].y, Tol);
            Assert.AreEqual(320.029f, s[8].x, Tol); Assert.AreEqual(225.7529f, s[8].y, Tol);
            Assert.AreEqual(600f, s[16].x, Tol); Assert.AreEqual(220f, s[16].y, Tol);
            // the far side runs back from the tip
            Assert.AreEqual(600f, s[17].x, Tol); Assert.AreEqual(219.99999f, s[17].y, Tol);
            Assert.AreEqual(39.987f, s[33].x, Tol); Assert.AreEqual(224.7432f, s[33].y, Tol);
        }

        [Test]
        public void The_band_paints_in_over_a_third_of_a_second_holds_and_fades_over_the_last_22_percent()
        {
            Assert.AreEqual(0f, InkBanner.Painted(0f));
            Assert.AreEqual(1f - Mathf.Pow(0.5f, 3f), InkBanner.Painted(0.16f), 1e-4f, "out cubic: half way in time is 7/8 of the way across");
            Assert.AreEqual(1f, InkBanner.Painted(0.32f));
            Assert.AreEqual(1f, InkBanner.Painted(1.2f));
            Assert.AreEqual(0f, InkBanner.FadeOut(0.78f * InkBanner.Duration - 0.001f));
            Assert.AreEqual(0.5f, InkBanner.FadeOut((0.78f + 0.11f) * InkBanner.Duration), 1e-3f);
            Assert.AreEqual(1f, InkBanner.FadeOut(InkBanner.Duration), 1e-3f);
            Assert.AreEqual(-80f, InkBanner.Slide(0f));
            Assert.AreEqual(0f, InkBanner.Slide(1f));
        }

        [Test]
        public void The_calligraphy_is_the_JS_pick()
        {
            Assert.AreEqual("闘", InkBanner.GlyphFor("Stretch cleared"));
            Assert.AreEqual("波", InkBanner.GlyphFor("Wave 3"));
            Assert.AreEqual("壱", InkBanner.GlyphFor("Round I"));
            Assert.AreEqual("影", InkBanner.GlyphFor("Shadow Palm"));
            Assert.AreEqual("闘", InkBanner.GlyphFor(null));
        }

        [Test]
        public void The_banner_brush_uses_the_JS_fonts_and_has_the_glyphs_it_needs()
        {
            foreach (var f in new[] { UiFont.Kanji, UiFont.Marker, UiFont.Teko })
            {
                var font = Ui.FontOf(f);
                Assert.IsNotNull(font, f.ToString());
                Assert.AreNotEqual("LegacyRuntime", font.name, f + " is the JS font, not the built-in fallback");
            }
            var kanji = Ui.FontOf(UiFont.Kanji);
            kanji.RequestCharactersInTexture("闘敗", 40);
            foreach (char c in "闘敗") Assert.IsTrue(kanji.HasCharacter(c), "Yuji Boku has " + c);
        }
    }
}
