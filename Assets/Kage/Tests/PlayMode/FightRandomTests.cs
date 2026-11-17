using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>The seeded random source every gameplay roll goes through (ADR-0002).</summary>
    public class FightRandomTests
    {
        [Test]
        public void SameSeedGivesTheSameSequence()
        {
            var a = new FightRandom(61);
            var b = new FightRandom(61);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Value(), b.Value());
        }

        [Test]
        public void DifferentSeedsGiveDifferentSequences()
        {
            var a = new FightRandom(1);
            var b = new FightRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.Value() == b.Value()) same++;
            Assert.Less(same, 5);
        }

        [Test]
        public void ValuesStayInRange()
        {
            var r = new FightRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                float v = r.Value();
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
                int n = r.Range(3, 9);
                Assert.GreaterOrEqual(n, 3);
                Assert.Less(n, 9);
                float f = r.Range(-2f, 5f);
                Assert.GreaterOrEqual(f, -2f);
                Assert.LessOrEqual(f, 5f);
            }
        }

        [Test]
        public void KnownSeedGivesAKnownFirstValue()
        {
            // Pins the generator: a seed must give the same numbers on every platform and Unity version.
            // SplitMix64 of seed 0: first output 0xE220A8397B1DCDAF, top 24 bits 0xE220A8 / 2^24.
            Assert.AreEqual(0xE220A8 / 16777216f, new FightRandom(0).Value());
        }

        [Test]
        public void NamedStreamsAreStableAndIndependent()
        {
            var a = new FightRandom(61);
            var b = new FightRandom(61);
            // drawing from one stream doesn't shift another
            for (int i = 0; i < 10; i++) a.Stream("ai/ronin").Value();
            Assert.AreEqual(a.Stream("crit").Value(), b.Stream("crit").Value());
            // the same name returns the same stream, which keeps its position
            var crit = b.Stream("crit");
            Assert.AreSame(crit, b.Stream("crit"));
            // different names give different numbers
            Assert.AreNotEqual(new FightRandom(61).Stream("crit").Value(), new FightRandom(61).Stream("spawn").Value());
        }

        [Test]
        public void ChanceFollowsTheProbability()
        {
            var r = new FightRandom(3);
            int hits = 0;
            for (int i = 0; i < 10000; i++) if (r.Chance(0.1f)) hits++;
            Assert.That(hits, Is.InRange(850, 1150));
            Assert.IsFalse(new FightRandom(3).Chance(0f));
        }
    }
}
