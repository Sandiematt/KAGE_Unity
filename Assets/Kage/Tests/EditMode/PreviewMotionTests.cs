using NUnit.Framework;

namespace Kage.Tests
{
    // The slow turn of the weapon illustration on the Armory's Weapons tab (JS startPreview). Expected numbers are the JS formulas evaluated
    // for a 1000 x 640 preview and a drawn length of 780.
    public class PreviewMotionTests
    {
        [Test]
        public void At_rest_the_blade_sits_at_minus_048_radians_unsquashed_left_of_centre()
        {
            var p = ArmoryPreviewMotion.At(0f, 780f);
            Assert.AreEqual(-0.48f, p.Angle, 1e-4f);
            Assert.AreEqual(1f, p.ScaleY, 1e-4f);
            Assert.AreEqual(209.420f, p.X, 0.01f);
            Assert.AreEqual(471.279f, p.Y, 0.01f);
        }

        [Test]
        public void The_blade_sways_bobs_and_turns_about_its_length_over_time()
        {
            var a = ArmoryPreviewMotion.At(2.5f, 780f);
            Assert.AreEqual(-0.41775f, a.Angle, 1e-4f);
            Assert.AreEqual(0.70544f, a.ScaleY, 1e-4f);
            Assert.AreEqual(200.573f, a.X, 0.01f);
            Assert.AreEqual(452.261f, a.Y, 0.01f);
            var b = ArmoryPreviewMotion.At(4f, 780f);
            Assert.AreEqual(-0.51540f, b.Angle, 1e-4f);
            Assert.AreEqual(0.51444f, b.ScaleY, 1e-4f);
            Assert.AreEqual(214.957f, b.X, 0.01f);
            Assert.AreEqual(476.168f, b.Y, 0.01f);
        }

        [Test]
        public void The_blade_never_swings_more_than_8_hundredths_of_a_radian_or_squashes_below_half()
        {
            for (int i = 0; i < 400; i++)
            {
                var p = ArmoryPreviewMotion.At(i * 0.173f, 492f);
                Assert.AreEqual(-0.48f, p.Angle, 0.0801f);
                Assert.GreaterOrEqual(p.ScaleY, 0.5f - 1e-4f);
                Assert.LessOrEqual(p.ScaleY, 1f + 1e-4f);
            }
        }

        [Test]
        public void A_weapon_is_drawn_4_point_1_times_its_length_but_never_wider_than_78_percent_of_the_preview()
        {
            Assert.AreEqual(492f, ArmoryPreviewMotion.DrawnLength(120), 0.01f);
            Assert.AreEqual(328f, ArmoryPreviewMotion.DrawnLength(0), 0.01f, "no length: the JS default of 80");
            Assert.AreEqual(780f, ArmoryPreviewMotion.DrawnLength(300), 0.01f);
        }
    }
}
