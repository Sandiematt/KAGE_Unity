using System.Linq;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// The fight clock's world time scale (SAN-92, ADR-0002): which frames the slowed world advances on, how long it
    /// lasts, who is exempt, and that hitstop doesn't spend it. Plain frames, no fight.
    /// </summary>
    public class WorldTimeScaleTests
    {
        static bool[] Frames(FightClock c, int n) => Enumerable.Range(0, n).Select(_ => { c.BeginFrame(); c.EndFrame(); return c.WorldRuns; }).ToArray();

        [Test]
        public void At_scale_one_the_world_runs_every_frame_and_everyone_is_exempt()
        {
            var c = new FightClock(true);
            Assert.AreEqual(1f, c.WorldScale);
            Assert.IsTrue(Frames(c, 5).All(r => r));
            Assert.IsTrue(c.IsExempt(null));
            Assert.AreEqual(1f, c.ScaleFor(null));
        }

        [Test]
        public void A_slowed_world_advances_on_its_scale_of_the_frames_then_runs_normally()
        {
            var c = new FightClock(true);
            c.SlowWorld(0.4f, 24, null);
            Assert.AreEqual(24, c.WorldFramesLeft);
            var slow = Frames(c, 24);
            Assert.AreEqual(9, slow.Count(r => r), "24 x 0.4 = 9.6: 9 world frames");
            CollectionAssert.AreEqual(new[] { false, false, true, false, true }, slow.Take(5).ToArray(), "0.4, 0.8, 1.2, 0.6, 1.0");
            Assert.AreEqual(0, c.WorldFramesLeft);
            Assert.IsTrue(Frames(c, 3).All(r => r), "normal again");
            Assert.AreEqual(1f, c.WorldScale);
        }

        [Test]
        public void Play_mode_counts_the_world_time_scale_the_same_way()
        {
            var c = new FightClock(false);
            c.SlowWorld(0.5f, 4, null);
            CollectionAssert.AreEqual(new[] { false, true, false, true, true }, Frames(c, 5));
        }

        [Test]
        public void Zero_holds_the_world_until_it_resumes()
        {
            var c = new FightClock(true);
            c.SlowWorld(0f, -1, null);
            Assert.IsFalse(Frames(c, 50).Any(r => r));
            Assert.AreEqual(-1, c.WorldFramesLeft);
            c.ResumeWorld();
            Assert.IsTrue(Frames(c, 2).All(r => r));
        }

        [Test]
        public void Hitstop_frames_do_not_spend_the_slow_world()
        {
            var c = new FightClock(true);
            c.SlowWorld(0.5f, 10, null);
            c.Hitstop(5);
            for (int i = 0; i < 5; i++) { Assert.IsFalse(c.BeginFrame()); c.EndFrame(); }
            Assert.AreEqual(10, c.WorldFramesLeft);
        }

        [Test]
        public void Scale_one_or_zero_frames_is_no_slow_world()
        {
            var c = new FightClock(true);
            c.SlowWorld(1f, 30, null);
            Assert.AreEqual(1f, c.WorldScale);
            c.SlowWorld(0.4f, 0, null);
            Assert.AreEqual(1f, c.WorldScale);
        }
    }
}
